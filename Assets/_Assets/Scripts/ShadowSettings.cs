using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The shadow option in Options > Gameplay:
//   Normal: real soft shadows from the sun (the ground shows them soft and tinted, see Magic/ToonGround).
//   Low:    no real shadows; every character gets a soft round blob shadow under it instead (cheaper and very readable).
//   Off:    no shadows at all.
// Characters register themselves (Attach) so they get a blob shadow when the option is Low.
public static class ShadowSettings
{
    public enum Quality { Normal = 0, Low = 1, Off = 2 }

    static readonly List<BlobShadow> blobs = new List<BlobShadow>();
    static readonly Dictionary<Light, LightShadows> originalShadows = new Dictionary<Light, LightShadows>();

    public static Quality Current => (Quality)Mathf.Clamp(PlayerSettings.ShadowQuality, 0, 2);

    public static string Label(Quality q) => q == Quality.Normal ? "Normal" : q == Quality.Low ? "Low" : "Off";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerSettings.Changed -= Apply;
        PlayerSettings.Changed += Apply;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        originalShadows.Clear();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) Attach(player.transform.root.gameObject, 0.55f);
        Apply();
    }

    // gives a character a blob shadow, shown only when the option is Low
    public static void Attach(GameObject go, float radius)
    {
        var blob = go.GetComponent<BlobShadow>();
        if (blob == null)
        {
            blob = go.AddComponent<BlobShadow>();
            blob.radius = radius;
        }
        if (!blobs.Contains(blob)) blobs.Add(blob);
        blob.enabled = Current == Quality.Low;
    }

    public static void Apply()
    {
        var q = Current;

        foreach (var light in Object.FindObjectsByType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            if (!originalShadows.ContainsKey(light)) originalShadows[light] = light.shadows == LightShadows.None ? LightShadows.Soft : light.shadows;
            light.shadows = q == Quality.Normal ? originalShadows[light] : LightShadows.None;
        }

        blobs.RemoveAll(b => b == null);
        foreach (var b in blobs) b.enabled = q == Quality.Low;
    }
}
