using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Main Menu
// Makes the game catalog (the spells and stages the menu uses), adds a StageRunner to the stage scene, builds the MainMenu scene
// and puts both scenes in the build settings. Running it again updates everything without touching your edits to other assets.
public static class MenuBuilder
{
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";
    const string WeaponDir = "Assets/_Assets/Weapons";
    const string ComboDir = "Assets/_Assets/Weapons/Combos";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string StageScenePath = "Assets/Scenes/Scene1.unity";

    [MenuItem("Tools/Magic/Build Main Menu")]
    public static void BuildAll()
    {
        BuildCatalog();
        AddStageRunner();
        BuildMenuScene();
    }

    // ---------- catalog ----------

    [MenuItem("Tools/Magic/Build Game Catalog")]
    public static void BuildCatalog()
    {
        Directory.CreateDirectory("Assets/_Assets/Resources");

        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<GameCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // every spell except the barrier, which is part of the base kit now
        string[] spellNames = { "ArcaneBolt", "ScatterShot", "FireBurst", "SparkStream", "LightningLance", "MeteorStrike" };
        var spells = new List<SpellData>();
        foreach (var n in spellNames)
        {
            var s = AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/" + n + ".asset");
            if (s != null) spells.Add(s);
        }
        catalog.spells = spells.ToArray();

        var combos = new List<SpellCombo>();
        foreach (var guid in AssetDatabase.FindAssets("t:SpellCombo", new[] { ComboDir }))
        {
            var c = AssetDatabase.LoadAssetAtPath<SpellCombo>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) combos.Add(c);
        }
        catalog.combos = combos.ToArray();

        catalog.slotCount = 4;
        catalog.defaultLoadout = new[] { "ArcaneBolt", "ScatterShot", "FireBurst", "MeteorStrike" };
        catalog.allowMulticast = true;
        catalog.multicastDelay = 0.15f;
        catalog.maxMulticast = 4;

        if (catalog.stages == null || catalog.stages.Length == 0)
            catalog.stages = new[]
            {
                new StageInfo { displayName = "Stage 1", description = "Survive five waves of enemies.", sceneName = "Scene1" },
            };

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Game catalog saved: " + catalog.spells.Length + " spells, " + catalog.combos.Length + " recipes, " + catalog.stages.Length + " stage(s).");
    }

    // ---------- stage runner ----------

    [MenuItem("Tools/Magic/Add Stage Runner To Open Scene")]
    public static void AddStageRunner()
    {
        var existing = Object.FindAnyObjectByType<StageRunner>();
        if (existing != null) return;

        var spawner = Object.FindAnyObjectByType<WaveSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("Add Stage Runner: this scene has no WaveSpawner.");
            return;
        }

        var go = new GameObject("StageRunner");
        var runner = go.AddComponent<StageRunner>();
        var so = new SerializedObject(runner);
        so.FindProperty("spawner").objectReferenceValue = spawner;
        so.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(go, "Add Stage Runner");

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
    }

    // ---------- menu scene ----------

    [MenuItem("Tools/Magic/Build Main Menu Scene")]
    public static void BuildMenuScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var camera = cam.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.06f, 0.11f, 1f);
        camera.orthographic = true;

        new GameObject("MainMenu", typeof(MainMenu));

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        // menu first, then the stage
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(StageScenePath, true),
        };
        if (File.Exists("Assets/Scenes/Scene2.unity")) scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Scene2.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();

        if (!string.IsNullOrEmpty(previous) && previous != MenuScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("Main menu scene built at " + MenuScenePath + " and added to the build settings (menu first).");
    }
}
