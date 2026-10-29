using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Add Spell Chants
// Writes the casting chant of every spell and fusion (the sentences shown above the player's head while it is cast).
// They live on each spell asset (Chant) and on charge tiers, so they can be rewritten in the Inspector. Running this again overwrites them.
public static class ChantBuilder
{
    static readonly Dictionary<string, string[]> Chants = new Dictionary<string, string[]>
    {
        // spells
        { "ArcaneBolt", new[] { "Threads of mana, gather in my palm", "Take shape, and strike true." } },
        { "ScatterShot", new[] { "Shards of the sky, scatter wide", "Let none slip between you", "Fall like rain." } },
        { "FireBurst", new[] { "Ember, awaken", "Burn with the breath of the sun", "Bloom, and consume." } },
        { "SparkStream", new[] { "Sparks, sing for me." } },
        { "LightningLance", new[] { "Storm above, hear my call", "Lend me your spear", "Pierce the dark." } },
        { "MeteorStrike", new[] { "Stars that sleep beyond the veil", "Break your silence", "Fall upon this earth!" } },
        { "FrostShard", new[] { "Cold of the deepest winter", "Pierce, and still." } },
        { "WindBlade", new[] { "Wind, sharpen your edge", "Cut without a sound." } },
        { "EarthSpike", new[] { "Stone beneath, rise and strike", "Shatter all that stands." } },
        { "VenomDart", new[] { "Silent thorn, drink the dark", "Sting, and spread." } },
        { "RadiantBeam", new[] { "Light of the dawn, gather", "Pierce every shadow." } },

        // fusions
        { "InfernalBolt", new[] { "Flame and arcane, be one", "Burn through the sky!" } },
        { "CinderStorm", new[] { "Embers scatter, ash take flight", "Storm of cinders, rise!" } },
        { "FirestormLance", new[] { "Fire and lightning, wed in the sky", "Pierce, and burn, and burn!" } },
        { "ThunderMeteor", new[] { "Thunder, carry the falling star", "Strike the earth as one!" } },
        { "GlacierBolt", new[] { "Ice and mana, freeze the air", "Glacier, fall." } },
        { "BlizzardLance", new[] { "Storm and frost, bind the wind", "Pierce with winter's fang." } },
        { "Cyclone", new[] { "Wind and flame, spin as one", "Cyclone, devour!" } },
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
            if (spell == null || !Chants.TryGetValue(spell.name, out var lines)) continue;

            spell.chant = lines;
            if (spell.name == "MeteorStrike" && spell.extraTiers != null && spell.extraTiers.Length > 0)
                spell.extraTiers[0].chant = ApocalypseChant;
            EditorUtility.SetDirty(spell);
            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Chants written for " + n + " spells and fusions.");
    }
}
