using System.Collections.Generic;
using UnityEngine;

// Spawns floating damage numbers for any Health that takes damage.
// Creates itself at scene load, so nothing needs to be placed in the scene.
public class DamageNumbers : MonoBehaviour
{
    class Entry
    {
        public GameObject go;
        public TextMesh text;
        public float spawnTime;
        public Vector3 velocity;
        public float baseSize;
        public Color color;
    }

    [Header("Look")]
    [SerializeField] Color enemyColor = Color.white;
    [SerializeField] Color playerColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] float baseSize = 0.035f;
    [SerializeField] float bigDamage = 30f;              // numbers at or above this grow
    [SerializeField] float bigScale = 1.6f;
    [SerializeField] float distanceScale = 0.05f;        // grows with distance so far numbers stay readable
    [SerializeField] float maxDistanceScale = 3.5f;

    [Header("Motion")]
    [SerializeField] float lifetime = 0.9f;
    [SerializeField] float riseSpeed = 1.8f;
    [SerializeField] float sideSpread = 0.6f;
    [SerializeField] float spawnHeight = 0.4f;           // above the hit point

    readonly List<Entry> active = new List<Entry>();
    readonly Queue<Entry> pool = new Queue<Entry>();
    Font font;
    Material material;
    Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<DamageNumbers>() != null) return;
        new GameObject("DamageNumbers").AddComponent<DamageNumbers>();
    }

    void OnEnable() => Health.AnyDamaged += OnDamaged;
    void OnDisable() => Health.AnyDamaged -= OnDamaged;

    void OnDamaged(Health health, float amount, Vector3 hitPoint)
    {
        var e = pool.Count > 0 ? pool.Dequeue() : CreateEntry();

        bool onPlayer = health.CompareTag("Player");
        e.color = onPlayer ? playerColor : enemyColor;
        e.baseSize = baseSize * (amount >= bigDamage ? bigScale : 1f);
        e.spawnTime = Time.time;
        e.velocity = new Vector3(Random.Range(-sideSpread, sideSpread), riseSpeed, Random.Range(-sideSpread, sideSpread));

        e.text.text = Mathf.CeilToInt(amount).ToString();
        e.text.color = e.color;
        e.go.transform.position = hitPoint + Vector3.up * spawnHeight;
        e.go.SetActive(true);
        active.Add(e);
    }

    Entry CreateEntry()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (material == null)
        {
            material = new Material(font.material);
            material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);   // draw over geometry
            material.renderQueue = 4000;
        }

        var go = new GameObject("DamageNumber");
        go.transform.SetParent(transform, false);
        var text = go.AddComponent<TextMesh>();
        text.font = font;
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 1f;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.SetActive(false);
        return new Entry { go = go, text = text };
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var e = active[i];
            float t = (Time.time - e.spawnTime) / lifetime;
            if (t >= 1f)
            {
                e.go.SetActive(false);
                pool.Enqueue(e);
                active.RemoveAt(i);
                continue;
            }

            var tr = e.go.transform;
            tr.position += e.velocity * Time.deltaTime;
            e.velocity.y = Mathf.Lerp(e.velocity.y, 0f, 3f * Time.deltaTime);   // rise slows down

            if (cam != null)
            {
                tr.rotation = cam.transform.rotation;   // always faces the camera
                float dist = Vector3.Distance(cam.transform.position, tr.position);
                float scale = e.baseSize * Mathf.Clamp(1f + dist * distanceScale, 1f, maxDistanceScale);
                // quick pop, then settle
                scale *= Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t * 6f));
                tr.localScale = Vector3.one * scale;
            }

            Color c = e.color;
            c.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);   // fades out over the second half
            e.text.color = c;
        }
    }
}
