using UnityEngine;
using UnityEngine.UI;

// Drives the player HUD: spell bar with cooldowns, broom indicator, mana bar, health bar, damage vignette,
// wave counter, weapon label and the hold-charge bar.
//
// The elements are real GameObjects under the GUIs canvas (made once by Tools > Magic > Build HUD). Position, size,
// colour, sprite and font are all whatever you set on those objects in the editor; this script only fills in the
// text, fill amounts and state highlights, and never moves or resizes anything except (optionally) the spell slots.
public class PlayerHUD : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Image border, bg, cooldown;
        public Text nameText, keyText, costText;
    }

    public enum SlotLayout
    {
        KeepPlaced,     // slots stay exactly where you put them; empty ones are just hidden
        CenterActive,   // slots in use are spread around the centre of where you placed them, using your spacing
    }

    [Header("Spell slots")]
    [SerializeField] SlotLayout slotLayout = SlotLayout.CenterActive;

    [Header("State colours")]
    [SerializeField] Color chargingColor = new Color(0.4f, 0.85f, 1f);      // border while charging a hold spell / flying
    [SerializeField] Color readyColor = new Color(1f, 0.8f, 0.2f);          // hold spell fully charged
    [SerializeField] Color warningColor = new Color(0.8f, 0.25f, 0.2f);
    [SerializeField] Color manaChargeColor = new Color(0.6f, 0.9f, 1f);     // mana bar pulse while charging mana

    [Header("Broom")]
    [SerializeField, Range(0f, 1f)] float broomLowBelow = 0.25f;     // while flying under this much stamina the broom box pounds
    [SerializeField] float broomPoundStrength = 0.06f;               // how much bigger it swells at the top of each beat

    [Header("Mana")]
    [SerializeField, Range(0f, 1f)] float chargeHintBelow = 0.5f;   // under this fraction of mana the bar reminds you which key charges it

    [Header("Tinting (turn off if your own art should show untouched)")]
    [SerializeField] bool shiftHealthColorWhenLow = true;                   // health fill blends from its colour to red as health drops
    [SerializeField] Color lowHealthColor = new Color(0.95f, 0.25f, 0.2f);

    [Header("Scene references (filled in by Tools > Magic > Build HUD)")]
    [SerializeField] Slot[] slots;
    [SerializeField] Slot broomBox;
    [SerializeField] Slot lockBox;                     // lock-on indicator (optional)
    [SerializeField] Image manaBg, manaFill;
    [SerializeField] Text manaText;
    [SerializeField] Image healthBg, healthChip, healthFill;
    [SerializeField] Text healthText;
    [SerializeField] Text waveText, weaponLabel;
    [SerializeField] Image vignette;
    [SerializeField] Image chargeBack, chargeFill;     // hold-charge bar under the crosshair

    PlayerSpellCaster caster;
    Mana mana;
    Health health;
    WaveSpawner waves;
    BroomFlight broom;
    TargetLockOn lockOn;
    Color lockBorder;

    // what you set in the editor, remembered as the "normal" look
    Vector2[] placedPositions;
    Color[] baseBg, baseBorder;
    Vector3 broomScale;
    float broomBeatPhase;   // advances every frame, so a changing beat rate speeds up smoothly instead of jumping
    Color broomBorder, manaBgBase, manaFillBase, healthFillBase, chargeFillBase;
    Vector3 healthBgScale;

    float vignetteAlpha;
    float manaFlash;
    float healthShown = 1f, healthChipValue = 1f;

    void Start()
    {
        if (slots == null || slots.Length == 0 || manaFill == null || healthFill == null)
        {
            Debug.LogWarning("PlayerHUD has no UI references. Run Tools > Magic > Build HUD.", this);
            enabled = false;
            return;
        }

        caster = FindAnyObjectByType<PlayerSpellCaster>();
        if (caster != null) mana = caster.GetComponentInParent<Mana>();
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            health = playerGo.GetComponentInParent<Health>();
            broom = playerGo.GetComponentInParent<BroomFlight>();
        }
        waves = FindAnyObjectByType<WaveSpawner>();
        lockOn = FindAnyObjectByType<TargetLockOn>();

        RememberEditorLook();

        if (broomBox != null && broom != null) broomBox.keyText.text = broom.MountKeyLabel;
        if (lockBox != null && lockOn != null) lockBox.keyText.text = lockOn.LockKeyLabel;

        if (health != null) health.Damaged += OnPlayerDamaged;
        if (mana != null) mana.Insufficient += OnInsufficient;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnPlayerDamaged;
        if (mana != null) mana.Insufficient -= OnInsufficient;
    }

    // everything the HUD changes at runtime goes back to what you set in the editor
    void RememberEditorLook()
    {
        placedPositions = new Vector2[slots.Length];
        baseBg = new Color[slots.Length];
        baseBorder = new Color[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            placedPositions[i] = slots[i].border.rectTransform.anchoredPosition;
            baseBg[i] = slots[i].bg.color;
            baseBorder[i] = slots[i].border.color;
        }

        if (broomBox != null) { broomBorder = broomBox.border.color; broomScale = broomBox.border.rectTransform.localScale; }
        if (lockBox != null) lockBorder = lockBox.border.color;
        manaBgBase = manaBg.color;
        manaFillBase = manaFill.color;
        healthFillBase = healthFill.color;
        healthBgScale = healthBg.rectTransform.localScale;
        if (chargeFill != null) chargeFillBase = chargeFill.color;
    }

    void OnPlayerDamaged(float amount) => vignetteAlpha = Mathf.Clamp(0.25f + amount * 0.01f, 0.25f, 0.6f);
    void OnInsufficient() => manaFlash = 1f;

    void Update()
    {
        ArrangeSlots();
        UpdateSpellBar();
        UpdateIndicators();
        UpdateMana();
        UpdateHealth();
        UpdateVignette();
        UpdateLabels();
    }

    // hides slots with no spell; optionally spreads the rest around the middle of where you placed them
    void ArrangeSlots()
    {
        if (caster == null) return;

        int used = 0;
        for (int i = 0; i < slots.Length; i++)
            if (caster.GetSpellAt(i) != null) used++;

        // spacing and centre come from the positions you placed the slots at
        float centerX = 0f, step = 0f;
        for (int i = 0; i < slots.Length; i++) centerX += placedPositions[i].x;
        centerX /= slots.Length;
        if (slots.Length > 1) step = (placedPositions[slots.Length - 1].x - placedPositions[0].x) / (slots.Length - 1);

        int k = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = caster.GetSpellAt(i) != null;
            slots[i].border.gameObject.SetActive(has);
            if (!has) continue;

            if (slotLayout == SlotLayout.CenterActive && slots.Length > 1)
            {
                float x = centerX + (k - (used - 1) * 0.5f) * step;
                slots[i].border.rectTransform.anchoredPosition = new Vector2(x, placedPositions[i].y);
            }
            else
            {
                slots[i].border.rectTransform.anchoredPosition = placedPositions[i];
            }
            k++;
        }
    }

    void UpdateSpellBar()
    {
        if (caster == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            var spell = caster.GetSpellAt(i);
            if (spell == null) continue;   // hidden by ArrangeSlots

            s.nameText.text = spell.displayName;
            s.keyText.text = caster.GetKeyLabel(i);
            s.costText.text = spell.hasHoldVariant
                ? Mathf.RoundToInt(spell.tap.manaCost) + " / " + Mathf.RoundToInt(spell.hold.manaCost)
                : Mathf.RoundToInt(spell.tap.manaCost).ToString();

            s.cooldown.fillAmount = caster.GetCooldown01(i);

            // can't afford: a red cast over whatever the background looks like
            bool affordable = mana == null || mana.CanAfford(spell.tap.manaCost);
            s.bg.color = affordable ? baseBg[i] : baseBg[i] * new Color(1f, 0.45f, 0.45f, 1f);

            // border: its normal colour, blue while charging a hold spell, gold once the hold version is ready
            Color border = baseBorder[i];
            if (caster.IsCharging(i))
                border = spell.hasHoldVariant && caster.GetCharge(i) >= 1f ? readyColor : chargingColor;
            s.border.color = border;
        }
    }

    void UpdateIndicators()
    {
        UpdateLockIndicator();
        if (broomBox == null || broom == null) return;

        bool flying = broom.IsFlying;
        float st = broom.Stamina01;

        // broom: fill = stamina, border shows the state
        broomBox.cooldown.color = Color.Lerp(lowHealthColor, new Color(0.3f, 0.85f, 0.9f), Mathf.Clamp01(st / 0.35f)) * new Color(1f, 1f, 1f, 0.55f);
        broomBox.cooldown.fillAmount = st;
        broomBox.costText.text = flying ? Mathf.CeilToInt(st * 100f) + "%" : (broom.CanMount ? "READY" : "LOW");
        broomBox.costText.color = flying ? Color.white : (broom.CanMount ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.5f, 0.45f));
        broomBox.border.color = flying ? chargingColor : (broom.IsExhausted || !broom.CanMount ? warningColor : broomBorder);

        // low on stamina while flying: the box pounds like a heartbeat, faster the closer it gets to empty
        float pound = 1f;
        if (flying && st < broomLowBelow)
        {
            float urgency = 1f - Mathf.Clamp01(st / Mathf.Max(broomLowBelow, 0.01f));      // 0 at the threshold, 1 when empty
            float rate = Mathf.Lerp(5f, 9f, urgency);
            broomBeatPhase += rate * Time.unscaledDeltaTime;
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(broomBeatPhase)), 3f);              // sharp thump, then rest
            pound = 1f + broomPoundStrength * beat;
            broomBox.border.color = Color.Lerp(warningColor, Color.white, beat * 0.5f);
        }
        broomBox.border.rectTransform.localScale = Vector3.Lerp(broomBox.border.rectTransform.localScale, broomScale * pound, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
    }

    // lock-on: "NONE" while nothing is locked, the target's name while locked
    void UpdateLockIndicator()
    {
        if (lockBox == null || lockOn == null) return;

        if (lockOn.IsLocked)
        {
            lockBox.costText.text = lockOn.CurrentTarget.name.Replace("(Clone)", "").Trim();
            lockBox.costText.color = Color.white;
            lockBox.border.color = readyColor;
        }
        else
        {
            lockBox.costText.text = "NONE";
            lockBox.costText.color = new Color(0.7f, 0.7f, 0.7f);
            lockBox.border.color = lockBorder;
        }
    }

    void UpdateMana()
    {
        if (mana == null) return;

        manaFlash = Mathf.MoveTowards(manaFlash, 0f, Time.unscaledDeltaTime * 3f);

        manaFill.fillAmount = Mathf.Lerp(manaFill.fillAmount, mana.Current / mana.Max, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        manaFill.color = mana.IsCharging
            ? Color.Lerp(manaFillBase, manaChargeColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f))
            : manaFillBase;
        manaBg.color = Color.Lerp(manaBgBase, manaBgBase * new Color(1f, 0.4f, 0.4f, 1f), manaFlash);

        string amount = Mathf.CeilToInt(mana.Current) + " / " + Mathf.CeilToInt(mana.Max);
        if (mana.IsCharging) manaText.text = "CHARGING  " + amount;
        else if (mana.Current < mana.Max * chargeHintBelow) manaText.text = amount + "    Hold [" + mana.ChargeKeyLabel + "] to charge";
        else manaText.text = amount;
    }

    void UpdateHealth()
    {
        if (health == null) return;

        float target = Mathf.Clamp01(health.Current / health.Max);
        healthShown = Mathf.Lerp(healthShown, target, 1f - Mathf.Exp(-HealthBarStyle.FillSmoothing * Time.unscaledDeltaTime));
        // the pale chip waits behind the fill, then drains down; it jumps up instantly when healing
        healthChipValue = target > healthChipValue ? target : Mathf.MoveTowards(healthChipValue, target, HealthBarStyle.ChipDrainPerSecond * Time.unscaledDeltaTime);

        healthFill.fillAmount = healthShown;
        healthChip.fillAmount = Mathf.Max(healthChipValue, healthShown);
        healthFill.color = shiftHealthColorWhenLow
            ? Color.Lerp(lowHealthColor, healthFillBase, Mathf.Clamp01(healthShown / 0.5f))
            : healthFillBase;
        healthText.text = Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Max);

        // the bar pulses when health is low
        float pulse = target < HealthBarStyle.LowThreshold && !health.IsDead ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
        healthBg.rectTransform.localScale = Vector3.Lerp(healthBg.rectTransform.localScale, healthBgScale * pulse, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
    }

    void UpdateVignette()
    {
        if (vignette == null) return;

        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 1.2f);

        // faint permanent edge while low on health
        float low = 0f;
        if (health != null && !health.IsDead && health.Current / health.Max < HealthBarStyle.LowThreshold)
            low = 0.12f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);

        var c = vignette.color;
        c.a = Mathf.Max(vignetteAlpha, low);
        vignette.color = c;
    }

    // wave counter, weapon name, and the hold-charge bar under the crosshair
    void UpdateLabels()
    {
        if (waveText != null) waveText.text = waves != null ? waves.StatusText : "";

        if (weaponLabel != null && caster != null && caster.Weapon != null)
            weaponLabel.text = "Weapon: " + caster.Weapon.displayName + (caster.CanSwitchWeapon ? "  (Tab to switch)" : "");

        if (chargeBack == null || chargeFill == null || caster == null) return;

        int charging = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            var spell = caster.GetSpellAt(i);
            if (spell != null && spell.hasHoldVariant && caster.IsCharging(i)) { charging = i; break; }
        }

        chargeBack.gameObject.SetActive(charging >= 0);
        if (charging < 0) return;

        float charge = caster.GetCharge(charging);
        chargeFill.fillAmount = charge;
        chargeFill.color = charge >= 1f ? readyColor : chargeFillBase;
    }
}
