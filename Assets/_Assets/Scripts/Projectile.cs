using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 30f;
    [SerializeField] float lifetime = 4f;
    [SerializeField] float radius = 0.1f;
    [SerializeField] LayerMask hitMask = ~0;   // exclude the Player layer here
    [SerializeField] float damage = 10f;
    [SerializeField] string damageTag = "Enemy";    // only damages this tag (use "Player" for enemy bullets)   

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
        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null && hit.collider.CompareTag(damageTag))
            target.TakeDamage(damage, hit.point);

        // TODO: spawn hit effect at hit.point
        Destroy(gameObject);
    }
}