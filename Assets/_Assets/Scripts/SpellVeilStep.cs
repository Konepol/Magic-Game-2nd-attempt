using System.Collections.Generic;
using UnityEngine;

// Veil Step. A petal blink spell.
// Tap: a short blink toward the aim; the petals dissolve where you were and re-form where you land.
// Hold: a star gate opens ahead; you fly through it at speed, leaving a fading afterimage that enemies go for.
// Passive (toggled in the spell list, costs mana per use): the broom's quick dash becomes a blink.
// Output: a longer blink and a longer dash.

// petals flying outward (the body dissolving) or inward (re-forming)
public class PetalBurst : MonoBehaviour
{
    class Petal { public Transform t; public Vector3 dir; public float spin; }
    readonly List<Petal> petals = new List<Petal>();
    float t, life = 0.6f;
    bool converge;
    Material mat;
    Vector3 center;

    public static void Spawn(Vector3 center, Color color, bool converge)
    {
        var go = new GameObject(converge ? "PetalsIn" : "PetalsOut");
        go.transform.position = center;
        var b = go.AddComponent<PetalBurst>();
        b.converge = converge;
        b.center = center;
        b.mat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.9f));
        for (int i = 0; i < 16; i++)
        {
            var p = new Petal();
            var g = new GameObject("Petal");
            g.transform.SetParent(go.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = PetalMesh.Get();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = b.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.localScale = Vector3.one * Random.Range(0.35f, 0.6f);
            p.t = g.transform;
            p.dir = Random.onUnitSphere;
            p.dir.y = p.dir.y * 0.6f + 0.25f;
            p.dir.Normalize();
            p.spin = Random.Range(-540f, 540f);
            b.petals.Add(p);
        }
        b.Place(0f);
    }

    void Place(float k)
    {
        float dist = converge ? Mathf.Lerp(2.6f, 0f, k * k) : Mathf.Lerp(0f, 2.6f, 1f - (1f - k) * (1f - k));
        float scale = converge ? Mathf.Lerp(0.2f, 1f, k) : Mathf.Lerp(1f, 0.1f, k);
        foreach (var p in petals)
        {
            p.t.position = center + p.dir * dist;
            p.t.rotation = Quaternion.LookRotation(p.dir) * Quaternion.Euler(90f, p.spin * k, 0f);
            p.t.localScale = Vector3.one * (0.45f * scale);
        }
        var c = mat.color; c.a = 0.9f * (converge ? Mathf.Clamp01(k * 3f) : 1f - k * k); SpellVisuals.SetColor(mat, c);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        Place(k);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// the gate: a ring with a five-pointed star, opening then closing
public class StarGate : MonoBehaviour
{
    LineRenderer ring, star;
    Material mat;
    float t, life = 1.3f, size;
    Quaternion facing;

    public static void Spawn(Vector3 position, Vector3 forward, Color color, float size)
    {
        var go = new GameObject("StarGate");
        go.transform.position = position;
        var g = go.AddComponent<StarGate>();
        g.size = size;
        g.facing = Quaternion.LookRotation(forward);
        g.mat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.95f));
        g.ring = Make(go.transform, g.mat, 0.16f, 64);
        g.star = Make(go.transform, g.mat, 0.1f, 6);
        g.star.loop = false;
    }

    static LineRenderer Make(Transform parent, Material m, float width, int points)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = points;
        lr.sharedMaterial = m;
        lr.widthMultiplier = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    void Update()
    {
        t += Time.deltaTime;
        float open = Mathf.Clamp01(t / 0.25f);
        float close = Mathf.Clamp01((life - t) / 0.3f);
        float r = size * (1f - Mathf.Pow(1f - open, 3f)) * close;
        float spin = t * 80f;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, transform.position + facing * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
        }
        // a star: every second point of five, closed
        for (int i = 0; i < 6; i++)
        {
            float a = ((i * 2) % 5) / 5f * Mathf.PI * 2f + spin * Mathf.Deg2Rad;
            star.SetPosition(i, transform.position + facing * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (r * 0.92f));
        }
        if (t >= life) Destroy(gameObject);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// what is left where the witch was: a fading glow enemies go for
public class AfterImage : MonoBehaviour
{
    Material mat;
    float t, life;

    public static void Spawn(Vector3 position, Color color, float life)
    {
        var go = new GameObject("AfterImage");
        go.transform.position = position;
        var a = go.AddComponent<AfterImage>();
        a.life = life;
        a.mat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.35f));
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        body.SetParent(go.transform, false);
        Destroy(body.GetComponent<Collider>());
        body.localPosition = Vector3.up * 1f;
        body.localScale = new Vector3(0.8f, 1f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = a.mat;
        body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Decoy.Make(go, 60f, life);
    }

    void Update()
    {
        t += Time.deltaTime;
        var c = mat.color; c.a = 0.35f * Mathf.Clamp01((life - t) / 1.2f) * (0.8f + 0.2f * Mathf.Sin(t * 9f)); SpellVisuals.SetColor(mat, c);
        if (t >= life) Destroy(gameObject);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// the launch on the ground: an extra push along a line that starts at once, is fastest at the start and eases out.
// It is added on top of normal movement (nothing is paused), so WASD and the jump still work while it carries you.
public class VeilDash : MonoBehaviour
{
    CharacterController cc;
    Vector3 dir;
    float peakSpeed, total, t;

    public static void Begin(Transform root, Vector3 dir, float speed, float seconds)
    {
        var d = root.gameObject.AddComponent<VeilDash>();
        d.cc = root.GetComponent<CharacterController>();
        d.dir = dir;
        d.peakSpeed = speed;
        d.total = Mathf.Max(0.1f, seconds);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / total);
        float speed = peakSpeed * (1f - k * k);            // ease out: a hard launch that bleeds off
        if (cc != null && cc.enabled) cc.Move(dir * speed * Time.deltaTime);
        else transform.position += dir * speed * Time.deltaTime;
        if (k >= 1f) Destroy(this);
    }
}

public static class VeilStep
{
    // how far the way ahead is clear for a body about a metre wide
    static float Clear(Vector3 from, Vector3 dir, float dist)
    {
        int mask = ~((1 << 2) | (1 << LayerMask.NameToLayer("Player")));
        if (Physics.SphereCast(from + Vector3.up, 0.45f, dir, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            return Mathf.Max(0f, hit.distance - 0.5f);
        return dist;
    }

    public static void DashBlink(Transform root, Vector3 dir, float dist, Color color)
    {
        dir.Normalize();
        float d = Clear(root.position, dir, dist);
        PetalBurst.Spawn(root.position + Vector3.up, color, false);
        SpellUtil.TeleportPlayer(root, root.position + dir * d);
        PetalBurst.Spawn(root.position + Vector3.up, color, true);
        SpellFX.Play(Sfx.Ready, root.position, 0.6f);
        CameraShake.Add(0.08f);
    }

    public static void Blink(SpellCastContext c)
    {
        float dist = Mathf.Max(4f, c.v.laserRange);
        Vector3 dir = c.aim;
        DashBlink(c.root, dir, dist, c.v.effectColor);
    }

    // the held form is a launch: the player is thrown along the aim at once (no standing still), through a star gate that opens ahead,
    // leaving an afterimage behind that enemies go for
    public static void Gate(SpellCastContext c)
    {
        Vector3 dir = c.aim;
        Vector3 start = c.root.position;
        float distance = Mathf.Max(10f, c.v.laserRange);
        float size = 2.3f * Mathf.Max(0.7f, c.v.projectileScale);
        const float seconds = 0.45f;

        StarGate.Spawn(start + Vector3.up * 1.1f + dir * 5f, dir, c.v.effectColor, size);
        AfterImage.Spawn(start, c.v.effectColor, 4f);
        PetalBurst.Spawn(start + Vector3.up, c.v.effectColor, false);

        // the distance covered by an ease-out launch is peak * seconds * 2/3
        float peak = distance / (seconds * (2f / 3f));
        var broom = c.root.GetComponent<BroomFlight>();
        if (broom != null && broom.IsFlying) broom.Launch(dir, distance / seconds, seconds);      // the broom holds a steady speed for the burst
        else VeilDash.Begin(c.root, dir, peak, seconds);

        SpellFX.Play(Sfx.Charge, start, 0.8f);
        CameraShake.Add(0.2f);
    }
}
