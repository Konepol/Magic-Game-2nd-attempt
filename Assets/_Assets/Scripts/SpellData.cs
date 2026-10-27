using System;
using UnityEngine;

public enum SpellKind
{
    Projectile,   // flies out and hits something
    Laser,        // instant line, hits immediately
    GroundAoE,    // hold to aim a ring on the ground, release to unleash it
    Barrier,      // a wall with health that blocks enemy shots
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

    [Header("Laser")]
    public float laserRange = 60f;
    public float laserRadius = 0.15f;       // thickness of the hit check
    public bool laserPierces = false;       // hits every enemy along the line instead of the first
    public float laserWidth = 0.3f;         // thickness of the visible beam
    public float laserDuration = 0.2f;      // how long the beam stays visible

    [Header("Ground AoE (hold to aim, release to cast)")]
    public float aoeRadius = 4f;
    public float aoeRange = 30f;            // furthest the ring can be placed from the player
    public float aoeHeight = 6f;            // how far above the ground the blast reaches (so flying enemies are hit)
    public float aoeDelay = 0.6f;           // warning time between release and the blast

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
}

[CreateAssetMenu(menuName = "Magic/Spell", fileName = "NewSpell")]
public class SpellData : ScriptableObject
{
    public string displayName = "New Spell";
    public Projectile defaultProjectile;

    [Header("Tap (press and release quickly)")]
    public SpellVariant tap = new SpellVariant();

    [Header("Hold (optional)")]
    public bool hasHoldVariant = false;
    public float holdThreshold = 0.5f;      // hold at least this long for the hold variant
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

    [Header("Tap-only spells")]
    public bool autoRepeatWhenHeld = false; // keeps casting the tap variant while the key is held (ignored when there is a hold variant)

    public SpellVariant Pick(float heldTime) => hasHoldVariant && heldTime >= holdThreshold ? hold : tap;
}
