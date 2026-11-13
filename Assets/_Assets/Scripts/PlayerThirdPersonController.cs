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
        public float BroomFallAirDamping = 0.8f;
        [Tooltip("How much heavier gravity gets for each second of the fall that follows leaving the broom")]
        public float FallGravityRamp = 1.2f;
        [Tooltip("Gravity never gets heavier than this many times normal")]
        public float FallGravityMax = 3f;

        private bool _broomFall;
        private float _broomFallTime;

        // she stepped off the broom: she keeps her speed (horizontal carry and vertical speed), and a long fall drops faster and faster
        public void BeginBroomFall(Vector3 carry, float verticalSpeed)
        {
            GroundedCheck();   // the flag is stale after a flight
            CarryVelocity = carry;
            _verticalVelocity = verticalSpeed;
            _broomFall = true;
            _broomFallTime = 0f;
        }

        [Header("Lock On")]
        public TargetLockOn LockOn;

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

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            if (IsFlying) return;   // BroomFlight controls movement

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

        private void Move()
        {
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
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

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

            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                            new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime +
                            CarryVelocity * Time.deltaTime);      // <- added

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
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
            _verticalVelocity = 0f;      // clears stored fall speed
            _speed = 0f;                 // clears stored ground speed
            _animationBlend = 0f;
            _fallTimeoutDelta = FallTimeout;
            _jumpTimeoutDelta = JumpTimeout;
        }
    }
}