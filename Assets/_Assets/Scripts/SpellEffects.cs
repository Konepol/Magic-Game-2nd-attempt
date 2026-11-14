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

// Area blast: shows a ring for the delay (an inner ring fills it), then damages everything inside.
// On the ground it is a column that starts at the ground and reaches Aoe Height up, so a tall one is a pillar that catches fliers at any altitude.
// Centred on a locked flier it is a column through that flier (or a ball around it when the spell is not taller than it is wide).
public class AoeStrike : MonoBehaviour
{
    Vector3 point;
    SpellVariant v;
    string damageTag;
    string fxKey;         // the spell's effect key, for its own impact effect
    bool inAir;           // centred on something in the air instead of on the ground
    float start;
    LineRenderer inner;
    Material mat;

    static readonly Collider[] buffer = new Collider[512];

    // a spell this wide is a siege spell: it gets the meteor shower, the screen flash and the field of blasts
    public const float ColossalRadius = 14f;

    public static bool IsTall(SpellVariant v) => v.aoeHeight > v.aoeRadius * 2f + 2f;
    public static float HeightOf(SpellVariant v) => Mathf.Max(v.aoeHeight, v.aoeRadius * 2f);

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
        if (v.aoeRadius >= ColossalRadius && SpellFX.UseMagicFX) SpellImpact.ColossalTelegraph(fxKey, point, v.aoeRadius, v.aoeDelay, v.effectColor, inAir);

        if (v.aoeDelay <= 0f) { s.Strike(); Destroy(go); return; }

        s.mat = SpellVisuals.Make(v.effectColor, false);
        Vector3 p = point + Vector3.up * 0.06f;
        var outer = SpellVisuals.CreateRing(go.transform, s.mat, 0.12f);
        s.inner = SpellVisuals.CreateRing(go.transform, s.mat, 0.08f);
        SpellVisuals.SetRing(outer, p, v.aoeRadius);
        SpellVisuals.SetRing(s.inner, p, 0.01f);

        // a pillar also shows a marker line up its axis, so someone flying can see where it will stand
        if (IsTall(v))
        {
            Bottom(point, v, inAir, out Vector3 low, out Vector3 high);
            var axis = SpellVisuals.CreateRing(go.transform, s.mat, 0.14f);
            axis.loop = false;
            axis.positionCount = 2;
            axis.SetPosition(0, low);
            axis.SetPosition(1, high);
        }
    }

    // the lowest and highest point of the area's axis
    public static void Bottom(Vector3 point, SpellVariant v, bool inAir, out Vector3 low, out Vector3 high)
    {
        float h = HeightOf(v);
        if (inAir) { low = point - Vector3.up * h * 0.5f; high = point + Vector3.up * h * 0.5f; }
        else { low = point; high = point + Vector3.up * h; }
    }

    // every collider inside the area (scaled sideways by radiusScale); returns how many were written to the buffer
    public static int Collect(Vector3 point, SpellVariant v, bool inAir, Collider[] into, float radiusScale = 1f)
    {
        float r = v.aoeRadius * radiusScale;
        float h = Mathf.Max(HeightOf(v), r * 2f);

        if (inAir && !IsTall(v)) return Physics.OverlapSphereNonAlloc(point, r, into, ~0, QueryTriggerInteraction.Ignore);

        Vector3 low = inAir ? point - Vector3.up * (h * 0.5f - r) : point + Vector3.up * r;
        Vector3 high = inAir ? point + Vector3.up * (h * 0.5f - r) : point + Vector3.up * (h - r);
        return Physics.OverlapCapsuleNonAlloc(low, high, r, into, ~0, QueryTriggerInteraction.Ignore);
    }

    // damages everything in the area once; used by the blast and by every tick of a lingering area
    public static void DamageArea(Vector3 point, SpellVariant v, bool inAir, string damageTag, float damage)
    {
        int n = Collect(point, v, inAir, buffer);
        var done = new System.Collections.Generic.HashSet<IDamageable>();
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            var barrier = col.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (barrier.Blocks(damageTag) && done.Add(barrier.Health)) barrier.Hit(damage, v.breaksBarriers, col.ClosestPoint(point));
                continue;
            }
            if (Destructible.TryHit(col, damage, point, done)) continue;
            if (!col.CompareTag(damageTag)) continue;
            var target = col.GetComponentInParent<IDamageable>();
            if (target != null && done.Add(target))
            {
                target.TakeDamage(damage, col.bounds.center);
                StatusEffects.Apply(col, v.status);
            }
        }
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
        if (fxKey == "Cyclone" && v.lingerDuration <= 0f && SpellFX.UseMagicFX)
        {
            // the old Cyclone: a ground vortex that pulls enemies in for a few seconds and hurts them steadily, instead of one blast
            SpellImpact.CycloneStart(point, r, 5f, v.damage / 5f, damageTag);
            SpellFX.Play(Sfx.Explosion, point, 0.6f);
            return;
        }

        DamageArea(point, v, inAir, damageTag, v.damage);

        ExplosionFlash.Spawn(inAir ? point : point + Vector3.up * 0.5f, r, v.effectColor, fxKey);
        if (SpellFX.UseMagicFX)
        {
            Bottom(point, v, inAir, out Vector3 low, out Vector3 high);
            if (IsTall(v)) SpellImpact.SkyPillar(low, r, high.y - low.y, v.effectColor);
            if (r >= ColossalRadius) SpellImpact.Colossal(fxKey, point, r, HeightOf(v), v.effectColor, inAir);
        }

        if (v.lingerDuration > 0f) SpellZone.Spawn(point, v, damageTag, inAir, fxKey);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// An area that stays after the blast: every tick it damages (and re-applies the status to) what is inside, and it can drag enemies toward
// its axis. On the ground it is a column from the ground up; around a flier it hangs in the air where it was cast.
public class SpellZone : MonoBehaviour
{
    Vector3 point;
    SpellVariant v;
    string damageTag, fxKey;
    bool inAir;
    float born, nextTick, nextPuff;
    Material mat;
    LineRenderer[] rings;
    Color color;

    static readonly Collider[] buffer = new Collider[256];
    readonly System.Collections.Generic.HashSet<Transform> pulled = new System.Collections.Generic.HashSet<Transform>();

    public static void Spawn(Vector3 point, SpellVariant v, string damageTag, bool inAir, string fxKey)
    {
        var go = SceneGroups.Put(new GameObject("SpellZone"), "VFX/Misc");
        var z = go.AddComponent<SpellZone>();
        z.point = point; z.v = v; z.damageTag = damageTag; z.inAir = inAir; z.fxKey = fxKey;
        z.born = Time.time;
        z.nextTick = Time.time + Mathf.Max(v.lingerTickInterval, 0.1f);
        z.color = v.effectColor;
        z.mat = SpellVisuals.Make(v.effectColor, true);

        // rings stacked up the area so its reach can be read from the air
        AoeStrike.Bottom(point, v, inAir, out Vector3 low, out Vector3 high);
        int count = AoeStrike.IsTall(v) ? Mathf.Clamp(Mathf.RoundToInt((high.y - low.y) / 9f) + 1, 2, 7) : 1;
        z.rings = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            z.rings[i] = SpellVisuals.CreateRing(go.transform, z.mat, i == 0 ? 0.16f : 0.09f);
            float k = count == 1 ? 0f : i / (float)(count - 1);
            Vector3 centre = count == 1 && inAir ? point : Vector3.Lerp(low, high, k) + Vector3.up * 0.08f;
            SpellVisuals.SetRing(z.rings[i], centre, v.aoeRadius);
        }
    }

    void Update()
    {
        float age = Time.time - born;
        if (age >= v.lingerDuration) { Destroy(gameObject); return; }

        // fades in the last second, and flickers a little so it reads as active
        float fade = Mathf.Clamp01(v.lingerDuration - age);
        Color c = color; c.a = fade * (0.55f + 0.25f * Mathf.Sin(Time.time * 9f));
        SpellVisuals.SetColor(mat, c);

        if (v.lingerPull > 0f) Pull();

        if (Time.time >= nextTick)
        {
            nextTick = Time.time + Mathf.Max(v.lingerTickInterval, 0.1f);
            if (v.lingerTickDamage > 0f || v.status.type != StatusType.None)
                AoeStrike.DamageArea(point, v, inAir, damageTag, v.lingerTickDamage);
        }

        // small bursts of the spell's own effect somewhere inside, so the area never looks empty
        if (Time.time >= nextPuff && SpellFX.UseMagicFX)
        {
            nextPuff = Time.time + 0.22f;
            AoeStrike.Bottom(point, v, inAir, out Vector3 low, out Vector3 high);
            Vector2 flat = Random.insideUnitCircle * v.aoeRadius * 0.85f;
            float up = AoeStrike.IsTall(v) ? Random.value * Random.value * (high.y - low.y) : (inAir ? 0f : 0.3f);
            Vector3 at = (AoeStrike.IsTall(v) || !inAir ? low : point) + new Vector3(flat.x, up, flat.y);
            if (!SpellImpact.Play(fxKey, at, Vector3.up, 1.4f, color)) SpellFX.Sparks(at, color);
        }
    }

    // drags enemies toward the axis (toward the centre point as well when the area hangs in the air)
    void Pull()
    {
        int n = AoeStrike.Collect(point, v, inAir, buffer, 1.35f);
        pulled.Clear();
        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (!col.CompareTag(damageTag)) continue;
            var h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || !pulled.Add(h.transform)) continue;

            Vector3 to = point - h.transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float dist = flat.magnitude;
            float step = v.lingerPull * Time.deltaTime;

            var agent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (dist > 0.8f) agent.Move(flat.normalized * Mathf.Min(step, dist));
                continue;
            }

            Vector3 move = Vector3.zero;
            if (dist > 0.8f) move += flat.normalized * Mathf.Min(step, dist);
            if (inAir && Mathf.Abs(to.y) > 0.5f) move.y = Mathf.Sign(to.y) * Mathf.Min(step * 0.6f, Mathf.Abs(to.y));
            h.transform.position += move;
        }
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
