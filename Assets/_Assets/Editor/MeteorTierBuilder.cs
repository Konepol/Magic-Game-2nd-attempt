using UnityEditor;
using UnityEngine;

// Tools > Magic > Build Meteor Charge Tiers
// Turns Meteor Strike into a spell that has to be charged: a tap only reminds you to hold, passing the cast time makes it "Cataclysm",
// and holding on to the second threshold makes it "Apocalypse" (much bigger and harder hitting, with a bigger circle drawn on top).
// Everything is stored on the Meteor Strike asset (Charge Tiers) and the new circle asset, so it can all be tuned by hand afterwards.
public static class MeteorTierBuilder
{
    const string WeaponDir = "Assets/_Assets/Weapons";
    const string CircleDir = "Assets/_Assets/Circles";
    const string ImageDir = "Assets/_Assets/Images/Circles";

    [MenuItem("Tools/Magic/Build Meteor Charge Tiers")]
    public static void Build()
    {
        var meteor = AssetDatabase.LoadAssetAtPath<SpellData>(WeaponDir + "/MeteorStrike.asset");
        if (meteor == null) { Debug.LogError("Build Meteor Charge Tiers: MeteorStrike.asset not found."); return; }

        // the second circle: bigger, busier, drawn on top of the first while charging toward the second threshold.
        // Ground layers are multipliers of the blast diameter, so they grow with the tier's bigger blast on their own.
        var big = Circle("Circle_Meteor_Greater", 1.5f,
            Aoe(L("Outer Ring", "Ring_Double", CircleAnchor.AimPoint, 1.12f, -6f, 0.0f, 0.5f)),
            Aoe(L("Outer Runes", "Rune_Band", CircleAnchor.AimPoint, 0.98f, 10f, 0.1f, 0.5f)),
            Aoe(L("Star", "Star8", CircleAnchor.AimPoint, 0.74f, -22f, 0.3f, 0.5f)),
            Aoe(L("Segments", "Segments", CircleAnchor.AimPoint, 0.5f, 35f, 0.45f, 0.45f)),
            L("Great Sigil", "Hexagram", CircleAnchor.Front, 3.4f, 14f, 0.0f, 0.6f),
            L("Great Runes", "Rune_Band", CircleAnchor.Front, 2.9f, -20f, 0.15f, 0.55f),
            L("Great Ring", "Ring_Double", CircleAnchor.Front, 3.9f, 8f, 0.05f, 0.5f),
            L("Great Star", "Star8", CircleAnchor.Front, 1.7f, 60f, 0.4f, 0.45f, CircleAppear.ScaleUp),
            L("Great Glow", "Glow", CircleAnchor.Front, 4.4f, 0f, 0.2f, 0.6f, CircleAppear.FadeIn, 0.35f),
            L("Floor Ring", "Ring_Double", CircleAnchor.Feet, 5.2f, 6f, 0.3f, 0.5f),
            L("Floor Segments", "Segments", CircleAnchor.Feet, 4.2f, -28f, 0.5f, 0.45f, CircleAppear.ScaleUp));
        big.readyPulse = 0.8f;

        meteor.tapDoesNothing = true;
        meteor.hasHoldVariant = true;
        meteor.holdThreshold = 1.5f;                       // first threshold: Cataclysm
        meteor.holdDisplayName = "Cataclysm";
        meteor.hold.damage = 120f;
        meteor.hold.aoeRadius = 9f;
        meteor.hold.manaCost = 50f;
        meteor.hold.aoeDelay = 1f;
        meteor.extraTiers = new[]
        {
            new ChargeTier
            {
                displayName = "Apocalypse", time = 3.4f,   // second threshold
                damageMultiplier = 2.6f, sizeMultiplier = 1.7f, manaMultiplier = 1.6f,
                circle = big, circleScale = 1.15f,
            },
        };

        EditorUtility.SetDirty(meteor);
        AssetDatabase.SaveAssets();
        Debug.Log("Meteor Strike now has charge tiers: tap does nothing, " + meteor.hold.damage + " damage at " + meteor.holdThreshold
                  + "s (Cataclysm), then " + meteor.extraTiers[0].displayName + " at " + meteor.extraTiers[0].time + "s.");
    }

    static CircleLayer L(string name, string image, CircleAnchor anchor, float size, float rot, float startAt, float drawTime,
        CircleAppear appear = CircleAppear.RadialWipe, float opacity = 1f)
    {
        return new CircleLayer
        {
            name = name, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImageDir + "/" + image + ".png"),
            anchor = anchor, size = size, rotationSpeed = rot, startAt = startAt, drawTime = drawTime, appear = appear, opacity = opacity,
        };
    }

    static CircleLayer Aoe(CircleLayer l) { l.scaleToAoeRadius = true; return l; }

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
