using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Apply Toon Ground
// Switches the floors, walls and props of the stages to the Magic/ToonGround shader (soft, lavender-tinted shadows). Each original material
// gets a toon copy (same texture and colour) in Materials/Toon, so the originals are kept. Running it again only refreshes the copies.
public static class ToonGroundApplier
{
    const string Dir = "Assets/_Assets/Materials/Toon";
    static readonly string[] Scenes = { "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity" };

    [MenuItem("Tools/Magic/Apply Toon Ground")]
    public static void Apply()
    {
        Directory.CreateDirectory(Dir);
        var shader = Shader.Find("Magic/ToonGround");
        int total = 0;

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var player = Object.FindAnyObjectByType<PlayerSpellCaster>();
            Transform playerRoot = player != null ? player.transform.root : null;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                if (playerRoot != null && r.transform.IsChildOf(playerRoot)) continue;
                if (r.GetComponentInParent<CreatureAnimator>() != null || r.GetComponentInParent<Canvas>() != null) continue;

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.shader.name.StartsWith("Universal Render Pipeline/Lit") && !m.shader.name.StartsWith("Universal Render Pipeline/Simple Lit")) continue;
                    if (m.renderQueue >= 2500) continue;                  // leave anything transparent alone
                    mats[i] = ToonCopy(m, shader);
                    changed = true;
                }
                if (changed) { r.sharedMaterials = mats; total++; }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Toon ground applied to " + total + " objects.");
    }

    static Material ToonCopy(Material src, Shader shader)
    {
        string name = (string.IsNullOrEmpty(src.name) ? "Surface" : src.name).Replace("/", "_") + "_Toon";
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        if (src.HasProperty("_BaseMap"))
        {
            m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
            m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
            m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
        }
        if (src.HasProperty("_BaseColor")) m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
        m.SetFloat("_ShadowSoftness", 0.7f);
        m.SetFloat("_ReceiveShadow", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
