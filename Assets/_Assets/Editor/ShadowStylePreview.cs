using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders a picture of the boss arena with one of the shadow styles ("soft", "hard" or "blob") so the styles can be compared.
// Nothing is saved: the materials and the extra characters are put back as they were.
public static class ShadowStylePreview
{
    public static string Shoot(string mode, string outPath, string scenePath = "Assets/Scenes/Scene1.unity")
    {
        EditorSceneManager.OpenScene(scenePath);
        var shader = Shader.Find("Magic/ToonGround");
        var player = Object.FindAnyObjectByType<PlayerSpellCaster>().transform.root;

        // characters to look at: the witch, a few enemies and the boss model
        var temps = new List<GameObject>();
        Vector3 c = player.position;
        string[] enemies = { "Rusher", "Charger", "Heavy", "Seeker" };
        for (int i = 0; i < enemies.Length; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Assets/Prefabs/Enemies/" + enemies[i] + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            go.transform.position = c + new Vector3(-3.6f + i * 2.4f, 1.0f * pf.transform.localScale.y, 4f);
            go.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            temps.Add(go);
        }
        var boss = Object.Instantiate(Resources.Load<GameObject>("Boss/WardenModel"));
        boss.transform.position = c + new Vector3(1f, 0f, 15f);
        boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        temps.Add(boss);

        // the ground and walls get the toon ground shader
        var swapped = new List<(Renderer r, Material[] mats)>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.transform.root == player || r.GetComponentInParent<CreatureAnimator>() != null || temps.Contains(r.transform.root.gameObject)) continue;
            var mats = r.sharedMaterials;
            if (mats.Length == 0 || mats[0] == null || !mats[0].shader.name.StartsWith("Universal Render Pipeline/")) continue;
            swapped.Add((r, mats));
            var copy = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = new Material(shader);
                if (mats[i] != null && mats[i].HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", mats[i].GetTexture("_BaseMap")); m.SetTextureScale("_BaseMap", mats[i].GetTextureScale("_BaseMap")); }
                if (mats[i] != null && mats[i].HasProperty("_BaseColor")) m.SetColor("_BaseColor", mats[i].GetColor("_BaseColor"));
                m.SetFloat("_ShadowSoftness", mode == "hard" ? 0f : 0.7f);
                if (mode == "before") { copy[i] = mats[i]; continue; }
                m.SetFloat("_ReceiveShadow", mode == "blob" ? 0f : 1f);
                copy[i] = m;
            }
            r.sharedMaterials = copy;
        }

        // blob style: the characters stop casting real shadows and get a round one each
        var castModes = new List<(Renderer r, UnityEngine.Rendering.ShadowCastingMode m)>();
        var blobs = new List<BlobShadow>();
        if (mode == "blob")
        {
            var chars = new List<Transform> { player };
            foreach (var t in temps) chars.Add(t.transform);
            foreach (var ch in chars)
            {
                foreach (var r in ch.GetComponentsInChildren<Renderer>()) { castModes.Add((r, r.shadowCastingMode)); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                var b = ch.gameObject.AddComponent<BlobShadow>();
                b.radius = ch == boss.transform ? 3.6f : ch == player ? 0.55f : 0.7f * ch.localScale.x;
                typeof(BlobShadow).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(b, null);
                typeof(BlobShadow).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(b, null);
                blobs.Add(b);
            }
        }

        // the picture
        var camGo = new GameObject("TmpCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.transform.position = c + new Vector3(4.5f, 4.2f, -3.5f);
        cam.transform.LookAt(c + new Vector3(-0.5f, 0.6f, 4f));
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
        RenderTexture.active = null;
        rt.Release();
        Object.DestroyImmediate(camGo);

        // put everything back
        foreach (var (r, mats) in swapped) r.sharedMaterials = mats;
        foreach (var (r, m) in castModes) if (r != null) r.shadowCastingMode = m;
        foreach (var b in blobs) if (b != null) Object.DestroyImmediate(b);
        foreach (var t in temps) Object.DestroyImmediate(t);
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) if (go.name.EndsWith("BlobShadow")) Object.DestroyImmediate(go);
        return "ok " + swapped.Count + " surfaces";
    }
}
