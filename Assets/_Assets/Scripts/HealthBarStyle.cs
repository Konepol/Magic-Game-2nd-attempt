using UnityEngine;

// One place for the look of every health bar (player HUD bar, enemy bars, barrier bars).
// Change a value here and all of them change together.
public static class HealthBarStyle
{
    public static readonly Color Background = new Color(0.05f, 0.05f, 0.08f, 0.92f);
    public static readonly Color Full = new Color(0.3f, 0.85f, 0.35f);
    public static readonly Color Low = new Color(0.95f, 0.25f, 0.2f);
    public static readonly Color Chip = new Color(1f, 0.85f, 0.4f);      // the pale bar that trails behind after damage

    public const float FillSmoothing = 10f;      // how quickly the main fill follows the real value
    public const float ChipDrainPerSecond = 0.5f; // fraction of the bar the trailing chip drains per second
    public const float LowThreshold = 0.25f;

    // green at healthy, shifting to red as health drops
    public static Color FillColor(float fraction) => Color.Lerp(Low, Full, Mathf.Clamp01(fraction / 0.5f));
}
