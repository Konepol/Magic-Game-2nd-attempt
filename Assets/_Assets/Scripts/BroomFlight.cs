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

    // Max Speed above doubles as the cruise speed the broom holds by itself in jet flight.
    [Header("Jet Flight (W or boost starts it, S brakes back to a hover)")]
    [SerializeField, Tooltip("Off = the broom only hovers and strafes, like before.")]
    bool jetFlight = true;
    [SerializeField, Tooltip("Speed the broom settles at while holding W. It is approached gradually, not hit like a wall.")]
    float topSpeed = 45f;
    [SerializeField, Tooltip("Speed the broom settles at while boosting.")]
    float boostTopSpeed = 80f;
    [SerializeField, Tooltip("Speed lost per second while holding S.")]
    float brakeDecel = 40f;
    [SerializeField, Tooltip("Speed gained per second in a straight dive, and lost in a straight climb.")]
    float diveGain = 12f;
    [SerializeField, Tooltip("Degrees per second the flight direction swings round to the view at cruise speed or slower.")]
    float cruiseTurnRate = 240f;
    [SerializeField, Tooltip("The faster you fly the wider you turn, but never slower than this many degrees per second.")]
    float minTurnRate = 70f;
    [SerializeField, Range(0f, 1f), Tooltip("How far A/D and Space/Ctrl push the flight path off to the side of the view.")]
    float sideSlip = 0.6f;

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

    [SerializeField, Tooltip("The strong sidestep (double-tap A or D while W is NOT held). With W held the sidestep uses Dash Speed and Dash Duration above. It is on top of the flight, so it must be much faster than the flight to read as a dash.")]
    float sideDashSpeed = 70f;
    [SerializeField] float sideDashDuration = 0.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Extra sidestep speed as a share of the current flight speed, so a fast flight still gets a clear sidestep.")]
    float sideDashSpeedShare = 0.4f;

    [SerializeField, Tooltip("A sideways dash also spins the broom once around.")]
    bool dashRoll = true;
    [SerializeField] float dashRollTime = 0.4f;

    [Header("Free Aim (key in Options: look and aim anywhere while the broom keeps its course)")]
    [SerializeField, Tooltip("On = only while the key is held. Off = the key switches it on and off.")]
    bool freeAimHold = false;

    [Header("Height Limit")]
    [SerializeField] float flightCeiling = 150f;      // above the ground below you
    [SerializeField] float ceilingSoftZone = 25f;     // start slowing the climb this far below the limit
    [SerializeField] LayerMask groundMask = ~0;       // exclude the Player layer
    [SerializeField] float groundCheckDistance = 500f;

    [Header("Sense Of Speed (grows from cruise speed up to the boost top speed)")]
    [SerializeField] CinemachineCamera[] fovCameras;     // free camera + LockOnCam
    [SerializeField] float fovKickAtTopSpeed = 20f;      // extra degrees of view
    [SerializeField] float fovBlendSpeed = 2.5f;         // amount per second (1 = full kick)
    [SerializeField, Tooltip("Extra distance the follow camera drops back.")]
    float cameraPullBack = 1.5f;
    [SerializeField, Range(0f, 1f), Tooltip("Camera rumble at the boost top speed.")]
    float speedShake = 0.3f;

    [Header("Flip Indicator (small horizon beside the crosshair)")]
    [SerializeField] bool flipIndicator = true;

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

    [Header("Broom Effects (sparkle trail, dash burst, rider lean: see BroomFX)")]
    [SerializeField] bool broomEffects = true;

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
    float dashTimer, dashCooldownLeft, dashSpeedNow, dashExitSpeed, dashLength, sideSpeedNow;
    Vector3 dashDir;
    bool cruising;                 // jet flight (true) or hovering (false)
    bool dashSide;                 // the current dash is a sidestep: it is added on top of the flight instead of replacing it
    float rollSign, rollTime = 100f;
    InputAction freeAimAction;
    bool freeAim;
    Quaternion heldFrame = Quaternion.identity;   // the course kept while free aiming

    // free aim: the view (and so the spell aim) turns freely while the broom keeps the course it had
    public bool IsFreeAim => IsFlying && freeAim;
    public Vector3 CourseDirection => velocity.sqrMagnitude > 1f ? velocity.normalized : heldFrame * Vector3.forward;
    public float SpeedFraction => SpeedAmount;
    public int DashCount { get; private set; }     // goes up by one each dash, so effects can react to it
    public Transform BroomModel => broomModel != null ? broomModel.transform : null;
    CameraZoom cameraZoom;

    public float Speed => IsFlying ? velocity.magnitude : 0f;
    // 0 at cruise speed or slower, 1 at the boost top speed
    float SpeedAmount => IsFlying ? Mathf.InverseLerp(maxSpeed, Mathf.Max(boostTopSpeed, maxSpeed + 1f), velocity.magnitude) : 0f;

    public bool IsDashing => dashTimer > 0f;
    public Vector3 DashDirection => dashDir;

    // double-tap a direction (press, release, press within the window) to dash along the current input;
    // other direction keys may stay held the whole time
    // The dash goes exactly the way of the key that was double-tapped, whatever else is held: A/D dash straight
    // sideways even while W is down.
    void UpdateDash(Vector2 move, Quaternion look, bool blocked)
    {
        dashCooldownLeft -= Time.deltaTime;
        int tapped = -1;
        for (int d = 0; d < 4; d++)
        {
            float axis = d == 0 ? move.y : d == 1 ? move.x : d == 2 ? -move.y : -move.x;
            bool held = axis > 0.5f;
            bool pressed = held && !prevHeld[d];
            prevHeld[d] = held;
            if (!pressed) continue;

            if (Time.time - lastTapTimes[d] <= doubleTapWindow) { tapped = d; lastTapTimes[d] = -10f; }
            else lastTapTimes[d] = Time.time;
        }
        if (tapped < 0 || blocked || dashCooldownLeft > 0f) return;

        if (mana == null || mana.Current <= dashManaCost + 0.5f)
        {
            if (mana != null) mana.NotifyInsufficient();
            return;
        }

        mana.Drain(dashManaCost);
        dashDir = look * (tapped == 0 ? Vector3.forward : tapped == 1 ? Vector3.right : tapped == 2 ? Vector3.back : Vector3.left);
        dashSide = tapped == 1 || tapped == 3;
        if (dashSide && dashRoll)
        {
            rollSign = tapped == 1 ? -1f : 1f;   // rolling right is negative Z
            rollTime = 0f;
        }
        // holding W while sidestepping keeps the short, light sidestep; without W it is the strong one
        bool strong = dashSide && move.y <= 0.1f;
        dashLength = strong ? sideDashDuration : dashDuration;
        sideSpeedNow = strong ? sideDashSpeed + velocity.magnitude * sideDashSpeedShare : dashSpeed;
        dashTimer = dashLength;
        dashCooldownLeft = dashCooldown;
        DashCount++;
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
        cameraZoom = FindAnyObjectByType<CameraZoom>();   // it owns the camera distance, so the pull-back goes through it

        if (mainCamera == null) mainCamera = Camera.main;
        if (broomModel != null) broomModel.SetActive(false);

        if (speedLines) lines = SpeedLines.Create();

        freeAimAction = new InputAction("FreeAim", InputActionType.Button, PlayerSettings.GetPath("freeAim"));
        if (broomEffects) gameObject.AddComponent<BroomFX>();
    }

    void Start()
    {
        if (flipIndicator && tpc.LockOn != null && tpc.LockOn.Indicator != null)
            FlipIndicator.Create(this, tpc.LockOn, mainCamera);
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
        lines.SetState(Mathf.InverseLerp(maxSpeed * speedLinesStart, jetFlight ? topSpeed : dashSpeed, v.magnitude),
            v, mainCamera.transform);
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

    void ApplyKeyBinding()
    {
        PlayerSettings.Apply("broom", broomAction.action);
        PlayerSettings.Apply("freeAim", freeAimAction);
    }

    void OnEnable()
    {
        PlayerSettings.Changed += ApplyKeyBinding;
        ApplyKeyBinding();
        freeAimAction.Enable();
        broomAction.action.performed += OnBroomPressed;
        broomAction.action.Enable();
        ascendAction.action.Enable();
        descendAction.action.Enable();
    }

    void OnDisable()
    {
        PlayerSettings.Changed -= ApplyKeyBinding;
        freeAimAction.Disable();
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
        cruising = false;   // always start in a hover
        freeAim = false;
        rollTime = 100f;
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
        bool locked = tpc.LockOn != null && tpc.LockOn.IsLocked;

        // free aim: from the moment it is switched on, the controls follow the course the broom had instead of the view
        bool wasFreeAim = freeAim;
        if (locked) freeAim = false;
        else if (freeAimHold) freeAim = freeAimAction.IsPressed();
        else if (freeAimAction.WasPressedThisFrame()) freeAim = !freeAim;
        if (freeAim && !wasFreeAim)
        {
            Vector3 course = velocity.sqrMagnitude > 4f ? velocity.normalized : look * Vector3.forward;
            Vector3 up = look * Vector3.up;
            if (Mathf.Abs(Vector3.Dot(course, up)) > 0.999f) up = SafeUp(course, look * Vector3.forward);
            heldFrame = Quaternion.LookRotation(course, up);
        }
        if (freeAim) look = heldFrame;

        Vector3 lookForward = look * Vector3.forward;
        Vector3 lookUp = look * Vector3.up;
        Vector2 move = charging ? Vector2.zero : input.move;
        float lift = charging ? 0f : (ascendAction.action.IsPressed() ? 1f : 0f) - (descendAction.action.IsPressed() ? 1f : 0f);
        Vector3 wish = lookForward * move.y + look * Vector3.right * move.x + lookUp * lift;
        bool hasInput = wish.sqrMagnitude > 0.01f;
        Vector3 wishDir = hasInput ? wish.normalized : lookForward;

        // ---------- jet or hover ----------
        // W (or boost) puts the broom into jet flight: it keeps flying forward by itself until you brake to a stop with S.
        // Stopped, locked on or charging mana, it hovers and strafes instead.
        bool canBoost = mana != null && mana.Current > 0f && !exhausted;
        bool wantBoost = input.sprint && canBoost && !charging && !casting;
        if (!jetFlight || locked || charging) cruising = false;
        else if (move.y > 0.1f || wantBoost) cruising = true;

        // boost ramps smoothly; when hovering it only counts while moving
        bool boosting = wantBoost && (cruising || hasInput);
        float targetBoost = boosting ? boostMultiplier : 1f;
        boostFactor = Mathf.MoveTowards(boostFactor, targetBoost, boostBlendSpeed * Time.deltaTime);

        float speedBefore = velocity.magnitude;
        bool wasDashing = dashTimer > 0f;
        UpdateDash(move, look, charging);

        // a sideways dash is a sidestep: a short burst straight to the side, added on top of the flight, which carries on
        // underneath exactly as it was. Forward and backward dashes take over the flight for their moment instead.
        Vector3 sideVelocity = Vector3.zero;
        if (dashTimer > 0f && dashSide)
        {
            float t = 1f - dashTimer / Mathf.Max(dashLength, 0.01f);
            sideVelocity = dashDir * (sideSpeedNow * Mathf.Lerp(1f, 0.3f, t * t));
            dashTimer -= Time.deltaTime;
        }

        if (dashTimer > 0f && !dashSide)
        {
            if (!wasDashing)   // a dash is always a clear burst, however fast you were already going
            {
                dashSpeedNow = Mathf.Max(dashSpeed, speedBefore * 1.25f);
                dashExitSpeed = Mathf.Max(speedBefore, maxSpeed * dashExitMultiplier);
            }
            velocity = dashDir * dashSpeedNow;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)   // leave the dash faster than normal flight; the extra speed bleeds off below
                velocity = dashDir * dashExitSpeed;
        }
        else if (cruising)
        {
            FlyJet(look, move, lift, castMul);
        }
        else
        {
            float speed = velocity.magnitude;
            float cap = maxSpeed * boostFactor * castMul;
            bool overspeed = speed > cap + 0.01f;
            // extra speed fades slowly, but coming out of jet flight far too fast it is shed quickly
            float ratio = Mathf.Max(1f, speed / Mathf.Max(2f * cap, 0.01f));
            float fade = Mathf.Min(overspeedDecay * ratio * ratio, brakeDecel);

            if (hasInput && overspeed)
            {
                // steer toward the input while keeping the speed
                // (this is also what turns a carried fall into forward flight)
                Vector3 steered = Vector3.MoveTowards(velocity, wishDir * speed, acceleration * boostFactor * Time.deltaTime);
                velocity = steered.normalized * Mathf.MoveTowards(speed, cap, fade * Time.deltaTime);
            }
            else if (hasInput)
            {
                velocity = Vector3.MoveTowards(velocity, wishDir * cap, acceleration * boostFactor * Time.deltaTime);
            }
            else
            {
                float decel = overspeed ? fade : maxSpeed / Mathf.Max(coastTime, 0.01f);
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, decel * Time.deltaTime);
            }
        }

        // ---------- height limit ----------
        UpdateGroundY();
        float ceiling = lastGroundY + flightCeiling;
        float remaining = ceiling - transform.position.y;

        // soft limit: the allowed climb speed shrinks to 0 as we near the ceiling
        float allowedUp = Mathf.Max(boostTopSpeed, dashSpeed) * Mathf.Clamp01(remaining / Mathf.Max(ceilingSoftZone, 0.01f));
        if (velocity.y > allowedUp) velocity.y = allowedUp;

        // above the ceiling (e.g. the ground dropped away): ease back down
        if (remaining < 0f)
            velocity.y = Mathf.Min(velocity.y, Mathf.Max(remaining * 2f, -maxSpeed));

        UpdateManaUse(boosting);
        if (!IsFlying) return;   // the mana ran out and we dismounted this frame

        // ---------- move ----------
        CollisionFlags flags = controller.Move((velocity + sideVelocity) * Time.deltaTime);

        // slide along obstacles: keep only the velocity that actually happened (the sidestep is not part of the flight)
        if (flags != CollisionFlags.None)
        {
            Vector3 actual = controller.velocity;
            velocity = sideVelocity == Vector3.zero ? actual
                : actual - Vector3.Project(actual, dashDir) + Vector3.Project(velocity, dashDir);
        }

        RotateBody(lookForward, lookUp);
    }

    // Jet flight: the broom always moves along its own flight direction. The view turns freely and the flight
    // direction swings round to it, quickly when slow and in wide arcs when fast. There is no speed cap: thrust
    // fights drag that grows with speed, so it settles near the top speed instead of hitting a wall.
    void FlyJet(Quaternion look, Vector2 move, float lift, float castMul)
    {
        float dt = Time.deltaTime;
        float speed = velocity.magnitude;

        // A/D and Space/Ctrl push the flight path off to the side of the view
        Vector3 steer = (look * new Vector3(move.x * sideSlip, lift * sideSlip, 1f)).normalized;
        Vector3 dir = speed > 0.5f ? velocity / speed : steer;
        float turnRate = Mathf.Max(minTurnRate, cruiseTurnRate * maxSpeed / Mathf.Max(speed, maxSpeed));
        dir = Vector3.RotateTowards(dir, steer, turnRate * Mathf.Deg2Rad * dt, 0f);

        float cruise = maxSpeed * castMul;
        float boostAmount = Mathf.InverseLerp(1f, boostMultiplier, boostFactor);
        bool braking = move.y < -0.1f;

        if (braking)
        {
            speed = Mathf.MoveTowards(speed, 0f, brakeDecel * dt);
            if (speed < 1f) cruising = false;   // stopped: back to hovering
        }
        else if (move.y > 0.1f || boostAmount > 0f)
        {
            float terminal = Mathf.Lerp(topSpeed, boostTopSpeed, boostAmount) * castMul;
            float r = speed / Mathf.Max(terminal, 0.01f);
            speed += Mathf.Max(acceleration * boostFactor * (1f - r * r), -brakeDecel) * dt;
        }
        else if (speed < cruise)
        {
            speed = Mathf.MoveTowards(speed, cruise, acceleration * dt);
        }
        else
        {
            float r = speed / Mathf.Max(cruise, 0.01f);
            speed = Mathf.MoveTowards(speed, cruise, Mathf.Min(overspeedDecay * r * r, brakeDecel) * dt);
        }

        // diving adds speed, climbing costs it
        if (!braking) speed = Mathf.Max(0f, speed - dir.y * diveGain * dt);

        velocity = dir * speed;
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

        // a sideways dash spins the broom once around, easing in and out
        float roll = 0f;
        if (rollTime < dashRollTime)
        {
            rollTime += Time.deltaTime;
            roll = rollSign * 360f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rollTime / Mathf.Max(dashRollTime, 0.01f)));
        }

        // hovering: a slow, gentle sway so the broom never looks frozen in the air
        float sway = 1f - Mathf.Clamp01(velocity.magnitude / Mathf.Max(maxSpeed, 0.01f));
        Quaternion idle = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f) * 1.5f * sway, 0f, Mathf.Sin(Time.time * 1.1f) * 2.5f * sway);

        transform.rotation = bodyRot * idle * Quaternion.Euler(0f, 0f, bank + roll);
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
        // driven by real speed; without jet flight, by the boost and the dash like before
        float target = !IsFlying ? 0f
            : jetFlight ? SpeedAmount
            : Mathf.Max(Mathf.InverseLerp(1f, boostMultiplier, boostFactor), dashTimer > 0f ? 1f : 0f);
        fovAmount = Mathf.MoveTowards(fovAmount, target, fovBlendSpeed * Time.deltaTime);

        if (fovAmount > 0f && speedShake > 0f) CameraShake.Hold(speedShake * fovAmount);

        if (Mathf.Approximately(fovAmount, prevFovAmount)) return;   // don't touch the lens when idle
        prevFovAmount = fovAmount;

        for (int i = 0; i < fovCameras.Length; i++)
        {
            var lens = fovCameras[i].Lens;                  // LensSettings is a struct: copy, edit, assign back
            lens.FieldOfView = baseFov[i] + fovKickAtTopSpeed * fovAmount;
            fovCameras[i].Lens = lens;
        }
        if (cameraZoom != null) cameraZoom.ExtraDistance = cameraPullBack * fovAmount;
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