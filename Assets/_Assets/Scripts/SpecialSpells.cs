using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Everything a hand-coded spell needs about the cast that just happened.
public class SpellCastContext
{
    public PlayerSpellCaster owner;
    public SpellData spell;
    public SpellVariant v;           // the form being cast, already scaled by the output level
    public float eff;                // effective output level (1 = normal)
    public Vector3 origin;           // the wand tip
    public Vector3 aim;              // direction to the aimed point
    public Vector3 aimPoint;         // where the crosshair points in the world
    public Transform lockTarget;     // the locked enemy, if any
    public string damageTag;
    public Transform root;           // the player (the CharacterController's transform)
    public Mana mana;
}

// Picks which hand-coded behaviour a variant of kind Special runs, by its 'special' id.
public static class SpecialSpells
{
    public static bool Cast(SpellCastContext c)
    {
        switch (c.v.special)
        {
            case "weaver_tap": WeaverThread.Tap(c); return true;
            case "weaver_hold": WeaverStitch.Begin(c); return true;
            case "moon_blade": MoonBlade.Cast(c); return true;
            case "moon_small": MoonCollapse.Cast(c, false); return true;
            case "moon_great": MoonCollapse.Cast(c, true); return true;
            case "bloom_parry": BloomParry.Cast(c); return true;
            case "bloom_dome": BendDome.Cast(c); return true;
            case "lantern_place": LanternSpell.Place(c); return true;
            case "lantern_throw": LanternSpell.Throw(c); return true;
            case "lantern_beacon": LanternSpell.Beacon(c); return true;
            case "veil_blink": VeilStep.Blink(c); return true;
            case "veil_gate": VeilStep.Gate(c); return true;
            default:
                Debug.LogWarning("[Spell] Unknown special spell id '" + c.v.special + "'");
                return false;
        }
    }
}

// Small helpers shared by the hand-coded spells.
public static class SpellUtil
{
    // additive, glowing material (alpha is the brightness)
    public static Material Glow(Color c)
    {
        var m = SpellVisuals.Make(c, true);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 1f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        return m;
    }

    // normal (alpha blended) material, drawn on both sides
    public static Material Solid(Color c)
    {
        var m = SpellVisuals.Make(c, true);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        return m;
    }

    public static Vector3 CenterOf(Health h)
    {
        var col = h.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : h.transform.position + Vector3.up;
    }

    static readonly Collider[] buffer = new Collider[512];

    // enemies live on the Enemy layer; looking only there keeps a dense forest of colliders from crowding them out of the result
    static int EnemyMask
    {
        get
        {
            int layer = LayerMask.NameToLayer("Enemy");
            return layer >= 0 ? 1 << layer : ~0;
        }
    }

    // every living enemy (by tag) within the radius, once each
    public static List<Health> Enemies(Vector3 center, float radius, string tag)
    {
        var list = new List<Health>();
        int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, EnemyMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = buffer[i].GetComponentInParent<Health>();
            if (h == null || h.IsDead || list.Contains(h)) continue;
            if (!h.gameObject.CompareTag(tag) && !buffer[i].CompareTag(tag)) continue;
            list.Add(h);
        }
        return list;
    }

    // the enemy closest to the aim line within a cone; a lantern-marked enemy wins
    public static Health BestInCone(Vector3 origin, Vector3 dir, float range, float coneDegrees, string tag, Health exclude = null)
    {
        Health best = null;
        float bestScore = float.MaxValue;
        foreach (var h in Enemies(origin, range, tag))
        {
            if (h == exclude) continue;
            Vector3 to = CenterOf(h) - origin;
            float angle = Vector3.Angle(dir, to);
            bool marked = LanternMark.Is(h.gameObject);
            if (angle > coneDegrees && !marked) continue;
            float score = angle / coneDegrees + to.magnitude / range - (marked ? 2f : 0f);
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    // pushes something that moves by itself (an agent, a controller or a plain body) by this much
    public static void MoveBody(Transform t, Vector3 delta)
    {
        if (t == null) return;
        var agent = t.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.Move(delta); return; }
        var cc = t.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) { cc.Move(delta); return; }
        t.position += delta;
    }

    public static void TeleportPlayer(Transform root, Vector3 position)
    {
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) { cc.enabled = false; root.position = position; cc.enabled = true; }
        else root.position = position;
    }

    // a ring of points in the plane facing the camera (for rims that should always look round)
    public static void SetBillboardRing(LineRenderer lr, Vector3 center, float radius, Camera cam)
    {
        Vector3 r = cam != null ? cam.transform.right : Vector3.right;
        Vector3 u = cam != null ? cam.transform.up : Vector3.up;
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, center + (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * radius);
        }
    }

    // the ground under a point (or the point itself if there is none)
    public static Vector3 GroundAt(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~((1 << 2) | (1 << LayerMask.NameToLayer("Player"))), QueryTriggerInteraction.Ignore))
            return hit.point;
        return p;
    }
}

// An enemy that sees one of these in range goes for it instead of the player (an afterimage, a thrown lantern).
public class Decoy : MonoBehaviour
{
    public static readonly List<Decoy> All = new List<Decoy>();
    public float radius = 40f;
    public float until;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static Decoy Make(GameObject go, float radius, float life)
    {
        var d = go.AddComponent<Decoy>();
        d.radius = radius;
        d.until = Time.time + life;
        return d;
    }

    public static Decoy Pick(Vector3 from)
    {
        Decoy best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < All.Count; i++)
        {
            var d = All[i];
            if (d == null || Time.time > d.until) continue;
            float dist = Vector3.Distance(from, d.transform.position);
            if (dist > d.radius || dist >= bestD) continue;
            best = d; bestD = dist;
        }
        return best;
    }
}

// A glowing shape that grows over the wand while a spell is chanted (the first one is Hollow Moon's dark disc).
public class WandChargeVisual : MonoBehaviour
{
    GameObject body;
    LineRenderer rim;
    Material bodyMat, rimMat;
    float size = 0.25f, shown;
    bool releasing;
    float releaseLeft = 0.2f;

    public static WandChargeVisual Create(string kind, Color color)
    {
        var go = new GameObject("WandChargeVisual_" + kind);
        var o = go.AddComponent<WandChargeVisual>();
        o.body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        o.body.transform.SetParent(go.transform, false);
        Object.Destroy(o.body.GetComponent<Collider>());
        o.bodyMat = SpellVisuals.Make(new Color(0.03f, 0.0f, 0.08f, 1f), false);
        var r = o.body.GetComponent<Renderer>();
        r.sharedMaterial = o.bodyMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        o.rimMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.9f));
        o.rim = SpellVisuals.CreateRing(go.transform, o.rimMat, 0.05f);
        o.rim.useWorldSpace = true;
        return o;
    }

    public void Set(Vector3 position, float newSize)
    {
        transform.position = position;
        size = newSize;
    }

    public void Release() { releasing = true; }

    void LateUpdate()
    {
        shown = Mathf.Lerp(shown, size, 1f - Mathf.Exp(-10f * Time.deltaTime));
        float scale = shown * 0.5f;
        if (releasing)
        {
            releaseLeft -= Time.deltaTime;
            if (releaseLeft <= 0f) { Destroy(gameObject); return; }
            scale *= releaseLeft / 0.2f;
        }

        body.transform.localScale = Vector3.one * scale;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 14f);
        SpellUtil.SetBillboardRing(rim, transform.position, scale * 0.5f * 1.12f * pulse, Camera.main);
    }

    void OnDestroy()
    {
        if (bodyMat != null) Destroy(bodyMat);
        if (rimMat != null) Destroy(rimMat);
    }
}
