using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 30f;
    [SerializeField] float lifetime = 4f;
    [SerializeField] float radius = 0.1f;
    [SerializeField] LayerMask hitMask = ~0;   // exclude the Player layer here
    [SerializeField] float damage = 10f;
    [SerializeField] string damageTag = "Enemy";    // only damages this tag (use "Player" for enemy bullets)

    float explosionRadius;
    int pierceRemaining;          // enemies it can still pass through (-1 = unlimited)
    bool explodeOnPierce;
    bool breaksBarriers;
    StatusEffectSpec status;
    float homingTurnRate;         // degrees per second, 0 = flies straight
    Transform homingTarget;
    Vector3 baseScale;
    float spawnTime;
    readonly HashSet<IDamageable> pierced = new HashSet<IDamageable>();

    static readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    static readonly Collider[] overlapBuffer = new Collider[8];
    static readonly Comparer<RaycastHit> byDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    public float Speed => speed;

    // called right after Instantiate to override the prefab values (spells use this)
    public void Init(float damage, float speed, float lifetime, float scale, float explosionRadius,
        int pierceCount = 0, bool explodeOnPierce = false, bool breaksBarriers = false,
        StatusEffectSpec status = default, float homingTurnRate = 0f)
    {
        this.damage = damage;
        this.speed = speed;
        this.lifetime = lifetime;
        this.explosionRadius = explosionRadius;
        pierceRemaining = pierceCount;
        this.explodeOnPierce = explodeOnPierce;
        this.breaksBarriers = breaksBarriers;
        this.status = status;
        this.homingTurnRate = homingTurnRate;
        transform.localScale *= scale;
        radius *= scale;
    }

    void Start()
    {
        if (SpellFX.UseMagicFX)
        {
            var rend = GetComponentInChildren<Renderer>();
            Color col = rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor") ? rend.sharedMaterial.GetColor("_BaseColor") : new Color(1f, 0.8f, 0.4f);
            MagicFX.StarTrail(transform, col, Mathf.Clamp(transform.localScale.x, 0.6f, 2f));
        }
        spawnTime = Time.time;
        baseScale = transform.localScale;
        Destroy(gameObject, lifetime);
    }


    void Update()
    {
        float speedFactor = homingTurnRate > 0f ? Steer() : 1f;
        if (homingTurnRate > 0f && CheckOverlap()) return;

        float dist = speed * speedFactor * Time.deltaTime;

        // sweep instead of moving blindly, so fast bullets can't pass through colliders
        int count = Physics.SphereCastNonAlloc(transform.position, radius, transform.forward,
            hitBuffer, dist, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hitBuffer, 0, count, byDistance);

        for (int i = 0; i < count; i++)
        {
            var hit = hitBuffer[i];
            if (hit.distance <= 0f) continue;   // started inside it, ignore

            // barriers: stop shots from the side they protect, let everyone else's shots through
            var barrier = hit.collider.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (!barrier.Blocks(damageTag)) continue;
                // an explosion already reaches the barrier, so only hit it directly when there is no blast
                if (explosionRadius > 0f) Explode(hit.point);
                else barrier.Hit(damage, breaksBarriers, hit.point);
                Destroy(gameObject);
                return;
            }

            var target = hit.collider.GetComponentInParent<IDamageable>();
            bool isTarget = target != null && hit.collider.CompareTag(damageTag);

            // enemies can be passed through while pierce charges remain
            if (isTarget && pierceRemaining != 0)
            {
                if (!pierced.Add(target)) continue;   // already passed through this one

                if (explodeOnPierce && explosionRadius > 0f) Explode(hit.point);
                else
                {
                    target.TakeDamage(damage, hit.point);
                    StatusEffects.Apply(hit.collider, status);
                }

                if (pierceRemaining > 0) pierceRemaining--;
                continue;
            }

            OnHit(hit);
            return;
        }

        transform.position += transform.forward * dist;
    }

    // homing shots turn toward the target and shrink away as their time limit runs out.
    // Returns a speed factor: close to the target it slows to what its turning can handle, so it curves in instead of orbiting.
    float Steer()
    {
        float factor = 1f;
        if (homingTarget == null)
        {
            var go = GameObject.FindGameObjectWithTag(damageTag);
            if (go != null) homingTarget = go.transform;
        }
        if (homingTarget != null)
        {
            Vector3 want = homingTarget.position + Vector3.up - transform.position;
            if (want.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(want), homingTurnRate * Time.deltaTime);
            factor = Mathf.Clamp(homingTurnRate * Mathf.Deg2Rad * want.magnitude / Mathf.Max(speed, 0.01f), 0.05f, 1f);
        }

        float left = spawnTime + lifetime - Time.time;
        if (left < 0.8f) transform.localScale = baseScale * Mathf.Clamp01(left / 0.8f);
        return factor;
    }

    // homing shots creep in slowly, so the target can already be inside the shot, where a sweep would not see it
    bool CheckOverlap()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = overlapBuffer[i];

            var barrier = col.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (!barrier.Blocks(damageTag)) continue;
                if (explosionRadius > 0f) Explode(transform.position);
                else barrier.Hit(damage, breaksBarriers, transform.position);
                Destroy(gameObject);
                return true;
            }

            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || !col.CompareTag(damageTag)) continue;

            if (explosionRadius > 0f) Explode(transform.position);
            else
            {
                target.TakeDamage(damage, transform.position);
                StatusEffects.Apply(col, status);
            }
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    void OnHit(RaycastHit hit)
    {
        if (explosionRadius > 0f)
        {
            Explode(hit.point);
        }
        else
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && hit.collider.CompareTag(damageTag))
            {
                target.TakeDamage(damage, hit.point);
                StatusEffects.Apply(hit.collider, status);
            }
        }

        // TODO: spawn hit effect at hit.point
        Destroy(gameObject);
    }

    // damages everything with the damage tag inside the radius, once each
    void Explode(Vector3 point)
    {
        var hits = Physics.OverlapSphere(point, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<IDamageable>();
        foreach (var col in hits)
        {
            var barrier = col.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (barrier.Blocks(damageTag) && done.Add(barrier.Health)) barrier.Hit(damage, breaksBarriers, col.ClosestPoint(point));
                continue;
            }
            if (!col.CompareTag(damageTag)) continue;
            var target = col.GetComponentInParent<IDamageable>();
            if (target != null && done.Add(target))
            {
                target.TakeDamage(damage, col.ClosestPoint(point));
                StatusEffects.Apply(col, status);
            }
        }
        ExplosionFlash.Spawn(point, explosionRadius);
    }
}

// quick expanding sphere so explosions are visible; replace with a real effect later
public class ExplosionFlash : MonoBehaviour
{
    float radius;
    float start;
    const float Duration = 0.15f;

    public static void Spawn(Vector3 point, float radius, Color? color = null)
    {
        Color c = color ?? new Color(1f, 0.55f, 0.15f);
        SpellFX.Explosion(point, radius, c);
        SpellFX.Play(Sfx.Explosion, point);
        CameraShake.AddAt(point, Mathf.Clamp01(radius * 0.07f));

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ExplosionFlash";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = point;
        go.transform.localScale = Vector3.zero;
        var f = go.AddComponent<ExplosionFlash>();
        f.radius = radius;
        f.start = Time.time;
        go.GetComponent<Renderer>().material.color = new Color(c.r, c.g, c.b, 1f);
    }

    void Update()
    {
        float t = (Time.time - start) / Duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        transform.localScale = Vector3.one * (radius * 2f * Mathf.Sqrt(t));
    }
}
