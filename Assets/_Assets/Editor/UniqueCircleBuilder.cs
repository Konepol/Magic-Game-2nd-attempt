using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Unique Spell Circles
// Gives every spell and fusion its own casting circle: new rune images drawn for each element (flame petals, snowflake, spiral,
// spikes, fangs, sun rays, lightning ring, scattered dots, crystal, square runes) combined in a layout of its own, with its own spin,
// tilt and ready colour. Fusions mix the images of the spells they are made of and add a layer of their own.
// Everything is a normal editable asset. Running it again overwrites the generated images and these circles.
public static class UniqueCircleBuilder
{
    const string ImageDir = "Assets/_Assets/Images/Circles";
    const string CircleDir = "Assets/_Assets/Circles";
    const string WeaponDir = "Assets/_Assets/Weapons";
    const string ComboDir = "Assets/_Assets/Weapons/Combos";
    const int N = 512;

    static readonly Dictionary<string, Texture2D> Tex = new Dictionary<string, Texture2D>();

    [MenuItem("Tools/Magic/Build Unique Spell Circles")]
    public static void Build()
    {
        Directory.CreateDirectory(ImageDir);
        Directory.CreateDirectory(CircleDir);
        Tex.Clear();

        // ---------- images ----------
        Make("Flame_Petals", c =>
        {
            c.Ring(0.47f, 0.008f); c.Ring(0.14f, 0.008f);
            for (int k = 0; k < 8; k++)
            {
                float a0 = 2f * Mathf.PI * k / 8f;
                var radial = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var tangent = new Vector2(-radial.y, radial.x);
                Vector2 pl = default, pr = default;
                for (int i = 0; i <= 16; i++)
                {
                    float t = i / 16f;
                    Vector2 mid = radial * Mathf.Lerp(0.17f, 0.44f, t) + tangent * (0.035f * Mathf.Sin(2f * Mathf.PI * t) * t);
                    float hw = 0.06f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.75f);
                    Vector2 l = mid + tangent * hw, r = mid - tangent * hw;
                    if (i > 0) { c.Seg(pl, l, 0.010f); c.Seg(pr, r, 0.010f); }
                    pl = l; pr = r;
                }
            }
        });

        Make("Snowflake", c =>
        {
            c.Poly(6, 0.47f, 0f, 0.008f); c.Ring(0.12f, 0.008f);
            for (int k = 0; k < 6; k++)
            {
                float a0 = 2f * Mathf.PI * k / 6f;
                var d = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                c.Seg(d * 0.12f, d * 0.43f, 0.010f);
                foreach (float at in new[] { 0.24f, 0.34f })
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float ab = a0 + side * Mathf.PI / 3f;
                        var b = new Vector2(Mathf.Cos(ab), Mathf.Sin(ab));
                        c.Seg(d * at, d * at + b * 0.075f, 0.008f);
                    }
                c.FillDot(d * 0.45f, 0.012f);
            }
        });

        Make("Spiral", c =>
        {
            c.Ring(0.47f, 0.008f); c.Arcs(0.43f, 0.008f, 12, 0.5f, 0f);
            for (int arm = 0; arm < 4; arm++)
            {
                float baseAng = 2f * Mathf.PI * arm / 4f;
                var armPts = new List<Vector2>();
                for (int i = 0; i <= 36; i++)
                {
                    float t = i / 36f;
                    float ang = baseAng + t * 2.6f;
                    armPts.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(0.05f, 0.42f, t));
                }
                c.TaperPath(armPts, 0.022f, 0.002f, 0.4f);
            }
        });

        Make("Spikes", c =>
        {
            c.Ring(0.22f, 0.008f);
            int teeth = 12;
            for (int i = 0; i < teeth * 2; i++)
            {
                float r0 = i % 2 == 0 ? 0.46f : 0.33f, r1 = i % 2 == 0 ? 0.33f : 0.46f;
                float a0 = 2f * Mathf.PI * i / (teeth * 2), a1 = 2f * Mathf.PI * (i + 1) / (teeth * 2);
                c.Seg(new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0, new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1, 0.012f);
            }
            for (int i = 0; i < teeth; i++)
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / teeth;
                c.Seg(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.33f, 0.006f);
            }
        });

        Make("Fangs", c =>
        {
            c.Ring(0.47f, 0.008f); c.Ring(0.2f, 0.008f);
            for (int k = 0; k < 6; k++)
            {
                float a0 = 2f * Mathf.PI * k / 6f;
                Vector2 prevO = default, prevI = default;
                for (int i = 0; i <= 14; i++)
                {
                    float t = i / 14f;
                    float ang = a0 + t * 0.55f;
                    float rOuter = Mathf.Lerp(0.22f, 0.45f, Mathf.Sin(t * Mathf.PI * 0.5f));
                    float rInner = Mathf.Lerp(0.22f, 0.45f, t * t);
                    Vector2 po = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rOuter;
                    Vector2 pi = new Vector2(Mathf.Cos(ang - 0.18f * (1f - t)), Mathf.Sin(ang - 0.18f * (1f - t))) * rInner;
                    if (i > 0) { c.Seg(prevO, po, 0.011f); c.Seg(prevI, pi, 0.009f); }
                    prevO = po; prevI = pi;
                }
                float ad = a0 + 2f * Mathf.PI / 12f;
                c.FillDot(new Vector2(Mathf.Cos(ad), Mathf.Sin(ad)) * 0.36f, 0.014f);
            }
        });

        Make("Rays", c =>
        {
            c.Ring(0.17f, 0.010f); c.Ring(0.47f, 0.006f);
            for (int i = 0; i < 24; i++)
            {
                float a = 2f * Mathf.PI * i / 24f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool longRay = i % 2 == 0;
                c.Taper(d * (longRay ? 0.22f : 0.3f), d * (longRay ? 0.46f : 0.39f), longRay ? 0.022f : 0.014f, 0.0015f, 0.5f);      // thick at the root, pointed and fading at the tip
            }
            c.RingDots(8, 0.13f, 0.012f);
        });

        Make("Bolt_Ring", c =>
        {
            Vector2 prev = default, first = default;
            int pts = 36;
            for (int i = 0; i <= pts; i++)
            {
                float a = 2f * Mathf.PI * i / pts;
                float r = 0.43f + (i % 2 == 0 ? 0.035f : -0.035f);
                Vector2 p = i == pts ? first : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) first = p;
                if (i > 0) c.Seg(prev, p, 0.010f);
                prev = p;
            }
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.5f * k + Mathf.PI * 0.25f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var n = new Vector2(-d.y, d.x);
                Vector2 pp = d * 0.06f;
                for (int s = 1; s <= 5; s++)
                {
                    Vector2 p = d * (0.06f + 0.07f * s) + n * (s % 2 == 0 ? 0.035f : -0.035f);
                    c.Seg(pp, p, 0.012f);
                    pp = p;
                }
            }
            c.Ring(0.05f, 0.008f);
        });

        Make("Bolt_Symbol", c =>
        {
            var pts = new[]
            {
                new Vector2(0.10f, 0.44f), new Vector2(-0.17f, 0.02f), new Vector2(-0.02f, 0.02f),
                new Vector2(-0.11f, -0.44f), new Vector2(0.18f, -0.06f), new Vector2(0.03f, -0.06f),
            };
            c.FillPoly(pts);
            c.Ring(0.47f, 0.008f);
        });

        Make("Scatter", c =>
        {
            c.Ring(0.47f, 0.006f);
            int count = 70;
            for (int i = 0; i < count; i++)
            {
                float r = 0.44f * Mathf.Sqrt((i + 0.5f) / count);
                float a = i * 137.508f * Mathf.Deg2Rad;
                c.FillDot(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0.008f + 0.010f * (i % 3) / 2f);
            }
        });

        Make("Crystal", c =>
        {
            c.Poly(6, 0.45f, 90f, 0.012f); c.Poly(6, 0.30f, 90f, 0.010f); c.Poly(6, 0.15f, 90f, 0.008f);
            for (int i = 0; i < 6; i++)
            {
                float a = (90f + 60f * i) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                c.Seg(d * 0.15f, d * 0.45f, 0.007f);
                c.FillDot(d * 0.45f, 0.016f);
            }
        });

        Make("Square_Runes", c =>
        {
            c.Poly(4, 0.46f, 45f, 0.012f); c.Poly(4, 0.46f, 0f, 0.012f);
            c.Ring(0.34f, 0.008f); c.Ring(0.22f, 0.008f);
            c.Runes(0.235f, 0.325f, 20, 11);
        });

        // a pinwheel of curved, tapered arms that whirl out from the middle, with arrowheads flung off the tips
        Make("Spiral_Vortex", c =>
        {
            c.Ring(0.47f, 0.005f);
            for (int layer = 0; layer < 2; layer++)
            {
                int arms = layer == 0 ? 7 : 7;
                float turn = layer == 0 ? 3.4f : 2.6f, r0 = layer == 0 ? 0.03f : 0.10f, r1 = layer == 0 ? 0.43f : 0.36f, off = layer == 0 ? 0f : Mathf.PI / arms;
                for (int arm = 0; arm < arms; arm++)
                {
                    float baseAng = off + 2f * Mathf.PI * arm / arms;
                    var pts = new List<Vector2>();
                    for (int i = 0; i <= 40; i++)
                    {
                        float t = i / 40f;
                        float ang = baseAng + Mathf.Pow(t, 0.8f) * turn;
                        pts.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(r0, r1, t));
                    }
                    c.TaperPath(pts, layer == 0 ? 0.020f : 0.012f, 0.0012f, 0.35f);

                    // an arrowhead beside the tip of every long arm
                    if (layer == 0)
                    {
                        Vector2 tip = pts[pts.Count - 1], back = pts[pts.Count - 5];
                        Vector2 dir = (tip - back).normalized, nrm = new Vector2(-dir.y, dir.x);
                        Vector2 at = tip + dir * 0.025f;
                        c.Taper(at, at - dir * 0.045f + nrm * 0.022f, 0.008f, 0.001f, 0.6f);
                        c.Taper(at, at - dir * 0.045f - nrm * 0.022f, 0.008f, 0.001f, 0.6f);
                    }
                }
            }
            c.RingDots(7, 0.46f, 0.009f);
        });

        // straight lines shot out from the middle like a starburst, a zig-zag ring and small diamonds
        Make("Burst_Lines", c =>
        {
            int rays = 20;
            for (int i = 0; i < rays; i++)
            {
                float a = 2f * Mathf.PI * i / rays;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool longRay = i % 2 == 0;
                c.Taper(d * (longRay ? 0.07f : 0.17f), d * (longRay ? 0.49f : 0.40f), longRay ? 0.020f : 0.012f, 0.0015f, 0.3f);
            }

            // zig-zag ring (sharp angular teeth, tapered like lightning)
            var zig = new List<Vector2>();
            int teeth = 16;
            for (int i = 0; i <= teeth * 2; i++)
            {
                float a = 2f * Mathf.PI * i / (teeth * 2);
                float r = 0.27f + (i % 2 == 0 ? 0.03f : -0.03f);
                zig.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            for (int i = 0; i < zig.Count - 1; i++) c.Seg(zig[i], zig[i + 1], 0.007f);

            for (int k = 0; k < 8; k++)
            {
                float a = 2f * Mathf.PI * (k + 0.5f) / 8f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                c.Poly4At(d * 0.36f, 0.030f, a * Mathf.Rad2Deg, 0.006f);
                c.Poly4At(d * 0.215f, 0.018f, a * Mathf.Rad2Deg, 0.005f);
            }
            c.Ring(0.115f, 0.006f);
        });

        // a field of separate small glyphs on an 8 x 8 grid, one per cell (the Sparkle reveal in the shader pops them in cell by cell)
        Make("Rune_Scatter", c =>
        {
            var rng = new System.Random(23);
            for (int gy = 0; gy < 8; gy++)
                for (int gx = 0; gx < 8; gx++)
                {
                    var centre = new Vector2((gx + 0.5f) / 8f - 0.5f, (gy + 0.5f) / 8f - 0.5f);
                    if (centre.magnitude > 0.43f) continue;                // keep the field round
                    if (rng.NextDouble() < 0.14) continue;                  // a few gaps so it reads as scattered
                    centre += new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.024f;
                    float size = 0.030f + 0.012f * (float)rng.NextDouble();
                    float rot = (float)rng.NextDouble() * 360f;
                    c.Glyph(rng.Next(0, 6), centre, size, rot);
                }
        });

        // a crosshair flare: long thin tapered lines through the middle (horizontal, vertical, short diagonals) and a bright core
        Make("Flare_Cross", c =>
        {
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.5f * k;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                c.Taper(d * 0.02f, d * 0.49f, 0.016f, 0.0015f, 0.2f);
            }
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.25f + Mathf.PI * 0.5f * k;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                c.Taper(d * 0.02f, d * 0.22f, 0.010f, 0.0012f, 0.3f);
            }
            c.FillDot(Vector2.zero, 0.022f);
            c.Ring(0.06f, 0.005f);
        });

        // the first thing to appear: one small diamond rune with a smaller one inside and four ticks
        Make("Seed_Diamond", c =>
        {
            c.Poly4At(Vector2.zero, 0.16f, 0f, 0.012f);
            c.Poly4At(Vector2.zero, 0.075f, 0f, 0.010f);
            c.FillDot(Vector2.zero, 0.012f);
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.5f * k;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                c.Seg(d * 0.33f, d * 0.43f, 0.008f);
            }
        });

        // ---------- the circles ----------
        // basic spells
        var scatter = Circle("Circle_Scatter", 1.3f, Tilt(5f, 1.5f), Ready(new Color(1f, 0.95f, 0.7f), 0.4f),
            L("Scatter", "Scatter", CircleAnchor.Front, 2.2f, 40f, 0.0f, 0.6f, CircleAppear.ScaleUp),
            L("Scatter Back", "Scatter", CircleAnchor.Front, 1.5f, -70f, 0.2f, 0.5f, CircleAppear.FadeIn),
            L("Ring", "Ring_Double", CircleAnchor.Front, 2.45f, -15f, 0.1f, 0.5f),
            L("Side Scatter A", "Scatter", CircleAnchor.Front, 0.7f, 120f, 0.5f, 0.3f, CircleAppear.ScaleUp, new Vector3(1.4f, 0.7f, 0.05f)),
            L("Side Scatter B", "Scatter", CircleAnchor.Front, 0.55f, -140f, 0.6f, 0.3f, CircleAppear.ScaleUp, new Vector3(-1.35f, -0.7f, 0.05f)));

        var fire = Circle("Circle_FireBurst", 1.3f, Tilt(4f, 1.2f), Ready(new Color(1f, 0.85f, 0.5f), 0.5f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 2.2f, 25f, 0.0f, 0.6f),
            L("Flame Petals Counter", "Flame_Petals", CircleAnchor.Front, 1.45f, -45f, 0.25f, 0.5f, CircleAppear.ScaleUp),
            L("Rays", "Rays", CircleAnchor.Front, 2.6f, -10f, 0.1f, 0.5f, CircleAppear.FadeIn, default, 0.6f),
            L("Floor Petals", "Flame_Petals", CircleAnchor.Feet, 3.0f, 15f, 0.3f, 0.6f),
            L("Floor Rays", "Rays", CircleAnchor.Feet, 2.1f, -30f, 0.5f, 0.4f, CircleAppear.ScaleUp));

        var lightning = Circle("Circle_Lightning", 1.4f, Tilt(14f, 3.2f), Ready(new Color(0.9f, 0.95f, 1f), 0.7f),
            L("Bolt Ring", "Bolt_Ring", CircleAnchor.Front, 2.3f, 90f, 0.0f, 0.5f),
            L("Bolt Ring Counter", "Bolt_Ring", CircleAnchor.Front, 1.5f, -150f, 0.2f, 0.4f, CircleAppear.ScaleUp),
            L("Cross", "Cross", CircleAnchor.Front, 3.0f, 20f, 0.1f, 0.5f, CircleAppear.FadeIn, default, 0.5f),
            L("Side Bolt A", "Bolt_Ring", CircleAnchor.Front, 0.7f, 200f, 0.55f, 0.3f, CircleAppear.RadialWipe, new Vector3(1.5f, 0.9f, 0.05f)),
            L("Side Bolt B", "Bolt_Ring", CircleAnchor.Front, 0.7f, -200f, 0.65f, 0.3f, CircleAppear.RadialWipe, new Vector3(-1.5f, -0.9f, 0.05f)));

        var frost = Circle("Circle_Frost", 1.3f, Tilt(3f, 0.6f), Ready(new Color(0.85f, 0.97f, 1f), 0.5f),
            L("Snowflake", "Snowflake", CircleAnchor.Front, 2.2f, 8f, 0.0f, 0.7f),
            L("Crystal", "Crystal", CircleAnchor.Front, 1.3f, -14f, 0.3f, 0.5f, CircleAppear.ScaleUp),
            L("Ring", "Ring_Single", CircleAnchor.Front, 2.5f, -4f, 0.1f, 0.5f),
            L("Floor Snowflake", "Snowflake", CircleAnchor.Feet, 3.2f, 6f, 0.4f, 0.6f),
            L("Floor Dots", "Dots_Ring", CircleAnchor.Feet, 2.4f, -10f, 0.6f, 0.35f, CircleAppear.ScaleUp));

        var wind = Circle("Circle_Wind", 1.3f, Tilt(18f, 2.6f), Ready(new Color(0.9f, 1f, 0.9f), 0.4f),
            L("Spiral", "Spiral", CircleAnchor.Front, 2.3f, 110f, 0.0f, 0.55f),
            L("Spiral Counter", "Spiral", CircleAnchor.Front, 1.5f, -170f, 0.15f, 0.5f, CircleAppear.ScaleUp),
            L("Segments", "Segments", CircleAnchor.Front, 2.7f, 55f, 0.2f, 0.5f),
            L("Floor Spiral", "Spiral", CircleAnchor.Feet, 3.0f, 80f, 0.4f, 0.6f),
            L("Floor Segments", "Segments", CircleAnchor.Feet, 2.1f, -120f, 0.6f, 0.4f, CircleAppear.ScaleUp));

        var earth = Circle("Circle_Earth", 1.2f, Tilt(2f, 0.5f), Ready(new Color(1f, 0.85f, 0.55f), 0.5f),
            Aoe(L("Area Spikes", "Spikes", CircleAnchor.AimPoint, 1.0f, 12f, 0.0f, 0.5f)),
            Aoe(L("Area Square", "Square_Runes", CircleAnchor.AimPoint, 0.8f, -20f, 0.2f, 0.5f)),
            Aoe(L("Area Glow", "Glow", CircleAnchor.AimPoint, 1.0f, 0f, 0.5f, 0.5f, CircleAppear.FadeIn, default, 0.25f)),
            L("Hand Spikes", "Spikes", CircleAnchor.Front, 0.9f, -50f, 0.0f, 0.4f),
            L("Floor Squares", "Square_Runes", CircleAnchor.Feet, 2.5f, 15f, 0.3f, 0.5f, CircleAppear.ScaleUp));

        var purple = new Color(0.55f, 0.12f, 0.95f);
        var venom = Circle("Circle_Venom", 1.3f, Tilt(9f, 1.8f), Ready(new Color(0.85f, 0.65f, 1f), 0.6f),
            L("Fangs", "Fangs", CircleAnchor.Front, 2.2f, -35f, 0.0f, 0.6f),
            L("Fangs Counter", "Fangs", CircleAnchor.Front, 1.4f, 60f, 0.25f, 0.5f, CircleAppear.ScaleUp),
            L("Drops", "Dots_Ring", CircleAnchor.Front, 2.6f, 25f, 0.1f, 0.5f),
            L("Floor Fangs", "Fangs", CircleAnchor.Feet, 2.8f, -20f, 0.4f, 0.6f),
            L("Floor Scatter", "Scatter", CircleAnchor.Feet, 2.0f, 30f, 0.6f, 0.4f, CircleAppear.ScaleUp));

        foreach (var layer in venom.layers) { layer.useSpellColor = false; layer.tint = purple; }       // venom is purple whatever the dart's colour
        EditorUtility.SetDirty(venom);

        var radiant = Circle("Circle_Radiant", 1.4f, Tilt(5f, 1.0f), Ready(new Color(1f, 1f, 0.8f), 0.9f),
            L("Rays", "Rays", CircleAnchor.Front, 2.4f, 18f, 0.0f, 0.55f),
            L("Rays Counter", "Rays", CircleAnchor.Front, 1.7f, -30f, 0.2f, 0.5f, CircleAppear.ScaleUp),
            L("Halo", "Ring_Double", CircleAnchor.Front, 2.8f, 6f, 0.1f, 0.5f),
            L("Glow", "Glow", CircleAnchor.Front, 3.2f, 0f, 0.3f, 0.5f, CircleAppear.FadeIn, default, 0.4f),
            L("Side Rays A", "Rays", CircleAnchor.Front, 0.7f, 90f, 0.6f, 0.3f, CircleAppear.ScaleUp, new Vector3(1.5f, 0.8f, 0.05f)),
            L("Side Rays B", "Rays", CircleAnchor.Front, 0.7f, -90f, 0.65f, 0.3f, CircleAppear.ScaleUp, new Vector3(-1.5f, -0.8f, 0.05f)));

        // fusions: the images of the spells they are made of, plus a layer of their own
        var infernal = Circle("Circle_InfernalBolt", 1.5f, Tilt(6f, 1.6f), Ready(new Color(1f, 0.8f, 0.45f), 0.6f),
            L("Runes", "Rune_Band", CircleAnchor.Front, 2.1f, 28f, 0.0f, 0.5f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 2.7f, -35f, 0.1f, 0.6f),
            L("Triangle", "Triangle", CircleAnchor.Front, 1.3f, -60f, 0.3f, 0.45f, CircleAppear.ScaleUp),
            L("Flame Core", "Flame_Petals", CircleAnchor.Front, 0.9f, 90f, 0.55f, 0.35f, CircleAppear.ScaleUp));

        var cinder = Circle("Circle_CinderStorm", 1.5f, Tilt(8f, 2.0f), Ready(new Color(1f, 0.75f, 0.4f), 0.6f),
            L("Scatter", "Scatter", CircleAnchor.Front, 2.6f, 35f, 0.0f, 0.6f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 1.9f, -40f, 0.15f, 0.6f),
            L("Rays", "Rays", CircleAnchor.Front, 3.0f, 12f, 0.1f, 0.5f, CircleAppear.FadeIn, default, 0.5f),
            L("Floor Scatter", "Scatter", CircleAnchor.Feet, 3.2f, -25f, 0.4f, 0.6f),
            L("Floor Petals", "Flame_Petals", CircleAnchor.Feet, 2.2f, 30f, 0.6f, 0.4f, CircleAppear.ScaleUp));

        var firestorm = Circle("Circle_FirestormLance", 1.5f, Tilt(10f, 2.4f), Ready(new Color(1f, 0.9f, 0.6f), 0.7f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 2.5f, 20f, 0.0f, 0.55f),
            L("Bolt Ring", "Bolt_Ring", CircleAnchor.Front, 1.9f, -120f, 0.15f, 0.5f),
            L("Cross", "Cross", CircleAnchor.Front, 3.0f, 30f, 0.2f, 0.5f, CircleAppear.FadeIn, default, 0.5f),
            L("Lance Rays", "Rays", CircleAnchor.Front, 1.1f, 150f, 0.5f, 0.4f, CircleAppear.ScaleUp));

        var thunder = Circle("Circle_ThunderMeteor", 1.3f, Tilt(8f, 2.0f), Ready(new Color(0.95f, 0.95f, 1f), 0.8f),
            Aoe(L("Area Bolts", "Bolt_Ring", CircleAnchor.AimPoint, 1.0f, 40f, 0.0f, 0.5f)),
            Aoe(L("Area Square", "Square_Runes", CircleAnchor.AimPoint, 0.8f, -20f, 0.2f, 0.5f)),
            Aoe(L("Area Glow", "Glow", CircleAnchor.AimPoint, 1.0f, 0f, 0.5f, 0.5f, CircleAppear.FadeIn, default, 0.25f)),
            L("Hand Bolts", "Bolt_Ring", CircleAnchor.Front, 1.2f, 160f, 0.0f, 0.4f),
            L("Hand Star", "Star8", CircleAnchor.Front, 2.0f, -35f, 0.2f, 0.5f));

        var glacier = Circle("Circle_GlacierBolt", 1.5f, Tilt(4f, 0.8f), Ready(new Color(0.85f, 0.97f, 1f), 0.6f),
            L("Snowflake", "Snowflake", CircleAnchor.Front, 2.6f, 10f, 0.0f, 0.6f),
            L("Triangle", "Triangle", CircleAnchor.Front, 1.6f, -40f, 0.2f, 0.5f, CircleAppear.ScaleUp),
            L("Rune Band", "Rune_Band", CircleAnchor.Front, 2.1f, 20f, 0.1f, 0.5f),
            L("Crystal Core", "Crystal", CircleAnchor.Front, 0.9f, -70f, 0.5f, 0.35f, CircleAppear.ScaleUp));

        var blizzard = Circle("Circle_BlizzardLance", 1.5f, Tilt(7f, 1.6f), Ready(new Color(0.9f, 1f, 1f), 0.6f),
            L("Snowflake", "Snowflake", CircleAnchor.Front, 2.5f, 30f, 0.0f, 0.55f),
            L("Bolt Ring", "Bolt_Ring", CircleAnchor.Front, 1.7f, -90f, 0.15f, 0.5f),
            L("Spiral", "Spiral", CircleAnchor.Front, 3.0f, 45f, 0.1f, 0.5f, CircleAppear.FadeIn, default, 0.6f),
            L("Crystal Core", "Crystal", CircleAnchor.Front, 0.9f, 120f, 0.5f, 0.35f, CircleAppear.ScaleUp));

        var cyclone = Circle("Circle_Cyclone", 1.5f, Tilt(20f, 3.0f), Ready(new Color(1f, 0.9f, 0.6f), 0.6f),
            L("Spiral", "Spiral", CircleAnchor.Front, 2.6f, 160f, 0.0f, 0.55f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 1.8f, -140f, 0.15f, 0.5f, CircleAppear.ScaleUp),
            L("Segments", "Segments", CircleAnchor.Front, 3.1f, 70f, 0.1f, 0.5f),
            L("Floor Spiral", "Spiral", CircleAnchor.Feet, 3.4f, 100f, 0.4f, 0.6f),
            L("Floor Petals", "Flame_Petals", CircleAnchor.Feet, 2.3f, -80f, 0.6f, 0.4f, CircleAppear.ScaleUp));

        var tremor = Circle("Circle_TremorStorm", 1.2f, Tilt(3f, 0.8f), Ready(new Color(1f, 0.8f, 0.5f), 0.8f),
            Aoe(L("Area Spikes", "Spikes", CircleAnchor.AimPoint, 1.0f, 8f, 0.0f, 0.5f)),
            Aoe(L("Area Scatter", "Scatter", CircleAnchor.AimPoint, 0.9f, -25f, 0.15f, 0.5f)),
            Aoe(L("Area Square", "Square_Runes", CircleAnchor.AimPoint, 0.7f, 20f, 0.3f, 0.5f)),
            Aoe(L("Area Glow", "Glow", CircleAnchor.AimPoint, 1.0f, 0f, 0.5f, 0.5f, CircleAppear.FadeIn, default, 0.3f)),
            L("Hand Spikes", "Spikes", CircleAnchor.Front, 1.0f, 50f, 0.0f, 0.4f),
            L("Floor Scatter", "Scatter", CircleAnchor.Feet, 3.0f, -20f, 0.3f, 0.5f, CircleAppear.ScaleUp));
        tremor.readyPulse = 0.5f;

        var plague = Circle("Circle_PlagueStorm", 1.5f, Tilt(10f, 2.0f), Ready(new Color(0.85f, 1f, 0.5f), 0.6f),
            L("Fangs", "Fangs", CircleAnchor.Front, 2.4f, -30f, 0.0f, 0.55f),
            L("Scatter", "Scatter", CircleAnchor.Front, 1.7f, 70f, 0.15f, 0.5f, CircleAppear.ScaleUp),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 3.0f, 15f, 0.1f, 0.5f, CircleAppear.FadeIn, default, 0.5f),
            L("Floor Fangs", "Fangs", CircleAnchor.Feet, 3.2f, -20f, 0.4f, 0.6f),
            L("Floor Scatter", "Scatter", CircleAnchor.Feet, 2.2f, 35f, 0.6f, 0.4f, CircleAppear.ScaleUp));

        var zero = Circle("Circle_AbsoluteZero", 1.2f, Tilt(2f, 0.5f), Ready(new Color(0.9f, 1f, 1f), 0.9f),
            Aoe(L("Area Snowflake", "Snowflake", CircleAnchor.AimPoint, 1.0f, 5f, 0.0f, 0.5f)),
            Aoe(L("Area Crystal", "Crystal", CircleAnchor.AimPoint, 0.75f, -9f, 0.15f, 0.5f)),
            Aoe(L("Area Hexagram", "Hexagram", CircleAnchor.AimPoint, 0.55f, 14f, 0.3f, 0.5f)),
            Aoe(L("Area Glow", "Glow", CircleAnchor.AimPoint, 1.0f, 0f, 0.5f, 0.5f, CircleAppear.FadeIn, default, 0.3f)),
            L("Hand Snowflake", "Snowflake", CircleAnchor.Front, 1.4f, 12f, 0.0f, 0.5f),
            L("Hand Rings", "Ring_Double", CircleAnchor.Front, 2.4f, -6f, 0.2f, 0.5f),
            L("Floor Crystal", "Crystal", CircleAnchor.Feet, 3.2f, -8f, 0.35f, 0.5f, CircleAppear.ScaleUp));
        zero.readyPulse = 0.7f;

        var sun = Circle("Circle_SunLance", 1.6f, Tilt(6f, 1.2f), Ready(new Color(1f, 0.95f, 0.65f), 1.0f),
            L("Rays", "Rays", CircleAnchor.Front, 2.8f, 15f, 0.0f, 0.55f),
            L("Bolt Ring", "Bolt_Ring", CircleAnchor.Front, 1.9f, -80f, 0.15f, 0.5f),
            L("Flame Petals", "Flame_Petals", CircleAnchor.Front, 1.3f, 55f, 0.3f, 0.45f, CircleAppear.ScaleUp),
            L("Halo", "Ring_Double", CircleAnchor.Front, 3.2f, -5f, 0.1f, 0.5f),
            L("Glow", "Glow", CircleAnchor.Front, 3.6f, 0f, 0.4f, 0.5f, CircleAppear.FadeIn, default, 0.4f));

        // the ultimate spells (three or more elements) are drawn in the colours of every element they are made from
        var fireC = new Color(1f, 0.55f, 0.2f); var frostC = new Color(0.6f, 0.9f, 1f); var earthC = new Color(0.85f, 0.65f, 0.3f);
        var goldC = new Color(1f, 0.9f, 0.45f); var boltC = new Color(0.55f, 0.7f, 1f); var venomC = new Color(0.6f, 0.2f, 0.95f);
        var meteorC = new Color(1f, 0.35f, 0.2f); var shardC = new Color(0.95f, 0.95f, 1f);
        Mix(tremor, earthC, meteorC, shardC);
        Mix(plague, venomC, fireC, shardC);
        Mix(zero, frostC, earthC, goldC, meteorC);
        Mix(sun, goldC, boltC, fireC);

        // ---------- new casting styles (from the anime references) ----------
        var yellow = new Color(1f, 0.85f, 0.3f);

        // The three new circles follow the references frame by frame (see CircleAppear / CircleExit):
        //  Vortex:     stray glyphs appear, a tiny pinwheel opens from its middle while whirling, holds, then a white flash and the lines shatter outward
        //  Burst:      one seed diamond, a haze, then every line pops at once shot outward (yellow first, green after), drifting and shimmering with no spin
        //  Rune Field: a seed cross, a crosshair flash, then glyphs pop in from the middle outward and hold still and shimmering

        // Spiral Vortex (new, from reference clip 1: see BuildVortex)
        var vortex = BuildVortex(yellow);

        // Rune Scatter
        var runeField = Circle("Circle_RuneField", 1.4f, Tilt(2f, 0.8f), Ready(new Color(1f, 0.95f, 0.6f), 0.7f),
            L("Seed", "Flare_Cross", CircleAnchor.Front, 0.7f, 0f, 0.0f, 0.18f, CircleAppear.Flash),
            L("Crosshair Flash", "Flare_Cross", CircleAnchor.Front, 3.0f, 0f, 0.1f, 0.3f, CircleAppear.Flash),
            L("Field Far", "Rune_Scatter", CircleAnchor.Front, 3.0f, 2f, 0.2f, 0.5f, CircleAppear.Cascade),
            L("Field Mid", "Rune_Scatter", CircleAnchor.Front, 2.2f, -3f, 0.3f, 0.5f, CircleAppear.Cascade),
            L("Field Near", "Rune_Scatter", CircleAnchor.Front, 1.4f, 4f, 0.42f, 0.5f, CircleAppear.Cascade));
        Accent(runeField, 0, yellow);
        Accent(runeField, 3, yellow);
        Hold(runeField, 2, 0f, 0.35f); Hold(runeField, 3, 0f, 0.35f); Hold(runeField, 4, 0f, 0.4f);

        // Radial Burst
        var burst = Circle("Circle_Burst", 1.3f, Tilt(3f, 1.8f), Ready(new Color(1f, 0.95f, 0.6f), 0.9f),
            L("Seed", "Seed_Diamond", CircleAnchor.Front, 0.7f, 0f, 0.0f, 0.2f, CircleAppear.Flash),
            L("Haze", "Glow", CircleAnchor.Front, 4.2f, 0f, 0.12f, 0.3f, CircleAppear.FadeIn, default, 0.4f),
            L("Flash Flare", "Flare_Cross", CircleAnchor.Front, 3.6f, 0f, 0.4f, 0.2f, CircleAppear.Flash),
            L("Burst Yellow", "Burst_Lines", CircleAnchor.Front, 3.3f, 0f, 0.4f, 0.12f, CircleAppear.Pop),
            L("Burst Green", "Burst_Lines", CircleAnchor.Front, 2.3f, 10f, 0.52f, 0.14f, CircleAppear.Pop),
            L("Glyph Field", "Rune_Scatter", CircleAnchor.Front, 3.0f, 0f, 0.62f, 0.3f, CircleAppear.Cascade));
        Accent(burst, 0, yellow);
        Accent(burst, 3, yellow);
        Accent(burst, 5, yellow);
        Hold(burst, 3, 0.05f, 0.3f); Hold(burst, 4, 0.04f, 0.25f); Hold(burst, 5, 0f, 0.35f);

        // ---------- how each circle comes in and goes out ----------
        // in: layer-by-layer Appear styles (default is the draw-on wipe); out: one Exit Style for the whole circle
        Exit(vortex, CircleExit.Fade, 0.3f);        Exit(runeField, CircleExit.Dissolve, 0.55f);   Exit(burst, CircleExit.Shockwave, 0.4f);
        Exit(fire, CircleExit.SpinAway, 0.4f);          Exit(frost, CircleExit.Dissolve, 0.5f);        Exit(wind, CircleExit.SpinAway, 0.45f);
        Exit(earth, CircleExit.Unwind, 0.5f);           Exit(venom, CircleExit.Implode, 0.35f);        Exit(radiant, CircleExit.Shockwave, 0.45f);
        Exit(infernal, CircleExit.Launch, 0.35f);       Exit(cinder, CircleExit.Dissolve, 0.5f);       Exit(firestorm, CircleExit.Launch, 0.35f);
        Exit(thunder, CircleExit.Shockwave, 0.4f);      Exit(glacier, CircleExit.Launch, 0.35f);       Exit(blizzard, CircleExit.Launch, 0.35f);
        Exit(cyclone, CircleExit.SpinAway, 0.45f);      Exit(tremor, CircleExit.Unwind, 0.55f);        Exit(plague, CircleExit.Implode, 0.4f);
        Exit(zero, CircleExit.Dissolve, 0.6f);          Exit(sun, CircleExit.Launch, 0.4f);

        Enter(wind, CircleAppear.SpinIn, 0, 1);         Enter(cyclone, CircleAppear.SpinIn, 0);        Enter(venom, CircleAppear.SpinIn, 0, 1);
        Enter(firestorm, CircleAppear.SpinIn, 0);       Enter(frost, CircleAppear.FlipIn, 0, 1);       Enter(glacier, CircleAppear.FlipIn, 0);
        Enter(blizzard, CircleAppear.FlipIn, 0);        Enter(plague, CircleAppear.FlipIn, 0);         Enter(sun, CircleAppear.FlipIn, 0);
        Enter(earth, CircleAppear.Stamp, 0, 1);         Enter(tremor, CircleAppear.Stamp, 0, 1);       Enter(zero, CircleAppear.Stamp, 0, 1);
        Enter(thunder, CircleAppear.Stamp, 0, 1);       Enter(infernal, CircleAppear.Stamp, 1);

        // ---------- assign ----------
        Assign(WeaponDir, "ArcaneBolt", vortex);
        Assign(WeaponDir, "ScatterShot", runeField);
        Assign(WeaponDir, "LightningLance", burst);
        Assign(WeaponDir, "FireBurst", fire);
        Assign(WeaponDir, "FrostShard", frost);
        Assign(WeaponDir, "WindBlade", wind);
        Assign(WeaponDir, "EarthSpike", earth);
        Assign(WeaponDir, "VenomDart", venom);
        Assign(WeaponDir, "RadiantBeam", radiant);

        Assign(ComboDir, "InfernalBolt", infernal);
        Assign(ComboDir, "CinderStorm", cinder);
        Assign(ComboDir, "FirestormLance", firestorm);
        Assign(ComboDir, "ThunderMeteor", thunder);
        Assign(ComboDir, "GlacierBolt", glacier);
        Assign(ComboDir, "BlizzardLance", blizzard);
        Assign(ComboDir, "Cyclone", cyclone);
        Assign(ComboDir, "TremorStorm", tremor);
        Assign(ComboDir, "PlagueStorm", plague);
        Assign(ComboDir, "AbsoluteZero", zero);
        Assign(ComboDir, "SunLance", sun);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Unique casting circles built in " + CircleDir + " and assigned (Arcane Bolt: vortex, Scatter Shot: rune field, other basic spells and 11 fusions: own circles). Lightning Lance: radial burst.");
    }

    // ---------- helpers ----------

    static void Assign(string dir, string spellName, CastingCircleData circle)
    {
        var s = AssetDatabase.LoadAssetAtPath<SpellData>(dir + "/" + spellName + ".asset");
        if (s == null) { Debug.LogWarning("Unique circles: spell " + spellName + " not found."); return; }
        s.holdCircle = circle;
        EditorUtility.SetDirty(s);
    }

    // the circle-level look: tilt and ready colour
    struct Look { public float tilt, tiltSpeed; public Color ready; public float flash; public bool isTilt; }
    static Look Tilt(float degrees, float speed) => new Look { tilt = degrees, tiltSpeed = speed, isTilt = true };
    static Look Ready(Color color, float flash) => new Look { ready = color, flash = flash };

    static CircleLayer L(string name, string texture, CircleAnchor anchor, float size, float rot, float startAt, float drawTime,
        CircleAppear appear = CircleAppear.RadialWipe, Vector3 offset = default, float opacity = 1f)
    {
        if (!Tex.TryGetValue(texture, out var t)) t = AssetDatabase.LoadAssetAtPath<Texture2D>(ImageDir + "/" + texture + ".png");
        return new CircleLayer
        {
            name = name, texture = t, anchor = anchor, size = size, rotationSpeed = rot,
            startAt = startAt, drawTime = drawTime, appear = appear, offset = offset, opacity = opacity,
        };
    }

    // layers take the colours in turn, whatever the spell's own colour is
    static void Mix(CastingCircleData circle, params Color[] colors)
    {
        for (int i = 0; i < circle.layers.Length; i++)
        {
            circle.layers[i].useSpellColor = false;
            circle.layers[i].tint = colors[i % colors.Length];
        }
        EditorUtility.SetDirty(circle);
    }

    // ---------- the vortex (reference clip 1): built one layer at a time ----------
    // Layer 1 (outer): the arrow ring. A ring of 4 diamonds and 4 V's that turn a little apart, fuse into groups, turn to face the
    // middle, then the whole ring scales down and locks small and circles the middle. It is drawn by CastingCircle itself (see
    // UpdateArrowRing) from two images: the V and the gapped diamond (two V's facing each other, open at the side corners).
    static CastingCircleData BuildVortex(Color yellow)
    {
        const float wingL = 0.30f;                       // wing length (fraction of the image width), the same as ArrowL in CastingCircle
        float half = 28.5f * Mathf.Deg2Rad;          // half the opening of a V (wings are about 57 degrees apart)
        float dx = wingL * Mathf.Cos(half), dy = wingL * Mathf.Sin(half);
        float D = 2.07f * wingL;                         // diamond tip to tip

        // a V: its tip in the middle of the image, pointing right, thick at the tip and thinning toward the wing ends
        Make("Arrow_V", c =>
        {
            c.Taper(Vector2.zero, new Vector2(-dx, dy), 0.014f, 0.004f, 0.7f);
            c.Taper(Vector2.zero, new Vector2(-dx, -dy), 0.014f, 0.004f, 0.7f);
        });
        // the diamond: a nose V pointing right and a tail V pointing left, tips D apart, the side corners left open
        Make("Arrow_Diamond", c =>
        {
            var nose = new Vector2(D / 2f, 0f);
            var tail = new Vector2(-D / 2f, 0f);
            c.Taper(nose, nose + new Vector2(-dx, dy), 0.014f, 0.004f, 0.7f);
            c.Taper(nose, nose + new Vector2(-dx, -dy), 0.014f, 0.004f, 0.7f);
            c.Taper(tail, tail + new Vector2(dx, dy), 0.014f, 0.004f, 0.7f);
            c.Taper(tail, tail + new Vector2(dx, -dy), 0.014f, 0.004f, 0.7f);
        });

        // Layer 2: S-curved arms (your drawing): 8 arms leave the middle, each bending one way and then back, all turned the same way.
        // Thick at the root, thinning to a point. Two copies (B is turned half a step with a different wiggle) make the two layers.
        for (int v = 0; v < 2; v++)
        {
            int vv = v;
            Make("Vortex_Arms_" + (v == 0 ? "A" : "B"), c =>
            {
                const int arms = 8;
                for (int i = 0; i < arms; i++)
                {
                    float a0 = 2f * Mathf.PI * i / arms + (vv == 0 ? 0f : Mathf.PI / arms);
                    float phase = vv == 0 ? 0f : 0.5f;
                    var pts = new List<Vector2>();
                    for (int k = 0; k <= 48; k++)
                    {
                        float t = k / 48f;
                        float ang = a0 + 0.2f * t + 0.26f * Mathf.Sin(t * 2f * Mathf.PI * 0.9f + phase);      // one gentle S per arm
                        float r = Mathf.Lerp(0.05f, 0.46f, t);
                        pts.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r);
                    }
                    c.TaperPath(pts, 0.013f, 0.004f, 0.9f);
                }
            });
        }

        // a double V (your drawing): a big narrow V with a smaller V nested just inside it, both tips pointing right.
        // Narrow: the wings are about 35 degrees apart.
        Make("Arrow_DoubleV", c =>
        {
            // the front V (tip at the middle of the image, pointing right) is the SMALLER one; the bigger V sits behind it,
            // its tip just inside the front V, the two sets of lines parallel with a clear gap
            float h = 17f * Mathf.Deg2Rad;
            float Lb = wingL, Ls = wingL * 0.5f;
            var frontTip = Vector2.zero;
            var backTip = new Vector2(-0.09f, 0f);
            c.Taper(frontTip, frontTip + new Vector2(-Ls * Mathf.Cos(h), Ls * Mathf.Sin(h)), 0.013f, 0.004f, 0.85f);
            c.Taper(frontTip, frontTip + new Vector2(-Ls * Mathf.Cos(h), -Ls * Mathf.Sin(h)), 0.013f, 0.004f, 0.85f);
            c.Taper(backTip, backTip + new Vector2(-Lb * Mathf.Cos(h), Lb * Mathf.Sin(h)), 0.014f, 0.004f, 0.8f);
            c.Taper(backTip, backTip + new Vector2(-Lb * Mathf.Cos(h), -Lb * Mathf.Sin(h)), 0.014f, 0.004f, 0.8f);
        });

        var ring = L("Arrow Ring", "Arrow_V", CircleAnchor.Front, 3.0f, 0f, 0.0f, 1.0f, CircleAppear.FadeIn);
        ring.arrowRing = true;
        ring.arrowV = Tex["Arrow_V"];
        ring.arrowDiamond = Tex["Arrow_Diamond"];
        ring.arrowAccent = yellow;
        ring.ringSpin = 90f;
        ring.orbitTurns = 1f;

        // the arms start small and grow until they reach their size, rotating as they grow (Bloom), then keep turning slowly
        var armsOuter = L("Arms Outer", "Vortex_Arms_A", CircleAnchor.Front, 2.6f, 40f, 0.0f, 0.8f, CircleAppear.Bloom);
        var armsInner = L("Arms Inner", "Vortex_Arms_B", CircleAnchor.Front, 1.6f, 60f, 0.0f, 0.8f, CircleAppear.Bloom);

        // Layer 3: a second, bigger ring of double-V pieces that only spins around, starting small and growing
        var ring2 = L("Arrow Ring 2", "Arrow_V", CircleAnchor.Front, 2.6f, 50f, 0.0f, 0.8f, CircleAppear.Bloom);
        ring2.arrowRing = true;
        ring2.ringOrbitOnly = true;
        ring2.pieceWing = 0.3f;
        ring2.pieceCount = 4;
        ring2.piecesFaceMiddle = false;
        ring2.arrowV = Tex["Arrow_V"];
        ring2.arrowDiamond = Tex["Arrow_DoubleV"];
        ring2.arrowAccent = yellow;

        var vortex = Circle("Circle_Vortex", 1.3f, Tilt(2f, 1.4f), Ready(new Color(1f, 0.95f, 0.6f), 0.4f), ring, armsOuter, armsInner, ring2);
        vortex.depthSpacing = 0.04f;
        return vortex;
    }

    // a layer drawn as several variants that are swapped
    static CircleLayer Var(CircleLayer layer, float fps, params string[] textures)
    {
        layer.variants = new Texture2D[textures.Length];
        for (int i = 0; i < textures.Length; i++) layer.variants[i] = Tex[textures[i]];
        layer.variantFps = fps;
        return layer;
    }

    // a layer that plays only while the circle leaves; its start and draw time are fractions of the exit
    static CircleLayer ExitOnly(CircleLayer layer, float growFrom)
    {
        layer.exitOnly = true;
        layer.exitGrow = growFrom;
        return layer;
    }

    static void FixedColor(CastingCircleData circle, int layerIndex, Color color)
    {
        circle.layers[layerIndex].useSpellColor = false;
        circle.layers[layerIndex].tint = color;
        EditorUtility.SetDirty(circle);
    }

    // slow outward drift and flicker while the cast is held
    static void Hold(CastingCircleData circle, int layerIndex, float drift, float shimmer)
    {
        circle.layers[layerIndex].holdDrift = drift;
        circle.layers[layerIndex].shimmer = shimmer;
        EditorUtility.SetDirty(circle);
    }

    static void Exit(CastingCircleData circle, CircleExit style, float seconds)
    {
        circle.exitStyle = style;
        circle.exitDuration = seconds;
        EditorUtility.SetDirty(circle);
    }

    // sets how the listed layers come in
    static void Enter(CastingCircleData circle, CircleAppear appear, params int[] layerIndexes)
    {
        foreach (int i in layerIndexes)
            if (i < circle.layers.Length) circle.layers[i].appear = appear;
        EditorUtility.SetDirty(circle);
    }

    // one layer keeps a fixed second colour whatever the spell's colour is (the yellow beside the green in the references)
    static void Accent(CastingCircleData circle, int layerIndex, Color color)
    {
        circle.layers[layerIndex].useSpellColor = false;
        circle.layers[layerIndex].tint = color;
        EditorUtility.SetDirty(circle);
    }

    static CircleLayer Aoe(CircleLayer l) { l.scaleToAoeRadius = true; return l; }

    static CastingCircleData Circle(string name, float frontDistance, Look tilt, Look ready, params CircleLayer[] layers)
    {
        string path = CircleDir + "/" + name + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<CastingCircleData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<CastingCircleData>();
            AssetDatabase.CreateAsset(d, path);
        }
        d.frontDistance = frontDistance;
        d.tiltDegrees = tilt.tilt;
        d.tiltSpeed = tilt.tiltSpeed;
        d.readyColor = ready.ready;
        d.readyFlash = ready.flash;
        d.layers = layers;
        EditorUtility.SetDirty(d);
        return d;
    }

    // ---------- image generation ----------

    static void Make(string name, Action<Canvas> draw)
    {
        var canvas = new Canvas();
        draw(canvas);

        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(canvas.a[i]));
        t.SetPixels(px);

        string path = ImageDir + "/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        Tex[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // a square of alpha values; shapes are measured in units of the image width, centred, so the edge is +-0.5
    class Canvas
    {
        public readonly float[] a = new float[N * N];

        // a stroke that is thick at one end and tapers to a point at the other, optionally fading out along the way
        // (the "brush stroke" look: wS = width at the start, wE = width at the end, aE = opacity at the end)
        public void Taper(Vector2 s, Vector2 e, float wS, float wE, float aE = 1f) => TaperSeg(s, e, wS, wE, 1f, aE);

        // the same along a curve: width and opacity ease from the first point to the last
        public void TaperPath(IList<Vector2> pts, float wS, float wE, float aE = 1f)
        {
            int n = pts.Count - 1;
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n;
                TaperSeg(pts[i], pts[i + 1], Mathf.Lerp(wS, wE, t0), Mathf.Lerp(wS, wE, t1), Mathf.Lerp(1f, aE, t0), Mathf.Lerp(1f, aE, t1));
            }
        }

        void TaperSeg(Vector2 s, Vector2 e, float wS, float wE, float aS, float aE)
        {
            Vector2 d = e - s;
            float len2 = Mathf.Max(d.sqrMagnitude, 1e-8f);
            float maxHalf = Mathf.Max(wS, wE) * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.x, e.x) - maxHalf + 0.5f) * N) - 2);
            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(s.x, e.x) + maxHalf + 0.5f) * N) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.y, e.y) - maxHalf + 0.5f) * N) - 2);
            int y1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(s.y, e.y) + maxHalf + 0.5f) * N) + 2);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - s, d) / len2);
                    float dist = (p - (s + d * t)).magnitude;
                    float half = Mathf.Lerp(wS, wE, t) * 0.5f;
                    float cov = Mathf.Clamp01((half - dist) * N + 0.5f) * Mathf.Lerp(aS, aE, t);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void Seg(Vector2 s, Vector2 e, float w)
        {
            Vector2 d = e - s;
            float len2 = Mathf.Max(d.sqrMagnitude, 1e-8f);
            float half = w * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.x, e.x) - half + 0.5f) * N) - 2);
            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(s.x, e.x) + half + 0.5f) * N) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(s.y, e.y) - half + 0.5f) * N) - 2);
            int y1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(s.y, e.y) + half + 0.5f) * N) + 2);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - s, d) / len2);
                    float dist = (p - (s + d * t)).magnitude;
                    float cov = Mathf.Clamp01((half - dist) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        // a solid shape: inside the polygon is opaque, with a soft one-pixel edge
        public void FillPoly(Vector2[] pts)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    bool inside = false;
                    float edge = float.MaxValue;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        if ((pts[i].y > p.y) != (pts[j].y > p.y) && p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
                        Vector2 d = pts[j] - pts[i];
                        float t = Mathf.Clamp01(Vector2.Dot(p - pts[i], d) / Mathf.Max(d.sqrMagnitude, 1e-8f));
                        edge = Mathf.Min(edge, (p - (pts[i] + d * t)).magnitude);
                    }
                    float cov = Mathf.Clamp01((inside ? 1f : -1f) * edge * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void Ring(float r, float w)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float cov = Mathf.Clamp01((w * 0.5f - Mathf.Abs(p.magnitude - r)) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void FillDot(Vector2 c, float radius)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radius + 0.5f) * N) - 2), x1 = Mathf.Min(N - 1, Mathf.CeilToInt((c.x + radius + 0.5f) * N) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((c.y - radius + 0.5f) * N) - 2), y1 = Mathf.Min(N - 1, Mathf.CeilToInt((c.y + radius + 0.5f) * N) + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float cov = Mathf.Clamp01((radius - (p - c).magnitude) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        // a small diamond (a stretched square) at a point, turned to point along rotDeg
        public void Poly4At(Vector2 centre, float r, float rotDeg, float w)
        {
            float rad = rotDeg * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var side = new Vector2(-fwd.y, fwd.x);
            Vector2 a0 = centre + fwd * r * 1.6f, a1 = centre + side * r * 0.8f, a2 = centre - fwd * r * 1.6f, a3 = centre - side * r * 0.8f;
            Seg(a0, a1, w); Seg(a1, a2, w); Seg(a2, a3, w); Seg(a3, a0, w);
        }

        // one small invented rune: kind 0 cross, 1 chevron, 2 diamond, 3 triangle, 4 sharp tick pair, 5 bent arrow
        public void Glyph(int kind, Vector2 c, float s, float rotDeg)
        {
            float rad = rotDeg * Mathf.Deg2Rad;
            var ux = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var uy = new Vector2(-ux.y, ux.x);
            Vector2 P(float x, float y) => c + ux * (x * s) + uy * (y * s);
            const float w = 0.0045f;

            switch (kind)
            {
                case 0:
                    Taper(P(0, -1), P(0, 1), 0.007f, 0.0015f); Taper(P(-0.8f, 0), P(0.8f, 0), 0.006f, 0.0015f);
                    break;
                case 1:
                    Taper(P(0, 0.9f), P(-1f, -0.7f), 0.008f, 0.0015f); Taper(P(0, 0.9f), P(1f, -0.7f), 0.008f, 0.0015f);
                    Taper(P(0, 0.1f), P(-0.7f, -1f), 0.006f, 0.001f, 0.6f); Taper(P(0, 0.1f), P(0.7f, -1f), 0.006f, 0.001f, 0.6f);
                    break;
                case 2:
                    Seg(P(0, 1.1f), P(0.7f, 0), w); Seg(P(0.7f, 0), P(0, -1.1f), w); Seg(P(0, -1.1f), P(-0.7f, 0), w); Seg(P(-0.7f, 0), P(0, 1.1f), w);
                    FillDot(c, 0.0045f);
                    break;
                case 3:
                    Seg(P(0, 1f), P(0.9f, -0.7f), w); Seg(P(0.9f, -0.7f), P(-0.9f, -0.7f), w); Seg(P(-0.9f, -0.7f), P(0, 1f), w);
                    break;
                case 4:
                    Taper(P(-0.5f, -1f), P(-0.5f, 1f), 0.007f, 0.0015f); Taper(P(0.5f, 1f), P(0.5f, -1f), 0.007f, 0.0015f);
                    break;
                default:
                    Seg(P(-1f, -0.6f), P(0f, 0.6f), w); Seg(P(0f, 0.6f), P(1f, -0.6f), w); Seg(P(0f, 0.6f), P(0f, -1f), w);
                    break;
            }
        }

        public void Poly(int sides, float r, float rotDeg, float w)
        {
            for (int i = 0; i < sides; i++) Seg(Vertex(i, sides, r, rotDeg), Vertex(i + 1, sides, r, rotDeg), w);
        }

        public void RingDots(int count, float r, float dotR)
        {
            for (int i = 0; i < count; i++) FillDot(Vertex(i, count, r, 90f), dotR);
        }

        static Vector2 Vertex(int i, int n, float r, float rotDeg)
        {
            float ang = (rotDeg + 360f * i / n) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }

        public void Arcs(float r, float w, int count, float fill, float rotDeg)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float ang = (Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg - rotDeg + 360f) % 360f;
                    float slot = 360f / count;
                    if (ang % slot > slot * fill) continue;
                    float cov = Mathf.Clamp01((w * 0.5f - Mathf.Abs(p.magnitude - r)) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void Runes(float rIn, float rOut, int count, int seed)
        {
            var rng = new System.Random(seed);
            float band = rOut - rIn;
            float rMid = (rIn + rOut) * 0.5f;
            float cellW = 2f * Mathf.PI * rMid / count;
            float glyph = Mathf.Min(cellW, band) * 0.72f;

            for (int i = 0; i < count; i++)
            {
                int n = rng.Next(2, 4);
                float ang = 2f * Mathf.PI * (i + 0.5f) / count;
                var radial = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var tangent = new Vector2(-radial.y, radial.x);
                Vector2 c = radial * rMid;

                for (int s = 0; s < n; s++)
                {
                    Vector2 s0 = new Vector2(rng.Next(-1, 2), rng.Next(-1, 2)) * 0.5f * glyph;
                    Vector2 s1 = new Vector2(rng.Next(-1, 2), rng.Next(-1, 2)) * 0.5f * glyph;
                    if (s0 == s1) s1 = new Vector2(s0.x, 0.5f * glyph);
                    Seg(c + tangent * s0.x + radial * s0.y, c + tangent * s1.x + radial * s1.y, 0.0075f);
                }
            }
        }
    }
}
