using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 30f;
    [SerializeField] float lifetime = 4f;
    [SerializeField] float radius = 0.1f;
    [SerializeField] LayerMask hitMask = ~0;   // exclude the Player layer here

    void Start() => Destroy(gameObject, lifetime);

    void Update()
    {
        float dist = speed * Time.deltaTime;

        // sweep instead of moving blindly, so fast bullets can't pass through colliders
        if (Physics.SphereCast(transform.position, radius, transform.forward,
                out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
        {
            OnHit(hit);
            return;
        }
        transform.position += transform.forward * dist;
    }

    void OnHit(RaycastHit hit)
    {
        // Debug.Log("[Projectile] hit " + hit.collider.name);
        // TODO: apply damage here, e.g. hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(...)
        // TODO: spawn hit effect at hit.point
        Destroy(gameObject);
    }
}