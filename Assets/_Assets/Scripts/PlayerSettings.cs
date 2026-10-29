using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

// The player's own settings that are not the sound volume: which key does what, and how holding spells works.
// Bindings are saved by name; anything that listens to a key applies them again when Changed is raised.
public static class PlayerSettings
{
    public struct Bind
    {
        public string id, label, defaultPath;
        public Bind(string id, string label, string defaultPath) { this.id = id; this.label = label; this.defaultPath = defaultPath; }
    }

    // everything that can be rebound, in the order the Options screen lists it
    public static readonly Bind[] Binds =
    {
        new Bind("spell0", "Spell slot 1", "<Mouse>/leftButton"),
        new Bind("spell1", "Spell slot 2", "<Mouse>/rightButton"),
        new Bind("spell2", "Spell slot 3", "<Keyboard>/q"),
        new Bind("spell3", "Spell slot 4", "<Keyboard>/e"),
        new Bind("spell4", "Spell slot 5", "<Keyboard>/r"),
        new Bind("spell5", "Spell slot 6", "<Keyboard>/t"),
        new Bind("spell6", "Spell slot 7", "<Keyboard>/g"),
        new Bind("combine", "Combine spells (queue)", "<Keyboard>/b"),
        new Bind("charge", "Charge mana", "<Keyboard>/c"),
        new Bind("barrier", "Barrier", "<Keyboard>/f"),
        new Bind("broom", "Broom (mount / dismount)", "<Keyboard>/x"),
        new Bind("lock", "Lock on to a target", "<Keyboard>/v"),
    };

    public static event Action Changed;                    // a binding or an option changed: listeners apply them again

    public static bool Capturing { get; private set; }     // waiting for the player to press the new key
    static int captureEndFrame = -10;

    // true while waiting for a key and for a moment after, so the same Esc press that cancelled it does not also close the screen
    public static bool CaptureBusy => Capturing || Time.frameCount - captureEndFrame <= 1;

    // ---------- spell hold option ----------

    // Shows the fusion paths diagram in the bottom-left while spells are held.
    public static bool FusionPaths
    {
        get => PlayerPrefs.GetInt("fusionPaths", 1) == 1;
        set
        {
            PlayerPrefs.SetInt("fusionPaths", value ? 1 : 0);
            Changed?.Invoke();
        }
    }

    // On: a spell key that is let go while another key of the chord is still down, and before its spell is ready, keeps counting as held,
    // so chords can be built one key at a time on keyboards that cannot hold many keys at once. Off: a key counts only while it is down.
    public static bool ChordHold
    {
        get => PlayerPrefs.GetInt("chordHold", 1) == 1;
        set
        {
            PlayerPrefs.SetInt("chordHold", value ? 1 : 0);
            Changed?.Invoke();
        }
    }

    // ---------- key bindings ----------

    static string Key(string id) => "bind_" + id;

    static string DefaultPath(string id)
    {
        foreach (var b in Binds) if (b.id == id) return b.defaultPath;
        return null;
    }

    public static string GetPath(string id)
    {
        string fallback = DefaultPath(id);
        return fallback == null ? null : PlayerPrefs.GetString(Key(id), fallback);
    }

    static void Store(string id, string path)
    {
        if (path == DefaultPath(id)) PlayerPrefs.DeleteKey(Key(id));
        else PlayerPrefs.SetString(Key(id), path);
    }

    // gives an action the key chosen for it (or its own default if none was chosen)
    public static void Apply(string id, InputAction action)
    {
        if (action == null || action.bindings.Count == 0 || DefaultPath(id) == null) return;

        if (PlayerPrefs.HasKey(Key(id))) action.ApplyBindingOverride(0, PlayerPrefs.GetString(Key(id)));
        else action.RemoveBindingOverride(0);
    }

    // a key can only do one thing: if another action already has it, the two swap keys
    public static void SetPath(string id, string path)
    {
        string old = GetPath(id);
        if (old == null || old == path) return;

        foreach (var b in Binds)
            if (b.id != id && GetPath(b.id) == path) Store(b.id, old);

        Store(id, path);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetBindings()
    {
        foreach (var b in Binds) PlayerPrefs.DeleteKey(Key(b.id));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // "<Mouse>/leftButton" -> "LMB", "<Keyboard>/e" -> "E"
    public static string Label(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";
        if (path.EndsWith("/leftButton")) return "LMB";
        if (path.EndsWith("/rightButton")) return "RMB";
        if (path.EndsWith("/middleButton")) return "MMB";
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    public static string LabelOf(string id) => Label(GetPath(id));

    // the label of whatever key an action ended up with
    public static string LabelOf(InputAction action) =>
        action != null && action.bindings.Count > 0 ? Label(action.bindings[0].effectivePath) : "-";

    // waits for the next keyboard key or mouse button; Escape cancels
    public static void Capture(Action<string> picked, Action cancelled)
    {
        Capturing = true;
        WaitForPress(picked, cancelled);
    }

    static void WaitForPress(Action<string> picked, Action cancelled)
    {
        InputSystem.onAnyButtonPress.CallOnce(control =>
        {
            if (!(control.device is Keyboard) && !(control.device is Mouse)) { WaitForPress(picked, cancelled); return; }

            if (control.device is Keyboard && control.path.EndsWith("/escape"))
            {
                Capturing = false;
                captureEndFrame = Time.frameCount;
                cancelled?.Invoke();
                return;
            }

            string path = "<" + control.device.layout + ">" + control.path.Substring(control.path.IndexOf('/', 1));
            Capturing = false;
            captureEndFrame = Time.frameCount;
            picked?.Invoke(path);
        });
    }
}
