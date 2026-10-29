using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Add Spell Chants
// Writes the casting chant of the big fusions (three or more spells) and clears it from everything else (the sentences shown above the player's head while it is cast).
// They live on each spell asset (Chant) and on charge tiers, so they can be rewritten in the Inspector. Running this again overwrites them.
public static class ChantBuilder
{
    static readonly Dictionary<string, string[]> Chants = new Dictionary<string, string[]>
    {
        // only the big fusions (three or more spells) are chanted
        { "TremorStorm", new[] { "Earth, star and shards combine", "Shake the ground to dust", "Tremor storm, arise!" } },
        { "PlagueStorm", new[] { "Venom and ember, rot and burn", "Let the plague bloom,", "and none be spared." } },
        { "AbsoluteZero", new[] { "Frost, stone, light and star", "Four forces, one stillness", "All motion ends.", "Absolute zero." } },
        { "SunLance", new[] { "Dawn, and storm, and fire, unite", "Become the sun's own spear!" } },
    };

    // the extra sentences of Meteor Strike while it is charged on to its second threshold
    static readonly string[] ApocalypseChant = { "Heavens, crack and bleed fire", "End all things beneath your light!" };

    [MenuItem("Tools/Magic/Add Spell Chants")]
    public static void Build()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:SpellData", new[] { "Assets/_Assets/Weapons" }))
        {
            var spell = AssetDatabase.LoadAssetAtPath<SpellData>(AssetDatabase.GUIDToAssetPath(guid));
            if (spell == null) continue;

            spell.chant = Chants.TryGetValue(spell.name, out var lines) ? lines : new string[0];
            if (spell.name == "MeteorStrike" && spell.extraTiers != null && spell.extraTiers.Length > 0)
                spell.extraTiers[0].chant = ApocalypseChant;
            EditorUtility.SetDirty(spell);
            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Chants written for " + n + " spells and fusions.");
    }
}
