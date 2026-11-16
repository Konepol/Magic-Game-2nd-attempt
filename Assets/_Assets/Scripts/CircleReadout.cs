using System.Collections.Generic;
using UnityEngine;

// What a casting circle tells the player about mana output, drawn around the circle itself (not in the HUD):
//  - a gauge ring that fills with the output level, with a white tick at the safe level and a red tick at this spell's backfire limit;
//    it turns orange past the safe level and red near the limit, and shakes and sparks when the limit is passed;
//  - pips for how many shots / threads the spell fires at this output (only when more than one);
//  - chevrons that circle faster and in greater number when output makes this form faster.
// It follows the circle's own pose, so it stays attached while the witch moves.
public class CircleReadout : MonoBehaviour
{
    const int Segments = 72;

    static Material sharedGlow;
    MaterialPropertyBlock block;
    LineRenderer back, fill, safeTick, limitTick;
    readonly List<LineRenderer> pips = new List<LineRenderer>();
    readonly List<LineRenderer> chevrons = new List<LineRenderer>();
    float chevronAngle;
    float nextSpark;

    public static CircleReadout Create(Transform parent)
    {
        var go = new GameObject("OutputReadout");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<CircleReadout>();
        r.block = new MaterialPropertyBlock();
        if (sharedGlow == null) sharedGlow = SpellUtil.Glow(Color.white);
        r.back = r.Line("Back", Segments + 1, true);
        r.fill = r.Line("Fill", Segments + 1, false);
        r.safeTick = r.Line("SafeTick", 2, false);
        r.limitTick = r.Line("LimitTick", 2, false);
        return r;
    }

    LineRenderer Line(string name, int points, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = points;
        lr.sharedMaterial = sharedGlow;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void Paint(LineRenderer lr, Color c, float width)
    {
        block.Clear();
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        lr.SetPropertyBlock(block);
        lr.widthMultiplier = width;
    }

    static Vector3 P(Vector3 center, Quaternion rot, float radius, float angle) =>
        center + rot * (new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);

    // eff: effective output, safe: the output the cost starts to climb at, limit: this spell's backfire limit,
    // extraCount: how many shots / threads the form fires at this output, speedExp: how strongly output speeds this form up
    public void Draw(Vector3 center, Quaternion rot, float radius, float eff, float safe, float limit, int count, float speedExp, Color spellColor, float alpha, float dt)
    {
        // (no backfire warning here: no limit tick, shaking, sparks or red; the gauge only shows the output level)
        const float danger = 0f;
        float span = 4f;                                  // the full ring is output 4
        float f = Mathf.Clamp01(eff / span);

        // colour: cool at low output, gold past the safe level
        Color col = Color.Lerp(spellColor, Color.white, 0.35f);
        if (eff > safe) col = Color.Lerp(col, new Color(1f, 0.72f, 0.28f), Mathf.Clamp01((eff - safe) / 1.5f));
        col.a = 1f;

        float w = Mathf.Max(0.02f, radius * 0.05f);
        Color backC = new Color(1f, 1f, 1f, 0.16f * alpha);
        for (int i = 0; i <= Segments; i++) back.SetPosition(i, P(center, rot, radius, Mathf.PI * 0.5f - i / (float)Segments * Mathf.PI * 2f));
        Paint(back, backC, w * 0.5f);

        // the filled arc runs clockwise from the top
        int n = Mathf.Max(2, Mathf.CeilToInt(f * Segments) + 1);
        fill.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float k = Mathf.Min(1f, i / (float)(n - 1)) * f;
            fill.SetPosition(i, P(center, rot, radius, Mathf.PI * 0.5f - k * Mathf.PI * 2f));
        }
        col.a *= alpha;
        Paint(fill, col, w);

        // a white tick at the safe level (where the cost starts to climb)
        Tick(safeTick, center, rot, radius, Mathf.Clamp01(safe / span), new Color(1f, 1f, 1f, 0.8f * alpha), w * 0.7f);
        limitTick.enabled = false;

        DrawPips(center, rot, radius, count, col, w, alpha);
        DrawChevrons(center, rot, radius, eff, speedExp, spellColor, w, alpha, dt);

    }

    void Tick(LineRenderer lr, Vector3 center, Quaternion rot, float radius, float k, Color c, float width)
    {
        float a = Mathf.PI * 0.5f - k * Mathf.PI * 2f;
        lr.SetPosition(0, P(center, rot, radius * 0.9f, a));
        lr.SetPosition(1, P(center, rot, radius * 1.12f, a));
        Paint(lr, c, width);
    }

    void DrawPips(Vector3 center, Quaternion rot, float radius, int count, Color c, float w, float alpha)
    {
        count = count > 1 ? Mathf.Min(count, 12) : 0;
        while (pips.Count < count) pips.Add(Line("Pip", 2, false));
        for (int i = 0; i < pips.Count; i++)
        {
            bool on = i < count;
            pips[i].enabled = on;
            if (!on) continue;
            // spread evenly around the inside of the gauge ring, starting at the top
            float a = Mathf.PI * 0.5f - (i + 0.5f) / count * Mathf.PI * 2f;
            pips[i].SetPosition(0, P(center, rot, radius * 0.8f, a));
            pips[i].SetPosition(1, P(center, rot, radius * 0.7f, a));
            Paint(pips[i], new Color(c.r, c.g, c.b, 0.9f * alpha), w * 1.4f);
        }
    }

    void DrawChevrons(Vector3 center, Quaternion rot, float radius, float eff, float speedExp, Color spellColor, float w, float alpha, float dt)
    {
        int want = speedExp > 0.01f ? Mathf.Clamp(Mathf.RoundToInt(1f + (eff - 1f) * speedExp * 6f), 1, 6) : 0;
        if (want < 1 && speedExp > 0.01f) want = 1;
        while (chevrons.Count < want) chevrons.Add(Line("Chevron", 3, false));

        chevronAngle -= (40f + 70f * Mathf.Max(0f, Mathf.Pow(eff, Mathf.Max(0.2f, speedExp * 2f)) - 1f)) * Mathf.Deg2Rad * dt;
        for (int i = 0; i < chevrons.Count; i++)
        {
            bool on = i < want;
            chevrons[i].enabled = on;
            if (!on) continue;
            float a = chevronAngle + i / (float)want * Mathf.PI * 2f;
            Vector3 p = P(center, rot, radius * 0.9f, a);
            Vector3 radial = rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 tangent = rot * new Vector3(Mathf.Sin(a), -Mathf.Cos(a), 0f);          // clockwise
            float s = radius * 0.07f;
            chevrons[i].SetPosition(0, p - tangent * s + radial * s * 0.8f);
            chevrons[i].SetPosition(1, p + tangent * s);
            chevrons[i].SetPosition(2, p - tangent * s - radial * s * 0.8f);
            Paint(chevrons[i], new Color(spellColor.r, spellColor.g, spellColor.b, 0.85f * alpha), w * 1.1f);
        }
    }
}
