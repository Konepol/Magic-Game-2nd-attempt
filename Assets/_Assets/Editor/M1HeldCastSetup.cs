using UnityEditor;
using UnityEngine;

// M1 part 8: a throwaway fusion recipe (Weaver + Lantern) so held fusion can be tried, and bank size controls for testing.
// Does not overwrite assets that already exist.
public static class M1HeldCastSetup
{
    const string Dir = "Assets/_Assets/Spells/";
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";

    [MenuItem("Tools/Magic/M1 Part 8: Add test fusion (Weaver + Lantern)")]
    public static void AddTestFusion()
    {
        var weaver = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "Weaver.asset");
        var lantern = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "Lantern.asset");
        if (weaver == null || lantern == null) { Debug.LogError("Run Tools > Magic > M1 Parts 3-7 first."); return; }

        var result = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "TestFusion.asset");
        if (result == null)
        {
            result = ScriptableObject.CreateInstance<SpellData>();
            EditorUtility.CopySerialized(weaver, result);
            result.displayName = "Beaconweave";
            result.roleLabel = "Test fusion";
            result.isFusion = true;
            result.fusedEquipCostMultiplier = 1.5f;
            result.hasHoldVariant = true;
            result.holdThreshold = 1.2f;
            result.holdDisplayName = "Beaconweave";
            result.hold = result.hold.Clone();
            result.hold.damage = 70f;
            result.hold.manaCost = 55f;
            result.hold.projectileCount = 3;
            result.hold.effectColor = new Color(1f, 0.8f, 0.6f);
            result.chant = new[] { "Light and thread, bind them where they stand." };
            result.extraTiers = new ChargeTier[0];
            result.passive = new SpellPassive();
            AssetDatabase.CreateAsset(result, Dir + "TestFusion.asset");
        }

        var combo = AssetDatabase.LoadAssetAtPath<SpellCombo>(Dir + "Fusions/Combo_TestFusion.asset");
        if (combo == null)
        {
            combo = ScriptableObject.CreateInstance<SpellCombo>();
            combo.ingredients = new[] { weaver, lantern };
            combo.result = result;
            AssetDatabase.CreateAsset(combo, Dir + "Fusions/Combo_TestFusion.asset");
        }

        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog != null)
        {
            var list = new System.Collections.Generic.List<SpellCombo>(catalog.combos ?? new SpellCombo[0]);
            if (!list.Contains(combo)) list.Add(combo);
            catalog.combos = list.ToArray();
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Test fusion ready: Weaver + Lantern = Beaconweave.");
    }

    [MenuItem("Tools/Magic/Held bank: raise by 1 (testing)")]
    public static void RaiseBank()
    {
        HeldCast.RaiseBank();
        Debug.Log("Held bank size is now " + HeldCast.BankSize);
    }

    [MenuItem("Tools/Magic/Held bank: reset to 1 (testing)")]
    public static void ResetBank()
    {
        HeldCast.BankSize = 1;
        Debug.Log("Held bank size is now 1");
    }
}
