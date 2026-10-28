using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class BroomFlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] InputActionReference broomAction;
    [SerializeField] InputActionReference ascendAction;
    [SerializeField] InputActionReference descendAction;
    [SerializeField] GameObject broomModel;
    [SerializeField] Animator playerAnimator;
    [SerializeField] string flyingBool = "Flying";

    [Header("Horizontal (WASD, relative to camera yaw)")]
    [SerializeField] float maxSpeed = 14f;
    [SerializeField] float acceleration = 20f;
    [SerializeField] float coastTime = 1.5f;
    [SerializeField] float turnSpeed = 10f;

    [Header("Vertical (Space / Ctrl)")]
    [SerializeField] float verticalSpeed = 8f;
    [SerializeField] bool strafeWhileCasting = true;   // casting turns the broom to face your aim (locked target or camera direction) while you keep moving
    [SerializeField, Range(0.05f, 1f)] float castSpeedMultiplier = 0.4f;   // speed while casting a spell (charging mana stops you completely instead)
    [SerializeField] float verticalAcceleration = 15f;
    [SerializeField] float verticalCoastTime = 0.4f;

    [Header("Boost (hold Shift)")]
    [SerializeField] float boostMultiplier = 1.8f;
    [SerializeField] float boostBlendSpeed = 4f;      // how fast the boost ramps up and down

    [Header("Height Limit")]
    [SerializeField] float maxHeightAboveGround = 40f;
    [SerializeField] float softLimitZone = 5f;        // start slowing the climb this far below the limit
    [SerializeField] LayerMask groundMask = ~0;       // exclude the Player layer
    [SerializeField] float groundCheckDistance = 500f;

    [Header("Boost FOV Kick")]
    [SerializeField] CinemachineCamera[] fovCameras;     // free camera + LockOnCam
    [SerializeField] float fovKick = 12f;                // extra degrees at full boost
    [SerializeField] float fovBlendSpeed = 2.5f;         // amount per second (1 = full kick)

    [Header("Mana (mounting is free)")]
    [SerializeField, Tooltip("Mana per second spent while flying.")]
    float flyDrain = 1.5f;
    [SerializeField, Tooltip("Extra mana per second spent while boosting.")]
    float boostDrain = 18f;
    [SerializeField, Range(0f, 1f), Tooltip("Natural mana regeneration while on the broom, as a share of normal. It slows down but never stops.")]
    float regenWhileFlying = 0.35f;
    [UnityEngine.Serialization.FormerlySerializedAs("boostRecoverThreshold")]
    [SerializeField, Tooltip("After running out of mana on the broom, the broom cannot be mounted (or boosted) again until the mana is back to this.")]
    float remountThreshold = 30f;

    [Header("Mount In Air (carry fall speed)")]
    [SerializeField] float fallBrakeTime = 0.7f;     // no input: seconds to stop the fall (higher = falls longer before hovering)
    [SerializeField] float redirectRate = 60f;       // direction key: fall speed converted into movement per second

    [Header("Dismount")]
    [SerializeField, Range(0f, 1f)] float dismountCarry = 0.5f;   // fraction of the glide speed handed over
    [SerializeField] float maxCarrySpeed = 10f;

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    public bool IsFlying { get; private set; }

    // read-only state for the HUD
    public bool IsExhausted => exhausted;
    public bool CanMount => !exhausted;                         // mounting costs nothing, but running dry locks the broom until some mana is back
    public float RemountThreshold => remountThreshold;
    public float ManaNow => mana != null ? mana.Current : 0f;
    public bool IsBoosting => IsFlying && boostFactor > 1.05f;
    public string MountKeyLabel =>
        broomAction != null && broomAction.action != null && broomAction.action.bindings.Count > 0
            ? broomAction.action.bindings[0].ToDisplayString()
            : "X";

    CharacterController controller;
    PlayerThirdPersonController tpc;
    StarterAssetsInputs input;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    int flyingHash;
    float boostFactor = 1f;
    float lastGroundY;
    bool exhausted;
    float fovAmount, prevFovAmount;
    float[] baseFov;

    void Log(string msg) { if (debugLogs) Debug.Log("[Broom] " + msg, this); }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        tpc = GetComponent<PlayerThirdPersonController>();
        input = GetComponent<StarterAssetsInputs>();
        flyingHash = Animator.StringToHash(flyingBool);
        mana = GetComponent<Mana>();
        baseFov = new float[fovCameras.Length];
        for (int i = 0; i < fovCameras.Length; i++)
            baseFov[i] = fovCameras[i].Lens.FieldOfView;

        if (mainCamera == null) mainCamera = Camera.main;
        if (broomModel != null) broomModel.SetActive(false);
    }

    void OnEnable()
    {
        broomAction.action.performed += OnBroomPressed;
        broomAction.action.Enable();
        ascendAction.action.Enable();
        descendAction.action.Enable();
    }

    void OnDisable()
    {
        broomAction.action.performed -= OnBroomPressed;
        if (mana != null) mana.SetRegenScale(this, 1f);
    }

    void OnBroomPressed(InputAction.CallbackContext ctx)
    {
        if (IsFlying) { Dismount(); return; }

        if (!CanMount)
        {
            Log("Broom locked until mana reaches " + remountThreshold.ToString("F0"));
            if (mana != null) mana.NotifyInsufficient();
            return;
        }

        Mount();
    }

    void Mount()
    {
        Log("MOUNT");
        // carry the fall speed into the broom (ignore the small constant push-down the controller keeps while grounded)
        float carried = tpc.Grounded ? 0f : Mathf.Min(0f, tpc.VerticalVelocity);
        tpc.ResetMotion();
        horizontalVelocity = Vector3.zero;
        verticalVelocity = carried;
        Log("Mounted with carried fall speed " + carried.ToString("F1"));
        IsFlying = true;
        tpc.IsFlying = true;
        horizontalVelocity = Vector3.zero;
        boostFactor = 1f;
        lastGroundY = transform.position.y;
        UpdateGroundY();
        input.jump = false;
        if (broomModel != null) broomModel.SetActive(true);
        SetAnimator(true);
    }

    public void Dismount()
    {
        Log("DISMOUNT");
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        boostFactor = 1f;
        Vector3 carry = Vector3.ClampMagnitude(horizontalVelocity * dismountCarry, maxCarrySpeed);

        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        boostFactor = 1f;
        tpc.ResetMotion();               // this clears CarryVelocity, so it must come first
        tpc.CarryVelocity = carry;       // horizontal only
        IsFlying = false;
        tpc.IsFlying = false;
        input.jump = false;
        if (broomModel != null) broomModel.SetActive(false);
        SetAnimator(false);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        
    }

    void SetAnimator(bool flying)
    {
        if (playerAnimator == null) return;
        foreach (var p in playerAnimator.parameters)
            if (p.nameHash == flyingHash) { playerAnimator.SetBool(flyingHash, flying); return; }
    }

    Mana mana;
    PlayerSpellCaster caster;

    void Update()
    {
        UpdateFov();

        if (!IsFlying) { UpdateManaUse(false); return; }

        // Space is also the jump key; clear it so it doesn't fire a jump after dismounting
        input.jump = false;

        // charging mana: no accelerating, boosting or climbing, the broom just coasts to a stop
        if (mana == null) mana = GetComponent<Mana>();
        bool charging = mana != null && mana.IsCharging;

        // casting a spell slows the broom but does not stop it
        if (caster == null) caster = GetComponentInChildren<PlayerSpellCaster>();
        bool casting = caster != null && caster.IsCasting;
        float castMul = casting ? castSpeedMultiplier : 1f;

        // ---------- horizontal: WASD, flat relative to camera yaw ----------
        Vector2 move = charging ? Vector2.zero : input.move;
        Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
        Vector3 wish = camForward * move.y + camRight * move.x;
        bool hasInput = move.sqrMagnitude > 0.01f;

        // direction key while falling: convert fall speed into movement along the input direction.
        // Only for a passive fall (e.g. mounting in mid-air); actively descending with the descend key must keep its speed.
        bool descendHeld = !charging && descendAction.action.IsPressed();
        if (hasInput && verticalVelocity < 0f && !descendHeld)
        {
            float amount = Mathf.Min(redirectRate * Time.deltaTime, -verticalVelocity);
            verticalVelocity += amount;
            horizontalVelocity += wish.normalized * amount;
        }

        // boost only counts while moving, and ramps smoothly
        bool canBoost = mana != null && mana.Current > 0f && !exhausted;
        bool boosting = input.sprint && hasInput && canBoost && !charging && !casting;
        float targetBoost = boosting ? boostMultiplier : 1f;
        boostFactor = Mathf.MoveTowards(boostFactor, targetBoost, boostBlendSpeed * Time.deltaTime);

        if (hasInput)
        {
            Vector3 target = wish.normalized * (maxSpeed * boostFactor * castMul);
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity, target, acceleration * boostFactor * Time.deltaTime);
        }
        else
        {
            float decel = maxSpeed / Mathf.Max(coastTime, 0.01f);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, decel * Time.deltaTime);
        }

        // ---------- vertical: Space up, Ctrl down ----------
        float vInput = charging ? 0f : (ascendAction.action.IsPressed() ? 1f : 0f)
             - (descendAction.action.IsPressed() ? 1f : 0f);

        // Space while falling: cancel the fall completely
        if (vInput > 0f && verticalVelocity < 0f)
            verticalVelocity = 0f;

        if (Mathf.Abs(vInput) > 0.01f)
        {
            verticalVelocity = Mathf.MoveTowards(
                verticalVelocity, vInput * verticalSpeed * castMul, verticalAcceleration * Time.deltaTime);
        }
        else
        {
            // falling brakes over fallBrakeTime, rising uses the normal short coast
            float coast = verticalVelocity < 0f ? fallBrakeTime : verticalCoastTime;
            float vDecel = Mathf.Abs(verticalVelocity) / Mathf.Max(coast, 0.01f);
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, vDecel * Time.deltaTime);
        }

        // ---------- height limit ----------
        UpdateGroundY();
        float ceiling = lastGroundY + maxHeightAboveGround;
        float remaining = ceiling - transform.position.y;

        // soft limit: the allowed climb speed shrinks to 0 as we near the ceiling
        float allowedUp = verticalSpeed * Mathf.Clamp01(remaining / Mathf.Max(softLimitZone, 0.01f));
        if (verticalVelocity > allowedUp) verticalVelocity = allowedUp;

        // above the ceiling (e.g. the ground dropped away): ease back down
        if (remaining < 0f)
            verticalVelocity = Mathf.Min(verticalVelocity, Mathf.Max(remaining * 2f, -verticalSpeed));

        UpdateManaUse(boosting);
        if (!IsFlying) return;   // the mana ran out and we dismounted this frame
        
        // ---------- move ----------
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);

        if (flags != CollisionFlags.None)
        {
            // slide along obstacles: keep only the velocity that actually happened
            Vector3 actual = controller.velocity;
            horizontalVelocity = new Vector3(actual.x, 0f, actual.z);
            verticalVelocity = actual.y;
        }

        RotateBody();
    }

    void RotateBody()
    {
        // casting: face the aim (locked target or camera direction) while still moving any direction, like strafing on foot
        if (strafeWhileCasting && caster != null && caster.TryGetFacingDirection(out Vector3 faceDir))
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir, Vector3.up), turnSpeed * Time.deltaTime);
            return;
        }

        // face the direction of travel, yaw only (height never tilts the body)
        if (horizontalVelocity.sqrMagnitude < 0.25f) return;

        Quaternion target = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void UpdateGroundY()
    {
        // if there is no ground below (flying over a void), keep the last known ground height
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                out RaycastHit hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
            lastGroundY = hit.point.y;
    }
    
    void UpdateFov()
    {
        float target = IsFlying ? Mathf.InverseLerp(1f, boostMultiplier, boostFactor) : 0f;
        fovAmount = Mathf.MoveTowards(fovAmount, target, fovBlendSpeed * Time.deltaTime);

        if (Mathf.Approximately(fovAmount, prevFovAmount)) return;   // don't touch the lens when idle
        prevFovAmount = fovAmount;

        for (int i = 0; i < fovCameras.Length; i++)
        {
            var lens = fovCameras[i].Lens;                  // LensSettings is a struct: copy, edit, assign back
            lens.FieldOfView = baseFov[i] + fovKick * fovAmount;
            fovCameras[i].Lens = lens;
        }
    }

    // mounting is free; flying (and boosting) spends mana while regeneration runs slowed down.
    // Running dry throws you off and locks the broom until the mana is back up to the remount threshold.
    void UpdateManaUse(bool boosting)
    {
        if (mana == null) mana = GetComponent<Mana>();
        if (mana == null) return;

        mana.SetRegenScale(this, IsFlying ? regenWhileFlying : 1f);

        if (IsFlying)
        {
            mana.Drain((flyDrain + (boosting ? boostDrain : 0f)) * Time.deltaTime);
            if (mana.Current <= 0.001f)
            {
                exhausted = true;
                Log("Out of mana, dismounting");
                Dismount();
            }
        }
        else if (exhausted && mana.Current >= remountThreshold)
        {
            exhausted = false;
        }
    }
}