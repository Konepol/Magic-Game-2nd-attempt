using System.Collections.Generic;
using UnityEngine;

// A designed combination: casting these spells together produces the Result spell instead.
// Order does not matter, and there can be two or more ingredients. The Result spell's HOLD version is what fires
// (or its Tap version if it has none); its own mana cost, cast time (Hold Threshold) and circle are the combination's.
// Pairs and groups without a recipe can still auto-combine (see WeaponData).
[CreateAssetMenu(menuName = "Magic/Spell Combo", fileName = "NewCombo")]
public class SpellCombo : ScriptableObject
{
    public SpellData[] ingredients = new SpellData[2];
    public SpellData result;

    public bool Matches(IList<SpellData> set)
    {
        if (ingredients == null || set == null || ingredients.Length != set.Count) return false;

        var remaining = new List<SpellData>(ingredients);
        foreach (var s in set)
            if (!remaining.Remove(s)) return false;
        return remaining.Count == 0;
    }
}
