using System.Collections.Generic;
using UnityEngine;

// The wand-arm and upper-body poses of the witch, as humanoid muscle values (muscle index -> -1..1). Shared by the wand placement and the clips.
public static class WitchPoses
{
    // right arm: 46 shoulder up, 47 shoulder front, 48 arm down-up, 49 arm front-back, 50 arm twist, 51 forearm stretch, 52 forearm twist, 53 hand down-up, 54 hand in-out
    // upper body: 0 spine front-back (+ = forward), 3 chest front-back, 12 head nod (+ = up)
    public static readonly Dictionary<int, float> Hold = new Dictionary<int, float> { { 48, -0.35f }, { 49, 0.45f }, { 51, 0.5f }, { 53, 0.1f } };
    public static readonly Dictionary<int, float> Chant = new Dictionary<int, float> { { 48, 0.25f }, { 49, 0.8f }, { 51, 0.0f }, { 53, 0.0f } };
    public static readonly Dictionary<int, float> PullBack = new Dictionary<int, float> { { 48, 0.1f }, { 49, 0.55f }, { 51, 0.65f }, { 53, 0.5f } };
    public static readonly Dictionary<int, float> Thrust = new Dictionary<int, float> { { 48, 0.35f }, { 49, 0.9f }, { 51, -0.1f }, { 53, -0.4f } };

    public static string Format(Dictionary<int, float> pose)
    {
        var parts = new List<string>();
        foreach (var kv in pose) parts.Add(kv.Key + ":" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(",", parts);
    }
}
