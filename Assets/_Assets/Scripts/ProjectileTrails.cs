using UnityEngine;

// What each projectile leaves behind it: star dust for the arcane spells, embers for fire, glittering snow for ice, drips for venom, streaks for wind,
// a spiral of flame for the Firestorm Lance, spores for the Plague Storm. Called by Projectile.Start with the spell's asset name.
public static class ProjectileTrails
{
    struct T
    {
        public bool billboard, stretch, soft, spiral;
        public float rate, life, size, speed, gravity, spread;
        public Color a, b;
        public Mesh mesh;
    }

    // returns false for spells that keep the plain star trail
    public static bool Attach(Transform follow, string key, Color color, float scale)
    {
        T t = default;
        switch (key)
        {
            case "FireBurst":
                t = new T { billboard = true, soft = true, rate = 45, life = 0.45f, size = 0.45f, speed = 0.4f, gravity = -0.35f, spread = 0.2f, a = new Color(1f, 0.45f, 0.1f), b = new Color(1f, 0.8f, 0.3f) }; break;
            case "InfernalBolt":
                t = new T { billboard = true, soft = true, rate = 60, life = 0.5f, size = 0.35f, speed = 0.5f, gravity = -0.25f, spread = 0.2f, a = new Color(0.3f, 0.05f, 0.04f), b = new Color(1f, 0.3f, 0.1f) }; break;
            case "CinderStorm":
                t = new T { billboard = true, rate = 22, life = 0.5f, size = 0.12f, speed = 0.3f, gravity = -0.15f, spread = 0.12f, a = new Color(1f, 0.55f, 0.2f), b = new Color(1f, 0.9f, 0.5f) }; break;
            case "FirestormLance":
                t = new T { billboard = true, soft = true, spiral = true, rate = 80, life = 0.5f, size = 0.4f, speed = 0.2f, gravity = -0.2f, spread = 0.5f, a = new Color(1f, 0.5f, 0.1f), b = new Color(1f, 0.85f, 0.4f) }; break;
            case "SunLance":
                t = new T { billboard = true, soft = true, rate = 70, life = 0.6f, size = 0.55f, speed = 0.4f, gravity = 0f, spread = 0.4f, a = new Color(1f, 0.8f, 0.3f), b = Color.white }; break;
            case "FrostShard":
            case "GlacierBolt":
                t = new T { billboard = true, rate = 55, life = 0.7f, size = 0.12f, speed = 0.3f, gravity = 0.15f, spread = 0.2f, a = new Color(0.7f, 0.93f, 1f), b = Color.white }; break;
            case "VenomDart":
                t = new T { mesh = MagicFX.OrbMesh(), rate = 28, life = 0.7f, size = 0.09f, speed = 0.1f, gravity = 1.6f, spread = 0.08f, a = new Color(0.4f, 0.9f, 0.25f), b = new Color(0.65f, 0.35f, 0.9f) }; break;
            case "PlagueStorm":
                t = new T { mesh = MagicFX.OrbMesh(), rate = 18, life = 0.8f, size = 0.09f, speed = 0.3f, gravity = -0.1f, spread = 0.2f, a = new Color(0.55f, 0.9f, 0.3f), b = new Color(0.7f, 0.4f, 0.9f) }; break;
            case "WindBlade":
                t = new T { billboard = true, rate = 70, life = 0.3f, size = 0.2f, speed = 0.1f, gravity = 0f, spread = 0.45f, a = new Color(0.7f, 1f, 0.85f), b = Color.white }; break;
            default:
                return false;
        }

        float sc = Mathf.Max(scale, 0.5f);
        var go = new GameObject("FXTrail_" + key);
        go.transform.SetParent(follow, false);
        go.transform.localPosition = Vector3.zero;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.startLifetime = t.life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, t.speed);
        main.startSize = new ParticleSystem.MinMaxCurve(t.size * 0.7f * sc, t.size * 1.3f * sc);
        main.startColor = new ParticleSystem.MinMaxGradient(t.a, t.b);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = t.gravity;

        var em = ps.emission; em.rateOverTime = t.rate;
        var shape = ps.shape; shape.shapeType = t.spiral ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere; shape.radius = t.spread * sc;
        if (t.spiral) { shape.rotation = new Vector3(0f, 0f, 0f); go.AddComponent<ModelSpin>().rate = new Vector3(0f, 0f, 900f); }

        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (t.mesh != null) { r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = t.mesh; r.sharedMaterial = MagicFX.StarMat; }
        else if (t.stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 4f; r.velocityScale = 0.04f; r.sharedMaterial = MagicFX.GlowMat; }
        else { r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = t.soft ? MagicFX.SoftMat : MagicFX.GlowMat; }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return true;
    }
}
