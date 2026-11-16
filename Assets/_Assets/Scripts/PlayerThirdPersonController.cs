using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class PlayerThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [HideInInspector] public bool IsFlying;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        [HideInInspector] public Vector3 CarryVelocity;   // momentum handed over from the broom
        [Tooltip("Higher = the carried momentum fades faster")]
        public float CarryDamping = 3f;

        [Header("After Leaving The Broom")]
        [Tooltip("Higher = the momentum she leaves the broom with fades faster in the air (on the ground the usual damping applies)")]
        public float BroomFallAirDamping = 2.5f;
        [Tooltip("How much heavier gravity gets for each second of the fall that follows leaving the broom")]
        public float FallGravityRamp = 1.2f;
        [Tooltip("Gravity never gets heavier than this many times normal")]
        public float FallGravityMax = 3f;

        private Vector3 _appliedCarry;   // the carry used by the last move
        private bool _broomFall;
        private float _broomFallTime;

        // she stepped off the broom: she keeps her speed (horizontal carry and vertical speed), and a long fall drops faster and faster
        public void BeginBroomFall(Vector3 carry, float verticalSpeed)
        {
            GroundedCheck();   // the flag is stale after a flight
            CarryVelocity = carry;
            // the controller still reports the flight's velocity: that is not her walking speed, so it must not be read back as such
            _appliedCarry = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z);
            _verticalVelocity = verticalSpeed;
            _broomFall = true;
            _broomFallTime = 0f;
            ClearMagicState();
        }

        [Header("Lock On")]
        public TargetLockOn LockOn;

        // ======================= Ground movement rework (M1) =======================
        // The old Starter Assets walk, sprint and jump values above are kept for the old model (switch Use New Ground Motion off to get it back).
        [Header("Ground feel (M1 rework)")]
        [Tooltip("On: the snappy ground movement below. Off: the old Starter Assets feel.")]
        public bool UseNewGroundMotion = true;
        public float RunSpeed = 6.5f;
        public float SprintRunSpeed = 10.5f;
        [Tooltip("Metres per second gained each second on the ground while a direction is held (higher = snappier start).")]
        public float GroundAcceleration = 60f;
        [Tooltip("How fast she stops on the ground when no direction is held. Also what turns her around quickly when she reverses.")]
        public float GroundDeceleration = 80f;
        [Tooltip("Air control: how fast the held direction changes her speed in the air.")]
        public float AirAcceleration = 24f;
        public float AirDeceleration = 6f;
        [Tooltip("Seconds to turn to face the way she runs (smaller = snappier turn).")]
        public float TurnSmoothTime = 0.05f;
        [Tooltip("Seconds after stepping off a ledge when a jump still works.")]
        public float CoyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing still happens when she lands.")]
        public float JumpBufferTime = 0.14f;
        public float NewJumpHeight = 1.7f;
        public float NewGravity = -32f;
        [Tooltip("Gravity multiplier while falling (a quicker, weightier fall).")]
        public float FallGravityMultiplier = 1.35f;
        [Tooltip("Gravity multiplier while rising and the jump key is let go (a short hop).")]
        public float JumpCutMultiplier = 2.4f;

        [Header("Magic movement (ground)")]
        [Tooltip("Left Ctrl: a magic dash in the held direction (or the way she faces). Hold it after the dash to slide. Costs mana. With Veil Step's passive on, the dash is a blink instead.")]
        public float MagicDashSpeed = 26f;
        public float MagicDashTime = 0.18f;
        public float MagicDashCost = 6f;
        public float MagicDashCooldown = 0.6f;
        [Tooltip("The slide after a dash: speed lost per second while Left Ctrl is held.")]
        public float SlideFriction = 16f;
        [Tooltip("How far Veil Step's blink goes when it replaces the dash.")]
        public float BlinkDistance = 11f;
        [Tooltip("Extra jumps in the air (a burst of magic) and what each costs in mana.")]
        public int AirJumps = 1;
        public float AirJumpHeight = 1.5f;
        public float AirJumpCost = 8f;
        [Tooltip("Tap jump in the air against a wall to climb it with magic: speed, mana per second, how far she reaches for the wall, how high it goes.")]
        public float ClimbSpeed = 5.5f;
        public float ClimbCostPerSecond = 7f;
        public float ClimbReach = 0.75f;
        public float MaxClimbHeight = 5f;

        // state of the new movement
        Vector3 _groundVel;
        bool _prevJumpHeld;
        float _coyote, _jumpBuffer, _fallingSince;
        int _airJumpsLeft;
        float _dashTimer, _dashCooldown, _slideTime;
        Vector3 _dashDir;
        bool _sliding, _climbing, _airDashUsed;
        float _climbStartY, _climbBlockedUntil;
        float _airJumpFx;
        System.Collections.Generic.HashSet<int> _animParams;

        public Vector3 GroundVelocity => _groundVel;
        public bool IsMagicDashing => _dashTimer > 0f;
        public bool IsSliding => _sliding;
        public bool IsClimbing => _climbing;
        public bool JustAirJumped => Time.time < _airJumpFx;
        public bool IsNewMotion => UseNewGroundMotion;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        public float VerticalVelocity => _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private Mana _mana;
        private PlayerSpellCaster _caster;
        private float _lockFaceVelocity;

        [Tooltip("While casting, the character faces where they are aiming (the locked target, or where the camera points) and moves around it (strafing). Otherwise they face the direction they walk.")]
        public bool StrafeWhileCasting = true;

        // true while casting: the direction to face instead of the walking direction
        private bool TryGetFacing(out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!StrafeWhileCasting) return false;
            if (_caster == null) _caster = GetComponentInChildren<PlayerSpellCaster>();
            return _caster != null && _caster.TryGetFacingDirection(out direction);
        }

        [Tooltip("Walking speed multiplier while casting a Slow spell (Stand spells stop you completely).")]
        [Range(0.05f, 1f)] public float SlowCastSpeedMultiplier = 0.5f;

        private bool Casting
        {
            get
            {
                if (_caster == null) _caster = GetComponentInChildren<PlayerSpellCaster>();
                return _caster != null && _caster.IsCasting;
            }
        }

        // casting a Stand spell: no walking, turning to move, or jumping
        private bool Rooted => _caster != null && _caster.ActiveCastMovement == CastMovement.Stand;

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();
            if (GetComponent<GroundMotionPose>() == null) gameObject.AddComponent<GroundMotionPose>();      // placeholder lean for the new ground movement
            if (_hasAnimator)
            {
                _animParams = new System.Collections.Generic.HashSet<int>();
                foreach (var p in _animator.parameters) _animParams.Add(p.nameHash);
            }

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            if (IsFlying) { if (_groundVel != Vector3.zero || _climbing || _sliding) ClearMagicState(); return; }   // BroomFlight controls movement

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        [Header("Broom Free Look (no up/down limit while flying)")]
        [Tooltip("How strongly the horizon is pulled level while flying. 0 = never levels.")]
        public float FlightLevelStrength = 4f;

        [Tooltip("Fastest the view rolls while levelling, in degrees per second.")]
        public float FlightMaxRollSpeed = 200f;

        [Tooltip("Seconds of level upside-down flight before the view rolls upright again. 0 = stay upside down.")]
        public float FlightRightSelfAfter = 2f;

        [Tooltip("Seconds to straighten the view after getting off the broom.")]
        public float FlightExitBlendTime = 0.35f;

        private Quaternion _freeLook = Quaternion.identity;
        private Quaternion _exitOffset = Quaternion.identity;
        private bool _freeLookActive;
        private float _invertedTime;
        private Unity.Cinemachine.CinemachineBrain _brain;

        // on the broom the view is a free rotation instead of yaw + clamped pitch, so it can go over the top
        private void SwitchFreeLook(bool on, bool locked)
        {
            _freeLookActive = on;
            if (_brain == null) _brain = _mainCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            // the follow camera builds its rig around "up"; give it the view's own up so the rig stays put when upside down
            if (_brain != null) _brain.WorldUpOverride = on ? CinemachineCameraTarget.transform : null;

            if (on)
            {
                _freeLook = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
                _exitOffset = Quaternion.identity;
                _invertedTime = 0f;
                return;
            }

            // back to yaw + pitch: keep looking the same way, and blend out whatever roll is left
            Vector3 fwd = _freeLook * Vector3.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                Vector3 up = _freeLook * Vector3.up;
                flat = fwd.y > 0f ? new Vector3(-up.x, 0f, -up.z) : new Vector3(up.x, 0f, up.z);
            }
            _cinemachineTargetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            _cinemachineTargetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg, BottomClamp, TopClamp);
            if (!locked)
                _exitOffset = Quaternion.Inverse(Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f)) * _freeLook;
        }

        private void FreeLook()
        {
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                // turn around the view's own axes, so the mouse does the same thing on screen at any angle
                _freeLook *= Quaternion.Euler(_input.look.y * deltaTimeMultiplier, _input.look.x * deltaTimeMultiplier, 0f);
            }

            // keep the horizon level: right way up, or exactly upside down after going over the top
            Vector3 fwd = _freeLook * Vector3.forward;
            Vector3 up = _freeLook * Vector3.up;
            bool inverted = up.y < 0f;
            _invertedTime = inverted && Mathf.Abs(fwd.y) < 0.5f ? _invertedTime + Time.deltaTime : 0f;
            bool righting = inverted && FlightRightSelfAfter > 0f && _invertedTime >= FlightRightSelfAfter;

            Vector3 levelUp = Vector3.ProjectOnPlane(inverted && !righting ? Vector3.down : Vector3.up, fwd);
            float level = levelUp.magnitude;   // 1 flying level, 0 pointing straight up or down (no levelling there)
            if (FlightLevelStrength > 0f && level > 0.01f)
            {
                float error = Vector3.SignedAngle(up, levelUp, fwd);
                float step = Mathf.Clamp(error * FlightLevelStrength, -FlightMaxRollSpeed, FlightMaxRollSpeed) * level * Time.deltaTime;
                step = Mathf.Clamp(step, -Mathf.Abs(error), Mathf.Abs(error));
                _freeLook = Quaternion.AngleAxis(step, fwd) * _freeLook;
            }

            _freeLook.Normalize();
            CinemachineCameraTarget.transform.rotation = _freeLook;
        }

        // Map Bounds turns the view while it takes her back into the map, so she looks where she is being taken
        // and is already heading the right way when the controls come back.
        public void TurnViewTowards(Vector3 direction, float degreesPerSecond)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            float step = degreesPerSecond * Time.deltaTime;
            if (_freeLookActive)
                _freeLook = Quaternion.RotateTowards(_freeLook, Quaternion.LookRotation(direction, Vector3.up), step);
            else
                _cinemachineTargetYaw = Mathf.MoveTowardsAngle(_cinemachineTargetYaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, step);
        }

        private void CameraRotation()
        {
            bool locked = LockOn != null && LockOn.IsLocked;
            bool freeLook = IsFlying && !locked;
            if (freeLook != _freeLookActive) SwitchFreeLook(freeLook, locked);
            if (freeLook) { FreeLook(); return; }

            // while locked on: ignore look input and copy the live camera's view instead,
            // so the free camera is already in the right place when you unlock
            if (locked)
            {
                _cinemachineTargetYaw = _mainCamera.transform.eulerAngles.y;
                _cinemachineTargetPitch = Mathf.Clamp(
                    Mathf.DeltaAngle(0f, _mainCamera.transform.eulerAngles.x), BottomClamp, TopClamp);

                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                    _cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
                return;
            }
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // just got off the broom: ease out the leftover roll instead of snapping upright
            if (Quaternion.Angle(_exitOffset, Quaternion.identity) > 0.1f)
                _exitOffset = Quaternion.Slerp(_exitOffset, Quaternion.identity,
                    1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(FlightExitBlendTime, 0.01f)));
            else
                _exitOffset = Quaternion.identity;

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f) * _exitOffset;
        }

        // ---------- the new ground movement ----------

        static readonly int AnimDashing = Animator.StringToHash("Dashing");
        static readonly int AnimSliding = Animator.StringToHash("Sliding");
        static readonly int AnimClimbing = Animator.StringToHash("Climbing");
        static readonly int AnimAirJump = Animator.StringToHash("AirJump");

        // sets an optional animator parameter (a future locomotion set can add them; missing ones are skipped quietly)
        private void SetAnimBool(int id, bool value)
        {
            if (_hasAnimator && _animParams != null && _animParams.Contains(id)) _animator.SetBool(id, value);
        }

        private bool TrySpend(float cost)
        {
            if (cost <= 0f) return true;
            if (_mana == null) _mana = GetComponent<Mana>();
            if (_mana == null) return true;
            if (!_mana.CanAfford(cost)) { _mana.NotifyInsufficient(); return false; }
            _mana.Spend(cost);
            return true;
        }

        private void MoveNew()
        {
            float dt = Time.deltaTime;
            if (_mana == null) _mana = GetComponent<Mana>();
            bool rooted = Rooted;
            bool casting = Casting;
            bool sprinting = _input.sprint && (_mana == null || !_mana.IsCharging) && !casting;
            float targetSpeed = sprinting ? SprintRunSpeed : RunSpeed;
            if (casting && !rooted) targetSpeed *= SlowCastSpeedMultiplier;

            bool hasInput = _input.move != Vector2.zero && !rooted;
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
            Vector3 desired = Vector3.zero;
            if (hasInput)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
                float magnitude = _input.analogMovement ? _input.move.magnitude : 1f;
                desired = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward * (targetSpeed * magnitude);
            }

            bool controlled = _dashTimer <= 0f && !_sliding && !_climbing;
            if (controlled)
            {
                float rate = hasInput ? (Grounded ? GroundAcceleration : AirAcceleration) : (Grounded ? GroundDeceleration : AirDeceleration);
                _groundVel = Vector3.MoveTowards(_groundVel, desired, rate * dt);
            }

            UpdateMagicMove(dt, hasInput, desired);

            // face the way she runs, or the aim while casting (strafing)
            if (TryGetFacing(out Vector3 faceDir))
            {
                float yaw = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
                float faced = Mathf.SmoothDampAngle(transform.eulerAngles.y, yaw, ref _lockFaceVelocity, TurnSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, faced, 0.0f);
            }
            else if ((hasInput || _dashTimer > 0f || _sliding) && !rooted)
            {
                float face = (_dashTimer > 0f || _sliding) && _groundVel.sqrMagnitude > 0.01f
                    ? Mathf.Atan2(_groundVel.x, _groundVel.z) * Mathf.Rad2Deg : _targetRotation;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, face, ref _rotationVelocity, TurnSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            // the carried broom momentum fades (faster on the ground)
            float damping = Grounded ? CarryDamping * 2f : _broomFall ? BroomFallAirDamping : CarryDamping;
            CarryVelocity = Vector3.Lerp(CarryVelocity, Vector3.zero, 1f - Mathf.Exp(-damping * dt));

            _controller.Move(_groundVel * dt + new Vector3(0f, _verticalVelocity, 0f) * dt + CarryVelocity * dt);
            _appliedCarry = CarryVelocity;

            // running into something takes the speed away instead of storing it
            Vector3 achieved = _controller.velocity - CarryVelocity;
            achieved.y = 0f;
            if (achieved.sqrMagnitude < _groundVel.sqrMagnitude) _groundVel = Vector3.MoveTowards(_groundVel, achieved, 80f * dt);

            // the animator keeps the old Speed scale (idle 0, walk about 2, run about 4, sprint 5.3) so the current clips still blend
            float speed = _groundVel.magnitude;
            float anim = speed <= RunSpeed ? speed / Mathf.Max(RunSpeed, 0.01f) * 4f : 4f + (speed - RunSpeed) / Mathf.Max(SprintRunSpeed - RunSpeed, 0.01f) * 1.335f;
            _animationBlend = Mathf.Lerp(_animationBlend, anim, 1f - Mathf.Exp(-14f * dt));
            if (_animationBlend < 0.01f) _animationBlend = 0f;
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, _input.analogMovement ? Mathf.Max(0.3f, _input.move.magnitude) : 1f);
            }
            SetAnimBool(AnimDashing, _dashTimer > 0f);
            SetAnimBool(AnimSliding, _sliding);
            SetAnimBool(AnimClimbing, _climbing);
            SetAnimBool(AnimAirJump, Time.time < _airJumpFx);
        }

        // Left Ctrl: a magic dash, and a slide while it stays held. With Veil Step's passive on the dash is a blink.
        private void UpdateMagicMove(float dt, bool hasInput, Vector3 desired)
        {
            _dashCooldown -= dt;
            if (Grounded) _airDashUsed = false;

            var kb = Keyboard.current;
            bool ctrlPressed = kb != null && kb.leftCtrlKey.wasPressedThisFrame;
            bool ctrlHeld = kb != null && kb.leftCtrlKey.isPressed;

            if (ctrlPressed && _dashCooldown <= 0f && !Rooted && !_climbing && (Grounded || !_airDashUsed))
            {
                Vector3 dir = hasInput ? desired.normalized : transform.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
                dir.Normalize();

                if (_caster == null) _caster = GetComponentInChildren<PlayerSpellCaster>();
                if (_caster != null && _caster.TryUsePassive("dash_to_blink"))
                {
                    VeilStep.DashBlink(transform, dir, BlinkDistance, new Color(1f, 0.7f, 0.85f));
                    _dashCooldown = MagicDashCooldown;
                    if (!Grounded) _airDashUsed = true;
                }
                else if (TrySpend(MagicDashCost))
                {
                    _dashTimer = MagicDashTime;
                    _dashDir = dir;
                    _dashCooldown = MagicDashCooldown;
                    _sliding = false;
                    if (!Grounded) _airDashUsed = true;
                    SpellFX.Play(Sfx.Cast, transform.position, 0.5f);
                    SpellFX.Burst(transform.position + Vector3.up * 0.9f, 0.6f, new Color(0.7f, 0.85f, 1f), 10);
                    CameraShake.Add(0.06f);
                }
            }

            if (_dashTimer > 0f)
            {
                _dashTimer -= dt;
                _groundVel = _dashDir * MagicDashSpeed;
                if (!Grounded) _verticalVelocity = Mathf.Max(_verticalVelocity, 0f);       // an air dash goes level
                if (_dashTimer <= 0f)
                {
                    // the dash ends: she carries on as a slide if Ctrl is still held on the ground, otherwise she runs out of it
                    _sliding = ctrlHeld && Grounded;
                    _slideTime = 0f;
                    if (!_sliding) _groundVel = _dashDir * Mathf.Max(MagicDashSpeed * 0.45f, RunSpeed);
                }
            }
            else if (_sliding)
            {
                _slideTime += dt;
                float speed = _groundVel.magnitude;
                // a little steering while she slides, and the speed bleeds off
                if (hasInput) _groundVel = Vector3.RotateTowards(_groundVel, desired.normalized * speed, 1.6f * dt, 0f);
                speed = Mathf.MoveTowards(speed, 0f, SlideFriction * dt);
                _groundVel = _groundVel.normalized * speed;
                if (!ctrlHeld || !Grounded || speed < RunSpeed * 0.5f) _sliding = false;
            }
        }

        private void JumpAndGravityNew()
        {
            float dt = Time.deltaTime;
            bool jumpHeld = _input.jump;
            bool pressed = jumpHeld && !_prevJumpHeld;
            _prevJumpHeld = jumpHeld;
            _jumpBuffer = pressed ? JumpBufferTime : _jumpBuffer - dt;

            if (Grounded)
            {
                _coyote = CoyoteTime;
                _airJumpsLeft = AirJumps;
                _fallTimeoutDelta = FallTimeout;
                _climbing = false;
                if (_hasAnimator) { _animator.SetBool(_animIDJump, false); _animator.SetBool(_animIDFreeFall, false); }
                if (_broomFall && _verticalVelocity <= 0.0f) { _broomFall = false; _broomFallTime = 0f; }
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;
            }
            else
            {
                _coyote -= dt;
                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= dt;
                else if (_hasAnimator) _animator.SetBool(_animIDFreeFall, true);
            }

            // jump: from the ground, or just after leaving it, or a press that was made a moment before landing
            if (_jumpBuffer > 0f && _coyote > 0f && !Rooted)
            {
                _verticalVelocity = Mathf.Sqrt(NewJumpHeight * -2f * NewGravity);
                _jumpBuffer = 0f;
                _coyote = 0f;
                _sliding = false;
                if (_hasAnimator) _animator.SetBool(_animIDJump, true);
            }
            else if (pressed && !Grounded && _coyote <= 0f && !Rooted)
            {
                // in the air: against a wall it climbs, otherwise a burst of magic gives another jump
                if (!_climbing && Time.time >= _climbBlockedUntil && WallAhead(out _)) StartClimb();
                else if (!_climbing && _airJumpsLeft > 0 && TrySpend(AirJumpCost))
                {
                    _airJumpsLeft--;
                    _verticalVelocity = Mathf.Sqrt(AirJumpHeight * -2f * NewGravity);
                    _airJumpFx = Time.time + 0.4f;
                    SpellFX.Burst(transform.position + Vector3.up * 0.2f, 0.8f, new Color(0.75f, 0.85f, 1f), 14);
                    SpellFX.Play(Sfx.Ready, transform.position, 0.5f);
                    PetalBurst.Spawn(transform.position + Vector3.up * 0.4f, new Color(0.75f, 0.85f, 1f), false);
                }
            }

            if (_climbing) { UpdateClimb(jumpHeld, dt); return; }

            // gravity: heavier falling, and a short hop when the jump key is let go early
            float g = NewGravity;
            if (_verticalVelocity < 0f) g *= FallGravityMultiplier;
            else if (!jumpHeld) g *= JumpCutMultiplier;
            if (_broomFall && !Grounded)
            {
                _broomFallTime += dt;
                g *= Mathf.Min(1f + FallGravityRamp * _broomFallTime, FallGravityMax);
            }
            if (_verticalVelocity > -_terminalVelocity) _verticalVelocity += g * dt;
        }

        // a wall in front of her chest that she can climb (steep, not an enemy or the player)
        private bool WallAhead(out RaycastHit hit)
        {
            int mask = Physics.DefaultRaycastLayers & ~((1 << 2) | (1 << gameObject.layer) | (1 << LayerMask.NameToLayer("Enemy")));
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            if (Physics.Raycast(origin, transform.forward, out hit, ClimbReach, mask, QueryTriggerInteraction.Ignore))
                return Mathf.Abs(hit.normal.y) < 0.35f;
            return false;
        }

        private void StartClimb()
        {
            if (!TrySpend(2f)) return;                       // a small cost to start
            _climbing = true;
            _climbStartY = transform.position.y;
            _verticalVelocity = ClimbSpeed;
            _groundVel = Vector3.zero;
            _airJumpFx = Time.time + 0.3f;
            SpellFX.Burst(transform.position + Vector3.up + transform.forward * 0.4f, 0.5f, new Color(0.75f, 0.85f, 1f), 10);
        }

        // climbing: she goes up the wall while the jump key is held, spending mana; at the top she hauls herself over
        private void UpdateClimb(bool jumpHeld, float dt)
        {
            bool wall = WallAhead(out RaycastHit hit);
            bool head = Physics.Raycast(transform.position + Vector3.up * 2.0f, transform.forward, ClimbReach + 0.3f,
                Physics.DefaultRaycastLayers & ~((1 << 2) | (1 << gameObject.layer) | (1 << LayerMask.NameToLayer("Enemy"))), QueryTriggerInteraction.Ignore);

            bool paid = _mana == null || _mana.Drain(ClimbCostPerSecond * dt) >= ClimbCostPerSecond * dt - 0.0001f;
            bool tooHigh = transform.position.y - _climbStartY > MaxClimbHeight;

            if (!jumpHeld || !wall || !paid || tooHigh)
            {
                _climbing = false;
                _climbBlockedUntil = Time.time + 0.25f;
                // reached the top: the wall chest-high is gone while she is still pushing up, so she vaults over the edge
                if (jumpHeld && !wall && paid && !tooHigh)
                {
                    _verticalVelocity = 6.5f;
                    _groundVel = transform.forward * 4.5f;
                }
                return;
            }

            // up the wall, pressed against it
            _verticalVelocity = ClimbSpeed;
            _groundVel = -hit.normal * 0.6f;
            _groundVel.y = 0f;
            if (!head && transform.position.y - _climbStartY > 0.5f) { /* the top is near: keep going until the wall ends below the chest */ }
        }

        private void Move()
        {
            if (UseNewGroundMotion) { MoveNew(); return; }
            // set target speed based on move speed, sprint speed and if sprint is pressed
            // no sprinting while charging mana
            if (_mana == null) _mana = GetComponent<Mana>();
            bool sprinting = _input.sprint && (_mana == null || !_mana.IsCharging) && !Casting;
            float targetSpeed = sprinting ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            bool rooted = Rooted;
            if (Casting && !rooted) targetSpeed *= SlowCastSpeedMultiplier;   // Slow mode: keep moving, slower
            if (_input.move == Vector2.zero || rooted) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            // (the carried broom momentum is part of the controller's velocity but not of her own walking speed: left in, the speed
            // below would feed on it every frame and throw her far)
            Vector3 ownVelocity = _controller.velocity - _appliedCarry;
            float currentHorizontalSpeed = new Vector3(ownVelocity.x, 0.0f, ownVelocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero && !rooted)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position (when locked on, facing is handled below)
                if (!TryGetFacing(out _)) transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            // casting: face the aim (locked target or camera direction), whatever direction we are moving
            if (TryGetFacing(out Vector3 faceDir))
            {
                float yaw = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
                float faced = Mathf.SmoothDampAngle(transform.eulerAngles.y, yaw, ref _lockFaceVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, faced, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // fade the carried momentum (faster on the ground so you don't slide forever)
            float damping = Grounded ? CarryDamping * 2f : _broomFall ? BroomFallAirDamping : CarryDamping;
            CarryVelocity = Vector3.Lerp(CarryVelocity, Vector3.zero, 1f - Mathf.Exp(-damping * Time.deltaTime));

            // move the player: walking, falling and the momentum carried over from the broom, in one move
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime +
                             CarryVelocity * Time.deltaTime);
            _appliedCarry = CarryVelocity;

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (UseNewGroundMotion) { JumpAndGravityNew(); return; }
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // landed after leaving the broom: the fall is over (not while still hopping up off it)
                if (_broomFall && _verticalVelocity <= 0.0f) { _broomFall = false; _broomFallTime = 0f; }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f && !Rooted)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                float gravity = Gravity;
                if (_broomFall && !Grounded)
                {
                    _broomFallTime += Time.deltaTime;
                    gravity *= Mathf.Min(1f + FallGravityRamp * _broomFallTime, FallGravityMax);
                }
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume * PlayerProfile.EffectVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume * PlayerProfile.EffectVolume);
            }
        }

        public void ResetMotion()
        {
            _broomFall = false;
            _broomFallTime = 0f;
            CarryVelocity = Vector3.zero;
            _appliedCarry = Vector3.zero;
            _verticalVelocity = 0f;      // clears stored fall speed
            _speed = 0f;                 // clears stored ground speed
            _animationBlend = 0f;
            _fallTimeoutDelta = FallTimeout;
            _jumpTimeoutDelta = JumpTimeout;
            ClearMagicState();
        }

        private void ClearMagicState()
        {
            _groundVel = Vector3.zero;
            _dashTimer = 0f;
            _sliding = false;
            _climbing = false;
            _coyote = 0f;
            _jumpBuffer = 0f;
        }
    }
}