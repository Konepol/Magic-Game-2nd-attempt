using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatusType { None, Burn, Slow, Stun, Freeze }      // Freeze: stunned inside an ice shell (Absolute Zero)

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

    public bool IsStunned => Has(StatusType.Stun) || Has(StatusType.Freeze);

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
        if (Has(StatusType.Freeze)) { tint = new Color(0.6f, 0.9f, 1f); return true; }
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
        if (spec.type == StatusType.Freeze) IceShell.Grow(gameObject, spec.duration);
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

// the ice that closes around a frozen enemy: a pale glassy shell with spikes that grow out of it, and a shatter when the freeze ends
public class IceShell : MonoBehaviour
{
    float grown, until; Vector3 center; Renderer[] rends; Transform shell; Material mat;
    readonly System.Collections.Generic.List<Transform> spikes = new System.Collections.Generic.List<Transform>();

    public static void Grow(GameObject target, float seconds)
    {
        var existing = target.GetComponentInChildren<IceShell>();
        if (existing != null) { existing.until = Mathf.Max(existing.until, Time.time + seconds); return; }
        var go = new GameObject("IceShell");
        go.transform.SetParent(target.transform, false);
        go.AddComponent<IceShell>().Build(target, seconds);
    }

    void Build(GameObject target, float seconds)
    {
        until = Time.time + seconds;
        rends = target.GetComponentsInChildren<Renderer>();
        var b = new Bounds(target.transform.position + Vector3.up, Vector3.one * 0.5f);
        bool first = true;
        foreach (var r in rends)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        center = b.center;
        float size = Mathf.Clamp(b.size.magnitude * 0.55f, 0.7f, 5f);

        mat = new Material(MagicFX.StarMat);
        var tint = new Color(0.7f, 0.92f, 1f, 0.42f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        mat.color = tint;

        shell = new GameObject("Shell").transform;
        shell.SetParent(transform, false);
        shell.gameObject.AddComponent<MeshFilter>().sharedMesh = MagicFX.OrbMesh();
        var sr = shell.gameObject.AddComponent<MeshRenderer>();
        sr.sharedMaterial = mat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
        shell.localScale = Vector3.one * size;

        var rng = new System.Random(target.name.GetHashCode());
        for (int i = 0; i < 9; i++)
        {
            Vector3 d = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.9f - 0.1f, (float)rng.NextDouble() - 0.5f).normalized;
            var sp = new GameObject("Spike").transform;
            sp.SetParent(transform, false);
            sp.gameObject.AddComponent<MeshFilter>().sharedMesh = SpellImpact.ShardMesh();
            var r2 = sp.gameObject.AddComponent<MeshRenderer>();
            r2.sharedMaterial = mat; r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r2.receiveShadows = false;
            sp.rotation = Quaternion.LookRotation(d);
            sp.localScale = Vector3.one * size * (0.5f + (float)rng.NextDouble() * 0.5f);
            spikes.Add(sp);
        }
        Place();
    }

    void Place()
    {
        shell.position = center;
        float k = Mathf.Clamp01(grown);
        float e = 1f - (1f - k) * (1f - k);
        float baseSize = shell.localScale.x;
        for (int i = 0; i < spikes.Count; i++)
        {
            var sp = spikes[i];
            sp.position = center + sp.forward * baseSize * 0.42f * e;
            sp.localScale = Vector3.one * baseSize * 0.9f * e * (0.6f + 0.4f * ((i * 37) % 10) / 10f);
        }
    }

    void Update()
    {
        grown += Time.deltaTime * 5f;
        var parent = transform.parent;
        if (parent != null) { var r = parent.GetComponentInChildren<Renderer>(); }
        // follow the body (a frozen enemy does not move, but a falling one might)
        if (Time.time >= until || (parent != null && parent.GetComponent<Health>() != null && parent.GetComponent<Health>().IsDead))
        {
            SpellImpact.Play("FrostShard", center, Vector3.up, 1.6f, new Color(0.7f, 0.92f, 1f));
            Destroy(mat);
            Destroy(gameObject);
            return;
        }
        Place();
    }
}
