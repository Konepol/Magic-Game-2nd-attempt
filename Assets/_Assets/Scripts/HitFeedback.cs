using UnityEngine;
using UnityEngine.AI;

// Flash on hit, knockback away from the hit point, and a tint while a status effect is on.
public class HitFeedback : MonoBehaviour
{
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashTime = 0.12f;
    [SerializeField] float knockbackPerDamage = 0.25f;
    [SerializeField] float maxKnockback = 6f;
    [SerializeField, Range(0f, 1f)] float knockbackResistance = 0f;   // 1 = immovable (heavy enemies)
    [SerializeField] float knockbackDecay = 8f;

    Health health;
    StatusEffects status;
    NavMeshAgent agent;
    Renderer[] renderers;
    Color[] baseColors;
    string[] colorProps;
    MaterialPropertyBlock block;
    float flashUntil;
    Vector3 knock;
    bool tintApplied;

    void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        block = new MaterialPropertyBlock();

        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            bool isBarQuad = r.GetComponentInParent<WorldHealthBar>() != null;
            if (!isBarQuad && (r is MeshRenderer || r is SkinnedMeshRenderer)) list.Add(r);
        }
        renderers = list.ToArray();

        baseColors = new Color[renderers.Length];
        colorProps = new string[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            if (m == null) { colorProps[i] = "_BaseColor"; baseColors[i] = Color.white; continue; }
            colorProps[i] = m.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            baseColors[i] = m.HasProperty(colorProps[i]) ? m.GetColor(colorProps[i]) : Color.white;
        }
    }

    void OnEnable() => Health.AnyDamaged += OnAnyDamaged;
    void OnDisable() => Health.AnyDamaged -= OnAnyDamaged;

    void OnAnyDamaged(Health h, float amount, Vector3 point)
    {
        if (h != health) return;

        flashUntil = Time.time + flashTime;

        Vector3 away = transform.position - point;
        away.y = 0f;
        if (away.sqrMagnitude > 0.04f && knockbackResistance < 1f)   // damage over time has no direction
            knock += away.normalized * Mathf.Min(amount * knockbackPerDamage, maxKnockback) * (1f - knockbackResistance);
    }

    void Update()
    {
        if (health != null && health.IsDead) return;

        if (knock.sqrMagnitude > 0.0025f)
        {
            Vector3 step = knock * Time.deltaTime;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
            else transform.position += step;
            knock = Vector3.Lerp(knock, Vector3.zero, 1f - Mathf.Exp(-knockbackDecay * Time.deltaTime));
        }

        if (status == null) status = GetComponent<StatusEffects>();

        bool flashing = Time.time < flashUntil;
        bool tinted = status != null && status.TryGetTint(out _);
        if (!flashing && !tinted && !tintApplied) return;

        Color tint = Color.white;
        if (tinted) status.TryGetTint(out tint);
        float flash = flashing ? (flashUntil - Time.time) / flashTime : 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = baseColors[i];
            if (tinted) c = Color.Lerp(c, tint, 0.5f);
            if (flashing) c = Color.Lerp(c, flashColor, flash);

            renderers[i].GetPropertyBlock(block);
            block.SetColor(colorProps[i], c);
            renderers[i].SetPropertyBlock(block);
        }
        tintApplied = flashing || tinted;
    }
}
