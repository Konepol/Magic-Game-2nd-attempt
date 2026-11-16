using UnityEditor;
using UnityEngine;

// M1 part 9: one casting circle per spell form, put together from the existing circle textures. It creates NEW circle assets only
// (Circle_<Spell>_<Form>.asset) and points the five M1 spell assets at them. It does not run the circle builder and does not touch any other
// circle or spell asset. Existing circle assets with the same names are left alone, so tweaks to them survive a second run.
public static class M1CircleAssets
{
    const string Tex = "Assets/_Assets/Images/Circles/";
    const string Out = "Assets/_Assets/Circles/";
    const string Spells = "Assets/_Assets/Spells/";

    [MenuItem("Tools/Magic/M1 Part 9: Build a circle for each M1 spell form")]
    public static void Run()
    {
        // ---- Weaver: a loom. A winding thread, stitch dots, needle ticks. The circle un-draws like thread being pulled out.
        var weaverHold = Make("Circle_Weaver_Hold", exit: CircleExit.Unwind, front: 1.3f, tilt: 6f,
            L("Soft Glow", "Glow", 2.7f, 0f, 0f, 0.5f, CircleAppear.FadeIn, 0.3f),
            L("Thread", "Spiral", 2.1f, 40f, 0f, 0.6f, CircleAppear.RadialWipe),
            L("Stitches", "Dots_Ring", 2.4f, -35f, 0.2f, 0.5f, CircleAppear.RadialWipe),
            L("Frame", "Ring_Single", 2.55f, 12f, 0.4f, 0.5f, CircleAppear.RadialWipe),
            L("Needles", "Segments", 1.5f, 70f, 0.55f, 0.4f, CircleAppear.SpinIn, 0.9f));
        var weaverTap = Make("Circle_Weaver_Tap", exit: CircleExit.Unwind, front: 1.2f, tilt: 4f,
            L("Thread", "Spiral", 1.8f, 90f, 0f, 0.8f, CircleAppear.RadialWipe),
            L("Stitches", "Dots_Ring", 2.1f, -60f, 0.1f, 0.8f, CircleAppear.RadialWipe, 0.9f));

        // ---- Hollow Moon: an eclipse. Heavy double ring, moon-phase segments, a dark core. It collapses inward when released.
        var moonHold = Make("Circle_HollowMoon_Hold", exit: CircleExit.Implode, front: 1.6f, tilt: 10f, ready: 0.15f,
            L("Shadow", "Glow", 3.2f, 0f, 0f, 0.5f, CircleAppear.FadeIn, 0.45f),
            L("Eclipse Ring", "Ring_Double", 2.7f, 8f, 0f, 0.5f, CircleAppear.RadialWipe),
            L("Phases", "Segments", 2.25f, -18f, 0.2f, 0.45f, CircleAppear.RadialWipe),
            L("Runes", "Rune_Band", 1.9f, 25f, 0.35f, 0.4f, CircleAppear.RadialWipe),
            L("Hollow", "Crystal", 1.2f, 0f, 0.55f, 0.35f, CircleAppear.Stamp, 1f));
        var moonGreat = Make("Circle_HollowMoon_Great", exit: CircleExit.Overload, front: 1.9f, tilt: 12f, ready: 0.2f,
            L("Corona", "Rays", 3.7f, 14f, 0f, 0.6f, CircleAppear.RadialOut, 0.85f),
            L("Outer Ring", "Ring_Double", 3.5f, -10f, 0.1f, 0.5f, CircleAppear.RadialWipe),
            L("Spikes", "Spikes", 3.0f, -24f, 0.25f, 0.5f, CircleAppear.SpinIn, 0.9f),
            L("Seal", "Hexagram", 2.4f, 30f, 0.5f, 0.45f, CircleAppear.Bloom));
        var moonTap = Make("Circle_HollowMoon_Tap", exit: CircleExit.SpinAway, front: 1.2f, tilt: 4f,
            L("Crescent", "Fangs", 1.8f, 220f, 0f, 0.8f, CircleAppear.SpinIn),
            L("Ring", "Ring_Single", 2.0f, -40f, 0.1f, 0.8f, CircleAppear.RadialWipe, 0.9f));

        // ---- Mirror Bloom: a glass flower. Petals open (Bloom), hex facets, and it shatters when it goes.
        var bloomHold = Make("Circle_MirrorBloom_Hold", exit: CircleExit.Shatter, front: 1.3f, tilt: 8f,
            L("Petals", "Flame_Petals", 2.3f, 18f, 0f, 0.55f, CircleAppear.Bloom),
            L("Facets", "Hex_Edge", 2.55f, -10f, 0.15f, 0.5f, CircleAppear.RadialWipe),
            L("Glass", "Hex_Lines", 1.8f, 30f, 0.35f, 0.45f, CircleAppear.FadeIn, 0.8f),
            L("Frame", "Ring_Single", 2.75f, 6f, 0.5f, 0.4f, CircleAppear.RadialWipe));
        var bloomTap = Make("Circle_MirrorBloom_Tap", exit: CircleExit.Shatter, front: 1.2f, tilt: 4f,
            L("Petals", "Flame_Petals", 1.9f, 50f, 0f, 0.8f, CircleAppear.Pop),
            L("Facets", "Hex_Edge", 2.15f, -25f, 0.1f, 0.8f, CircleAppear.RadialWipe, 0.9f));

        // ---- Lantern: a wick and its light. Rays, an eight-point star, a ring of sparks. It scatters like embers.
        var lanternHold = Make("Circle_Lantern_Hold", exit: CircleExit.Dissolve, front: 1.2f, tilt: 5f, ready: 0.1f,
            L("Light", "Glow", 3f, 0f, 0f, 0.5f, CircleAppear.FadeIn, 0.5f),
            L("Rays", "Rays", 2.8f, 10f, 0.1f, 0.5f, CircleAppear.ScaleUp, 0.9f),
            L("Sparks", "Dots_Ring", 2.35f, -20f, 0.25f, 0.45f, CircleAppear.RadialWipe),
            L("Flame", "Star8", 1.7f, -20f, 0.4f, 0.45f, CircleAppear.Burst));
        var lanternBeacon = Make("Circle_Lantern_Beacon", exit: CircleExit.Shockwave, front: 1.4f, tilt: 6f, ready: 0.2f,
            L("Column Rays", "Rays", 3.8f, 8f, 0f, 0.6f, CircleAppear.RadialOut, 0.9f),
            L("Cross", "Flare_Cross", 3.1f, 0f, 0.2f, 0.5f, CircleAppear.Flash, 1f),
            L("Seal", "Star8", 2.4f, 25f, 0.4f, 0.45f, CircleAppear.SpinIn));
        var lanternTap = Make("Circle_Lantern_Tap", exit: CircleExit.Dissolve, front: 1.1f, tilt: 3f,
            L("Flame", "Star8", 1.6f, 30f, 0f, 0.8f, CircleAppear.Flash),
            L("Sparks", "Dots_Ring", 1.9f, -40f, 0.1f, 0.8f, CircleAppear.RadialWipe, 0.8f));

        // ---- Veil Step: a gate. A star, a triangle, a ring of petals. It shoots forward the way the player is thrown.
        var veilHold = Make("Circle_VeilStep_Hold", exit: CircleExit.Launch, front: 1.3f, tilt: 6f,
            L("Star", "Star8", 2.4f, 35f, 0f, 0.5f, CircleAppear.SpinIn),
            L("Gate", "Triangle", 2.0f, -30f, 0.15f, 0.5f, CircleAppear.RadialWipe),
            L("Frame", "Ring_Single", 2.65f, 10f, 0.3f, 0.45f, CircleAppear.RadialWipe),
            L("Petals", "Flame_Petals", 1.6f, -60f, 0.5f, 0.4f, CircleAppear.Bloom));
        var veilTap = Make("Circle_VeilStep_Tap", exit: CircleExit.Launch, front: 1.1f, tilt: 3f,
            L("Gate", "Triangle", 1.7f, 80f, 0f, 0.8f, CircleAppear.SpinIn),
            L("Frame", "Ring_Single", 2.0f, -50f, 0.1f, 0.8f, CircleAppear.RadialWipe, 0.9f));

        Assign("Weaver", tap: weaverTap, hold: weaverHold);
        Assign("HollowMoon", tap: moonTap, hold: moonHold, tier0: moonGreat);
        Assign("MirrorBloom", tap: bloomTap, hold: bloomHold);
        Assign("Lantern", tap: lanternTap, hold: lanternHold, tier0: lanternBeacon);
        Assign("VeilStep", tap: veilTap, hold: veilHold);
        AssetDatabase.SaveAssets();
        Debug.Log("M1 circles built: one per spell form (Weaver, Hollow Moon, Mirror Bloom, Lantern, Veil Step).");
    }

    static CircleLayer L(string name, string texture, float size, float spin, float start, float draw, CircleAppear appear, float opacity = 1f)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + texture + ".png");
        if (tex == null) Debug.LogWarning("Circle texture missing: " + texture);
        return new CircleLayer
        {
            name = name, texture = tex, size = size, rotationSpeed = spin, startAt = start, drawTime = draw, appear = appear,
            opacity = opacity, anchor = CircleAnchor.Front, useSpellColor = true, tint = Color.white,
        };
    }

    static CastingCircleData Make(string name, CircleExit exit, float front, float tilt, params CircleLayer[] layers) =>
        Make(name, exit, front, tilt, 0f, layers);

    static CastingCircleData Make(string name, CircleExit exit, float front, float tilt, float ready, params CircleLayer[] layers)
    {
        string path = Out + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<CastingCircleData>(path);
        if (existing != null) return existing;

        var d = ScriptableObject.CreateInstance<CastingCircleData>();
        d.frontDistance = front;
        d.exitStyle = exit;
        d.tiltDegrees = tilt;
        d.readyPulse = ready;
        d.layers = layers;
        AssetDatabase.CreateAsset(d, path);
        return d;
    }

    static void Assign(string spellName, CastingCircleData tap, CastingCircleData hold, CastingCircleData tier0 = null)
    {
        var s = AssetDatabase.LoadAssetAtPath<SpellData>(Spells + spellName + ".asset");
        if (s == null) { Debug.LogWarning("Spell asset missing: " + spellName); return; }
        s.tapCircle = tap;
        s.holdCircle = hold;
        if (tier0 != null && s.extraTiers != null && s.extraTiers.Length > 0) s.extraTiers[0].circle = tier0;
        EditorUtility.SetDirty(s);
    }
}
