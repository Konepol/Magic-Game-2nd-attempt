using UnityEngine;

// Lantern. A floating light.
// Tap: place it at the aim. It lights dark places and marks the enemies near it (other spells home on marked enemies).
// Hold: throw it; where it lands, enemies are lured to it. Held longer: a beacon, a tall light you can teleport to (a self-teleport for now:
// tap Lantern while a beacon stands and you go to it).
// Output: a bigger, brighter light that marks further.

// an enemy marked by a lantern: homing shots and Weaver prefer it
public class LanternMark : MonoBehaviour
{
    public float until;

    public static void Mark(GameObject enemy, float seconds)
    {
        var m = enemy.GetComponentInParent<LanternMark>();
        if (m == null) m = enemy.AddComponent<LanternMark>();
        m.until = Time.time + seconds;
    }

    public static bool Is(GameObject go)
    {
        if (go == null) return false;
        var m = go.GetComponentInParent<LanternMark>();
        return m != null && Time.time < m.until;
    }
}

public class LanternLight : MonoBehaviour
{
    public static LanternLight Beacon;      // the standing beacon, if any

    string damageTag;
    float life, markRadius, size, baseHeight;
    bool isBeacon;
    Light lamp;
    Material coreMat, haloMat, columnMat;
    Transform core;
    LineRenderer halo;
    float t, nextMark;
    Color color;

    public static LanternLight Spawn(Vector3 position, SpellCastContext c, float sizeScale, bool beacon)
    {
        var go = new GameObject(beacon ? "LanternBeacon" : "Lantern");
        go.transform.position = position;
        var l = go.AddComponent<LanternLight>();
        l.damageTag = c.damageTag;
        l.color = c.v.effectColor;
        l.size = Mathf.Max(0.5f, sizeScale);
        l.life = beacon ? 90f : Mathf.Max(8f, c.v.barrierDuration);
        l.markRadius = Mathf.Max(6f, c.v.aoeRadius);
        l.isBeacon = beacon;
        l.baseHeight = position.y;
        l.Build();
        if (beacon)
        {
            if (Beacon != null) Destroy(Beacon.gameObject);
            Beacon = l;
        }
        return l;
    }

    public void EnableLure(float radius, float seconds) => Decoy.Make(gameObject, radius, seconds);

    void Build()
    {
        float s = size * (isBeacon ? 1.6f : 1f);
        coreMat = SpellUtil.Glow(new Color(1f, 0.92f, 0.65f, 1f));
        core = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        core.SetParent(transform, false);
        Destroy(core.GetComponent<Collider>());
        core.GetComponent<Renderer>().sharedMaterial = coreMat;
        core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        core.localScale = Vector3.one * 0.4f * s;

        haloMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.6f));
        halo = SpellVisuals.CreateRing(transform, haloMat, 0.05f * s);

        var lg = new GameObject("Light");
        lg.transform.SetParent(transform, false);
        lamp = lg.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = Color.Lerp(color, new Color(1f, 0.9f, 0.7f), 0.5f);
        lamp.range = (isBeacon ? 34f : 15f) * size;
        lamp.intensity = (isBeacon ? 6f : 3f) * Mathf.Sqrt(size);
        lamp.shadows = LightShadows.None;

        if (isBeacon)
        {
            columnMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.28f));
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            col.SetParent(transform, false);
            Destroy(col.GetComponent<Collider>());
            col.GetComponent<Renderer>().sharedMaterial = columnMat;
            col.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            col.localScale = new Vector3(1.2f * size, 30f, 1.2f * size);
            col.localPosition = Vector3.up * 28f;
        }
    }

    void Update()
    {
        t += Time.deltaTime;
        life -= Time.deltaTime;
        float fade = Mathf.Clamp01(life / 1.5f);

        float bob = Mathf.Sin(t * 2.2f) * 0.15f;
        transform.position = new Vector3(transform.position.x, baseHeight + bob, transform.position.z);
        float flicker = 1f + 0.07f * Mathf.Sin(t * 13f) + 0.04f * Mathf.Sin(t * 29f);
        lamp.intensity = (isBeacon ? 6f : 3f) * Mathf.Sqrt(size) * flicker * fade;
        SpellUtil.SetBillboardRing(halo, transform.position, 0.45f * size * (isBeacon ? 1.6f : 1f) * flicker, Camera.main);
        var hc = haloMat.color; hc.a = 0.6f * fade; SpellVisuals.SetColor(haloMat, hc);

        if (Time.time >= nextMark)
        {
            nextMark = Time.time + 0.3f;
            foreach (var h in SpellUtil.Enemies(transform.position, markRadius * size, damageTag))
                LanternMark.Mark(h.gameObject, 1.2f);
        }
        if (life <= 0f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Beacon == this) Beacon = null;
        if (coreMat != null) Destroy(coreMat);
        if (haloMat != null) Destroy(haloMat);
        if (columnMat != null) Destroy(columnMat);
    }
}

// the throw: an arc to the landing point, then the lantern lures enemies there
public class LanternThrow : MonoBehaviour
{
    Vector3 from, to;
    float duration, t;
    SpellCastContext c;
    float size;
    Material mat;

    public static void Begin(SpellCastContext c, Vector3 landing, float size)
    {
        var go = new GameObject("LanternThrow");
        var th = go.AddComponent<LanternThrow>();
        th.c = c;
        th.from = c.origin;
        th.to = landing;
        th.size = size;
        th.duration = Mathf.Clamp(Vector3.Distance(c.origin, landing) / 30f, 0.4f, 1.3f);

        th.mat = SpellUtil.Glow(new Color(1f, 0.9f, 0.6f, 1f));
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        ball.SetParent(go.transform, false);
        Destroy(ball.GetComponent<Collider>());
        ball.GetComponent<Renderer>().sharedMaterial = th.mat;
        ball.localScale = Vector3.one * 0.45f;
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.4f; trail.widthMultiplier = 0.3f; trail.sharedMaterial = th.mat;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        Vector3 p = Vector3.Lerp(from, to, k);
        p.y += Mathf.Sin(k * Mathf.PI) * Mathf.Min(6f, Vector3.Distance(from, to) * 0.25f);
        transform.position = p;
        if (k >= 1f)
        {
            var l = LanternLight.Spawn(to, c, size, false);
            l.EnableLure(40f * size, 7f);
            SpellFX.Sparks(to, c.v.effectColor);
            SpellFX.Play(Sfx.Hit, to, 0.6f);
            Destroy(gameObject);
        }
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

public static class LanternSpell
{
    static Vector3 Target(SpellCastContext c, float maxDist)
    {
        Vector3 p = c.lockTarget != null ? c.lockTarget.position : c.aimPoint;
        Vector3 flat = p - c.root.position; flat.y = 0f;
        if (flat.magnitude > maxDist) p = c.root.position + flat.normalized * maxDist;
        return SpellUtil.GroundAt(p) + Vector3.up * 1.6f;
    }

    public static void Place(SpellCastContext c)
    {
        // a beacon is standing: tapping goes to it (self-teleport placeholder until multiplayer)
        if (LanternLight.Beacon != null)
        {
            Vector3 ground = SpellUtil.GroundAt(LanternLight.Beacon.transform.position);
            PetalBurst.Spawn(c.root.position + Vector3.up, c.v.effectColor, false);
            SpellUtil.TeleportPlayer(c.root, ground + Vector3.up * 0.2f);
            PetalBurst.Spawn(ground + Vector3.up, c.v.effectColor, true);
            Object.Destroy(LanternLight.Beacon.gameObject);
            c.owner.Notify("Beacon");
            return;
        }
        float size = Mathf.Max(0.6f, c.v.projectileScale);
        LanternLight.Spawn(Target(c, 24f), c, size, false);
        SpellFX.Play(Sfx.Ready, c.origin, 0.7f);
    }

    public static void Throw(SpellCastContext c)
    {
        float size = Mathf.Max(0.6f, c.v.projectileScale);
        LanternThrow.Begin(c, Target(c, Mathf.Max(20f, c.v.laserRange)), size);
        SpellFX.Play(Sfx.Cast, c.origin, 0.7f);
    }

    public static void Beacon(SpellCastContext c)
    {
        float size = Mathf.Max(0.6f, c.v.projectileScale);
        LanternLight.Spawn(Target(c, 30f), c, size, true);
        SpellFX.Play(Sfx.Charge, c.origin, 1f);
        CameraShake.Add(0.25f);
    }
}
