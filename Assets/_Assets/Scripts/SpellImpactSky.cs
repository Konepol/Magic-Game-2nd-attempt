using UnityEngine;

// The effects of the sky-combat spells: the pillar of light of a tall area spell, the field of blasts and the meteor shower of a siege
// spell, and the rubble of broken scenery. Part of SpellImpact (see SpellImpact.cs); built from the same pieces as the other effects.
public static partial class SpellImpact
{
    // a column of light from `bottom` up: a bright core that fades, with rings climbing it so its height reads from any angle
    public static void SkyPillar(Vector3 bottom, float radius, float height, Color c)
    {
        Vector3 top = bottom + Vector3.up * height;
        LaserBeam.Spawn(bottom, top, Mathf.Clamp(radius * 0.45f, 0.5f, 7f), new Color(c.r, c.g, c.b, 0.55f), 0.45f);
        LaserBeam.Spawn(bottom, top, Mathf.Clamp(radius * 0.12f, 0.15f, 2f), Color.Lerp(c, Color.white, 0.7f), 0.6f);

        int rings = Mathf.Clamp(Mathf.RoundToInt(height / 8f), 2, 8);
        for (int i = 0; i < rings; i++)
        {
            float k = (i + 0.5f) / rings;
            Vector3 at = bottom + Vector3.up * height * k;
            float delay = k * 0.25f;
            if (delay < 0.02f) Ring(at, Vector3.up, radius * 1.15f, c, 0.5f, 1f);
            else After(delay, () => Ring(at, Vector3.up, radius * 1.15f, c, 0.5f, 1f));
        }
    }

    // while a siege spell is on its way: the sky darkens and smaller strikes come down around the main one
    public static void ColossalTelegraph(string key, Vector3 point, float radius, float delay, Color c, bool inAir)
    {
        ScreenFX.Fade(new Color(0.05f, 0.03f, 0.08f), 0.4f, Mathf.Max(delay * 0.8f, 0.1f), delay * 0.2f, 0.9f);
        CameraShake.Add(0.15f);
        if (inAir) return;

        int extra = Mathf.Clamp(Mathf.RoundToInt(radius / 4f), 4, 9);
        for (int i = 0; i < extra; i++)
        {
            Vector2 flat = Random.insideUnitCircle.normalized * radius * Random.Range(0.35f, 0.9f);
            Vector3 at = GroundAt(point + new Vector3(flat.x, 0f, flat.y));
            Telegraph(key, at, 6f, delay * Random.Range(0.7f, 1f), c, false);
        }
    }

    // the strike of a siege spell: a white flash, the whole ground shaking, a field of the spell's own blasts across the area,
    // shock rings racing out past its edge, and a wide scorch left behind
    public static void Colossal(string key, Vector3 point, float radius, float height, Color c, bool inAir)
    {
        ScreenFX.Flash(new Color(1f, 0.97f, 0.9f), 0.75f, 0.7f);
        CameraShake.Add(1f);
        TremorShake.Spawn(point, 1.6f);

        Ring(point, Vector3.up, radius * 1.1f, Color.white, 0.6f, 1f);
        After(0.12f, () => Ring(point, Vector3.up, radius * 1.7f, c, 0.9f, 1f));
        After(0.3f, () => Ring(point, Vector3.up, radius * 2.3f, Lighter(c, 0.5f), 1.2f, 1f));

        if (inAir)
        {
            MagicFX.Core(point, radius * 0.9f, Lighter(c, 0.4f), 0.6f);
            return;
        }

        MagicFX.Core(point + Vector3.up * radius * 0.3f, radius * 0.8f, Lighter(c, 0.4f), 0.6f);
        Scorch.Spawn(point, radius * 1.15f, 22f, new Color(0.05f, 0.04f, 0.04f));
        Cracks.Spawn(point, radius * 1.3f, 16f, 0.5f, new Color(0.08f, 0.06f, 0.05f));

        // blasts spread over the area, the inner ones first
        int blasts = Mathf.Clamp(Mathf.RoundToInt(radius * 0.5f), 6, 16);
        for (int i = 0; i < blasts; i++)
        {
            float k = (i + 1f) / blasts;
            float angle = i * 2.399963f;                                   // golden angle: an even spiral
            Vector3 at = GroundAt(point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.85f * Mathf.Sqrt(k));
            After(0.05f + k * 0.55f, () =>
            {
                if (!Play(key, at, Vector3.up, 6f, c)) SpellFX.Explosion(at, 6f, c);
            });
        }
        Dust(point, radius * 0.9f, new Color(0.45f, 0.4f, 0.38f));
    }

    // dust and tumbling stone chunks: scenery that is chipped or brought down
    public static void Rubble(Vector3 p, float size, Color dust, int chunks)
    {
        if (!SpellFX.UseMagicFX) { SpellFX.Shatter(p, dust); return; }
        Dust(p, size, dust);
        Debris(p, size, chunks);
    }

    static void After(float delay, System.Action act)
    {
        SceneGroups.Put(new GameObject("FXLater"), "VFX/Ground").AddComponent<Later>().Setup(delay, act);
    }
}
