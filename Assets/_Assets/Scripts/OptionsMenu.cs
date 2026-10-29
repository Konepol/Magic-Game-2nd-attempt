using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Options screen, with a page for each kind of option: Audio, Key bindings and Spell hold.
// It is built from code into any full-screen panel, so the main menu and the pause menu share the same one.
public class OptionsMenu
{
    readonly RectTransform[] pages = new RectTransform[3];
    readonly Button[] tabs = new Button[3];

    Text volumeText;
    Slider volumeSlider;

    readonly Dictionary<string, Text> keyLabels = new Dictionary<string, Text>();
    string capturingId;
    Text bindHint;

    Text holdState;
    Button holdToggle;

    static readonly string[] PageNames = { "Audio", "Key bindings", "Spell hold" };

    public static OptionsMenu Build(RectTransform parent, System.Action back)
    {
        var menu = new OptionsMenu();
        menu.BuildAll(parent, back);
        return menu;
    }

    void BuildAll(RectTransform parent, System.Action back)
    {
        var head = UIKit.Label(parent, "Header", "OPTIONS", 80, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1200f, 110f));

        for (int i = 0; i < 3; i++)
        {
            int page = i;
            var tab = UIKit.MakeButton(parent, PageNames[i], new Vector2(380f, 70f), () => ShowPage(page), 30);
            UIKit.Place((RectTransform)tab.transform, new Vector2(0.5f, 1f), new Vector2(-400f + i * 400f, -235f), new Vector2(380f, 70f));
            tabs[i] = tab;

            var container = UIKit.Child(parent, "Page_" + PageNames[i]);
            UIKit.Place(container, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1500f, 640f));
            pages[i] = container;
        }

        BuildAudio(pages[0]);
        BuildBindings(pages[1]);
        BuildSpellHold(pages[2]);

        var backButton = UIKit.MakeButton(parent, "Back", new Vector2(260f, 70f), () => { Cancel(); back?.Invoke(); }, 28);
        UIKit.Place((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(260f, 70f));

        ShowPage(0);
    }

    // call when the screen is opened
    public void Open()
    {
        Cancel();
        volumeSlider.SetValueWithoutNotify(PlayerProfile.Volume);
        volumeText.text = Mathf.RoundToInt(PlayerProfile.Volume * 100f) + "%";
        RefreshBindings();
        RefreshHold();
        ShowPage(0);
    }

    void ShowPage(int index)
    {
        Cancel();
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].gameObject.SetActive(i == index);
            UIKit.SetChosen(tabs[i], i == index);
        }
    }

    // ---------- audio ----------

    void BuildAudio(RectTransform page)
    {
        var label = UIKit.Label(page, "VolumeLabel", "Master Volume", 40, Color.white, TextAnchor.MiddleLeft, true);
        UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 70f), new Vector2(600f, 60f));

        volumeText = UIKit.Label(page, "VolumeValue", "", 40, UIKit.Accent, TextAnchor.MiddleRight, true);
        UIKit.Place(volumeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, 70f), new Vector2(240f, 60f));

        volumeSlider = UIKit.MakeSlider(page, new Vector2(900f, 34f), PlayerProfile.Volume, v =>
        {
            PlayerProfile.Volume = v;
            volumeText.text = Mathf.RoundToInt(v * 100f) + "%";
        });
        UIKit.Place((RectTransform)volumeSlider.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 34f));
        volumeText.text = Mathf.RoundToInt(PlayerProfile.Volume * 100f) + "%";
    }

    // ---------- key bindings ----------

    void BuildBindings(RectTransform page)
    {
        bindHint = UIKit.Label(page, "Hint", "Click a key, then press the new one (Esc cancels). A key that is already used swaps places.", 26, UIKit.Dim);
        UIKit.Place(bindHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1500f, 44f));

        UIKit.MakeScroll(page, "Bindings", new Vector2(1400f, 470f), out var rows);
        UIKit.Place((RectTransform)rows.parent.parent, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1400f, 470f));

        foreach (var bind in PlayerSettings.Binds)
        {
            string id = bind.id;
            var row = UIKit.Box(rows, "Row_" + id, UIKit.PanelColor);
            row.rectTransform.sizeDelta = new Vector2(1380f, 64f);

            var name = UIKit.Label(row.transform, "Name", bind.label, 32, Color.white, TextAnchor.MiddleLeft, true);
            UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(660f, 56f));

            var button = UIKit.MakeButton(row.transform, "", new Vector2(360f, 50f), () => BeginCapture(id), 28);
            UIKit.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(360f, 50f));
            keyLabels[id] = button.GetComponentInChildren<Text>();
        }

        var reset = UIKit.MakeButton(page, "Reset to defaults", new Vector2(420f, 64f), () => { Cancel(); PlayerSettings.ResetBindings(); RefreshBindings(); }, 28);
        UIKit.Place((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 64f));
    }

    void BeginCapture(string id)
    {
        Cancel();
        capturingId = id;
        keyLabels[id].text = "Press a key...";
        keyLabels[id].color = UIKit.Gold;

        PlayerSettings.Capture(path =>
        {
            capturingId = null;
            PlayerSettings.SetPath(id, path);
            RefreshBindings();
        }, () =>
        {
            capturingId = null;
            RefreshBindings();
        });
    }

    // closes an unfinished capture (switching page, going back)
    void Cancel()
    {
        if (capturingId == null) return;
        capturingId = null;
        RefreshBindings();
    }

    void RefreshBindings()
    {
        foreach (var kv in keyLabels)
        {
            kv.Value.text = PlayerSettings.LabelOf(kv.Key);
            kv.Value.color = Color.white;
        }
    }

    // ---------- spell hold ----------

    void BuildSpellHold(RectTransform page)
    {
        var title = UIKit.Label(page, "Title", "Chord hold", 48, Color.white, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1200f, 70f));

        var info = UIKit.Label(page, "Info",
            "Some keyboards cannot register many keys held at the same time.\n\n" +
            "ON:  let go of a spell key while another key is still down (and before its spell is ready) and it still counts as held.\n" +
            "E + R, let go of E, then press T is cast as E + R + T. Everything is cast when the last key comes up.\n\n" +
            "OFF:  a spell only counts while its key is physically down.",
            30, UIKit.Dim, TextAnchor.UpperCenter);
        UIKit.Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1300f, 380f));

        holdToggle = UIKit.MakeButton(page, "", new Vector2(520f, 84f), () => { PlayerSettings.ChordHold = !PlayerSettings.ChordHold; RefreshHold(); }, 34);
        UIKit.Place((RectTransform)holdToggle.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(520f, 84f));
        holdState = holdToggle.GetComponentInChildren<Text>();
        RefreshHold();
    }

    void RefreshHold()
    {
        bool on = PlayerSettings.ChordHold;
        holdState.text = "Chord hold: " + (on ? "ON" : "OFF");
        UIKit.SetChosen(holdToggle, on);
    }
}
