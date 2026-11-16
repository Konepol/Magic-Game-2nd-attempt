using System.Collections.Generic;
using UnityEngine;

// Hollow Moon. A dark disc grows over the wand while chanting (see WandChargeVisual).
// Tap: a spinning crescent blade. Held: a moon hangs in the sky, then collapses into a huge ring that shrinks and crushes everything inside.
// Output: bigger (the blade, the moon and the ring), at a low output limit because it is a big spell.

static class CrescentMesh
{
    static Mesh cached;

    // a flat crescent lying in the XZ plane, tips pointing back (-Z), belly forward
    public static Mesh Get()
    {
        if (cached != null) return cached;
        const int n = 24;
        float tip = 140f * Mathf.Deg2Rad;
        float cosTip = Mathf.Cos(tip);
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float th = Mathf.Lerp(-tip, tip, i / (float)n);
            float z = Mathf.Cos(th), x = Mathf.Sin(th);
            float innerZ = cosTip + (Mathf.Cos(th) - cosTip) * 0.88f;
            verts.Add(new Vector3(x, 0f, z));            // outer edge
            verts.Add(new Vector3(x, 0f, innerZ));       // inner edge
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.AddRange(new[] { a, c, b, b, c, d });
        }
        cached = new Mesh { name = "Crescent" };
        cached.SetVertices(verts);
        cached.SetTriangles(tris, 0);
        cached.RecalculateNormals();
        cached.RecalculateBounds();
        return cached;
    }
}

public class MoonBlade : MonoBehaviour
{
    Vector3 dir;
    float speed, life, radius, damage;
    string enemyTag;
    readonly HashSet<Health> hit = new HashSet<Health>();
    Transform spinner;
    Material bodyMat, glowMat;
    Color color;

    public static void Cast(SpellCastContext c)
    {
        var go = new GameObject("MoonBlade");
        go.transform.position = c.origin + c.aim * 0.8f;
        var b = go.AddComponent<MoonBlade>();
        b.dir = c.aim;
        b.speed = Mathf.Max(20f, c.v.projectileSpeed);
        b.life = Mathf.Max(0.6f, c.v.lifetime);
        b.radius = 1.3f * Mathf.Max(0.5f, c.v.projectileScale);
        b.damage = c.v.damage;
        b.enemyTag = c.damageTag;
        b.color = c.v.effectColor;
        b.Build();
        SpellFX.Play(Sfx.Cast, c.origin, 0.8f);
        CameraShake.Add(0.1f);
    }

    void Build()
    {
        spinner = new GameObject("Spin").transform;
        spinner.SetParent(transform, false);
        spinner.localRotation = Quaternion.Euler(18f, 0f, 0f);
        spinner.localScale = Vector3.one * radius;

        bodyMat = SpellUtil.Solid(new Color(0.06f, 0.02f, 0.14f, 0.96f));
        glowMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.9f));
        MakePart("Body", bodyMat, 1f);
        MakePart("Rim", glowMat, 1.12f);

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.widthMultiplier = radius * 1.1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.sharedMaterial = glowMat;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void MakePart(string name, Material mat, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(spinner, false);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = CrescentMesh.Get();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        life -= Time.deltaTime;
        spinner.Rotate(0f, 900f * Time.deltaTime, 0f, Space.Self);
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector3 from = transform.position;
        Vector3 to = from + dir * speed * Time.deltaTime;

        // a wall or the ground ends it (enemies and the player do not)
        if (Physics.Linecast(from, to, out RaycastHit wall, ~((1 << 2) | (1 << LayerMask.NameToLayer("Player"))), QueryTriggerInteraction.Ignore)
            && wall.collider.GetComponentInParent<Health>() == null)
        {
            SpellFX.Sparks(wall.point, color);
            Destroy(gameObject);
            return;
        }
        transform.position = to;

        foreach (var h in SpellUtil.Enemies(to, radius * 1.1f, enemyTag))
        {
            if (!hit.Add(h)) continue;
            h.TakeDamage(damage, to);
            SpellFX.Sparks(SpellUtil.CenterOf(h), color);
            SpellFX.Play(Sfx.Hit, to, 0.6f);
        }
    }

    void OnDestroy()
    {
        if (bodyMat != null) Destroy(bodyMat);
        if (glowMat != null) Destroy(glowMat);
    }
}

// The moon: a dark disc appears high above the target, hangs, then the ring on the ground shrinks and crushes what is inside.
public class MoonCollapse : MonoBehaviour
{
    float radius, damage, height;
    bool great;
    Vector3 center;
    Color color;
    string enemyTag;
    GameObject disc;
    LineRenderer ring, rim;
    Material discMat, ringMat;
    float t;
    int phase;                // 0 rising, 1 hanging, 2 collapsing, 3 fading
    float pullTick;
    readonly HashSet<Health> ticked = new HashSet<Health>();
    CameraZoom zoom;
    float discSize;

    const float Rise = 0.9f, Hang = 0.8f, Collapse = 1.0f;

    public static void Cast(SpellCastContext c, bool great)
    {
        Vector3 center = c.lockTarget != null ? SpellUtil.GroundAt(c.lockTarget.position) : SpellUtil.GroundAt(c.aimPoint);
        // not further than a long throw away
        Vector3 flat = center - c.root.position; flat.y = 0f;
        float maxDist = great ? 60f : 45f;
        if (flat.magnitude > maxDist) center = SpellUtil.GroundAt(c.root.position + flat.normalized * maxDist);

        var go = new GameObject(great ? "HollowMoon_Great" : "HollowMoon");
        var m = go.AddComponent<MoonCollapse>();
        m.great = great;
        m.center = center;
        m.radius = Mathf.Max(3f, c.v.aoeRadius);
        m.damage = c.v.damage;
        m.color = c.v.effectColor;
        m.enemyTag = c.damageTag;
        m.height = Mathf.Max(8f, m.radius * 1.1f);
        m.Build();
        SpellFX.Play(Sfx.Charge, center, 1f);
    }

    void Build()
    {
        discSize = radius * 0.55f;
        disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        disc.transform.SetParent(transform, false);
        Destroy(disc.GetComponent<Collider>());
        discMat = SpellVisuals.Make(new Color(0.02f, 0f, 0.06f, 1f), false);
        var r = disc.GetComponent<Renderer>();
        r.sharedMaterial = discMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        disc.transform.position = center + Vector3.up * height;
        disc.transform.localScale = Vector3.zero;

        ringMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.95f));
        rim = SpellVisuals.CreateRing(transform, ringMat, 0.18f);
        ring = SpellVisuals.CreateRing(transform, ringMat, 0.35f);
        ring.positionCount = 96;
        ring.enabled = false;
        zoom = Object.FindFirstObjectByType<CameraZoom>();
    }

    void Update()
    {
        t += Time.deltaTime;
        Vector3 discPos = center + Vector3.up * height;
        float pull = 0f;

        switch (phase)
        {
            case 0:
            {
                float k = Mathf.Clamp01(t / Rise);
                float eased = 1f - (1f - k) * (1f - k);
                disc.transform.localScale = Vector3.one * (discSize * eased);
                disc.transform.position = discPos;
                SpellUtil.SetBillboardRing(rim, discPos, discSize * eased * 0.5f * 1.06f, Camera.main);
                // the ring on the ground draws in as a warning
                ring.enabled = true;
                SpellVisuals.SetRing(ring, center + Vector3.up * 0.15f, radius);
                SetRingAlpha(0.25f + 0.35f * k);
                if (great && zoom != null) zoom.CastPullOut = Mathf.Lerp(0f, 14f, eased);
                if (t >= Rise) { phase = 1; t = 0f; }
                break;
            }
            case 1:
            {
                float bob = Mathf.Sin(t * 5f) * 0.15f;
                disc.transform.position = discPos + Vector3.up * bob;
                SpellUtil.SetBillboardRing(rim, discPos + Vector3.up * bob, discSize * 0.5f * 1.06f * (1f + 0.03f * Mathf.Sin(t * 20f)), Camera.main);
                SpellVisuals.SetRing(ring, center + Vector3.up * 0.15f, radius);
                SetRingAlpha(0.6f + 0.2f * Mathf.Sin(t * 12f));
                if (t >= Hang) { phase = 2; t = 0f; SpellFX.Play(Sfx.Charge, center, 1f); }
                break;
            }
            case 2:
            {
                float k = Mathf.Clamp01(t / Collapse);
                float eased = k * k;
                float r = Mathf.Lerp(radius, 0.4f, eased);
                SpellVisuals.SetRing(ring, center + Vector3.up * 0.15f, r);
                SetRingAlpha(1f);
                // the moon falls into the ring as it shrinks
                Vector3 p = Vector3.Lerp(discPos, center + Vector3.up * 1.5f, eased);
                disc.transform.position = p;
                disc.transform.localScale = Vector3.one * Mathf.Lerp(discSize, discSize * 0.35f, eased);
                SpellUtil.SetBillboardRing(rim, p, disc.transform.localScale.x * 0.5f * 1.06f, Camera.main);

                // everything inside is dragged to the middle and ground down
                pullTick -= Time.deltaTime;
                foreach (var h in SpellUtil.Enemies(center, r + 1f, enemyTag))
                {
                    Vector3 to = center - h.transform.position; to.y = 0f;
                    if (to.magnitude > 0.5f) SpellUtil.MoveBody(h.transform, to.normalized * Mathf.Min(to.magnitude, (great ? 22f : 16f) * Time.deltaTime));
                    if (pullTick <= 0f) h.TakeDamage(damage * 0.04f, h.transform.position + Vector3.up);
                }
                if (pullTick <= 0f) pullTick = 0.2f;
                CameraShake.Add(0.02f + 0.05f * eased);
                if (k >= 1f) Crush();
                break;
            }
            default:
            {
                float k = Mathf.Clamp01(t / 0.5f);
                SetRingAlpha(1f - k);
                disc.transform.localScale = Vector3.one * Mathf.Lerp(discSize * 0.35f, 0f, k);
                if (great && zoom != null) zoom.CastPullOut = Mathf.Lerp(14f, 0f, k);
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    void SetRingAlpha(float a)
    {
        var c = ringMat.color; c.a = a; SpellVisuals.SetColor(ringMat, c);
    }

    void Crush()
    {
        phase = 3;
        t = 0f;
        ring.enabled = false;
        foreach (var h in SpellUtil.Enemies(center, radius * 0.5f + 2f, enemyTag))
        {
            h.TakeDamage(damage, h.transform.position + Vector3.up);
            SpellFX.Sparks(SpellUtil.CenterOf(h), color);
        }
        SpellFX.Explosion(center + Vector3.up, radius * 0.5f, color);
        SpellFX.Play(Sfx.Explosion, center, 1f);
        CameraShake.Add(great ? 1.2f : 0.7f);
    }

    void OnDestroy()
    {
        if (zoom != null) zoom.CastPullOut = 0f;
        if (discMat != null) Destroy(discMat);
        if (ringMat != null) Destroy(ringMat);
    }
}
