using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Boss Stage
// Makes Stage 2: a copy of the first stage scene with the waves removed, a ruined courtyard arena (broken walls, pillars, rubble) and
// The Warden of Ruins in the middle. Adds the scene to the build settings and a "Stage 2" entry to the game catalog.
// Running it again rebuilds the courtyard and the boss but keeps the rest of the scene (your other edits) as they are.
public static class BossStageBuilder
{
    const string Stage1Path = "Assets/Scenes/Scene1.unity";
    const string Stage2Path = "Assets/Scenes/Scene2.unity";
    const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string MaterialDir = "Assets/_Assets/Materials";

    static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -28f);
    static readonly Vector3 BossStart = new Vector3(0f, 0f, 14f);

    [MenuItem("Tools/Magic/Build Boss Stage")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!File.Exists(Stage2Path)) AssetDatabase.CopyAsset(Stage1Path, Stage2Path);
        var scene = EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);

        // no waves: the boss is the stage
        foreach (var w in Object.FindObjectsByType<WaveSpawner>()) Object.DestroyImmediate(w.gameObject);

        BuildArena();
        MovePlayer();
        PlaceBoss();
        SetUpRunner();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.62f, 0.58f, 0.55f);
        RenderSettings.fogDensity = 0.0045f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AddToBuildSettings();
        AddStageToCatalog();
        Debug.Log("Boss stage built in " + Stage2Path + " and added to the build settings and the game catalog.");
    }

    // ---------- the courtyard ----------

    static Material LitMaterial(string name, Color color)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader != null ? shader : Shader.Find("Standard"));
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildArena()
    {
        var env = GameObject.Find("Environments");
        if (env == null) { Debug.LogError("Build Boss Stage: the scene has no 'Environments' object."); return; }

        var stone = LitMaterial("CourtyardStone", new Color(0.43f, 0.4f, 0.36f));
        var floor = LitMaterial("CourtyardFloor", new Color(0.22f, 0.21f, 0.2f));

        // the floor: 80 x 80, centred on the middle of the courtyard
        var plane = env.transform.Find("Plane");
        if (plane != null)
        {
            plane.position = Vector3.zero;
            plane.localScale = new Vector3(8f, 1f, 8f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = floor;
        }

        var old = env.transform.Find("Courtyard");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var yard = new GameObject("Courtyard").transform;
        yard.SetParent(env.transform, false);

        var rng = new System.Random(11);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // broken walls around the edge: segments of different heights, with gaps where they have fallen
        for (int side = 0; side < 4; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
            for (int i = -3; i <= 3; i++)
            {
                if (rng.NextDouble() < 0.28) continue;                 // a gap
                float height = R(3f, 9f);
                Vector3 pos = turn * new Vector3(i * 12f, height * 0.5f, 38f);
                Block(yard, "Wall", PrimitiveType.Cube, pos, new Vector3(11f, height, 2.6f), side * 90f + R(-3f, 3f), stone);
            }
        }

        // corner towers, each broken off at a different height
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                float height = R(7f, 12f);
                Block(yard, "Tower", PrimitiveType.Cylinder, new Vector3(sx * 36f, height * 0.5f, sz * 36f), new Vector3(7f, height * 0.5f, 7f), 0f, stone);
            }

        // pillars in two rings: tall ones outside, shorter cover closer in
        for (int i = 0; i < 10; i++)
            Pillar(yard, stone, R(5f, 9f), 22f, i * 36f + R(-8f, 8f), rng);
        for (int i = 0; i < 5; i++)
            Pillar(yard, stone, R(3f, 5.5f), 11f, i * 72f + 20f + R(-10f, 10f), rng);

        // rubble
        for (int i = 0; i < 16; i++)
        {
            float a = R(0f, 360f) * Mathf.Deg2Rad, d = R(8f, 33f);
            Vector3 p = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (NearSpawn(p, 7f)) continue;
            float h = R(1f, 2.6f);
            Block(yard, "Rubble", PrimitiveType.Cube, new Vector3(p.x, h * 0.4f, p.z), new Vector3(R(1.5f, 3.8f), h, R(1.5f, 3.8f)), R(0f, 360f), stone);
        }
    }

    static bool NearSpawn(Vector3 p, float distance)
    {
        Vector3 a = p - PlayerStart, b = p - BossStart;
        a.y = b.y = 0f;
        return a.magnitude < distance || b.magnitude < distance + 4f;
    }

    static void Pillar(Transform parent, Material mat, float height, float ring, float angleDeg, System.Random rng)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector3 p = new Vector3(Mathf.Cos(a) * ring, 0f, Mathf.Sin(a) * ring);
        if (NearSpawn(p, 6f)) return;
        Block(parent, "Pillar", PrimitiveType.Cylinder, new Vector3(p.x, height * 0.5f, p.z), new Vector3(2.7f, height * 0.5f, 2.7f), 0f, mat);
    }

    static void Block(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, float yaw, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ---------- the player, the boss, the runner ----------

    static void MovePlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var root = player.transform.root;
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        root.position = new Vector3(PlayerStart.x, root.position.y, PlayerStart.z);
        root.rotation = Quaternion.identity;
        if (cc != null) cc.enabled = true;
    }

    static void PlaceBoss()
    {
        foreach (var b in Object.FindObjectsByType<BossEnemy>()) Object.DestroyImmediate(b.gameObject);

        var go = new GameObject("Boss");
        go.transform.position = BossStart;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);        // facing the player
        go.AddComponent<BossEnemy>();
        Undo.RegisterCreatedObjectUndo(go, "Add Boss");
    }

    static void SetUpRunner()
    {
        var runner = Object.FindAnyObjectByType<StageRunner>();
        if (runner == null)
        {
            var go = new GameObject("StageRunner");
            runner = go.AddComponent<StageRunner>();
        }

        var so = new SerializedObject(runner);
        so.FindProperty("spawner").objectReferenceValue = null;
        so.FindProperty("boss").objectReferenceValue = Object.FindAnyObjectByType<BossEnemy>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- registration ----------

    static void AddToBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(Stage1Path, true),
            new EditorBuildSettingsScene(Stage2Path, true),
        };
    }

    static void AddStageToCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/_Assets/Resources/GameCatalog.asset");
        if (catalog == null) return;

        var list = new System.Collections.Generic.List<StageInfo>(catalog.stages ?? new StageInfo[0]);
        if (!list.Exists(s => s.sceneName == "Scene2"))
            list.Add(new StageInfo { displayName = "Stage 2", description = "Face the Warden of Ruins in a crumbling courtyard.", sceneName = "Scene2" });
        catalog.stages = list.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
