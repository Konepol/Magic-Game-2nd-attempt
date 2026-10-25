using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

public class TargetLockOn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] Transform lockOnPivot;              // child of player, has the Aim Constraint
    [SerializeField] Transform freeCamRoot;              // PlayerCameraRoot
    [SerializeField] CinemachineTargetGroup targetGroup;
    [SerializeField] InputActionReference lockOnAction;

    [Header("Camera State Animator (drives State-Driven Camera)")]
    [SerializeField] Animator cameraStateAnimator;
    [SerializeField] string lockOnBool = "LockOn";

    [Header("Crosshair / Lock-on Indicator (same object)")]
    [SerializeField] RectTransform indicator;
    [SerializeField] Animator indicatorAnimator;         // optional, auto-found on the indicator
    [SerializeField] string indicatorBool = "LockOn";
    [SerializeField] Vector3 indicatorOffset = new Vector3(0f, 1f, 0f);

    [Header("Detection")]
    [SerializeField] string enemyTag = "Enemy";
    [SerializeField] LayerMask obstacleLayer;
    [SerializeField] float detectRadius = 20f;
    [SerializeField] float maxAngle = 35f;
    [SerializeField] float breakDistance = 25f;
    [SerializeField] float loseSightTime = 2f;
    bool hadLock;

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    public Transform CurrentTarget { get; private set; }
    public bool IsLocked => CurrentTarget != null;
    public Vector3 AimPoint => CurrentTarget != null
        ? CurrentTarget.position + indicatorOffset
        : Vector3.zero;

    AimConstraint aim;
    int camBoolHash, indicatorBoolHash;
    float hiddenTimer;

    void Log(string msg) { if (debugLogs) Debug.Log("[LockOn] " + msg, this); }
    void Warn(string msg) { Debug.LogWarning("[LockOn] " + msg, this); }

    void Awake()
    {
        if (mainCamera == null) Warn("mainCamera is NOT assigned");
        if (lockOnPivot == null) Warn("lockOnPivot is NOT assigned");
        if (targetGroup == null) Warn("targetGroup is NOT assigned");
        if (cameraStateAnimator == null) Warn("cameraStateAnimator is NOT assigned");
        if (indicator == null) Warn("indicator is NOT assigned");

        camBoolHash = Animator.StringToHash(lockOnBool);
        indicatorBoolHash = Animator.StringToHash(indicatorBool);

        aim = lockOnPivot != null ? lockOnPivot.GetComponent<AimConstraint>() : null;
        if (aim == null) Warn("No AimConstraint on lockOnPivot");
        else aim.constraintActive = false;

        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);        // always visible: it is the crosshair
            if (indicatorAnimator == null)
                indicatorAnimator = indicator.GetComponent<Animator>();
            if (indicatorAnimator == null)
                Warn("No Animator on the indicator, only its position will change");
        }

        SetIndicatorState(false);
    }

    void OnEnable()
    {
        if (lockOnAction == null || lockOnAction.action == null)
        {
            Warn("lockOnAction is null, input will NOT work");
            return;
        }
        lockOnAction.action.performed += OnLockOnPressed;
        lockOnAction.action.Enable();
    }

    void OnDisable()
    {
        if (lockOnAction != null && lockOnAction.action != null)
            lockOnAction.action.performed -= OnLockOnPressed;
    }

    void OnLockOnPressed(InputAction.CallbackContext ctx)
    {
        Log("STEP 1: key pressed");
        ToggleLockOn();
    }

    public void ToggleLockOn()
    {
        if (IsLocked) { Unlock(); return; }

        Transform best = FindBestTarget();
        if (best != null) Lock(best);
        else Log("No valid target, nothing happens");
    }

    Transform FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            lockOnPivot.position, detectRadius, ~0, QueryTriggerInteraction.Ignore);

        Transform best = null;
        float bestScore = float.MaxValue;

        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;

            Vector3 point = col.bounds.center;
            float angle = Vector3.Angle(mainCamera.transform.forward, point - mainCamera.transform.position);

            if (angle > maxAngle) { Log("  - '" + col.name + "' REJECTED: angle " + angle.ToString("F1")); continue; }
            if (!HasLineOfSight(point)) { Log("  - '" + col.name + "' REJECTED: line of sight"); continue; }

            float dist = Vector3.Distance(lockOnPivot.position, point);
            float score = angle / maxAngle + 0.5f * (dist / detectRadius);
            if (score < bestScore) { bestScore = score; best = col.transform; }
        }
        return best;
    }

    bool HasLineOfSight(Vector3 point) =>
        !Physics.Linecast(lockOnPivot.position, point, obstacleLayer);

    void Lock(Transform target)
    {
        Log("LOCKING onto '" + target.name + "'");
        CurrentTarget = target;
        hiddenTimer = 0f;

        targetGroup.Targets.Clear();
        targetGroup.AddMember(lockOnPivot, 1f, 1f);
        targetGroup.AddMember(target, 0.8f, 1f);

        while (aim.sourceCount > 0) aim.RemoveSource(0);
        aim.AddSource(new ConstraintSource { sourceTransform = target, weight = 1f });
        aim.constraintActive = true;

        cameraStateAnimator.SetBool(camBoolHash, true);
        SetIndicatorState(true);
        hadLock = true;
    }

    public void Unlock()
    {
        Log("UNLOCK");
        CurrentTarget = null;
        aim.constraintActive = false;

        cameraStateAnimator.SetBool(camBoolHash, false);
        SetIndicatorState(false);
        hadLock = false;
    }

    void SetIndicatorState(bool locked)
    {
        if (indicatorAnimator != null)
            indicatorAnimator.SetBool(indicatorBoolHash, locked);
    }

    void Update()
    {
        if (!IsLocked)
        {
            if (hadLock) { Log("AUTO-UNLOCK: target destroyed"); Unlock(); }
            return;
        }

        if (!CurrentTarget.gameObject.activeInHierarchy) { Log("AUTO-UNLOCK: target gone"); Unlock(); return; }

        float dist = Vector3.Distance(lockOnPivot.position, CurrentTarget.position);
        if (dist > breakDistance) { Log("AUTO-UNLOCK: too far"); Unlock(); return; }

        hiddenTimer = HasLineOfSight(CurrentTarget.position) ? 0f : hiddenTimer + Time.deltaTime;
        if (hiddenTimer > loseSightTime) { Log("AUTO-UNLOCK: hidden too long"); Unlock(); }
    }

    void LateUpdate()
    {
        // not locked: keep the pivot yaw matched to the free camera
        if (!IsLocked && freeCamRoot != null)
            lockOnPivot.rotation = Quaternion.Euler(0f, freeCamRoot.eulerAngles.y, 0f);

        if (indicator == null) return;

        if (IsLocked)
        {
            // follow the enemy; if it is behind the camera, keep the last position
            Vector3 screen = mainCamera.WorldToScreenPoint(CurrentTarget.position + indicatorOffset);
            if (screen.z > 0f) indicator.position = screen;
        }
        else
        {
            // free camera: act as the crosshair at screen center
            indicator.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
    }
}