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

    // The broom flies where you look: W/S along the view, A/D sideways, Space/Ctrl along the view's own up and down.
    // The view has no up/down limit (see PlayerThirdPersonController), so pulling up keeps going into a loop.
    [Header("Flight (WASD + Space / Ctrl, relative to where you look)")]
    [SerializeField] float maxSpeed = 14f;
    [SerializeField] float acceleration = 20f;
    [SerializeField] float coastTime = 1.5f;
    [SerializeField] float turnSpeed = 10f;
    [SerializeField] bool strafeWhileCasting = true;   // casting turns the broom to face your aim (locked target or camera direction) while you keep moving
    [SerializeField, Range(0.05f, 1f)] float castSpeedMultiplier = 0.4f;   // speed while casting a spell (charging mana stops you completely instead)

    [Header("Boost (hold Shift)")]
    [SerializeField] float boostMultiplier = 1.8f;
    [SerializeField] float boostBlendSpeed = 4f;      // how fast the boost ramps up and down

    [Header("Quick Dash (double-tap a direction)")]
    [SerializeField] float dashSpeed = 32f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.7f;
    [SerializeField, Tooltip("Speed right after the dash, as a multiple of normal flight speed (1.5 = 50% faster).")]
    float dashExitMultiplier = 1.5f;
    [SerializeField, Tooltip("Units per second the extra speed fades at until it is back to normal.")]
    float overspeedDecay = 5f;
    [SerializeField, Tooltip("Max seconds between the two taps.")]
    float doubleTapWindow = 0.28f;

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
    float boostDrain = 8f;
    [SerializeField, Tooltip("Mana spent on each quick dash.")]
    float dashManaCost = 6f;
    [SerializeField, Range(0f, 1f), Tooltip("Natural mana regeneration while on the broom, as a share of normal. It slows down but never stops.")]
    float regenWhileFlying = 0.35f;
    [UnityEngine.Serialization.FormerlySerializedAs("boostRecoverThreshold")]
    [SerializeField, Tooltip("After running out of mana on the broom, the broom cannot be mounted (or boosted) again until the mana is back to this.")]
    float remountThreshold = 30f;

    [Header("Dismount")]
    [SerializeField, Range(0f, 1f)] float dismountCarry = 0.5f;   // fraction of the glide speed handed over
    [SerializeField] float maxCarrySpeed = 10f;

    [Header("Speed Lines")]
    [SerializeField] bool speedLines = true;
    [SerializeField, Tooltip("Lines start once you are this much faster than normal flight.")]
    float speedLinesStart = 1.15f;

    [Header("Bank (lean into turns)")]
    [SerializeField] float maxBank = 30f;
    [SerializeField, Tooltip("Degrees of roll per degree/second of turning.")]
    float bankPerYawRate = 0.25f;
    [SerializeField] float tiltSmoothTime = 0.15f;

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
        broomAction != null && broomAction.action != null ? PlayerSettings.LabelOf(broomAction.action) : "X";

    CharacterController controller;
    PlayerThirdPersonController tpc;
    StarterAssetsInputs input;
    Vector3 velocity;
    Quaternion bodyRot = Quaternion.identity;   // where the broom points, before the bank is added
    float bank, bankVel;
    int flyingHash;
    float boostFactor = 1f;
    float lastGroundY;
    bool exhausted;
    float fovAmount, prevFovAmount;
    float[] baseFov;
    SpeedLines lines;
    // each direction (0 up, 1 right, 2 down, 3 left) is tracked on its own, so tapping one while holding another still counts
    readonly bool[] prevHeld = new bool[4];
    readonly float[] lastTapTimes = { -10f, -10f, -10f, -10f };
    float dashTimer, dashCooldownLeft;
    Vector3 dashDir;

    public bool IsDashing => dashTimer > 0f;
    public Vector3 DashDirection => dashDir;

    // double-tap a direction (press, release, press within the window) to dash along the current input;
    // other direction keys may stay held the whole time
    void UpdateDash(Vector2 move, Vector3 wish, bool blocked)
    {
        dashCooldownLeft -= Time.deltaTime;
        bool isDouble = false;
        for (int d = 0; d < 4; d++)
        {
            float axis = d == 0 ? move.y : d == 1 ? move.x : d == 2 ? -move.y : -move.x;
            bool held = axis > 0.5f;
            bool pressed = held && !prevHeld[d];
            prevHeld[d] = held;
            if (!pressed) continue;

            if (Time.time - lastTapTimes[d] <= doubleTapWindow) { isDouble = true; lastTapTimes[d] = -10f; }
            else lastTapTimes[d] = Time.time;
        }
        if (!isDouble || blocked || dashCooldownLeft > 0f) return;

        if (mana == null || mana.Current <= dashManaCost + 0.5f)
        {
            if (mana != null) mana.NotifyInsufficient();
            return;
        }

        mana.Drain(dashManaCost);
        dashDir = wish.normalized;
        dashTimer = dashDuration;
        dashCooldownLeft = dashCooldown;
        Log("DASH");
    }

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

        if (speedLines) lines = SpeedLines.Create();
    }

    void OnDestroy()
    {
        if (lines != null) Destroy(lines.gameObject);
    }

    // streaks fade in above normal flight speed and peak at dash speed
    void UpdateSpeedLines()
    {
        if (lines == null) return;
        Vector3 v = IsFlying ? velocity : Vector3.zero;
        lines.SetState(Mathf.InverseLerp(maxSpeed * speedLinesStart, dashSpeed, v.magnitude),
            transform.position + Vector3.up, v);
    }

    void LateUpdate() => UpdateSpeedLines();

    // The frame the controls are relative to. Free flight: exactly where the view points, at any angle.
    // Locked on: the camera's direction kept flat, so W closes in on the target without diving.
    Quaternion LookFrame()
    {
        if (tpc.LockOn != null && tpc.LockOn.IsLocked)
        {
            Vector3 flat = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
            return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat, Vector3.up) : mainCamera.transform.rotation;
        }
        return tpc.CinemachineCameraTarget.transform.rotation;
    }

    void ApplyKeyBinding() => PlayerSettings.Apply("broom", broomAction.action);

    void OnEnable()
    {
        PlayerSettings.Changed += ApplyKeyBinding;
        ApplyKeyBinding();
        broomAction.action.performed += OnBroomPressed;
        broomAction.action.Enable();
        ascendAction.action.Enable();
        descendAction.action.Enable();
    }

    void OnDisable()
    {
        PlayerSettings.Changed -= ApplyKeyBinding;
        broomAction.action.performed -= OnBroomPressed;
        if (mana != null) mana.SetRegenScale(this, 1f);
    }

    void OnBroomPressed(InputAction.CallbackContext ctx)
    {
        if (IsFlying) { Dismount(); return; }

        if (GroundingAura.Active)
        {
            Log("Grounded: the broom will not carry you right now");
            return;
        }

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
        velocity = Vector3.up * carried;
        bodyRot = transform.rotation;
        bank = bankVel = 0f;
        Log("Mounted with carried fall speed " + carried.ToString("F1"));
        IsFlying = true;
        tpc.IsFlying = true;
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
        dashTimer = 0f;
        Vector3 carry = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z) * dismountCarry, maxCarrySpeed);

        // stand the body back upright, facing the way the broom was heading
        Vector3 heading = Vector3.ProjectOnPlane(bodyRot * Vector3.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f) heading = Vector3.ProjectOnPlane(bodyRot * Vector3.up, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f) heading = Vector3.forward;

        velocity = Vector3.zero;
        boostFactor = 1f;
        tpc.ResetMotion();               // this clears CarryVelocity, so it must come first
        tpc.CarryVelocity = carry;       // horizontal only
        IsFlying = false;
        tpc.IsFlying = false;
        input.jump = false;
        if (broomModel != null) broomModel.SetActive(false);
        SetAnimator(false);
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
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

        if (IsFlying && GroundingAura.Active) Dismount();       // the boss's grounding aura throws you off

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

        // ---------- input: everything is relative to where you look, in full 3D ----------
        Quaternion look = LookFrame();
        Vector3 lookForward = look * Vector3.forward;
        Vector3 lookUp = look * Vector3.up;
        Vector2 move = charging ? Vector2.zero : input.move;
        float lift = charging ? 0f : (ascendAction.action.IsPressed() ? 1f : 0f) - (descendAction.action.IsPressed() ? 1f : 0f);
        Vector3 wish = lookForward * move.y + look * Vector3.right * move.x + lookUp * lift;
        bool hasInput = wish.sqrMagnitude > 0.01f;
        Vector3 wishDir = hasInput ? wish.normalized : lookForward;

        // boost only counts while moving, and ramps smoothly
        bool canBoost = mana != null && mana.Current > 0f && !exhausted;
        bool boosting = input.sprint && hasInput && canBoost && !charging && !casting;
        float targetBoost = boosting ? boostMultiplier : 1f;
        boostFactor = Mathf.MoveTowards(boostFactor, targetBoost, boostBlendSpeed * Time.deltaTime);

        UpdateDash(move, wishDir, charging);
        if (dashTimer > 0f)
        {
            velocity = dashDir * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)   // leave the dash faster than normal flight; the extra speed bleeds off below
                velocity = dashDir * Mathf.Max(maxSpeed * dashExitMultiplier, maxSpeed * boostFactor * castMul);
        }
        else
        {
            float speed = velocity.magnitude;
            float cap = maxSpeed * boostFactor * castMul;
            bool overspeed = speed > cap + 0.01f;

            if (hasInput && overspeed)
            {
                // steer toward the input while keeping the speed, and let the extra speed fade slowly
                // (this is also what turns a carried fall into forward flight)
                Vector3 steered = Vector3.MoveTowards(velocity, wishDir * speed, acceleration * boostFactor * Time.deltaTime);
                velocity = steered.normalized * Mathf.MoveTowards(speed, cap, overspeedDecay * Time.deltaTime);
            }
            else if (hasInput)
            {
                velocity = Vector3.MoveTowards(velocity, wishDir * cap, acceleration * boostFactor * Time.deltaTime);
            }
            else
            {
                float decel = overspeed ? overspeedDecay : maxSpeed / Mathf.Max(coastTime, 0.01f);
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, decel * Time.deltaTime);
            }
        }

        // ---------- height limit ----------
        UpdateGroundY();
        float ceiling = lastGroundY + maxHeightAboveGround;
        float remaining = ceiling - transform.position.y;

        // soft limit: the allowed climb speed shrinks to 0 as we near the ceiling
        float allowedUp = maxSpeed * boostMultiplier * Mathf.Clamp01(remaining / Mathf.Max(softLimitZone, 0.01f));
        if (velocity.y > allowedUp) velocity.y = allowedUp;

        // above the ceiling (e.g. the ground dropped away): ease back down
        if (remaining < 0f)
            velocity.y = Mathf.Min(velocity.y, Mathf.Max(remaining * 2f, -maxSpeed));

        UpdateManaUse(boosting);
        if (!IsFlying) return;   // the mana ran out and we dismounted this frame

        // ---------- move ----------
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);

        // slide along obstacles: keep only the velocity that actually happened
        if (flags != CollisionFlags.None) velocity = controller.velocity;

        RotateBody(lookForward, lookUp);
    }

    // The whole body follows the view's frame, so going over the top really turns the rider upside down.
    // (The CharacterController's capsule always stays upright, whatever the transform does.)
    void RotateBody(Vector3 lookForward, Vector3 lookUp)
    {
        Vector3 forward;
        if (strafeWhileCasting && caster != null && caster.TryGetFacingDirection(out Vector3 faceDir))
        {
            // casting: face the aim (locked target or camera direction) while still moving any direction, like strafing on foot
            forward = faceDir;
        }
        else
        {
            // face the direction of travel; Space / Ctrl on their own lift the broom without turning its nose
            forward = Vector3.ProjectOnPlane(velocity, lookUp);
            if (forward.sqrMagnitude < 0.25f) forward = Vector3.ProjectOnPlane(bodyRot * Vector3.forward, lookUp);
        }
        if (forward.sqrMagnitude < 0.0001f) forward = lookForward;
        forward.Normalize();
        if (Mathf.Abs(Vector3.Dot(forward, lookUp)) > 0.999f) lookUp = SafeUp(forward, lookForward);

        Vector3 before = bodyRot * Vector3.forward;
        bodyRot = Quaternion.Slerp(bodyRot, Quaternion.LookRotation(forward, lookUp), turnSpeed * Time.deltaTime);

        // lean into the turn: turning right rolls right (negative Z). Only sideways turning counts, pulling up does not.
        float turnRate = Vector3.SignedAngle(Vector3.ProjectOnPlane(before, lookUp),
            Vector3.ProjectOnPlane(bodyRot * Vector3.forward, lookUp), lookUp) / Mathf.Max(Time.deltaTime, 0.0001f);
        bank = Mathf.SmoothDamp(bank, Mathf.Clamp(-turnRate * bankPerYawRate, -maxBank, maxBank), ref bankVel, tiltSmoothTime);

        transform.rotation = bodyRot * Quaternion.Euler(0f, 0f, bank);
    }

    // an up direction that is safe to use when the facing direction lines up with the view's up
    static Vector3 SafeUp(Vector3 forward, Vector3 lookForward)
    {
        Vector3 up = Vector3.ProjectOnPlane(-lookForward, forward);
        return up.sqrMagnitude > 0.0001f ? up : Vector3.ProjectOnPlane(Vector3.up, forward) + Vector3.right * 0.001f;
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
        float target = IsFlying ? Mathf.Max(Mathf.InverseLerp(1f, boostMultiplier, boostFactor), dashTimer > 0f ? 1f : 0f) : 0f;
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