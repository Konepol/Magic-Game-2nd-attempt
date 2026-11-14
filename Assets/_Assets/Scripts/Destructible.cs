using System.Collections.Generic;
using UnityEngine;

// Scenery that the big spells can break: pillars, walls, towers, bridges, crystal growths.
// Put it on the prop (its colliders can be on it or on its children; no tag is needed). Explosions, sky pillars, beams and direct hits all
// reach it, but a hit weaker than Min Hit Damage does nothing, so quick spells chip nothing off and only heavy casts bring it down.
// When it breaks, every child mesh becomes a falling chunk; an optional prefab (a ruined version) can take its place.
public class Destructible : MonoBehaviour, IDamageable
{
    [SerializeField] float health = 150f;
    [SerializeField, Tooltip("Hits that do less damage than this are ignored. Around 40 lets charged spells and fusions break it; 150 or more leaves it to the siege spells.")]
    float minHitDamage = 40f;
    [SerializeField, Tooltip("Optional: spawned in its place when it breaks (rubble, a broken stump).")]
    GameObject brokenPrefab;
    [SerializeField] Color dustColor = new Color(0.72f, 0.62f, 0.5f);
    [SerializeField] float chunkLifetime = 4f;
    [SerializeField, Tooltip("Enemies and the player standing this close when it comes down take this much damage (0 = harmless).")]
    float collapseDamage = 0f;
    [SerializeField] float collapseRadius = 4f;

    public bool IsBroken { get; private set; }
    public float Health01 => Mathf.Clamp01(health / Mathf.Max(startHealth, 0.01f));

    public event System.Action<Destructible> Broken;

    float startHealth;

    void Awake() => startHealth = health;

    // for blasts: true when the collider belongs to a destructible (it is damaged once per blast, tracked in `done`)
    public static bool TryHit(Collider col, float damage, Vector3 from, HashSet<IDamageable> done)
    {
        var d = col.GetComponentInParent<Destructible>();
        if (d == null) return false;
        if (done == null || done.Add(d)) d.TakeDamage(damage, col.ClosestPoint(from));
        return true;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (IsBroken || amount < minHitDamage) return;

        health -= amount;
        if (health > 0f)
        {
            SpellImpact.Rubble(hitPoint, 1.2f, dustColor, 5);
            return;
        }
        Break(hitPoint, amount);
    }

    void Break(Vector3 hitPoint, float force)
    {
        IsBroken = true;
        Broken?.Invoke(this);

        Bounds b = new Bounds(transform.position, Vector3.one);
        bool first = true;
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        float size = Mathf.Clamp(b.extents.magnitude, 1f, 12f);

        SpellImpact.Rubble(b.center, size, dustColor, 18);
        SpellFX.Play(Sfx.Explosion, b.center, 0.9f);
        CameraShake.AddAt(b.center, Mathf.Clamp01(size * 0.08f));

        if (collapseDamage > 0f)
        {
            var done = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(b.center, collapseRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform)) continue;
                var t = col.GetComponentInParent<IDamageable>();
                if (t != null && !(t is Destructible) && done.Add(t)) t.TakeDamage(collapseDamage, col.ClosestPoint(b.center));
            }
        }

        if (brokenPrefab != null) Instantiate(brokenPrefab, transform.position, transform.rotation);

        // nothing can stand on it or be blocked by it any more
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

        // every mesh under it becomes a chunk that is thrown away from the hit and falls
        var chunks = SceneGroups.Get("VFX/Ground");
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            var t = r.transform;
            t.SetParent(chunks, true);
            var body = t.gameObject.AddComponent<Rigidbody>();
            body.mass = 5f;
            Vector3 away = (r.bounds.center - hitPoint).normalized + Vector3.up * 0.6f;
            body.linearVelocity = away * Mathf.Clamp(force * 0.04f, 3f, 14f) + Random.insideUnitSphere * 2f;
            body.angularVelocity = Random.insideUnitSphere * 4f;
            Destroy(t.gameObject, chunkLifetime + Random.value);
        }

        // a prop that is a single mesh is itself the chunk: it only stops being a destructible
        if (GetComponent<Rigidbody>() != null) Destroy(this);
        else Destroy(gameObject);
    }
}
