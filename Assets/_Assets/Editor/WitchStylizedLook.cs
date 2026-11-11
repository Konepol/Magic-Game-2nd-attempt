using UnityEditor;
using UnityEngine;

// Tools > Magic > Stylized Toon > Apply Stylized Look To Witch
// Puts the witch body, wand and wand gem on the Magic/ToonStylized shader with the "Witch Hat Cel + Kake-ami" preset (hard cel shadows,
// kake-ami hatching, variable ink outline), keeping what the character needs: vertex colours, the rest-pose position for patterns that stay
// on the cloth, the sparkles on the robe and the wand gem glow. WitchBuilder calls this too, so rebuilding the witch keeps the look.
public static class WitchStylizedLook
{
    const string ShaderPath = "Assets/_Assets/Shaders/ToonStylized.shader";
    const string MaterialDir = "Assets/_Assets/Materials";

    [MenuItem("Tools/Magic/Stylized Toon/Apply Stylized Look To Witch")]
    public static void ApplyFromMenu()
    {
        var body = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Witch_Body.mat");
        var wand = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Witch_Wand.mat");
        var gem = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Witch_WandGem.mat");
        if (body == null || wand == null || gem == null) { Debug.LogWarning("Witch materials not found: run Tools > Magic > Build Witch Character first."); return; }
        Apply(body, wand, gem);
        Debug.Log("Witch is now on Magic/ToonStylized.");
    }

    public static void Apply(Material body, Material wand, Material gem)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) { Debug.LogError("Missing " + ShaderPath); return; }

        foreach (var m in new[] { body, wand, gem })
        {
            m.shader = shader;
            ToonPresets.Apply(m, 7);                       // Witch Hat Cel + Kake-ami
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_UseVertexColor", 1);              // the witch is coloured with vertex colours
            m.SetFloat("_UseRestPosition", 1);             // patterns and sparkles use the rest pose stored in UV0, so they don't swim when animated
            m.SetFloat("_OutlineSmooth", 0);               // the mesh already has smooth normals; blending with the position direction would bend limbs
            m.SetFloat("_SparkleStrength", 0);
            m.SetFloat("_EmissionStrength", 0);
            m.SetColor("_EmissionColor", Color.black);
        }

        body.SetFloat("_SparkleStrength", 1);              // stars on the robe
        body.SetFloat("_OutlineGroup", 1);

        // the wand is held in front of the body: its own outline group, so its line is drawn over the body instead of hidden by it
        wand.SetFloat("_OutlineGroup", 2);
        wand.SetFloat("_OutlineWidth", 4);

        gem.SetFloat("_OutlineGroup", 2);
        gem.SetFloat("_OutlineWidth", 3);
        gem.SetFloat("_HatchStrength", 0);
        gem.SetColor("_EmissionColor", new Color(0.75f, 0.9f, 1f));
        gem.SetFloat("_EmissionStrength", 0.9f);           // WandGlow animates this per frame
        gem.SetFloat("_RimStrength", 1.4f);

        foreach (var m in new[] { body, wand, gem }) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
    }
}
