using System.Collections.Generic;
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

public class PlayerSpellCaster : MonoBehaviour, CastingCircle.IAnchorSource
{
    [Header("References")]
    [SerializeField] Camera mainCamera;
    [SerializeField] TargetLockOn lockOn;
    [SerializeField] Transform muzzle;               // empty at the hand or weapon
    [SerializeField] Transform launchPoint;          // where shots and beams come out (the wand tip); the muzzle is used when empty
    [SerializeField] WeaponData weapon;
    [SerializeField] WeaponData[] loadout;           // weapons to cycle through with the switch key
    [SerializeField, Tooltip("Use the spells the player picked in the main menu's Customize screen instead of the weapon above.")]
    bool useProfileLoadout = true;

    [Header("Skill Keys (one per weapon slot)")]
    [SerializeField] InputAction[] skillActions =
    {
        new InputAction("Skill1", InputActionType.Button, "<Mouse>/leftButton"),
        new InputAction("Skill2", InputActionType.Button, "<Mouse>/rightButton"),
        new InputAction("Skill3", InputActionType.Button, "<Keyboard>/q"),
        new InputAction("Skill4", InputActionType.Button, "<Keyboard>/e"),
    };
    [SerializeField, Tooltip("Keys for the slots after the ones above (slot 5, 6, 7 ...).")]
    string[] extraSkillKeys = { "<Keyboard>/r", "<Keyboard>/t", "<Keyboard>/g" };
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
    [SerializeField] float fizzleCooldown = 0.35f;     // short lockout after a spell fizzles, so it cannot be spammed
    [SerializeField] float faceAimAfterCast = 0.35f;   // how long after a cast the player keeps facing the aim (strafing), even for spells that do not restrict movement

    [Header("Feedback")]
    [SerializeField] bool showChargeBar = true;
    [SerializeField] bool showWeaponLabel = true;

    float[] cooldownEnd;
    float castingUntil;
    CastingCircle[] holdCircles;
    List<CastingCircle>[] tierCircles;     // the extra circles of a spell with charge tiers, one per tier reached
    int[] tierReached;
    bool[] latched;                 // a key that was let go while another key of the chord is still down: it keeps counting as held
    bool[] inCombo;          // this slot's key is part of the active combined cast
    bool[] ignoredPress;     // a key press that was refused (cannot combine); ignored until the key is released
    bool[] readyPlayed;      // the "ready" sound has played for this hold
    float[] lastTapCircle;
    float[] paid;            // mana already taken from the player while this slot is being cast (it is paid gradually)
    float[] effHeld;         // how long each hold has been held at full speed (multicasting slows it down)
    [Header("Multicasting")]
    [SerializeField] float multicastAngleStep = 42f;       // degrees between neighbouring casts on the ring around the player
    [SerializeField] float multicastRingRadius = 1.8f;     // how much further from the player the ring is than a single cast's circle
    [SerializeField] float multicastSideScale = 0.7f;      // size of the casts that are not at the front
    float facingUntil;
    CastMovement lastCastMovement;
    float[] cooldownTotal;
    Mana mana;
    Health health;
    float[] pressTime;
    bool[] charging;
    LineRenderer aimRing;
    Material aimRingMat;

    public WeaponData Weapon => weapon;

    // raised when a cast fails: "Fizzled", "Interrupted" or "Not enough mana" (the HUD shows it)
    public event System.Action<string> CastFailed;
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

    // While a cast key is held, and for a moment after any cast (whether or not the spell restricts movement), the player
    // should face where they are aiming: the locked target if there is one, otherwise where the camera points.
    // This is what lets them strafe while casting.
    public bool TryGetFacingDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!enabled || charging == null || mainCamera == null) return false;

        bool active = Time.time < facingUntil;
        for (int i = 0; i < charging.Length && !active; i++) active = charging[i];
        if (!active) return false;

        Vector3 dir = (lockOn != null && lockOn.IsLocked)
            ? lockOn.AimPoint - transform.position
            : mainCamera.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;

        direction = dir.normalized;
        return true;
    }

    public bool CanSwitchWeapon => loadout != null && loadout.Length > 1;

    public void Equip(WeaponData newWeapon)
    {
        DropAllCircles();
        weapon = newWeapon;
        AllocateSlots(Mathf.Max(CapacityFor(newWeapon), 1));       // a different weapon may have more or fewer spells
    }

    // 0..1 progress of the current hold toward the hold variant, 0 when not charging
    public float GetCharge(int slot)
    {
        var spell = GetSpell(slot);
        if (spell == null || !spell.hasHoldVariant || !charging[slot]) return 0f;
        return Mathf.Clamp01(effHeld[slot] / Mathf.Max(spell.holdThreshold, 0.01f));
    }

    void Awake()
    {
        mana = GetComponentInParent<Mana>();
        health = GetComponentInParent<Health>();

        // more spells than the first four keys: the extra slots get their own keys
        if (extraSkillKeys != null && extraSkillKeys.Length > 0)
        {
            var all = new List<InputAction>(skillActions);
            for (int i = 0; i < extraSkillKeys.Length; i++)
                all.Add(new InputAction("Skill" + (skillActions.Length + i + 1), InputActionType.Button, extraSkillKeys[i]));
            skillActions = all.ToArray();
        }

        if (useProfileLoadout)
        {
            var chosen = PlayerProfile.BuildWeapon();
            if (chosen != null) { weapon = chosen; loadout = null; }
        }
        AllocateSlots(CapacityFor(weapon));
    }

    // a weapon may have fewer or more spells than there are keys; spells past the last key have no key bound yet
    int CapacityFor(WeaponData w)
    {
        int n = skillActions.Length;
        if (w != null && w.spells != null) n = Mathf.Max(n, w.spells.Length);
        if (loadout != null)
            foreach (var l in loadout)
                if (l != null && l.spells != null) n = Mathf.Max(n, l.spells.Length);
        return n;
    }

    void AllocateSlots(int n)
    {
        cooldownEnd = new float[n];
        cooldownTotal = new float[n];
        pressTime = new float[n];
        effHeld = new float[n];
        paid = new float[n];
        charging = new bool[n];
        holdCircles = new CastingCircle[n];
        tierCircles = new List<CastingCircle>[n];
        tierReached = new int[n];
        latched = new bool[n];
        inCombo = new bool[n];
        ignoredPress = new bool[n];
        readyPlayed = new bool[n];
        lastTapCircle = new float[n];
    }

    InputAction ActionFor(int slot) => slot >= 0 && slot < skillActions.Length ? skillActions[slot] : null;

    // the keys the player chose in Options
    void ApplyKeyBindings()
    {
        for (int i = 0; i < skillActions.Length; i++) PlayerSettings.Apply("spell" + i, skillActions[i]);
        PlayerSettings.Apply("combine", combineAction);
        PlayerSettings.Apply("barrier", cancelAction);
    }

    void OnEnable()
    {
        PlayerSettings.Changed += ApplyKeyBindings;
        ApplyKeyBindings();
        foreach (var a in skillActions) a.Enable();
        combineAction.Enable();
        cancelAction.Enable();
        switchWeaponAction.Enable();
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        PlayerSettings.Changed -= ApplyKeyBindings;
        foreach (var a in skillActions) a.Disable();
        combineAction.Disable();
        cancelAction.Disable();
        queueing = false;
        queued.Clear();
        ClearPending();
        switchWeaponAction.Disable();
        if (health != null) health.Damaged -= OnDamaged;
        SetAimRing(false, Vector3.zero, 0f, Color.white);
        DropAllCircles();
    }

    void OnDestroy()
    {
        if (aimRingMat != null) Destroy(aimRingMat);
    }

    // ---------- read-only info for the HUD ----------

    public int SlotCount => cooldownEnd != null ? cooldownEnd.Length : skillActions.Length;
    public SpellData GetSpellAt(int slot) => GetSpell(slot);
    public bool IsCharging(int slot) => charging[slot];

    public float GetCooldown01(int slot)
    {
        float total = cooldownTotal[slot];
        return total <= 0f ? 0f : Mathf.Clamp01((cooldownEnd[slot] - Time.time) / total);
    }

    public string GetKeyLabel(int slot)
    {
        var a = ActionFor(slot);
        if (a == null) return "-";                       // past the last key: its binding is not decided yet
        return PlayerSettings.LabelOf(a);
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
            queueing = false;
            queued.Clear();

            for (int i = 0; i < charging.Length; i++) charging[i] = false;
            DropAllCircles();             // charging mana also removes a combined spell that is being cast
            entryInfos.Clear();           // and the cast bars above the player go with it
            SetAimRing(false, Vector3.zero, 0f, Color.white);
            return;
        }

        if (pendingSpec != null && Time.time >= pendingUntil) ClearPending();
        if (combineAction.WasPressedThisFrame())
        {
            if (cancelAction.IsPressed()) CancelCombine();          // F + B: the barrier keeps working, F is only read here
            else if (pendingSpec != null) StartPendingCast();
            else ToggleQueue();
        }
        if (queueing) UpdateQueue();          // the casts already going keep running while spells are queued

        bool aimingNow = false;
        float multicastFactor = MulticastFactor;      // casting several spells at once makes each one slower

        for (int i = 0; i < cooldownEnd.Length; i++)
        {
            var spell = GetSpell(i);
            if (spell == null) { charging[i] = false; DropCircle(i); continue; }

            var action = ActionFor(i);
            if (action == null) { charging[i] = false; DropCircle(i); continue; }      // no key bound to this slot yet

            // part of a combined cast: the combination runs this key
            if (inCombo[i]) continue;

            // queueing a combination: the keys pick spells for it, they do not cast (casts already going carry on)
            if (queueing && !charging[i]) continue;

            // a press that was refused (nothing to combine with) stays ignored until the key is let go
            if (ignoredPress[i])
            {
                if (!action.IsPressed()) ignoredPress[i] = false;
                continue;
            }

            bool hasHold = spell.hasHoldVariant;
            bool aimedTapOnly = !hasHold && spell.tap.NeedsAim;   // aim with the key held, release to cast

            if (hasHold || aimedTapOnly)
            {
                if (action.WasPressedThisFrame())
                {
                    if (Time.time >= cooldownEnd[i])
                    {
                        // hold spells can join the casts already being held (multicast)
                        if (hasHold) OnHoldPressed(i, spell);
                        else StartHold(i);

                        if (ignoredPress[i] || inCombo[i]) continue;
                    }
                }

                if (!charging[i]) { latched[i] = false; continue; }

                if (!inCombo[i]) effHeld[i] += Time.deltaTime / multicastFactor;
                float realHeld = Time.time - pressTime[i];                 // for the tap window
                float held = effHeld[i];                                   // for the circle and the cast time
                float castTime = Mathf.Max(spell.holdThreshold, 0.05f);
                var baseVariant = hasHold ? spell.hold : spell.tap;     // what the first circle is made for
                bool complete = held >= castTime;
                int tier = hasHold && complete ? spell.TierAt(held) : 0;
                var heldVariant = hasHold ? spell.VariantForTier(tier) : spell.tap;     // the variant this hold cast would fire as right now

                // after the tap window the player has committed to a hold cast: the circle starts drawing and finishes with the cast time
                if (held >= spell.tapWindow)
                {
                    // The mana is taken gradually while the spell is chanted and cast. If it runs out the chant and the circle break at once;
                    // mana already taken is lost, and what was paid is counted when the spell fires.
                    if (mana != null)
                    {
                        float baseCost = hasHold ? spell.hold.manaCost : spell.tap.manaCost;
                        bool basePhase = held < castTime;
                        float need = PayTarget(spell, held, hasHold) - paid[i];

                        if ((paid[i] <= 0f && !mana.CanAfford(baseCost)) || (basePhase && need > 0f && !mana.CanAfford(need)))
                        {
                            charging[i] = false;
                            latched[i] = false;
                            mana.NotifyInsufficient();
                            FizzleCast(i, "Not enough mana");
                            continue;
                        }
                        if (need > 0f) paid[i] += mana.Drain(need);       // past the cast time (charge tiers) it takes what is there
                    }

                    if (holdCircles[i] == null && spell.holdCircle != null)
                        holdCircles[i] = CastingCircle.Create(spell.holdCircle, baseVariant.effectColor, this, baseVariant, spell.circleScale);

                    if (holdCircles[i] != null)
                        holdCircles[i].SetProgress((held - spell.tapWindow) / Mathf.Max(castTime - spell.tapWindow, 0.05f));

                    // the chant above the head: one sentence at a time while the circle draws, more for the charge tiers
                    if (hasHold)
                    {
                        var chantLines = spell.AllChantLines();
                        if (chantLines.Length > 0) ChantDisplay.Show(i, chantLines, ChantReveal(spell, held), baseVariant.effectColor);
                    }

                    // charging past the cast time: each tier adds a bigger circle, and a chime and a small jolt mark the step up
                    if (hasHold && spell.TierCount > 0)
                    {
                        UpdateTierCircles(i, spell, held);
                        if (tier > tierReached[i])
                        {
                            tierReached[i] = tier;
                            SpellFX.Play(Sfx.Ready, muzzle.position, 1f);
                            CameraShake.Add(0.2f);
                        }
                    }

                    // aimed spells without a casting circle keep the plain ring
                    if (heldVariant.NeedsAim && spell.holdCircle == null)
                    {
                        aimingNow = true;
                        SetAimRing(true, GetAoeTarget(heldVariant, out _), heldVariant.aoeRadius, heldVariant.effectColor);
                    }
                }

                bool released = action.WasReleasedThisFrame();

                if (released)
                {
                    charging[i] = false;
                    latched[i] = false;

                    if (realHeld < spell.tapWindow)
                    {
                        DropCircle(i);
                        if (spell.tapDoesNothing) HoldHint(i, spell);        // this spell has to be charged: a tap only reminds the player
                        else Cast(i, spell.tap, spell);                      // a quick tap: instant
                    }
                    else if (hasHold && !complete)
                    {
                        FizzleCast(i, "Fizzled");                            // let go before the cast time: the spell fails, nothing is cast
                    }
                    else
                    {
                        RefundPaid(i);                                       // the cost is taken again in full as it fires
                        // the strongest tier the mana can pay for
                        var fire = heldVariant;
                        int firedTier = 0;
                        if (hasHold)
                        {
                            firedTier = tier;
                            while (firedTier > 0 && mana != null && !mana.CanAfford(spell.VariantForTier(firedTier).manaCost)) firedTier--;
                            fire = spell.VariantForTier(firedTier);
                        }

                        TakeCentre(i);                                       // released while other casts are going: it rotates to the centre as it fires
                        FinishHoldCast(i, Cast(i, fire, spell), firedTier);  // the circle was complete and the player let go: the spell fires
                    }
                }
                else if (complete && holdCircles[i] != null)
                {
                    holdCircles[i].Complete();                               // fully drawn: ready, waiting for the player to release
                    if (!readyPlayed[i])
                    {
                        readyPlayed[i] = true;
                        SpellFX.Play(Sfx.Ready, muzzle.position, 0.8f);
                    }
                }
            }
            else
            {
                bool keyDown = action.IsPressed();
                bool wants = spell.autoRepeatWhenHeld ? keyDown : action.WasPressedThisFrame();
                if (wants && Time.time >= cooldownEnd[i]) Cast(i, spell.tap, spell);

                // a rapid-fire spell keeps its circle up and spinning for as long as the key is held
                if (spell.autoRepeatWhenHeld && spell.holdCircle != null)
                {
                    if (action.WasPressedThisFrame()) pressTime[i] = Time.time;

                    if (keyDown)
                    {
                        if (holdCircles[i] == null)
                            holdCircles[i] = CastingCircle.Create(spell.holdCircle, spell.tap.effectColor, this, spell.tap, spell.circleScale);

                        var rapidChant = spell.AllChantLines();
                        if (rapidChant.Length > 0) ChantDisplay.Show(i, rapidChant, rapidChant.Length, spell.tap.effectColor);

                        float p = (Time.time - pressTime[i]) / Mathf.Max(spell.holdThreshold, 0.05f);
                        if (holdCircles[i] != null)
                        {
                            holdCircles[i].SetProgress(p);
                            if (p >= 1f) holdCircles[i].Complete();
                        }
                    }
                    else
                    {
                        DropCircle(i);
                    }
                }
            }
        }

        UpdateCombo();
        UpdateMulticastLayout();

        if (!aimingNow) SetAimRing(false, Vector3.zero, 0f, Color.white);
    }

    // how much of the spell's mana should have been paid after being held this long: the base cost over the cast time, then each charge tier's
    // extra cost over the time to reach it
    float PayTarget(SpellData spell, float held, bool hasHold)
    {
        float cost = hasHold ? spell.hold.manaCost : spell.tap.manaCost;
        float castTime = Mathf.Max(spell.holdThreshold, 0.05f);
        if (held < castTime) return cost * Mathf.Clamp01((held - spell.tapWindow) / Mathf.Max(castTime - spell.tapWindow, 0.05f));
        if (!hasHold) return cost;

        float from = castTime, prev = cost;
        for (int t = 0; t < spell.TierCount; t++)
        {
            float to = spell.extraTiers[t].time;
            float tierCost = spell.VariantForTier(t + 1).manaCost;
            if (held < to) return Mathf.Lerp(prev, tierCost, Mathf.Clamp01((held - from) / Mathf.Max(to - from, 0.05f)));
            from = to;
            prev = tierCost;
        }
        return prev;
    }

    void RefundPaid(int slot)
    {
        if (mana != null && paid[slot] > 0f) mana.Restore(paid[slot]);
        paid[slot] = 0f;
    }

    // how many sentences of the spell's chant are showing after being held this long: the spell's own sentences over its cast time, then
    // each charge tier's sentences while that tier is being charged
    int ChantReveal(SpellData spell, float held)
    {
        int count = 0;
        float castTime = Mathf.Max(spell.holdThreshold, 0.05f);

        int own = spell.chant != null ? spell.chant.Length : 0;
        if (own > 0)
            count += held >= castTime ? own : Mathf.CeilToInt(Mathf.Clamp01((held - spell.tapWindow) / Mathf.Max(castTime - spell.tapWindow, 0.05f)) * own);

        float start = castTime;
        for (int t = 0; t < spell.TierCount; t++)
        {
            float end = spell.extraTiers[t].time;
            int lines = spell.extraTiers[t].chant != null ? spell.extraTiers[t].chant.Length : 0;
            if (lines > 0 && held >= start)
                count += held >= end ? lines : Mathf.CeilToInt(Mathf.Clamp01((held - start) / Mathf.Max(end - start, 0.05f)) * lines);
            start = end;
        }
        return count;
    }

    // the extra circles of a charge-tier spell: each one draws between the previous tier's time and its own, on top of the earlier ones
    void UpdateTierCircles(int slot, SpellData spell, float held)
    {
        var tiers = spell.extraTiers;
        var list = tierCircles[slot] ?? (tierCircles[slot] = new List<CastingCircle>());

        float start = Mathf.Max(spell.holdThreshold, 0.05f);
        for (int t = 0; t < tiers.Length; t++)
        {
            float end = tiers[t].time;
            if (held >= start && tiers[t].circle != null)
            {
                while (list.Count <= t) list.Add(null);
                if (list[t] == null)
                {
                    var v = spell.VariantForTier(t + 1);
                    list[t] = CastingCircle.Create(tiers[t].circle, v.effectColor, this, v, spell.circleScale * tiers[t].circleScale);
                }

                if (list[t] != null)
                {
                    list[t].SetProgress((held - start) / Mathf.Max(end - start, 0.05f));
                    if (held >= end) list[t].Complete();
                }
            }
            start = end;
        }
    }

    void EachTierCircle(int slot, System.Action<CastingCircle> action)
    {
        var list = tierCircles != null ? tierCircles[slot] : null;
        if (list == null) return;
        foreach (var c in list) if (c != null) action(c);
        list.Clear();
    }

    // a quick tap on a spell that must be charged
    void HoldHint(int slot, SpellData spell)
    {
        CastFailed?.Invoke("Hold to charge");
        if (spell.tapCircle != null && Time.time - lastTapCircle[slot] > 0.3f)
        {
            lastTapCircle[slot] = Time.time;
            CastingCircle.PlayOneShot(spell.tapCircle, spell.hold.effectColor, this, spell.hold, spell.tapCircleScale);
        }
    }

    // ---------- combining spells (recipes) and multicasting ----------

    class ComboSpec
    {
        public string name;                // shown once the combination is ready
        public string preName;             // the ingredients, shown while it is being cast
        public float castTime, manaCost, cooldown;
        public SpellData result;
        public SpellVariant variant;
        public CastingCircleData circle;
        public float circleScale = 1f;
        public Color color = Color.white;
        public bool interruptible;
        public float interruptDamage = 1f;
        public CastMovement heldMovement;
        public float recovery;
    }

    class ComboState
    {
        public List<int> slots = new List<int>();
        public ComboSpec spec;
        public float startTime;
        public float effHeld;
        public bool readyPlayed;
        public float paid;                // mana taken so far while it is cast
        public bool heldByB;              // cast from the ready combined spell: B is held to cast it, like a normal hold spell
        public readonly List<CastingCircle> circles = new List<CastingCircle>();
    }

    ComboState combo;

    // read-only info for the HUD: the recipe box above the health bar
    // for the fusion paths shown above the spell bar
    public IReadOnlyList<SpellCombo> Combos => weapon != null && weapon.combos != null ? weapon.combos : (IReadOnlyList<SpellCombo>)System.Array.Empty<SpellCombo>();

    public bool IsHeld(int slot) => charging != null && slot >= 0 && slot < charging.Length && charging[slot] && !ignoredPress[slot];

    // the slot of the spell whose key was pressed last among those being held (-1 if none)
    public int NewestHeldSlot
    {
        get
        {
            int best = -1;
            float latest = -1f;
            for (int j = 0; charging != null && j < charging.Length; j++)
                if (IsHeld(j) && GetSpell(j) != null && pressTime[j] >= latest) { best = j; latest = pressTime[j]; }
            return best;
        }
    }

    public bool ComboActive => combo != null;

    // any spell (or the combined spell) is being chanted right now
    public bool IsChanting
    {
        get
        {
            if (combo != null) return true;
            for (int j = 0; charging != null && j < charging.Length; j++)
                if (charging[j] && !ignoredPress[j]) return true;
            return false;
        }
    }

    // a spell was cast: true for a hold (chanted) cast, false for a tap
    public event System.Action<bool> Casted;
    public float ComboCost => combo != null ? combo.spec.manaCost : 0f;
    public float ComboProgress => combo != null ? Mathf.Clamp01(combo.effHeld / Mathf.Max(combo.spec.castTime, 0.05f)) : 0f;
    public bool ComboReady => combo != null && ComboProgress >= 1f;
    public string ComboName => combo == null ? "" : (ComboReady ? combo.spec.name : combo.spec.preName);

    public string ComboKeys
    {
        get
        {
            if (combo == null) return "";
            var keys = new List<string>();
            foreach (int slot in combo.slots) keys.Add(GetKeyLabel(slot));
            return string.Join("+", keys);
        }
    }

    // one thing being cast (a single spell or a recipe combination), for the HUD's charging bars
    public struct CastEntryInfo
    {
        public string name;
        public float progress;
        public bool ready;
        public bool primary;      // the one at the centre of the aim
        public bool isCombo;
        public bool chanted;      // its name closes the chant above the head, so the bar does not repeat it
    }

    readonly List<CastEntryInfo> entryInfos = new List<CastEntryInfo>();
    public IReadOnlyList<CastEntryInfo> CastEntries => entryInfos;

    // Where each cast sits on the ring around the player: 0 is the front, 1 2 3 go round to the right, -1 -2 -3 to the left.
    // A single cast sits at the front. Once a second cast joins, nobody holds the front: the first moves to 1, the new one to -1,
    // the next to 2, -2, 3, -3 ... The front is only used by a cast that has just been released.
    readonly Dictionary<int, int> places = new Dictionary<int, int>();
    int leavingId = -1;                          // a released cast that is still shown (fading) at the front
    float leavingUntil;

    const int ComboId = 1000;

    // number of casts going on at once
    int EntryCount
    {
        get
        {
            int n = combo != null ? 1 : 0;
            for (int j = 0; j < charging.Length; j++)
                if (charging[j] && !inCombo[j] && !ignoredPress[j]) n++;
            return n;
        }
    }

    // every extra cast adds a fraction to every cast time
    float MulticastFactor => 1f + (weapon != null ? weapon.multicastDelay : 0.3f) * Mathf.Max(0, EntryCount - 1);

    void StartHold(int i)
    {
        charging[i] = true;
        pressTime[i] = Time.time;
        effHeld[i] = 0f;
        paid[i] = 0f;
        readyPlayed[i] = false;
        tierReached[i] = 0;
        latched[i] = false;
    }

    // ---------- combining with B: press B, press the spells to combine, press B again ----------

    readonly InputAction combineAction = new InputAction("Combine", InputActionType.Button, "<Keyboard>/b");
    readonly InputAction cancelAction = new InputAction("CancelCombine", InputActionType.Button, "<Keyboard>/f");      // the barrier key, held with B to cancel
    bool queueing;
    readonly List<int> queued = new List<int>();

    public bool Queueing => queueing;
    public IReadOnlyList<int> QueuedSlots => queued;
    public bool IsQueued(int slot) => queued.Contains(slot);

    // is this spell part of a combination right now: queued, in the ready combined spell, or in the one being cast
    public bool IsCombining(int slot) =>
        queued.Contains(slot) || (pendingSlots != null && pendingSlots.Contains(slot)) || (combo != null && combo.slots.Contains(slot));

    bool Equipped(SpellData spell)
    {
        for (int j = 0; j < cooldownEnd.Length; j++) if (GetSpell(j) == spell) return true;
        return false;
    }

    // a recipe that contains every spell in the set and is made only of equipped spells
    bool RecipeFits(SpellCombo recipe, List<SpellData> set, bool strictlyBigger)
    {
        if (recipe == null || recipe.result == null || recipe.ingredients == null) return false;
        foreach (var x in set) if (System.Array.IndexOf(recipe.ingredients, x) < 0) return false;
        if (strictlyBigger && recipe.ingredients.Length <= set.Count) return false;
        foreach (var ing in recipe.ingredients) if (!Equipped(ing)) return false;
        return true;
    }

    List<SpellData> QueuedSpells(int extraSlot = -1)
    {
        var set = new List<SpellData>();
        foreach (int q in queued) set.Add(GetSpell(q));
        if (extraSlot >= 0) set.Add(GetSpell(extraSlot));
        return set;
    }

    // can this spell join the queue? It must be ready and still lead to a fusion made only of spells that are equipped
    public bool CanQueue(int slot)
    {
        var sp = GetSpell(slot);
        if (sp == null || !sp.hasHoldVariant || charging[slot] || queued.Contains(slot) || Time.time < cooldownEnd[slot] || weapon == null || weapon.combos == null) return false;

        var set = QueuedSpells(slot);
        foreach (var recipe in weapon.combos) if (RecipeFits(recipe, set, false)) return true;
        return false;
    }

    // ---- the ready combined spell: it waits a while to be cast by holding B ----

    const float PendingTime = 15f;
    ComboSpec pendingSpec;
    List<int> pendingSlots;
    float pendingUntil;

    public bool HasPending => pendingSpec != null;
    public string PendingName => pendingSpec != null ? pendingSpec.name : "";
    public float PendingCost => pendingSpec != null ? pendingSpec.manaCost : 0f;
    public float PendingLeft01 => pendingSpec != null ? Mathf.Clamp01((pendingUntil - Time.time) / PendingTime) : 0f;
    public Color PendingColor => pendingSpec != null ? pendingSpec.color : Color.white;

    void ClearPending()
    {
        pendingSpec = null;
        pendingSlots = null;
    }

    void SetPending(List<int> set, ComboSpec spec)
    {
        pendingSpec = spec;
        pendingSlots = set;
        pendingUntil = Time.time + PendingTime;
        if (muzzle != null) SpellFX.Play(Sfx.Ready, muzzle.position, 0.8f);
    }

    // B pressed with a combined spell waiting: start casting it, and its time limit goes away
    void StartPendingCast()
    {
        if (combo != null) return;
        foreach (int j in pendingSlots)
            if (GetSpell(j) == null || charging[j]) return;

        var spec = pendingSpec;
        var set = pendingSlots;

        if (mana != null && !mana.CanAfford(spec.manaCost))
        {
            mana.NotifyInsufficient();
            CastFailed?.Invoke("Not enough mana");
            return;
        }

        // casts already going: the combined spell joins them (multicast) if everything allows it
        if (EntryCount > 0 && !CanMulticast(spec.result))
        {
            CastFailed?.Invoke("Can't cast together");
            return;
        }
        ClearPending();

        foreach (int j in set)
        {
            pressTime[j] = Time.time;
            effHeld[j] = 0f;
            readyPlayed[j] = false;
            tierReached[j] = 0;
            latched[j] = false;
        }
        FormCombo(set, spec);
        combo.heldByB = true;
    }

    void ToggleQueue()
    {
        if (!queueing)
        {
            if (combo != null) return;
            queueing = true;
            queued.Clear();
            if (muzzle != null) SpellFX.Play(Sfx.Ready, muzzle.position, 0.5f);
            return;
        }

        FinishQueue();
    }

    // F + B: drops the spells being queued and the ready combined spell (what is already being cast is not touched)
    void CancelCombine()
    {
        if (!queueing && pendingSpec == null) return;
        queueing = false;
        queued.Clear();
        ClearPending();
    }

    // B was pressed while queueing: an empty queue just closes it; spells that make a recipe become the combined spell; otherwise it stays
    // open so a wrong selection is not lost by accident (F + B cancels)
    void FinishQueue()
    {
        if (queued.Count == 0) { queueing = false; return; }

        var spec = queued.Count >= 2 ? BuildRecipeSpec(queued) : null;
        if (spec == null) { CastFailed?.Invoke("Can't combine"); return; }

        queueing = false;
        var set = new List<int>(queued);
        queued.Clear();
        SetPending(set, spec);
    }

    void UpdateQueue()
    {
        for (int i = 0; i < cooldownEnd.Length; i++)
        {
            var action = ActionFor(i);
            if (action == null || !action.WasPressedThisFrame()) continue;
            if (!CanQueue(i)) continue;

            queued.Add(i);
            if (muzzle != null) SpellFX.Sparks(muzzle.position, GetSpell(i).hold.effectColor);

            // a recipe with no bigger recipe beyond it completes by itself
            if (queued.Count >= 2 && BuildRecipeSpec(queued) != null)
            {
                bool more = false;
                var set = QueuedSpells();
                foreach (var recipe in weapon.combos) if (RecipeFits(recipe, set, true)) more = true;
                if (!more) { FinishQueue(); return; }
            }
        }
    }

    // a hold spell key was pressed: start it, or cast it alongside the others (multicast) if everything involved allows it
    void OnHoldPressed(int i, SpellData spell)
    {
        if (EntryCount == 0) { StartHold(i); return; }

        pressTime[i] = Time.time;
        if (CanMulticast(spell)) { StartHold(i); return; }

        // cannot cast together: the cast already going carries on, and this one does not cast
        ignoredPress[i] = true;
        CastFailed?.Invoke("Can't cast together");
    }

    bool CanMulticast(SpellData incoming)
    {
        if (weapon == null || !weapon.allowMulticast || !incoming.multicastable) return false;
        if (EntryCount >= weapon.maxMulticast) return false;

        for (int j = 0; j < charging.Length; j++)
        {
            if (!charging[j] || inCombo[j] || ignoredPress[j]) continue;
            var sp = GetSpell(j);
            if (sp != null && !sp.multicastable) return false;
        }
        if (combo != null && combo.spec.result != null && !combo.spec.result.multicastable) return false;      // the combined spell counts as one cast
        return true;
    }

    // a designed recipe for exactly these spells, otherwise nothing
    ComboSpec BuildRecipeSpec(List<int> slots)
    {
        if (weapon == null || weapon.combos == null) return null;

        var spells = new List<SpellData>();
        foreach (int slot in slots)
        {
            var sp = GetSpell(slot);
            if (sp == null || !sp.hasHoldVariant) return null;
            spells.Add(sp);
        }

        foreach (var recipe in weapon.combos)
            if (recipe != null && recipe.result != null && recipe.Matches(spells))
                return SpecFromRecipe(recipe, slots);
        return null;
    }

    ComboSpec SpecFromRecipe(SpellCombo recipe, List<int> slots)
    {
        var result = recipe.result;
        var variant = result.hasHoldVariant ? result.hold : result.tap;

        return new ComboSpec
        {
            name = result.displayName, preName = JoinNames(slots), result = result, variant = variant,
            castTime = result.hasHoldVariant ? result.holdThreshold : 0.8f,
            manaCost = variant.manaCost, cooldown = variant.cooldown,
            circle = result.holdCircle, circleScale = result.circleScale, color = variant.effectColor,
            interruptible = result.interruptible, interruptDamage = result.interruptDamage,
            heldMovement = result.heldMovement, recovery = result.heldRecovery,
        };
    }

    string JoinNames(List<int> slots)
    {
        var names = new List<string>();
        foreach (int slot in slots) names.Add(GetSpell(slot).displayName);
        return string.Join(" + ", names);
    }

    void FormCombo(List<int> set, ComboSpec spec)
    {
        float carried = 0f;
        if (combo == null)
        {
            combo = new ComboState();
            float earliest = float.MaxValue;
            foreach (int j in set) earliest = Mathf.Min(earliest, pressTime[j]);
            combo.startTime = earliest;
        }
        else carried = combo.effHeld;

        foreach (int j in set) carried = Mathf.Max(carried, effHeld[j]);       // time already spent holding counts
        combo.effHeld = carried;

        foreach (int j in set)
        {
            DropCircle(j);                        // the individual circles merge into the combined one
            inCombo[j] = true;
            charging[j] = true;
        }

        ClearComboCircles(0.2f);
        combo.slots = set;
        combo.spec = spec;
        combo.readyPlayed = false;

        if (spec.circle != null)
            combo.circles.Add(CastingCircle.Create(spec.circle, spec.color, this, spec.variant, spec.circleScale));

        SpellFX.Sparks(muzzle != null ? muzzle.position : transform.position, spec.color);
    }

    void UpdateCombo()
    {
        if (combo == null) return;

        combo.effHeld += Time.deltaTime / MulticastFactor;
        float p = ComboProgress;
        bool ready = p >= 1f;

        var comboChant = combo.spec.result != null ? combo.spec.result.AllChantLines() : null;
        if (comboChant != null && comboChant.Length > 0) ChantDisplay.Show(ComboId, comboChant, Mathf.CeilToInt(p * comboChant.Length), combo.spec.color);

        foreach (var c in combo.circles)
        {
            if (c == null) continue;
            c.SetProgress(p);
            if (ready) c.Complete();
        }

        if (ready && !combo.readyPlayed)
        {
            combo.readyPlayed = true;
            if (muzzle != null) SpellFX.Play(Sfx.Ready, muzzle.position, 0.9f);
        }

        // the mana is taken gradually while it is cast; if it runs out the chant and the circle break at once
        if (mana != null)
        {
            float need = combo.spec.manaCost * Mathf.Clamp01(p) - combo.paid;
            if ((combo.paid <= 0f && !mana.CanAfford(combo.spec.manaCost)) || (need > 0f && !mana.CanAfford(need)))
            {
                mana.NotifyInsufficient();
                FizzleCombo("Not enough mana");
                return;
            }
            if (need > 0f) combo.paid += mana.Drain(need);
        }

        // cast from the ready combined spell: hold B until it is ready, let go to fire it (too early and it fizzles)
        if (combo.heldByB && !combineAction.IsPressed())
        {
            if (ready) FireCombo();
            else FizzleCombo("Fizzled");
        }
    }

    void FireCombo()
    {
        var cb = combo;
        var spec = cb.spec;

        if (mana != null && cb.paid > 0f) { mana.Restore(cb.paid); cb.paid = 0f; }       // the cost is taken again in full as it fires
        bool ok = Cast(cb.slots[0], spec.variant, spec.result);       // the recipe's own cost is on its spell
        if (!ok) { FizzleCombo("Not enough mana"); return; }

        // the whole group shares one cooldown, and one movement / recovery rule
        foreach (int j in cb.slots)
        {
            cooldownEnd[j] = Time.time + spec.cooldown;
            cooldownTotal[j] = spec.cooldown;
            charging[j] = false;
            inCombo[j] = false;
        }
        castingUntil = Time.time + spec.recovery;
        facingUntil = Time.time + Mathf.Max(faceAimAfterCast, spec.recovery);
        lastCastMovement = spec.heldMovement;

        foreach (var c in cb.circles) if (c != null) c.FadeOut(0.4f);
        ChantDisplay.Finish(ComboId, spec.name);
        combo = null;
    }

    void FizzleCombo(string reason)
    {
        if (combo == null) return;

        foreach (var c in combo.circles) if (c != null) c.Fizzle();
        ChantDisplay.Fail(ComboId);
        CastFailed?.Invoke(reason);

        foreach (int j in combo.slots)
        {
            cooldownEnd[j] = Time.time + fizzleCooldown;
            cooldownTotal[j] = fizzleCooldown;
            charging[j] = false;
            inCombo[j] = false;
        }

        if (muzzle != null)
        {
            SpellFX.Play(Sfx.Fizzle, muzzle.position, 0.9f);
            SpellFX.Sparks(muzzle.position + transform.forward * 0.5f, new Color(1f, 0.4f, 0.3f));
        }
        combo = null;
    }

    void ClearComboCircles(float fade)
    {
        if (combo == null) return;
        foreach (var c in combo.circles) if (c != null) c.FadeOut(fade);
        combo.circles.Clear();
        ChantDisplay.Drop(ComboId);
    }

    // Several casts at once sit on a ring around the player (see places). Releasing a cast moves it to the front as it fires; when a cast
    // leaves, the others close the gap by moving toward the front, and a lone cast that is left goes back to the front.
    void UpdateMulticastLayout()
    {
        entryInfos.Clear();

        var active = new List<int>();
        for (int j = 0; j < charging.Length; j++)
            if (charging[j] && !inCombo[j] && !ignoredPress[j] && GetSpell(j) != null) active.Add(j);
        if (combo != null) active.Add(ComboId);

        // the fired cast is gone
        if (leavingId >= 0 && Time.time >= leavingUntil)
        {
            places.Remove(leavingId);
            leavingId = -1;
        }

        var gone = new List<int>();
        foreach (var kv in places) if (kv.Key != leavingId && !active.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (int id in gone) places.Remove(id);

        active.Sort((x, y) => StartOf(x).CompareTo(StartOf(y)));
        foreach (int id in active)
        {
            if (places.ContainsKey(id)) continue;

            // the second cast starts a multicast: the first leaves the front for place 1
            if (leavingId < 0 && places.Count == 1)
                foreach (var key in new List<int>(places.Keys)) places[key] = 1;

            places[id] = FreePlace();
        }

        if (places.Count == 0) { leavingId = -1; return; }
        CloseGaps();

        float radius = places.Count > 1 ? multicastRingRadius : 1f;

        var order = new List<KeyValuePair<int, int>>();       // (place, id), listed left to right for the bars
        foreach (var kv in places)
        {
            int id = kv.Key, place = kv.Value;
            float angle = places.Count == 1 ? 0f : place * multicastAngleStep;
            float scale = places.Count == 1 || id == leavingId ? 1f : multicastSideScale;

            if (id == ComboId)
            {
                if (combo != null) foreach (var c in combo.circles) if (c != null) c.SetPlacement(angle, radius, scale);
            }
            else if (holdCircles[id] != null)
            {
                holdCircles[id].SetPlacement(angle, radius, scale);
                var extra = tierCircles[id];
                if (extra != null) foreach (var c in extra) if (c != null) c.SetPlacement(angle, radius, scale);
            }

            if (id != leavingId) order.Add(new KeyValuePair<int, int>(place, id));
        }

        order.Sort((x, y) => x.Key.CompareTo(y.Key));
        foreach (var kv in order) entryInfos.Add(MakeInfo(kv.Value, places.Count == 1));
    }

    bool PlaceTaken(int place)
    {
        foreach (var kv in places) if (kv.Value == place) return true;
        return false;
    }

    // the first free place: 0 for the very first cast, then 1, -1, 2, -2, 3, -3 ...
    int FreePlace()
    {
        if (places.Count == 0) return 0;
        for (int k = 1; k < 64; k++)
        {
            int place = k % 2 == 1 ? (k + 1) / 2 : -(k / 2);
            if (!PlaceTaken(place)) return place;
        }
        return 0;
    }

    // everyone moves toward the front to fill a gap: the right side becomes 1 2 3 ..., the left side -1 -2 -3 ...
    // (the front itself is only for a cast being fired, or for a cast that is alone)
    void CloseGaps()
    {
        if (places.Count == 1)
        {
            foreach (var key in new List<int>(places.Keys)) places[key] = 0;
            return;
        }

        var right = new List<int>();
        var left = new List<int>();
        foreach (var kv in places)
        {
            if (kv.Key == leavingId) continue;
            if (kv.Value >= 0) right.Add(kv.Key);                  // a cast left at the front joins the right side
            else left.Add(kv.Key);
        }
        right.Sort((x, y) => places[x].CompareTo(places[y]));
        left.Sort((x, y) => places[y].CompareTo(places[x]));          // -1 first

        for (int i = 0; i < right.Count; i++) places[right[i]] = i + 1;
        for (int i = 0; i < left.Count; i++) places[left[i]] = -(i + 1);
        if (leavingId >= 0 && places.ContainsKey(leavingId)) places[leavingId] = 0;
    }

    // a released cast moves to the front and fires from there (its circle glides to the front while it fades)
    void TakeCentre(int slot)
    {
        if (leavingId >= 0 || !places.TryGetValue(slot, out int place) || place == 0 || places.Count <= 1) return;

        places[slot] = 0;
        if (holdCircles[slot] != null) holdCircles[slot].SetPlacement(0f, multicastRingRadius, 1f);
        if (tierCircles[slot] != null) foreach (var c in tierCircles[slot]) if (c != null) c.SetPlacement(0f, multicastRingRadius, 1f);
        leavingId = slot;
        leavingUntil = Time.time + 0.45f;
    }

    float StartOf(int id) => id == ComboId ? combo.startTime : pressTime[id];

    bool IsReady(int id)
    {
        if (id == ComboId) return ComboReady;
        var sp = GetSpell(id);
        return sp != null && effHeld[id] >= Mathf.Max(sp.holdThreshold, 0.05f);
    }

    CastEntryInfo MakeInfo(int id, bool primary)
    {
        if (id == ComboId)
            return new CastEntryInfo { name = combo.spec.name, progress = ComboProgress, ready = ComboReady, primary = primary, isCombo = true, chanted = combo.spec.result != null && combo.spec.result.chant != null && combo.spec.result.chant.Length > 0 };

        var sp = GetSpell(id);
        bool ready = IsReady(id);

        // a charge-tier spell: the name follows the tier reached, and the bar fills toward the next tier
        int tier = ready ? sp.TierAt(effHeld[id]) : 0;
        float progress = Mathf.Clamp01(effHeld[id] / Mathf.Max(sp.holdThreshold, 0.05f));
        if (ready && sp.TierCount > 0)
        {
            if (tier >= sp.TierCount) progress = 1f;
            else
            {
                float from = tier == 0 ? sp.holdThreshold : sp.extraTiers[tier - 1].time;
                progress = Mathf.Clamp01((effHeld[id] - from) / Mathf.Max(sp.extraTiers[tier].time - from, 0.05f));
            }
        }

        return new CastEntryInfo
        {
            name = ready ? sp.TierName(tier) : sp.displayName,
            progress = progress,
            ready = ready, primary = primary, isCombo = false,
            chanted = sp.chant != null && sp.chant.Length > 0,
        };
    }

    // ---------- casting circles ----------

    // getting hit while a strong cast is drawing breaks it
    void OnDamaged(float amount)
    {
        if (combo != null && combo.spec.interruptible && amount >= combo.spec.interruptDamage && Time.time - combo.startTime > 0.2f)
        {
            FizzleCombo("Interrupted");
            return;
        }

        for (int i = 0; i < charging.Length; i++)
        {
            if (!charging[i] || inCombo[i]) continue;
            var spell = GetSpell(i);
            if (spell == null || !spell.interruptible || amount < spell.interruptDamage) continue;
            if (Time.time - pressTime[i] < spell.tapWindow) continue;     // still just a tap, no circle yet

            charging[i] = false;
            FizzleCast(i, "Interrupted");
            cooldownEnd[i] += 0.3f;                                         // being broken costs a little extra time
        }
    }

    // the spell fired as tier `tier` (0 = the normal hold version). Its circles fade out as a finished cast; a higher tier's circle that was
    // still drawing (or that the mana could not pay for) falls back: it breaks instead, so it never looks like that bigger spell was cast.
    void FinishHoldCast(int slot, bool success, int tier = 0)
    {
        if (!success) { FizzleCast(slot, "Not enough mana"); return; }

        ChantDisplay.Finish(slot, GetSpell(slot) != null ? GetSpell(slot).TierName(tier) : null);
        if (holdCircles[slot] != null) holdCircles[slot].FadeOut(0.35f);
        holdCircles[slot] = null;

        var list = tierCircles[slot];
        if (list == null) return;
        for (int j = 0; j < list.Count; j++)
        {
            if (list[j] == null) continue;
            if (j < tier) list[j].FadeOut(0.35f);       // circle j belongs to tier j + 1
            else list[j].Fizzle();
        }
        list.Clear();
    }

    // the spell failed (released too early, or not enough mana): break the circle, no cast, short lockout
    void FizzleCast(int slot, string reason)
    {
        CastFailed?.Invoke(reason);

        ChantDisplay.Fail(slot);
        if (holdCircles[slot] != null) holdCircles[slot].Fizzle();
        holdCircles[slot] = null;
        EachTierCircle(slot, c => c.Fizzle());

        cooldownEnd[slot] = Time.time + fizzleCooldown;
        cooldownTotal[slot] = fizzleCooldown;
        if (muzzle != null)
        {
            SpellFX.Play(Sfx.Fizzle, muzzle.position, 0.9f);
            SpellFX.Sparks(muzzle.position + transform.forward * 0.5f, new Color(1f, 0.4f, 0.3f));
        }
    }

    void DropCircle(int slot)
    {
        if (holdCircles == null) return;
        ChantDisplay.Drop(slot);
        EachTierCircle(slot, c => c.FadeOut(0.2f));
        if (holdCircles[slot] == null) return;
        holdCircles[slot].FadeOut(0.2f);
        holdCircles[slot] = null;
    }

    void DropAllCircles()
    {
        if (holdCircles == null) return;
        if (latched != null) System.Array.Clear(latched, 0, latched.Length);
        for (int i = 0; i < holdCircles.Length; i++) DropCircle(i);

        if (combo != null)
        {
            ClearComboCircles(0.2f);
            foreach (int j in combo.slots) { inCombo[j] = false; charging[j] = false; }
            combo = null;
        }
    }

    // per-frame cache so many circle layers do not each repeat the same aiming raycasts
    readonly Pose[] poseCache = new Pose[3];
    readonly int[] poseFrame = { -1, -1, -1 };
    readonly SpellVariant[] poseVariant = new SpellVariant[3];
    readonly float[] poseDistance = new float[3];

    // where a circle layer's anchor is right now
    public Pose GetCirclePose(CircleAnchor anchor, CastingCircleData data, SpellVariant variant)
    {
        int k = (int)anchor;
        if (poseFrame[k] == Time.frameCount && poseVariant[k] == variant && Mathf.Approximately(poseDistance[k], data.frontDistance))
            return poseCache[k];

        Pose pose = ComputeCirclePose(anchor, data, variant);
        poseCache[k] = pose;
        poseFrame[k] = Time.frameCount;
        poseVariant[k] = variant;
        poseDistance[k] = data.frontDistance;
        return pose;
    }

    Pose ComputeCirclePose(CircleAnchor anchor, CastingCircleData data, SpellVariant variant)
    {
        if (muzzle == null || mainCamera == null) return new Pose(transform.position, Quaternion.identity);

        switch (anchor)
        {
            case CircleAnchor.Front:
            {
                Vector3 aim = GetAimDirection();
                return new Pose(muzzle.position + aim * data.frontDistance, Quaternion.LookRotation(aim));
            }
            case CircleAnchor.Feet:
            {
                Vector3 p = transform.root.position;
                if (RaycastSkippingOwnBarriers(p + Vector3.up, Vector3.down, 3f, groundMask, out RaycastHit hit)) p = hit.point;
                return new Pose(p + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
            }
            default:   // AimPoint
            {
                bool inAir = false;
                Vector3 p = (variant != null && variant.NeedsAim)
                    ? GetAoeTarget(variant, out inAir)
                    : ((lockOn != null && lockOn.IsLocked) ? lockOn.AimPoint : GetCursorAimPoint());

                if (inAir) return new Pose(p, Quaternion.LookRotation(mainCamera.transform.forward));   // target is in the air: stand the circle up facing the camera
                return new Pose(p + Vector3.up * 0.06f, Quaternion.Euler(90f, 0f, 0f));
            }
        }
    }

    bool Cast(int slot, SpellVariant v, SpellData spell)
    {
        if (Time.time < cooldownEnd[slot] || muzzle == null || mainCamera == null) return false;

        if (mana != null && !mana.CanAfford(v.manaCost))
        {
            mana.NotifyInsufficient();
            return false;
        }

        bool cast = false;
        switch (v.kind)
        {
            case SpellKind.Projectile: cast = CastProjectile(v, spell); break;
            case SpellKind.Laser: cast = CastLaser(v, spell); break;
            case SpellKind.GroundAoE:
                AoeStrike.Spawn(GetAoeTarget(v, out bool inAir), v, damageTag, inAir, spell.name);
                cast = true;
                break;
            case SpellKind.Barrier: cast = CastBarrier(v); break;
        }

        if (!cast) return false;

        cooldownEnd[slot] = Time.time + v.cooldown;
        bool wasHold = spell.hasHoldVariant && ReferenceEquals(v, spell.hold);
        Casted?.Invoke(wasHold);
        castingUntil = Time.time + (wasHold ? spell.heldRecovery : spell.tapRecovery);
        facingUntil = Time.time + Mathf.Max(faceAimAfterCast, wasHold ? spell.heldRecovery : spell.tapRecovery);
        lastCastMovement = wasHold ? spell.heldMovement : spell.tapMovement;
        cooldownTotal[slot] = v.cooldown;
        if (mana != null) mana.Spend(v.manaCost);

        Sfx sound = v.kind == SpellKind.Laser ? Sfx.Laser : v.kind == SpellKind.Barrier ? Sfx.BarrierPlace : Sfx.Cast;
        if (sound == Sfx.BarrierPlace || !SpellSounds.Cast(spell.name, muzzle.position)) SpellFX.Play(sound, muzzle.position, 0.8f);
        SpellFX.CastFlash(LaunchPosition, v.effectColor);

        // a tapped (instant) spell gets a small quick circle; limited in rate so rapid-fire spells do not flood the screen
        if (spell.tapCircle != null && ReferenceEquals(v, spell.tap) && Time.time - lastTapCircle[slot] > 0.3f)
        {
            lastTapCircle[slot] = Time.time;
            CastingCircle.PlayOneShot(spell.tapCircle, v.effectColor, this, v, spell.tapCircleScale);
        }
        return true;
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

    // where a spell would go right now: toward the locked target, or the cursor / crosshair
    public Vector3 AimDirection => muzzle != null && mainCamera != null ? GetAimDirection() : transform.forward;
    public Vector3 AimOrigin => muzzle != null ? muzzle.position : transform.position;

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

    Vector3 LaunchPosition => launchPoint != null ? launchPoint.position : muzzle.position;

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

            var p = Instantiate(prefab, LaunchPosition, Quaternion.LookRotation(shotDir));
            p.effectColor = v.effectColor;
            p.fxKey = spell.name;
            ProjectileModels.Attach(p.gameObject, spell.name, v.effectColor);
            p.Init(v.damage, v.projectileSpeed, v.lifetime, v.projectileScale, v.explosionRadius,
                v.pierceCount, v.explodeOnPierce, v.breaksBarriers, v.status);
        }
        return true;
    }

    // instant hit along the aim line
    bool CastLaser(SpellVariant v, SpellData spell)
    {
        Vector3 origin = LaunchPosition;
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
        if (!SpellFX.UseMagicFX || !SpellImpact.Play(spell.name, end, -dir, 1.1f, v.effectColor)) SpellFX.Sparks(end, v.effectColor);
        SpellSounds.Impact(spell.name, end, 0f, 0.6f);
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
