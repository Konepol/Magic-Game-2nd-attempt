using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build More Spells And Fusions
// Adds five spells (Frost Shard, Wind Blade, Earth Spike, Venom Dart, Radiant Beam) and seven fusions, four of them needing three or four
// spells, and puts them into the game catalog so the spell book and the combinations menu list them.
// Everything is an ordinary asset in Assets/_Assets/Weapons (spells) and Weapons/Combos (fusions and recipes): tune them by hand afterwards.
// Running it again overwrites the numbers set here.
public static class MoreSpellsBuilder
{
    const string WeaponDir = "Assets/_Assets/Weapons";
    const string ComboDir = "Assets/_Assets/Weapons/Combos";
    const string CircleDir = "Assets/_Assets/Circles";
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";

    [MenuItem("Tools/Magic/Build More Spells And Fusions")]
    public static void Build()
    {
        Directory.CreateDirectory(ComboDir);

        var bolt = Load("ArcaneBolt"); var scatter = Load("ScatterShot"); var fire = Load("FireBurst");
        var lance = Load("LightningLance"); var meteor = Load("MeteorStrike");

        var arcane = Circle("Circle_Arcane"); var storm = Circle("Circle_Storm"); var tap = Circle("Circle_Tap");
        var ground = Circle("Circle_Meteor"); var great = Circle("Circle_Meteor_Greater"); var combo = Circle("Circle_Combo");

        // ---------- five new spells ----------

        var frost = Spell("FrostShard", "Frost Shard", bolt, arcane, 0.9f, 0.7f, "Glacial Spray", t =>
        {
            t.damage = 8f; t.projectileSpeed = 48f; t.projectileScale = 0.8f; t.manaCost = 6f; t.cooldown = 0.3f;
            t.effectColor = new Color(0.55f, 0.85f, 1f);
            t.status = new StatusEffectSpec { type = StatusType.Slow, duration = 3f, magnitude = 0.5f };
        }, h =>
        {
            h.damage = 8f; h.projectileCount = 5; h.spreadDegrees = 26f; h.projectileSpeed = 42f; h.projectileScale = 0.85f;
            h.manaCost = 24f; h.cooldown = 1.0f; h.breaksBarriers = false;
            h.effectColor = new Color(0.55f, 0.85f, 1f);
            h.status = new StatusEffectSpec { type = StatusType.Slow, duration = 4f, magnitude = 0.55f };
        });

        var wind = Spell("WindBlade", "Wind Blade", bolt, arcane, 1.0f, 0.9f, "Hurricane Blade", t =>
        {
            t.damage = 9f; t.projectileSpeed = 62f; t.projectileScale = 1.6f; t.pierceCount = -1; t.lifetime = 3f;
            t.manaCost = 7f; t.cooldown = 0.35f; t.effectColor = new Color(0.65f, 1f, 0.8f);
        }, h =>
        {
            h.damage = 28f; h.projectileSpeed = 55f; h.projectileScale = 3.4f; h.pierceCount = -1; h.lifetime = 3f;
            h.projectileCount = 1; h.spreadDegrees = 0f; h.manaCost = 28f; h.cooldown = 1.2f; h.breaksBarriers = false;
            h.effectColor = new Color(0.65f, 1f, 0.8f);
        });

        var earth = Spell("EarthSpike", "Earth Spike", meteor, ground, 0.8f, 1.1f, "Quake", t =>
        {
            t.kind = SpellKind.GroundAoE; t.damage = 20f; t.aoeRadius = 3.5f; t.aoeRange = 30f; t.aoeHeight = 5f; t.aoeDelay = 0.45f;
            t.manaCost = 14f; t.cooldown = 1.2f; t.effectColor = new Color(0.8f, 0.6f, 0.3f); t.breaksBarriers = false;
            t.status = new StatusEffectSpec { type = StatusType.Stun, duration = 0.8f };
        }, h =>
        {
            h.kind = SpellKind.GroundAoE; h.damage = 45f; h.aoeRadius = 7f; h.aoeRange = 32f; h.aoeHeight = 5f; h.aoeDelay = 0.8f;
            h.manaCost = 38f; h.cooldown = 2.5f; h.effectColor = new Color(0.8f, 0.6f, 0.3f); h.breaksBarriers = false;
            h.status = new StatusEffectSpec { type = StatusType.Stun, duration = 1.5f };
        });

        var venom = Spell("VenomDart", "Venom Dart", bolt, arcane, 0.85f, 0.6f, "Deadly Venom", t =>
        {
            t.damage = 5f; t.projectileSpeed = 50f; t.projectileScale = 0.7f; t.manaCost = 5f; t.cooldown = 0.3f;
            t.effectColor = new Color(0.55f, 1f, 0.3f);
            t.status = new StatusEffectSpec { type = StatusType.Burn, duration = 6f, magnitude = 6f };
        }, h =>
        {
            h.damage = 14f; h.projectileSpeed = 45f; h.projectileScale = 1.1f; h.projectileCount = 1; h.spreadDegrees = 0f;
            h.manaCost = 18f; h.cooldown = 1.0f; h.breaksBarriers = false; h.effectColor = new Color(0.55f, 1f, 0.3f);
            h.status = new StatusEffectSpec { type = StatusType.Burn, duration = 10f, magnitude = 14f };
        });

        var radiant = Spell("RadiantBeam", "Radiant Beam", lance, storm, 1.0f, 1.0f, "Radiant Lance", t =>
        {
            t.kind = SpellKind.Laser; t.damage = 10f; t.laserRange = 120f; t.laserRadius = 0.2f; t.laserPierces = true;
            t.laserWidth = 0.35f; t.laserDuration = 0.22f; t.manaCost = 8f; t.cooldown = 0.5f; t.effectColor = new Color(1f, 0.95f, 0.6f);
        }, h =>
        {
            h.kind = SpellKind.Laser; h.damage = 45f; h.laserRange = 150f; h.laserRadius = 0.5f; h.laserPierces = true;
            h.laserWidth = 1.0f; h.laserDuration = 0.35f; h.manaCost = 34f; h.cooldown = 1.8f; h.breaksBarriers = false;
            h.effectColor = new Color(1f, 0.95f, 0.6f);
        });

        // ---------- seven fusions ----------

        var projectile = bolt.defaultProjectile;

        var glacier = Fusion("GlacierBolt", "Glacier Bolt", projectile, 1.2f, combo, 0.8f, new Color(0.6f, 0.9f, 1f), v =>
        {
            v.damage = 55f; v.projectileSpeed = 50f; v.projectileScale = 2.2f; v.explosionRadius = 5f;
            v.status = new StatusEffectSpec { type = StatusType.Slow, duration = 4f, magnitude = 0.6f };
            v.manaCost = 40f; v.cooldown = 2.2f;
        });

        var blizzard = Fusion("BlizzardLance", "Blizzard Lance", null, 1.4f, combo, 0.85f, new Color(0.7f, 0.95f, 1f), v =>
        {
            v.kind = SpellKind.Laser; v.damage = 45f; v.laserRange = 150f; v.laserRadius = 0.5f; v.laserPierces = true; v.laserWidth = 1.2f; v.laserDuration = 0.4f;
            v.status = new StatusEffectSpec { type = StatusType.Slow, duration = 5f, magnitude = 0.6f };
            v.manaCost = 45f; v.cooldown = 2.4f;
        });

        var cyclone = Fusion("Cyclone", "Cyclone", projectile, 1.4f, combo, 0.85f, new Color(1f, 0.7f, 0.35f), v =>
        {
            v.damage = 30f; v.projectileSpeed = 55f; v.projectileScale = 3f; v.pierceCount = -1; v.explodeOnPierce = true; v.explosionRadius = 4f;
            v.status = new StatusEffectSpec { type = StatusType.Burn, duration = 3f, magnitude = 8f };
            v.manaCost = 50f; v.cooldown = 2.6f;
        });

        var tremor = Fusion("TremorStorm", "Tremor Storm", null, 2.2f, great, 1.1f, new Color(0.85f, 0.65f, 0.3f), v =>
        {
            v.kind = SpellKind.GroundAoE; v.damage = 90f; v.aoeRadius = 12f; v.aoeRange = 35f; v.aoeHeight = 8f; v.aoeDelay = 1.0f;
            v.status = new StatusEffectSpec { type = StatusType.Stun, duration = 2f };
            v.manaCost = 90f; v.cooldown = 4f;
        });

        var plague = Fusion("PlagueStorm", "Plague Storm", projectile, 1.9f, combo, 0.95f, new Color(0.6f, 1f, 0.3f), v =>
        {
            v.damage = 14f; v.projectileCount = 14; v.spreadDegrees = 100f; v.projectileSpeed = 40f; v.projectileScale = 0.9f; v.explosionRadius = 2.5f;
            v.status = new StatusEffectSpec { type = StatusType.Burn, duration = 8f, magnitude = 14f };
            v.manaCost = 70f; v.cooldown = 3.2f;
        });

        var zero = Fusion("AbsoluteZero", "Absolute Zero", null, 2.8f, great, 1.25f, new Color(0.7f, 0.9f, 1f), v =>
        {
            v.kind = SpellKind.GroundAoE; v.damage = 140f; v.aoeRadius = 15f; v.aoeRange = 35f; v.aoeHeight = 9f; v.aoeDelay = 1.2f;
            v.status = new StatusEffectSpec { type = StatusType.Stun, duration = 2.5f };
            v.manaCost = 120f; v.cooldown = 5f;
        });

        var sun = Fusion("SunLance", "Sun Lance", null, 2.0f, combo, 1.0f, new Color(1f, 0.9f, 0.4f), v =>
        {
            v.kind = SpellKind.Laser; v.damage = 110f; v.laserRange = 160f; v.laserRadius = 0.7f; v.laserPierces = true; v.laserWidth = 1.8f; v.laserDuration = 0.5f;
            v.breaksBarriers = true;
            v.status = new StatusEffectSpec { type = StatusType.Burn, duration = 6f, magnitude = 20f };
            v.manaCost = 80f; v.cooldown = 3.5f;
        });

        // ---------- recipes ----------

        Recipe("Combo_GlacierBolt", glacier, bolt, frost);
        Recipe("Combo_BlizzardLance", blizzard, lance, frost);
        Recipe("Combo_Cyclone", cyclone, wind, fire);
        Recipe("Combo_TremorStorm", tremor, earth, meteor, scatter);
        Recipe("Combo_PlagueStorm", plague, venom, scatter, fire);
        Recipe("Combo_AbsoluteZero", zero, frost, earth, radiant, meteor);
        Recipe("Combo_SunLance", sun, radiant, lance, fire);

        AssetDatabase.SaveAssets();
        AddToCatalog(new[] { frost, wind, earth, venom, radiant });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Five spells and seven fusions built and added to the game catalog.");
    }

    // ---------- helpers ----------

    static SpellData Load(string name) => AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/" + name + ".asset");

    static CastingCircleData Circle(string name) => AssetDatabase.LoadAssetAtPath<CastingCircleData>(CircleDir + "/" + name + ".asset");

    // a spell made from a copy of an existing one (so it starts with working timings and movement rules), then changed
    static SpellData Spell(string assetName, string display, SpellData template, CastingCircleData holdCircle, float circleScale, float castTime,
        string holdName, System.Action<SpellVariant> tapSetup, System.Action<SpellVariant> holdSetup)
    {
        string path = WeaponDir + "/" + assetName + ".asset";
        var s = AssetDatabase.LoadAssetAtPath<SpellData>(path);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SpellData>();
            AssetDatabase.CreateAsset(s, path);
        }

        EditorUtility.CopySerialized(template, s);
        s.name = assetName;
        s.displayName = display;
        s.holdDisplayName = holdName;
        s.hasHoldVariant = true;
        s.holdThreshold = castTime;
        s.tapDoesNothing = false;
        s.extraTiers = null;
        s.autoRepeatWhenHeld = false;
        s.multicastable = true;
        s.holdCircle = holdCircle;
        s.tapCircle = Circle("Circle_Tap");
        s.circleScale = circleScale;

        tapSetup(s.tap);
        holdSetup(s.hold);
        EditorUtility.SetDirty(s);
        return s;
    }

    static SpellData Fusion(string assetName, string display, Projectile projectile, float castTime, CastingCircleData circle, float circleScale,
        Color color, System.Action<SpellVariant> setup)
    {
        string path = ComboDir + "/" + assetName + ".asset";
        var s = AssetDatabase.LoadAssetAtPath<SpellData>(path);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<SpellData>();
            AssetDatabase.CreateAsset(s, path);
        }

        s.displayName = display;
        s.defaultProjectile = projectile;
        s.hasHoldVariant = true;
        s.holdThreshold = castTime;
        s.tapWindow = 0.2f;
        s.holdCircle = circle;
        s.circleScale = circleScale;
        s.tapCircle = null;
        s.interruptible = true;
        s.interruptDamage = 5f;
        s.holdingMovement = CastMovement.Stand;
        s.heldMovement = CastMovement.Stand;
        s.heldRecovery = 0.7f;
        s.multicastable = true;

        var v = new SpellVariant { effectColor = color };
        setup(v);
        s.hold = v;
        s.tap = v.Clone();
        EditorUtility.SetDirty(s);
        return s;
    }

    static void Recipe(string name, SpellData result, params SpellData[] ingredients)
    {
        string path = ComboDir + "/" + name + ".asset";
        var c = AssetDatabase.LoadAssetAtPath<SpellCombo>(path);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<SpellCombo>();
            AssetDatabase.CreateAsset(c, path);
        }
        c.ingredients = ingredients;
        c.result = result;
        EditorUtility.SetDirty(c);
    }

    static void AddToCatalog(SpellData[] newSpells)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog == null) { Debug.LogWarning("More Spells: the game catalog was not found (run Tools > Magic > Build Game Catalog first)."); return; }

        var spells = new List<SpellData>(catalog.spells ?? new SpellData[0]);
        foreach (var s in newSpells) if (!spells.Contains(s)) spells.Add(s);
        catalog.spells = spells.ToArray();

        var combos = new List<SpellCombo>();
        foreach (var guid in AssetDatabase.FindAssets("t:SpellCombo", new[] { ComboDir }))
        {
            var c = AssetDatabase.LoadAssetAtPath<SpellCombo>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) combos.Add(c);
        }
        catalog.combos = combos.ToArray();
        EditorUtility.SetDirty(catalog);
    }
}
