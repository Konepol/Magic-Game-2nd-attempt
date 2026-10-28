using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Weapon", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject
{
    public string displayName = "New Weapon";

    [Tooltip("Spell per skill slot, in order (slot 1 first). Leave a slot empty to leave it unused.")]
    public SpellData[] spells = new SpellData[4];

    [Header("Combining spells (hold two or more spell keys together)")]
    [Tooltip("Designed combinations for this weapon.")]
    public SpellCombo[] combos;
    [Tooltip("Hold spells with no recipe can still be cast together: each keeps its own circle and charging bar, and they share the aim. Off = a second spell that cannot combine is ignored.")]
    [UnityEngine.Serialization.FormerlySerializedAs("allowAutoCombine")]
    public bool allowMulticast = true;
    [Tooltip("Casting several spells at once delays all of them: each extra spell adds this fraction to every cast time.")]
    public float multicastDelay = 0.3f;
    [Min(2)] public int maxMulticast = 3;

    public SpellData GetSpell(int slot) => slot >= 0 && slot < spells.Length ? spells[slot] : null;
}
