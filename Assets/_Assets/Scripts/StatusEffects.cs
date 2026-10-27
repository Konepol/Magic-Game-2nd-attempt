using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { None, Burn, Slow, Stun }

[Serializable]
public struct StatusEffectSpec
{
    public StatusType type;
    public float duration;     // seconds
    public float magnitude;    // burn: damage per second, slow: fraction of speed removed (0.5 = half speed), stun: unused
}

// Timed effects on anything with a Health: burn (damage over time), slow, stun.
// Created automatically on the first application.
public class StatusEffects : MonoBehaviour
{
    class Active
    {
        public float until;
        public float magnitude;
    }

    const float BurnTick = 0.5f;

    readonly Dictionary<StatusType, Active> active = new Dictionary<StatusType, Active>();
    Health health;
    float nextBurnTick;

    public bool IsStunned => Has(StatusType.Stun);

    // multiply movement speed by this
    public float SpeedMultiplier
    {
        get
        {
            if (active.TryGetValue(StatusType.Slow, out var a) && Time.time < a.until)
                return Mathf.Clamp(1f - a.magnitude, 0.1f, 1f);
            return 1f;
        }
    }

    public bool Has(StatusType t) => active.TryGetValue(t, out var a) && Time.time < a.until;

    // colour used to tint the model while an effect is on
    public bool TryGetTint(out Color tint)
    {
        if (Has(StatusType.Stun)) { tint = new Color(1f, 0.95f, 0.3f); return true; }
        if (Has(StatusType.Burn)) { tint = new Color(1f, 0.45f, 0.1f); return true; }
        if (Has(StatusType.Slow)) { tint = new Color(0.35f, 0.6f, 1f); return true; }
        tint = Color.white;
        return false;
    }

    public static void Apply(Component target, StatusEffectSpec spec)
    {
        if (target == null || spec.type == StatusType.None || spec.duration <= 0f) return;

        var h = target.GetComponentInParent<Health>();
        if (h == null) return;

        var se = h.GetComponent<StatusEffects>();
        if (se == null) se = h.gameObject.AddComponent<StatusEffects>();
        se.Add(spec);
    }

    public void Add(StatusEffectSpec spec)
    {
        if (active.TryGetValue(spec.type, out var a) && Time.time < a.until)
        {
            // refresh, keeping the stronger of the two
            a.until = Mathf.Max(a.until, Time.time + spec.duration);
            a.magnitude = Mathf.Max(a.magnitude, spec.magnitude);
            return;
        }
        active[spec.type] = new Active { until = Time.time + spec.duration, magnitude = spec.magnitude };
        if (spec.type == StatusType.Burn) nextBurnTick = Time.time + BurnTick;
    }

    void Awake() => health = GetComponent<Health>();

    void Update()
    {
        if (health == null || health.IsDead) return;

        if (Has(StatusType.Burn) && Time.time >= nextBurnTick)
        {
            nextBurnTick = Time.time + BurnTick;
            health.TakeDamage(active[StatusType.Burn].magnitude * BurnTick, transform.position);
        }
    }
}
