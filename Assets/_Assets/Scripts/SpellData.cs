using System;
using UnityEngine;

public enum SpellKind
{
    Projectile,   // flies out and hits something
    Laser,        // instant line, hits immediately
    GroundAoE,    // hold to aim a ring on the ground, release to unleash it
    Barrier,      // a wall with health that blocks enemy shots
}

// A stronger version of a hold spell, reached by keeping the key held past the cast time (see SpellData.extraTiers).
[Serializable]
public class ChargeTier
{
    public string displayName = "Greater";
    [Tooltip("Seconds held (counted like the cast time) at which this tier is reached. Must be later than the previous tier.")]
    public float time = 3f;
    [Tooltip("Multiplies the hold version's damage.")]
    public float damageMultiplier = 1.5f;
    [Tooltip("Multiplies the blast / explosion size (and projectile size).")]
    public float sizeMultiplier = 1.3f;
    public float manaMultiplier = 1.3f;
    [Tooltip("An extra casting circle that draws itself on top of the earlier ones while charging toward this tier. Make it bigger and busier.")]
    public CastingCircleData circle;
    [Range(0.3f, 3f)] public float circleScale = 1f;
    [Tooltip("Sentences chanted while charging toward this tier (they appear after the spell's own chant).")]
    public string[] chant;
}

// One way of casting a spell. A spell has a tap variant and, optionally, a hold variant.
[Serializable]
public class SpellVariant
{
    public SpellKind kind = SpellKind.Projectile;

    [Header("Damage (all kinds)")]
    public float damage = 10f;
    public bool breaksBarriers = false;     // instantly destroys a barrier it hits instead of just damaging it
    public Color effectColor = new Color(0.4f, 0.8f, 1f, 1f);

    [Header("Projectile")]
    public Projectile projectilePrefab;     // empty = use the spell's default projectile
    public float projectileSpeed = 40f;
    public float lifetime = 4f;
    public float projectileScale = 1f;
    public float explosionRadius = 0f;      // 0 = direct hit only; above 0 = damages everything in this radius on impact
    public int pierceCount = 0;             // enemies it passes through before stopping (0 = stops on the first, -1 = unlimited)
    public bool explodeOnPierce = false;    // detonates at every enemy it passes through (uses the explosion radius)
    [Min(1)] public int projectileCount = 1;
    public float spreadDegrees = 0f;        // total fan angle, projectiles are spaced evenly across it

    [Header("Projectile: sky combat")]
    [Tooltip("Extra reach above and below the shot, in metres: the hit shape becomes a standing capsule, so a shot aimed at a flier's height still connects when the flier climbs or dives. The ground and walls ignore it.")]
    public float hitHeight = 0f;
    [Tooltip("Airburst fuse: the shot goes off as soon as an enemy is within this distance (it does not have to touch). Give it an explosion radius at least this big.")]
    public float proximityFuse = 0f;
    [Tooltip("Degrees per second the shot turns toward its target: the locked enemy, otherwise the nearest enemy ahead of it. 0 = flies straight.")]
    public float homingTurnRate = 0f;

    [Header("Laser")]
    public float laserRange = 60f;
    public float laserRadius = 0.15f;       // thickness of the hit check
    public bool laserPierces = false;       // hits every enemy along the line instead of the first
    public float laserWidth = 0.3f;         // thickness of the visible beam
    public float laserDuration = 0.2f;      // how long the beam stays visible
    [Tooltip("After the beam hits an enemy it jumps to this many more enemies nearby.")]
    public int chainCount = 0;
    public float chainRange = 14f;          // how far each jump can reach
    [Range(0.1f, 1f)] public float chainFalloff = 0.7f;     // damage kept on each jump

    [Header("Ground AoE (hold to aim, release to cast)")]
    public float aoeRadius = 4f;
    public float aoeRange = 30f;            // furthest the ring can be placed from the player
    public float aoeHeight = 6f;            // how far above the ground the blast reaches (so flying enemies are hit)
    public float aoeDelay = 0.6f;           // warning time between release and the blast
    [Tooltip("Above 0 the area stays for this many seconds after the blast and keeps hurting what is inside (a storm, a burning crater, a gravity well).")]
    public float lingerDuration = 0f;
    public float lingerTickDamage = 0f;     // damage on every tick while it lingers (the status effect is applied again too)
    public float lingerTickInterval = 0.5f;
    [Tooltip("Metres per second enemies inside the lingering area are dragged toward its centre.")]
    public float lingerPull = 0f;

    [Header("Barrier")]
    public float barrierWidth = 4f;
    public float barrierHeight = 3f;
    public float barrierThickness = 0.3f;
    public float barrierHealth = 60f;
    public float barrierDuration = 8f;
    public float barrierDistance = 3f;      // placed this far in front of the player

    [Header("Status Effect (applied to enemies it damages)")]
    public StatusEffectSpec status;         // type None = nothing; burn magnitude = damage per second, slow = fraction of speed removed

    [Header("Cost and Timing")]
    public float manaCost = 0f;
    public float cooldown = 0.4f;           // before this spell slot can be used again

    // variants that are aimed with a ring while the key is held
    public bool NeedsAim => kind == SpellKind.GroundAoE;

    public SpellVariant Clone() => (SpellVariant)MemberwiseClone();
}

[CreateAssetMenu(menuName = "Magic/Spell", fileName = "NewSpell")]
public class SpellData : ScriptableObject
{
    public string displayName = "New Spell";
    public Projectile defaultProjectile;

    [Header("Look and sound")]
    [Tooltip("Which family of effects this spell uses: the projectile model, trail, impact, beam, sound and cast pose are all picked by this key (ArcaneBolt, FireBurst, LightningLance, MeteorStrike ...). Empty = the asset's own name.")]
    public string fxStyle = "";
    [Tooltip("Short label for the spell book (Homing swarm, Sky pillar ...). Empty = worked out from the spell kind.")]
    public string roleLabel = "";

    // the key every effect, sound and pose table is looked up with
    public string FxKey => string.IsNullOrEmpty(fxStyle) ? name : fxStyle;

    [Header("Tap (press and release quickly)")]
    public SpellVariant tap = new SpellVariant();

    [Header("Hold (optional)")]
    public bool hasHoldVariant = false;
    public float holdThreshold = 0.5f;      // the CAST TIME: the circle completes after this long; the spell then waits for the key to be released
    public SpellVariant hold = new SpellVariant { damage = 30f, cooldown = 1.2f };

    [Header("Movement while casting (stronger spells should be stricter and take longer)")]
    [Tooltip("TAP: after the spell fires from a quick press.")]
    public CastMovement tapMovement = CastMovement.Free;
    public float tapRecovery = 0.2f;

    [Tooltip("HOLDING: while the key is down and the spell is charging or being aimed, before it fires.")]
    public CastMovement holdingMovement = CastMovement.Slow;
    [Tooltip("Holding only counts once the key has been down this long, so a quick tap does not restrict movement.")]
    public float holdingStartsAfter = 0.15f;

    [Tooltip("HELD: after the hold version has fired.")]
    public CastMovement heldMovement = CastMovement.Stand;
    public float heldRecovery = 0.5f;

    [Header("Hold casting (hold spells take time to cast)")]
    [Tooltip("Letting go within this time is a quick tap: the tap version fires instantly. Holding longer commits to the hold cast, and letting go before the cast time fizzles it (nothing is cast).")]
    public float tapWindow = 0.2f;
    [Tooltip("Strong casts can be broken: taking a hit while the circle is drawing makes the spell fizzle.")]
    public bool interruptible = false;
    [Tooltip("A hit has to do at least this much damage to interrupt the cast.")]
    public float interruptDamage = 1f;

    [Header("Casting circle")]
    [Tooltip("Drawn while the key is held. It completes when the cast time (Hold Threshold) is up.")]
    public CastingCircleData holdCircle;
    [Tooltip("The small quick circle that plays when the spell is tapped (instant cast).")]
    public CastingCircleData tapCircle;
    [Range(0.3f, 2f), Tooltip("Stronger spells get a bigger circle.")]
    public float circleScale = 1f;
    [Range(0.1f, 1f)] public float tapCircleScale = 0.45f;

    [Header("Naming")]
    [Tooltip("The name shown once the hold circle completes. Leave empty for 'Greater <name>'.")]
    public string holdDisplayName = "";

    [Header("Combining and multicasting")]
    [Tooltip("Can be cast at the same time as other spells (multicast) when there is no designed recipe. Recipes ignore this.")]
    [UnityEngine.Serialization.FormerlySerializedAs("autoCombinable")]
    public bool multicastable = true;

    [Header("Chant (shown above the player's head while casting)")]
    [Tooltip("One sentence per row; they appear one after another over the cast time.")]
    public string[] chant;

    [Header("Charge tiers (hold spells)")]
    [Tooltip("Tapping the key does nothing except remind the player to hold it.")]
    public bool tapDoesNothing = false;
    [Tooltip("Keep holding past the cast time and the spell grows into a stronger version at each of these times: bigger, harder hitting, with a bigger circle.")]
    public ChargeTier[] extraTiers;

    [Header("Tap-only spells")]
    public bool autoRepeatWhenHeld = false; // keeps casting the tap variant while the key is held (ignored when there is a hold variant)

    // the name once the hold version is ready
    public string HoldName => string.IsNullOrEmpty(holdDisplayName) ? "Greater " + displayName : holdDisplayName;

    public SpellVariant Pick(float heldTime) => hasHoldVariant && heldTime >= holdThreshold ? hold : tap;

    // ---------- charge tiers: 0 = the normal hold version (reached at the cast time), 1.. = the extra tiers ----------

    [NonSerialized] SpellVariant[] tierVariants;

    [NonSerialized] string[] chantCache;

    void OnValidate() { tierVariants = null; chantCache = null; }

    // the spell's own chant followed by the chant of each charge tier
    public string[] AllChantLines()
    {
        if (chantCache != null) return chantCache;

        var list = new System.Collections.Generic.List<string>();
        if (chant != null) list.AddRange(chant);
        if (extraTiers != null)
            foreach (var t in extraTiers)
                if (t != null && t.chant != null) list.AddRange(t.chant);
        return chantCache = list.ToArray();
    }

    public int TierCount => extraTiers != null ? extraTiers.Length : 0;

    // the tier reached after holding this long (only meaningful once the cast time is up)
    public int TierAt(float held)
    {
        int tier = 0;
        for (int i = 0; i < TierCount; i++)
            if (held >= extraTiers[i].time) tier = i + 1;
        return tier;
    }

    public SpellVariant VariantForTier(int tier)
    {
        if (tier <= 0 || tier > TierCount) return hold;

        if (tierVariants == null || tierVariants.Length != TierCount + 1) tierVariants = new SpellVariant[TierCount + 1];
        if (tierVariants[tier] != null) return tierVariants[tier];

        var t = extraTiers[tier - 1];
        var v = hold.Clone();
        v.damage *= t.damageMultiplier;
        v.aoeRadius *= t.sizeMultiplier;
        v.explosionRadius *= t.sizeMultiplier;
        v.projectileScale *= t.sizeMultiplier;
        v.hitHeight *= t.sizeMultiplier;
        v.proximityFuse *= t.sizeMultiplier;
        v.lingerTickDamage *= t.damageMultiplier;
        v.manaCost *= t.manaMultiplier;
        tierVariants[tier] = v;
        return v;
    }

    public string TierName(int tier) => tier <= 0 || tier > TierCount ? HoldName : extraTiers[tier - 1].displayName;
}
