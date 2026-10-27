using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Magic > Build HUD
// Builds the player HUD as real GameObjects under the GUIs canvas and wires them into PlayerHUD.
// Running it again replaces the HUD folder, so use it to reset the layout (it overwrites manual edits to those objects).
public static class HudBuilder
{
    const string WhitePath = "Assets/_Assets/Images/UI/White.png";

    static Sprite white;
    static Font font;

    [MenuItem("Tools/Magic/Build HUD")]
    public static void Build()
    {
        var guis = GameObject.Find("GUIs");
        if (guis == null)
        {
            Debug.LogError("Build HUD: no GUIs object in the scene.");
            return;
        }

        // the HUD is laid out for 1920x1080 and scales from there
        var scaler = guis.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        white = EnsureWhiteSprite();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // clear any previous build (including the old runtime-generated HUD object)
        var previous = guis.transform.Find("HUD");
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        foreach (var oldHud in Object.FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include))
            if (oldHud != null && oldHud.transform.parent == null) Undo.DestroyObjectImmediate(oldHud.gameObject);

        // HUD folder + driver component
        var hudGo = new GameObject("HUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(hudGo, "Build HUD");
        hudGo.transform.SetParent(guis.transform, false);
        Stretch(hudGo.GetComponent<RectTransform>());
        var hud = hudGo.AddComponent<PlayerHUD>();
        var so = new SerializedObject(hud);

        // starting layout only: once built, every position and size below is whatever you set in the editor
        const float slotSize = 96f, slotSpacing = 12f, indicatorGap = 30f, indicatorScale = 0.85f, bottom = 28f;

        var caster = Object.FindAnyObjectByType<PlayerSpellCaster>();
        int count = caster != null ? caster.SlotCount : 4;

        // ---- damage vignette (behind everything) ----
        var vignette = MakeImage(hudGo.transform, "DamageVignette", new Color(0.8f, 0f, 0f, 0f));
        Stretch(vignette.rectTransform);

        // ---- spell bar ----
        var spellBar = NewRect(hudGo.transform, "SpellBar");
        Stretch(spellBar);
        float total = count * slotSize + (count - 1) * slotSpacing;
        float y = bottom + slotSize * 0.5f;

        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            float x = -total * 0.5f + slotSize * 0.5f + i * (slotSize + slotSpacing);
            var slot = BuildSlot(spellBar, "Slot" + (i + 1), new Vector2(x, y), slotSize);
            slot.nameText.text = "Spell " + (i + 1);
            slot.keyText.text = (i + 1).ToString();
            AssignSlot(slotsProp.GetArrayElementAtIndex(i), slot);
        }

        // ---- broom indicator ----
        float side = total * 0.5f + indicatorGap + slotSize * indicatorScale * 0.5f;
        var broom = BuildSlot(hudGo.transform, "BroomIndicator", new Vector2(-side, y), slotSize);
        broom.border.rectTransform.localScale = Vector3.one * indicatorScale;
        broom.nameText.text = "BROOM";
        broom.keyText.text = "X";
        broom.costText.text = "READY";
        AssignSlot(so.FindProperty("broomBox"), broom);

        float rowWidth = Mathf.Max(total + 2f * (slotSize * indicatorScale + indicatorGap), 360f);

        // ---- mana bar ----
        var manaBar = NewRect(hudGo.transform, "ManaBar");
        Place(manaBar, new Vector2(0.5f, 0f), new Vector2(0f, bottom + slotSize + 30f), new Vector2(rowWidth, 20f));
        var manaBg = manaBar.gameObject.AddComponent<Image>();
        manaBg.sprite = white;
        manaBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
        manaBg.raycastTarget = false;
        var manaFill = MakeImage(manaBar, "Fill", new Color(0.25f, 0.55f, 1f));
        Stretch(manaFill.rectTransform, 2f);
        SetFilled(manaFill);
        var manaText = MakeText(manaBar, "Label", 14, TextAnchor.MiddleCenter, Color.white);
        Stretch(manaText.rectTransform);
        manaText.text = "100 / 100";

        // ---- health bar ----
        var healthBar = NewRect(hudGo.transform, "HealthBar");
        Place(healthBar, new Vector2(0.5f, 0f), new Vector2(0f, bottom + slotSize + 62f), new Vector2(rowWidth, 26f));
        var healthBg = healthBar.gameObject.AddComponent<Image>();
        healthBg.sprite = white;
        healthBg.color = HealthBarStyle.Background;
        healthBg.raycastTarget = false;
        var healthChip = MakeImage(healthBar, "Chip", HealthBarStyle.Chip);
        Stretch(healthChip.rectTransform, 2f);
        SetFilled(healthChip);
        var healthFill = MakeImage(healthBar, "Fill", HealthBarStyle.Full);
        Stretch(healthFill.rectTransform, 2f);
        SetFilled(healthFill);
        var healthText = MakeText(healthBar, "Label", 16, TextAnchor.MiddleCenter, Color.white);
        Stretch(healthText.rectTransform);
        healthText.fontStyle = FontStyle.Bold;
        var outline = healthText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        healthText.text = "100 / 100";

        // ---- wave counter ----
        var waveText = MakeText(hudGo.transform, "WaveText", 28, TextAnchor.UpperCenter, Color.white);
        Place(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 50f));
        waveText.fontStyle = FontStyle.Bold;
        waveText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        waveText.text = "Wave 1 / 5";

        // ---- weapon label (top left) ----
        var weaponLabel = MakeText(hudGo.transform, "WeaponLabel", 14, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
        weaponLabel.rectTransform.anchorMin = weaponLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        weaponLabel.rectTransform.pivot = new Vector2(0f, 1f);
        weaponLabel.rectTransform.anchoredPosition = new Vector2(14f, -12f);
        weaponLabel.rectTransform.sizeDelta = new Vector2(500f, 24f);
        weaponLabel.text = "Weapon";

        // ---- hold-charge bar under the crosshair ----
        var chargeBack = MakeImage(hudGo.transform, "ChargeBar", new Color(0f, 0f, 0f, 0.6f));
        Place(chargeBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(160f, 10f));
        var chargeFill = MakeImage(chargeBack.transform, "Fill", new Color(0.4f, 0.85f, 1f));
        Stretch(chargeFill.rectTransform);
        SetFilled(chargeFill);
        chargeBack.gameObject.SetActive(false);   // shown only while charging a hold spell

        // ---- hook everything up ----
        so.FindProperty("manaBg").objectReferenceValue = manaBg;
        so.FindProperty("manaFill").objectReferenceValue = manaFill;
        so.FindProperty("manaText").objectReferenceValue = manaText;
        so.FindProperty("healthBg").objectReferenceValue = healthBg;
        so.FindProperty("healthChip").objectReferenceValue = healthChip;
        so.FindProperty("healthFill").objectReferenceValue = healthFill;
        so.FindProperty("healthText").objectReferenceValue = healthText;
        so.FindProperty("waveText").objectReferenceValue = waveText;
        so.FindProperty("weaponLabel").objectReferenceValue = weaponLabel;
        so.FindProperty("vignette").objectReferenceValue = vignette;
        so.FindProperty("chargeBack").objectReferenceValue = chargeBack;
        so.FindProperty("chargeFill").objectReferenceValue = chargeFill;
        so.ApplyModifiedProperties();
        AddLockIndicator();

        // the HUD draws these now, so the caster's own on-screen versions are switched off
        if (caster != null)
        {
            var cso = new SerializedObject(caster);
            cso.FindProperty("showChargeBar").boolValue = false;
            cso.FindProperty("showWeaponLabel").boolValue = false;
            cso.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(guis.scene);
        Selection.activeGameObject = hudGo;
        Debug.Log("HUD built under GUIs/HUD. Edit the objects there or re-run Tools > Magic > Build HUD to reset them.");
    }

    // Adds just the lock-on indicator to an already built HUD, mirroring the broom box on the other side of the spells.
    // It leaves everything else you have edited alone.
    [MenuItem("Tools/Magic/Add Lock Indicator To HUD")]
    public static void AddLockIndicator()
    {
        var hudGo = GameObject.Find("GUIs/HUD");
        var hud = hudGo != null ? hudGo.GetComponent<PlayerHUD>() : null;
        if (hud == null)
        {
            Debug.LogError("Add Lock Indicator: build the HUD first (Tools > Magic > Build HUD).");
            return;
        }

        var existing = hudGo.transform.Find("LockIndicator");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        white = EnsureWhiteSprite();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var broomT = hudGo.transform.Find("BroomIndicator") as RectTransform;
        Vector2 pos = broomT != null ? new Vector2(-broomT.anchoredPosition.x, broomT.anchoredPosition.y) : new Vector2(300f, 76f);
        Vector3 scale = broomT != null ? broomT.localScale : Vector3.one * 0.85f;

        var lockBox = BuildSlot(hudGo.transform, "LockIndicator", pos, 96f);
        Undo.RegisterCreatedObjectUndo(lockBox.border.gameObject, "Add Lock Indicator");
        lockBox.border.rectTransform.localScale = scale;
        lockBox.nameText.text = "LOCK";
        lockBox.keyText.text = "V";
        lockBox.costText.text = "NONE";
        lockBox.cooldown.color = new Color(0.95f, 0.25f, 0.2f, 0.5f);

        var so = new SerializedObject(hud);
        AssignSlot(so.FindProperty("lockBox"), lockBox);
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(hudGo.scene);
    }

    // ---------- helpers ----------

    static Sprite EnsureWhiteSprite()
    {
        if (!File.Exists(WhitePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WhitePath));
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            File.WriteAllBytes(WhitePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhitePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhitePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
    }

    static PlayerHUD.Slot BuildSlot(Transform parent, string name, Vector2 pos, float slotSize)
    {
        var s = new PlayerHUD.Slot();

        s.border = MakeImage(parent, name, new Color(0.3f, 0.3f, 0.3f));
        Place(s.border.rectTransform, new Vector2(0.5f, 0f), pos, new Vector2(slotSize + 6f, slotSize + 6f));

        s.bg = MakeImage(s.border.transform, "Background", new Color(0.08f, 0.08f, 0.1f, 0.9f));
        Stretch(s.bg.rectTransform, 3f);

        s.cooldown = MakeImage(s.bg.transform, "CooldownFill", new Color(0f, 0f, 0f, 0.7f));
        Stretch(s.cooldown.rectTransform);
        s.cooldown.type = Image.Type.Filled;
        s.cooldown.fillMethod = Image.FillMethod.Vertical;
        s.cooldown.fillOrigin = (int)Image.OriginVertical.Bottom;
        s.cooldown.fillAmount = 0f;

        s.nameText = MakeText(s.bg.transform, "Name", 15, TextAnchor.MiddleCenter, Color.white);
        Stretch(s.nameText.rectTransform, 4f);

        s.keyText = MakeText(s.bg.transform, "Key", 16, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.9f));
        Stretch(s.keyText.rectTransform, 5f);
        s.keyText.fontStyle = FontStyle.Bold;

        s.costText = MakeText(s.bg.transform, "Cost", 13, TextAnchor.LowerRight, new Color(0.5f, 0.8f, 1f));
        Stretch(s.costText.rectTransform, 5f);

        return s;
    }

    static void AssignSlot(SerializedProperty prop, PlayerHUD.Slot s)
    {
        prop.FindPropertyRelative("border").objectReferenceValue = s.border;
        prop.FindPropertyRelative("bg").objectReferenceValue = s.bg;
        prop.FindPropertyRelative("cooldown").objectReferenceValue = s.cooldown;
        prop.FindPropertyRelative("nameText").objectReferenceValue = s.nameText;
        prop.FindPropertyRelative("keyText").objectReferenceValue = s.keyText;
        prop.FindPropertyRelative("costText").objectReferenceValue = s.costText;
    }

    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = white;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void SetFilled(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 1f;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
