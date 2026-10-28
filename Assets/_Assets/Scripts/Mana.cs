using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Spell resource, separate from the broom's flight stamina.
// Regenerates slowly after a delay, or much faster while the charge key is held.
public class Mana : MonoBehaviour
{
    [SerializeField] float maxMana = 100f;
    [SerializeField] float regenPerSecond = 6f;
    [SerializeField] float regenDelay = 1.5f;          // seconds after spending before normal regen starts

    [Header("Charging (hold to regain faster)")]
    [SerializeField] InputAction chargeAction = new InputAction("ChargeMana", InputActionType.Button, "<Keyboard>/r");
    [SerializeField] float chargeRegenPerSecond = 35f; // replaces normal regen while charging, starts instantly
    [SerializeField] float interruptLockout = 1f;      // getting hit stops the charge and blocks it for this long
    [SerializeField] Color chargeColor = new Color(0.4f, 0.8f, 1f, 1f);

    public float Current { get; private set; }
    public float Max => maxMana;
    public bool IsCharging { get; private set; }

    // set by anything that holds mana back (the player's barrier): no natural regeneration while true (holding the charge key still works)
    public bool RegenBlocked => RegenScale <= 0f;

    // natural regeneration speed, as the product of everything slowing it: the barrier stops it (0), the broom slows it
    public float RegenScale
    {
        get
        {
            float scale = 1f;
            foreach (var kv in regenScales) scale *= kv.Value;
            return scale;
        }
    }

    readonly System.Collections.Generic.Dictionary<object, float> regenScales = new System.Collections.Generic.Dictionary<object, float>();

    // 1 = normal, 0 = no regeneration; each owner (barrier, broom) sets its own, and 1 clears it
    public void SetRegenScale(object owner, float scale)
    {
        if (scale >= 0.999f) regenScales.Remove(owner);
        else regenScales[owner] = Mathf.Max(0f, scale);
    }

    public void SetRegenBlocked(object owner, bool blocked) => SetRegenScale(owner, blocked ? 0f : 1f);

    // slows (or speeds up) charging with the charge key; the barrier slows it while it is up
    public float ChargeRateMultiplier { get; set; } = 1f;

    public string ChargeKeyLabel =>
        chargeAction.bindings.Count > 0 ? chargeAction.bindings[0].ToDisplayString() : "R";

    public event Action<float, float> Changed;        // current, max
    public event Action Insufficient;                  // a cast was refused for lack of mana

    Health health;
    float lastSpendTime = -999f;
    float lockoutUntil;
    LineRenderer ring;
    Material ringMat;

    void Awake()
    {
        Current = maxMana;
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        chargeAction.Enable();
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        regenScales.Clear();
        ChargeRateMultiplier = 1f;
        chargeAction.Disable();
        if (health != null) health.Damaged -= OnDamaged;
        IsCharging = false;
        if (ring != null) ring.enabled = false;
    }

    void Start() => Changed?.Invoke(Current, maxMana);

    void OnDestroy()
    {
        if (ringMat != null) Destroy(ringMat);
    }

    public bool CanAfford(float cost) => Current >= cost - 0.001f;

    public bool Spend(float cost)
    {
        if (cost <= 0f) return true;
        if (!CanAfford(cost)) return false;
        Current = Mathf.Max(0f, Current - cost);
        lastSpendTime = Time.time;
        Changed?.Invoke(Current, maxMana);
        return true;
    }

    // takes up to this much mana (a barrier soaking damage); returns how much was actually taken
    public float Drain(float amount)
    {
        if (amount <= 0f || Current <= 0f) return 0f;
        float taken = Mathf.Min(amount, Current);
        Current -= taken;
        lastSpendTime = Time.time;
        Changed?.Invoke(Current, maxMana);
        return taken;
    }

    public void Restore(float amount)
    {
        Current = Mathf.Min(maxMana, Current + amount);
        Changed?.Invoke(Current, maxMana);
    }

    public void NotifyInsufficient() => Insufficient?.Invoke();

    void OnDamaged(float amount)
    {
        if (!IsCharging) return;
        IsCharging = false;
        lockoutUntil = Time.time + interruptLockout;
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            IsCharging = false;
            UpdateRing();
            return;
        }

        bool wasCharging = IsCharging;
        IsCharging = chargeAction.IsPressed() && Current < maxMana && Time.time >= lockoutUntil;
        if (IsCharging && !wasCharging) SpellFX.Play(Sfx.Charge, transform.position);

        float rate = 0f;
        if (IsCharging) rate = chargeRegenPerSecond * ChargeRateMultiplier;
        else if (Time.time - lastSpendTime >= regenDelay) rate = regenPerSecond * RegenScale;

        if (rate > 0f && Current < maxMana)
        {
            Current = Mathf.Min(maxMana, Current + rate * Time.deltaTime);
            Changed?.Invoke(Current, maxMana);
        }

        UpdateRing();
    }

    // a ring that pulls inward around the feet while charging
    void UpdateRing()
    {
        if (!IsCharging)
        {
            if (ring != null) ring.enabled = false;
            return;
        }

        if (ring == null)
        {
            ringMat = SpellVisuals.Make(chargeColor, true);
            ring = SpellVisuals.CreateRing(null, ringMat, 0.1f);
        }

        float t = Mathf.Repeat(Time.time * 1.4f, 1f);
        Color c = chargeColor; c.a = 1f - t;
        SpellVisuals.SetColor(ringMat, c);
        ring.enabled = true;
        SpellVisuals.SetRing(ring, transform.position + Vector3.up * 0.05f, Mathf.Lerp(1.8f, 0.4f, t));
    }
}
