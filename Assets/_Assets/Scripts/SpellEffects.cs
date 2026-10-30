using UnityEngine;

// shared helpers for the visuals of non-projectile spells
public static class SpellVisuals
{
    public static Material Make(Color color, bool transparent)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        SetColor(m, color);
        if (transparent && m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);   // SrcAlpha
            m.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        return m;
    }

    public static void SetColor(Material m, Color c)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    public static LineRenderer CreateRing(Transform parent, Material mat, float width)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(parent != null ? parent : SceneGroups.Get("VFX/Misc"), false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 48;
        lr.sharedMaterial = mat;
        lr.startWidth = lr.endWidth = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    public static void SetRing(LineRenderer lr, Vector3 center, float radius)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }
}

// instant beam visual: a line that fades out
public class LaserBeam : MonoBehaviour
{
    LineRenderer line;
    Material mat;
    Color color;
    float width, duration, start;

    public static void Spawn(Vector3 from, Vector3 to, float width, Color color, float duration, string fxKey = null)
    {
        if (fxKey != null && SpellFX.UseMagicFX && BeamFX.Handles(fxKey)) { BeamFX.Spawn(from, to, width, color, duration, fxKey); return; }
        var go = SceneGroups.Put(new GameObject("LaserBeam"), "VFX/Beams");
        var b = go.AddComponent<LaserBeam>();
        b.mat = SpellVisuals.Make(color, true);
        b.line = go.AddComponent<LineRenderer>();
        b.line.positionCount = 2;
        b.line.useWorldSpace = true;
        b.line.sharedMaterial = b.mat;
        b.line.numCapVertices = 4;
        b.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        b.line.receiveShadows = false;
        b.line.SetPosition(0, from);
        b.line.SetPosition(1, to);
        b.color = color;
        b.width = width;
        b.duration = Mathf.Max(duration, 0.02f);
        b.start = Time.time;
    }

    void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        float fade = 1f - t;
        line.startWidth = line.endWidth = width * fade;
        Color c = color; c.a = fade;
        SpellVisuals.SetColor(mat, c);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// ground blast: shows a ring for the delay (an inner ring fills it), then damages everything inside
public class AoeStrike : MonoBehaviour
{
    Vector3 point;
    SpellVariant v;
    string damageTag;
    string fxKey;         // the spell's asset name, for its own impact effect
    bool inAir;           // centred on something in the air: hits a sphere around the point instead of a column above the ground
    float start;
    LineRenderer inner;
    Material mat;

    public static void Spawn(Vector3 point, SpellVariant v, string damageTag, bool inAir = false, string fxKey = null)
    {
        var go = SceneGroups.Put(new GameObject("AoeStrike"), "VFX/Misc");
        var s = go.AddComponent<AoeStrike>();
        s.point = point;
        s.v = v;
        s.damageTag = damageTag;
        s.inAir = inAir;
        s.fxKey = fxKey;
        s.start = Time.time;
        SpellImpact.Telegraph(fxKey, point, v.aoeRadius, v.aoeDelay, v.effectColor, inAir);

        if (v.aoeDelay <= 0f) { s.Strike(); Destroy(go); return; }

        s.mat = SpellVisuals.Make(v.effectColor, false);
        Vector3 p = point + Vector3.up * 0.06f;
        var outer = SpellVisuals.CreateRing(go.transform, s.mat, 0.12f);
        s.inner = SpellVisuals.CreateRing(go.transform, s.mat, 0.08f);
        SpellVisuals.SetRing(outer, p, v.aoeRadius);
        SpellVisuals.SetRing(s.inner, p, 0.01f);
    }

    void Update()
    {
        float t = (Time.time - start) / Mathf.Max(v.aoeDelay, 0.01f);
        if (t >= 1f) { Strike(); Destroy(gameObject); return; }
        SpellVisuals.SetRing(inner, point + Vector3.up * 0.06f, v.aoeRadius * t);
    }

    void Strike()
    {
        float r = v.aoeRadius;
        if (fxKey == "Cyclone" && SpellFX.UseMagicFX)
        {
            // Cyclone is a ground vortex now: it pulls enemies in for a few seconds and hurts them steadily, instead of one blast
            SpellImpact.CycloneStart(point, r, 5f, v.damage / 5f, damageTag);
            SpellFX.Play(Sfx.Explosion, point, 0.6f);
            return;
        }
        float h = Mathf.Max(v.aoeHeight, r * 2f);
        var cols = inAir
            ? Physics.OverlapSphere(point, r, ~0, QueryTriggerInteraction.Ignore)
            : Physics.OverlapCapsule(point + Vector3.up * r, point + Vector3.up * (h - r), r, ~0, QueryTriggerInteraction.Ignore);
        var done = new System.Collections.Generic.HashSet<IDamageable>();
        foreach (var col in cols)
        {
            var barrier = col.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (barrier.Blocks(damageTag) && done.Add(barrier.Health)) barrier.Hit(v.damage, v.breaksBarriers, col.ClosestPoint(point));
                continue;
            }
            if (!col.CompareTag(damageTag)) continue;
            var target = col.GetComponentInParent<IDamageable>();
            if (target != null && done.Add(target))
            {
                target.TakeDamage(v.damage, col.ClosestPoint(point));
                StatusEffects.Apply(col, v.status);
            }
        }
        ExplosionFlash.Spawn(inAir ? point : point + Vector3.up * 0.5f, r, v.effectColor, fxKey);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
