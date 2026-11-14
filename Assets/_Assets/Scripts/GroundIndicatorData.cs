using System;
using UnityEngine;

public enum IndicatorShape
{
    Circle,   // an area on the ground (width = the area's diameter)
    Cone,     // a fan that starts at the caster (length = how far it reaches)
    Line,     // a strip that starts at the caster (length = how far it reaches, width = how wide)
}

// How a layer is drawn on while the indicator appears, or how a charge fill is revealed as the spell charges.
public enum IndicatorReveal
{
    FadeIn,        // fades in
    Clockwise,     // drawn on clockwise from the top
    FromCentre,    // grows outward from the middle (a cone grows out from its tip)
    FromBottom,    // drawn on from the near end to the far end (for a line)
    Burst,         // snaps out from the middle with an overshoot
}

[Serializable]
public class IndicatorLayer
{
    public string name = "Layer";
    public Texture2D texture;

    [Header("Look")]
    public bool useSpellColor = true;                  // tinted by the spell's colour; off = use Tint as it is
    public Color tint = Color.white;
    [Range(0f, 1f)] public float opacity = 1f;
    [Tooltip("Draws a soft dark copy underneath so the lines stay readable on pale ground (needs the outline material).")]
    public bool outline = true;

    [Header("Size and place")]
    [Tooltip("Fraction of the whole indicator (1 = as big as the indicator). If 'Absolute Size' is on, this is a size in metres instead.")]
    public float size = 1f;
    public bool absoluteSize = false;
    [Tooltip("Moves the layer, as a fraction of the indicator's width (x) and length (y). Y points away from the caster for a cone or line.")]
    public Vector2 offset = Vector2.zero;

    [Header("Motion")]
    [Tooltip("Degrees per second, negative turns the other way.")]
    public float spin = 0f;
    [Tooltip("Swings back and forth this many degrees (0 = off).")]
    public float swingDegrees = 0f;
    public float swingPeriod = 3f;
    [Tooltip("Slides along the length of the indicator and fades in and out, as a fraction of the length (0 = off). Looks best on a line.")]
    public float slideSpan = 0f;
    public float slidePeriod = 2.4f;
    [Range(0f, 1f)] public float slidePhase = 0f;
    [Tooltip("Moves the middle of the slide, as a fraction of the length.")]
    public float slideShift = 0f;

    [Header("Draw on (fractions of the indicator's draw-on time)")]
    [Range(0f, 1f)] public float startAt = 0f;
    [Range(0.02f, 1f)] public float drawTime = 0.5f;
    public IndicatorReveal appear = IndicatorReveal.Clockwise;

    [Header("Charge fill")]
    [Tooltip("This layer is hidden until the spell starts charging, then it is revealed as the charge grows. It ignores the draw-on settings.")]
    public bool chargeFill = false;
    public IndicatorReveal chargeReveal = IndicatorReveal.Clockwise;
}

// The look of a ground indicator: a flat marker that shows where an aimed spell will land or how far it reaches.
// It draws itself on, keeps moving while the spell is aimed, fills as the spell charges, and flashes when it is ready.
// Assign one to a spell (SpellData > Aim Indicator). Without one, area spells use the default circle.
[CreateAssetMenu(menuName = "Magic/Ground Indicator", fileName = "NewGroundIndicator")]
public class GroundIndicatorData : ScriptableObject
{
    public IndicatorShape shape = IndicatorShape.Circle;

    [Tooltip("Seconds the indicator takes to draw itself on. Layers start and finish inside this time.")]
    public float drawOnSeconds = 0.5f;

    [Header("Stroke glow")]
    [Range(0f, 4f)] public float glow = 1.2f;
    [Range(0f, 1f)] public float coreWhite = 0.35f;

    [Header("When the spell is ready (fully charged)")]
    [Tooltip("How long the ready flash lasts.")]
    public float lockFlashSeconds = 0.4f;
    [Range(0f, 2f), Tooltip("How much brighter everything gets at the flash (0 = no flash).")]
    public float lockFlash = 0.5f;
    [Range(0f, 0.3f), Tooltip("How much the indicator swells at the flash.")]
    public float lockSwell = 0.05f;

    [Header("Readability")]
    [Range(0f, 1f), Tooltip("Strength of the dark copy under each layer that has Outline on (0 = off).")]
    public float outlineStrength = 0.6f;

    public IndicatorLayer[] layers = new IndicatorLayer[0];
}
