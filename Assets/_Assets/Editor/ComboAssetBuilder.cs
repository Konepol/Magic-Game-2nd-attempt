using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Example Combos
// Makes four example combined spells and their recipes and adds them to the example weapons.
// Recipes live in Assets/_Assets/Weapons/Combos. Edit them freely, or make your own with Create > Magic > Spell Combo.
// Running it again overwrites the generated combo assets (and re-adds them to the weapons).
public static class ComboAssetBuilder
{
    const string WeaponDir = "Assets/_Assets/Weapons";
    const string ComboDir = "Assets/_Assets/Weapons/Combos";
    const string CircleDir = "Assets/_Assets/Circles";
    const string ImageDir = "Assets/_Assets/Images/Circles";

    [MenuItem("Tools/Magic/Build Example Combos")]
    public static void Build()
    {
        Directory.CreateDirectory(ComboDir);

        var projectile = AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/ArcaneBolt.asset").defaultProjectile;
        var bolt = Spell("ArcaneBolt"); var scatter = Spell("ScatterShot"); var fire = Spell("FireBurst");
        var lance = Spell("LightningLance"); var meteor = Spell("MeteorStrike"); var ward = Spell("ArcaneWard");

        // the barrier is defensive: it is not cast alongside other spells (a designed recipe still could include it)
        ward.multicastable = false;
        meteor.holdDisplayName = "Cataclysm";     // an example of a hold version that has a name of its own
        EditorUtility.SetDirty(ward);
        EditorUtility.SetDirty(meteor);

        // one big circle for the combined projectile spells
        var comboCircle = Circle("Circle_Combo", 1.5f,
            L("Glow", Tex("Glow"), CircleAnchor.Front, 3.4f, 0f, 0.0f, 0.5f, CircleAppear.FadeIn, default, 0.4f),
            L("Hexagram", Tex("Hexagram"), CircleAnchor.Front, 2.7f, 18f, 0.0f, 0.55f),
            L("Rune Band", Tex("Rune_Band"), CircleAnchor.Front, 2.3f, -22f, 0.15f, 0.5f),
            L("Outer Ring", Tex("Ring_Double"), CircleAnchor.Front, 3.1f, 10f, 0.1f, 0.5f),
            L("Star", Tex("Star8"), CircleAnchor.Front, 1.5f, 50f, 0.4f, 0.4f, CircleAppear.ScaleUp),
            L("Side Triangle", Tex("Triangle"), CircleAnchor.Front, 0.95f, 70f, 0.55f, 0.35f, CircleAppear.RadialWipe, new Vector3(1.9f, 1.2f, 0.05f)),
            L("Side Dots", Tex("Dots_Ring"), CircleAnchor.Front, 0.8f, -80f, 0.6f, 0.3f, CircleAppear.ScaleUp, new Vector3(-1.9f, -1.1f, 0.05f)),
            L("Floor Ring", Tex("Ring_Double"), CircleAnchor.Feet, 4.0f, 8f, 0.3f, 0.6f),
            L("Floor Segments", Tex("Segments"), CircleAnchor.Feet, 3.1f, -30f, 0.5f, 0.4f, CircleAppear.ScaleUp));
        comboCircle.readyPulse = 0.6f;
        var groundCircle = AssetDatabase.LoadAssetAtPath<CastingCircleData>(CircleDir + "/Circle_Meteor.asset");

        // ---------- the combined spells ----------
        var infernal = Result("InfernalBolt", "Infernal Bolt", projectile, 1.3f, comboCircle, 0.8f, new Color(1f, 0.45f, 0.2f), v =>
        {
            v.damage = 65f; v.projectileSpeed = 55f; v.projectileScale = 2.4f; v.explosionRadius = 4f;
            v.status = new StatusEffectSpec { type = StatusType.Burn, duration = 5f, magnitude = 12f };
            v.manaCost = 48f; v.cooldown = 2.5f;
        });

        var cinder = Result("CinderStorm", "Cinder Storm", projectile, 1.4f, comboCircle, 0.8f, new Color(1f, 0.6f, 0.25f), v =>
        {
            v.damage = 11f; v.projectileCount = 11; v.spreadDegrees = 85f; v.projectileSpeed = 45f; v.projectileScale = 0.8f; v.explosionRadius = 2.2f;
            v.status = new StatusEffectSpec { type = StatusType.Burn, duration = 4f, magnitude = 8f };
            v.manaCost = 46f; v.cooldown = 2.2f;
        });

        var firestorm = Result("FirestormLance", "Firestorm Lance", projectile, 1.5f, comboCircle, 0.85f, new Color(1f, 0.7f, 0.25f), v =>
        {
            v.damage = 55f; v.projectileSpeed = 50f; v.projectileScale = 1.8f; v.explosionRadius = 4.5f;
            v.pierceCount = -1; v.explodeOnPierce = true; v.breaksBarriers = true;
            v.status = new StatusEffectSpec { type = StatusType.Stun, duration = 0.8f, magnitude = 0f };
            v.manaCost = 58f; v.cooldown = 3f;
        });

        var thunder = Result("ThunderMeteor", "Thunder Meteor", null, 2.0f, groundCircle, 1.3f, new Color(1f, 0.9f, 0.35f), v =>
        {
            v.kind = SpellKind.GroundAoE; v.damage = 110f; v.aoeRadius = 10f; v.aoeRange = 35f; v.aoeDelay = 1.0f; v.aoeHeight = 8f;
            v.status = new StatusEffectSpec { type = StatusType.Stun, duration = 1.5f, magnitude = 0f };
            v.manaCost = 70f; v.cooldown = 4f;
        });

        // ---------- recipes ----------
        var r1 = Recipe("Combo_InfernalBolt", infernal, bolt, fire);
        var r2 = Recipe("Combo_CinderStorm", cinder, scatter, fire);
        var r3 = Recipe("Combo_FirestormLance", firestorm, lance, fire);
        var r4 = Recipe("Combo_ThunderMeteor", thunder, lance, meteor);

        AddToWeapon("ApprenticeStaff", r1, r2);
        AddToWeapon("ArcanistStaff", r3, r4);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Example combos built in " + ComboDir + " and added to the example weapons.");
    }

    // ---------- helpers ----------

    static SpellData Spell(string name) => AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/" + name + ".asset");

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ImageDir + "/" + name + ".png");

    static SpellData Result(string assetName, string display, Projectile projectile, float castTime, CastingCircleData circle,
        float circleScale, Color color, System.Action<SpellVariant> setup)
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
        s.holdThreshold = castTime;                 // the combination's cast time
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

    static SpellCombo Recipe(string name, SpellData result, params SpellData[] ingredients)
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
        return c;
    }

    static void AddToWeapon(string weaponName, params SpellCombo[] combos)
    {
        var w = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDir + "/" + weaponName + ".asset");
        if (w == null) return;
        w.combos = combos;
        w.allowMulticast = true;
        EditorUtility.SetDirty(w);
    }

    static CircleLayer L(string name, Texture2D tex, CircleAnchor anchor, float size, float rot, float startAt, float drawTime,
        CircleAppear appear = CircleAppear.RadialWipe, Vector3 offset = default, float opacity = 1f)
    {
        return new CircleLayer
        {
            name = name, texture = tex, anchor = anchor, size = size, rotationSpeed = rot,
            startAt = startAt, drawTime = drawTime, appear = appear, offset = offset, opacity = opacity,
        };
    }

    static CastingCircleData Circle(string name, float frontDistance, params CircleLayer[] layers)
    {
        string path = CircleDir + "/" + name + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<CastingCircleData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<CastingCircleData>();
            AssetDatabase.CreateAsset(d, path);
        }
        d.frontDistance = frontDistance;
        d.layers = layers;
        EditorUtility.SetDirty(d);
        return d;
    }
}
