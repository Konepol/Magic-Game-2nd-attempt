using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Look presets for ToonShade/Stylized.
//  ToonShade > Presets > Apply > <name>   applies a look to the selected materials (or the materials on the selected objects).
//                                          Colour, texture and glow are left alone, only the look settings change.
//  ToonShade > Presets > Create Sample Materials   makes one ready-to-use material per preset in Assets/Materials/Presets.
// To add or change a preset, edit the entries in Presets below.
public static class ToonPresets
{
    const string ShaderName = "Magic/ToonStylized";

    // every look setting a preset controls, so applying a preset never leaves values of the previous one behind
    static Dictionary<string, float> Base() => new Dictionary<string, float>
    {
        { "_ShadowStyle", 0 }, { "_PaintAmount", 0.35f }, { "_PaintScale", 4 },
        { "_HatchStrength", 0 }, { "_HatchSpacing", 9 }, { "_HatchAngle", 45 }, { "_HatchThickness", 0.5f }, { "_HatchStart", 0.5f }, { "_HatchGap", 0.18f },
        { "_ShadeAmount", 1 }, { "_ReceiveShadow", 1 }, { "_CastSoftness", 0.8f },
        { "_TerminatorPos", 0.5f }, { "_TerminatorSoftness", 0.3f }, { "_CoreShadowPos", 0.22f }, { "_CoreShadowStrength", 0.35f },
        { "_ShadowHueShift", 0.55f }, { "_ShadowSaturation", 1.25f }, { "_ShadowDepth", 0.5f }, { "_BounceStrength", 0.1f },
        { "_SpecStrength", 0.5f }, { "_RimStrength", 0.25f },
        { "_OutlineWidth", 5 }, { "_OutlineVariation", 0.8f }, { "_OutlineThinMin", 0.25f }, { "_OutlineThickMax", 2.2f }, { "_OutlineVarFreq", 2.5f },
        { "_OutlineShiftX", 0 }, { "_OutlineShiftY", 0 }, { "_OutlineShiftFromLight", 0 }, { "_OutlineShiftAmount", 1 }, { "_OutlineOffset", 0 },
        { "_OutlineFromAlbedo", 0.6f }, { "_OutlineFadeStart", 8 }, { "_OutlineFarMin", 0.15f }, { "_OutlineDepthPush", 6 }, { "_OutlineSmooth", 0.6f },
        { "_AmbientLift", 0.3f }, { "_RimPower", 3 }, { "_SpecSize", 0.12f }, { "_SpecSoftness", 0.03f },
    };

    static Dictionary<string, Color> BaseColors() => new Dictionary<string, Color>
    {
        { "_OutlineColor", new Color(0.1f, 0.08f, 0.2f) },
        { "_HatchColor", new Color(0.16f, 0.1f, 0.3f) },
    };

    public static readonly (string name, Action<Dictionary<string, float>, Dictionary<string, Color>, Material> tweak)[] Presets =
    {
        ("Soft Pastel", (v, c, m) => { }),                                                         // the base values: soft gradient shadow, ink outline

        ("Anime Hard Cel", (v, c, m) =>
        {
            v["_ShadowStyle"] = 1; v["_TerminatorPos"] = 0.5f; v["_CoreShadowStrength"] = 0.45f;
            v["_CastSoftness"] = 0.2f; v["_RimStrength"] = 0.3f; v["_OutlineWidth"] = 4; v["_OutlineVariation"] = 0.6f;
        }),

        ("Painterly", (v, c, m) =>
        {
            v["_ShadowStyle"] = 2; v["_PaintAmount"] = 0.4f; v["_PaintScale"] = 4;
            v["_OutlineWidth"] = 4; v["_OutlineVariation"] = 1f; v["_OutlineVarFreq"] = 2.5f;
        }),

        ("Kake-ami Ink", (v, c, m) =>
        {
            v["_ShadowStyle"] = 1; v["_CastSoftness"] = 0.2f; v["_CoreShadowStrength"] = 0.5f;
            v["_HatchStrength"] = 0.85f; v["_HatchSpacing"] = 8; v["_HatchAngle"] = 45; v["_HatchThickness"] = 0.5f; v["_HatchStart"] = 0.55f; v["_HatchGap"] = 0.16f;
            v["_OutlineWidth"] = 5; v["_OutlineVariation"] = 0.9f; v["_OutlineThickMax"] = 2.4f;
            v["_ShadowDepth"] = 0.65f;
        }),

        ("Cel With Light Hatch", (v, c, m) =>
        {
            v["_ShadowStyle"] = 1; v["_CastSoftness"] = 0.2f;
            v["_HatchStrength"] = 0.45f; v["_HatchSpacing"] = 12; v["_HatchAngle"] = 45; v["_HatchStart"] = 0.45f;
            v["_OutlineWidth"] = 4; v["_OutlineVariation"] = 0.7f;
        }),

        ("Painterly With Hatch", (v, c, m) =>
        {
            v["_ShadowStyle"] = 2; v["_PaintAmount"] = 0.35f;
            v["_HatchStrength"] = 0.6f; v["_HatchSpacing"] = 10; v["_HatchAngle"] = 60; v["_HatchStart"] = 0.5f;
            v["_OutlineWidth"] = 4; v["_OutlineVariation"] = 1f;
        }),

        ("Flat Ink (No Shadow)", (v, c, m) =>
        {
            v["_ShadeAmount"] = 0; v["_ReceiveShadow"] = 0; v["_SpecStrength"] = 0.25f; v["_RimStrength"] = 0.15f;
            v["_OutlineWidth"] = 5; v["_OutlineVariation"] = 0.8f;
        }),

        // the witch hat brim + cloth look (hard cel + kake-ami), copied from WitchHat_Brim / WitchHat_Cloth as saved on 2026-11-11
        ("Witch Hat Cel + Kake-ami", (v, c, m) =>
        {
            v["_ShadowStyle"] = 1; v["_PaintAmount"] = 0.368f;
            v["_TerminatorPos"] = 0.6f; v["_TerminatorSoftness"] = 0.005f; v["_CoreShadowPos"] = 0.408f; v["_CoreShadowStrength"] = 0.403f;
            v["_CastSoftness"] = 0; v["_ShadowDepth"] = 0; v["_ShadowHueShift"] = 0.5f; v["_ShadowSaturation"] = 1.118f; v["_BounceStrength"] = 0.124f;
            v["_AmbientLift"] = 0.25f; v["_RimPower"] = 2.5f; v["_RimStrength"] = 0.1f; v["_SpecSize"] = 0.1f; v["_SpecSoftness"] = 0.001f; v["_SpecStrength"] = 0.15f;
            v["_HatchStrength"] = 0.5f; v["_HatchSpacing"] = 15; v["_HatchAngle"] = 30; v["_HatchThickness"] = 0.25f; v["_HatchStart"] = 0.75f; v["_HatchGap"] = 0.1f;
            v["_OutlineWidth"] = 11.09f; v["_OutlineVariation"] = 1; v["_OutlineThinMin"] = 0; v["_OutlineThickMax"] = 1.291f; v["_OutlineVarFreq"] = 2.13f;
            v["_OutlineSmooth"] = 0.27f; v["_OutlineDepthPush"] = 0; v["_OutlineFadeStart"] = 7.3f; v["_OutlineFarMin"] = 0.2f; v["_OutlineFromAlbedo"] = 0;
            v["_OutlineShiftAmount"] = 0;
            c["_OutlineColor"] = new Color(1f, 0.28951165f, 0.16509432f);
            c["_HatchColor"] = new Color(0.16f, 0.1f, 0.3f);
        }),
    };

    public static void Apply(Material m, int index)
    {
        var v = Base();
        var c = BaseColors();
        Presets[index].tweak(v, c, m);
        Undo.RecordObject(m, "Apply toon preset");
        foreach (var kv in v) if (m.HasProperty(kv.Key)) m.SetFloat(kv.Key, kv.Value);
        foreach (var kv in c) if (m.HasProperty(kv.Key)) m.SetColor(kv.Key, kv.Value);
        EditorUtility.SetDirty(m);
    }

    // ---------------- menu ----------------

    static IEnumerable<Material> SelectedToonMaterials()
    {
        var seen = new HashSet<Material>();
        foreach (var o in Selection.objects)
        {
            if (o is Material m) { if (Ok(m) && seen.Add(m)) yield return m; }
            else if (o is GameObject go)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var sm in r.sharedMaterials) if (Ok(sm) && seen.Add(sm)) yield return sm;
        }
    }
    static bool Ok(Material m) => m != null && m.shader != null && m.shader.name == ShaderName;

    static void ApplyToSelection(int index)
    {
        int n = 0;
        foreach (var m in SelectedToonMaterials()) { Apply(m, index); n++; }
        if (n == 0) Debug.LogWarning("ToonShade preset: select a material (or an object) that uses " + ShaderName + ".");
        else AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/1 Soft Pastel")] static void A0() => ApplyToSelection(0);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/2 Anime Hard Cel")] static void A1() => ApplyToSelection(1);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/3 Painterly")] static void A2() => ApplyToSelection(2);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/4 Kake-ami Ink")] static void A3() => ApplyToSelection(3);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/5 Cel With Light Hatch")] static void A4() => ApplyToSelection(4);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/6 Painterly With Hatch")] static void A5() => ApplyToSelection(5);
    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/7 Flat Ink (No Shadow)")] static void A6() => ApplyToSelection(6);

    [MenuItem("Tools/Magic/Stylized Toon/Presets/Apply/8 Witch Hat Cel + Kake-ami")] static void A7() => ApplyToSelection(7);

    [MenuItem("Tools/Magic/Stylized Toon/Presets/Create Sample Materials")]
    static void CreateSamples()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError(ShaderName + " not found."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Presets")) AssetDatabase.CreateFolder("Assets/Materials", "Presets");

        for (int i = 0; i < Presets.Length; i++)
        {
            string path = "Assets/Materials/Presets/Toon_" + Presets[i].name.Replace(" ", "").Replace("(", "").Replace(")", "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); m.SetColor("_BaseColor", new Color(0.62f, 0.45f, 0.9f)); AssetDatabase.CreateAsset(m, path); }
            Apply(m, i);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("ToonShade: sample preset materials are in Assets/Materials/Presets.");
    }
}
