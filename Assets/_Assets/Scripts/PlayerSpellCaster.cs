using UnityEngine;
using UnityEngine.InputSystem;

// How a spell restricts movement while it is being cast. Set per spell variant, so each spell decides for itself.
// (On the broom the player is slowed by both Slow and Stand, never stopped.)
public enum CastMovement
{
    Free,    // no restriction
    Slow,    // keeps moving, but slower
    Stand,   // cannot walk, sprint or jump on foot
}

public class PlayerSpellCaster : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] TargetLockOn lockOn;
    [SerializeField] Transform muzzle;               // empty at the hand or weapon
    [SerializeField] WeaponData weapon;
    [SerializeField] WeaponData[] loadout;           // weapons to cycle through with the switch key

    [Header("Skill Keys (one per weapon slot)")]
    [SerializeField] InputAction[] skillActions =
    {
        new InputAction("Skill1", InputActionType.Button, "<Mouse>/leftButton"),
        new InputAction("Skill2", InputActionType.Button, "<Mouse>/rightButton"),
        new InputAction("Skill3", InputActionType.Button, "<Keyboard>/q"),
        new InputAction("Skill4", InputActionType.Button, "<Keyboard>/e"),
    };
    [SerializeField] InputAction switchWeaponAction = new InputAction("SwitchWeapon", InputActionType.Button, "<Keyboard>/tab");

    [Header("Targeting")]
    [SerializeField] string damageTag = "Enemy";     // what the spells hurt
    [SerializeField] string ownTag = "Player";       // what barriers protect

    [Header("Aiming")]
    [SerializeField] LayerMask aimMask = ~0;         // exclude the Player layer
    [SerializeField] LayerMask groundMask = ~0;      // what the ground ring and barriers rest on (exclude Player and Enemy)
    [SerializeField] float maxAimDistance = 100f;
    [SerializeField] bool useMousePosition = false;  // false = screen center crosshair

    [Header("Casting")]

    [Header("Feedback")]
    [SerializeField] bool showChargeBar = true;
    [SerializeField] bool showWeaponLabel = true;

    float[] cooldownEnd;
    float castingUntil;
    CastMovement lastCastMovement;
    float[] cooldownTotal;
    Mana mana;
    float[] pressTime;
    bool[] charging;
    LineRenderer aimRing;
    Material aimRingMat;

    public WeaponData Weapon => weapon;
    // The strictest movement restriction from: spells being held right now (their HOLDING state, once held past a moment),
    // and the spell just fired (its TAP or HELD state, for its recovery time). Movement scripts slow or root the player from this.
    public CastMovement ActiveCastMovement
    {
        get
        {
            var result = CastMovement.Free;
            if (!enabled || charging == null) return result;

            if (Time.time < castingUntil) result = lastCastMovement;

            for (int i = 0; i < charging.Length; i++)
            {
                if (!charging[i]) continue;
                var spell = GetSpell(i);
                if (spell == null) continue;
                if (Time.time - pressTime[i] < spell.holdingStartsAfter) continue;   // still just a tap
                if (spell.holdingMovement > result) result = spell.holdingMovement;
            }
            return result;
        }
    }

    public bool IsCasting => ActiveCastMovement != CastMovement.Free;

    public bool CanSwitchWeapon => loadout != null && loadout.Length > 1;

    public void Equip(WeaponData newWeapon)
    {
        weapon = newWeapon;
        for (int i = 0; i < charging.Length; i++) charging[i] = false;
    }

    // 0..1 progress of the current hold toward the hold variant, 0 when not charging
    public float GetCharge(int slot)
    {
        var spell = GetSpell(slot);
        if (spell == null || !spell.hasHoldVariant || !charging[slot]) return 0f;
        return Mathf.Clamp01((Time.time - pressTime[slot]) / Mathf.Max(spell.holdThreshold, 0.01f));
    }

    void Awake()
    {
        mana = GetComponentInParent<Mana>();
        cooldownEnd = new float[skillActions.Length];
        cooldownTotal = new float[skillActions.Length];
        pressTime = new float[skillActions.Length];
        charging = new bool[skillActions.Length];
    }

    void OnEnable()
    {
        foreach (var a in skillActions) a.Enable();
        switchWeaponAction.Enable();
    }

    void OnDisable()
    {
        foreach (var a in skillActions) a.Disable();
        switchWeaponAction.Disable();
        SetAimRing(false, Vector3.zero, 0f, Color.white);
    }

    void OnDestroy()
    {
        if (aimRingMat != null) Destroy(aimRingMat);
    }

    // ---------- read-only info for the HUD ----------

    public int SlotCount => skillActions.Length;
    public SpellData GetSpellAt(int slot) => GetSpell(slot);
    public bool IsCharging(int slot) => charging[slot];

    public float GetCooldown01(int slot)
    {
        float total = cooldownTotal[slot];
        return total <= 0f ? 0f : Mathf.Clamp01((cooldownEnd[slot] - Time.time) / total);
    }

    public string GetKeyLabel(int slot)
    {
        var a = skillActions[slot];
        return a.bindings.Count > 0 ? a.bindings[0].ToDisplayString() : "";
    }

    SpellData GetSpell(int slot) => weapon != null ? weapon.GetSpell(slot) : null;

    void Update()
    {
        if (loadout != null && loadout.Length > 1 && switchWeaponAction.WasPressedThisFrame())
        {
            int idx = System.Array.IndexOf(loadout, weapon);
            Equip(loadout[(idx + 1) % loadout.Length]);
        }

        // charging mana takes over: no casting or aiming until it stops
        if (mana != null && mana.IsCharging)
        {
            for (int i = 0; i < charging.Length; i++) charging[i] = false;
            SetAimRing(false, Vector3.zero, 0f, Color.white);
            return;
        }

        bool aimingNow = false;

        for (int i = 0; i < skillActions.Length; i++)
        {
            var spell = GetSpell(i);
            if (spell == null) { charging[i] = false; continue; }

            var action = skillActions[i];

            // spells with a hold variant, or an aimed tap, are decided on release; the rest fire on press
            bool decidedOnRelease = spell.hasHoldVariant || spell.tap.NeedsAim;

            if (decidedOnRelease)
            {
                if (action.WasPressedThisFrame() && Time.time >= cooldownEnd[i])
                {
                    charging[i] = true;
                    pressTime[i] = Time.time;
                }

                if (!charging[i]) continue;

                var variant = spell.Pick(Time.time - pressTime[i]);

                // the ring follows the aim while the key is held
                if (variant.NeedsAim)
                {
                    aimingNow = true;
                    SetAimRing(true, GetAoeTarget(variant, out _), variant.aoeRadius, variant.effectColor);
                }

                if (action.WasReleasedThisFrame())
                {
                    charging[i] = false;
                    Cast(i, variant, spell);
                }
            }
            else
            {
                bool wants = spell.autoRepeatWhenHeld ? action.IsPressed() : action.WasPressedThisFrame();
                if (wants && Time.time >= cooldownEnd[i]) Cast(i, spell.tap, spell);
            }
        }

        if (!aimingNow) SetAimRing(false, Vector3.zero, 0f, Color.white);
    }

    void Cast(int slot, SpellVariant v, SpellData spell)
    {
        if (Time.time < cooldownEnd[slot] || muzzle == null || mainCamera == null) return;

        if (mana != null && !mana.CanAfford(v.manaCost))
        {
            mana.NotifyInsufficient();
            return;
        }

        bool cast = false;
        switch (v.kind)
        {
            case SpellKind.Projectile: cast = CastProjectile(v, spell); break;
            case SpellKind.Laser: cast = CastLaser(v); break;
            case SpellKind.GroundAoE:
                AoeStrike.Spawn(GetAoeTarget(v, out bool inAir), v, damageTag, inAir);
                cast = true;
                break;
            case SpellKind.Barrier: cast = CastBarrier(v); break;
        }

        if (!cast) return;

        cooldownEnd[slot] = Time.time + v.cooldown;
        bool wasHold = spell.hasHoldVariant && ReferenceEquals(v, spell.hold);
        castingUntil = Time.time + (wasHold ? spell.heldRecovery : spell.tapRecovery);
        lastCastMovement = wasHold ? spell.heldMovement : spell.tapMovement;
        cooldownTotal[slot] = v.cooldown;
        if (mana != null) mana.Spend(v.manaCost);

        Sfx sound = v.kind == SpellKind.Laser ? Sfx.Laser : v.kind == SpellKind.Barrier ? Sfx.BarrierPlace : Sfx.Cast;
        SpellFX.Play(sound, muzzle.position, 0.8f);
    }

    // ---------- aiming ----------

    // like Physics.Raycast, but our own barriers are invisible to it so they never get in the way of aiming or placing spells
    bool RaycastSkippingOwnBarriers(Vector3 origin, Vector3 direction, float distance, LayerMask mask, out RaycastHit result)
    {
        var hits = Physics.RaycastAll(origin, direction, distance, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var barrier = h.collider.GetComponentInParent<Barrier>();
            if (barrier != null && barrier.Blocks(ownTag)) continue;
            result = h;
            return true;
        }
        result = default;
        return false;
    }

    Vector3 GetAimDirection()
    {
        Vector3 aimPoint = (lockOn != null && lockOn.IsLocked) ? lockOn.AimPoint : GetCursorAimPoint();
        Vector3 dir = aimPoint - muzzle.position;

        // aim point closer than the muzzle would flip the shot backwards, so fall back to camera forward
        if (dir.sqrMagnitude < 0.01f || Vector3.Dot(dir, mainCamera.transform.forward) < 0f)
            dir = mainCamera.transform.forward;
        return dir.normalized;
    }

    Vector3 GetCursorAimPoint()
    {
        Vector2 screenPoint = useMousePosition
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Ray ray = mainCamera.ScreenPointToRay(screenPoint);

        return RaycastSkippingOwnBarriers(ray.origin, ray.direction, maxAimDistance, aimMask, out RaycastHit hit)
            ? hit.point
            : ray.GetPoint(maxAimDistance);   // nothing hit: aim at a far point along the ray
    }

    // the spot on the ground the ring sits on, limited to the spell's range from the player
    // where an area spell lands: on the target itself when locked on (even if it is in the air),
    // otherwise on the ground under the crosshair
    Vector3 GetAoeTarget(SpellVariant v, out bool inAir)
    {
        inAir = false;
        bool locked = lockOn != null && lockOn.IsLocked;
        Vector3 p = locked ? lockOn.AimPoint : GetCursorAimPoint();

        Vector3 origin = transform.position;
        Vector3 flat = p - origin;
        flat.y = 0f;
        if (flat.magnitude > v.aoeRange) p = origin + flat.normalized * v.aoeRange + Vector3.up * (p.y - origin.y);

        bool hasGround = RaycastSkippingOwnBarriers(p + Vector3.up * 50f, Vector3.down, 200f, groundMask, out RaycastHit hit);

        // locked onto something well above the ground: keep the spell at its height instead of dropping it to the floor
        if (locked && hasGround && p.y - hit.point.y > 2.5f)
        {
            inAir = true;
            return p;
        }
        return hasGround ? hit.point : p;
    }

    void SetAimRing(bool visible, Vector3 center, float radius, Color color)
    {
        if (!visible)
        {
            if (aimRing != null) aimRing.enabled = false;
            return;
        }

        if (aimRing == null)
        {
            aimRingMat = SpellVisuals.Make(color, false);
            aimRing = SpellVisuals.CreateRing(null, aimRingMat, 0.12f);
        }
        SpellVisuals.SetColor(aimRingMat, color);
        aimRing.enabled = true;
        SpellVisuals.SetRing(aimRing, center + Vector3.up * 0.06f, radius);
    }

    // ---------- spell kinds ----------

    bool CastProjectile(SpellVariant v, SpellData spell)
    {
        var prefab = v.projectilePrefab != null ? v.projectilePrefab : spell.defaultProjectile;
        if (prefab == null) return false;

        Vector3 dir = GetAimDirection();

        int count = Mathf.Max(1, v.projectileCount);
        for (int k = 0; k < count; k++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-v.spreadDegrees * 0.5f, v.spreadDegrees * 0.5f, k / (float)(count - 1));
            Vector3 shotDir = Quaternion.AngleAxis(angle, mainCamera.transform.up) * dir;

            var p = Instantiate(prefab, muzzle.position, Quaternion.LookRotation(shotDir));
            p.Init(v.damage, v.projectileSpeed, v.lifetime, v.projectileScale, v.explosionRadius,
                v.pierceCount, v.explodeOnPierce, v.breaksBarriers, v.status);
        }
        return true;
    }

    // instant hit along the aim line
    bool CastLaser(SpellVariant v)
    {
        Vector3 origin = muzzle.position;
        Vector3 dir = GetAimDirection();
        Vector3 end = origin + dir * v.laserRange;

        var hits = Physics.SphereCastAll(origin, v.laserRadius, dir, v.laserRange, aimMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var done = new System.Collections.Generic.HashSet<IDamageable>();
        foreach (var hit in hits)
        {
            if (hit.distance <= 0f) continue;                        // started inside it
            if (hit.collider.transform.IsChildOf(transform.root)) continue;   // never hit ourselves

            var barrier = hit.collider.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (!barrier.Blocks(damageTag)) continue;            // our own barrier lets the beam through
                bool destroyed = barrier.Hit(v.damage, v.breaksBarriers, hit.point);
                if (destroyed && v.breaksBarriers) continue;         // broken instantly: the beam carries on
                end = hit.point;
                break;
            }

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && hit.collider.CompareTag(damageTag))
            {
                if (done.Add(target))
                {
                    target.TakeDamage(v.damage, hit.point);
                    StatusEffects.Apply(hit.collider, v.status);
                }
                if (!v.laserPierces) { end = hit.point; break; }
                continue;
            }

            end = hit.point;   // solid surface stops the beam
            break;
        }

        LaserBeam.Spawn(origin, end, v.laserWidth, v.effectColor, v.laserDuration);
        SpellFX.Sparks(end, v.effectColor);
        return true;
    }

    // a wall in front of the player, facing where they are aiming
    bool CastBarrier(SpellVariant v)
    {
        // the wall faces wherever the player is aiming (the locked target, or the crosshair), including up and down
        Vector3 aim = (lockOn != null && lockOn.IsLocked)
            ? (lockOn.AimPoint - muzzle.position).normalized
            : GetAimDirection();

        // roughly level: snap upright onto the ground. Standing on the ground allows a wider "roughly level" than being in the air.
        bool standing = RaycastSkippingOwnBarriers(transform.position + Vector3.up, Vector3.down, 1.6f, groundMask, out _);
        float snapLimit = standing ? 0.45f : 0.2f;

        if (Mathf.Abs(aim.y) > snapLimit)
        {
            Barrier.Spawn(muzzle.position + aim * v.barrierDistance, Quaternion.LookRotation(aim, Vector3.up),
                new Vector3(v.barrierWidth, v.barrierHeight, v.barrierThickness),
                v.barrierHealth, v.barrierDuration, ownTag, v.effectColor);
            return true;
        }

        Vector3 dir = aim;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
        dir.Normalize();

        Vector3 pos = transform.position + dir * v.barrierDistance;

        // standing on (or near) the ground: the wall rests on it. In the air: it hangs at the player's height.
        bool nearGround = RaycastSkippingOwnBarriers(pos + Vector3.up * 3f, Vector3.down, 8f, groundMask, out RaycastHit hit)
                          && hit.point.y >= transform.position.y - 1.5f;
        pos.y = nearGround ? hit.point.y + v.barrierHeight * 0.5f : muzzle.position.y;

        Barrier.Spawn(pos, Quaternion.LookRotation(dir),
            new Vector3(v.barrierWidth, v.barrierHeight, v.barrierThickness),
            v.barrierHealth, v.barrierDuration, ownTag, v.effectColor);
        return true;
    }

    // charge bar under the crosshair for hold spells, plus the equipped weapon name
    void OnGUI()
    {
        if (showWeaponLabel && weapon != null)
        {
            string hint = loadout != null && loadout.Length > 1 ? "  (Tab to switch)" : "";
            GUI.Label(new Rect(10f, 10f, 400f, 24f), "Weapon: " + weapon.displayName + hint);
        }

        if (!showChargeBar || charging == null) return;

        for (int i = 0; i < charging.Length; i++)
        {
            if (!charging[i]) continue;
            var spell = GetSpell(i);
            if (spell == null || !spell.hasHoldVariant) continue;

            float charge = GetCharge(i);
            const float w = 160f, h = 10f;
            var rect = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f + 40f, w, h);

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = charge >= 1f ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.8f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * charge, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
            return;   // one bar is enough
        }
    }
}
