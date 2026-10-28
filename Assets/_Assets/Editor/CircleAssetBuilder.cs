using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Casting Circle Assets
// Generates placeholder circle and rune images, a material, ready-made casting circle assets, and assigns them to the
// example spells. Everything is a normal editable asset: swap the images, move layers, change the order.
// Running it again overwrites the generated images and circle assets (and re-assigns them to the example spells).
public static class CircleAssetBuilder
{
    const string ImageDir = "Assets/_Assets/Images/Circles";
    const string CircleDir = "Assets/_Assets/Circles";
    const string ResourcesDir = "Assets/_Assets/Resources";
    const string WeaponDir = "Assets/_Assets/Weapons";
    const int N = 512;

    [MenuItem("Tools/Magic/Build Casting Circle Assets")]
    public static void Build()
    {
        foreach (var d in new[] { ImageDir, CircleDir, ResourcesDir })
            Directory.CreateDirectory(d);

        // ---------- images ----------
        var tex = new System.Collections.Generic.Dictionary<string, Texture2D>();
        tex["Ring_Single"] = Save("Ring_Single", c => { c.Ring(0.47f, 0.012f); c.Ring(0.435f, 0.006f); });
        tex["Ring_Double"] = Save("Ring_Double", c => { c.Ring(0.47f, 0.014f); c.Ring(0.405f, 0.008f); c.Ticks(0.415f, 0.455f, 72, 0.004f); });
        tex["Rune_Band"] = Save("Rune_Band", c => { c.Ring(0.47f, 0.010f); c.Ring(0.335f, 0.010f); c.Runes(0.35f, 0.455f, 28, 7); });
        tex["Triangle"] = Save("Triangle", c => { c.Poly(3, 0.44f, 90f, 0.012f); c.Ring(0.465f, 0.008f); c.PolyDots(3, 0.44f, 90f, 0.02f); });
        tex["Hexagram"] = Save("Hexagram", c => { c.Poly(3, 0.42f, 90f, 0.012f); c.Poly(3, 0.42f, -90f, 0.012f); c.Ring(0.455f, 0.012f); });
        tex["Star8"] = Save("Star8", c => { c.Star(8, 3, 0.44f, 0.010f); c.Ring(0.465f, 0.010f); c.Ring(0.15f, 0.008f); });
        tex["Dots_Ring"] = Save("Dots_Ring", c => { c.RingDots(16, 0.43f, 0.018f); c.Ring(0.395f, 0.006f); c.Ring(0.47f, 0.006f); });
        tex["Segments"] = Save("Segments", c => { c.Arcs(0.45f, 0.016f, 6, 0.7f, 0f); c.Arcs(0.375f, 0.010f, 6, 0.55f, 30f); });
        tex["Cross"] = Save("Cross", c => { c.Ring(0.47f, 0.010f); c.Ring(0.3f, 0.006f); c.Seg(new Vector2(0, -0.47f), new Vector2(0, 0.47f), 0.006f); c.Seg(new Vector2(-0.47f, 0), new Vector2(0.47f, 0), 0.006f); c.Poly(4, 0.2f, 45f, 0.010f); });
        tex["Glow"] = Save("Glow", c => c.Glow());

        // ---------- material (the shader needs one that can be loaded at runtime) ----------
        string matPath = ResourcesDir + "/CastingCircleMaterial.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Magic/CastingCircle");
            if (shader == null) { Debug.LogError("Build Casting Circle Assets: shader 'Magic/CastingCircle' not found (it may still be compiling, try again)."); return; }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // ---------- circle assets ----------
        var arcane = Circle("Circle_Arcane", 1.3f,
            L("Glow", tex["Glow"], CircleAnchor.Front, 2.6f, 0f, 0.0f, 0.5f, CircleAppear.FadeIn, default, 0.35f),
            L("Rune Band", tex["Rune_Band"], CircleAnchor.Front, 2.0f, 25f, 0.0f, 0.55f),
            L("Outer Ring", tex["Ring_Double"], CircleAnchor.Front, 2.25f, -15f, 0.15f, 0.5f),
            L("Triangle", tex["Triangle"], CircleAnchor.Front, 1.2f, -50f, 0.35f, 0.4f, CircleAppear.ScaleUp),
            L("Side Star", tex["Star8"], CircleAnchor.Front, 0.8f, 90f, 0.5f, 0.35f, CircleAppear.RadialWipe, new Vector3(1.25f, 0.9f, 0.05f)),
            L("Side Dots", tex["Dots_Ring"], CircleAnchor.Front, 0.6f, -70f, 0.6f, 0.3f, CircleAppear.ScaleUp, new Vector3(-1.2f, -0.8f, 0.05f)));

        var fire = Circle("Circle_Fire", 1.3f,
            L("Hexagram", tex["Hexagram"], CircleAnchor.Front, 2.0f, 20f, 0.0f, 0.6f),
            L("Rune Band", tex["Rune_Band"], CircleAnchor.Front, 1.7f, -30f, 0.2f, 0.5f),
            L("Outer Ring", tex["Ring_Single"], CircleAnchor.Front, 2.35f, 10f, 0.0f, 0.4f),
            L("Floor Ring", tex["Ring_Double"], CircleAnchor.Feet, 3.2f, 10f, 0.3f, 0.6f),
            L("Floor Segments", tex["Segments"], CircleAnchor.Feet, 2.4f, -40f, 0.5f, 0.4f, CircleAppear.ScaleUp));

        var storm = Circle("Circle_Storm", 1.4f,
            L("Star", tex["Star8"], CircleAnchor.Front, 2.0f, 35f, 0.0f, 0.55f),
            L("Outer Ring", tex["Ring_Double"], CircleAnchor.Front, 2.35f, -20f, 0.1f, 0.5f),
            L("Cross", tex["Cross"], CircleAnchor.Front, 1.4f, 60f, 0.4f, 0.4f, CircleAppear.ScaleUp),
            L("Side Dots", tex["Dots_Ring"], CircleAnchor.Front, 0.7f, 70f, 0.55f, 0.3f, CircleAppear.RadialWipe, new Vector3(1.4f, -0.9f, 0.05f)),
            L("Side Segments", tex["Segments"], CircleAnchor.Front, 0.7f, -60f, 0.65f, 0.3f, CircleAppear.RadialWipe, new Vector3(-1.4f, 1.0f, 0.05f)));

        var ward = Circle("Circle_Ward", 1.3f,
            L("Hexagram", tex["Hexagram"], CircleAnchor.Front, 2.2f, 12f, 0.0f, 0.6f),
            L("Outer Ring", tex["Ring_Double"], CircleAnchor.Front, 2.6f, -10f, 0.1f, 0.5f),
            L("Rune Band", tex["Rune_Band"], CircleAnchor.Front, 1.9f, 22f, 0.3f, 0.5f),
            L("Floor Ring", tex["Ring_Single"], CircleAnchor.Feet, 3.0f, 15f, 0.4f, 0.5f),
            L("Floor Dots", tex["Dots_Ring"], CircleAnchor.Feet, 2.3f, -25f, 0.6f, 0.35f, CircleAppear.ScaleUp));

        // ground spell: the circle on the ground matches the real blast area (sizes are multipliers of the area's diameter)
        var meteor = Circle("Circle_Meteor", 1.2f,
            Aoe(L("Area Ring", tex["Ring_Double"], CircleAnchor.AimPoint, 1.0f, 8f, 0.0f, 0.5f)),
            Aoe(L("Area Runes", tex["Rune_Band"], CircleAnchor.AimPoint, 0.86f, -12f, 0.15f, 0.5f)),
            Aoe(L("Area Hexagram", tex["Hexagram"], CircleAnchor.AimPoint, 0.62f, 25f, 0.35f, 0.5f)),
            Aoe(L("Area Glow", tex["Glow"], CircleAnchor.AimPoint, 1.0f, 0f, 0.5f, 0.5f, CircleAppear.FadeIn, default, 0.25f)),
            L("Hand Star", tex["Star8"], CircleAnchor.Front, 0.9f, 40f, 0.0f, 0.4f),
            L("Floor Segments", tex["Segments"], CircleAnchor.Feet, 2.4f, -35f, 0.3f, 0.5f, CircleAppear.ScaleUp));

        meteor.readyPulse = 0.6f;   // the heavy ground spell keeps a pulsing swell while waiting to be released

        // the small quick circle for taps
        var tap = Circle("Circle_Tap", 1.1f,
            L("Ring", tex["Ring_Double"], CircleAnchor.Front, 1.4f, 60f, 0.0f, 0.6f),
            L("Runes", tex["Rune_Band"], CircleAnchor.Front, 1.05f, -90f, 0.2f, 0.6f));

        // ---------- the example spells: circles and cast times (stronger spells take longer) ----------
        // the last number says whether a hit can break the cast: only the strong, slow ones
        Assign("ArcaneBolt", arcane, tap, 0.9f, 0.6f, false);
        Assign("ScatterShot", arcane, tap, 1.1f, 0.8f, false);
        Assign("FireBurst", fire, tap, 1.1f, 1.0f, true);
        Assign("LightningLance", storm, tap, 1.0f, 0.9f, true);
        Assign("MeteorStrike", meteor, tap, 1.0f, 1.5f, true);
        Assign("ArcaneWard", ward, tap, 1.1f, 1.0f, false);

        // Spark Stream is rapid-fire: no cast time, but a small circle that draws in quickly and keeps spinning while the key is held
        var spark = Circle("Circle_Spark", 1.1f,
            L("Spinning Ring", tex["Ring_Double"], CircleAnchor.Front, 1.5f, 140f, 0.0f, 0.6f),
            L("Spinning Runes", tex["Rune_Band"], CircleAnchor.Front, 1.15f, -180f, 0.2f, 0.6f),
            L("Spinning Segments", tex["Segments"], CircleAnchor.Front, 0.8f, 260f, 0.4f, 0.5f, CircleAppear.ScaleUp));
        var sparkSpell = AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/SparkStream.asset");
        if (sparkSpell != null)
        {
            sparkSpell.holdCircle = spark;
            sparkSpell.tapCircle = null;
            sparkSpell.circleScale = 0.9f;
            sparkSpell.holdThreshold = 0.25f;     // for this spell it is only how long the circle takes to draw in
            EditorUtility.SetDirty(sparkSpell);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Casting circle assets built in " + CircleDir + " and assigned to the example spells.");
    }

    // ---------- spell assignment ----------

    static void Assign(string spellName, CastingCircleData hold, CastingCircleData tap, float scale, float castTime, bool interruptible)
    {
        var s = AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/" + spellName + ".asset");
        if (s == null) return;
        s.holdCircle = hold;
        s.tapCircle = tap;
        s.circleScale = scale;
        s.holdThreshold = castTime;
        s.interruptible = interruptible;
        s.interruptDamage = 5f;
        s.tapWindow = 0.2f;
        EditorUtility.SetDirty(s);
    }

    // ---------- circle asset helpers ----------

    static CircleLayer L(string name, Texture2D tex, CircleAnchor anchor, float size, float rot, float startAt, float drawTime,
        CircleAppear appear = CircleAppear.RadialWipe, Vector3 offset = default, float opacity = 1f)
    {
        return new CircleLayer
        {
            name = name, texture = tex, anchor = anchor, size = size, rotationSpeed = rot,
            startAt = startAt, drawTime = drawTime, appear = appear, offset = offset, opacity = opacity,
        };
    }

    static CircleLayer Aoe(CircleLayer l) { l.scaleToAoeRadius = true; return l; }

    static CastingCircleData Circle(string name, float frontDistance, params CircleLayer[] layers)
    {
        string path = CircleDir + "/" + name + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<CastingCircleData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<CastingCircleData>();
            AssetDatabase.CreateAsset(d, path);
        }
        d.frontDistance = frontDistance;
        d.layers = layers;
        EditorUtility.SetDirty(d);
        return d;
    }

    // =====================================================================
    // image generation (white shapes with soft alpha, tinted by the spell colour at runtime)
    // =====================================================================

    static Texture2D Save(string name, Action<Canvas> draw)
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
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // a square of alpha values; shapes are measured in units of the image width, centred, so the edge is +-0.5
    class Canvas
    {
        public readonly float[] a = new float[N * N];

        // soft anti-aliased coverage from a distance (in image-width units) to the shape's centre line
        void Stroke(Func<Vector2, float> distance, float width)
        {
            float half = width * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float cov = Mathf.Clamp01((half - distance(p)) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void Ring(float r, float w) => Stroke(p => Mathf.Abs(p.magnitude - r), w);

        public void Seg(Vector2 s, Vector2 e, float w) => Stroke(p => SegDist(p, s, e), w);

        static float SegDist(Vector2 p, Vector2 s, Vector2 e)
        {
            Vector2 d = e - s;
            float t = Mathf.Clamp01(Vector2.Dot(p - s, d) / Mathf.Max(d.sqrMagnitude, 1e-8f));
            return (p - (s + d * t)).magnitude;
        }

        // filled dot
        public void FillDot(Vector2 c, float radius)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float cov = Mathf.Clamp01((radius - (p - c).magnitude) * N + 0.5f);
                    if (cov > a[y * N + x]) a[y * N + x] = cov;
                }
        }

        public void Poly(int sides, float r, float rotDeg, float w)
        {
            for (int i = 0; i < sides; i++)
                Seg(Vertex(i, sides, r, rotDeg), Vertex(i + 1, sides, r, rotDeg), w);
        }

        public void PolyDots(int sides, float r, float rotDeg, float dotR)
        {
            for (int i = 0; i < sides; i++) FillDot(Vertex(i, sides, r, rotDeg), dotR);
        }

        // star made by joining every 'skip'th point on a circle, e.g. 8 points skipping 3
        public void Star(int points, int skip, float r, float w)
        {
            for (int i = 0; i < points; i++)
                Seg(Vertex(i, points, r, 90f), Vertex(i + skip, points, r, 90f), w);
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

        // short radial tick marks between two radii
        public void Ticks(float rIn, float rOut, int count, float w)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = 2f * Mathf.PI * i / count;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float len = (i % 6 == 0) ? rOut : Mathf.Lerp(rIn, rOut, 0.55f);
                Seg(dir * rIn, dir * len, w);
            }
        }

        // broken ring: 'count' arcs, each covering 'fill' of its slot
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

        // a ring of invented glyphs: each cell gets a few random strokes
        public void Runes(float rIn, float rOut, int count, int seed)
        {
            var rng = new System.Random(seed);
            float band = rOut - rIn;
            float rMid = (rIn + rOut) * 0.5f;
            float cellW = 2f * Mathf.PI * rMid / count;
            float glyph = Mathf.Min(cellW, band) * 0.72f;

            for (int i = 0; i < count; i++)
            {
                var strokes = new System.Collections.Generic.List<(Vector2, Vector2)>();
                int n = rng.Next(2, 4);
                for (int s = 0; s < n; s++)
                {
                    Vector2 s0 = new Vector2(rng.Next(-1, 2), rng.Next(-1, 2)) * 0.5f;
                    Vector2 s1 = new Vector2(rng.Next(-1, 2), rng.Next(-1, 2)) * 0.5f;
                    if (s0 == s1) s1 = new Vector2(s0.x, -s0.y == s0.y ? 0.5f : -s0.y);
                    strokes.Add((s0 * glyph, s1 * glyph));
                }

                float ang = 2f * Mathf.PI * (i + 0.5f) / count;
                var radial = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var tangent = new Vector2(-radial.y, radial.x);
                Vector2 c = radial * rMid;

                foreach (var (s0, s1) in strokes)
                {
                    Vector2 w0 = c + tangent * s0.x + radial * s0.y;
                    Vector2 w1 = c + tangent * s1.x + radial * s1.y;
                    Seg(w0, w1, 0.0075f);
                }
            }
        }

        public void Glow()
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float d = Mathf.Clamp01(1f - p.magnitude / 0.5f);
                    a[y * N + x] = d * d * 0.6f;
                }
        }
    }
}
