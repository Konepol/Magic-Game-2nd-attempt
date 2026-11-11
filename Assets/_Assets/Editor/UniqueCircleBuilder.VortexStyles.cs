using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Vortex Style Circles
// Gives the other spells the STYLE of the finished vortex circle (Circle_Vortex, on Arcane Bolt), not a copy of it: the same building
// blocks (arrow ring, S-shaped arms, rings of double-V pieces, polygon pairs with filled tips) with a different arm count and curve,
// a different polygon, a different number of ring pieces, an accent colour and spin speeds for each spell.
// Circle_Vortex is only read. Each spell gets a new asset Circle_VStyle_<Spell>; the older circles stay in the project.
// Running this again rebuilds the Circle_VStyle_ assets (and re-assigns them), so do not tweak those in the Inspector first.
public static partial class UniqueCircleBuilder
{
    struct VStyle
    {
        public string spell, dir;
        public int arms;
        public float sweep, amp;
        public bool jagged;
        public int poly;                 // sides of the polygon pair
        public int pieces;               // pieces on the double-V rings
        public Color accent;
        public float spin;               // multiplies every spin of the circle
        public bool ground;              // polygon pair and double-V rings lie on the ground at the aim point (area spells)
        public bool purple;              // venom: purple whatever the spell colour is
    }

    static VStyle S(string spell, string dir, int arms, float sweep, float amp, bool jagged, int poly, int pieces, Color accent, float spin, bool ground = false, bool purple = false)
        => new VStyle { spell = spell, dir = dir, arms = arms, sweep = sweep, amp = amp, jagged = jagged, poly = poly, pieces = pieces, accent = accent, spin = spin, ground = ground, purple = purple };

    [MenuItem("Tools/Magic/Build Vortex Style Circles")]
    public static void BuildVortexStyleCircles()
    {
        Tex.Clear();
        var template = AssetDatabase.LoadAssetAtPath<CastingCircleData>(CircleDir + "/Circle_Vortex.asset");
        if (template == null) { Debug.LogError("Circle_Vortex.asset not found"); return; }

        var fireC = new Color(1f, 0.85f, 0.4f); var iceC = new Color(0.8f, 0.95f, 1f); var windC = new Color(0.85f, 1f, 0.9f);
        var goldC = new Color(1f, 0.8f, 0.4f); var whiteC = Color.white; var limeC = new Color(0.8f, 1f, 0.3f);
        var sunC = new Color(1f, 0.95f, 0.65f); var emberC = new Color(1f, 0.7f, 0.35f); var violetC = new Color(0.9f, 0.85f, 1f);

        string W = WeaponDir, C = ComboDir;
        var styles = new List<VStyle>
        {
            // basic spells
            S("FireBurst", W, 6, 0.30f, 0.32f, false, 3, 3, fireC, 1.3f),
            S("FrostShard", W, 6, 0.05f, 0.10f, false, 6, 6, iceC, 0.6f),
            S("WindBlade", W, 5, 1.00f, 0.45f, false, 5, 5, windC, 1.6f),
            S("EarthSpike", W, 4, 0.00f, 0.05f, false, 4, 4, goldC, 0.5f, ground: true),
            S("LightningLance", W, 8, 0.00f, 0.35f, true, 8, 8, whiteC, 1.8f),
            S("VenomDart", W, 7, 0.70f, 0.40f, false, 7, 7, limeC, 1.1f, purple: true),
            S("RadiantBeam", W, 12, 0.00f, 0.03f, false, 12, 6, sunC, 0.7f),
            S("ScatterShot", W, 9, 0.30f, 0.35f, false, 9, 5, goldC, 1.2f),
            S("ArcaneWard", W, 6, 0.15f, 0.15f, false, 6, 6, violetC, 0.5f),
            S("MeteorStrike", W, 5, 0.50f, 0.20f, false, 5, 5, emberC, 0.8f, ground: true),
            // fusions: the style of the spells they are made of, mixed
            S("InfernalBolt", C, 6, 0.30f, 0.30f, false, 6, 6, fireC, 1.3f),
            S("CinderStorm", C, 9, 0.40f, 0.40f, false, 9, 7, emberC, 1.4f),
            S("FirestormLance", C, 8, 0.00f, 0.35f, true, 3, 3, fireC, 1.7f),
            S("ThunderMeteor", C, 8, 0.00f, 0.30f, true, 4, 4, whiteC, 1.2f, ground: true),
            S("GlacierBolt", C, 6, 0.05f, 0.10f, false, 6, 3, iceC, 0.7f),
            S("BlizzardLance", C, 6, 0.00f, 0.18f, true, 6, 6, iceC, 1.2f),
            S("Cyclone", C, 5, 1.20f, 0.50f, false, 5, 5, goldC, 1.8f),
            S("TremorStorm", C, 4, 0.00f, 0.25f, true, 4, 4, goldC, 0.8f, ground: true),
            S("PlagueStorm", C, 7, 0.80f, 0.50f, false, 7, 7, limeC, 1.2f),
            S("AbsoluteZero", C, 6, 0.05f, 0.08f, false, 6, 6, iceC, 0.4f, ground: true),
            S("SunLance", C, 12, 0.00f, 0.03f, false, 12, 6, sunC, 0.9f),
        };

        var madePoly = new HashSet<int>();
        foreach (var st in styles)
        {
            string key = st.spell;
            MakeStyleArms("VArms_" + key + "_A", st, 0f, 0f);
            MakeStyleArms("VArms_" + key + "_B", st, Mathf.PI / st.arms, 0.5f);
            if (madePoly.Add(st.poly)) MakePolygonPair(st.poly);

            var asset = ApplyStyle(template, st);
            var spell = AssetDatabase.LoadAssetAtPath<SpellData>(st.dir + "/" + st.spell + ".asset");
            if (spell == null) { Debug.LogWarning("Vortex style: spell " + st.spell + " not found."); continue; }
            spell.holdCircle = asset;
            EditorUtility.SetDirty(spell);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vortex style circles built (" + styles.Count + ") and assigned. Circle_Vortex was not changed.");
    }

    // ---------- images ----------

    // S-shaped arms like the vortex's; sweep and amp set how curved they are, jagged makes them zig-zag (lightning)
    static void MakeStyleArms(string name, VStyle st, float angleOffset, float phase)
    {
        Make(name, c =>
        {
            var rng = new System.Random(st.arms * 31 + (int)(phase * 10f) + st.spell.Length);
            for (int i = 0; i < st.arms; i++)
            {
                float a0 = 2f * Mathf.PI * i / st.arms + angleOffset;
                var pts = new List<Vector2>();
                if (st.jagged)
                {
                    int segs = 9;
                    float jitter = 0f;
                    for (int k = 0; k <= segs; k++)
                    {
                        float t = k / (float)segs;
                        jitter = (k % 2 == 0 ? 1f : -1f) * st.amp * (0.5f + 0.5f * (float)rng.NextDouble());
                        float ang = a0 + st.sweep * t + (k == 0 ? 0f : jitter * 0.55f);
                        pts.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(0.05f, 0.46f, t));
                    }
                    c.TaperPath(pts, 0.013f, 0.004f, 0.9f);
                }
                else
                {
                    for (int k = 0; k <= 48; k++)
                    {
                        float t = k / 48f;
                        float ang = a0 + st.sweep * t + st.amp * Mathf.Sin(t * 2f * Mathf.PI * 0.9f + phase);
                        pts.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Mathf.Lerp(0.05f, 0.46f, t));
                    }
                    c.TaperPath(pts, 0.013f, 0.004f, 0.9f);
                }
            }
        });
    }

    // two n-sided polygons, one turned half a step from the other; the tips of one that stick out past the other are filled
    static void MakePolygonPair(int n)
    {
        const float R = 0.455f;
        float apothemB = R * Mathf.Cos(Mathf.PI / n);                        // distance of the other polygon's flat edge
        float t = (R - apothemB) / (R - R * Mathf.Cos(2f * Mathf.PI / n));   // how far along A's edge the crossing is

        Make("VPoly_" + n + "_Lines", c =>
        {
            c.Poly(n, R, 90f, 0.006f);
            c.Poly(n, R, 90f + 180f / n, 0.006f);
            foreach (var tri in Tips(n, R, t)) c.FillPoly(tri);
        });
        Make("VPoly_" + n + "_Edge", c =>
        {
            foreach (var tri in Tips(n, R, t)) { c.Seg(tri[0], tri[1], 0.009f); c.Seg(tri[0], tri[2], 0.009f); }
        });
    }

    static List<Vector2[]> Tips(int n, float R, float t)
    {
        var list = new List<Vector2[]>();
        for (int i = 0; i < n; i++)
        {
            Vector2 V = Vtx(i, n, R), Wp = Vtx(i + 1, n, R), Wm = Vtx(i - 1, n, R);
            list.Add(new[] { V, V + (Wp - V) * t, V + (Wm - V) * t });
        }
        return list;
    }

    static Vector2 Vtx(int i, int n, float R)
    {
        float a = (90f + 360f * i / n) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
    }

    // ---------- the circle asset ----------

    static CastingCircleData ApplyStyle(CastingCircleData template, VStyle st)
    {
        string path = CircleDir + "/Circle_VStyle_" + st.spell + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<CastingCircleData>(path);
        if (d == null) { d = ScriptableObject.CreateInstance<CastingCircleData>(); AssetDatabase.CreateAsset(d, path); }
        EditorUtility.CopySerialized(template, d);          // the whole vortex, then changed below
        d.name = "Circle_VStyle_" + st.spell;               // the copy also copied the template's name

        Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(ImageDir + "/" + n + ".png");
        var purple = new Color(0.55f, 0.12f, 0.95f);

        foreach (var l in d.layers)
        {
            l.spinSpeed *= st.spin;
            bool hexLayer = l.name == "Hexagons" || l.name == "Hexagon Edge";
            bool orbitRing = l.arrowRing && !l.pointToCenter;

            if (l.name == "Arms Outer") l.texture = T("VArms_" + st.spell + "_A");
            else if (l.name == "Arms Inner") l.texture = T("VArms_" + st.spell + "_B");
            else if (l.name == "Hexagons") l.texture = T("VPoly_" + st.poly + "_Lines");
            else if (l.name == "Hexagon Edge") { l.texture = T("VPoly_" + st.poly + "_Edge"); l.tint = st.accent; }

            if (l.arrowRing) l.arrowAccent = st.accent;
            if (orbitRing) l.pieceCount = Mathf.Clamp(st.pieces, 1, 8);

            // area spells: the polygons and the double-V rings lie on the ground where the spell lands (sizes become multipliers of the area)
            if (st.ground && (hexLayer || orbitRing))
            {
                l.anchor = CircleAnchor.AimPoint;
                l.scaleToAoeRadius = true;
                l.size = hexLayer ? (l.size > 1f ? 0.7f : 0.42f) : (l.size > 2f ? 0.95f : 0.6f);
                l.offset = Vector3.zero;
            }

            if (st.purple && !(l.name == "Hexagon Edge") && !l.arrowRing)
            {
                l.useSpellColor = false;
                l.tint = purple;
            }
        }
        EditorUtility.SetDirty(d);
        return d;
    }
}
