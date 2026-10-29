using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc during a stage: freezes the game and shows Resume, Restart, volume and Quit to main menu, with the stage's statistics so far and
// the spells you have equipped (and the combinations they allow). Lives next to the StageRunner, which adds it by itself.
public class PauseMenu : MonoBehaviour
{
    StageRunner runner;
    GameCatalog catalog;
    GameObject root;
    Text statsText, loadoutText, fusionText;
    RectTransform optionsRoot;
    OptionsMenu options;
    bool paused;

    // what was switched off while paused, so it can be switched back on
    PlayerInput playerInput;
    bool inputWasOn;
    readonly List<PlayerSpellCaster> casters = new List<PlayerSpellCaster>();
    StarterAssetsInputs inputs;

    void Awake()
    {
        runner = GetComponent<StageRunner>();
        catalog = GameCatalog.Load();
    }

    void OnDestroy() => Time.timeScale = 1f;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (PlayerSettings.CaptureBusy) return;                 // that Esc only cancelled picking a key

        if (optionsRoot != null && optionsRoot.gameObject.activeSelf) { CloseOptions(); return; }

        if (paused) Resume();
        else if (runner == null || (!runner.ResultsShown && !runner.PlayerDead)) Pause();
    }

    // ---------- pausing ----------

    void Pause()
    {
        paused = true;
        Time.timeScale = 0f;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            inputs = player.GetComponentInParent<StarterAssetsInputs>();
            if (inputs != null)
            {
                inputs.MoveInput(Vector2.zero);
                inputs.LookInput(Vector2.zero);
                inputs.JumpInput(false);
                inputs.SprintInput(false);
                inputs.cursorLocked = false;
            }

            playerInput = player.GetComponentInParent<PlayerInput>();
            inputWasOn = playerInput != null && playerInput.enabled;
            if (playerInput != null) playerInput.enabled = false;

            casters.Clear();
            foreach (var c in player.GetComponentsInChildren<PlayerSpellCaster>())
                if (c.enabled) { casters.Add(c); c.enabled = false; }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (root == null) Build();
        Refresh();
        root.SetActive(true);
    }

    void Resume()
    {
        paused = false;
        Time.timeScale = 1f;
        if (optionsRoot != null) optionsRoot.gameObject.SetActive(false);
        if (root != null) root.SetActive(false);

        if (playerInput != null && inputWasOn) playerInput.enabled = true;
        foreach (var c in casters) if (c != null) c.enabled = true;
        casters.Clear();

        if (inputs != null) inputs.cursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- screen ----------

    void Build()
    {
        UIKit.EnsureEventSystem();
        var canvas = UIKit.MakeCanvas("PauseMenu", 150);
        root = canvas.gameObject;
        root.transform.SetParent(transform, false);

        var dim = UIKit.Box(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        UIKit.Stretch(dim.rectTransform);

        var panel = UIKit.Box(canvas.transform, "Panel", UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 820f));

        var title = UIKit.Label(panel.transform, "Title", "PAUSED", 80, UIKit.Gold, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(1300f, 110f));

        // left: the buttons and the volume
        string[] names = { "Resume", "Restart Stage", "Options", "Quit to Main Menu" };
        System.Action[] actions = { Resume, Restart, OpenOptions, QuitToMenu };
        for (int i = 0; i < names.Length; i++)
        {
            var b = UIKit.MakeButton(panel.transform, names[i], new Vector2(480f, 84f), actions[i], 32);
            UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(330f, -230f - i * 104f), new Vector2(480f, 84f));
        }

        // the options open over the pause screen
        var overlay = UIKit.Box(canvas.transform, "OptionsOverlay", new Color(0.03f, 0.04f, 0.08f, 1f));
        UIKit.Stretch(overlay.rectTransform);
        optionsRoot = overlay.rectTransform;
        options = OptionsMenu.Build(optionsRoot, CloseOptions);
        optionsRoot.gameObject.SetActive(false);

        // right: what has happened so far, and what you are carrying
        var info = UIKit.Box(panel.transform, "Info", new Color(0.04f, 0.06f, 0.1f, 1f));
        UIKit.Place(info.rectTransform, new Vector2(1f, 0.5f), new Vector2(-450f, -40f), new Vector2(780f, 620f));

        var statsHead = UIKit.Label(info.transform, "StatsHead", "THIS STAGE SO FAR", 24, UIKit.Dim, TextAnchor.MiddleLeft, true);
        UIKit.Place(statsHead.rectTransform, new Vector2(0f, 1f), new Vector2(210f, -34f), new Vector2(380f, 36f));
        statsText = UIKit.Label(info.transform, "Stats", "", 28, Color.white, TextAnchor.UpperLeft);
        UIKit.Place(statsText.rectTransform, new Vector2(0f, 1f), new Vector2(380f, -170f), new Vector2(720f, 220f));

        var loadoutHead = UIKit.Label(info.transform, "LoadoutHead", "YOUR SPELLS", 24, UIKit.Dim, TextAnchor.MiddleLeft, true);
        UIKit.Place(loadoutHead.rectTransform, new Vector2(0f, 1f), new Vector2(150f, -318f), new Vector2(260f, 36f));
        loadoutText = UIKit.Label(info.transform, "Loadout", "", 26, Color.white, TextAnchor.UpperLeft);
        loadoutText.supportRichText = true;
        UIKit.Place(loadoutText.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -475f), new Vector2(330f, 260f));

        var fusionHead = UIKit.Label(info.transform, "FusionHead", "FUSIONS  (B, then the spells)", 24, UIKit.Dim, TextAnchor.MiddleLeft, true);
        UIKit.Place(fusionHead.rectTransform, new Vector2(0f, 1f), new Vector2(520f, -318f), new Vector2(460f, 36f));
        fusionText = UIKit.Label(info.transform, "Fusions", "", 24, Color.white, TextAnchor.UpperLeft);
        fusionText.supportRichText = true;
        fusionText.resizeTextForBestFit = true;
        fusionText.resizeTextMinSize = 13;
        fusionText.resizeTextMaxSize = 24;
        UIKit.Place(fusionText.rectTransform, new Vector2(0f, 1f), new Vector2(555f, -480f), new Vector2(470f, 270f));
    }

    void Refresh()
    {
        if (runner != null)
        {
            string text = "Time               " + PlayerProfile.FormatTime(runner.Elapsed)
                        + "\nDamage dealt       " + Mathf.RoundToInt(runner.DamageDealt)
                        + "\nDamage taken       " + Mathf.RoundToInt(runner.DamageTaken)
                        + "\nEnemies defeated   " + runner.TotalKills;
            if (runner.Kills.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in runner.Kills) parts.Add(kv.Key + " x" + kv.Value);
                parts.Sort();
                text += "\n<color=#B0B0B0>" + string.Join(",  ", parts) + "</color>";
            }
            statsText.supportRichText = true;
            statsText.text = text;
        }

        // the spells on each key, and the fusions those allow
        var caster = FindAnyObjectByType<PlayerSpellCaster>();
        var lines = new List<string>();
        var keyOf = new Dictionary<string, string>();
        var slotOf = new Dictionary<string, int>();
        if (caster != null)
            for (int i = 0; i < caster.SlotCount; i++)
            {
                var spell = caster.GetSpellAt(i);
                string key = caster.GetKeyLabel(i);
                if (spell != null) { keyOf[spell.name] = key; slotOf[spell.name] = i; }
                lines.Add("<color=#FFD15A>" + key + "</color>   " + (spell != null ? spell.displayName : "<color=#808080>empty</color>"));
            }
        loadoutText.text = string.Join("\n", lines);

        var fusions = new List<string>();
        if (catalog != null && catalog.combos != null)
            foreach (var c in catalog.combos)
            {
                if (c == null || c.result == null || c.ingredients == null) continue;
                var keys = new List<string>();
                var order = new List<int>();
                bool all = true;
                foreach (var ing in c.ingredients)
                {
                    if (ing == null) continue;
                    if (slotOf.TryGetValue(ing.name, out int sl)) order.Add(sl); else all = false;
                }
                order.Sort();                                       // in the order of the spell keys
                foreach (int sl in order) keys.Add(caster.GetKeyLabel(sl));
                if (all) fusions.Add("<color=#FFD15A>" + c.result.displayName + "</color>   <color=#9FD8FF>" + string.Join(" + ", keys) + "</color>");
            }
        fusionText.text = fusions.Count > 0 ? string.Join("\n", fusions) : "<color=#808080>none with these spells</color>";
    }

    void OpenOptions()
    {
        options.Open();
        optionsRoot.gameObject.SetActive(true);
    }

    void CloseOptions() => optionsRoot.gameObject.SetActive(false);

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void QuitToMenu()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(PlayerProfile.MenuScene)) SceneManager.LoadScene(PlayerProfile.MenuScene);
        else Resume();
    }
}
