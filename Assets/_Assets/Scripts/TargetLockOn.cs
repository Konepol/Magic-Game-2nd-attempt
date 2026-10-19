using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

public class TargetLockOn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] Transform lockOnPivot;              // child of player, has the Aim Constraint
    [SerializeField] CinemachineTargetGroup targetGroup;
    [SerializeField] Animator cameraStateAnimator;       // the CameraStateAnimator object
    [SerializeField] string lockOnBool = "LockOn";
    [SerializeField] InputActionReference lockOnAction;

    [Header("Detection")]
    [SerializeField] string enemyTag = "Enemy";
    [SerializeField] LayerMask obstacleLayer;            // walls/ground only, NOT player or enemies
    [SerializeField] float detectRadius = 20f;
    [SerializeField] float maxAngle = 35f;
    [SerializeField] float breakDistance = 25f;
    [SerializeField] float loseSightTime = 2f;

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    public Transform CurrentTarget { get; private set; }
    public bool IsLocked => CurrentTarget != null;

    AimConstraint aim;
    int lockOnHash;
    float hiddenTimer;

    void Log(string msg) { if (debugLogs) Debug.Log("[LockOn] " + msg, this); }
    void Warn(string msg) { Debug.LogWarning("[LockOn] " + msg, this); }

    void Awake()
    {
        Log("STEP 0: Awake, checking references");
        if (mainCamera == null) Warn("mainCamera is NOT assigned");
        if (lockOnPivot == null) Warn("lockOnPivot is NOT assigned");
        if (targetGroup == null) Warn("targetGroup is NOT assigned");
        if (cameraStateAnimator == null) Warn("cameraStateAnimator is NOT assigned");
        if (lockOnAction == null) Warn("lockOnAction is NOT assigned");

        lockOnHash = Animator.StringToHash(lockOnBool);

        aim = lockOnPivot != null ? lockOnPivot.GetComponent<AimConstraint>() : null;
        if (aim == null) Warn("No AimConstraint on lockOnPivot");
        else { aim.constraintActive = false; Log("AimConstraint found, set inactive"); }

        if (cameraStateAnimator != null)
        {
            bool hasParam = false;
            foreach (var p in cameraStateAnimator.parameters)
                if (p.nameHash == lockOnHash) hasParam = true;
            if (!hasParam) Warn("Animator has no parameter named '" + lockOnBool + "'");
        }
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
        Log("Subscribed to input '" + lockOnAction.action.name + "'");
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
        if (IsLocked)
        {
            Log("STEP 2: already locked on '" + CurrentTarget.name + "', unlocking");
            Unlock();
            return;
        }

        Log("STEP 2: searching for a target");
        Transform best = FindBestTarget();
        if (best != null) Lock(best);
        else Log("STEP 6: no valid target, nothing happens");
    }

    Transform FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            lockOnPivot.position, detectRadius, ~0, QueryTriggerInteraction.Ignore);
        Log("STEP 3: OverlapSphere found " + hits.Length + " colliders");

        Transform best = null;
        float bestScore = float.MaxValue;
        int tagMatches = 0;

        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            tagMatches++;

            Vector3 point = col.bounds.center;
            float angle = Vector3.Angle(mainCamera.transform.forward, point - mainCamera.transform.position);

            if (angle > maxAngle)
            {
                Log("  - '" + col.name + "' REJECTED: angle " + angle.ToString("F1") + " > " + maxAngle);
                continue;
            }
            if (!HasLineOfSight(point))
            {
                Log("  - '" + col.name + "' REJECTED: line of sight blocked");
                continue;
            }

            float dist = Vector3.Distance(lockOnPivot.position, point);
            float score = angle / maxAngle + 0.5f * (dist / detectRadius);
            Log("  - '" + col.name + "' OK: angle " + angle.ToString("F1") + ", dist " + dist.ToString("F1") + ", score " + score.ToString("F2"));

            if (score < bestScore) { bestScore = score; best = col.transform; }
        }

        Log("STEP 4: " + tagMatches + " colliders had tag '" + enemyTag + "'. Best = " + (best != null ? best.name : "none"));
        return best;
    }

    bool HasLineOfSight(Vector3 point)
    {
        bool blocked = Physics.Linecast(lockOnPivot.position, point, out RaycastHit hit, obstacleLayer);
        if (blocked) Log("    LOS blocked by '" + hit.collider.name + "'");
        return !blocked;
    }

    void Lock(Transform target)
    {
        Log("STEP 5: LOCKING onto '" + target.name + "'");
        CurrentTarget = target;
        hiddenTimer = 0f;

        targetGroup.AddMember(target, 0.8f, 1f);
        Log("  - Target Group members: " + targetGroup.Targets.Count + " (expected 2)");

        while (aim.sourceCount > 0) aim.RemoveSource(0);
        aim.AddSource(new ConstraintSource { sourceTransform = target, weight = 1f });
        aim.constraintActive = true;
        Log("  - Aim sources: " + aim.sourceCount + ", active: " + aim.constraintActive + ", weight: " + aim.weight);

        cameraStateAnimator.SetBool(lockOnHash, true);
        Log("  - Animator bool '" + lockOnBool + "' set to TRUE");
    }

    public void Unlock()
    {
        Log("UNLOCK: releasing target");
        CurrentTarget = null;
        if (targetGroup.Targets.Count > 1)
            targetGroup.RemoveMember(targetGroup.Targets[1].Object);
        aim.constraintActive = false;
        lockOnPivot.localRotation = Quaternion.identity;

        cameraStateAnimator.SetBool(lockOnHash, false);
        Log("  - Animator bool '" + lockOnBool + "' set to FALSE");
    }

    void Update()
    {
        if (!IsLocked) return;

        if (!CurrentTarget.gameObject.activeInHierarchy)
        {
            Log("AUTO-UNLOCK: target disabled or destroyed");
            Unlock();
            return;
        }

        float dist = Vector3.Distance(lockOnPivot.position, CurrentTarget.position);
        if (dist > breakDistance)
        {
            Log("AUTO-UNLOCK: too far (" + dist.ToString("F1") + ")");
            Unlock();
            return;
        }

        hiddenTimer = HasLineOfSight(CurrentTarget.position) ? 0f : hiddenTimer + Time.deltaTime;
        if (hiddenTimer > loseSightTime)
        {
            Log("AUTO-UNLOCK: hidden for " + hiddenTimer.ToString("F1") + "s");
            Unlock();
        }
    }
}