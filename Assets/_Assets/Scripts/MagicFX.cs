using System.Collections.Generic;
using UnityEngine;

// Spell effects in the style of the "star synthesis" reference, all in 3D: chubby cartoon stars that tumble in space, a glowing core ball that
// swells and pops, tiny gold sparks, and bright crescent streaks that orbit the core. Everything is built from code (no assets) and tinted by
// the spell's colour, warmed toward gold so it keeps that soft golden-glow look.
public static class MagicFX
{
    static Mesh starMesh, orbMesh;
    static Material starMat, glowMat, softMat, coreMat, streakMat;

    // ---------- shared pieces ----------

    static Color Warm(Color c, float toGold = 0.35f) => Color.Lerp(c, new Color(1f, 0.86f, 0.4f), toGold);

    // a chubby five-pointed star with a rounded bevel, about 1 unit across and 0.5 thick (a lofted outline: a front point, a ring and a back point)
    public static Mesh StarMesh()
    {
        if (starMesh != null) return starMesh;

        const int points = 5;
        var ring = new List<Vector2>();
        for (int i = 0; i < points * 2; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
            float r = i % 2 == 0 ? 0.5f : 0.27f;
            ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }

        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        int front = 0, back = 1;
        v.Add(new Vector3(0f, 0f, 0.2f)); n.Add(Vector3.forward);
        v.Add(new Vector3(0f, 0f, -0.2f)); n.Add(Vector3.back);

        // two rings of vertices (a rounded edge): at 0.55 of the outline pushed in (front and back faces) and at full size (the rim)
        int cnt = ring.Count;
        int fRing = v.Count; for (int i = 0; i < cnt; i++) { v.Add(new Vector3(ring[i].x * 0.55f, ring[i].y * 0.55f, 0.16f)); n.Add(new Vector3(ring[i].x, ring[i].y, 1.2f).normalized); }
        int rimF = v.Count; for (int i = 0; i < cnt; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, 0.07f)); n.Add(new Vector3(ring[i].x, ring[i].y, 0.6f).normalized); }
        int rimB = v.Count; for (int i = 0; i < cnt; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, -0.07f)); n.Add(new Vector3(ring[i].x, ring[i].y, -0.6f).normalized); }
        int bRing = v.Count; for (int i = 0; i < cnt; i++) { v.Add(new Vector3(ring[i].x * 0.55f, ring[i].y * 0.55f, -0.16f)); n.Add(new Vector3(ring[i].x, ring[i].y, -1.2f).normalized); }

        for (int i = 0; i < cnt; i++)
        {
            int j = (i + 1) % cnt;
            t.AddRange(new[] { front, fRing + i, fRing + j });                        // the centre cap
            t.AddRange(new[] { fRing + i, rimF + i, rimF + j, fRing + i, rimF + j, fRing + j });
            t.AddRange(new[] { rimF + i, rimB + i, rimB + j, rimF + i, rimB + j, rimF + j });   // the rim
            t.AddRange(new[] { rimB + i, bRing + i, bRing + j, rimB + i, bRing + j, rimB + j });
            t.AddRange(new[] { back, bRing + j, bRing + i });
        }

        starMesh = new Mesh { name = "MagicStar" };
        starMesh.SetVertices(v); starMesh.SetNormals(n); starMesh.SetTriangles(t, 0);
        starMesh.RecalculateBounds();
        return starMesh;
    }

    static Mesh Orb()
    {
        if (orbMesh != null) return orbMesh;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbMesh = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return orbMesh;
    }

    static Material Unlit(bool additive, Texture tex = null)
    {
        if (additive && tex != null)
        {
            var glow = Shader.Find("Magic/ParticleGlow");           // the URP particle shader ignored the soft texture's alpha, leaving white squares
            if (glow != null)
            {
                var g = new Material(glow);
                g.SetTexture("_BaseMap", tex);
                return g;
            }
        }
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", additive ? 1f : 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        return m;
    }

    static Texture2D SoftDot(int n = 32, float power = 2f)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), power)));
            }
        tex.Apply();
        return tex;
    }

    static void EnsureMaterials()
    {
        if (starMat == null) starMat = Unlit(false);
        if (glowMat == null) glowMat = Unlit(true, SoftDot(32, 2f));
        if (coreMat == null)
        {
            var orb = Shader.Find("Magic/GlowOrb");
            coreMat = orb != null ? new Material(orb) : Unlit(true, SoftDot(64, 1.4f));
        }
        if (streakMat == null) streakMat = Unlit(true, SoftDot(16, 1f));
    }

    // ---------- the pop: stars tumble out, the core swells, sparks fly, a crescent whips around ----------

    public static void Pop(Vector3 pos, float radius, Color color, int stars = 8)
    {
        EnsureMaterials();
        radius = Mathf.Max(radius, 0.4f);
        Color warm = Warm(color, 0.45f);

        // the glowing core: a soft ball that swells and fades
        var core = new GameObject("FXCore").AddComponent<CoreGlow>();
        core.Setup(pos, radius * 0.9f, warm, coreMat, 0.38f);

        // 3D stars tumbling outward
        Burst(pos, StarMesh(), starMat, stars, radius * 0.22f, radius * 0.12f, new Vector2(radius * 1.2f, radius * 2.8f), 0.7f, Warm(color, 0.55f), true);
        // little round sparks
        Burst(pos, Orb(), glowMat, Mathf.RoundToInt(10 + radius * 6f), radius * 0.05f, radius * 0.025f, new Vector2(radius * 0.8f, radius * 3.5f), 0.55f, warm, false);

        // crescent streaks that whip once around the core
        for (int i = 0; i < 2; i++)
        {
            var s = new GameObject("FXStreak").AddComponent<OrbitStreak>();
            s.Setup(pos, radius * 0.7f, warm, streakMat, i * Mathf.PI, 0.45f);
        }
    }

    // the soft glowing ball on its own, for the spell impact effects
    public static void Core(Vector3 pos, float radius, Color color, float seconds = 0.35f)
    {
        EnsureMaterials();
        new GameObject("FXCore").AddComponent<CoreGlow>().Setup(pos, radius, Warm(color, 0.08f), coreMat, seconds);
    }

    public static Material StarMat { get { EnsureMaterials(); return starMat; } }
    public static Material GlowMat { get { EnsureMaterials(); return glowMat; } }
    // the same soft dot but alpha-blended instead of added: shows up on bright ground and sky (flames, dust, venom, clouds)
    public static Material SoftMat
    {
        get
        {
            EnsureMaterials();
            if (softMat == null)
            {
                softMat = new Material(glowMat);
                if (softMat.HasProperty("_DstBlend")) softMat.SetFloat("_DstBlend", 10f);
            }
            return softMat;
        }
    }
    public static Material CoreMat { get { EnsureMaterials(); return coreMat; } }
    public static Mesh OrbMesh() => Orb();

    // a smaller version for hits and impacts
    public static void Hit(Vector3 pos, Color color) => Pop(pos, 0.7f, color, 4);

    // ---------- gathering: sparks and stars fly in toward a point, then it pops (used when a spell is cast from the wand) ----------

    public static void Gather(Vector3 pos, Color color, float seconds = 0.28f)
    {
        EnsureMaterials();
        var g = new GameObject("FXGather").AddComponent<GatherFX>();
        g.Setup(pos, Warm(color, 0.4f), seconds, starMat, glowMat, StarMesh(), Orb());
    }

    // ---------- a trail of little stars behind a projectile ----------

    public static ParticleSystem StarTrail(Transform follow, Color color, float scale = 1f)
    {
        EnsureMaterials();
        var go = new GameObject("FXStarTrail");
        go.transform.SetParent(follow, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * scale, 0.22f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(Warm(color, 0.5f), Color.white);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 26f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f * scale;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = StarMesh();
        r.sharedMaterial = starMat;
        ps.Play();
        return ps;
    }

    // ---------- helpers ----------

    // a one-shot particle burst of meshes with 3D tumbling
    static void Burst(Vector3 pos, Mesh mesh, Material mat, int count, float sizeMin, float sizeMax, Vector2 speed, float life, Color color, bool tumble)
    {
        var go = new GameObject("FXMeshBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax + sizeMin);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = tumble ? 0.35f : 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        if (tumble)
        {
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        if (tumble)
        {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        }

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));

        var vel = ps.limitVelocityOverLifetime;
        vel.enabled = true;
        vel.dampen = 0.15f;
        vel.limit = speed.y * 0.4f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = mesh;
        r.sharedMaterial = mat;
        ps.Play();
    }

    // ---------- small behaviours ----------

    class CoreGlow : MonoBehaviour
    {
        float time, life; Vector3 pos; float radius; Color color; Transform ball; Material mat;

        public void Setup(Vector3 p, float r, Color c, Material m, float seconds)
        {
            pos = p; radius = r; color = c; life = seconds;
            mat = new Material(m);
            transform.position = p;

            ball = new GameObject("Ball").transform;
            ball.SetParent(transform, false);
            // a camera-facing glow quad would be flat, so the glow is two nested spheres: a bright core and a soft halo
            for (int i = 0; i < 2; i++)
            {
                var part = new GameObject(i == 0 ? "Core" : "Halo");
                part.transform.SetParent(ball, false);
                part.AddComponent<MeshFilter>().sharedMesh = Orb();
                var rend = part.AddComponent<MeshRenderer>();
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                part.transform.localScale = Vector3.one * (i == 0 ? 0.55f : 1f);
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / life);
            float swell = k < 0.3f ? Mathf.SmoothStep(0.2f, 1f, k / 0.3f) : Mathf.Lerp(1f, 1.5f, (k - 0.3f) / 0.7f);
            ball.localScale = Vector3.one * radius * 2f * swell;
            float alpha = k < 0.15f ? k / 0.15f : 1f - Mathf.Clamp01((k - 0.15f) / 0.85f);
            var white = Color.Lerp(color, Color.white, k < 0.3f ? 1f - k / 0.3f : 0f);
            var c = white; c.a = alpha * 0.9f;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (time >= life) { Destroy(mat); Destroy(gameObject); }
        }
    }

    class OrbitStreak : MonoBehaviour
    {
        float time, life, radius, phase; Vector3 center; TrailRenderer trail; Vector3 tilt;

        public void Setup(Vector3 c, float r, Color color, Material mat, float startPhase, float seconds)
        {
            center = c; radius = r; life = seconds; phase = startPhase;
            tilt = new Vector3(Random.Range(-0.5f, 0.5f), 1f, Random.Range(-0.5f, 0.5f)).normalized;
            transform.position = c;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = 0.22f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f * Mathf.Max(r, 0.5f)), new Keyframe(0.5f, 0.1f * Mathf.Max(r, 0.5f)), new Keyframe(1f, 0f));
            trail.minVertexDistance = 0.02f;
            trail.startColor = Color.white;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Place(0f);
            trail.Clear();
        }

        void Place(float k)
        {
            float a = phase + k * Mathf.PI * 1.6f;
            Vector3 u = Vector3.Cross(tilt, Vector3.up); if (u.sqrMagnitude < 0.01f) u = Vector3.right;
            u.Normalize();
            Vector3 v = Vector3.Cross(tilt, u);
            float r = radius * (0.8f + 0.5f * k);
            transform.position = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
        }

        void Update()
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / life);
            Place(k);
            if (time >= life + 0.25f) Destroy(gameObject);
        }
    }

    class GatherFX : MonoBehaviour
    {
        struct Bit { public Transform t; public Vector3 from; public float delay; public bool star; public Vector3 spin; }
        readonly List<Bit> bits = new List<Bit>();
        float time, life; Vector3 pos; Color color; Material own;

        public void Setup(Vector3 p, Color c, float seconds, Material starMat, Material dotMat, Mesh star, Mesh orb)
        {
            pos = p; color = c; life = seconds;
            transform.position = p;
            own = new Material(starMat);
            if (own.HasProperty("_BaseColor")) own.SetColor("_BaseColor", c);
            own.color = c;

            for (int i = 0; i < 12; i++)
            {
                bool isStar = i < 4;
                var go = new GameObject(isStar ? "GStar" : "GDot");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = isStar ? star : orb;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = isStar ? own : dotMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                Vector3 dir = Random.onUnitSphere;
                bits.Add(new Bit { t = go.transform, from = dir * Random.Range(0.5f, 0.9f), delay = Random.value * 0.35f, star = isStar, spin = Random.onUnitSphere * 360f });
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / life);
            foreach (var b in bits)
            {
                float local = Mathf.Clamp01((k - b.delay) / (1f - b.delay));
                float e = local * local;
                b.t.localPosition = Vector3.Lerp(b.from, Vector3.zero, e);
                float s = (b.star ? 0.2f : 0.07f) * Mathf.Sin(Mathf.Clamp01(local) * Mathf.PI * 0.5f + 0.2f) * (1f - e * 0.6f);
                b.t.localScale = Vector3.one * Mathf.Max(0.01f, s);
                if (b.star) b.t.Rotate(b.spin * Time.deltaTime, Space.Self);
            }
            if (time >= life)
            {
                Pop(pos, 0.45f, color, 3);
                Destroy(own);
                Destroy(gameObject);
            }
        }
    }
}
