using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Screen shake by driving the noise on the Cinemachine cameras. Creates itself at scene load.
public class CameraShake : MonoBehaviour
{
    static CameraShake instance;

    [SerializeField] float maxAmplitude = 7f;      // added to the camera's own noise amplitude at full shake
    [SerializeField] float maxFrequency = 9f;
    [SerializeField] float decayPerSecond = 1.6f;

    class Rig
    {
        public CinemachineBasicMultiChannelPerlin noise;
        public float baseAmplitude, baseFrequency;
    }

    readonly List<Rig> rigs = new List<Rig>();
    float trauma;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<CameraShake>() != null) return;
        new GameObject("CameraShake").AddComponent<CameraShake>();
    }

    void Awake() => instance = this;

    public static void Add(float amount)
    {
        if (instance != null) instance.trauma = Mathf.Clamp01(instance.trauma + amount);
    }

    // shake that fades with distance from the camera
    public static void AddAt(Vector3 point, float amount, float range = 45f)
    {
        var cam = Camera.main;
        if (cam == null) { Add(amount); return; }
        float falloff = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, point) / range);
        Add(amount * falloff);
    }

    void Scan()
    {
        rigs.Clear();
        foreach (var n in FindObjectsByType<CinemachineBasicMultiChannelPerlin>(FindObjectsInactive.Include))
            rigs.Add(new Rig { noise = n, baseAmplitude = n.AmplitudeGain, baseFrequency = n.FrequencyGain });
    }

    void Update()
    {
        if (rigs.Count == 0 && Time.unscaledTime >= nextScan)
        {
            Scan();
            nextScan = Time.unscaledTime + 1f;
        }

        trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
        float shake = trauma * trauma;

        foreach (var r in rigs)
        {
            if (r.noise == null) continue;
            r.noise.AmplitudeGain = r.baseAmplitude + shake * maxAmplitude;
            r.noise.FrequencyGain = r.baseFrequency + shake * maxFrequency;
        }
    }
}
