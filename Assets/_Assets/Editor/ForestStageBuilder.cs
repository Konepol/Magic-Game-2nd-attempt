using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Forest Stage
// Makes the forest stage: a copy of the first stage scene without the waves and the old floor, with a Forest Map (the world, made
// when the scene starts) and Map Bounds (the edge of the map and what happens when you leave it). It also makes the forest's
// materials, sets the sky, light and fog, and adds the scene to the build settings and the game catalog.
// Running it again keeps the scene, the Forest Map and Map Bounds settings and the materials as you left them; it only puts back
// what is missing.
public static class ForestStageBuilder
{
    const string Stage1Path = "Assets/Scenes/Scene1.unity";
    const string ForestPath = "Assets/Scenes/Forest.unity";
    const string SceneName = "Forest";
    const string MaterialDir = "Assets/_Assets/Materials/Forest";
    const string ToonShader = "Magic/ToonStylized";

    [MenuItem("Tools/Magic/Build Forest Stage")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (Shader.Find(ToonShader) == null) { Debug.LogError("Build Forest Stage: the shader " + ToonShader + " is missing."); return; }

        bool fresh = !File.Exists(ForestPath);
        if (fresh) AssetDatabase.CopyAsset(Stage1Path, ForestPath);
        var scene = EditorSceneManager.OpenScene(ForestPath, OpenSceneMode.Single);

        ClearOldStage();
        var map = SetUpMap();
        SetUpBounds(map);
        SetUpRunner();
        if (fresh)
        {
            PlacePlayer(map);
            SetUpView();
            SetUpSkyAndLight();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        AddStageToCatalog();
        Debug.Log("Forest stage built in " + ForestPath + " and added to the build settings and the game catalog.");
    }

    // ---------- what the copied stage no longer needs ----------

    static void ClearOldStage()
    {
        foreach (var w in Object.FindObjectsByType<WaveSpawner>(FindObjectsInactive.Include)) Object.DestroyImmediate(w.gameObject);
        foreach (var b in Object.FindObjectsByType<BossEnemy>(FindObjectsInactive.Include)) Object.DestroyImmediate(b.gameObject);

        var env = GameObject.Find("Environments");
        if (env == null) return;

        // the old floor, and the navigation mesh that was baked for the enemies when the stage started
        var plane = env.transform.Find("Plane");
        if (plane != null) Object.DestroyImmediate(plane.gameObject);
        foreach (string type in new[] { "NavMeshAutoBuild", "NavMeshSurface" })
            foreach (var c in env.GetComponents<Component>())
                if (c != null && c.GetType().Name == type) Object.DestroyImmediate(c);
    }

    // ---------- materials ----------

    // made once; after that they are yours to tune (Tools > Magic > Stylized Toon > Presets also works on them)
    static Material ToonMaterial(string name, System.Action<Material> setup)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        Directory.CreateDirectory(MaterialDir);
        m = new Material(Shader.Find(ToonShader));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_UseVertexColor", 1f);          // every forest colour is painted into the meshes
        m.SetFloat("_OutlineSmooth", 0f);           // the meshes hold many things each, so there is no single centre to smooth from
        m.SetFloat("_SpecStrength", 0f);
        m.SetColor("_ShadowTint", new Color(0.60f, 0.66f, 0.94f));
        m.SetFloat("_ShadowHueShift", 0.6f);
        m.SetFloat("_AmbientLift", 0.35f);
        setup(m);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Ground() => ToonMaterial("ForestGround", m =>
    {
        m.SetFloat("_ShadowStyle", 0f);
        m.SetFloat("_ShadeAmount", 0.4f);           // hills shade gently; the cast shadows of the trees do the rest
        m.SetFloat("_TerminatorSoftness", 0.35f);
        m.SetFloat("_ShadowDepth", 0.5f);
        m.SetFloat("_CoreShadowStrength", 0.15f);
        m.SetFloat("_CastSoftness", 0.9f);
        m.SetFloat("_RimStrength", 0f);
        m.SetFloat("_OutlineWidth", 0f);
        m.SetFloat("_OutlineGroup", 3f);            // its own group, so the trees get their ink line against the grass
    });

    static Material Foliage() => ToonMaterial("ForestFoliage", m =>
    {
        m.SetFloat("_ShadowStyle", 2f);             // painterly edge: brushy shadow shapes on the leaves
        m.SetFloat("_PaintAmount", 0.3f);
        m.SetFloat("_PaintScale", 0.5f);            // brush size for things metres across
        m.SetFloat("_TerminatorPos", 0.5f);
        m.SetFloat("_ShadowDepth", 0.55f);
        m.SetFloat("_CoreShadowStrength", 0.3f);
        m.SetColor("_BounceColor", new Color(0.85f, 0.95f, 0.6f));
        m.SetFloat("_BounceStrength", 0.18f);
        m.SetColor("_RimColor", new Color(1f, 0.96f, 0.8f));
        m.SetFloat("_RimStrength", 0.2f);
        m.SetFloat("_CastSoftness", 0.6f);
        m.SetColor("_OutlineColor", new Color(0.2f, 0.16f, 0.36f));
        m.SetFloat("_OutlineFromAlbedo", 0.85f);
        m.SetFloat("_OutlineWidth", 2.5f);
        m.SetFloat("_OutlineVariation", 0.5f);
        m.SetFloat("_OutlineVarFreq", 0.4f);
        m.SetFloat("_OutlineFadeStart", 70f);
        m.SetFloat("_OutlineFarMin", 0.3f);
        m.SetFloat("_OutlineDepthPush", 2f);
        m.SetFloat("_OutlineGroup", 2f);
    });

    static Material Glow() => ToonMaterial("ForestGlow", m =>
    {
        m.SetFloat("_ShadowStyle", 0f);
        m.SetFloat("_ShadeAmount", 0.25f);
        m.SetFloat("_ReceiveShadow", 0.3f);
        m.SetColor("_RimColor", Color.white);
        m.SetFloat("_RimStrength", 0.7f);
        m.SetColor("_EmissionColor", new Color(0.3f, 0.34f, 0.36f));
        m.SetFloat("_EmissionStrength", 1f);
        m.SetFloat("_OutlineWidth", 0f);
        m.SetFloat("_OutlineGroup", 2f);
    });

    static Material Water() => ToonMaterial("ForestWater", m =>
    {
        m.SetFloat("_ShadowStyle", 0f);
        m.SetFloat("_ShadeAmount", 0f);
        m.SetFloat("_ReceiveShadow", 0.6f);
        m.SetFloat("_SpecStrength", 0.85f);
        m.SetFloat("_SpecSize", 0.3f);
        m.SetFloat("_SpecSoftness", 0.02f);
        m.SetColor("_RimColor", Color.white);
        m.SetFloat("_RimPower", 4f);
        m.SetFloat("_RimStrength", 0.9f);           // whitens toward the far shore, like light on water
        m.SetFloat("_OutlineWidth", 0f);
        m.SetFloat("_OutlineGroup", 3f);
    });

    static Material Simple(string name, string shaderName, System.Action<Material> setup)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var shader = Shader.Find(shaderName);
        if (shader == null) { Debug.LogWarning("Build Forest Stage: shader " + shaderName + " not found, " + name + " was not made."); return null; }
        Directory.CreateDirectory(MaterialDir);
        m = new Material(shader);
        setup(m);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Veil() => Simple("ForestEdgeVeil", "Magic/BoundaryVeil", m => { });

    static Material Motes() => Simple("ForestMotes", "Magic/ParticleGlow", m =>
    {
        m.SetTexture("_BaseMap", SoftDot());
        m.SetFloat("_DstBlend", 1f);
    });

    static Material Sky() => Simple("ForestSky", "Skybox/Procedural", m =>
    {
        m.SetFloat("_SunSize", 0.05f);
        m.SetFloat("_SunSizeConvergence", 4f);
        m.SetFloat("_AtmosphereThickness", 0.72f);
        m.SetColor("_SkyTint", new Color(0.62f, 0.62f, 0.9f));
        m.SetColor("_GroundColor", new Color(0.78f, 0.86f, 0.92f));
        m.SetFloat("_Exposure", 1.25f);
    });

    // a small round soft dot for the drifting motes
    static Texture2D SoftDot()
    {
        string path = MaterialDir + "/MoteDot.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        Directory.CreateDirectory(MaterialDir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- the map and its edge ----------

    static void SetIfEmpty(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
    }

    static ForestMap SetUpMap()
    {
        var map = Object.FindAnyObjectByType<ForestMap>();
        if (map == null) map = new GameObject("ForestMap").AddComponent<ForestMap>();

        var so = new SerializedObject(map);
        SetIfEmpty(so, "groundMaterial", Ground());
        SetIfEmpty(so, "foliageMaterial", Foliage());
        SetIfEmpty(so, "glowMaterial", Glow());
        SetIfEmpty(so, "waterMaterial", Water());
        SetIfEmpty(so, "motesMaterial", Motes());
        so.ApplyModifiedPropertiesWithoutUndo();

        // the Scene view preview was made the moment the component appeared, before it had its materials: make it again
        map.enabled = false;
        map.enabled = true;
        return map;
    }

    static void SetUpBounds(ForestMap map)
    {
        var bounds = Object.FindAnyObjectByType<MapBounds>();
        if (bounds == null)
        {
            bounds = new GameObject("MapBounds").AddComponent<MapBounds>();
            bounds.transform.position = map.transform.position;
        }
        bounds.radius = map.playRadius;             // the mountains and the trees are laid out around this size

        var so = new SerializedObject(bounds);
        so.Update();
        SetIfEmpty(so, "veilMaterial", Veil());
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bounds);
    }

    static void SetUpRunner()
    {
        var runner = Object.FindAnyObjectByType<StageRunner>();
        if (runner == null) runner = new GameObject("StageRunner").AddComponent<StageRunner>();
        var so = new SerializedObject(runner);
        so.FindProperty("spawner").objectReferenceValue = null;
        so.FindProperty("boss").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- first build only: where she starts, how far she sees, the sky ----------

    static void PlacePlayer(ForestMap map)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var root = player.transform.root;
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        Vector3 p = map.transform.position;
        root.position = new Vector3(p.x, map.HeightAt(p) + 0.3f, p.z);
        root.rotation = Quaternion.identity;
        if (cc != null) cc.enabled = true;
    }

    static void SetUpView()
    {
        // the mountains at the far side of the map must still be drawn
        foreach (var vc in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(vc);
            var far = so.FindProperty("Lens.FarClipPlane");
            if (far == null) continue;
            far.floatValue = 3000f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            cam.farClipPlane = 3000f;
            EditorUtility.SetDirty(cam);
        }
    }

    static void SetUpSkyAndLight()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Directional) continue;
            light.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
            light.color = new Color(1f, 0.95f, 0.86f);
            EditorUtility.SetDirty(light);
        }

        var sky = Sky();
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.66f, 0.76f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.72f, 0.80f, 0.82f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.56f, 0.50f);

        // haze: the far woods and the mountains fade toward the colour of the horizon
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.97f);
        RenderSettings.fogDensity = 0.00095f;
    }

    // ---------- registration ----------

    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ForestPath)) return;
        scenes.Add(new EditorBuildSettingsScene(ForestPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void AddStageToCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/_Assets/Resources/GameCatalog.asset");
        if (catalog == null) return;

        var list = new List<StageInfo>(catalog.stages ?? new StageInfo[0]);
        if (!list.Exists(s => s.sceneName == SceneName))
            list.Add(new StageInfo { displayName = "Whispering Forest", description = "A wide forest to fly through at full speed. No enemies yet.", sceneName = SceneName });
        catalog.stages = list.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
