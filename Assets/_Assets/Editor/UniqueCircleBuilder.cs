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
                Vector2 prev = default;
                for (int i = 0; i <= 36; i++)
                {
                    float t = i / 36f;
                    float ang = baseAng + t * 2.6f;
                    Vector2 p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(0.05f, 0.42f, t);
                    if (i > 0) c.Seg(prev, p, 0.012f);
                    prev = p;
                }
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
                c.Seg(d * (longRay ? 0.22f : 0.3f), d * (longRay ? 0.46f : 0.39f), longRay ? 0.012f : 0.008f);
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

        var venom = Circle("Circle_Venom", 1.3f, Tilt(9f, 1.8f), Ready(new Color(0.8f, 1f, 0.5f), 0.6f),
            L("Fangs", "Fangs", CircleAnchor.Front, 2.2f, -35f, 0.0f, 0.6f),
            L("Fangs Counter", "Fangs", CircleAnchor.Front, 1.4f, 60f, 0.25f, 0.5f, CircleAppear.ScaleUp),
            L("Drops", "Dots_Ring", CircleAnchor.Front, 2.6f, 25f, 0.1f, 0.5f),
            L("Floor Fangs", "Fangs", CircleAnchor.Feet, 2.8f, -20f, 0.4f, 0.6f),
            L("Floor Scatter", "Scatter", CircleAnchor.Feet, 2.0f, 30f, 0.6f, 0.4f, CircleAppear.ScaleUp));

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

        // ---------- assign ----------
        Assign(WeaponDir, "ScatterShot", scatter);
        Assign(WeaponDir, "FireBurst", fire);
        Assign(WeaponDir, "LightningLance", lightning);
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
        Debug.Log("Unique casting circles built in " + CircleDir + " and assigned to 8 spells and 11 fusions.");
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
