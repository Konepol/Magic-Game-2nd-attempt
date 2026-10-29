using System.Collections.Generic;

// While any owner has the aura on, the player cannot stay on the broom or mount it (the boss uses it while winding up its slam).
public static class GroundingAura
{
    static readonly HashSet<object> owners = new HashSet<object>();

    public static bool Active => owners.Count > 0;

    public static void Set(object owner, bool on)
    {
        if (on) owners.Add(owner);
        else owners.Remove(owner);
    }

    public static void Clear() => owners.Clear();
}
