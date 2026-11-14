using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Ground Indicators
// Sets up the ground indicator textures, the dark outline material, and three ready-made indicator assets in Resources:
//   GroundIndicator_Circle  (the default marker for area spells that have no casting circle: it replaces the plain ring)
//   GroundIndicator_Cone, GroundIndicator_Line  (assign to a spell with SpellData > Aim Indicator, and set its length and width)
// The layer tables below are the design: sizes are fractions of the whole indicator, spins are degrees per second, start and draw
// times are fractions of the draw-on time. Running it again rebuilds the three assets from these tables.
public static class GroundIndicatorBuilder
{
    const string TextureDir = "Assets/_Assets/Images/GroundIndicators";
    const string ResourcesDir = "Assets/_Assets/Resources";
    const string GlowPath = "Assets/_Assets/Images/Circles/Glow.png";

    [MenuItem("Tools/Magic/Build Ground Indicators")]
    public static void Build()
    {
        Directory.CreateDirectory(ResourcesDir);
        AssetDatabase.Refresh();

        // textures: mip maps on (the glow and the dark outline are made from blurred low mip levels), clamped, no tiling
        foreach (var file in Directory.GetFiles(TextureDir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        // dark outline material (loaded at runtime from Resources, like the casting circle material)
        string matPath = ResourcesDir + "/GroundIndicatorOutlineMaterial.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
        {
            var shader = Shader.Find("Magic/GroundIndicatorOutline");
            if (shader == null) { Debug.LogError("Build Ground Indicators: shader 'Magic/GroundIndicatorOutline' not found (it may still be compiling, try again)."); return; }
            AssetDatabase.CreateAsset(new Material(shader), matPath);
        }

        var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath);
        Texture2D T(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "/" + name + ".png");
            if (t == null) Debug.LogError("Build Ground Indicators: missing texture " + name + " in " + TextureDir);
            return t;
        }

        // ---------- circle: diameter = the area's diameter ----------
        Make("GroundIndicator_Circle", IndicatorShape.Circle,
            Glow(glow, 1.1f, Vector2.zero),
            Layer("Band", T("GroundCircle_Band"), 1f, -45f, 0f, 0.7f, IndicatorReveal.Clockwise, 0.9f),
            Layer("Outer Track", T("GroundCircle_Outer"), 1f, 22f, 0f, 0.6f, IndicatorReveal.Clockwise, 0.9f),
            Layer("Inner Ring", T("GroundCircle_Ring"), 1f, 75f, 0.2f, 0.6f, IndicatorReveal.Clockwise, 1f),
            Layer("Core", T("GroundCircle_Core"), 1f, -28f, 0.4f, 0.6f, IndicatorReveal.Burst, 1f),
            Fill("Charge Fill", T("GroundCircle_ChargeFill"), IndicatorReveal.Clockwise),
            Echo("Echo Outer", T("GroundCircle_Outer"), 1.047f, -30f, 0f, 0.6f, 0.45f),
            Echo("Echo Ring", T("GroundCircle_Ring"), 0.983f, -95f, 0.2f, 0.6f, 0.4f));

        // ---------- cone: its tip is at the caster; length = how far it reaches ----------
        Make("GroundIndicator_Cone", IndicatorShape.Cone,
            Glow(glow, 0.7f, new Vector2(0f, 0.325f)),
            Layer("Track", T("GroundCone_Track"), 1f, 0f, 0f, 0.6f, IndicatorReveal.FromCentre, 0.9f),
            Layer("Band", T("GroundCone_Band"), 1f, 0f, 0.1f, 0.6f, IndicatorReveal.FromCentre, 0.9f),
            Swing(Layer("Inner Arcs", T("GroundCone_Ring"), 1f, 0f, 0.25f, 0.5f, IndicatorReveal.FromCentre, 1f), 6f, 3.2f),
            Layer("Core", T("GroundCircle_Core"), 0.2f, -28f, 0.4f, 0.6f, IndicatorReveal.Burst, 1f),
            Fill("Charge Fill", T("GroundCone_ChargeFill"), IndicatorReveal.FromCentre));

        // ---------- line: its near end is at the caster; length = how far it reaches, width = how wide the strip is ----------
        var star = Layer("Star", T("GroundLine_Star"), 0.85f, -28f, 0.4f, 0.6f, IndicatorReveal.Burst, 1f);
        star.absoluteSize = true;                       // metres: it stays the size of the strip however long the line is
        star.offset = new Vector2(0f, 0.217f);
        Make("GroundIndicator_Line", IndicatorShape.Line,
            Layer("Track", T("GroundLine_Track"), 1f, 0f, 0f, 0.6f, IndicatorReveal.FromBottom, 0.9f),
            Layer("Band", T("GroundLine_Band"), 1f, 0f, 0.1f, 0.6f, IndicatorReveal.FromBottom, 0.9f),
            Slide(Layer("Arcs", T("GroundLine_Ring"), 1f, 0f, 0.9f, 0.1f, IndicatorReveal.FadeIn, 0.9f), 0f),
            Slide(Layer("Arcs 2", T("GroundLine_Ring"), 1f, 0f, 0.9f, 0.1f, IndicatorReveal.FadeIn, 0.5f), 0.5f),
            star,
            Fill("Charge Fill", T("GroundLine_ChargeFill"), IndicatorReveal.FromBottom));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Build Ground Indicators: done. The circle replaces the plain aim ring for area spells without a casting circle. " +
                  "For a cone or line, assign GroundIndicator_Cone or GroundIndicator_Line to a spell (SpellData > Aim Indicator).");
    }

    static IndicatorLayer Layer(string name, Texture2D tex, float size, float spin, float start, float draw, IndicatorReveal appear, float opacity)
    {
        return new IndicatorLayer
        {
            name = name, texture = tex, size = size, spin = spin, startAt = start, drawTime = draw, appear = appear,
            opacity = opacity, useSpellColor = true, tint = Color.white, outline = true,
        };
    }

    // the soft pool of light under the indicator
    static IndicatorLayer Glow(Texture2D tex, float size, Vector2 offset)
    {
        var l = Layer("Glow", tex, size, 0f, 0f, 0.5f, IndicatorReveal.FadeIn, 0.18f);
        l.tint = new Color(0.8f, 0.8f, 0.8f, 1f);
        l.offset = offset;
        l.outline = false;
        return l;
    }

    // the brighter fill that grows as the spell charges
    static IndicatorLayer Fill(string name, Texture2D tex, IndicatorReveal reveal)
    {
        var l = Layer(name, tex, 1f, 0f, 0f, 0.1f, IndicatorReveal.FadeIn, 0.7f);
        l.chargeFill = true;
        l.chargeReveal = reveal;
        l.outline = false;
        return l;
    }

    // a fainter copy that turns the other way, so the layers seem to sit at different heights
    static IndicatorLayer Echo(string name, Texture2D tex, float size, float spin, float start, float draw, float opacity)
    {
        var l = Layer(name, tex, size, spin, start, draw, IndicatorReveal.Clockwise, opacity);
        l.outline = false;
        return l;
    }

    static IndicatorLayer Swing(IndicatorLayer l, float degrees, float period)
    {
        l.swingDegrees = degrees;
        l.swingPeriod = period;
        return l;
    }

    static IndicatorLayer Slide(IndicatorLayer l, float phase)
    {
        l.slideSpan = 0.5f;
        l.slidePeriod = 2.4f;
        l.slidePhase = phase;
        l.slideShift = -0.02f;
        return l;
    }

    static void Make(string name, IndicatorShape shape, params IndicatorLayer[] layers)
    {
        string path = ResourcesDir + "/" + name + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<GroundIndicatorData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GroundIndicatorData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.shape = shape;
        data.layers = layers;
        EditorUtility.SetDirty(data);
    }
}
