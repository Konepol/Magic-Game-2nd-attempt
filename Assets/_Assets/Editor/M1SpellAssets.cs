using UnityEditor;
using UnityEngine;

// M1 parts 3 to 7: creates the five spell assets (Weaver, Hollow Moon, Mirror Bloom, Lantern, Veil Step), puts them in the catalog and the
// default loadout, and adds test helpers (a projectile dummy, a dark test area). Safe to run again: existing spell assets are not overwritten,
// so Inspector tweaks stay.
public static class M1SpellAssets
{
    const string Dir = "Assets/_Assets/Spells/";
    const string CatalogPath = "Assets/_Assets/Resources/GameCatalog.asset";

    static SpellData source;      // StarBolt: lends its default projectile and casting circles (circles are reused, not rebuilt)
    static SpellData bigSource;   // Starfall: a busier circle for the big spell

    [MenuItem("Tools/Magic/M1 Parts 3-7: Build the five spells")]
    public static void Run()
    {
        source = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "StarBolt.asset");
        bigSource = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "Starfall.asset");

        var weaver = Make("Weaver", s =>
        {
            s.displayName = "Weaver"; s.roleLabel = "Thread / stitch";
            var color = new Color(1f, 0.55f, 0.8f);
            s.tap = V("weaver_tap", color, damage: 12f, mana: 4f, cooldown: 0.35f, speed: 55f, range: 40f,
                resp: new OutputResponse { reachExp = 0.5f, extraProjectilesPerOutput = 1.5f, damageExp = 0.3f });
            s.hold = V("weaver_hold", color, damage: 26f, mana: 22f, cooldown: 1.4f, range: 45f,
                resp: new OutputResponse { reachExp = 0.5f, extraProjectilesPerOutput = 2f, damageExp = 0.4f });
            s.hasHoldVariant = true; s.holdThreshold = 0.7f; s.holdDisplayName = "Stitchwork";
            s.chant = new[] { "Thread of dusk, run through the dark.", "Stitch them all together." };
            s.outputLimit = 2.2f; s.backfireDamage = 14f; s.circleScale = 0.8f;
            s.holdingMovement = CastMovement.Slow; s.heldMovement = CastMovement.Free; s.heldRecovery = 0.3f;
        });

        var moon = Make("HollowMoon", s =>
        {
            s.displayName = "Hollow Moon"; s.roleLabel = "Crescent / moon";
            var color = new Color(0.62f, 0.4f, 1f);
            s.tap = V("moon_blade", color, damage: 40f, mana: 14f, cooldown: 0.6f, speed: 38f, life: 1.3f,
                resp: new OutputResponse { sizeExp = 0.6f, speedExp = 0.3f, damageExp = 0.5f });
            s.hold = V("moon_small", color, damage: 120f, mana: 50f, cooldown: 3f, aoe: 9f,
                resp: new OutputResponse { sizeExp = 1f, damageExp = 0.6f });
            s.hasHoldVariant = true; s.holdThreshold = 1.0f; s.holdDisplayName = "Hollow Moon";
            s.chant = new[] { "A disc of night above my hand,", "hollow, hanging, hungry." };
            var great = new ChargeTier
            {
                displayName = "Great Hollow Moon", time = 2.6f, ownForm = true, manaMultiplier = 1f,
                chant = new[] { "Fall inward, ring, and take what is within." },
            };
            great.form = V("moon_great", color, damage: 320f, mana: 120f, cooldown: 6f, aoe: 20f,
                resp: new OutputResponse { sizeExp = 1f, damageExp = 0.6f });
            s.extraTiers = new[] { great };
            s.chargeVisual = "DarkDisc";
            s.outputLimit = 1.7f; s.backfireDamage = 30f; s.circleScale = 1.4f; s.interruptible = true; s.interruptDamage = 12f;
            s.holdingMovement = CastMovement.Slow; s.heldMovement = CastMovement.Stand; s.heldRecovery = 0.8f;
            if (bigSource != null) s.holdCircle = bigSource.holdCircle;
        });

        var bloom = Make("MirrorBloom", s =>
        {
            s.displayName = "Mirror Bloom"; s.roleLabel = "Parry / dome";
            var color = new Color(0.7f, 0.95f, 1f);
            s.tap = V("bloom_parry", color, damage: 18f, mana: 14f, cooldown: 0.9f,
                resp: new OutputResponse { sizeExp = 0.5f, extraProjectilesPerOutput = 2f, damageExp = 0.5f });
            s.hold = V("bloom_dome", color, damage: 0f, mana: 35f, cooldown: 5f, barrierWidth: 8f, barrierDuration: 6f,
                resp: new OutputResponse { sizeExp = 0.8f, damageExp = 0f });
            s.hasHoldVariant = true; s.holdThreshold = 0.6f; s.holdDisplayName = "Glass Dome";
            s.chant = new[] { "Open, glass petals, and keep what is mine." };
            s.outputLimit = 2.4f; s.backfireDamage = 10f; s.circleScale = 0.9f;
            s.holdingMovement = CastMovement.Slow; s.heldMovement = CastMovement.Free; s.heldRecovery = 0.2f;
            s.tapMovement = CastMovement.Free;
        });

        var lantern = Make("Lantern", s =>
        {
            s.displayName = "Lantern"; s.roleLabel = "Light / lure / beacon";
            var color = new Color(1f, 0.85f, 0.5f);
            s.tap = V("lantern_place", color, damage: 0f, mana: 8f, cooldown: 0.5f, aoe: 14f, barrierDuration: 40f,
                resp: new OutputResponse { sizeExp = 0.5f, damageExp = 0f });
            s.hold = V("lantern_throw", color, damage: 0f, mana: 16f, cooldown: 1.2f, range: 40f, aoe: 14f, barrierDuration: 40f,
                resp: new OutputResponse { sizeExp = 0.5f, reachExp = 0.4f, damageExp = 0f });
            s.hasHoldVariant = true; s.holdThreshold = 0.5f; s.holdDisplayName = "Thrown Lantern";
            s.chant = new[] { "Little light, go ahead of me." };
            var beacon = new ChargeTier
            {
                displayName = "Beacon", time = 1.8f, ownForm = true, manaMultiplier = 1f,
                chant = new[] { "Rise, and let them find the way." },
            };
            beacon.form = V("lantern_beacon", color, damage: 0f, mana: 40f, cooldown: 4f, aoe: 20f, barrierDuration: 90f,
                resp: new OutputResponse { sizeExp = 0.6f, damageExp = 0f });
            s.extraTiers = new[] { beacon };
            s.outputLimit = 2.6f; s.backfireDamage = 8f; s.circleScale = 0.7f;
            s.holdingMovement = CastMovement.Free; s.heldMovement = CastMovement.Free; s.heldRecovery = 0.2f;
        });

        var veil = Make("VeilStep", s =>
        {
            s.displayName = "Veil Step"; s.roleLabel = "Blink / star gate";
            var color = new Color(1f, 0.7f, 0.85f);
            s.tap = V("veil_blink", color, damage: 0f, mana: 8f, cooldown: 0.5f, range: 9f,
                resp: new OutputResponse { reachExp = 0.5f, damageExp = 0f });
            s.hold = V("veil_gate", color, damage: 0f, mana: 22f, cooldown: 3f, range: 30f,
                resp: new OutputResponse { reachExp = 0.5f, sizeExp = 0.4f, damageExp = 0f });
            s.hasHoldVariant = true; s.holdThreshold = 0.45f; s.holdDisplayName = "Star Gate";
            s.chant = new[] { "Petals, part for me." };
            s.passive = new SpellPassive
            {
                enabled = true, displayName = "Petal Dash", special = "dash_to_blink", manaStyle = PassiveManaStyle.PerActivation,
                activationCost = 3f, description = "While on, the broom's quick dash becomes a blink: petals dissolve and re-form where you land.",
            };
            s.outputLimit = 3.0f; s.backfireDamage = 8f; s.circleScale = 0.7f;
            s.tapMovement = CastMovement.Free; s.holdingMovement = CastMovement.Free; s.heldMovement = CastMovement.Free; s.heldRecovery = 0.1f;
        });

        var test = AssetDatabase.LoadAssetAtPath<SpellData>(Dir + "TestSpell.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog != null)
        {
            var list = new System.Collections.Generic.List<SpellData> { weaver, moon, bloom, lantern, veil };
            if (test != null) list.Add(test);
            catalog.spells = list.ToArray();
            catalog.combos = new SpellCombo[0];
            catalog.defaultLoadout = new[] { "Weaver", "HollowMoon", "MirrorBloom", "Lantern", "VeilStep", test != null ? test.name : "" };
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("M1 spells ready: Weaver, Hollow Moon, Mirror Bloom, Lantern, Veil Step (catalog and default loadout updated).");
    }

    static SpellData Make(string name, System.Action<SpellData> fill)
    {
        string path = Dir + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<SpellData>(path);
        if (existing != null) return existing;

        var s = ScriptableObject.CreateInstance<SpellData>();
        if (source != null)
        {
            s.defaultProjectile = source.defaultProjectile;
            s.holdCircle = source.holdCircle;
            s.tapCircle = source.tapCircle;
        }
        s.fxStyle = "ArcaneBolt";      // reuses the existing cast pose and sounds until each spell gets its own
        s.multicastable = false;
        fill(s);
        AssetDatabase.CreateAsset(s, path);
        return s;
    }

    static SpellVariant V(string special, Color color, float damage, float mana, float cooldown, float speed = 40f, float life = 4f,
        float range = 60f, float aoe = 4f, float barrierWidth = 4f, float barrierDuration = 8f, OutputResponse resp = null)
    {
        return new SpellVariant
        {
            kind = SpellKind.Special, special = special, effectColor = color, damage = damage, manaCost = mana, cooldown = cooldown,
            projectileSpeed = speed, lifetime = life, laserRange = range, aoeRadius = aoe, barrierWidth = barrierWidth,
            barrierDuration = barrierDuration, output = resp ?? new OutputResponse(),
        };
    }

    // ---------- test helpers ----------

    static Vector3 InFront(float distance)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return Vector3.zero;
        Vector3 p = player.transform.position + player.transform.forward * distance;
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f)) p = hit.point;
        return p;
    }

    [MenuItem("Tools/Magic/Spawn Projectile Dummy (tests Mirror Bloom)")]
    public static void SpawnDummy()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "ProjectileDummy";
        go.transform.position = InFront(18f) + Vector3.up;
        go.tag = "Enemy";
        var h = go.AddComponent<Health>();
        h.Configure(500f, false, 0f);
        go.AddComponent<ProjectileDummy>();
        Undo.RegisterCreatedObjectUndo(go, "Spawn projectile dummy");
        Selection.activeGameObject = go;
    }

    [MenuItem("Tools/Magic/Spawn Dark Test Area (tests Lantern)")]
    public static void SpawnDarkArea()
    {
        var root = new GameObject("DarkTestArea");
        root.transform.position = InFront(40f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.07f, 0.07f, 0.08f);
        mat.name = "DarkTestAreaMat";

        const float w = 36f, h = 12f, t = 1f;
        Wall(root.transform, mat, new Vector3(0f, h + 0.5f, 0f), new Vector3(w, t, w));                 // roof (blocks the sun)
        Wall(root.transform, mat, new Vector3(0f, h * 0.5f, w * 0.5f), new Vector3(w, h, t));            // back
        Wall(root.transform, mat, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, w));           // left
        Wall(root.transform, mat, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, w));            // right
        // front: two pillars of wall leaving an opening to walk in
        Wall(root.transform, mat, new Vector3(-w * 0.35f, h * 0.5f, -w * 0.5f), new Vector3(w * 0.3f, h, t));
        Wall(root.transform, mat, new Vector3(w * 0.35f, h * 0.5f, -w * 0.5f), new Vector3(w * 0.3f, h, t));

        var zone = new GameObject("DarkZone");
        zone.transform.SetParent(root.transform, false);
        zone.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        var box = zone.AddComponent<BoxCollider>();
        box.size = new Vector3(w - 2f, h, w - 2f);
        box.isTrigger = true;
        zone.AddComponent<DarkZone>();

        Undo.RegisterCreatedObjectUndo(root, "Spawn dark test area");
        Selection.activeGameObject = root;
    }

    static void Wall(Transform parent, Material mat, Vector3 pos, Vector3 size)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall";
        w.transform.SetParent(parent, false);
        w.transform.localPosition = pos;
        w.transform.localScale = size;
        w.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
