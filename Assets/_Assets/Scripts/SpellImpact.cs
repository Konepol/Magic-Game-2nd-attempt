using UnityEngine;

// One impact effect per spell (and per fusion), built from the same pieces as MagicFX: a glow core, tumbling 3D shapes, glowing billboards, stretched
// streaks, shock rings, vortexes and lightning arcs. Each spell mixes them differently so it reads at a glance: fire flares upward, frost shatters,
// wind spins, venom splashes, lightning forks, earth throws rocks. Play returns false for a key it does not know, so callers can fall back.
public static class SpellImpact
{
    // ---------- entry point ----------

    // key: the spell's asset name; normal: the surface the spell hit (up for a ground blast); radius: how big the effect should be
    public static bool Play(string key, Vector3 pos, Vector3 normal, float radius, Color color)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
        normal.Normalize();
        float r = Mathf.Clamp(radius, 0.5f, 7f);
        float lod = Lod();

        switch (key)
        {
            case "ArcaneBolt": Arcane(pos, normal, r, color, lod); break;
            case "ScatterShot": Scatter(pos, normal, r, color, lod); break;
            case "FireBurst": Fire(pos, normal, r, color, lod, 1f); break;
            case "SparkStream": Sparkle(pos, normal, r, color, lod); break;
            case "FrostShard": Frost(pos, normal, r, color, lod, 1f); break;
            case "WindBlade": Wind(pos, normal, r, color, lod, 1f); break;
            case "VenomDart": Venom(pos, normal, r, color, lod); break;

            case "InfernalBolt": Fire(pos, normal, r, color, lod, 1.3f); Ring(pos, normal, r * 1.5f, Lighter(color, 0.3f), 0.4f, lod); break;
            case "CinderStorm": Embers(pos, normal, r, color, lod); break;
            case "FirestormLance": Column(pos, normal, r, color, lod); break;
            case "GlacierBolt": Frost(pos, normal, r, color, lod, 1.5f); Ring(pos, normal, r * 2.2f, color, 0.55f, lod); break;
            case "Cyclone": Wind(pos, normal, r, color, lod, 1.5f); Vortex(pos, normal, r * 0.8f, 3.5f, 7f, color, lod); break;
            case "PlagueStorm": Plague(pos, normal, r, color, lod); break;

            case "LightningLance": Lightning(pos, normal, r, color, lod, 1f); break;
            case "RadiantBeam": Radiant(pos, normal, r, color, lod, 1f); break;
            case "BlizzardLance": Frost(pos, normal, r, color, lod, 1.2f); Vortex(pos, normal, r * 0.7f, 2.5f, 5f, Lighter(color, 0.4f), lod); break;
            case "SunLance": Radiant(pos, normal, r, color, lod, 1.4f); Fire(pos, normal, r * 0.9f, new Color(1f, 0.6f, 0.2f), lod, 1f); break;
            case "EarthSpike": Earth(pos, normal, r, color, lod, 1f); break;
            case "MeteorStrike": Meteor(pos, normal, r, color, lod); break;
            case "AbsoluteZero": Absolute(pos, normal, r, color, lod); break;
            case "ThunderMeteor": Meteor(pos, normal, r, color, lod); Lightning(pos, normal, r * 1.3f, new Color(1f, 0.95f, 0.5f), lod, 1.3f); break;
            case "TremorStorm": Earth(pos, normal, r * 1.2f, color, lod, 1.4f); Ring(pos, normal, r * 2.4f, Lighter(color, 0.3f), 0.5f, lod); break;
            default: return false;
        }
        return true;
    }

    // shots like the 14-pellet Plague Storm spawn many impacts in one frame, so later ones in the same frame are made lighter
    static int lodFrame, lodCount;
    static float Lod()
    {
        if (Time.frameCount != lodFrame) { lodFrame = Time.frameCount; lodCount = 0; }
        lodCount++;
        return lodCount <= 2 ? 1f : lodCount <= 6 ? 0.45f : 0.2f;
    }

    // the glow ball floats a little off the surface so the ground does not cut it in half
    static void Core(Vector3 p, Vector3 n, float radius, Color c, float seconds) => MagicFX.Core(p + n * radius * 0.5f, radius * 0.8f, Lighter(c, 0.2f), seconds);

    static Color Lighter(Color c, float t) => Color.Lerp(c, Color.white, t);
    static int N(int count, float lod) => Mathf.Max(1, Mathf.RoundToInt(count * lod));

    // ---------- the spells ----------

    static void Arcane(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        // a small violet pop: a glow, one thin ring and a few stars
        Core(p, n, r * 0.7f, c, 0.28f);
        Ring(p, n, r * 1.2f, c, 0.3f, lod);
        Stars(p, r * 0.8f, Lighter(c, 0.35f), N(5, lod), 0.9f);
        Dots(p, n, r, Lighter(c, 0.5f), N(8, lod), 180f, 2f, 0f);
    }

    static void Scatter(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 0.5f, c, 0.22f);
        Stars(p, r * 0.6f, Lighter(c, 0.3f), N(3, lod), 0.8f);
        Dots(p, n, r, Lighter(c, 0.4f), N(6, lod), 120f, 2.5f, 0.6f);
    }

    static void Fire(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.9f, c, 0.35f);
        // flames: soft glowing puffs that rise and thin out, hot yellow in the middle of the flare
        Emit(p, n, new Spec { count = N(10, lod), size = new Vector2(r * 0.5f, r * 0.9f), speed = new Vector2(r * 0.6f, r * 2.2f), life = new Vector2(0.4f, 0.8f), gravity = -0.35f, cone = 60f, grow = true, soft = true, a = c, b = new Color(1f, 0.85f, 0.4f) });
        Emit(p, n, new Spec { count = N(12, lod), size = new Vector2(r * 0.07f, r * 0.14f), speed = new Vector2(r * 1f, r * 3.5f), life = new Vector2(0.5f, 1.0f), gravity = -0.15f, cone = 100f, a = new Color(1f, 0.7f, 0.3f), b = new Color(1f, 0.95f, 0.7f) });
        Stars(p, r * 0.7f, new Color(1f, 0.8f, 0.4f), N(4, lod), 0.8f);
    }

    static void Sparkle(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 0.45f, c, 0.2f);
        Emit(p, n, new Spec { stretch = true, count = N(14, lod), size = new Vector2(0.05f, 0.1f), speed = new Vector2(4f, 9f), life = new Vector2(0.2f, 0.4f), cone = 120f, a = Lighter(c, 0.4f), b = Color.white });
        Stars(p, r * 0.5f, Lighter(c, 0.4f), N(2, lod), 0.8f);
    }

    static void Frost(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.7f, Lighter(c, 0.4f), 0.3f);
        // ice shards fly out and drop
        Emit(p, n, new Spec { mesh = Shard(), count = N(Mathf.RoundToInt(8 * mul), lod), size = new Vector2(r * 0.14f, r * 0.3f), speed = new Vector2(r * 1.5f, r * 4f), life = new Vector2(0.5f, 0.9f), gravity = 1.1f, cone = 150f, tumble = true, a = Lighter(c, 0.3f), b = Color.white });
        Ring(p, n, r * 1.6f, Lighter(c, 0.3f), 0.5f, lod);
        // snow-like sparkles that hang in the air
        Emit(p, n, new Spec { count = N(10, lod), size = new Vector2(r * 0.06f, r * 0.12f), speed = new Vector2(0.3f, r * 1.2f), life = new Vector2(0.7f, 1.3f), gravity = 0.05f, cone = 180f, a = c, b = Color.white });
        Stars(p, r * 0.5f, Color.white, N(3, lod), 0.7f);
    }

    static void Wind(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.5f, c, 0.22f);
        // streaks race outward along the surface, and two thin rings chase them
        Emit(p, n, new Spec { circle = true, stretch = true, count = N(16, lod), size = new Vector2(0.05f, 0.1f), speed = new Vector2(r * 3f, r * 6f), life = new Vector2(0.25f, 0.45f), shapeRadius = r * 0.2f, a = c, b = Color.white });
        Ring(p, n, r * 1.8f, Lighter(c, 0.4f), 0.3f, lod);
        Ring(p, n, r * 1.1f, Color.white, 0.22f, lod);
    }

    static void Venom(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 0.5f, c, 0.25f);
        // a sticky splash that falls back down, and a few bubbles that float up
        Emit(p, n, new Spec { mesh = Orb(), count = N(12, lod), size = new Vector2(r * 0.08f, r * 0.16f), speed = new Vector2(r * 1.5f, r * 4.5f), life = new Vector2(0.45f, 0.8f), gravity = 1.6f, cone = 110f, a = c, b = Lighter(c, 0.35f) });
        Emit(p, n, new Spec { count = N(6, lod), size = new Vector2(r * 0.15f, r * 0.3f), speed = new Vector2(0.2f, r * 0.8f), life = new Vector2(0.8f, 1.4f), gravity = -0.12f, cone = 90f, soft = true, a = c, b = Lighter(c, 0.5f) });
    }

    static void Embers(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 0.6f, c, 0.25f);
        // glowing embers that drift up and flicker out
        Emit(p, n, new Spec { count = N(18, lod), size = new Vector2(r * 0.05f, r * 0.12f), speed = new Vector2(r * 0.8f, r * 3f), life = new Vector2(0.7f, 1.4f), gravity = -0.3f, cone = 140f, a = c, b = new Color(1f, 0.9f, 0.55f) });
        Stars(p, r * 0.5f, new Color(1f, 0.8f, 0.4f), N(3, lod), 0.7f);
    }

    static void Column(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 1.0f, c, 0.4f);
        // a tall pillar of fire
        Emit(p, n, new Spec { count = N(14, lod), size = new Vector2(r * 0.5f, r * 0.8f), speed = new Vector2(r * 2.5f, r * 5f), life = new Vector2(0.35f, 0.7f), gravity = -0.2f, cone = 14f, shapeRadius = r * 0.3f, grow = true, soft = true, a = c, b = new Color(1f, 0.9f, 0.5f) });
        Emit(p, n, new Spec { stretch = true, count = N(10, lod), size = new Vector2(0.06f, 0.12f), speed = new Vector2(r * 3f, r * 7f), life = new Vector2(0.3f, 0.6f), cone = 20f, shapeRadius = r * 0.25f, a = new Color(1f, 0.8f, 0.4f), b = Color.white });
        Ring(p, n, r * 1.8f, c, 0.4f, lod);
        Stars(p, r * 0.8f, new Color(1f, 0.85f, 0.4f), N(5, lod), 1f);
    }

    static void Plague(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, n, r * 0.6f, c, 0.25f);
        // a poison cloud that swells and thins out, with bubbles popping up in it
        Emit(p, n, new Spec { count = N(7, lod), size = new Vector2(r * 0.5f, r * 0.9f), speed = new Vector2(0.2f, r * 1.2f), life = new Vector2(0.8f, 1.3f), gravity = -0.05f, cone = 150f, grow = true, soft = true, a = new Color(c.r * 0.8f, c.g * 0.9f, c.b * 0.6f), b = c });
        Emit(p, n, new Spec { mesh = Orb(), count = N(6, lod), size = new Vector2(r * 0.07f, r * 0.14f), speed = new Vector2(r * 0.5f, r * 2f), life = new Vector2(0.6f, 1.1f), gravity = -0.25f, cone = 150f, a = Lighter(c, 0.3f), b = Lighter(c, 0.6f) });
    }

    static void Lightning(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.6f, Lighter(c, 0.5f), 0.22f);
        if (lod > 0.3f) Arcs.Spawn(p, n, r * 1.8f, Lighter(c, 0.5f), Mathf.Max(3, Mathf.RoundToInt(6 * mul)));
        Emit(p, n, new Spec { stretch = true, count = N(12, lod), size = new Vector2(0.04f, 0.08f), speed = new Vector2(6f, 12f), life = new Vector2(0.15f, 0.3f), cone = 170f, a = c, b = Color.white });
        Stars(p, r * 0.5f, Lighter(c, 0.4f), N(3, lod), 0.8f);
    }

    static void Radiant(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.9f, Lighter(c, 0.5f), 0.3f);
        // thin rays of light shoot out in every direction
        Emit(p, n, new Spec { stretch = true, stretchLength = 8f, count = N(14, lod), size = new Vector2(0.05f, 0.09f), speed = new Vector2(r * 4f, r * 8f), life = new Vector2(0.2f, 0.4f), cone = 180f, a = c, b = Color.white });
        Stars(p, r * 0.8f, new Color(1f, 0.9f, 0.5f), N(6, lod), 1f);
        Ring(p, n, r * 1.6f, Lighter(c, 0.4f), 0.35f, lod);
    }

    static void Earth(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, n, r * 0.6f, c, 0.25f);
        var rock = new Color(0.62f, 0.45f, 0.3f);
        // rocks thrown up and back down, plus a low puff of dust
        Emit(p, n, new Spec { mesh = Chunk(), count = N(10, lod), size = new Vector2(r * 0.1f, r * 0.24f), speed = new Vector2(r * 2f, r * 5f), life = new Vector2(0.7f, 1.2f), gravity = 2f, cone = 70f, tumble = true, a = rock, b = Lighter(rock, 0.3f) });
        Emit(p, n, new Spec { circle = true, count = N(8, lod), size = new Vector2(r * 0.4f, r * 0.7f), speed = new Vector2(r * 1f, r * 2.5f), life = new Vector2(0.5f, 0.9f), shapeRadius = r * 0.3f, grow = true, soft = true, a = new Color(0.75f, 0.62f, 0.45f), b = new Color(0.9f, 0.8f, 0.6f) });
        Ring(p, n, r * 1.6f, Lighter(c, 0.3f), 0.4f, lod);
    }

    static void Meteor(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Fire(p, n, r * 1.2f, c, lod, 1f);
        Earth(p, n, r * 0.8f, new Color(0.8f, 0.5f, 0.3f), lod, 1f);
        Ring(p, n, r * 2.6f, new Color(1f, 0.7f, 0.3f), 0.6f, lod);
    }

    static void Absolute(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Frost(p, n, r, c, lod, 1.5f);
        Ring(p, n, r * 1.6f, Color.white, 0.5f, lod);
        Ring(p, n, r * 2.6f, c, 0.8f, lod);
        Vortex(p, n, r * 1.2f, 3f, 4f, Lighter(c, 0.5f), lod);
    }

    // ---------- the building blocks ----------

    class Spec
    {
        public Mesh mesh;                         // null: a soft glowing billboard
        public int count = 8;
        public Vector2 size = new Vector2(0.1f, 0.2f), speed = new Vector2(1f, 3f), life = new Vector2(0.4f, 0.7f);
        public float gravity, cone = 180f, shapeRadius = 0.05f, spin, rise, stretchLength = 3f;
        public bool tumble, stretch, grow, circle, flat, soft;
        public AnimationCurve sizeCurve;          // overrides the default size over lifetime
        public Color a = Color.white, b = Color.white;
    }

    // a one-shot particle burst; dir is the way the burst opens (the surface normal)
    static void Emit(Vector3 pos, Vector3 dir, Spec s)
    {
        var go = new GameObject("FXImpact");
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(s.life.x, s.life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(s.speed.x, s.speed.y);
        float boost = s.mesh == null && !s.stretch ? 1.4f : 1.2f;                     // everything reads a little bigger in the game view than in the numbers
        main.startSize = new ParticleSystem.MinMaxCurve(s.size.x * boost, s.size.y * boost);
        main.startColor = new ParticleSystem.MinMaxGradient(s.a, s.b);
        main.gravityModifier = s.gravity;
        main.simulationSpace = s.flat ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        if (s.tumble)
        {
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        }

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.count) });

        var shape = ps.shape;
        if (s.circle) { shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = s.shapeRadius; }
        else if (s.cone >= 170f) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = s.shapeRadius; }
        else if (s.cone > 95f) { shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = s.shapeRadius; }
        else { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = s.cone; shape.radius = s.shapeRadius; }

        if (s.spin != 0f || s.rise != 0f)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(s.rise);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(s.spin);
        }

        // billboards fade out, solid shapes shrink away
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, s.sizeCurve != null ? s.sizeCurve : s.mesh == null
            ? (s.grow ? new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.25f)) : AnimationCurve.Linear(0f, 1f, 1f, 0.4f))
            : new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.12f, 1f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0f)));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(s.mesh == null ? 0.7f : 1f, 0.45f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (s.mesh != null) { r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = s.mesh; r.sharedMaterial = MagicFX.StarMat; if (s.flat) r.alignment = ParticleSystemRenderSpace.Local; }
        else if (s.stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = s.stretchLength; r.velocityScale = 0.05f; r.sharedMaterial = MagicFX.GlowMat; }
        else { r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = s.soft ? MagicFX.SoftMat : MagicFX.GlowMat; }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    static void Stars(Vector3 p, float r, Color c, int count, float size)
    {
        Emit(p, Vector3.up, new Spec { mesh = MagicFX.StarMesh(), count = count, size = new Vector2(r * 0.12f * size, r * 0.26f * size), speed = new Vector2(r * 1.2f, r * 3f), life = new Vector2(0.5f, 0.9f), gravity = 0.4f, tumble = true, a = c, b = Color.white });
    }

    static void Dots(Vector3 p, Vector3 n, float r, Color c, int count, float cone, float speedMul, float gravity)
    {
        Emit(p, n, new Spec { count = count, size = new Vector2(r * 0.05f, r * 0.1f), speed = new Vector2(r * 0.8f, r * speedMul * 1.5f), life = new Vector2(0.4f, 0.8f), gravity = gravity, cone = cone, a = c, b = Color.white });
    }

    // a spiralling column: streaks climb along the normal while circling it
    static void Vortex(Vector3 p, Vector3 n, float r, float height, float spin, Color c, float lod)
    {
        Emit(p, n, new Spec { circle = true, stretch = true, stretchLength = 2f, count = N(18, lod), size = new Vector2(0.06f, 0.12f), speed = new Vector2(0f, 0f), life = new Vector2(0.5f, 0.8f), shapeRadius = r * 0.7f, rise = height, spin = spin, a = c, b = Color.white });
    }

    // a thin glowing ring that races outward along the surface and fades
    static void Ring(Vector3 p, Vector3 n, float radius, Color c, float seconds, float lod)
    {
        if (lod < 0.3f) return;
        var grow = new AnimationCurve(new Keyframe(0f, 0.15f, 5f, 5f), new Keyframe(1f, 1f, 0f, 0f));
        Emit(p + n * 0.05f, n, new Spec { mesh = Torus(), flat = true, count = 1, size = new Vector2(radius, radius), speed = new Vector2(0f, 0f), life = new Vector2(seconds, seconds), shapeRadius = 0.001f, cone = 5f, sizeCurve = grow, a = Lighter(c, 0.3f), b = Lighter(c, 0.3f) });
    }

    // ---------- meshes ----------

    static Mesh shardMesh, chunkMesh, ringMesh;

    static Mesh Orb() => MagicFX.OrbMesh();

    // a long four-sided crystal (a bipyramid) about 1 unit long: ice shards
    static Mesh Shard()
    {
        if (shardMesh != null) return shardMesh;
        var v = new[] { new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, -0.5f), new Vector3(0.14f, 0f, 0f), new Vector3(0f, 0.14f, 0f), new Vector3(-0.14f, 0f, 0f), new Vector3(0f, -0.14f, 0f) };
        var verts = new System.Collections.Generic.List<Vector3>(); var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 4; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % 4;
            AddFlat(verts, tris, v[0], v[a], v[b]);
            AddFlat(verts, tris, v[1], v[b], v[a]);
        }
        shardMesh = Build("ImpactShard", verts, tris);
        return shardMesh;
    }

    // a rough little rock: a squashed cube
    static Mesh Chunk()
    {
        if (chunkMesh != null) return chunkMesh;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chunkMesh = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return chunkMesh;
    }

    static void AddFlat(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        // wound so the face looks away from the middle
        Vector3 nrm = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(nrm, (a + b + c) / 3f) >= 0f) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
        else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); }
    }

    static Mesh Build(string name, System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris)
    {
        var m = new Mesh { name = name };
        m.SetVertices(verts); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // a thin torus of radius 1 lying in the xy plane
    public static Mesh Torus()
    {
        if (ringMesh != null) return ringMesh;
        const int major = 40, minor = 6; const float thick = 0.045f;
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= major; i++)
        {
            float a = i / (float)major * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= minor; j++)
            {
                float b = j / (float)minor * Mathf.PI * 2f;
                v.Add(dir * (1f + Mathf.Cos(b) * thick) + Vector3.forward * Mathf.Sin(b) * thick);
            }
        }
        for (int i = 0; i < major; i++)
            for (int j = 0; j < minor; j++)
            {
                int a = i * (minor + 1) + j, b = a + 1, d = a + minor + 1, e = d + 1;
                t.AddRange(new[] { a, d, b, b, d, e });
            }
        ringMesh = Build("ImpactTorus", v, t);
        return ringMesh;
    }

    // ---------- behaviours ----------

    // forking lightning bolts that flicker for a moment around the impact
    class Arcs : MonoBehaviour
    {
        LineRenderer[] lines; Vector3 pos, normal; float reach, time, nextJitter; Material mat;
        const float Life = 0.3f;

        public static void Spawn(Vector3 p, Vector3 n, float reach, Color c, int count)
        {
            var a = new GameObject("FXArcs").AddComponent<Arcs>();
            a.pos = p; a.normal = n; a.reach = reach;
            a.mat = SpellVisuals.Make(c, true);
            a.lines = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Arc");
                go.transform.SetParent(a.transform, false);
                var l = go.AddComponent<LineRenderer>();
                l.useWorldSpace = true;
                l.sharedMaterial = a.mat;
                l.numCapVertices = 3;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                l.receiveShadows = false;
                a.lines[i] = l;
            }
            a.Reroll(0f);
        }

        void Reroll(float k)
        {
            foreach (var l in lines)
            {
                Vector3 dir = (Random.onUnitSphere + normal * 0.6f).normalized;
                Vector3 end = pos + dir * reach * Random.Range(0.6f, 1f);
                const int segs = 7;
                l.positionCount = segs + 1;
                Vector3 side1 = Vector3.Cross(dir, Vector3.up); if (side1.sqrMagnitude < 0.01f) side1 = Vector3.right;
                side1.Normalize();
                Vector3 side2 = Vector3.Cross(dir, side1);
                for (int s = 0; s <= segs; s++)
                {
                    float f = s / (float)segs;
                    float jitter = s == 0 || s == segs ? 0f : reach * 0.07f;
                    l.SetPosition(s, Vector3.Lerp(pos, end, f) + (side1 * Random.Range(-1f, 1f) + side2 * Random.Range(-1f, 1f)) * jitter);
                }
                float w = 0.07f * (1f - k);
                l.startWidth = w; l.endWidth = w * 0.3f;
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            float k = time / Life;
            if (k >= 1f) { Destroy(mat); Destroy(gameObject); return; }
            if (time >= nextJitter) { nextJitter = time + 0.05f; Reroll(k); }
        }
    }
}
