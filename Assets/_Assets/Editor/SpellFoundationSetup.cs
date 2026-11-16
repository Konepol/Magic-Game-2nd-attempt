using UnityEditor;
using UnityEngine;

// M1 part 1: takes the old spells out of the game catalog (the assets stay in the project) and adds a throwaway test spell
// that exercises forms, the passive, the output limit and the fused-equip cost.
// Safe to run again: it never overwrites an existing TestSpell (so Inspector tweaks stay).
public static class SpellFoundationSetup
{
    const string TestPath = "Assets/_Assets/Spells/TestSpell.asset";
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";

    [MenuItem("Tools/Magic/M1 Part 1: Set up test spell and clear old spell list")]
    public static void Run()
    {
        var test = AssetDatabase.LoadAssetAtPath<SpellData>(TestPath);
        if (test == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<SpellData>("Assets/_Assets/Spells/StarBolt.asset");
            test = ScriptableObject.CreateInstance<SpellData>();
            if (source != null) EditorUtility.CopySerialized(source, test);

            test.displayName = "Test Spell";
            test.roleLabel = "Foundation test";
            test.fxStyle = "ArcaneBolt";
            test.chant = new[] { "Test, test, one two." };

            // output does something different to each form: the tap gets faster and fires more bolts, the beam gets wider and longer
            test.tap.output = new OutputResponse { speedExp = 0.6f, extraProjectilesPerOutput = 2f, damageExp = 0.3f };
            test.tap.damage = 8f; test.tap.manaCost = 3f; test.tap.cooldown = 0.25f;

            test.hasHoldVariant = true;
            test.holdThreshold = 0.5f;
            test.hold.damage = 30f; test.hold.manaCost = 12f;
            test.holdDisplayName = "Test Bolt";

            // a second form reached by holding longer: not a bigger bolt, a laser
            var form = new ChargeTier
            {
                displayName = "Test Beam", time = 1.6f, ownForm = true, manaMultiplier = 1f,
                chant = new[] { "Now I become a beam." },
            };
            form.form = new SpellVariant
            {
                kind = SpellKind.Laser, damage = 45f, manaCost = 28f, cooldown = 1.2f, laserRange = 70f, laserWidth = 0.7f,
                laserDuration = 0.35f, laserPierces = true, output = new OutputResponse { sizeExp = 0.8f, reachExp = 0.4f }, effectColor = new Color(0.4f, 1f, 0.8f),
            };
            test.extraTiers = new[] { form };

            test.passive = new SpellPassive
            {
                enabled = true, displayName = "Test Surge", manaStyle = PassiveManaStyle.PerActivation, activationCost = 4f,
                damageMultiplier = 1.5f, description = "Each cast hits 50% harder and costs 4 extra mana.",
            };
            test.outputLimit = 1.5f;
            test.backfireDamage = 12f;

            AssetDatabase.CreateAsset(test, TestPath);
            Debug.Log("Created " + TestPath);
        }

        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog == null) { Debug.LogError("GameCatalog not found at " + CatalogPath); return; }

        // the old spells stay in the project, only the catalog (what the game offers) changes
        catalog.spells = new[] { test };
        catalog.combos = new SpellCombo[0];
        catalog.defaultLoadout = new[] { test.name };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Catalog now lists only: " + test.name + ". Old spell assets are untouched.");
    }
}
