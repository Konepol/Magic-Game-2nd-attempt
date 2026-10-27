using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Weapon", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject
{
    public string displayName = "New Weapon";

    [Tooltip("Spell per skill slot, in order (slot 1 first). Leave a slot empty to leave it unused.")]
    public SpellData[] spells = new SpellData[4];

    public SpellData GetSpell(int slot) => slot >= 0 && slot < spells.Length ? spells[slot] : null;
}
