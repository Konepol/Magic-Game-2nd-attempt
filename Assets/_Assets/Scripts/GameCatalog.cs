using System;
using UnityEngine;

[Serializable]
public class StageInfo
{
    public string displayName = "Stage 1";
    [TextArea] public string description;
    [Tooltip("Name of the scene that holds this stage (it must be in the build settings).")]
    public string sceneName = "Scene1";
}

// Everything the main menu and the loadout need to know about: the spells you can equip, the recipes between them, and the stage list.
// Lives in a Resources folder so any scene can load it.
[CreateAssetMenu(menuName = "Magic/Game Catalog", fileName = "GameCatalog")]
public class GameCatalog : ScriptableObject
{
    [Header("Spells")]
    [Tooltip("Every spell the player can pick in Customize. The barrier is part of the base kit and is not listed here.")]
    public SpellData[] spells;
    [Tooltip("Recipes that fuse held spells into a new one. They work with whatever the player has equipped.")]
    public SpellCombo[] combos;
    [Min(1)] public int slotCount = 4;
    [Tooltip("Asset names of the spells equipped the first time (one per slot, empty = nothing).")]
    public string[] defaultLoadout;

    [Header("Casting several spells at once")]
    public bool allowMulticast = true;
    public float multicastDelay = 0.15f;
    [Min(2)] public int maxMulticast = 4;

    [Header("Stages")]
    public StageInfo[] stages;

    [Header("Shared assets (were loaded by name from Resources)")]
    public GameObject wardenModel;
    public Material castingCircleMaterial;
    public SpellFXLibrary spellFXLibrary;
    [Tooltip("The health bar a barrier shows once damaged (the shared EnemyHealthBar prefab, built in the editor).")]
    public GameObject healthBarPrefab;
    [Tooltip("Menu canvases baked by the builders, found by prefab name (MainMenuCanvas, PauseMenu, DeathScreen, StageResults).")]
    public GameObject[] uiPrefabs;

    public const string ResourcePath = "GameCatalog";

    static GameCatalog cached;

    // the one asset that still has to be found by name; everything else is reached through its fields
    public static GameCatalog Load()
    {
        if (cached == null) cached = Resources.Load<GameCatalog>(ResourcePath);
        return cached;
    }

    public GameObject FindUIPrefab(string prefabName)
    {
        if (uiPrefabs == null) return null;
        foreach (var p in uiPrefabs) if (p != null && p.name == prefabName) return p;
        return null;
    }

    public SpellData FindSpell(string assetName)
    {
        if (spells == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var s in spells) if (s != null && s.name == assetName) return s;
        return null;
    }
}
