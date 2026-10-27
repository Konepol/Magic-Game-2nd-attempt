using UnityEngine;

// Health bar floating above whatever it is a child of (enemies, barriers). It appears when the owner is hurt.
// Used as the shared EnemyHealthBar prefab, and styled by HealthBarStyle like the player's bar.
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] float width = 1.2f;
    [SerializeField] float height = 0.12f;
    [SerializeField] float heightOffset = 0.5f;       // above the top of the owner's collider
    [SerializeField] float visibleTime = 3f;

    [Header("Optional art (leave empty for the default look)")]
    [SerializeField] Texture2D backgroundTexture;
    [SerializeField] Texture2D fillTexture;
    [SerializeField] Texture2D chipTexture;

    Health health;
    Collider ownerCollider;
    Transform visual, fill, chip;
    Material bgMat, fillMat, chipMat;
    float hideAt;
    float target = 1f, shown = 1f, chipValue = 1f;
    Camera cam;

    void Awake()
    {
        health = GetComponentInParent<Health>();
        ownerCollider = health != null ? health.GetComponent<Collider>() : null;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        bgMat = SpellVisuals.Make(HealthBarStyle.Background, false);
        chipMat = SpellVisuals.Make(HealthBarStyle.Chip, false);
        fillMat = SpellVisuals.Make(HealthBarStyle.Full, false);
        ApplyTexture(bgMat, backgroundTexture, true);
        ApplyTexture(chipMat, chipTexture, false);
        ApplyTexture(fillMat, fillTexture, false);

        var bg = MakeQuad("BG", bgMat);
        bg.localScale = new Vector3(width + 0.06f, height + 0.06f, 1f);
        bg.localPosition = new Vector3(0f, 0f, 0.003f);
        chip = MakeQuad("Chip", chipMat);
        chip.localPosition = new Vector3(0f, 0f, 0.002f);
        fill = MakeQuad("Fill", fillMat);

        visual.gameObject.SetActive(false);
    }

    static void ApplyTexture(Material m, Texture2D tex, bool resetTint)
    {
        if (tex == null) return;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        if (resetTint) SpellVisuals.SetColor(m, Color.white);
    }

    Transform MakeQuad(string name, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.transform.SetParent(visual, false);
        return q.transform;
    }

    void OnEnable()
    {
        if (health == null) health = GetComponentInParent<Health>();
        if (health == null) return;
        health.Changed += OnChanged;
        health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health == null) return;
        health.Changed -= OnChanged;
        health.Damaged -= OnDamaged;
    }

    void OnDestroy()
    {
        if (bgMat != null) Destroy(bgMat);
        if (fillMat != null) Destroy(fillMat);
        if (chipMat != null) Destroy(chipMat);
    }

    void OnChanged(float current, float max) => target = Mathf.Clamp01(current / max);

    void OnDamaged(float amount) => hideAt = Time.time + visibleTime;

    void LateUpdate()
    {
        if (health == null) return;

        bool show = Time.time < hideAt && !health.IsDead;
        if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
        if (!show) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // sit above the owner, face the camera, keep the same world size whatever the owner's scale is
        Transform owner = health.transform;
        float top = ownerCollider != null ? ownerCollider.bounds.max.y : owner.position.y + 1f;
        transform.position = new Vector3(owner.position.x, top + heightOffset, owner.position.z);
        transform.rotation = cam.transform.rotation;
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(1f / Mathf.Max(ps.x, 0.01f), 1f / Mathf.Max(ps.y, 0.01f), 1f / Mathf.Max(ps.z, 0.01f));

        // main fill follows quickly; the pale chip drains slowly behind it
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-HealthBarStyle.FillSmoothing * Time.deltaTime));
        chipValue = target > chipValue ? target : Mathf.MoveTowards(chipValue, target, HealthBarStyle.ChipDrainPerSecond * Time.deltaTime);

        SetBar(fill, shown);
        SetBar(chip, Mathf.Max(chipValue, shown));
        SpellVisuals.SetColor(fillMat, HealthBarStyle.FillColor(shown));
    }

    // bars grow from the left edge
    void SetBar(Transform bar, float fraction)
    {
        float w = Mathf.Max(width * fraction, 0.001f);
        bar.localScale = new Vector3(w, height, 1f);
        bar.localPosition = new Vector3(-width * 0.5f + w * 0.5f, 0f, bar == fill ? 0f : 0.002f);
    }
}
