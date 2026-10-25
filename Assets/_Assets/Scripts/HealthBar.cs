using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] Health health;                   // auto-found in parents if empty
    [SerializeField] Image fill;                      // Image Type = Filled, Horizontal
    [SerializeField] Image delayedFill;               // optional: lighter "chip" bar that trails behind
    [SerializeField] CanvasGroup group;               // optional, for hiding
    [SerializeField] Color fullColor = new Color(0.3f, 0.9f, 0.3f);
    [SerializeField] Color lowColor = new Color(0.95f, 0.25f, 0.2f);
    [SerializeField] float fillSpeed = 8f;
    [SerializeField] float delayedSpeed = 0.6f;       // fill amount per second for the trailing bar

    [Header("World space (enemy bars)")]
    [SerializeField] bool hideUntilDamaged = false;
    [SerializeField] float visibleTime = 3f;
    [SerializeField] float fadeSpeed = 4f;

    float target = 1f, shown = 1f, delayed = 1f, hideAt;

    void Awake()
    {
        if (health == null) health = GetComponentInParent<Health>();
        if (group == null) group = GetComponent<CanvasGroup>();
        if (hideUntilDamaged && group != null) group.alpha = 0f;
    }

    void OnEnable()
    {
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

    void OnChanged(float current, float max) => target = current / max;

    void OnDamaged(float amount) => hideAt = Time.time + visibleTime;

    void Update()
    {
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSpeed * Time.unscaledDeltaTime));
        fill.fillAmount = shown;
        fill.color = Color.Lerp(lowColor, fullColor, Mathf.Clamp01(shown / 0.5f));

        if (delayedFill != null)
        {
            // trailing bar waits, then drains down to the real value; jumps up instantly when healing
            delayed = target > delayed ? target : Mathf.MoveTowards(delayed, target, delayedSpeed * Time.unscaledDeltaTime);
            delayedFill.fillAmount = delayed;
        }

        if (hideUntilDamaged && group != null)
        {
            float a = (Time.time < hideAt && !health.IsDead) ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, a, fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}