using System.Collections.Generic;
using UnityEngine;

// Weaver. Tap: a glowing thread unspools from the wand and snakes in a lazy S-curve to the target, then stitches to it.
// Hold: several threads are stitched between the witch and enemies; on release they snap tight and yank the targets together.
// Output: more threads and a longer reach.

// One strand of glowing thread (a wide soft glow and a thin white core) following an S-curve between two points.
class ThreadLine
{
    public LineRenderer glow, core;
    public Material glowMat, coreMat;
    Vector3 side;
    float amplitude;
    const int Points = 40;

    public ThreadLine(Transform parent, Color color, float width)
    {
        glowMat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 0.85f));
        coreMat = SpellUtil.Glow(new Color(1f, 0.95f, 0.98f, 1f));
        glow = Make(parent, glowMat, width * 3.2f);
        core = Make(parent, coreMat, width);
    }

    LineRenderer Make(Transform parent, Material mat, float width)
    {
        var go = new GameObject("Strand");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = Points;
        lr.sharedMaterial = mat;
        lr.widthMultiplier = width;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.5f));
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    public void Shape(Vector3 start, Vector3 end, float sign)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        Vector3 d = dist > 0.001f ? dir / dist : Vector3.forward;
        side = Vector3.Cross(d, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();
        side *= sign;
        amplitude = Mathf.Clamp(dist * 0.14f, 0.8f, 5f);
    }

    // draws the strand from the start to a fraction along the curve; slack 1 = full S, 0 = pulled straight; wobble = small lively ripples
    public void Draw(Vector3 start, Vector3 end, float reach, float slack, float wobble, float alpha)
    {
        Vector3 up = Vector3.up;
        for (int i = 0; i < Points; i++)
        {
            float u = reach * i / (Points - 1f);
            Vector3 p = Vector3.Lerp(start, end, u);
            float s = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u * 0.3f);
            p += side * (amplitude * slack * s);
            p += up * (amplitude * slack * 0.4f * Mathf.Sin(u * Mathf.PI));
            if (wobble > 0f) p += side * (wobble * 0.15f * Mathf.Sin(u * 24f - Time.time * 18f) * Mathf.Sin(u * Mathf.PI));
            glow.SetPosition(i, p);
            core.SetPosition(i, p);
        }
        SetAlpha(alpha);
    }

    public void SetAlpha(float a)
    {
        var g = glowMat.color; g.a = 0.85f * a; SpellVisuals.SetColor(glowMat, g);
        var c = coreMat.color; c.a = a; SpellVisuals.SetColor(coreMat, c);
    }

    public void SetWidth(float multiplier)
    {
        glow.widthMultiplier = multiplier * 3.2f;
        core.widthMultiplier = multiplier;
    }

    public void Destroy()
    {
        if (glowMat != null) Object.Destroy(glowMat);
        if (coreMat != null) Object.Destroy(coreMat);
    }
}

// a tapped thread: flies, stitches, tightens and fades
public class WeaverThread : MonoBehaviour
{
    PlayerSpellCaster owner;
    Health target;
    Vector3 end;
    float damage, speed, delay, sign;
    Color color;
    ThreadLine line;
    LineRenderer stitch;
    Material stitchMat;
    float t, phaseT;
    int phase;                // 0 flying, 1 stitched, 2 fading
    bool hit;

    const float Width = 0.07f;

    public static void Tap(SpellCastContext c)
    {
        var v = c.v;
        float reach = Mathf.Max(8f, v.laserRange);
        int count = Mathf.Max(1, v.projectileCount);

        var first = c.lockTarget != null ? c.lockTarget.GetComponentInParent<Health>() : null;
        if (first == null) first = SpellUtil.BestInCone(c.origin, c.aim, reach, 16f, c.damageTag);

        var targets = new List<Health>();
        if (first != null) targets.Add(first);
        if (count > 1)
        {
            var others = SpellUtil.Enemies(c.origin, reach, c.damageTag);
            others.Remove(first);
            others.Sort((a, b) => Vector3.Angle(c.aim, SpellUtil.CenterOf(a) - c.origin).CompareTo(Vector3.Angle(c.aim, SpellUtil.CenterOf(b) - c.origin)));
            foreach (var h in others) { if (targets.Count >= count) break; targets.Add(h); }
        }

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("WeaverThread");
            var w = go.AddComponent<WeaverThread>();
            w.owner = c.owner;
            w.color = v.effectColor;
            w.damage = v.damage;
            w.speed = Mathf.Max(20f, v.projectileSpeed);
            w.delay = i * 0.07f;
            w.sign = (i % 2 == 0) ? 1f : -1f;
            if (i < targets.Count) { w.target = targets[i]; w.end = SpellUtil.CenterOf(targets[i]); }
            else
            {
                // nothing to stitch to: the thread still snakes out along the aim, spread a little, and fades
                Vector3 dir = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * 7f, Vector3.up) * c.aim;
                w.end = c.origin + dir * Mathf.Min(reach, 28f);
            }
            w.line = new ThreadLine(go.transform, v.effectColor, Width);
            w.line.Shape(c.origin, w.end, w.sign);
            w.line.Draw(c.origin, w.end, 0f, 1f, 0f, 0f);
        }
        SpellFX.Play(Sfx.Cast, c.origin, 0.6f);
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }
        Vector3 start = owner.LaunchPoint;
        if (target != null && !target.IsDead) end = SpellUtil.CenterOf(target);

        if (delay > 0f) { delay -= Time.deltaTime; return; }

        line.Shape(start, end, sign);
        float dist = Vector3.Distance(start, end);

        switch (phase)
        {
            case 0:
                t += Time.deltaTime * speed / Mathf.Max(dist, 1f);
                line.Draw(start, end, Mathf.Clamp01(t), 1f, 1f, 1f);
                if (t >= 1f) Stitch(start);
                break;
            case 1:
                phaseT += Time.deltaTime;
                // the lazy S tightens as the stitch takes hold
                line.Draw(start, end, 1f, Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(phaseT / 0.9f)), 0.3f, 1f);
                UpdateStitch();
                if (phaseT >= 1.1f) { phase = 2; phaseT = 0f; }
                break;
            default:
                phaseT += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(phaseT / 0.35f);
                line.Draw(start, end, 1f, 0.1f, 0f, a);
                if (stitchMat != null) { var m = stitchMat.color; m.a = a; SpellVisuals.SetColor(stitchMat, m); }
                if (phaseT >= 0.35f) Destroy(gameObject);
                break;
        }
    }

    void Stitch(Vector3 start)
    {
        phase = 1;
        phaseT = 0f;
        if (target == null || target.IsDead) { phase = 2; return; }
        if (!hit)
        {
            hit = true;
            target.TakeDamage(damage, end);
            SpellFX.Sparks(end, color);
            SpellFX.Play(Sfx.Hit, end, 0.7f);
        }

        // a few crossed stitches around the point the thread is sewn into
        stitchMat = SpellUtil.Glow(new Color(1f, 0.9f, 0.95f, 1f));
        var go = new GameObject("Stitches");
        go.transform.SetParent(transform, false);
        stitch = go.AddComponent<LineRenderer>();
        stitch.useWorldSpace = true;
        stitch.positionCount = 11;
        stitch.sharedMaterial = stitchMat;
        stitch.widthMultiplier = 0.05f;
        stitch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void UpdateStitch()
    {
        if (stitch == null || target == null) return;
        Vector3 start = owner.LaunchPoint;
        Vector3 dir = (end - start).normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
        for (int i = 0; i < stitch.positionCount; i++)
        {
            float k = i / (stitch.positionCount - 1f);
            Vector3 along = end - dir * (0.2f + k * 1.4f);
            stitch.SetPosition(i, along + side * ((i % 2 == 0) ? 0.22f : -0.22f));
        }
    }

    void OnDestroy()
    {
        line?.Destroy();
        if (stitchMat != null) Destroy(stitchMat);
    }
}

// a held cast: threads drawn to several enemies, then a snap that yanks them together
public class WeaverStitch : MonoBehaviour
{
    class Strand { public Health h; public ThreadLine line; public float sign; public Vector3 from; }

    PlayerSpellCaster owner;
    readonly List<Strand> strands = new List<Strand>();
    float damage;
    Color color;
    float t;
    int phase;                       // 0 drawing out, 1 slack, 2 snap and yank, 3 fade
    Vector3 centroid;

    public static void Begin(SpellCastContext c)
    {
        var v = c.v;
        float reach = Mathf.Max(10f, v.laserRange * 0.8f);
        int count = Mathf.Max(3, v.projectileCount + 2);

        var found = SpellUtil.Enemies(c.origin, reach, c.damageTag);
        found.Sort((a, b) => Vector3.Angle(c.aim, SpellUtil.CenterOf(a) - c.origin).CompareTo(Vector3.Angle(c.aim, SpellUtil.CenterOf(b) - c.origin)));

        var go = new GameObject("WeaverStitch");
        var w = go.AddComponent<WeaverStitch>();
        w.owner = c.owner;
        w.damage = v.damage;
        w.color = v.effectColor;

        for (int i = 0; i < found.Count && w.strands.Count < count; i++)
        {
            if (Vector3.Angle(c.aim, SpellUtil.CenterOf(found[i]) - c.origin) > 75f) continue;
            var s = new Strand { h = found[i], sign = i % 2 == 0 ? 1f : -1f };
            s.line = new ThreadLine(go.transform, v.effectColor, 0.09f);
            w.strands.Add(s);
        }

        if (w.strands.Count == 0)
        {
            c.owner.Notify("Nothing to stitch");
            Destroy(go);
            return;
        }
        SpellFX.Play(Sfx.Cast, c.origin, 0.9f);
        CameraShake.Add(0.15f);
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        Vector3 start = owner.LaunchPoint;

        foreach (var s in strands)
            if (s.h != null && !s.h.IsDead) { s.from = SpellUtil.CenterOf(s.h); }

        switch (phase)
        {
            case 0:
            {
                float u = Mathf.Clamp01(t / 0.4f);
                foreach (var s in strands) { s.line.Shape(start, s.from, s.sign); s.line.Draw(start, s.from, u, 1f, 1f, 1f); }
                if (t >= 0.4f) { phase = 1; t = 0f; }
                break;
            }
            case 1:
            {
                foreach (var s in strands) { s.line.Shape(start, s.from, s.sign); s.line.Draw(start, s.from, 1f, Mathf.Lerp(1f, 0.7f, t / 0.35f), 0.8f, 1f); }
                if (t >= 0.35f) Snap();
                break;
            }
            case 2:
            {
                float k = Mathf.Clamp01(t / 0.3f);
                foreach (var s in strands)
                {
                    s.line.Shape(start, s.from, s.sign);
                    s.line.Draw(start, s.from, 1f, 0f, 0f, 1f);
                    s.line.SetWidth(Mathf.Lerp(0.2f, 0.07f, k));
                }
                if (t >= 0.3f) { phase = 3; t = 0f; Crush(); }
                break;
            }
            default:
            {
                float a = 1f - Mathf.Clamp01(t / 0.3f);
                foreach (var s in strands) { s.line.Shape(start, s.from, s.sign); s.line.Draw(start, s.from, 1f, 0f, 0f, a); }
                if (t >= 0.3f) Destroy(gameObject);
                break;
            }
        }
    }

    // the threads go tight: everything stitched is pulled toward the middle of the group
    void Snap()
    {
        phase = 2;
        t = 0f;
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var s in strands) if (s.h != null) { sum += s.h.transform.position; n++; }
        centroid = n > 0 ? sum / n : transform.position;

        SpellFX.Play(Sfx.Explosion, owner.LaunchPoint, 0.5f);
        CameraShake.Add(0.35f);
        foreach (var s in strands)
        {
            if (s.h == null) continue;
            s.h.TakeDamage(damage, s.from);
            SpellFX.Sparks(s.from, color);
            StartCoroutine(Yank(s.h.transform, 0.65f));
        }
    }

    System.Collections.IEnumerator Yank(Transform body, float fraction)
    {
        Vector3 total = centroid - body.position;
        total.y = 0f;
        total *= fraction;
        float time = 0.28f, done = 0f;
        while (done < time && body != null)
        {
            float step = Time.deltaTime;
            float k0 = done / time, k1 = Mathf.Min(1f, (done + step) / time);
            float ease0 = 1f - (1f - k0) * (1f - k0), ease1 = 1f - (1f - k1) * (1f - k1);
            SpellUtil.MoveBody(body, total * (ease1 - ease0));
            done += step;
            yield return null;
        }
    }

    // enemies that were dragged next to each other collide
    void Crush()
    {
        foreach (var a in strands)
        {
            if (a.h == null || a.h.IsDead) continue;
            int near = 0;
            foreach (var b in strands)
                if (b != a && b.h != null && Vector3.Distance(a.h.transform.position, b.h.transform.position) < 3.2f) near++;
            if (near > 0)
            {
                a.h.TakeDamage(damage * 0.3f * near, a.h.transform.position + Vector3.up);
                SpellFX.Sparks(a.h.transform.position + Vector3.up, Color.white);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var s in strands) s.line?.Destroy();
    }
}
