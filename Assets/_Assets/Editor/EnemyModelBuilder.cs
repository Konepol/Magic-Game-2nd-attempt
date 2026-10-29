using System.IO;
using UnityEditor;
using UnityEngine;
using MB = WitchBuilder.MeshBuilder;

// Tools > Magic > Build Enemy Models
// Gives every enemy a little toon creature instead of a plain capsule: an imp (Rusher), a tusked bruiser (Charger), a stone golem (Heavy),
// a ghost (Seeker) and a winged eye (Flyer). Each is made of separate pieces with pivots, which CreatureAnimator moves. The old capsule mesh
// is hidden (its collider stays). Running it again rebuilds them.
public static class EnemyModelBuilder
{
    const string ModelDir = "Assets/_Assets/Models/Enemies";
    const string MaterialDir = "Assets/_Assets/Materials";
    const string PrefabDir = "Assets/_Assets/Prefabs/Enemies";

    static Color C(float r, float g, float b) => new Color(r, g, b).linear;

    // ----- shared with the boss -----

    public static Material ToonMaterial(string name, Color rim, Color outline)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Assets/Shaders/ToonCharacter.shader");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetColor("_RimColor", rim);
        mat.SetColor("_OutlineColor", outline);
        mat.SetFloat("_OutlineWidth", 0.02f);
        mat.SetFloat("_SparkleStrength", 0f);
        mat.SetColor("_ShadeColor", new Color(0.62f, 0.6f, 0.82f));
        mat.SetFloat("_AmbientLift", 0.5f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static Material GlowMaterial(string name, Color glow, float strength)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Assets/Shaders/ToonCharacter.shader");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetColor("_EmissionColor", glow);
        mat.SetFloat("_EmissionStrength", strength);
        mat.SetFloat("_RimStrength", 1.2f);
        mat.SetFloat("_SparkleStrength", 0f);
        mat.SetColor("_OutlineColor", glow * 0.35f);
        mat.SetFloat("_OutlineWidth", 0.012f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // a piece of the model: an object with a pivot at "pos" and a mesh built around it in its own space
    public static Transform Part(Transform parent, string name, Vector3 pos, System.Action<MB> build, Material toon, Material glow, string meshName, Vector3? euler = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;

        var mb = new MB(go.transform, null);
        build(mb);
        var mesh = mb.ToStaticMesh(meshName);
        Directory.CreateDirectory(ModelDir);
        AssetDatabase.CreateAsset(mesh, ModelDir + "/" + meshName + ".asset");

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = mesh.subMeshCount > 1 ? new[] { toon, glow } : new[] { toon };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go.transform;
    }

    // a tube along a few points with radii (in the part's local space)
    public static void Limb(MB mb, Color c, float r0, float r1, params Vector3[] pts)
    {
        var radii = new float[pts.Length];
        for (int i = 0; i < pts.Length; i++) radii[i] = Mathf.Lerp(r0, r1, i / (float)(pts.Length - 1));
        mb.TubeLocal(pts, radii, c, 0f, 10);
    }

    // ----- the enemies -----

    [MenuItem("Tools/Magic/Build Enemy Models")]
    public static void Build()
    {
        Directory.CreateDirectory(ModelDir);
        BuildImp();
        BuildBruiser();
        BuildGolem();
        BuildGhost();
        BuildFlyer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy models built.");
    }

    static void Edit(string prefabName, System.Action<GameObject, Transform> build, CreatureAnimator.Kind kind, Vector3 modelScale)
    {
        string path = PrefabDir + "/" + prefabName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);

        var old = root.transform.Find("Model");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        model.localScale = modelScale;
        build(root, model);

        var anim = root.GetComponent<CreatureAnimator>();
        if (anim == null) anim = root.AddComponent<CreatureAnimator>();
        anim.kind = kind;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---- Rusher: a small red imp ----
    static void BuildImp()
    {
        var red = C(0.93f, 0.40f, 0.44f); var dark = C(0.74f, 0.26f, 0.34f); var belly = C(1.00f, 0.74f, 0.70f); var cream = C(1f, 0.94f, 0.82f); var ink = C(0.20f, 0.10f, 0.20f);
        var toon = ToonMaterial("Enemy_Imp", new Color(1f, 0.75f, 0.75f), new Color(0.35f, 0.08f, 0.18f));
        var glow = GlowMaterial("Enemy_ImpGlow", new Color(1f, 0.9f, 0.4f), 1.6f);

        Edit("Rusher", (root, m) =>
        {
            Part(m, "Body", new Vector3(0f, -0.2f, 0f), mb =>
            {
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.45f, 0.5f, 0.4f), red, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -0.05f, 0.28f), new Vector3(0.3f, 0.34f, 0.18f), belly, 0f);
            }, toon, glow, "Imp_Body");
            Part(m, "Head", new Vector3(0f, 0.42f, 0f), mb =>
            {
                mb.EllipsoidLocal(new Vector3(0f, 0.22f, 0f), new Vector3(0.46f, 0.4f, 0.42f), red, 0f);
                mb.EllipsoidLocal(new Vector3(0f, 0.12f, 0.4f), new Vector3(0.12f, 0.045f, 0.03f), ink, 0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Limb(mb, cream, 0.075f, 0.005f, new Vector3(0.22f * s, 0.5f, 0f), new Vector3(0.3f * s, 0.78f, 0.03f), new Vector3(0.27f * s, 1.0f, 0.1f));
                    Limb(mb, dark, 0.07f, 0.005f, new Vector3(0.42f * s, 0.3f, 0f), new Vector3(0.62f * s, 0.4f, 0f), new Vector3(0.78f * s, 0.55f, 0f));
                    mb.EllipsoidLocal(new Vector3(0.16f * s, 0.05f, 0.41f), new Vector3(0.03f, 0.035f, 0.02f), cream, 0f);      // little fangs
                    mb.EllipsoidLocal(new Vector3(0.28f * s, 0.12f, 0.34f), new Vector3(0.09f, 0.05f, 0.03f), belly, 0f);       // cheeks
                    mb.SubMesh(1);
                    mb.EllipsoidLocal(new Vector3(0.17f * s, 0.27f, 0.37f), new Vector3(0.11f, 0.13f, 0.05f), cream, 0f);       // glowing eyes
                    mb.SubMesh(0);
                    mb.EllipsoidLocal(new Vector3(0.17f * s, 0.27f, 0.415f), new Vector3(0.045f, 0.075f, 0.02f), ink, 0f);
                }
            }, toon, glow, "Imp_Head");
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(m, "Arm" + side, new Vector3(0.45f * s, 0.05f, 0f), mb =>
                {
                    Limb(mb, red, 0.09f, 0.075f, Vector3.zero, new Vector3(0.12f * s, -0.22f, 0.04f), new Vector3(0.16f * s, -0.42f, 0.1f));
                    mb.EllipsoidLocal(new Vector3(0.16f * s, -0.46f, 0.11f), new Vector3(0.1f, 0.1f, 0.1f), dark, 0f);
                }, toon, glow, "Imp_Arm" + side);
                Part(m, "Leg" + side, new Vector3(0.2f * s, -0.62f, 0f), mb =>
                {
                    Limb(mb, red, 0.12f, 0.1f, Vector3.zero, new Vector3(0f, -0.2f, 0f), new Vector3(0f, -0.36f, 0f));
                    mb.EllipsoidLocal(new Vector3(0f, -0.4f, 0.09f), new Vector3(0.14f, 0.07f, 0.2f), dark, 0f);
                }, toon, glow, "Imp_Leg" + side);
            }
            Part(m, "Tail", new Vector3(0f, -0.5f, -0.3f), mb =>
            {
                Limb(mb, dark, 0.07f, 0.02f, Vector3.zero, new Vector3(0f, -0.08f, -0.25f), new Vector3(0f, 0.08f, -0.45f), new Vector3(0f, 0.28f, -0.5f));
                mb.StarLocal(new Vector3(0f, 0.32f, -0.5f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f), 0.14f, 0.05f, dark, 0f);
            }, toon, glow, "Imp_Tail");
        }, CreatureAnimator.Kind.Imp, Vector3.one);
    }

    // ---- Charger: an orange tusked bruiser ----
    static void BuildBruiser()
    {
        var body = C(0.99f, 0.64f, 0.34f); var dark = C(0.82f, 0.44f, 0.24f); var snout = C(1f, 0.80f, 0.72f); var cream = C(1f, 0.96f, 0.86f); var ink = C(0.22f, 0.12f, 0.12f);
        var toon = ToonMaterial("Enemy_Bruiser", new Color(1f, 0.85f, 0.7f), new Color(0.38f, 0.16f, 0.06f));
        var glow = GlowMaterial("Enemy_BruiserGlow", new Color(1f, 0.85f, 0.35f), 1.5f);

        Edit("Charger", (root, m) =>
        {
            Part(m, "Body", new Vector3(0f, -0.1f, -0.05f), mb =>
            {
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.58f, 0.52f, 0.72f), body, 0f);
                mb.EllipsoidLocal(new Vector3(0f, 0.34f, -0.1f), new Vector3(0.4f, 0.22f, 0.5f), dark, 0f);     // a darker back
            }, toon, glow, "Bruiser_Body");
            Part(m, "Head", new Vector3(0f, 0.08f, 0.6f), mb =>
            {
                mb.EllipsoidLocal(new Vector3(0f, 0f, 0.15f), new Vector3(0.44f, 0.4f, 0.42f), body, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -0.08f, 0.52f), new Vector3(0.23f, 0.18f, 0.2f), snout, 0f);
                mb.EllipsoidLocal(new Vector3(0.08f, -0.06f, 0.7f), new Vector3(0.035f, 0.045f, 0.02f), ink, 0f);
                mb.EllipsoidLocal(new Vector3(-0.08f, -0.06f, 0.7f), new Vector3(0.035f, 0.045f, 0.02f), ink, 0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Limb(mb, cream, 0.065f, 0.005f, new Vector3(0.18f * s, -0.14f, 0.5f), new Vector3(0.3f * s, -0.02f, 0.66f), new Vector3(0.32f * s, 0.2f, 0.74f));     // tusks
                    mb.EllipsoidLocal(new Vector3(0.34f * s, 0.34f, 0.1f), new Vector3(0.12f, 0.14f, 0.06f), dark, 0f);                                           // ears
                    Limb(mb, cream, 0.06f, 0.005f, new Vector3(0.2f * s, 0.34f, 0.12f), new Vector3(0.3f * s, 0.55f, 0.1f));                                      // little horns
                    mb.SubMesh(1);
                    mb.EllipsoidLocal(new Vector3(0.2f * s, 0.12f, 0.46f), new Vector3(0.09f, 0.075f, 0.04f), cream, 0f);
                    mb.SubMesh(0);
                    mb.EllipsoidLocal(new Vector3(0.2f * s, 0.12f, 0.5f), new Vector3(0.04f, 0.05f, 0.02f), ink, 0f);
                    mb.EllipsoidLocal(new Vector3(0.2f * s, 0.2f, 0.5f), new Vector3(0.1f, 0.025f, 0.02f), ink, 0f);                                              // angry brows
                }
            }, toon, glow, "Bruiser_Head");
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(m, "Leg" + side, new Vector3(0.32f * s, -0.5f, 0f), mb =>
                {
                    foreach (float z in new[] { 0.4f, -0.4f })
                    {
                        Limb(mb, body, 0.12f, 0.1f, new Vector3(0f, 0f, z), new Vector3(0f, -0.25f, z));
                        mb.EllipsoidLocal(new Vector3(0f, -0.3f, z + 0.03f), new Vector3(0.14f, 0.08f, 0.16f), dark, 0f);
                    }
                }, toon, glow, "Bruiser_Leg" + side);
            }
            Part(m, "Tail", new Vector3(0f, 0.15f, -0.7f), mb =>
            {
                Limb(mb, dark, 0.06f, 0.02f, Vector3.zero, new Vector3(0f, 0.1f, -0.2f));
                mb.EllipsoidLocal(new Vector3(0f, 0.14f, -0.26f), new Vector3(0.09f, 0.09f, 0.09f), cream, 0f);
            }, toon, glow, "Bruiser_Tail");
        }, CreatureAnimator.Kind.Bruiser, Vector3.one);
    }

    // ---- Heavy: a purple stone golem with a glowing core ----
    static void BuildGolem()
    {
        var stone = C(0.66f, 0.50f, 0.88f); var dark = C(0.44f, 0.32f, 0.68f); var plate = C(0.82f, 0.70f, 0.98f); var ink = C(0.16f, 0.10f, 0.28f);
        var toon = ToonMaterial("Enemy_Golem", new Color(0.9f, 0.8f, 1f), new Color(0.2f, 0.1f, 0.38f));
        var glow = GlowMaterial("Enemy_GolemGlow", new Color(1f, 0.55f, 0.95f), 1.6f);

        Edit("Heavy", (root, m) =>
        {
            Part(m, "Body", new Vector3(0f, -0.1f, 0f), mb =>
            {
                mb.BoxLocal(Vector3.zero, new Vector3(0.58f, 0.58f, 0.46f), stone, 0f, 3.6f);
                mb.BoxLocal(new Vector3(0f, 0.05f, 0.3f), new Vector3(0.38f, 0.38f, 0.2f), plate, 0f, 3.6f);
                mb.BoxLocal(new Vector3(0f, -0.52f, 0f), new Vector3(0.5f, 0.1f, 0.42f), dark, 0f, 3.2f);
                foreach (float s in new[] { -1f, 1f }) mb.EllipsoidLocal(new Vector3(0.58f * s, 0.45f, 0f), new Vector3(0.22f, 0.2f, 0.24f), dark, 0f);
            }, toon, glow, "Golem_Body");
            Part(m, "Core", new Vector3(0f, -0.05f, 0.5f), mb =>
            {
                mb.SubMesh(1);
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.2f, 0.2f, 0.1f), Color.white, 0f);
            }, toon, glow, "Golem_Core");
            Part(m, "Head", new Vector3(0f, 0.55f, 0.02f), mb =>
            {
                mb.BoxLocal(new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.26f, 0.3f), stone, 0f, 3.6f);
                mb.SubMesh(1);
                mb.EllipsoidLocal(new Vector3(0.12f, 0.26f, 0.29f), new Vector3(0.08f, 0.05f, 0.03f), Color.white, 0f);
                mb.EllipsoidLocal(new Vector3(-0.12f, 0.26f, 0.29f), new Vector3(0.08f, 0.05f, 0.03f), Color.white, 0f);
                mb.SubMesh(0);
                mb.StarLocal(new Vector3(0f, 0.55f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), 0.1f, 0.05f, plate, 0f);
            }, toon, glow, "Golem_Head");
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(m, "Arm" + side, new Vector3(0.72f * s, 0.35f, 0f), mb =>
                {
                    Limb(mb, stone, 0.2f, 0.18f, Vector3.zero, new Vector3(0.05f * s, -0.3f, 0.05f), new Vector3(0.05f * s, -0.58f, 0.08f));
                    mb.BoxLocal(new Vector3(0.05f * s, -0.7f, 0.1f), new Vector3(0.26f, 0.22f, 0.26f), dark, 0f, 3.2f);
                }, toon, glow, "Golem_Arm" + side);
                Part(m, "Leg" + side, new Vector3(0.28f * s, -0.62f, 0f), mb =>
                {
                    Limb(mb, stone, 0.2f, 0.18f, Vector3.zero, new Vector3(0f, -0.2f, 0f), new Vector3(0f, -0.33f, 0f));
                    mb.BoxLocal(new Vector3(0f, -0.4f, 0.06f), new Vector3(0.22f, 0.09f, 0.28f), dark, 0f, 3.2f);
                }, toon, glow, "Golem_Leg" + side);
            }
        }, CreatureAnimator.Kind.Golem, Vector3.one);
    }

    // ---- Seeker: a cyan ghost ----
    static void BuildGhost()
    {
        var body = C(0.60f, 0.92f, 0.96f); var dark = C(0.40f, 0.76f, 0.86f); var blush = C(1f, 0.70f, 0.78f); var ink = C(0.14f, 0.24f, 0.40f);
        var toon = ToonMaterial("Enemy_Ghost", new Color(0.85f, 1f, 1f), new Color(0.1f, 0.3f, 0.45f));
        var glow = GlowMaterial("Enemy_GhostGlow", new Color(0.6f, 1f, 1f), 1.2f);

        Edit("Seeker", (root, m) =>
        {
            Part(m, "Body", new Vector3(0f, 0.1f, 0f), mb =>
            {
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.52f, 0.62f, 0.5f), body, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -0.1f, 0.2f), new Vector3(0.4f, 0.45f, 0.34f), C(0.78f, 0.97f, 1f), 0f);
            }, toon, glow, "Ghost_Body");
            Part(m, "Head", new Vector3(0f, 0.55f, 0f), mb =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    mb.EllipsoidLocal(new Vector3(0.17f * s, -0.12f, 0.43f), new Vector3(0.075f, 0.105f, 0.05f), ink, 0f);
                    mb.EllipsoidLocal(new Vector3(0.17f * s + 0.02f, -0.07f, 0.47f), new Vector3(0.025f, 0.03f, 0.02f), Color.white, 0f);
                    mb.EllipsoidLocal(new Vector3(0.3f * s, -0.24f, 0.38f), new Vector3(0.09f, 0.05f, 0.03f), blush, 0f);
                }
                mb.EllipsoidLocal(new Vector3(0f, -0.26f, 0.45f), new Vector3(0.06f, 0.035f, 0.025f), ink, 0f);
                Limb(mb, dark, 0.06f, 0.015f, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.28f, 0f), new Vector3(-0.04f, 0.46f, 0f));         // a curl on top
                mb.SubMesh(1);
                mb.EllipsoidLocal(new Vector3(-0.04f, 0.5f, 0f), new Vector3(0.07f, 0.07f, 0.07f), Color.white, 0f);                                   // a glowing wisp
            }, toon, glow, "Ghost_Head");
            Part(m, "Tail", new Vector3(0f, -0.45f, 0f), mb =>
            {
                Limb(mb, body, 0.38f, 0.02f, Vector3.zero, new Vector3(0f, -0.2f, -0.08f), new Vector3(0f, -0.4f, -0.2f), new Vector3(0f, -0.58f, -0.1f));
            }, toon, glow, "Ghost_Tail");
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(m, "Arm" + side, new Vector3(0.5f * s, 0.15f, 0.05f), mb =>
                {
                    Limb(mb, dark, 0.09f, 0.07f, Vector3.zero, new Vector3(0.18f * s, -0.08f, 0.06f), new Vector3(0.3f * s, -0.18f, 0.1f));
                    mb.EllipsoidLocal(new Vector3(0.31f * s, -0.19f, 0.1f), new Vector3(0.085f, 0.085f, 0.085f), dark, 0f);
                }, toon, glow, "Ghost_Arm" + side);
            }
        }, CreatureAnimator.Kind.Ghost, Vector3.one);
    }

    // ---- Flyer: a winged eye ----
    static void BuildFlyer()
    {
        var body = C(0.74f, 0.78f, 0.96f); var wing = C(0.52f, 0.48f, 0.84f); var wingTip = C(0.92f, 0.84f, 1f); var ink = C(0.22f, 0.16f, 0.36f); var gold = C(1f, 0.85f, 0.45f);
        var toon = ToonMaterial("Enemy_Flyer", new Color(0.9f, 0.9f, 1f), new Color(0.18f, 0.12f, 0.36f));
        var glow = GlowMaterial("Enemy_FlyerGlow", new Color(1f, 0.4f, 0.5f), 1.8f);

        // the prefab is a box scaled (2, 4, 2): the model cancels that and is about 1.7 m across
        Edit("FlyingEnemy", (root, m) =>
        {
            Part(m, "Body", Vector3.zero, mb =>
            {
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), body, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -0.05f, 0.12f), new Vector3(0.4f, 0.4f, 0.4f), C(0.88f, 0.9f, 1f), 0f);
                Limb(mb, ink, 0.025f, 0.012f, new Vector3(0f, 0.45f, 0f), new Vector3(0.03f, 0.7f, 0f), new Vector3(-0.02f, 0.9f, 0f));       // antenna
                mb.StarLocal(new Vector3(-0.02f, 0.94f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), 0.11f, 0.05f, gold, 0f);
            }, toon, glow, "Flyer_Body");
            Part(m, "Core", new Vector3(0f, 0.02f, 0.42f), mb =>
            {
                mb.SubMesh(1);
                mb.EllipsoidLocal(Vector3.zero, new Vector3(0.22f, 0.22f, 0.1f), Color.white, 0f);
                mb.SubMesh(0);
                mb.EllipsoidLocal(new Vector3(0f, 0f, 0.06f), new Vector3(0.09f, 0.14f, 0.03f), ink, 0f);
            }, toon, glow, "Flyer_Core");
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(m, "Wing" + side, new Vector3(0.42f * s, 0.12f, 0f), mb =>
                {
                    mb.EllipsoidLocal(new Vector3(0.38f * s, 0.05f, 0f), new Vector3(0.45f, 0.06f, 0.24f), wing, 0f);
                    mb.EllipsoidLocal(new Vector3(0.78f * s, 0.12f, -0.02f), new Vector3(0.26f, 0.045f, 0.16f), wingTip, 0f);
                    mb.EllipsoidLocal(new Vector3(0.3f * s, 0.07f, -0.2f), new Vector3(0.3f, 0.05f, 0.14f), wing, 0f);
                }, toon, glow, "Flyer_Wing" + side);
            }
            Part(m, "Tail", new Vector3(0f, -0.15f, -0.45f), mb =>
            {
                Limb(mb, ink, 0.06f, 0.015f, Vector3.zero, new Vector3(0f, -0.12f, -0.2f), new Vector3(0f, -0.1f, -0.4f));
                mb.StarLocal(new Vector3(0f, -0.1f, -0.45f), new Vector3(0f, 0f, -1f), new Vector3(0f, 1f, 0f), 0.12f, 0.05f, gold, 0f);
            }, toon, glow, "Flyer_Tail");
        }, CreatureAnimator.Kind.Flyer, new Vector3(0.75f, 0.375f, 0.75f));
    }
}
