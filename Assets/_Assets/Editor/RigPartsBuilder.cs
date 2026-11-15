using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Rig Parts
// Turns the parts of the witch that used to be made in code while playing into real objects you can select and edit:
//   - BroomTail (ribbon) and StarDust (particles) under the broom model, on the BroomFX component
//   - SpeedLines with its Stars child, under a "Systems" object
//   - the health bar prefab used by barriers (and enemies): Prefabs/UI/EnemyHealthBar
// Their materials and the star mesh are saved as assets (Materials/Rig_*, Models/Rig_Star): existing ones are kept, so changes you
// made to them survive. Running the tool again rebuilds the objects, which resets edits made to them: to rebuild only one part use
// the "Rebuild Parts" entry in the component's menu (the three dots in the Inspector).
// The casting circle is not part of this.
public static class RigPartsBuilder
{
    const string MaterialDir = "Assets/_Assets/Materials";
    const string ImageDir = "Assets/_Assets/Images/FX";
    const string ModelDir = "Assets/_Assets/Models";
    const string BarPrefab = "Assets/_Assets/Prefabs/UI/EnemyHealthBar.prefab";
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";

    [MenuItem("Tools/Magic/Build Rig Parts")]
    public static void Build()
    {
        foreach (var d in new[] { MaterialDir, ImageDir, ModelDir }) Directory.CreateDirectory(d);

        // ---------- assets (kept if they already exist) ----------
        var dot = LoadOrMake<Texture2D>(ImageDir + "/Rig_SoftDot.png", path =>
        {
            File.WriteAllBytes(path, MagicFX.SoftDot(32, 1.5f).EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        });
        var glow = LoadOrMake<Material>(MaterialDir + "/Rig_Glow.mat", p => Save(MagicFX.Unlit(true, dot), p));
        var starMat = LoadOrMake<Material>(MaterialDir + "/Rig_Star.mat", p => Save(MagicFX.Unlit(false), p));
        var star = LoadOrMake<Mesh>(ModelDir + "/Rig_Star.asset", p => Save(Object.Instantiate(MagicFX.StarMesh()), p));
        var barBg = LoadOrMake<Material>(MaterialDir + "/Rig_Bar_BG.mat", p => Save(SpellVisuals.Make(HealthBarStyle.Background, false), p));
        var barChip = LoadOrMake<Material>(MaterialDir + "/Rig_Bar_Chip.mat", p => Save(SpellVisuals.Make(HealthBarStyle.Chip, false), p));
        var barFill = LoadOrMake<Material>(MaterialDir + "/Rig_Bar_Fill.mat", p => Save(SpellVisuals.Make(HealthBarStyle.Full, false), p));

        // ---------- the health bar prefab ----------
        GameObject barAsset = null;
        if (File.Exists(BarPrefab))
        {
            using (var edit = new PrefabUtility.EditPrefabContentsScope(BarPrefab))
            {
                var bar = edit.prefabContentsRoot.GetComponentInChildren<WorldHealthBar>(true);
                if (bar != null)
                {
                    var so = new SerializedObject(bar);
                    so.FindProperty("bgMaterial").objectReferenceValue = barBg;
                    so.FindProperty("chipMaterial").objectReferenceValue = barChip;
                    so.FindProperty("fillMaterial").objectReferenceValue = barFill;
                    so.ApplyModifiedProperties();
                    bar.BuildParts();
                }
            }
            barAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BarPrefab);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog != null && barAsset != null) { catalog.healthBarPrefab = barAsset; EditorUtility.SetDirty(catalog); }
        AssetDatabase.SaveAssets();

        // ---------- the scenes that have a witch ----------
        string returnTo = EditorSceneManager.GetActiveScene().path;
        int scenesDone = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !File.Exists(entry.path)) continue;
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            var broom = Object.FindAnyObjectByType<BroomFlight>(FindObjectsInactive.Include);
            if (broom == null) continue;

            var fx = broom.GetComponent<BroomFX>();
            if (fx == null) fx = Undo.AddComponent<BroomFX>(broom.gameObject);
            fx.BuildInScene(glow, starMat, star, broom);

            var lines = Object.FindAnyObjectByType<SpeedLines>(FindObjectsInactive.Include);
            if (lines == null)
            {
                var systems = GameObject.Find("/Systems");
                if (systems == null) systems = new GameObject("Systems");
                var go = new GameObject("SpeedLines");
                go.transform.SetParent(systems.transform, false);
                lines = go.AddComponent<SpeedLines>();
            }
            lines.BuildInScene(glow, starMat, star);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            scenesDone++;
        }
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

        Debug.Log("Rig parts built in " + scenesDone + " scene(s); health bar prefab and materials saved in " + MaterialDir);
    }

    static T LoadOrMake<T>(string path, System.Func<string, T> make) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        return existing != null ? existing : make(path);
    }

    static T Save<T>(T asset, string path) where T : Object
    {
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
