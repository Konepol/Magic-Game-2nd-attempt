using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Everything the player keeps between sessions (volumes, spells in each slot, key bindings, best times, options), as one
// ScriptableObject at Resources/PlayerSave. It replaces PlayerPrefs, and works like it: GetFloat / SetString / HasKey ...
//
//  - In the editor the asset itself is used, so you can read and change a "save" in the Inspector (every value is one row in Entries).
//  - In a build the asset is only the default. The player's changes are kept in a copy that is written to
//    <persistent data path>/player_save.json and read back on the next start.
//  - Before every build the asset and that file are wiped, so a build never ships with the saved data of whoever tested it
//    (see PlayerSaveBuildWipe).
[CreateAssetMenu(menuName = "Magic/Player Save", fileName = "PlayerSave")]
public class PlayerSave : ScriptableObject
{
    [Serializable]
    public struct Entry { public string key; public string value; }

    [Tooltip("One row per saved value. Delete a row (or use Wipe in the menu) to go back to the default of that value.")]
    public List<Entry> entries = new List<Entry>();

    const string ResourcePath = "PlayerSave";
    public const string FileName = "player_save.json";

    static PlayerSave instance;

    public static PlayerSave Instance
    {
        get
        {
            if (instance != null) return instance;
            var asset = Resources.Load<PlayerSave>(ResourcePath);
            if (asset == null) asset = CreateInstance<PlayerSave>();     // the asset was deleted: start empty
#if UNITY_EDITOR
            instance = asset;                                            // edit the asset directly
#else
            instance = Instantiate(asset);                               // a copy: the asset stays the default
            instance.ReadFile();
#endif
            return instance;
        }
    }

    // a fresh play session in the editor must not keep an instance from the last one
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // ---------- the PlayerPrefs-like part ----------

    int IndexOf(string key)
    {
        for (int i = 0; i < entries.Count; i++) if (entries[i].key == key) return i;
        return -1;
    }

    public static bool HasKey(string key) => Instance.IndexOf(key) >= 0;

    public static string GetString(string key, string fallback = "")
    {
        var s = Instance;
        int i = s.IndexOf(key);
        return i >= 0 ? s.entries[i].value : fallback;
    }

    public static void SetString(string key, string value)
    {
        var s = Instance;
        int i = s.IndexOf(key);
        var e = new Entry { key = key, value = value };
        if (i >= 0) s.entries[i] = e; else s.entries.Add(e);
        s.MarkChanged();
    }

    public static float GetFloat(string key, float fallback = 0f)
        => float.TryParse(GetString(key, null), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    public static void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));

    public static int GetInt(string key, int fallback = 0)
        => int.TryParse(GetString(key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    public static void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));

    public static void DeleteKey(string key)
    {
        var s = Instance;
        int i = s.IndexOf(key);
        if (i < 0) return;
        s.entries.RemoveAt(i);
        s.MarkChanged();
    }

    // ---------- keeping it ----------

    void MarkChanged()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // writes the file in a build (in the editor the asset is saved with the project)
    public static void Save()
    {
#if UNITY_EDITOR
        if (instance != null) UnityEditor.EditorUtility.SetDirty(instance);
#else
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(Instance)); }
        catch (Exception e) { Debug.LogWarning("Could not save the player's data: " + e.Message); }
#endif
    }

#if !UNITY_EDITOR
    void ReadFile()
    {
        try { if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), this); }
        catch (Exception e) { Debug.LogWarning("Could not read the player's data: " + e.Message); }
    }
#endif

    // everything back to the defaults (also removes the saved file)
    [ContextMenu("Wipe")]
    public void Wipe()
    {
        entries.Clear();
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (Exception) { }
        MarkChanged();
    }
}
