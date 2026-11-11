using System;
using UnityEngine;

// Where a circle layer is anchored.
public enum CircleAnchor
{
    Front,      // upright in front of the player, facing the way they are aiming (like a portal the spell comes out of)
    Feet,       // flat on the ground under the player
    AimPoint,   // flat on the ground where the spell will land (for ground spells), or at the crosshair
}

// How a layer appears.
public enum CircleAppear
{
    RadialWipe,   // drawn on clockwise from the top
    ScaleUp,      // grows from the middle
    FadeIn,       // fades in
    Burst,        // snaps out from the middle with an overshoot, like a vortex opening
    Sparkle,      // for a layer holding a grid of glyphs (8 x 8 cells): each glyph pops in at its own moment, then shimmers
    SpinIn,       // grows while whirling, the spin slowing to a stop
    Stamp,        // starts big and faint, then slams down onto its place
    FlipIn,       // lies edge-on and swings up to face you
    RadialOut,    // drawn from the middle outward in a travelling ring, the leading edge burning bright
    Cascade,      // for a glyph-grid layer: glyphs pop in from the middle outward, each with a flash
    Bloom,        // grows from the middle outward while it whirls and settles (a pinwheel opening)
    Flash,        // a quick flare: swells, peaks and is gone again (a seed glyph or a crosshair flash)
    Pop,          // everything at once, shot outward from the middle in a few frames
}

// How a whole circle leaves when the spell is released.
public enum CircleExit
{
    Fade,         // fades with a slight swell
    Implode,      // sucked into the middle, spinning faster and faster
    Shockwave,    // flashes white and rushes outward like a ripple
    SpinAway,     // whips round and shrinks away
    Unwind,       // un-draws itself, the last drawn part first
    Dissolve,     // breaks into specks
    Launch,       // shoots forward toward where the spell is going
    Shatter,      // white flash, then the lines are pulled outward and break into specks
    Overload,     // the lines stay for a moment while they are stretched longer and thinner, then fade; layers marked 'Exit Only' play their own flash, shockwave and embers
}

[Serializable]
public class CircleLayer
{
    public string name = "Layer";
    public Texture2D texture;

    [Header("Look")]
    public bool useSpellColor = true;                  // tinted by the spell's colour; off = use Tint as it is
    public Color tint = Color.white;
    [Range(0f, 1f)] public float opacity = 1f;
    public float rotationSpeed = 30f;                  // degrees per second, negative turns the other way

    [Header("Where")]
    public CircleAnchor anchor = CircleAnchor.Front;
    [Tooltip("Moves this layer away from the middle of its anchor: x = right, y = up, z = out away from the viewer (metres, before the spell's circle scale).")]
    public Vector3 offset = Vector3.zero;
    [Tooltip("Diameter in metres. If 'Scale To Aoe Radius' is on, this is a multiplier of the area's own diameter instead.")]
    public float size = 1.5f;
    public bool scaleToAoeRadius = false;

    [Header("When (this is the casting order)")]
    [Range(0f, 1f), Tooltip("When this layer starts drawing, as a fraction of the cast time. Lower numbers appear first.")]
    public float startAt = 0f;
    [Range(0.02f, 1f), Tooltip("How much of the cast time this layer takes to draw.")]
    public float drawTime = 0.4f;
    public CircleAppear appear = CircleAppear.RadialWipe;

    [Header("Arrow ring (a ring of diamond + V groups: they turn apart, fuse, turn to face the middle, shrink and circle it)")]
    [Tooltip("Turns this layer into the arrow ring. It then draws its own pieces from the two images below instead of the Texture above. Start At / Draw Time set when the whole sequence plays.")]
    public bool arrowRing = false;
    public Texture2D arrowV;
    public Texture2D arrowDiamond;
    [Tooltip("Colour of the big V pieces. The diamonds use the spell's colour.")]
    public Color arrowAccent = new Color(1f, 0.85f, 0.3f);
    [Tooltip("Degrees per second the small ring circles the middle once it has settled.")]
    public float ringSpin = 90f;
    [Tooltip("While the pieces appear and before the shape is complete, the whole ring spins around the middle this many full turns, starting fast and slowing to a stop as the shape completes (0 = no spin).")]
    public float orbitTurns = 1f;

    [Header("Hand-drawn animation")]
    [Tooltip("Optional: several drawings of this layer that are swapped one after another (a hand-drawn wiggle). The Texture above is the first one.")]
    public Texture2D[] variants;
    [Tooltip("How many times a second the drawings are swapped.")]
    public float variantFps = 10f;
    [Tooltip("0 = the spin is smooth. Above 0 the spin moves in jumps this many times a second (like animation drawn on twos).")]
    public float stepFps = 0f;

    [Header("Exit only")]
    [Tooltip("This layer is hidden while the circle builds and holds, and plays only while the circle leaves (a flash, a shockwave, embers). Start At and Draw Time then mean a fraction of the exit.")]
    public bool exitOnly = false;
    [Tooltip("Exit-only layers start at this fraction of their size and grow to full size.")]
    public float exitGrow = 0.4f;

    [Header("While it holds")]
    [Tooltip("The layer slowly grows by this fraction of its size per second while the cast is held (0.05 = 5% a second).")]
    public float holdDrift = 0f;
    [Range(0f, 1f), Tooltip("Flickers the layer's brightness.")]
    public float shimmer = 0f;
}

// A set of circle layers that draw themselves over a spell's cast time. Assign one to a spell to give it a casting circle.
[CreateAssetMenu(menuName = "Magic/Casting Circle", fileName = "NewCastingCircle")]
public class CastingCircleData : ScriptableObject
{
    [Tooltip("How far in front of the hand the 'Front' anchor sits, in metres.")]
    public float frontDistance = 1.3f;

    [Header("Leaving (when the spell is released)")]
    public CircleExit exitStyle = CircleExit.Fade;
    [Tooltip("How long the exit takes in seconds. 0 = use the length the game asks for (short for quick taps).")]
    public float exitDuration = 0f;

    [Header("Stroke glow")]
    [Range(0f, 4f), Tooltip("Soft halo around every stroke, and a hotter white core. 0 = flat lines.")]
    public float glow = 1.2f;
    [Range(0f, 1f), Tooltip("How much the centre of each stroke turns white.")]
    public float coreWhite = 0.35f;

    [Header("Depth (every layer sits at its own distance)")]
    [Tooltip("Each layer sits this much further from the previous one along the circle's depth, so the layers float apart. Upright circles stack away from the viewer; flat circles stack upward. A layer's own Offset z adds to this.")]
    public float depthSpacing = 0.3f;
    [Tooltip("Layers on the ground (Feet / Aim Point) stack upward by this much each, so they stay on the floor instead of floating up.")]
    public float groundLayerSpacing = 0.03f;
    [Tooltip("Each layer slowly tilts and wobbles by up to this many degrees, each on its own rhythm, so the layers shift against each other as the camera moves.")]
    [Range(0f, 25f)] public float tiltDegrees = 8f;
    public float tiltSpeed = 1.2f;
    [Tooltip("Layers on the ground (Feet / Aim Point) stay flat. They only tilt when they are raised at least this high above the ground (set by a negative Offset z, in metres, before the spell's circle scale).")]
    public float groundTiltMinHeight = 0.5f;

    [Header("When the cast is ready (the circle is complete)")]
    [Tooltip("The circle flashes and shifts toward this colour once it is complete.")]
    public Color readyColor = new Color(1f, 0.88f, 0.45f, 1f);
    [Range(0f, 1f), Tooltip("How far it shifts toward the ready colour (0 = no colour change).")]
    public float readyColorBlend = 0.45f;
    [Range(0f, 2f), Tooltip("Strength of the flash at the moment it completes (0 = no flash).")]
    public float readyFlash = 1f;
    [Range(0f, 1f), Tooltip("A pulsing swell while the circle waits to be released. Off by default: switch it on only for the spells that should have it.")]
    public float readyPulse = 0f;

    public CircleLayer[] layers = new CircleLayer[0];
}
