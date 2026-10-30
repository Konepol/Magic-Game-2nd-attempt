using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// Reuses projectiles instead of instantiating and destroying one per shot. A pool holds one kind of shot (same prefab, spell, colour and size) and the
// pools of one spell sit together under one group object in the hierarchy ("Projectile Pools / FireBurst"). The pools are sized from what is in play:
// the equipped weapon's spells (and the fusions it can make) for the player, the shooters of the current wave for enemies, and the boss's current
// colour for its shots. They are filled a couple of objects per frame, so equipping a weapon or starting a wave does not hitch. A pool that is asked
// for more than its size simply grows; a kind that is no longer needed is freed once its last shot has landed.
public static class ProjectilePool
{
    public enum Owner { Player, Enemy, Boss }

    public class Pool
    {
        internal readonly Stack<Component> free = new Stack<Component>();
        internal Func<Pool, Component> create;
        internal Key key;
        internal Transform group;
        internal Owner owner;
        internal int total;       // made so far, in flight or waiting
        internal int demand;      // how many the current situation calls for; 0 = not needed any more
    }

    internal readonly struct Key : IEquatable<Key>
    {
        readonly int prefab, color, scale;
        readonly string model, fx;

        public Key(int prefab, string model, string fx, Color color, float scale)
        {
            this.prefab = prefab;
            this.model = model;
            this.fx = fx;
            Color32 c = color;
            this.color = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            this.scale = Mathf.RoundToInt(scale * 100f);
        }

        public bool Equals(Key o) => prefab == o.prefab && color == o.color && scale == o.scale && model == o.model && fx == o.fx;
        public override bool Equals(object o) => o is Key k && Equals(k);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = prefab;
                h = h * 31 + color;
                h = h * 31 + scale;
                h = h * 31 + (model != null ? model.GetHashCode() : 0);
                h = h * 31 + (fx != null ? fx.GetHashCode() : 0);
                return h;
            }
        }
    }

    class Runner : MonoBehaviour
    {
        void Update() { if (!WarmStep(2)) enabled = false; }
    }

    static readonly Dictionary<Key, Pool> pools = new Dictionary<Key, Pool>();
    static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
    static Transform root;
    static Runner runner;

    static Transform Root()
    {
        if (root == null)        // first use, or the scene changed and took the old pools with it
        {
            pools.Clear();
            groups.Clear();
            var go = new GameObject("Projectile Pools");
            root = go.transform;
            runner = go.AddComponent<Runner>();
            runner.enabled = false;
        }
        return root;
    }

    static Transform Group(string name)
    {
        var parent = Root();
        if (groups.TryGetValue(name, out var g) && g != null) return g;
        g = new GameObject(name).transform;
        g.SetParent(parent, false);
        groups[name] = g;
        return g;
    }

    static Pool PoolFor(Key key, string group, Owner owner, Func<Pool, Component> create)
    {
        Root();
        if (pools.TryGetValue(key, out var pool)) return pool;
        pool = new Pool { key = key, group = Group(group), owner = owner, create = create };
        pools[key] = pool;
        return pool;
    }

    // ---------- taking and giving back ----------

    // a player or enemy shot, ready to be positioned: call Init on it afterwards. modelKey picks the 3D model, fxKey the impact effect (null for enemies)
    public static Projectile Get(Projectile prefab, string modelKey, string fxKey, Color color, float scale, Vector3 position, Quaternion rotation)
    {
        var pool = ProjectilePoolFor(prefab, modelKey, fxKey, color, scale);
        return (Projectile)Take(pool, position, rotation);
    }

    static Pool ProjectilePoolFor(Projectile prefab, string modelKey, string fxKey, Color color, float scale)
    {
        var key = new Key(prefab.GetInstanceID(), modelKey, fxKey, color, scale);
        return PoolFor(key, fxKey ?? modelKey, modelKey == "Enemy" ? Owner.Enemy : Owner.Player, p =>
        {
            var proj = Object.Instantiate(prefab, p.group);
            proj.pool = p;
            proj.effectColor = color;
            proj.fxKey = fxKey;
            ProjectileModels.Attach(proj.gameObject, modelKey, color);
            return proj;
        });
    }

    internal static Pool BossPool(Color color)
    {
        var key = new Key(0, "Boss", null, color, 1f);
        return PoolFor(key, "Boss", Owner.Boss, p => BossProjectile.Create(p, color));
    }

    internal static Component Take(Pool pool, Vector3 position, Quaternion rotation)
    {
        if (pool.demand == 0) pool.demand = 1;       // used again: keep it alive
        Component item = null;
        while (item == null && pool.free.Count > 0) item = pool.free.Pop();
        if (item == null) item = Make(pool);
        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        return item;
    }

    static Component Make(Pool pool)
    {
        pool.total++;
        return pool.create(pool);
    }

    // a shot that has landed or run out
    internal static void Release(Pool pool, Component item)
    {
        if (!item.gameObject.activeSelf) return;       // already given back
        if (pool.demand == 0)
        {
            Object.Destroy(item.gameObject);
            pool.total--;
            Forget(pool);
            return;
        }
        item.gameObject.SetActive(false);
        pool.free.Push(item);
    }

    static void Forget(Pool pool)
    {
        if (pool.total <= 0) pools.Remove(pool.key);
    }

    // ---------- sizing ----------

    // Describe what is needed now with BeginDemand / Demand... / EndDemand. Pools of that owner that are not asked for are freed.
    static void BeginDemand(Owner owner)
    {
        foreach (var pool in pools.Values) if (pool.owner == owner) pool.demand = 0;
    }

    static void Demand(Pool pool, int count)
    {
        // enemies of one kind add up; the player's spell variants that share a pool do not
        pool.demand = pool.owner == Owner.Enemy ? pool.demand + count : Mathf.Max(pool.demand, count);
    }

    static void EndDemand(Owner owner)
    {
        List<Pool> unused = null;
        foreach (var pool in pools.Values)
            if (pool.owner == owner && pool.demand == 0) (unused ?? (unused = new List<Pool>())).Add(pool);

        if (unused != null)
            foreach (var pool in unused)
            {
                while (pool.free.Count > 0)
                {
                    var item = pool.free.Pop();
                    if (item != null) Object.Destroy(item.gameObject);
                    pool.total--;
                }
                Forget(pool);
            }

        if (runner != null) runner.enabled = true;
    }

    // fills the pools a few objects per frame; false when they are all full
    static bool WarmStep(int budget)
    {
        foreach (var pool in pools.Values)
            while (pool.total < pool.demand)
            {
                if (budget-- <= 0) return true;
                var item = Make(pool);
                item.gameObject.SetActive(false);
                pool.free.Push(item);
            }
        return false;
    }

    // the player's pools follow the equipped weapon: its spells, their hold versions and the fusions it can make
    public static void WarmFor(WeaponData weapon)
    {
        BeginDemand(Owner.Player);
        if (weapon != null)
        {
            if (weapon.spells != null) foreach (var s in weapon.spells) DemandSpell(s);
            if (weapon.combos != null) foreach (var c in weapon.combos) if (c != null) DemandSpell(c.result);
        }
        EndDemand(Owner.Player);
    }

    static void DemandSpell(SpellData spell)
    {
        if (spell == null) return;
        DemandVariant(spell, spell.tap);
        if (spell.hasHoldVariant) DemandVariant(spell, spell.hold);
    }

    static void DemandVariant(SpellData spell, SpellVariant v)
    {
        if (v == null || v.kind != SpellKind.Projectile) return;
        var prefab = v.projectilePrefab != null ? v.projectilePrefab : spell.defaultProjectile;
        if (prefab == null) return;

        // shots that can be in the air at once: a volley for every cooldown that fits in the time a shot usually flies
        int volleys = Mathf.Clamp(Mathf.CeilToInt(1.5f / Mathf.Max(v.cooldown, 0.1f)), 1, 6);
        int count = Mathf.Min(Mathf.Max(1, v.projectileCount) * volleys, 24);
        Demand(ProjectilePoolFor(prefab, spell.name, spell.name, v.effectColor, v.projectileScale), count);
    }

    // the enemies of a wave: begin, then each shooter says what it needs, then end
    public static void BeginEnemyDemand() => BeginDemand(Owner.Enemy);
    public static void EndEnemyDemand() => EndDemand(Owner.Enemy);
    public static void DemandEnemyShots(Projectile prefab, Color color, float scale, int count)
        => Demand(ProjectilePoolFor(prefab, "Enemy", null, color, scale), count);

    // the boss only throws one colour at a time
    public static void WarmBoss(Color color, int count)
    {
        BeginDemand(Owner.Boss);
        Demand(BossPool(color), count);
        EndDemand(Owner.Boss);
    }
}
