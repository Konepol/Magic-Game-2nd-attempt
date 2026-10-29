using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Options screen, with a page for each kind of option: Audio, Key bindings and Gameplay.
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

    static readonly string[] PageNames = { "Audio", "Key bindings", "Gameplay" };

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
        BuildGameplay(pages[2]);

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
        RefreshGameplay();
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

    // ---------- gameplay ----------

    Text pathsState, hintText;
    Button pathsToggle;
    const string HintIdle = "Hover over an option to see what it does.";

    void BuildGameplay(RectTransform page)
    {
        hintText = UIKit.Label(page, "Hint", HintIdle, 30, UIKit.Dim, TextAnchor.UpperCenter);
        UIKit.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1300f, 230f));

        pathsToggle = UIKit.MakeButton(page, "", new Vector2(640f, 90f), () => { PlayerSettings.FusionPaths = !PlayerSettings.FusionPaths; RefreshGameplay(); }, 34);
        UIKit.Place((RectTransform)pathsToggle.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(640f, 90f));
        pathsState = pathsToggle.GetComponentInChildren<Text>();
        Hint(pathsToggle.gameObject,
            "While you hold a spell key, a diagram in the bottom-left shows the fusions it can lead to: " +
            "each spell you add moves the group up and renames it to what it becomes. Paths that do not fit are cut at the edge of the box.");

        RefreshGameplay();
    }

    void Hint(GameObject target, string message)
    {
        var hover = target.AddComponent<HoverHint>();
        hover.target = hintText;
        hover.message = message;
        hover.idleMessage = HintIdle;
    }

    void RefreshGameplay()
    {
        bool paths = PlayerSettings.FusionPaths;
        pathsState.text = "Fusion paths: " + (paths ? "ON" : "OFF");
        UIKit.SetChosen(pathsToggle, paths);

        if (hintText != null) hintText.text = HintIdle;
    }
}
