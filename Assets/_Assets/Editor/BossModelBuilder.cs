using System.IO;
using UnityEditor;
using UnityEngine;
using MB = WitchBuilder.MeshBuilder;

// Tools > Magic > Build Boss Model
// Builds the Warden of Ruins: a big, round-edged stone guardian in the same soft toon style, with gold trim, shoulder crystals, a crowned head
// with glowing eyes, glowing rune bands and a chest core. Saved as Resources/Boss/WardenModel; the boss loads it when it spawns and keeps
// using its pivots (Head, ArmL, ArmR, LegL, LegR) for the fight animations. The glow parts change colour with the fight phase.
public static class BossModelBuilder
{
    const string PrefabPath = "Assets/_Assets/Resources/Boss/WardenModel.prefab";

    static Color C(float r, float g, float b) => new Color(r, g, b).linear;

    [MenuItem("Tools/Magic/Build Boss Model")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/_Assets/Resources/Boss");

        var stone = C(0.74f, 0.70f, 0.86f); var dark = C(0.52f, 0.48f, 0.70f); var light = C(0.90f, 0.87f, 0.98f); var gold = C(1f, 0.84f, 0.45f);
        var toon = EnemyModelBuilder.ToonMaterial("Boss_Stone", new Color(0.9f, 0.88f, 1f), new Color(0.16f, 0.12f, 0.34f));
        var glow = EnemyModelBuilder.GlowMaterial("Boss_Glow", new Color(1f, 0.8f, 0.3f), 1.8f);

        var root = new GameObject("WardenModel");
        var v = root.transform;

        // legs: a thick leg and a big round boot, pivoting at the hip
        foreach (float s in new[] { -1f, 1f })
        {
            string side = s < 0 ? "L" : "R";
            EnemyModelBuilder.Part(v, "Leg" + side, new Vector3(1.5f * s, 2.7f, 0f), mb =>
            {
                EnemyModelBuilder.Limb(mb, stone, 1.05f, 0.95f, Vector3.zero, new Vector3(0f, -1.2f, 0f), new Vector3(0f, -2.0f, 0f));
                mb.BoxLocal(new Vector3(0f, -2.25f, 0.35f), new Vector3(1.25f, 0.5f, 1.6f), dark, 0f, 3.2f);
                mb.EllipsoidLocal(new Vector3(0f, -1.7f, 0f), new Vector3(1.15f, 0.14f, 1.15f), gold, 0f);
            }, toon, glow, "Warden_Leg" + side);
        }

        // trunk with a lighter belly, a gold belt and two glowing rune bands
        EnemyModelBuilder.Part(v, "Body", new Vector3(0f, 4.2f, 0f), mb =>
        {
            mb.BoxLocal(Vector3.zero, new Vector3(2.5f, 2.2f, 1.9f), stone, 0f, 3.2f);
            mb.BoxLocal(new Vector3(0f, -0.1f, 1.2f), new Vector3(1.7f, 1.6f, 0.8f), light, 0f, 3.4f);
            mb.EllipsoidLocal(new Vector3(0f, -1.55f, 0f), new Vector3(2.7f, 0.2f, 2.1f), gold, 0f);
            mb.SubMesh(1);
            mb.EllipsoidLocal(new Vector3(0f, -1.0f, 0f), new Vector3(2.65f, 0.09f, 2.05f), Color.white, 0f);
            mb.EllipsoidLocal(new Vector3(0f, 1.25f, 0f), new Vector3(2.65f, 0.09f, 2.05f), Color.white, 0f);
        }, toon, glow, "Warden_Body");

        EnemyModelBuilder.Part(v, "Core", new Vector3(0f, 4.2f, 1.95f), mb =>
        {
            mb.SubMesh(1);
            mb.EllipsoidLocal(Vector3.zero, new Vector3(0.75f, 0.75f, 0.3f), Color.white, 0f);
            mb.SubMesh(0);
            mb.StarLocal(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), 1.25f, 0.35f, gold, 0f);
        }, toon, glow, "Warden_Core");

        // shoulders with round pauldrons and crystals
        EnemyModelBuilder.Part(v, "Shoulders", new Vector3(0f, 6.3f, 0f), mb =>
        {
            mb.BoxLocal(Vector3.zero, new Vector3(4.0f, 0.85f, 1.7f), stone, 0f, 3.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.EllipsoidLocal(new Vector3(4.2f * s, 0.15f, 0f), new Vector3(1.55f, 1.25f, 1.55f), dark, 0f);
                mb.EllipsoidLocal(new Vector3(4.2f * s, 0.8f, 0f), new Vector3(1.0f, 0.2f, 1.0f), gold, 0f);
                EnemyModelBuilder.Limb(mb, light, 0.4f, 0.02f, new Vector3(4.2f * s, 0.9f, 0f), new Vector3(4.3f * s, 1.8f, 0f), new Vector3(4.2f * s, 2.5f, 0f));
                EnemyModelBuilder.Limb(mb, light, 0.28f, 0.02f, new Vector3(3.5f * s, 0.8f, 0.1f), new Vector3(3.3f * s, 1.5f, 0.2f));
            }
        }, toon, glow, "Warden_Shoulders");

        // head: a big rounded block with glowing eyes, a gold crown and curved horns
        EnemyModelBuilder.Part(v, "Head", new Vector3(0f, 7.4f, 0.1f), mb =>
        {
            mb.BoxLocal(new Vector3(0f, 0.85f, 0f), new Vector3(1.75f, 1.35f, 1.55f), stone, 0f, 2.6f);
            mb.BoxLocal(new Vector3(0f, 0.2f, 0.85f), new Vector3(1.2f, 0.65f, 0.7f), light, 0f, 3f);
            mb.EllipsoidLocal(new Vector3(0f, 2.05f, 0f), new Vector3(1.55f, 0.17f, 1.4f), gold, 0f);
            for (int i = 0; i < 7; i++)
            {
                float a = (i / 6f - 0.5f) * Mathf.PI * 0.9f;
                var b = new Vector3(Mathf.Sin(a) * 1.3f, 2.1f, Mathf.Cos(a) * 1.1f - 0.1f);
                EnemyModelBuilder.Limb(mb, gold, 0.17f, 0.01f, b, b + new Vector3(Mathf.Sin(a) * 0.15f, 0.7f, 0f));
            }
            foreach (float s in new[] { -1f, 1f })
                EnemyModelBuilder.Limb(mb, light, 0.28f, 0.02f, new Vector3(1.6f * s, 1.5f, 0f), new Vector3(2.3f * s, 1.9f, 0f), new Vector3(2.6f * s, 2.8f, 0.1f));
            mb.SubMesh(1);
            foreach (float s in new[] { -1f, 1f })
                mb.EllipsoidLocal(new Vector3(0.62f * s, 1.0f, 1.5f), new Vector3(0.4f, 0.22f, 0.12f), Color.white, 0f);
            mb.SubMesh(0);
            mb.EllipsoidLocal(new Vector3(0f, 0.2f, 1.58f), new Vector3(0.4f, 0.07f, 0.06f), dark, 0f);
        }, toon, glow, "Warden_Head");

        // arms: a thick arm with a glowing rune ring, a gold cuff and a huge round fist
        foreach (float s in new[] { -1f, 1f })
        {
            string side = s < 0 ? "L" : "R";
            EnemyModelBuilder.Part(v, "Arm" + side, new Vector3(5.1f * s, 6.4f, 0f), mb =>
            {
                EnemyModelBuilder.Limb(mb, stone, 1.0f, 0.95f, Vector3.zero, new Vector3(0f, -1.6f, 0f), new Vector3(0f, -3.2f, 0f));
                mb.EllipsoidLocal(new Vector3(0f, -3.6f, 0f), new Vector3(1.15f, 0.2f, 1.15f), gold, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -5.4f, 0f), new Vector3(1.6f, 1.55f, 1.6f), dark, 0f);
                mb.EllipsoidLocal(new Vector3(0f, -5.2f, 1.1f), new Vector3(0.7f, 0.4f, 0.4f), light, 0f);
                mb.SubMesh(1);
                mb.EllipsoidLocal(new Vector3(0f, -1.3f, 0f), new Vector3(1.1f, 0.08f, 1.1f), Color.white, 0f);
            }, toon, glow, "Warden_Arm" + side);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Boss model built.");
    }
}
