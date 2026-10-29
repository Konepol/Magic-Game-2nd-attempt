using UnityEditor;
using UnityEngine;

// Helper for checking animation poses without playing: applies muscle values to the player in the open scene and photographs it.
public static class PoseTester
{
    // muscles: "48:0.5,49:-0.2" (muscle index : value); angle: where the camera stands around the player in degrees (0 = in front)
    public static string Shoot(string muscles, string outPath, float angle = 35f, float height = 1.05f, float distance = 5.2f)
    {
        var caster = Object.FindAnyObjectByType<PlayerSpellCaster>();
        var root = caster.transform.root;
        var animator = root.GetComponent<Animator>();

        // remember the skeleton so the scene is left exactly as it was
        var all = root.GetComponentsInChildren<Transform>(true);
        var savedPos = new Vector3[all.Length];
        var savedRot = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) { savedPos[i] = all[i].localPosition; savedRot[i] = all[i].localRotation; }

        var handler = new HumanPoseHandler(animator.avatar, root);
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);

        if (!string.IsNullOrEmpty(muscles))
            foreach (var part in muscles.Split(','))
            {
                var kv = part.Split(':');
                pose.muscles[int.Parse(kv[0])] = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
            }
        handler.SetHumanPose(ref pose);

        var go = new GameObject("TmpCam");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.7f, 0.82f, 1f);
        cam.fieldOfView = 34;
        Vector3 target = new Vector3(root.position.x, animator.GetBoneTransform(HumanBodyBones.Hips).position.y + 0.3f, root.position.z);
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * root.forward;
        cam.transform.position = target + dir * distance;
        cam.transform.LookAt(target);

        var rt = new RenderTexture(600, 800, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(600, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 600, 800), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(go);
        rt.Release();
        handler.Dispose();
        for (int i = 0; i < all.Length; i++) { all[i].localPosition = savedPos[i]; all[i].localRotation = savedRot[i]; }
        return "ok";
    }
}
