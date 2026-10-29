using UnityEngine;
using UnityEngine.InputSystem;

// The player's own barrier, part of the base kit (not a spell). Hold the key and a honeycomb of hexagon panels hovers in front of the player,
// pointing wherever a spell would be aimed (the cursor / crosshair, or the locked target), like the spells do. While it is up natural mana regeneration stops, and the wall's strength IS the player's mana:
// every hit drains mana, and when the mana is gone the barrier breaks and cannot be raised again for a few seconds.
[RequireComponent(typeof(Mana))]
public class PlayerBarrier : MonoBehaviour
{
    [SerializeField] InputAction raiseAction = new InputAction("Barrier", InputActionType.Button, "<Keyboard>/f");
    [SerializeField, Tooltip("On: hold the key to keep the barrier up. Off: press once to raise it, press again to lower it.")]
    bool holdToRaise = true;

    [Header("Shape")]
    [SerializeField] float distance = 2.2f;                // how far in front of the player the wall hovers
    [SerializeField] float width = 3.2f;                   // size of the whole honeycomb
    [SerializeField] float height = 2.4f;
    [SerializeField, Tooltip("Size of one hexagon panel, centre to corner. Smaller panels = more of them.")]
    float hexRadius = 0.42f;
    [SerializeField] float thickness = 0.2f;
    [SerializeField] float hoverHeight = 0.1f;             // lift above the player's feet
    [SerializeField] float followSpeed = 16f;              // how quickly it follows the aim
    [SerializeField, Tooltip("How far up or down the aim can point (0 = level, 1 = straight up) before the wall tips over to face it.")]
    float levelLimit = 0.3f;
    [SerializeField] Color color = new Color(0.4f, 0.8f, 1f, 0.4f);

    [Header("Charging mana while the barrier is up")]
    [SerializeField, Range(0.1f, 1f), Tooltip("Share of the hexagon panels that stay up while charging (the outer ones drop away).")]
    float chargingCoverage = 0.45f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Charging is this much of its normal speed while the barrier is up.")]
    float chargeRateWithBarrier = 0.4f;

    [Header("Mana")]
    [SerializeField, Tooltip("Mana lost per point of damage the barrier takes.")]
    float manaPerDamage = 1f;
    [SerializeField, Tooltip("The barrier cannot be raised with less mana than this.")]
    float minManaToRaise = 5f;
    [SerializeField, Tooltip("After the barrier breaks it cannot be raised again for this long.")]
    float breakLockout = 3f;

    public bool IsUp => barrier != null;
    public float LockoutRemaining => Mathf.Max(0f, lockoutUntil - Time.time);
    public int PanelsUp => barrier != null ? barrier.PanelsUp : 0;
    public int PanelsTotal => barrier != null ? barrier.PanelsTotal : 0;
    public string KeyLabel => raiseAction.bindings.Count > 0 ? raiseAction.bindings[0].ToDisplayString() : "F";

    Mana mana;
    Health health;
    PlayerSpellCaster caster;
    Barrier barrier;
    bool wanted;
    float lockoutUntil;

    void Awake()
    {
        mana = GetComponent<Mana>();
        health = GetComponent<Health>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
    }

    void OnEnable() => raiseAction.Enable();

    void OnDisable()
    {
        raiseAction.Disable();
        Lower();
    }

    void Update()
    {
        if (health != null && health.IsDead) { Lower(); return; }

        if (holdToRaise) wanted = raiseAction.IsPressed();
        else if (raiseAction.WasPressedThisFrame()) wanted = !wanted;

        bool canRaise = Time.time >= lockoutUntil && mana.Current >= minManaToRaise;
        if (wanted && barrier == null && canRaise) Raise();
        else if (barrier != null && !wanted) Lower();

        if (barrier != null)
        {
            mana.SetRegenBlocked(this, true);
            mana.ChargeRateMultiplier = chargeRateWithBarrier;                 // charging still works, more slowly
            barrier.SetCoverage(mana.IsCharging ? chargingCoverage : 1f);      // and with fewer panels
            Follow(false);
        }
    }

    Vector3 Aim()
    {
        Vector3 a = caster != null ? caster.AimDirection : transform.forward;
        return a.sqrMagnitude < 0.001f ? transform.forward : a.normalized;
    }

    // wherever the player aims. Roughly level aim keeps the wall upright at the player's height; looking well up or down tips it to face the aim.
    void Pose(out Vector3 pos, out Quaternion rot)
    {
        Vector3 aim = Aim();

        if (Mathf.Abs(aim.y) > levelLimit)
        {
            Vector3 origin = caster != null ? caster.AimOrigin : transform.position + Vector3.up * 1.4f;
            pos = origin + aim * distance;
            rot = Quaternion.LookRotation(aim, Vector3.up);
            return;
        }

        Vector3 flat = aim;
        flat.y = 0f;
        flat = flat.sqrMagnitude < 0.001f ? transform.forward : flat.normalized;
        pos = transform.position + flat * distance + Vector3.up * (height * 0.5f + hoverHeight);
        rot = Quaternion.LookRotation(flat, Vector3.up);
    }

    void Raise()
    {
        Pose(out Vector3 pos, out Quaternion rot);
        barrier = Barrier.SpawnFromMana(pos, rot, width, height, hexRadius, thickness, mana, manaPerDamage, tag, color);
        barrier.Broken += OnBroken;
        SpellFX.Play(Sfx.BarrierPlace, pos, 0.9f);
    }

    void Follow(bool snap)
    {
        Pose(out Vector3 pos, out Quaternion rot);
        float k = snap ? 1f : 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        barrier.transform.SetPositionAndRotation(Vector3.Lerp(barrier.transform.position, pos, k),
                                                 Quaternion.Slerp(barrier.transform.rotation, rot, k));
    }

    void OnBroken()
    {
        barrier = null;
        mana.SetRegenBlocked(this, false);
        mana.ChargeRateMultiplier = 1f;
        wanted = false;
        lockoutUntil = Time.time + breakLockout;
    }

    void Lower()
    {
        wanted = false;
        mana.SetRegenBlocked(this, false);
        mana.ChargeRateMultiplier = 1f;
        if (barrier == null) return;

        barrier.Broken -= OnBroken;
        Destroy(barrier.gameObject);
        barrier = null;
    }
}
