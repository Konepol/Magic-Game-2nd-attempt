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
    [SerializeField] Slot comboBox;                    // combined spell, hidden until two or more spells are combined (optional)
    [SerializeField] Image manaBg, manaFill;
    [SerializeField] Text manaText;
    [SerializeField] Image healthBg, healthChip, healthFill;
    [SerializeField] Text healthText;
    [SerializeField] Text waveText, weaponLabel;
    [SerializeField] Image vignette;
    [SerializeField] Image chargeBack, chargeFill;     // hold-charge bar under the crosshair
    [SerializeField] Text castLabel;                   // why a cast failed (briefly)
    [Header("Charging bars: one per spell being cast, side by side (index 0 is the first bar)")]
    [SerializeField] Image[] castBarBack;
    [SerializeField] Image[] castBarFill;
    [SerializeField] Text[] castBarLabel;
    [SerializeField] float castBarSpacing = 190f;      // distance between side-by-side bars

    PlayerSpellCaster caster;
    Mana mana;
    Health health;
    WaveSpawner waves;
    BroomFlight broom;
    TargetLockOn lockOn;
    Color lockBorder;
    Color comboBg;

    // what you set in the editor, remembered as the "normal" look
    Vector2[] placedPositions;
    Color[] baseBg, baseBorder;
    Vector3 broomScale;
    float broomBeatPhase;   // advances every frame, so a changing beat rate speeds up smoothly instead of jumping
    Color broomBorder, manaBgBase, manaFillBase, healthFillBase, chargeFillBase;
    Vector3 healthBgScale;

    string castFailText;
    Vector2 broomPlaced, lockPlaced;                    // where the broom and lock boxes were placed; they step outward when the row gets wider
    int baseSlotCount;                                  // slots that were placed in the editor; extra ones are made for weapons with more spells
    Vector2 castBarBasePos;
    Vector3 castBarScale = Vector3.one;
    float castFailUntil;
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

        baseSlotCount = slots.Length;
        RememberEditorLook();
        ExtendSlots(caster != null ? caster.SlotCount : slots.Length);

        // combined spell names can be long ("Fusion: A + B + C"), so that text shrinks to fit its box
        if (comboBox != null)
        {
            comboBox.nameText.resizeTextForBestFit = true;
            comboBox.nameText.resizeTextMinSize = 8;
            comboBox.nameText.resizeTextMaxSize = 15;
        }

        if (broomBox != null && broom != null) broomBox.keyText.text = broom.MountKeyLabel;
        if (lockBox != null && lockOn != null) lockBox.keyText.text = lockOn.LockKeyLabel;

        if (health != null) health.Damaged += OnPlayerDamaged;
        if (caster != null) caster.CastFailed += OnCastFailed;
        if (mana != null) mana.Insufficient += OnInsufficient;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnPlayerDamaged;
        if (caster != null) caster.CastFailed -= OnCastFailed;
        if (mana != null) mana.Insufficient -= OnInsufficient;
    }

    // a weapon may have more spells than the slots placed in the editor: extra slots are copies of the last one,
    // continuing the row with the same spacing (this also runs whenever a different weapon needs more slots)
    void ExtendSlots(int needed)
    {
        if (needed <= slots.Length) return;

        int old = slots.Length;
        var list = new System.Collections.Generic.List<Slot>(slots);
        var template = slots[baseSlotCount - 1];
        float step = baseSlotCount > 1 ? (placedPositions[baseSlotCount - 1].x - placedPositions[0].x) / (baseSlotCount - 1) : 110f;

        System.Array.Resize(ref placedPositions, needed);
        System.Array.Resize(ref baseBg, needed);
        System.Array.Resize(ref baseBorder, needed);

        for (int k = old; k < needed; k++)
        {
            var go = Instantiate(template.border.gameObject, template.border.transform.parent);
            go.name = "Slot" + (k + 1);
            var bg = go.transform.Find("Background");
            list.Add(new Slot
            {
                border = go.GetComponent<Image>(),
                bg = bg.GetComponent<Image>(),
                cooldown = bg.Find("CooldownFill").GetComponent<Image>(),
                nameText = bg.Find("Name").GetComponent<Text>(),
                keyText = bg.Find("Key").GetComponent<Text>(),
                costText = bg.Find("Cost").GetComponent<Text>(),
            });

            placedPositions[k] = new Vector2(placedPositions[baseSlotCount - 1].x + step * (k - baseSlotCount + 1), placedPositions[baseSlotCount - 1].y);
            baseBg[k] = baseBg[baseSlotCount - 1];
            baseBorder[k] = baseBorder[baseSlotCount - 1];
        }
        slots = list.ToArray();
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

        // the charging bars: remember where the first one is, so several can be spread around it
        if (castBarBack == null || castBarBack.Length == 0)
        {
            if (chargeBack != null && chargeFill != null) { castBarBack = new[] { chargeBack }; castBarFill = new[] { chargeFill }; castBarLabel = new Text[1]; }
        }
        if (castBarBack != null && castBarBack.Length > 0 && castBarBack[0] != null)
        {
            castBarBasePos = castBarBack[0].rectTransform.anchoredPosition;
            castBarScale = castBarBack[0].rectTransform.localScale;
            if (castBarFill[0] != null) chargeFillBase = castBarFill[0].color;
        }

        if (broomBox != null) { broomBorder = broomBox.border.color; broomScale = broomBox.border.rectTransform.localScale; broomPlaced = broomBox.border.rectTransform.anchoredPosition; }
        if (lockBox != null) { lockBorder = lockBox.border.color; lockPlaced = lockBox.border.rectTransform.anchoredPosition; }
        if (comboBox != null) { comboBg = comboBox.bg.color; comboBox.border.gameObject.SetActive(false); }
        manaBgBase = manaBg.color;
        manaFillBase = manaFill.color;
        healthFillBase = healthFill.color;
        healthBgScale = healthBg.rectTransform.localScale;
        if (chargeFill != null) chargeFillBase = chargeFill.color;
    }

    void OnPlayerDamaged(float amount) => vignetteAlpha = Mathf.Clamp(0.25f + amount * 0.01f, 0.25f, 0.6f);
    void OnInsufficient() => manaFlash = 1f;

    void OnCastFailed(string reason)
    {
        castFailText = reason;
        castFailUntil = Time.unscaledTime + 0.9f;
    }

    void Update()
    {
        ArrangeSlots();
        UpdateSpellBar();
        UpdateIndicators();
        UpdateCombo();
        UpdateMana();
        UpdateHealth();
        UpdateVignette();
        UpdateLabels();
    }

    // hides slots with no spell; optionally spreads the rest around the middle of where you placed them
    void ArrangeSlots()
    {
        if (caster == null) return;
        if (caster.SlotCount > slots.Length) ExtendSlots(caster.SlotCount);

        int used = 0;
        for (int i = 0; i < slots.Length; i++)
            if (caster.GetSpellAt(i) != null) used++;

        // spacing and centre come from the positions you placed the slots at
        int baseCount = Mathf.Max(baseSlotCount, 1);
        float centerX = 0f, step = 0f;
        for (int i = 0; i < baseCount; i++) centerX += placedPositions[i].x;
        centerX /= baseCount;
        if (baseCount > 1) step = (placedPositions[baseCount - 1].x - placedPositions[0].x) / (baseCount - 1);

        // a row wider than the one placed in the editor pushes the broom and lock boxes outward so they never overlap the slots
        float extra = Mathf.Max(0, used - baseCount) * Mathf.Abs(step) * 0.5f;
        if (broomBox != null) broomBox.border.rectTransform.anchoredPosition = new Vector2(broomPlaced.x - extra, broomPlaced.y);
        if (lockBox != null) lockBox.border.rectTransform.anchoredPosition = new Vector2(lockPlaced.x + extra, lockPlaced.y);

        int k = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            bool has = caster.GetSpellAt(i) != null;
            slots[i].border.gameObject.SetActive(has);
            if (!has) continue;

            if (slotLayout == SlotLayout.CenterActive && baseCount > 1)
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

    // the combined spell's box: only visible while two or more spells are being combined
    void UpdateCombo()
    {
        if (comboBox == null || caster == null) return;

        bool active = caster.ComboActive;
        if (comboBox.border.gameObject.activeSelf != active) comboBox.border.gameObject.SetActive(active);
        if (!active) return;

        bool ready = caster.ComboReady;
        comboBox.nameText.text = caster.ComboName;
        comboBox.keyText.text = caster.ComboKeys;
        comboBox.costText.text = Mathf.RoundToInt(caster.ComboCost).ToString();
        comboBox.cooldown.fillAmount = 1f - caster.ComboProgress;      // darkness drains away as the cast completes

        bool affordable = mana == null || mana.CanAfford(caster.ComboCost);
        comboBox.bg.color = affordable ? comboBg : comboBg * new Color(1f, 0.45f, 0.45f, 1f);
        comboBox.border.color = ready ? readyColor : chargingColor;
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

        if (caster == null) return;

        // one charging bar per spell being cast, side by side; the name changes to the stronger version once it is ready
        var entries = caster.CastEntries;
        int n = entries.Count;
        int bars = castBarBack != null ? castBarBack.Length : 0;
        float glide = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);

        for (int k = 0; k < bars; k++)
        {
            if (castBarBack[k] == null) continue;

            bool on = k < n;
            if (castBarBack[k].gameObject.activeSelf != on) castBarBack[k].gameObject.SetActive(on);
            if (!on) continue;

            var e = entries[k];
            var rt = castBarBack[k].rectTransform;
            Vector2 target = new Vector2(castBarBasePos.x + (k - (n - 1) * 0.5f) * castBarSpacing, castBarBasePos.y);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, glide);
            rt.localScale = Vector3.Lerp(rt.localScale, castBarScale * (e.primary || n == 1 ? 1f : 0.9f), glide);

            if (castBarFill[k] != null)
            {
                castBarFill[k].fillAmount = e.progress;
                castBarFill[k].color = e.ready ? readyColor : chargeFillBase;
            }
            if (castBarLabel != null && k < castBarLabel.Length && castBarLabel[k] != null)
            {
                castBarLabel[k].text = e.name;
                castBarLabel[k].color = e.ready ? readyColor : (e.primary || n == 1 ? Color.white : new Color(1f, 1f, 1f, 0.8f));
            }
        }

        // why the last cast failed, briefly
        if (castLabel != null)
        {
            bool failing = Time.unscaledTime < castFailUntil;
            if (castLabel.gameObject.activeSelf != failing) castLabel.gameObject.SetActive(failing);
            if (failing)
            {
                castLabel.text = castFailText;
                castLabel.color = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0f), new Color(1f, 0.3f, 0.25f, 1f), Mathf.Clamp01((castFailUntil - Time.unscaledTime) / 0.5f));
            }
        }
    }
}
