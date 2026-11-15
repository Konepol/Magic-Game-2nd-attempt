using System;

// Which spells have their passive part toggled on. Kept in the player's save, toggled from the spell list.
// Whether a toggled-on passive is actually working right now (it may be starved of mana) is decided by PlayerSpellCaster.
public static class SpellPassives
{
    static string Key(SpellData spell) => "passive_" + spell.name;

    public static event Action<SpellData> Changed;

    public static bool IsOn(SpellData spell) => spell != null && spell.HasPassive && PlayerSave.GetInt(Key(spell), 0) == 1;

    public static void Set(SpellData spell, bool on)
    {
        if (spell == null || !spell.HasPassive) return;
        PlayerSave.SetInt(Key(spell), on ? 1 : 0);
        PlayerSave.Save();
        Changed?.Invoke(spell);
    }

    public static void Toggle(SpellData spell) => Set(spell, !IsOn(spell));
}
