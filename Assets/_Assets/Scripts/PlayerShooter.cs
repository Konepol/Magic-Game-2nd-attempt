using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] TargetLockOn lockOn;
    [SerializeField] Transform muzzle;               // empty at the hand or weapon
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] InputActionReference fireAction;

    [Header("Aiming")]
    [SerializeField] LayerMask aimMask = ~0;         // exclude the Player layer
    [SerializeField] float maxAimDistance = 100f;
    [SerializeField] bool useMousePosition = false;  // false = screen center crosshair

    [Header("Firing")]
    [SerializeField] float fireRate = 6f;            // shots per second
    [SerializeField] bool holdToFire = true;

    float nextFireTime;

    void OnEnable() => fireAction.action.Enable();

    void Update()
    {
        bool wantsFire = holdToFire
            ? fireAction.action.IsPressed()
            : fireAction.action.WasPressedThisFrame();

        if (wantsFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Fire();
        }
    }

    void Fire()
    {
        Vector3 aimPoint = (lockOn != null && lockOn.IsLocked)
            ? lockOn.AimPoint
            : GetCursorAimPoint();

        Vector3 dir = aimPoint - muzzle.position;

        // aim point closer than the muzzle would flip the shot backwards, so fall back to camera forward
        if (dir.sqrMagnitude < 0.01f || Vector3.Dot(dir, mainCamera.transform.forward) < 0f)
            dir = mainCamera.transform.forward;

        Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
    }

    Vector3 GetCursorAimPoint()
    {
        Vector2 screenPoint = useMousePosition
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Ray ray = mainCamera.ScreenPointToRay(screenPoint);

        return Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : ray.GetPoint(maxAimDistance);   // nothing hit: aim at a far point along the ray
    }
}