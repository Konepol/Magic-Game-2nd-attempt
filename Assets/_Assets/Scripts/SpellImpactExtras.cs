using UnityEngine;

// Smaller impact pieces shared by several spells: lingering mist (venom, plague, frost) and the dark smoke ring of the Infernal Bolt.
public static partial class SpellImpact
{
    // a soft cloud that stays around for a while
    static void Mist(Vector3 p, float r, Color c, float seconds, int count)
    {
        Emit(p, Vector3.up, new Spec { circle = true, count = count, size = new Vector2(r * 0.5f, r * 0.9f), speed = new Vector2(0.05f, r * 0.5f), life = new Vector2(seconds * 0.7f, seconds), gravity = -0.02f, shapeRadius = r * 0.5f, grow = true, soft = true, a = c, b = Lighter(c, 0.35f) });
    }

    // a ring of dark smoke spreading along the ground
    static void SmokeRing(Vector3 p, Vector3 n, float r, float lod)
    {
        Emit(p, n, new Spec { circle = true, count = N(16, lod), size = new Vector2(r * 0.45f, r * 0.75f), speed = new Vector2(r * 2f, r * 3.2f), life = new Vector2(0.9f, 1.5f), gravity = -0.05f, shapeRadius = r * 0.15f, grow = true, soft = true, a = new Color(0.12f, 0.09f, 0.09f), b = new Color(0.3f, 0.2f, 0.18f) });
        Ring(p, n, r * 2.2f, new Color(0.25f, 0.1f, 0.08f), 0.6f, lod);
    }

    // a ripple that spreads over the barrier from where it was hit
    public static void BarrierRipple(Vector3 point, Transform barrier, Color c)
    {
        if (!SpellFX.UseMagicFX) return;
        Vector3 n = barrier != null ? barrier.forward : Vector3.up;
        var ring = Lighter(new Color(c.r, c.g, c.b, 1f), 0.45f);
        Ring(point, n, 1.8f, ring, 0.45f, 1f);
        Ring(point, n, 0.9f, Color.white, 0.3f, 1f);
        Stars(point, 0.5f, ring, 3, 0.6f);
    }
}
