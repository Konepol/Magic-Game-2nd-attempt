using System.IO;
using UnityEditor;
using UnityEngine;

// Renders a casting circle at chosen points of its drawing progress to PNG files (edit mode, no scene needed).
// Used to check how a circle really looks in the game's own renderer: CircleSnapshot.Capture(path, folder, progresses, size, extraHoldSeconds)
public static class CircleSnapshot
{
    class Anchors : CastingCircle.IAnchorSource
    {
        public Pose GetCirclePose(CircleAnchor anchor, CastingCircleData data, SpellVariant variant)
        {
            return new Pose(new Vector3(0f, 501.5f, data.frontDistance), Quaternion.LookRotation(Vector3.forward));
        }
    }

    public static string Capture(string circleAssetPath, string outFolder, float[] progresses, int size, float extraHoldSeconds, float r, float g, float b)
    {
        var data = AssetDatabase.LoadAssetAtPath<CastingCircleData>(circleAssetPath);
        if (data == null) return "circle asset not found";
        Directory.CreateDirectory(outFolder);

        var camGo = new GameObject("snapshotCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 501.5f, -3.5f);      // far above the scene so nothing else is in view
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.2f, 0.18f);
        var rt = new RenderTexture(size, size, 24);
        cam.targetTexture = rt;

        var circle = CastingCircle.Create(data, new Color(r, g, b), new Anchors(), null, 1f);
        if (circle == null) { Object.DestroyImmediate(camGo); rt.Release(); return "circle not created"; }

        int n = 0;
        foreach (float p in progresses)
        {
            circle.SetProgress(p);
            circle.Tick(0.016f);
            cam.Render();
            Save(rt, Path.Combine(outFolder, "frame_" + (n++) + ".png"));
        }

        // keep it held at full progress for a while (the settled ring circles the middle)
        circle.SetProgress(1f);
        for (float t = 0f; t < extraHoldSeconds; t += 0.016f) circle.Tick(0.016f);
        cam.Render();
        Save(rt, Path.Combine(outFolder, "frame_" + (n++) + ".png"));

        Object.DestroyImmediate(circle.gameObject);
        Object.DestroyImmediate(camGo);
        rt.Release();
        return "saved " + n + " frames";
    }

    static void Save(RenderTexture rt, string path)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        RenderTexture.active = prev;
    }
}
