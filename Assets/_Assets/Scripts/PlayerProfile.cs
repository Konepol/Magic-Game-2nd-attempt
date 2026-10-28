using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// What the player has chosen and achieved, kept between sessions: the spells in each slot, the master volume and the best time per stage.
public static class PlayerProfile
{
    public const string MenuScene = "MainMenu";

    const string LoadoutKey = "loadout";
    const string VolumeKey = "volume";
    const string EmptySlot = "-";

    // the stage being played (set by the stage select, or worked out from the scene when a stage is opened directly)
    public static int CurrentStage { get; set; } = -1;

    // ---------- volume ----------

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            ApplyVolume();
        }
    }

    public static void ApplyVolume() => AudioListener.volume = Volume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        ApplyVolume();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyVolume();

    // ---------- loadout ----------

    // one spell asset name per slot, "" for an empty slot
    public static string[] GetLoadout(GameCatalog catalog)
    {
        int n = catalog != null ? catalog.slotCount : 4;
        var result = new string[n];
        string saved = PlayerPrefs.GetString(LoadoutKey, null);

        string[] source = null;
        if (!string.IsNullOrEmpty(saved)) source = saved.Split(';');
        else if (catalog != null && catalog.defaultLoadout != null) source = catalog.defaultLoadout;

        var used = new HashSet<string>();
        for (int i = 0; i < n; i++)
        {
            string name = source != null && i < source.Length ? source[i] : "";
            if (name == EmptySlot || catalog == null || catalog.FindSpell(name) == null || !used.Add(name)) name = "";
            result[i] = name;
        }
        return result;
    }

    public static void SetLoadout(string[] names)
    {
        var parts = new string[names.Length];
        for (int i = 0; i < names.Length; i++) parts[i] = string.IsNullOrEmpty(names[i]) ? EmptySlot : names[i];
        PlayerPrefs.SetString(LoadoutKey, string.Join(";", parts));
    }

    // the weapon the player carries into a stage, made from the chosen spells
    public static WeaponData BuildWeapon()
    {
        var catalog = GameCatalog.Load();
        if (catalog == null) return null;

        string[] names = GetLoadout(catalog);
        var w = ScriptableObject.CreateInstance<WeaponData>();
        w.name = "Loadout";
        w.displayName = "Loadout";
        w.spells = new SpellData[names.Length];
        for (int i = 0; i < names.Length; i++) w.spells[i] = catalog.FindSpell(names[i]);
        w.combos = catalog.combos;
        w.allowMulticast = catalog.allowMulticast;
        w.multicastDelay = catalog.multicastDelay;
        w.maxMulticast = catalog.maxMulticast;
        return w;
    }

    // ---------- records ----------

    public static float GetBestTime(int stage) => PlayerPrefs.GetFloat("best_" + stage, -1f);

    // returns true if this is a new best
    public static bool SubmitTime(int stage, float seconds)
    {
        float best = GetBestTime(stage);
        if (best >= 0f && seconds >= best) return false;
        PlayerPrefs.SetFloat("best_" + stage, seconds);
        PlayerPrefs.Save();
        return true;
    }

    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return minutes + ":" + rest.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    // stage index for the scene that is open, if the catalog lists it
    public static int StageOfScene(GameCatalog catalog, string sceneName)
    {
        if (catalog == null || catalog.stages == null) return -1;
        for (int i = 0; i < catalog.stages.Length; i++)
            if (catalog.stages[i] != null && catalog.stages[i].sceneName == sceneName) return i;
        return -1;
    }
}
