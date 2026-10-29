using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu: Start (stage select), Customize (your spell slots, the spell table and the combinations), Options (volume) and Quit.
// The whole screen is built from code when the scene starts.
public class MainMenu : MonoBehaviour
{

    GameCatalog catalog;
    Transform canvasRoot;
    RectTransform mainPanel, stagePanel, customizePanel, optionsPanel;
    RectTransform spellMenu, comboMenu, keyPrompt;       // overlays on top of the customize screen (the prompt is on top of those)
    RectTransform current;

    // customize
    string[] loadout;
    int bookSlot = -1;                                   // the key the spell book was opened for; -1 = opened on its own (the key is asked afterwards)
    readonly List<Text> slotName = new List<Text>();
    readonly List<Text> slotInfo = new List<Text>();
    Text readyText;

    // spell table
    RectTransform spellRows;
    Text spellMenuTitle, spellMenuHint;

    // the "which key?" prompt
    Text promptTitle, promptSub;
    RectTransform promptCards;

    // combination list
    RectTransform comboRows;

    // options
    OptionsMenu options;
    readonly List<Text> slotKeyText = new List<Text>();

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        catalog = GameCatalog.Load();
        UIKit.EnsureEventSystem();
        PlayerProfile.ApplyVolume();

        var canvas = UIKit.MakeCanvas("MainMenuCanvas", 0);
        canvas.transform.SetParent(transform, false);
        canvasRoot = canvas.transform;

        var bg = UIKit.Box(canvasRoot, "Background", new Color(0.04f, 0.06f, 0.11f, 1f));
        UIKit.Stretch(bg.rectTransform);
        AddGlow(canvasRoot);

        mainPanel = BuildMain(canvasRoot);
        stagePanel = BuildStages(canvasRoot);
        customizePanel = BuildCustomize(canvasRoot);
        optionsPanel = BuildOptions(canvasRoot);
        spellMenu = BuildSpellMenu(canvasRoot);
        comboMenu = BuildComboMenu(canvasRoot);
        keyPrompt = BuildKeyPrompt(canvasRoot);

        Show(mainPanel);
    }

    void Update()
    {
        // the key prompt also takes the key itself: press the key of a slot to put the spell there (keys that are no slot do nothing)
        if (keyPrompt != null && keyPrompt.gameObject.activeSelf && Keyboard.current != null && Time.frameCount > promptOpenFrame && loadout != null)
            for (int k = 0; k < loadout.Length; k++)
            {
                string path = PlayerSettings.GetPath("spell" + k);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>")) continue;      // mouse buttons are only clicked, so a click never picks by accident
                if (!(UnityEngine.InputSystem.InputSystem.FindControl(path) is UnityEngine.InputSystem.Controls.ButtonControl key) || !key.wasPressedThisFrame) continue;
                if (promptTaken != null && promptTaken.Contains(k)) return;
                keyPrompt.gameObject.SetActive(false);
                promptOnPick?.Invoke(k);
                return;
            }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (PlayerSettings.CaptureBusy) return;                 // that Esc only cancelled picking a key

        // Esc closes the prompt first, then the book or the list, then goes back to the main menu
        if (keyPrompt.gameObject.activeSelf) keyPrompt.gameObject.SetActive(false);
        else if (spellMenu.gameObject.activeSelf || comboMenu.gameObject.activeSelf) CloseOverlays();
        else if (current != mainPanel) Show(mainPanel);
    }

    void Show(RectTransform panel)
    {
        current = panel;
        foreach (var p in new[] { mainPanel, stagePanel, customizePanel, optionsPanel })
            if (p != null) p.gameObject.SetActive(p == panel);
        CloseOverlays();
        if (panel == optionsPanel && options != null) options.Open();
    }

    void CloseOverlays()
    {
        if (spellMenu != null) spellMenu.gameObject.SetActive(false);
        if (comboMenu != null) comboMenu.gameObject.SetActive(false);
        if (keyPrompt != null) keyPrompt.gameObject.SetActive(false);
        if (current == customizePanel) RefreshCustomize();
    }

    // a soft bluish glow behind the title so the screen is not a flat colour
    static void AddGlow(Transform parent)
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(0.25f, 0.5f, 0.9f, a * a * 0.35f));
            }
        tex.Apply();

        var rt = UIKit.Child(parent, "Glow");
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        UIKit.Place(rt, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(2000f, 1400f));
    }

    RectTransform Panel(Transform parent, string name)
    {
        var rt = UIKit.Child(parent, name);
        UIKit.Stretch(rt);
        return rt;
    }

    Button BackButton(Transform parent)
    {
        var b = UIKit.MakeButton(parent, "Back", new Vector2(260f, 70f), () => Show(mainPanel), 28);
        UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(260f, 70f));
        return b;
    }

    // ---------- main ----------

    RectTransform BuildMain(Transform parent)
    {
        var p = Panel(parent, "Main");

        var title = UIKit.Label(p, "Title", "MAGIC GAME", 130, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1400f, 180f));
        var sub = UIKit.Label(p, "Subtitle", "Wands, broomsticks and far too many circles", 30, UIKit.Dim);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 50f));

        string[] names = { "Start", "Customize", "Options", "Quit" };
        System.Action[] actions = { () => Show(stagePanel), () => { Show(customizePanel); RefreshCustomize(); }, () => Show(optionsPanel), Quit };
        for (int i = 0; i < names.Length; i++)
        {
            var b = UIKit.MakeButton(p, names[i], new Vector2(420f, 90f), actions[i], 38);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f - i * 115f), new Vector2(420f, 90f));
        }
        return p;
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- stage select ----------

    RectTransform BuildStages(Transform parent)
    {
        var p = Panel(parent, "Stages");
        var head = UIKit.Label(p, "Header", "SELECT STAGE", 80, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 110f));

        var stages = catalog != null ? catalog.stages : null;
        int count = stages != null ? stages.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var info = stages[i];
            var card = UIKit.MakeButton(p, info.displayName, new Vector2(1100f, 170f), () => StartStage(index), 1);
            UIKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f - i * 190f), new Vector2(1100f, 170f));

            // the button's own label is replaced by a name, a description and the best time
            var own = card.transform.Find("Text");
            if (own != null) Destroy(own.gameObject);

            var name = UIKit.Label(card.transform, "Name", info.displayName, 52, Color.white, TextAnchor.MiddleLeft, true);
            UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(480f, 34f), new Vector2(880f, 70f));
            var desc = UIKit.Label(card.transform, "Description", info.description, 26, UIKit.Dim, TextAnchor.MiddleLeft);
            UIKit.Place(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(480f, -28f), new Vector2(880f, 70f));

            float best = PlayerProfile.GetBestTime(i);
            var record = UIKit.Label(card.transform, "Best", best >= 0f ? "BEST\n" + PlayerProfile.FormatTime(best) : "NOT\nCLEARED", 28,
                best >= 0f ? UIKit.Accent : UIKit.Dim, TextAnchor.MiddleCenter, true);
            UIKit.Place(record.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 100f));
        }

        if (count == 0)
        {
            var none = UIKit.Label(p, "None", "No stages yet", 40, UIKit.Dim);
            UIKit.Place(none.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 80f));
        }

        BackButton(p);
        return p;
    }

    void StartStage(int index)
    {
        var info = catalog.stages[index];
        if (!Application.CanStreamedLevelBeLoaded(info.sceneName))
        {
            Debug.LogWarning("The scene '" + info.sceneName + "' is not in the build settings.");
            return;
        }
        PlayerProfile.CurrentStage = index;
        SceneManager.LoadScene(info.sceneName);
    }

    // ---------- customize: the slots ----------

    RectTransform BuildCustomize(Transform parent)
    {
        var p = Panel(parent, "Customize");
        var head = UIKit.Label(p, "Header", "CUSTOMIZE SPELLS", 80, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 110f));
        var hint = UIKit.Label(p, "Hint", "Click a key to pick its spell, or open the spell book and pick the spell first.", 28, UIKit.Dim);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1500f, 50f));

        loadout = PlayerProfile.GetLoadout(catalog);
        int slots = loadout.Length;
        const float gap = 20f;
        float cardW = Mathf.Min(340f, (1760f - (slots - 1) * gap) / slots);      // narrower cards when there are more keys
        bool narrow = cardW < 300f;
        float total = slots * cardW + (slots - 1) * gap;

        for (int i = 0; i < slots; i++)
        {
            int slot = i;
            float x = -total * 0.5f + cardW * 0.5f + i * (cardW + gap);

            // the whole card is a button: clicking the displayed spell opens the table for this slot
            var card = UIKit.MakeButton(p, "Slot" + (i + 1), new Vector2(cardW, 440f), () => OpenSpellMenu(slot), 1);
            UIKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(x, 60f), new Vector2(cardW, 440f));
            var own = card.transform.Find("Text");
            if (own != null) Destroy(own.gameObject);

            var keyText = UIKit.Label(card.transform, "Key", KeyLabel(i), narrow ? 40 : 46, UIKit.Gold, TextAnchor.MiddleCenter, true);
            slotKeyText.Add(keyText);
            UIKit.Place(keyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(cardW - 20f, 60f));

            var name = UIKit.Label(card.transform, "Name", "", narrow ? 30 : 40, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(cardW - 30f, 110f));
            slotName.Add(name);

            var info = UIKit.Label(card.transform, "Info", "", narrow ? 19 : 24, UIKit.Dim, TextAnchor.UpperCenter);
            UIKit.Place(info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(cardW - 40f, 150f));
            slotInfo.Add(info);

            var change = UIKit.Label(card.transform, "Change", "click to change", 22, UIKit.Accent, TextAnchor.MiddleCenter);
            UIKit.Place(change.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(cardW - 20f, 36f));
        }

        // the two menus that open on top of this screen
        var spells = UIKit.MakeButton(p, "Spell Book", new Vector2(380f, 80f), () => OpenSpellMenu(-1), 32);
        UIKit.Place((RectTransform)spells.transform, new Vector2(0.5f, 0f), new Vector2(-210f, 255f), new Vector2(380f, 80f));
        var combos = UIKit.MakeButton(p, "Combinations", new Vector2(380f, 80f), OpenComboMenu, 32);
        UIKit.Place((RectTransform)combos.transform, new Vector2(0.5f, 0f), new Vector2(210f, 255f), new Vector2(380f, 80f));

        readyText = UIKit.Label(p, "Ready", "", 26, UIKit.Dim, TextAnchor.MiddleCenter);
        readyText.supportRichText = true;
        UIKit.Place(readyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1500f, 44f));

        BackButton(p);
        return p;
    }

    void RefreshCustomize()
    {
        if (catalog == null || slotName.Count == 0) return;
        loadout = PlayerProfile.GetLoadout(catalog);
        for (int i = 0; i < loadout.Length; i++)
        {
            if (i < slotKeyText.Count) slotKeyText[i].text = KeyLabel(i);
            var spell = catalog.FindSpell(loadout[i]);
            slotName[i].text = spell != null ? spell.displayName : "(empty)";
            slotName[i].color = spell != null ? Color.white : UIKit.Dim;
            slotInfo[i].text = spell != null ? Describe(spell) : "Nothing equipped";
        }

        // which combinations the current spells already allow
        var ready = new List<string>();
        if (catalog.combos != null)
            foreach (var combo in catalog.combos)
                if (combo != null && combo.result != null && AllEquipped(combo)) ready.Add(combo.result.displayName);
        readyText.text = ready.Count > 0 ? "Ready to combine:  <color=#FFD15A>" + string.Join(",  ", ready) + "</color>" : "No combinations ready with these spells";
    }

    bool AllEquipped(SpellCombo combo)
    {
        foreach (var ing in combo.ingredients)
            if (ing != null && System.Array.IndexOf(loadout, ing.name) < 0) return false;
        return true;
    }

    static string Describe(SpellData s)
    {
        string text = KindName(s) + "\n";
        if (s.tapDoesNothing) text += "Hold to charge\n";
        else text += "Tap: " + Mathf.RoundToInt(s.tap.manaCost) + " mana\n";
        if (s.hasHoldVariant) text += "Hold: " + HoldChain(s) + "\n" + Mathf.RoundToInt(s.hold.manaCost) + " mana, " + s.holdThreshold.ToString("0.#") + "s cast";
        else if (s.autoRepeatWhenHeld) text += "Hold to keep firing";
        return text;
    }

    static string KindName(SpellData s)
    {
        switch (s.tap.kind)
        {
            case SpellKind.Laser: return "Beam";
            case SpellKind.GroundAoE: return "Ground area";
            default: return "Projectile";
        }
    }

    // "Cataclysm > Apocalypse" for a spell that grows while charged
    static string HoldChain(SpellData s)
    {
        string text = s.HoldName;
        for (int i = 0; i < s.TierCount; i++) text += " > " + s.extraTiers[i].displayName;
        return text;
    }

    string KeyLabel(int slot) => PlayerSettings.LabelOf("spell" + slot) is string label && label != "-" ? label : "slot " + (slot + 1);

    // ---------- equipping ----------

    void SaveLoadout()
    {
        PlayerProfile.SetLoadout(loadout);
        RefreshCustomize();
    }

    // puts a spell into a slot; if it was in another slot, the two swap
    void EquipSpell(int slot, SpellData spell)
    {
        string name = spell != null ? spell.name : "";
        if (!string.IsNullOrEmpty(name))
        {
            int at = System.Array.IndexOf(loadout, name);
            if (at == slot) return;
            if (at >= 0) loadout[at] = loadout[slot];
        }
        loadout[slot] = name;
        SaveLoadout();
        RefreshSpellMenu();
    }

    static List<SpellData> Ingredients(SpellCombo combo)
    {
        var list = new List<SpellData>();
        foreach (var ing in combo.ingredients) if (ing != null && !list.Contains(ing)) list.Add(ing);
        return list;
    }

    string SpellNameOn(int slot)
    {
        var spell = string.IsNullOrEmpty(loadout[slot]) ? null : catalog.FindSpell(loadout[slot]);
        return spell != null ? spell.displayName : "";
    }

    // puts every ingredient on the key chosen for it. A spell that was on a chosen key is not lost: it moves onto a key an ingredient came from, if one was freed.
    void ApplyCombo(List<SpellData> ings, int[] slots)
    {
        var displaced = new List<string>();
        var vacated = new List<int>();
        var chosen = new HashSet<int>(slots);

        foreach (var ing in ings)
        {
            int from = System.Array.IndexOf(loadout, ing.name);
            if (from >= 0 && !chosen.Contains(from)) vacated.Add(from);
        }
        foreach (int slot in slots)
            if (!string.IsNullOrEmpty(loadout[slot]) && !ings.Exists(x => x.name == loadout[slot])) displaced.Add(loadout[slot]);

        foreach (var ing in ings)
        {
            int from = System.Array.IndexOf(loadout, ing.name);
            if (from >= 0) loadout[from] = "";
        }
        for (int k = 0; k < ings.Count; k++) loadout[slots[k]] = ings[k].name;
        for (int i = 0; i < displaced.Count && i < vacated.Count; i++) loadout[vacated[i]] = displaced[i];

        SaveLoadout();
    }

    // ---------- the spell book (a table of every spell) ----------

    RectTransform BuildSpellMenu(Transform parent)
    {
        var overlay = Overlay(parent, "SpellMenu", new Vector2(1640f, 900f), out var panel);

        spellMenuTitle = UIKit.Label(panel, "Title", "SPELL BOOK", 64, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(spellMenuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1500f, 90f));
        spellMenuHint = UIKit.Label(panel, "Hint", "", 26, UIKit.Dim);
        UIKit.Place(spellMenuHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1500f, 44f));

        float y = -175f;
        Header(panel, "Spell", 40f, 330f, y);
        Header(panel, "Type", 400f, 220f, y);
        Header(panel, "Tap", 650f, 150f, y);
        Header(panel, "Hold", 830f, 420f, y);
        Header(panel, "Equipped on", 1290f, 250f, y);

        UIKit.MakeScroll(panel, "Table", new Vector2(1560f, 600f), out spellRows);
        var scrollRt = (RectTransform)spellRows.parent.parent;
        UIKit.Place(scrollRt, new Vector2(0.5f, 1f), new Vector2(0f, -505f), new Vector2(1560f, 600f));

        var done = UIKit.MakeButton(panel, "Done", new Vector2(300f, 70f), CloseOverlays, 30);
        UIKit.Place((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(300f, 70f));
        return overlay;
    }

    // a dimmed full-screen layer with a panel in the middle
    RectTransform Overlay(Transform parent, string name, Vector2 size, out RectTransform panel)
    {
        var dim = UIKit.Box(parent, name, new Color(0f, 0f, 0f, 0.78f));
        UIKit.Stretch(dim.rectTransform);

        var box = UIKit.Box(dim.transform, "Panel", new Color(0.07f, 0.1f, 0.17f, 1f));
        UIKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        panel = box.rectTransform;

        dim.gameObject.SetActive(false);
        return dim.rectTransform;
    }

    static void Header(Transform parent, string text, float left, float width, float y)
    {
        var t = UIKit.Label(parent, "Header_" + text, text.ToUpper(), 22, UIKit.Dim, TextAnchor.MiddleLeft, true);
        UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(left + width * 0.5f + 20f, y), new Vector2(width, 36f));
    }

    // slot >= 0: opened from a key, so choosing a spell puts it on that key. -1: opened on its own, so choosing a spell asks which key.
    void OpenSpellMenu(int slot)
    {
        bookSlot = slot;
        spellMenu.gameObject.SetActive(true);
        RefreshSpellMenu();
    }

    void RefreshSpellMenu()
    {
        if (!spellMenu.gameObject.activeSelf) return;

        if (bookSlot >= 0)
        {
            spellMenuTitle.text = "SPELL FOR " + KeyLabel(bookSlot).ToUpper();
            string now = SpellNameOn(bookSlot);
            spellMenuHint.text = "Now on this key: " + (now.Length > 0 ? now : "nothing") + ".  Click a spell to put it here.";
        }
        else
        {
            spellMenuTitle.text = "SPELL BOOK";
            spellMenuHint.text = "Click a spell, then choose the key for it.";
        }

        for (int i = spellRows.childCount - 1; i >= 0; i--) Destroy(spellRows.GetChild(i).gameObject);

        if (bookSlot >= 0) SpellRow(null);
        if (catalog != null && catalog.spells != null)
            foreach (var spell in catalog.spells)
                if (spell != null) SpellRow(spell);
    }

    void OnSpellChosen(SpellData spell)
    {
        if (bookSlot >= 0)
        {
            EquipSpell(bookSlot, spell);            // straight onto the key the book was opened for
            CloseOverlays();
            return;
        }

        OpenKeyPrompt("Which key for " + spell.displayName + "?", "Pick the key it goes on.", spell, null, slot => EquipSpell(slot, spell));
    }

    void SpellRow(SpellData spell)
    {
        bool here = bookSlot >= 0 && (spell != null ? loadout[bookSlot] == spell.name : string.IsNullOrEmpty(loadout[bookSlot]));
        var row = UIKit.MakeButton(spellRows, spell != null ? spell.displayName : "(empty)", new Vector2(1540f, 96f), () => OnSpellChosen(spell), 1);
        UIKit.SetChosen(row, here);
        var own = row.transform.Find("Text");
        if (own != null) Destroy(own.gameObject);

        if (spell == null)
        {
            Cell(row.transform, "Name", "(leave this key empty)", 34, UIKit.Dim, 40f, 700f, true);
            return;
        }

        Cell(row.transform, "Name", spell.displayName, 36, Color.white, 40f, 350f, true);
        Cell(row.transform, "Type", KindName(spell), 26, UIKit.Dim, 400f, 220f, false);
        Cell(row.transform, "Tap", spell.tapDoesNothing ? "-" : Mathf.RoundToInt(spell.tap.manaCost) + " mana", 26, UIKit.Dim, 650f, 150f, false);

        string hold = spell.hasHoldVariant
            ? HoldChain(spell) + "\n" + Mathf.RoundToInt(spell.hold.manaCost) + " mana, " + spell.holdThreshold.ToString("0.#") + "s cast"
            : (spell.autoRepeatWhenHeld ? "keeps firing" : "-");
        Cell(row.transform, "Hold", hold, 24, UIKit.Dim, 830f, 440f, false);

        string where = "";
        for (int i = 0; i < loadout.Length; i++) if (loadout[i] == spell.name) where = KeyLabel(i);
        Cell(row.transform, "Equipped", where, 30, UIKit.Gold, 1290f, 250f, true);
    }

    static void Cell(Transform row, string name, string text, int size, Color color, float left, float width, bool bold)
    {
        var t = UIKit.Label(row, name, text, size, color, TextAnchor.MiddleLeft, bold);
        UIKit.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(left + width * 0.5f, 0f), new Vector2(width, 90f));
    }

    // ---------- the "which key?" prompt ----------

    RectTransform BuildKeyPrompt(Transform parent)
    {
        int keys = loadout != null ? loadout.Length : (catalog != null ? catalog.slotCount : 4);
        var overlay = Overlay(parent, "KeyPrompt", new Vector2(Mathf.Max(1200f, keys * 250f + 120f), 640f), out var panel);

        promptTitle = UIKit.Label(panel, "Title", "", 56, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(promptTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(1500f, 90f));
        promptSub = UIKit.Label(panel, "Sub", "", 28, UIKit.Dim);
        UIKit.Place(promptSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(1500f, 44f));

        promptCards = UIKit.Child(panel, "Cards");
        UIKit.Place(promptCards, new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(1500f, 300f));

        var cancel = UIKit.MakeButton(panel, "Cancel", new Vector2(280f, 66f), () => keyPrompt.gameObject.SetActive(false), 28);
        UIKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(280f, 66f));
        return overlay;
    }

    // Asks which key something goes on and shows what each key holds right now. `taken` keys (already chosen for another spell in the
    // same combination) cannot be picked again. onPick runs with the chosen key.
    HashSet<int> promptTaken;
    System.Action<int> promptOnPick;
    int promptOpenFrame;

    void OpenKeyPrompt(string title, string subtitle, SpellData spell, HashSet<int> taken, System.Action<int> onPick)
    {
        promptTaken = taken;
        promptOnPick = onPick;
        promptOpenFrame = Time.frameCount;
        promptTitle.text = title;
        promptSub.text = subtitle + "  You can also press the key.";
        for (int i = promptCards.childCount - 1; i >= 0; i--) Destroy(promptCards.GetChild(i).gameObject);

        int keys = loadout.Length;
        const float w = 230f, gap = 20f;
        float total = keys * w + (keys - 1) * gap;

        for (int k = 0; k < keys; k++)
        {
            int slot = k;
            bool isTaken = taken != null && taken.Contains(k);
            bool here = spell != null && loadout[k] == spell.name;
            string now = SpellNameOn(k);

            var card = UIKit.MakeButton(promptCards, KeyLabel(k), new Vector2(w, 280f), () =>
            {
                keyPrompt.gameObject.SetActive(false);
                onPick(slot);
            }, 1);
            UIKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(-total * 0.5f + w * 0.5f + k * (w + gap), 0f), new Vector2(w, 280f));
            var own = card.transform.Find("Text");
            if (own != null) Destroy(own.gameObject);
            card.interactable = !isTaken;
            UIKit.SetChosen(card, here);

            var key = UIKit.Label(card.transform, "Key", KeyLabel(k), 48, isTaken ? UIKit.Dim : UIKit.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Place(key.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(w - 16f, 60f));

            var current = UIKit.Label(card.transform, "Now", now.Length > 0 ? now : "(empty)", 30, now.Length > 0 ? Color.white : UIKit.Dim, TextAnchor.MiddleCenter, true);
            UIKit.Place(current.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(w - 20f, 100f));

            string note = isTaken ? "chosen for another spell" : here ? "already here" : now.Length > 0 ? "will be replaced" : "free";
            var noteText = UIKit.Label(card.transform, "Note", note, 22, here ? UIKit.Gold : UIKit.Dim, TextAnchor.MiddleCenter);
            UIKit.Place(noteText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(w - 20f, 36f));
        }

        keyPrompt.gameObject.SetActive(true);
    }

    // ---------- the combinations ----------

    RectTransform BuildComboMenu(Transform parent)
    {
        var overlay = Overlay(parent, "ComboMenu", new Vector2(1640f, 900f), out var panel);

        var title = UIKit.Label(panel, "Title", "COMBINATIONS", 64, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1500f, 90f));
        var hint = UIKit.Label(panel, "Hint", "Hold the keys of all the spells together to cast the stronger spell. Press Equip and choose a key for each spell.", 26, UIKit.Dim);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(1500f, 44f));

        UIKit.MakeScroll(panel, "List", new Vector2(1560f, 620f), out comboRows);
        var scrollRt = (RectTransform)comboRows.parent.parent;
        UIKit.Place(scrollRt, new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(1560f, 620f));

        var done = UIKit.MakeButton(panel, "Done", new Vector2(300f, 70f), CloseOverlays, 30);
        UIKit.Place((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(300f, 70f));
        return overlay;
    }

    void OpenComboMenu()
    {
        comboMenu.gameObject.SetActive(true);
        RefreshComboMenu();
    }

    void RefreshComboMenu()
    {
        for (int i = comboRows.childCount - 1; i >= 0; i--) Destroy(comboRows.GetChild(i).gameObject);
        if (catalog == null || catalog.combos == null) return;

        foreach (var combo in catalog.combos)
            if (combo != null && combo.result != null && combo.ingredients != null) ComboRow(combo);
    }

    void ComboRow(SpellCombo combo)
    {
        var ings = Ingredients(combo);
        bool fits = ings.Count <= loadout.Length;
        bool equipped = AllEquipped(combo);

        var row = UIKit.Box(comboRows, "Combo_" + combo.result.displayName, equipped ? new Color(0.22f, 0.18f, 0.08f, 1f) : UIKit.PanelColor);
        row.rectTransform.sizeDelta = new Vector2(1540f, 130f);

        var result = UIKit.Label(row.transform, "Result", combo.result.displayName, 42, UIKit.Gold, TextAnchor.MiddleLeft, true);
        UIKit.Place(result.rectTransform, new Vector2(0f, 0.5f), new Vector2(340f, 28f), new Vector2(600f, 56f));

        var names = new List<string>();
        foreach (var ing in ings) names.Add(ing.displayName);
        var parts = UIKit.Label(row.transform, "Parts", string.Join("  +  ", names), 30, Color.white, TextAnchor.MiddleLeft);
        UIKit.Place(parts.rectTransform, new Vector2(0f, 0.5f), new Vector2(340f, -24f), new Vector2(600f, 44f));

        var variant = combo.result.hasHoldVariant ? combo.result.hold : combo.result.tap;
        string stats = Mathf.RoundToInt(variant.manaCost) + " mana, " + (combo.result.hasHoldVariant ? combo.result.holdThreshold.ToString("0.#") + "s cast" : "instant");
        var info = UIKit.Label(row.transform, "Info", stats, 26, UIKit.Dim, TextAnchor.MiddleLeft);
        UIKit.Place(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(950f, 0f), new Vector2(380f, 60f));

        string label = equipped ? "Change keys" : (fits ? "Equip" : "Needs " + ings.Count + " keys");
        var button = UIKit.MakeButton(row.transform, label, new Vector2(300f, 76f), () => StartComboPrompts(combo), 30);
        UIKit.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(300f, 76f));
        button.interactable = fits;
        if (!fits) button.GetComponentInChildren<Text>().color = UIKit.Dim;
    }

    // one "which key?" prompt for every spell the combination needs, in order; nothing changes until the last one is answered
    void StartComboPrompts(SpellCombo combo)
    {
        var ings = Ingredients(combo);
        if (ings.Count == 0 || ings.Count > loadout.Length) return;

        var slots = new int[ings.Count];
        AskIngredient(combo, ings, slots, 0);
    }

    void AskIngredient(SpellCombo combo, List<SpellData> ings, int[] slots, int index)
    {
        var taken = new HashSet<int>();
        for (int k = 0; k < index; k++) taken.Add(slots[k]);

        string title = combo.result.displayName + ": key for " + ings[index].displayName;
        string subtitle = "Spell " + (index + 1) + " of " + ings.Count + ".  Pick the key it goes on.";
        OpenKeyPrompt(title, subtitle, ings[index], taken, slot =>
        {
            slots[index] = slot;
            if (index + 1 < ings.Count) AskIngredient(combo, ings, slots, index + 1);
            else
            {
                ApplyCombo(ings, slots);
                RefreshComboMenu();
            }
        });
    }

    // ---------- options ----------

    RectTransform BuildOptions(Transform parent)
    {
        var p = Panel(parent, "Options");
        options = OptionsMenu.Build(p, () => Show(mainPanel));
        return p;
    }
}
