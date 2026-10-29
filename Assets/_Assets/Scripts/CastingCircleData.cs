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
}

// A set of circle layers that draw themselves over a spell's cast time. Assign one to a spell to give it a casting circle.
[CreateAssetMenu(menuName = "Magic/Casting Circle", fileName = "NewCastingCircle")]
public class CastingCircleData : ScriptableObject
{
    [Tooltip("How far in front of the hand the 'Front' anchor sits, in metres.")]
    public float frontDistance = 1.3f;

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
