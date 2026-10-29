using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Witch Character
// Builds the chibi witch (big head, pointed hat with a star and a crescent moon, short robe, boots) as one skinned mesh that is bound to the
// existing skeleton of the player, so every animation keeps working, and a wand held in the right hand. The look comes from the
// "Magic/ToonCharacter" shader (soft pastel bands, glass rim, sparkles, outline). Running it again rebuilds both.
public static class WitchBuilder
{
    const string ModelDir = "Assets/_Assets/Models/Witch";
    const string MaterialDir = "Assets/_Assets/Materials";
    static readonly string[] Scenes = { "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity" };

    // colours as they should look on screen (the shader reads vertex colours as linear values)
    static Color C(float r, float g, float b) => new Color(r, g, b).linear;
    static readonly Color Robe = C(0.72f, 0.82f, 1.00f), RobeDark = C(0.50f, 0.62f, 0.92f), Hat = C(0.40f, 0.48f, 0.88f), Gold = C(1.00f, 0.82f, 0.42f);
    static readonly Color Skin = C(1.00f, 0.88f, 0.80f), Hair = C(0.84f, 0.88f, 1.00f), Eye = C(0.14f, 0.18f, 0.42f), Blush = C(1.00f, 0.62f, 0.72f);
    static readonly Color Boot = C(0.46f, 0.55f, 0.90f), Stocking = C(0.94f, 0.96f, 1.00f), White = C(1f, 1f, 1f), WandWood = C(0.80f, 0.70f, 0.95f);

    [MenuItem("Tools/Magic/Build Witch Character")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(ModelDir);
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var caster = Object.FindAnyObjectByType<PlayerSpellCaster>();
            if (caster == null) { Debug.LogWarning("Witch: no player in " + path); continue; }
            Build(caster.transform.root, Path.GetFileNameWithoutExtension(path));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Witch built in " + Scenes.Length + " scenes.");
    }

    // ------------------------------------------------------------------------------------------------------------------

    static void Build(Transform root, string sceneName)
    {
        var animator = root.GetComponent<Animator>();
        var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (animator == null || skin == null) { Debug.LogWarning("Witch: player has no animator or skinned mesh."); return; }

        Vector3 fwd = root.forward, up = root.up, right = root.right;
        Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
        Vector3 P(HumanBodyBones b) => B(b).position;

        var toon = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Assets/Shaders/ToonCharacter.shader");
        var bodyMat = Material(MaterialDir + "/Witch_Body.mat", toon, mat => { });
        var wandMat = Material(MaterialDir + "/Witch_Wand.mat", toon, mat => { mat.SetColor("_RimColor", new Color(0.9f, 0.95f, 1f)); });
        bodyMat.SetColor("_ShadeColor", new Color(0.66f, 0.72f, 0.96f));
        bodyMat.SetFloat("_AmbientLift", 0.5f);
        var gemMat = Material(MaterialDir + "/Witch_WandGem.mat", toon, mat =>
        {
            mat.SetColor("_EmissionColor", new Color(0.75f, 0.9f, 1f));
            mat.SetFloat("_EmissionStrength", 0.9f);
            mat.SetFloat("_RimStrength", 1.4f);
            mat.SetFloat("_SparkleStrength", 0f);
        });

        // ---------------- the body ----------------
        var mb = new MeshBuilder(skin.transform, new List<Transform>(skin.bones));

        Vector3 head = P(HumanBodyBones.Neck) + up * 0.20f;
        var headBone = B(HumanBodyBones.Head);

        // head and face
        mb.Ellipsoid(head, new Vector3(0.255f, 0.235f, 0.245f), Skin, headBone, 0f);
        mb.Ellipsoid(head + fwd * 0.03f - up * 0.03f, new Vector3(0.2f, 0.19f, 0.2f), Skin, headBone, 0f);          // rounder cheeks
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 eye = head + fwd * 0.215f + right * (0.088f * s) - up * 0.02f;
            mb.Ellipsoid(eye, new Vector3(0.036f, 0.056f, 0.022f), Eye, headBone, 0f);
            mb.Ellipsoid(eye + fwd * 0.012f + right * (0.01f * s) + up * 0.02f, new Vector3(0.014f, 0.016f, 0.01f), White, headBone, 0f);   // shine
            mb.Ellipsoid(head + fwd * 0.2f + right * (0.15f * s) - up * 0.075f, new Vector3(0.04f, 0.024f, 0.016f), Blush, headBone, 0f);
        }
        mb.Ellipsoid(head + fwd * 0.236f - up * 0.085f, new Vector3(0.018f, 0.009f, 0.008f), Eye, headBone, 0f);          // mouth

        // hair: a cap, bangs and long hair at the back
        mb.Ellipsoid(head + up * 0.045f - fwd * 0.03f, new Vector3(0.275f, 0.235f, 0.27f), Hair, headBone, 0f);
        for (int i = -2; i <= 2; i++)
            mb.Ellipsoid(head + fwd * 0.2f + right * (0.075f * i) + up * (0.15f - Mathf.Abs(i) * 0.012f), new Vector3(0.062f, 0.075f, 0.05f), Hair, headBone, 0f);
        mb.Ellipsoid(head - fwd * 0.12f - up * 0.17f, new Vector3(0.21f, 0.3f, 0.13f), Hair, headBone, 0f);
        foreach (float s in new[] { -1f, 1f })
            mb.Ellipsoid(head - fwd * 0.05f + right * (0.235f * s) - up * 0.06f, new Vector3(0.05f, 0.14f, 0.07f), Hair, headBone, 0f);   // side locks

        // hat: wide brim, a bent cone, a gold band, a star and a crescent moon
        Vector3 hatBase = head + up * 0.18f;
        mb.Ellipsoid(hatBase, new Vector3(0.43f, 0.035f, 0.43f), Hat, headBone, 1f);
        mb.Tube(new[] { hatBase + up * 0.01f, hatBase + up * 0.2f - fwd * 0.01f, hatBase + up * 0.38f - fwd * 0.07f, hatBase + up * 0.5f - fwd * 0.17f, hatBase + up * 0.52f - fwd * 0.3f },
                new[] { 0.235f, 0.18f, 0.115f, 0.06f, 0.0f }, Hat, headBone, 1f, 18);
        mb.Ellipsoid(hatBase + up * 0.045f, new Vector3(0.24f, 0.03f, 0.24f), Gold, headBone, 0f);                          // band
        mb.Star(hatBase + up * 0.115f + fwd * 0.215f, fwd, up, 0.072f, 0.032f, Gold, headBone);
        mb.Crescent(hatBase + up * 0.2f + right * 0.1f + fwd * 0.12f, right * 0.5f + fwd * 0.87f, up, 0.065f, Gold, headBone);

        // body: bodice, belt, short robe with a hem, collar
        var chest = B(HumanBodyBones.Chest);
        Vector3 torso = Vector3.Lerp(P(HumanBodyBones.Spine), P(HumanBodyBones.Chest), 0.7f);
        mb.Ellipsoid(torso + up * 0.03f, new Vector3(0.2f, 0.25f, 0.155f), Robe, chest, 1f);
        Vector3 waist = P(HumanBodyBones.Spine) + up * 0.0f;
        mb.Ellipsoid(waist - up * 0.06f, new Vector3(0.215f, 0.03f, 0.17f), Gold, B(HumanBodyBones.Spine), 0f);             // belt
        Vector3 hips = P(HumanBodyBones.Hips);
        mb.Tube(new[] { hips + up * 0.1f, hips - up * 0.05f, hips - up * 0.2f, hips - up * 0.33f }, new[] { 0.2f, 0.25f, 0.3f, 0.34f }, Robe, B(HumanBodyBones.Hips), 1f, 20);
        mb.Ellipsoid(hips - up * 0.335f, new Vector3(0.345f, 0.028f, 0.345f), RobeDark, B(HumanBodyBones.Hips), 1f);          // hem
        mb.Ellipsoid(P(HumanBodyBones.Neck) - up * 0.01f, new Vector3(0.14f, 0.05f, 0.12f), RobeDark, chest, 1f);             // collar
        mb.Star(P(HumanBodyBones.Neck) - up * 0.06f + fwd * 0.115f, fwd, up, 0.05f, 0.022f, Gold, chest);                     // brooch

        // arms and legs
        foreach (bool left in new[] { true, false })
        {
            var upperArm = B(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lowerArm = B(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = B(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            mb.Ellipsoid(upperArm.position, new Vector3(0.075f, 0.075f, 0.075f), Robe, upperArm, 1f);
            mb.Tube(new[] { upperArm.position, lowerArm.position }, new[] { 0.068f, 0.07f }, Robe, upperArm, 1f, 12);
            mb.Ellipsoid(lowerArm.position, new Vector3(0.072f, 0.072f, 0.072f), Robe, lowerArm, 1f);
            mb.Tube(new[] { lowerArm.position, hand.position }, new[] { 0.072f, 0.095f }, Robe, lowerArm, 1f, 12);        // wide sleeve
            mb.Ellipsoid(Vector3.Lerp(lowerArm.position, hand.position, 0.97f), new Vector3(0.1f, 0.1f, 0.1f), RobeDark, lowerArm, 1f);   // cuff
            mb.Ellipsoid(hand.position + (hand.position - lowerArm.position).normalized * 0.04f, new Vector3(0.062f, 0.062f, 0.062f), Skin, hand, 0f);

            var upperLeg = B(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var lowerLeg = B(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = B(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            mb.Ellipsoid(upperLeg.position, new Vector3(0.085f, 0.085f, 0.085f), Stocking, upperLeg, 0f);
            mb.Tube(new[] { upperLeg.position, lowerLeg.position }, new[] { 0.082f, 0.068f }, Stocking, upperLeg, 0f, 12);
            mb.Ellipsoid(lowerLeg.position, new Vector3(0.07f, 0.07f, 0.07f), Stocking, lowerLeg, 0f);
            Vector3 ankle = foot.position + up * 0.05f;
            mb.Tube(new[] { lowerLeg.position, ankle }, new[] { 0.068f, 0.06f }, Stocking, lowerLeg, 0f, 12);
            mb.Tube(new[] { Vector3.Lerp(lowerLeg.position, ankle, 0.4f), ankle }, new[] { 0.085f, 0.08f }, Boot, lowerLeg, 0f, 12);                 // boot shaft
            mb.Ellipsoid(ankle, new Vector3(0.088f, 0.02f, 0.088f), Gold, lowerLeg, 0f);                                                                // gold trim
            mb.Ellipsoid(new Vector3(foot.position.x, root.position.y + 0.075f, foot.position.z) + fwd * 0.07f, new Vector3(0.085f, 0.075f, 0.16f), Boot, foot, 0f);
        }

        var body = mb.ToMesh("WitchBody_" + sceneName, skin);
        AssetDatabase.CreateAsset(body, ModelDir + "/WitchBody_" + sceneName + ".asset");
        skin.sharedMesh = body;
        skin.sharedMaterials = new[] { bodyMat };
        skin.updateWhenOffscreen = true;

        // ---------------- the wand ----------------
        // it is placed while the arm holds the ready pose, so the wand points forward and up in the hand when she holds it in front of her
        var hand2 = B(HumanBodyBones.RightHand);
        var old = hand2.Find("Wand");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var all = root.GetComponentsInChildren<Transform>(true);
        var savedPos = new Vector3[all.Length];
        var savedRot = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) { savedPos[i] = all[i].localPosition; savedRot[i] = all[i].localRotation; }

        var handler = new HumanPoseHandler(animator.avatar, root);
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);
        foreach (var kv in WitchPoses.Hold) pose.muscles[kv.Key] = kv.Value;
        handler.SetHumanPose(ref pose);
        handler.Dispose();

        var wand = new GameObject("Wand");
        wand.transform.position = hand2.position + (hand2.position - B(HumanBodyBones.RightLowerArm).position).normalized * 0.04f;
        wand.transform.rotation = Quaternion.LookRotation((fwd + up * 0.5f).normalized, up);
        wand.transform.SetParent(hand2, true);

        for (int i = 0; i < all.Length; i++)
            if (all[i] != wand.transform) { all[i].localPosition = savedPos[i]; all[i].localRotation = savedRot[i]; }

        var wb = new MeshBuilder(wand.transform, null);
        // everything below is in the wand's own space: z is along the wand
        wb.TubeLocal(new[] { new Vector3(0f, 0f, -0.14f), new Vector3(0f, 0f, 0.2f), new Vector3(0f, 0f, 0.46f) }, new[] { 0.018f, 0.014f, 0.01f }, WandWood, 0f, 10);
        wb.EllipsoidLocal(new Vector3(0f, 0f, -0.14f), new Vector3(0.024f, 0.024f, 0.024f), Gold, 0f);                // pommel
        wb.EllipsoidLocal(new Vector3(0f, 0f, 0.02f), new Vector3(0.022f, 0.022f, 0.012f), Gold, 0f);                 // grip ring
        wb.EllipsoidLocal(new Vector3(0f, 0f, 0.46f), new Vector3(0.026f, 0.026f, 0.01f), Gold, 0f);                  // gem collar
        wb.StarLocal(new Vector3(0f, 0f, 0.505f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -1f), 0.075f, 0.034f, Gold, 0f);        // star frame, glassy gem inside
        wb.SubMesh(1);
        wb.EllipsoidLocal(new Vector3(0f, 0f, 0.505f), new Vector3(0.036f, 0.036f, 0.03f), White, 0f);              // the glowing gem
        wb.EllipsoidLocal(new Vector3(0.03f, 0f, 0.43f), new Vector3(0.013f, 0.013f, 0.013f), White, 0f);            // little bead
        wb.EllipsoidLocal(new Vector3(-0.03f, 0f, 0.38f), new Vector3(0.011f, 0.011f, 0.011f), White, 0f);

        var wandMesh = wb.ToStaticMesh("Wand_" + sceneName);
        AssetDatabase.CreateAsset(wandMesh, ModelDir + "/Wand_" + sceneName + ".asset");
        wand.AddComponent<MeshFilter>().sharedMesh = wandMesh;
        var wr = wand.AddComponent<MeshRenderer>();
        wr.sharedMaterials = new[] { wandMat, gemMat };

        var tip = new GameObject("WandTip").transform;
        tip.SetParent(wand.transform, false);
        tip.localPosition = new Vector3(0f, 0f, 0.55f);

        // shots and beams come out of the wand tip
        var so = new SerializedObject(Object.FindAnyObjectByType<PlayerSpellCaster>());
        so.FindProperty("launchPoint").objectReferenceValue = tip;
        so.ApplyModifiedProperties();

        var glow = wand.AddComponent<WandGlow>();
        glow.gemIndex = 1;
        EditorUtility.SetDirty(wand);
        BuildBroom(root, sceneName, wandMat);
        Debug.Log("Witch built for " + sceneName + " (" + body.vertexCount + " vertices).");
    }

    // a simple broom to ride: a lavender handle, a bundle of straw bristles tied with gold, and a little star at the front
    static void BuildBroom(Transform root, string sceneName, Material mat)
    {
        var holder = root.Find("Broom");
        if (holder == null) return;

        // the old stand-in model stays in the scene but is hidden
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.name != "WitchBroom") r.enabled = false;

        var old = holder.Find("WitchBroom");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("WitchBroom");
        go.transform.SetParent(holder, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var straw = C(0.98f, 0.84f, 0.55f);
        var strawDark = C(0.88f, 0.68f, 0.40f);
        var mb = new MeshBuilder(go.transform, null);
        mb.TubeLocal(new[] { new Vector3(0f, 0.04f, -0.75f), new Vector3(0f, 0.05f, 0.1f), new Vector3(0f, 0.10f, 0.95f) }, new[] { 0.034f, 0.03f, 0.026f }, WandWood, 0f, 12);
        mb.TubeLocal(new[] { new Vector3(0f, 0.04f, -0.7f), new Vector3(0f, 0.04f, -0.95f), new Vector3(0f, 0.035f, -1.2f), new Vector3(0f, 0.03f, -1.42f) },
                     new[] { 0.06f, 0.15f, 0.17f, 0.04f }, straw, 0f, 16);
        mb.TubeLocal(new[] { new Vector3(0f, 0.035f, -1.1f), new Vector3(0f, 0.03f, -1.45f) }, new[] { 0.11f, 0.02f }, strawDark, 0f, 12);
        mb.EllipsoidLocal(new Vector3(0f, 0.04f, -0.72f), new Vector3(0.075f, 0.075f, 0.035f), Gold, 0f);                  // the tie
        mb.EllipsoidLocal(new Vector3(0f, 0.10f, 0.98f), new Vector3(0.036f, 0.036f, 0.036f), Gold, 0f);
        mb.StarLocal(new Vector3(0f, 0.1f, 1.07f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f), 0.09f, 0.03f, Gold, 0f);

        var mesh = mb.ToStaticMesh("Broom_" + sceneName);
        AssetDatabase.CreateAsset(mesh, ModelDir + "/Broom_" + sceneName + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Material(string path, Shader shader, System.Action<Material> setup)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // a tiny mesh builder: parts are added in world space (or the wand's space) and bound to one bone each

    public class MeshBuilder
    {
        readonly Transform space;                   // vertices are stored relative to this transform
        readonly List<Transform> bones;
        readonly List<Vector3> verts = new List<Vector3>(), rest = new List<Vector3>(), norms = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> boneIndex = new List<int>();
        readonly List<int>[] tris = { new List<int>(), new List<int>() };
        int sub;

        public MeshBuilder(Transform space, List<Transform> bones) { this.space = space; this.bones = bones; }

        public void SubMesh(int i) { sub = i; }

        public void Clear() { verts.Clear(); rest.Clear(); norms.Clear(); colors.Clear(); boneIndex.Clear(); tris[0].Clear(); tris[1].Clear(); sub = 0; }

        int Bone(Transform b) => b == null || bones == null ? 0 : Mathf.Max(0, bones.IndexOf(b));

        // add one vertex given in world space
        int Add(Vector3 world, Vector3 normalWorld, Color c, float sparkle, Transform bone)
        {
            verts.Add(space.InverseTransformPoint(world));
            norms.Add(space.InverseTransformDirection(normalWorld).normalized);
            rest.Add(space.InverseTransformPoint(world));
            colors.Add(new Color(c.r, c.g, c.b, sparkle));
            boneIndex.Add(Bone(bone));
            return verts.Count - 1;
        }

        // ---- world-space parts (the body) ----

        public void Ellipsoid(Vector3 center, Vector3 radii, Color c, Transform bone, float sparkle, int segs = 18, int rings = 12)
        {
            int start = verts.Count;
            for (int y = 0; y <= rings; y++)
            {
                float v = y / (float)rings, ph = v * Mathf.PI;
                for (int x = 0; x <= segs; x++)
                {
                    float th = x / (float)segs * Mathf.PI * 2f;
                    var u = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Sin(th));       // axes: x = right, y = up, z = forward of the world
                    Vector3 pos = center + new Vector3(u.x * radii.x, u.y * radii.y, u.z * radii.z);
                    Vector3 n = new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z);
                    Add(pos, n, c, sparkle, bone);
                }
            }
            Grid(start, segs + 1, rings + 1);
        }

        void Grid(int start, int cols, int rows)
        {
            for (int y = 0; y < rows - 1; y++)
                for (int x = 0; x < cols - 1; x++)
                {
                    int a = start + y * cols + x, b = a + 1, c2 = a + cols, d = c2 + 1;
                    tris[sub].AddRange(new[] { a, c2, b, b, c2, d });
                }
        }

        // a tube through points with a radius at each point
        public void Tube(Vector3[] pts, float[] radii, Color c, Transform bone, float sparkle, int segs)
        {
            int start = verts.Count;
            Vector3 refAxis = Mathf.Abs(Vector3.Dot((pts[pts.Length - 1] - pts[0]).normalized, Vector3.right)) > 0.9f ? Vector3.forward : Vector3.right;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 dir = (pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 u = Vector3.Cross(dir, refAxis).normalized, w = Vector3.Cross(dir, u).normalized;
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2f;
                    Vector3 radial = u * Mathf.Cos(th) + w * Mathf.Sin(th);
                    Add(pts[i] + radial * radii[i], radial, c, sparkle, bone);
                }
            }
            Grid(start, segs + 1, pts.Length);
            // rounded ends
            if (radii[0] > 0.01f) Ellipsoid(pts[0], Vector3.one * radii[0], c, bone, sparkle, segs, 8);
            if (radii[radii.Length - 1] > 0.01f) Ellipsoid(pts[pts.Length - 1], Vector3.one * radii[radii.Length - 1], c, bone, sparkle, segs, 8);
        }

        // a five-pointed star prism facing "face", with "upDir" up
        public void Star(Vector3 center, Vector3 face, Vector3 upDir, float outer, float thickness, Color c, Transform bone)
        {
            face.Normalize();
            Vector3 right = Vector3.Cross(upDir, face).normalized;
            Vector3 up = Vector3.Cross(face, right).normalized;
            var poly = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : outer * 0.45f;
                poly.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            Prism(center, face, right, up, poly, thickness, c, bone);
        }

        // a crescent moon prism facing "face"
        public void Crescent(Vector3 center, Vector3 face, Vector3 upDir, float radius, Color c, Transform bone)
        {
            face.Normalize();
            Vector3 right = Vector3.Cross(upDir, face).normalized;
            Vector3 up = Vector3.Cross(face, right).normalized;
            var poly = new List<Vector2>();
            int n = 14;
            for (int i = 0; i <= n; i++)                                             // the outer arc
            {
                float a = Mathf.Lerp(-130f, 130f, i / (float)n) * Mathf.Deg2Rad;
                poly.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            for (int i = n; i >= 0; i--)                                             // the inner arc, shifted
            {
                float a = Mathf.Lerp(-110f, 110f, i / (float)n) * Mathf.Deg2Rad;
                poly.Add(new Vector2(Mathf.Cos(a) * 0.78f + 0.34f, Mathf.Sin(a) * 0.82f) * radius);
            }
            Prism(center, face, right, up, poly, radius * 0.45f, c, bone);
        }

        // an extruded flat polygon with flat-shaded sides (a fan for the front and back, which is fine for stars and crescents)
        void Prism(Vector3 center, Vector3 face, Vector3 right, Vector3 up, List<Vector2> poly, float thickness, Color c, Transform bone)
        {
            Vector3 front = face * thickness * 0.5f, back = -front;
            Vector3 Pt(Vector2 p, Vector3 off) => center + right * p.x + up * p.y + off;
            Vector2 mid = Vector2.zero;
            foreach (var p in poly) mid += p;
            mid /= poly.Count;

            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                // front face triangle (mid, a, b) and back face
                int f0 = Add(Pt(mid, front), face, c, 0f, bone), f1 = Add(Pt(a, front), face, c, 0f, bone), f2 = Add(Pt(b, front), face, c, 0f, bone);
                tris[sub].AddRange(new[] { f0, f2, f1 });
                int b0 = Add(Pt(mid, back), -face, c, 0f, bone), b1 = Add(Pt(a, back), -face, c, 0f, bone), b2 = Add(Pt(b, back), -face, c, 0f, bone);
                tris[sub].AddRange(new[] { b0, b1, b2 });
                // side quad
                Vector3 sideN = (right * (b.y - a.y) - up * (b.x - a.x)).normalized;
                int s0 = Add(Pt(a, front), sideN, c, 0f, bone), s1 = Add(Pt(b, front), sideN, c, 0f, bone), s2 = Add(Pt(a, back), sideN, c, 0f, bone), s3 = Add(Pt(b, back), sideN, c, 0f, bone);
                tris[sub].AddRange(new[] { s0, s1, s2, s1, s3, s2 });
            }
        }

        // ---- parts given in the wand's own space (z along the wand) ----

        Vector3 W(Vector3 local) => space.TransformPoint(local);

        public void TubeLocal(Vector3[] pts, float[] radii, Color c, float sparkle, int segs)
        {
            var world = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) world[i] = W(pts[i]);
            Tube(world, radii, c, null, sparkle, segs);
        }

        public void EllipsoidLocal(Vector3 center, Vector3 radii, Color c, float sparkle)
        {
            // an ellipsoid whose axes follow the wand: build it round in the world with the mean radius, then squash along z
            int start = verts.Count;
            const int segs = 14, rings = 10;
            for (int y = 0; y <= rings; y++)
            {
                float ph = y / (float)rings * Mathf.PI;
                for (int x = 0; x <= segs; x++)
                {
                    float th = x / (float)segs * Mathf.PI * 2f;
                    var u = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Sin(ph) * Mathf.Sin(th), Mathf.Cos(ph));
                    Vector3 lp = center + new Vector3(u.x * radii.x, u.y * radii.y, u.z * radii.z);
                    Vector3 ln = new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z);
                    Add(W(lp), space.TransformDirection(ln), c, sparkle, null);
                }
            }
            Grid(start, segs + 1, rings + 1);
        }

        // a rounded box: a superellipsoid, in the local space (sharpness 2 = ellipsoid, 4+ = nearly a box)
        public void BoxLocal(Vector3 center, Vector3 half, Color c, float sparkle, float sharpness = 4f)
        {
            int start = verts.Count;
            const int segs = 20, rings = 14;
            float e = 2f / sharpness;
            for (int y = 0; y <= rings; y++)
            {
                float ph = y / (float)rings * Mathf.PI;
                for (int x = 0; x <= segs; x++)
                {
                    float th = x / (float)segs * Mathf.PI * 2f;
                    float sx = Mathf.Sin(ph) * Mathf.Cos(th), sy = Mathf.Cos(ph), sz = Mathf.Sin(ph) * Mathf.Sin(th);
                    var u = new Vector3(SgnPow(sx, e), SgnPow(sy, e), SgnPow(sz, e));
                    Vector3 lp = center + new Vector3(u.x * half.x, u.y * half.y, u.z * half.z);
                    var ln = new Vector3(SgnPow(u.x, sharpness - 1f) / half.x, SgnPow(u.y, sharpness - 1f) / half.y, SgnPow(u.z, sharpness - 1f) / half.z);
                    if (ln.sqrMagnitude < 1e-8f) ln = new Vector3(sx / half.x, sy / half.y, sz / half.z);
                    Add(W(lp), space.TransformDirection(ln), c, sparkle, null);
                }
            }
            Grid(start, segs + 1, rings + 1);
        }

        static float SgnPow(float v, float p) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), p);

        public void StarLocal(Vector3 center, Vector3 upLocal, Vector3 faceLocal, float outer, float thickness, Color c, float sparkle)
        {
            Star(W(center), space.TransformDirection(faceLocal), space.TransformDirection(upLocal), outer, thickness, c, null);
        }

        // ---- result ----

        void Finish(Mesh m, bool skinned, SkinnedMeshRenderer skin)
        {
            m.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetColors(colors);
            m.SetUVs(0, rest);
            m.subMeshCount = tris[1].Count > 0 ? 2 : 1;
            m.SetTriangles(tris[0], 0);
            if (tris[1].Count > 0) m.SetTriangles(tris[1], 1);

            if (skinned)
            {
                var weights = new BoneWeight[verts.Count];
                for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = boneIndex[i], weight0 = 1f };
                m.boneWeights = weights;
                var bind = new Matrix4x4[bones.Count];
                for (int i = 0; i < bind.Length; i++) bind[i] = bones[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
                m.bindposes = bind;
            }
            // make every triangle face the way its vertex normals point
            for (int t = 0; t < m.subMeshCount; t++)
            {
                var list = tris[t];
                for (int i = 0; i + 2 < list.Count; i += 3)
                {
                    Vector3 a = verts[list[i]], b = verts[list[i + 1]], c = verts[list[i + 2]];
                    Vector3 faceN = Vector3.Cross(b - a, c - a);
                    Vector3 avg = norms[list[i]] + norms[list[i + 1]] + norms[list[i + 2]];
                    if (Vector3.Dot(faceN, avg) < 0f) { int tmp = list[i + 1]; list[i + 1] = list[i + 2]; list[i + 2] = tmp; }
                }
                m.SetTriangles(list, t);
            }
            m.RecalculateBounds();
        }

        public Mesh ToMesh(string name, SkinnedMeshRenderer skin)
        {
            var m = new Mesh { name = name };
            Finish(m, true, skin);
            return m;
        }

        public Mesh ToStaticMesh(string name)
        {
            var m = new Mesh { name = name };
            Finish(m, false, null);
            return m;
        }
    }
}
