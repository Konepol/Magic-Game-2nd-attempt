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

    public const string ResourcePath = "GameCatalog";

    public static GameCatalog Load() => Resources.Load<GameCatalog>(ResourcePath);

    public SpellData FindSpell(string assetName)
    {
        if (spells == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var s in spells) if (s != null && s.name == assetName) return s;
        return null;
    }
}
