using System.Collections.Generic;
using UnityEngine;

// The ground and sky spells: stone spikes, falling meteors, the Absolute Zero freeze wave, Tremor Storm's waves of eruptions, Cyclone's pull,
// plus the decals (cracks, scorch, frost), the screen dim/flash and the debris they share. Part of SpellImpact (see SpellImpact.cs).
public static partial class SpellImpact
{
    // ---------- before the strike (the telegraph delay of a ground spell) ----------

    public static void Telegraph(string key, Vector3 point, float radius, float delay, Color c, bool inAir)
    {
        if (string.IsNullOrEmpty(key) || inAir) return;
        switch (key)
        {
            case "MeteorStrike": MeteorFall.Spawn(point, radius, delay, c, false); break;
            case "ThunderMeteor": MeteorFall.Spawn(point, radius, delay, new Color(1f, 0.9f, 0.5f), true); break;
            case "AbsoluteZero": ScreenFX.Fade(new Color(0.04f, 0.09f, 0.22f), 0.6f, delay, 0.5f, 1.4f); Dust(point, radius * 0.4f, new Color(0.8f, 0.95f, 1f)); break;
            case "TremorStorm": Cracks.Spawn(point, radius, delay + 3f, delay * 0.9f, new Color(0.12f, 0.09f, 0.07f)); CameraShake.AddAt(point, 0.12f); break;
            case "EarthSpike": Dust(point, radius * 0.5f, new Color(0.75f, 0.62f, 0.45f)); break;
        }
    }

    // ---------- the strikes ----------

    static void EarthGround(Vector3 p, Vector3 n, float r, Color c, float lod, float mul)
    {
        r *= mul;
        Core(p, Vector3.up, r * 0.3f, new Color(0.9f, 0.75f, 0.5f), 0.2f);
        Spikes(p, r, N(Mathf.RoundToInt(5 * mul + 3), lod), 0.9f, StoneColors, 0f, 0.9f, 1.2f);
        Dust(p, r, new Color(0.78f, 0.65f, 0.48f));
        Debris(p, r, N(10, lod));
        Cracks.Spawn(p, r * 1.1f, 4f, 0.25f, new Color(0.12f, 0.09f, 0.07f));
        CameraShake.AddAt(p, 0.18f * mul);
    }

    static void MeteorGround(Vector3 p, Vector3 n, float r, Color c, float lod, bool thunder)
    {
        MagicFX.Core(p + Vector3.up * r * 0.4f, r * 0.9f, thunder ? Color.white : new Color(1f, 0.7f, 0.35f), 0.4f);
        Ring(p, Vector3.up, r * 2.6f, new Color(1f, 0.75f, 0.35f), 0.7f, lod);
        Ring(p, Vector3.up, r * 1.6f, Color.white, 0.4f, lod);
        Emit(p, Vector3.up, new Spec { count = N(14, lod), size = new Vector2(r * 0.5f, r * 0.9f), speed = new Vector2(r * 0.8f, r * 2.6f), life = new Vector2(0.4f, 0.8f), gravity = -0.3f, cone = 70f, grow = true, soft = true, a = new Color(1f, 0.55f, 0.15f), b = new Color(1f, 0.85f, 0.4f) });
        // dark smoke that rises and spreads
        Emit(p, Vector3.up, new Spec { count = N(10, lod), size = new Vector2(r * 0.7f, r * 1.2f), speed = new Vector2(r * 0.4f, r * 1.6f), life = new Vector2(1.2f, 2f), gravity = -0.08f, cone = 60f, grow = true, soft = true, a = new Color(0.16f, 0.14f, 0.14f), b = new Color(0.32f, 0.28f, 0.26f) });
        Emit(p, Vector3.up, new Spec { mesh = Chunk(), count = N(16, lod), size = new Vector2(r * 0.08f, r * 0.22f), speed = new Vector2(r * 2f, r * 6f), life = new Vector2(0.9f, 1.6f), gravity = 2f, cone = 80f, tumble = true, a = new Color(0.35f, 0.24f, 0.17f), b = new Color(0.6f, 0.4f, 0.25f) });
        Stars(p, r, new Color(1f, 0.8f, 0.4f), N(6, lod), 1f);
        Scorch.Spawn(p, r * 1.7f, 12f, new Color(0.05f, 0.04f, 0.04f));
        Cracks.Spawn(p, r * 1.9f, 10f, 0.3f, new Color(0.08f, 0.06f, 0.05f));
        if (thunder)
        {
            if (lod > 0.3f) Arcs.Spawn(p + Vector3.up * 0.3f, Vector3.up, r * 2.4f, new Color(1f, 0.95f, 0.55f), 9);
            ScreenFX.Flash(new Color(1f, 1f, 0.95f), 0.7f, 0.35f);
        }
        CameraShake.AddAt(p, thunder ? 0.6f : 0.5f);
    }

    // the freeze wave: ice crystals grow outward across the ground ring by ring
    static void AbsoluteGround(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, Vector3.up, r * 0.5f, Color.white, 0.5f);
        Ring(p, Vector3.up, r * 0.9f, Color.white, 0.55f, lod);
        Ring(p, Vector3.up, r * 1.6f, c, 0.9f, lod);
        FrostGround.Spawn(p, r * 1.5f, 7f);
        for (int ring = 0; ring < 5; ring++)
            Spikes(p, r * (0.2f + ring * 0.2f), N(8 + ring * 3, lod), 1.4f - ring * 0.1f, IceColors, 0.12f * ring, 0.5f, 3.2f);
        Emit(p, Vector3.up, new Spec { mesh = Shard(), count = N(30, lod), size = new Vector2(r * 0.05f, r * 0.12f), speed = new Vector2(r * 1f, r * 3f), life = new Vector2(0.8f, 1.4f), gravity = 0.6f, cone = 120f, tumble = true, a = new Color(0.7f, 0.92f, 1f), b = Color.white });
        Emit(p, Vector3.up, new Spec { circle = true, count = N(24, lod), size = new Vector2(r * 0.4f, r * 0.7f), speed = new Vector2(r * 2f, r * 4f), life = new Vector2(0.8f, 1.4f), shapeRadius = r * 0.2f, grow = true, soft = true, a = new Color(0.75f, 0.92f, 1f), b = Color.white });
        Stars(p, r * 0.6f, Color.white, N(8, lod), 1f);
        CameraShake.AddAt(p, 0.35f);
    }

    // repeated eruptions across a wide area: three waves of spikes spreading outward, cracks, heavy dust, shaking
    static void TremorGround(Vector3 p, Vector3 n, float r, Color c, float lod)
    {
        Core(p, Vector3.up, r * 0.3f, new Color(0.9f, 0.7f, 0.45f), 0.25f);
        for (int wave = 0; wave < 3; wave++)
        {
            float radius = r * (0.35f + wave * 0.3f);
            Spikes(p, radius, N(8 + wave * 4, lod), 1.1f, StoneColors, wave * 0.35f, 0.6f, 1.6f);
            if (wave > 0) RingLater(p, r * (0.9f + wave * 0.5f), wave * 0.35f);
        }
        Dust(p, r * 1.3f, new Color(0.7f, 0.58f, 0.42f));
        Dust(p, r * 0.8f, new Color(0.62f, 0.5f, 0.38f));
        Debris(p, r, N(18, lod));
        Cracks.Spawn(p, r * 1.3f, 10f, 0.4f, new Color(0.1f, 0.07f, 0.05f));
        TremorShake.Spawn(p, 1.2f);
    }

    static void RingLater(Vector3 p, float radius, float delay) { SceneGroups.Put(new GameObject("FXLater"), "VFX/Ground").AddComponent<Later>().Setup(delay, () => Ring(p, Vector3.up, radius, new Color(0.9f, 0.78f, 0.55f), 0.5f, 1f)); }

    // ---------- Cyclone: a ground vortex that drags enemies to its centre ----------

    public static void CycloneStart(Vector3 p, float radius, float seconds, float damagePerSecond, string tag)
    {
        SceneGroups.Put(new GameObject("CycloneField"), "VFX/Ground").AddComponent<CycloneField>().Setup(p, radius, seconds, damagePerSecond, tag);
    }

    // ---------- shared pieces ----------

    static readonly Color[] StoneColors = { new Color(0.52f, 0.42f, 0.32f), new Color(0.46f, 0.44f, 0.42f), new Color(0.62f, 0.5f, 0.36f), new Color(0.38f, 0.34f, 0.3f) };
    static readonly Color[] IceColors = { new Color(0.65f, 0.9f, 1f), new Color(0.85f, 0.97f, 1f), new Color(0.5f, 0.78f, 1f) };

    static void Dust(Vector3 p, float r, Color c)
    {
        Emit(p, Vector3.up, new Spec { circle = true, count = 10, size = new Vector2(r * 0.35f, r * 0.6f), speed = new Vector2(r * 0.8f, r * 2f), life = new Vector2(0.7f, 1.3f), shapeRadius = r * 0.25f, grow = true, soft = true, a = c, b = Lighter(c, 0.3f) });
    }

    static void Debris(Vector3 p, float r, int count)
    {
        Emit(p, Vector3.up, new Spec { mesh = Chunk(), count = count, size = new Vector2(r * 0.05f, r * 0.14f), speed = new Vector2(r * 1.5f, r * 4.5f), life = new Vector2(0.8f, 1.4f), gravity = 2f, cone = 75f, tumble = true, a = new Color(0.42f, 0.33f, 0.25f), b = new Color(0.62f, 0.5f, 0.36f) });
    }

    // stone or ice spikes erupting from the ground in a ring (radius 0 = a single one in the middle); wave start delay in seconds
    static void Spikes(Vector3 center, float radius, int count, float height, Color[] palette, float delay, float hold, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * (radius > 0.01f ? Random.Range(0.85f, 1.1f) : 0f);
            pos = GroundAt(pos);
            var go = SceneGroups.Put(new GameObject("FXSpike"), "VFX/Ground");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.value * 360f, Random.Range(-14f, 14f));
            float s = scale * Random.Range(0.55f, 1f);
            go.transform.localScale = new Vector3(s * 0.6f, s * height, s * 0.6f);
            go.AddComponent<MeshFilter>().sharedMesh = SpikeMesh();
            var r = go.AddComponent<MeshRenderer>();
            Color col = palette[Random.Range(0, palette.Length)];
            bool ice = palette == IceColors;
            r.sharedMaterial = LitMaterial(col, ice ? 0.85f : 0.1f, ice ? col * 0.5f : Color.black);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<SpikeRise>().Setup(delay + Random.value * 0.15f, hold + Random.value * 0.3f, s * height * 1.05f);
        }
    }

    static Vector3 GroundAt(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return p;
    }

    static readonly Dictionary<int, Material> litMats = new Dictionary<int, Material>();

    static Material LitMaterial(Color c, float smooth, Color emission)
    {
        int key = (Mathf.RoundToInt(c.r * 31) << 20) | (Mathf.RoundToInt(c.g * 31) << 15) | (Mathf.RoundToInt(c.b * 31) << 10) | Mathf.RoundToInt(smooth * 10) | (emission.maxColorComponent > 0.01f ? 1 << 30 : 0);
        if (litMats.TryGetValue(key, out var m) && m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.SetColor("_BaseColor", c); m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0.01f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
        litMats[key] = m;
        return m;
    }

    static Mesh spikeMesh;
    // a faceted rock/ice spike: a six-sided cone 1 unit tall with a rough base
    static Mesh SpikeMesh()
    {
        if (spikeMesh != null) return spikeMesh;
        var v = new List<Vector3>(); var t = new List<int>();
        var ring = new Vector3[6];
        var rng = new System.Random(11);
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f; float rr = 0.5f * (0.8f + (float)rng.NextDouble() * 0.4f);
            ring[i] = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
        }
        Vector3 apex = new Vector3(0.04f, 1f, -0.03f), mid = new Vector3(0f, 0.35f, 0f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % 6];
            AddFlat(v, t, apex, a, b);
            AddFlat(v, t, b, a, new Vector3(0f, -0.2f, 0f));
        }
        spikeMesh = Build("ImpactSpike", v, t);
        return spikeMesh;
    }

    // ---------- behaviours ----------

    class Later : MonoBehaviour
    {
        float wait; System.Action act;
        public void Setup(float seconds, System.Action a) { wait = seconds; act = a; }
        void Update() { wait -= Time.deltaTime; if (wait <= 0f) { act?.Invoke(); Destroy(gameObject); } }
    }

    // pushes a spike up out of the ground, holds, then sinks it back
    class SpikeRise : MonoBehaviour
    {
        float time, delay, hold, depth; Vector3 basePos;
        public void Setup(float d, float h, float dep) { delay = d; hold = h; depth = dep; basePos = transform.position; transform.position = basePos - Vector3.up * depth; }
        void Update()
        {
            time += Time.deltaTime;
            float t = time - delay;
            float y;
            if (t < 0f) y = -depth;
            else if (t < 0.12f) y = -depth * (1f - Mathf.Sin(t / 0.12f * Mathf.PI * 0.5f));
            else if (t < 0.12f + hold) y = 0f;
            else y = -depth * Mathf.Clamp01((t - 0.12f - hold) / 0.4f);
            transform.position = basePos + Vector3.up * y;
            if (t > 0.12f + hold + 0.4f) Destroy(gameObject);
        }
    }

    // a flaming rock falling out of the sky onto the marker
    class MeteorFall : MonoBehaviour
    {
        float time, delay; Vector3 from, to; Transform rock; bool thunder; float nextArc;

        public static void Spawn(Vector3 target, float radius, float delay, Color c, bool thunder)
        {
            var go = SceneGroups.Put(new GameObject("FXMeteor"), "VFX/Ground");
            var m = go.AddComponent<MeteorFall>();
            m.delay = Mathf.Max(delay, 0.2f); m.to = target; m.thunder = thunder;
            m.from = target + new Vector3(-12f, 55f, -9f);
            float size = Mathf.Clamp(radius * 0.32f, 0.9f, 3.2f);
            go.transform.position = m.from;
            go.transform.localScale = Vector3.one * size;

            // a lumpy dark rock with a hot glow around it
            var rockMat = LitMaterial(new Color(0.22f, 0.15f, 0.11f), 0.1f, new Color(0.5f, 0.15f, 0.03f));
            for (int i = 0; i < 4; i++)
            {
                var part = new GameObject("Rock").transform;
                part.SetParent(go.transform, false);
                part.localPosition = Random.insideUnitSphere * 0.25f;
                part.localScale = new Vector3(Random.Range(0.7f, 1f), Random.Range(0.6f, 0.9f), Random.Range(0.7f, 1f)) * (i == 0 ? 1.1f : 0.7f);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = Orb();
                var r = part.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = rockMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i == 0) m.rock = part;
            }
            var glow = new GameObject("Glow").transform; glow.SetParent(go.transform, false); glow.localScale = Vector3.one * 2.6f;
            glow.gameObject.AddComponent<MeshFilter>().sharedMesh = Orb();
            var gr = glow.gameObject.AddComponent<MeshRenderer>();
            var gm = new Material(MagicFX.CoreMat); if (gm.HasProperty("_BaseColor")) gm.SetColor("_BaseColor", thunder ? new Color(1f, 0.95f, 0.6f, 0.8f) : new Color(1f, 0.5f, 0.12f, 0.8f));
            gr.sharedMaterial = gm; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // the fire trail behind it (world space, so it stays in the sky as the rock drops)
            var trail = new GameObject("Trail");
            trail.transform.SetParent(go.transform, false);
            var ps = trail.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 1.1f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(size * 1.6f, size * 2.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.85f, 0.4f)); main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.1f;
            var em = ps.emission; em.rateOverTime = 70f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = size * 0.4f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.25f, 0.2f, 0.2f), 0.7f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = g;
            var rend = trail.GetComponent<ParticleSystemRenderer>(); rend.sharedMaterial = MagicFX.SoftMat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        void Update()
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / delay);
            transform.position = Vector3.Lerp(from, to + Vector3.up * 0.4f, k * k);
            transform.Rotate(new Vector3(120f, 80f, 40f) * Time.deltaTime, Space.World);
            if (thunder && Time.time >= nextArc && k < 0.97f) { nextArc = Time.time + 0.12f; Arcs.Spawn(transform.position, Vector3.up, transform.localScale.x * 2.2f, new Color(1f, 0.95f, 0.55f), 3); }
            if (k >= 1f) { var tr = GetComponentInChildren<ParticleSystem>(); if (tr != null) { tr.transform.SetParent(null); tr.Stop(); Destroy(tr.gameObject, 1.5f); } Destroy(gameObject); }
        }
    }

    // a screen-wide dim or white flash drawn under the HUD
    class ScreenFX : MonoBehaviour
    {
        static ScreenFX instance;
        UnityEngine.UI.Image image;
        Color color; float peak, tin, thold, tout, time; bool active;

        static ScreenFX Get()
        {
            if (instance != null) return instance;
            var go = SceneGroups.Put(new GameObject("ScreenFX"), "Systems");
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -50;
            var imgGo = new GameObject("Image"); imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<UnityEngine.UI.Image>(); img.raycastTarget = false; img.color = new Color(0, 0, 0, 0);
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            instance = go.AddComponent<ScreenFX>(); instance.image = img;
            return instance;
        }

        public static void Fade(Color c, float peakAlpha, float seconds, float hold, float outSeconds)
        {
            var s = Get(); s.color = c; s.peak = peakAlpha; s.tin = Mathf.Max(seconds, 0.05f); s.thold = hold; s.tout = Mathf.Max(outSeconds, 0.05f); s.time = 0f; s.active = true;
        }

        public static void Flash(Color c, float alpha, float seconds) => Fade(c, alpha, 0.04f, 0.02f, seconds);

        void Update()
        {
            if (!active) return;
            time += Time.deltaTime;
            float a;
            if (time < tin) a = Mathf.SmoothStep(0f, 1f, time / tin);
            else if (time < tin + thold) a = 1f;
            else a = 1f - Mathf.Clamp01((time - tin - thold) / tout);
            var c = color; c.a = a * peak; image.color = c;
            if (time >= tin + thold + tout) { active = false; image.color = new Color(0, 0, 0, 0); }
        }
    }

    // keeps the camera shaking in pulses for a while (Tremor Storm)
    class TremorShake : MonoBehaviour
    {
        float left, next; Vector3 point;
        public static void Spawn(Vector3 p, float seconds) { var t = SceneGroups.Put(new GameObject("FXTremor"), "VFX/Ground").AddComponent<TremorShake>(); t.point = p; t.left = seconds; }
        void Update() { left -= Time.deltaTime; if (Time.time >= next) { next = Time.time + 0.12f; CameraShake.AddAt(point, 0.22f); } if (left <= 0f) Destroy(gameObject); }
    }

    static Texture2D crackTex, scorchTex, frostTex;

    static Texture2D CrackTexture()
    {
        if (crackTex != null) return crackTex;
        const int n = 128;
        var px = new Color[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
        var rng = new System.Random(5);
        for (int branch = 0; branch < 12; branch++)
        {
            float a = (float)(branch / 12.0 * Mathf.PI * 2f + rng.NextDouble() * 0.4f);
            float x = n * 0.5f, y = n * 0.5f; float len = n * (0.28f + (float)rng.NextDouble() * 0.2f);
            for (float s = 0; s < len; s += 1f)
            {
                a += ((float)rng.NextDouble() - 0.5f) * 0.45f;
                x += Mathf.Cos(a); y += Mathf.Sin(a);
                float w = Mathf.Lerp(2.2f, 0.7f, s / len);
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                {
                    int px2 = Mathf.RoundToInt(x) + dx, py = Mathf.RoundToInt(y) + dy;
                    if (px2 < 0 || py < 0 || px2 >= n || py >= n) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= w) px[py * n + px2].a = Mathf.Max(px[py * n + px2].a, Mathf.Clamp01(1.2f - d / w * 0.6f));
                }
                if (rng.NextDouble() < 0.04 && s > len * 0.3f) { /* a side branch grows as its own short walk */ float a2 = a + ((float)rng.NextDouble() - 0.5f) * 2f; float x2 = x, y2 = y; for (int q = 0; q < len * 0.35f; q++) { a2 += ((float)rng.NextDouble() - 0.5f) * 0.5f; x2 += Mathf.Cos(a2); y2 += Mathf.Sin(a2); int ix = Mathf.RoundToInt(x2), iy = Mathf.RoundToInt(y2); if (ix >= 0 && iy >= 0 && ix < n && iy < n) px[iy * n + ix].a = Mathf.Max(px[iy * n + ix].a, 0.8f); } }
            }
        }
        crackTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        crackTex.SetPixels(px); crackTex.Apply();
        return crackTex;
    }

    static Texture2D ScorchTexture()
    {
        if (scorchTex != null) return scorchTex;
        const int n = 64; var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float noise = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 1.6f) * (0.65f + 0.35f * noise));
        }
        scorchTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; scorchTex.SetPixels(px); scorchTex.Apply();
        return scorchTex;
    }

    static Texture2D FrostTexture()
    {
        if (frostTex != null) return frostTex;
        const int n = 128; var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float noise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.6f + Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.4f;
            float a = Mathf.Clamp01((1f - d) * 1.4f) * Mathf.Lerp(0.45f, 1f, noise);
            px[y * n + x] = new Color(1f, 1f, 1f, a);
        }
        frostTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; frostTex.SetPixels(px); frostTex.Apply();
        return frostTex;
    }

    // a flat textured quad lying on the ground that grows, stays, then fades
    abstract class Decal : MonoBehaviour
    {
        protected float time, life, grow, size; protected Material mat; protected Color color;

        protected static T Make<T>(string name, Vector3 p, float size, float life, float grow, Color color, Texture tex, float yaw) where T : Decal
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            SceneGroups.Put(go, "VFX/Ground");
            Destroy(go.GetComponent<Collider>());
            var d = go.AddComponent<T>();
            d.size = size; d.life = life; d.grow = Mathf.Max(grow, 0.05f); d.color = color;
            Vector3 pos = GroundAt(p);
            go.transform.position = pos + Vector3.up * 0.04f;
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            d.mat = MagicFX.SoftMaterial(tex);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = d.mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            d.Apply(0f);
            return d;
        }

        protected void Apply(float t)
        {
            float g = Mathf.Clamp01(t / grow);
            transform.localScale = Vector3.one * size * (0.25f + 0.75f * (1f - (1f - g) * (1f - g)));
            float fade = t < life - 1.5f ? 1f : Mathf.Clamp01((life - t) / 1.5f);
            var c = color; c.a *= fade;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        }

        void Update()
        {
            time += Time.deltaTime;
            Apply(time);
            if (time >= life) { Destroy(mat); Destroy(gameObject); }
        }
    }

    class Cracks : Decal
    {
        public static void Spawn(Vector3 p, float radius, float life, float grow, Color c) => Make<Cracks>("FXCracks", p, radius * 2f, life, grow, new Color(c.r, c.g, c.b, 0.9f), CrackTexture(), Random.value * 360f);
    }

    class Scorch : Decal
    {
        public static void Spawn(Vector3 p, float radius, float life, Color c) => Make<Scorch>("FXScorch", p, radius * 2f, life, 0.2f, new Color(c.r, c.g, c.b, 0.85f), ScorchTexture(), Random.value * 360f);
    }

    class FrostGround : Decal
    {
        public static void Spawn(Vector3 p, float radius, float life) => Make<FrostGround>("FXFrost", p, radius * 2f, life, 0.8f, new Color(0.75f, 0.93f, 1f, 0.8f), FrostTexture(), Random.value * 360f);
    }

    // Cyclone: drags every enemy in range toward the centre (flying ones are pulled down too) and hurts them while the vortex spins
    class CycloneField : MonoBehaviour
    {
        float time, life, radius, dps, nextTick; Vector3 center; string damageTag; ParticleSystem funnel;
        static readonly Collider[] buffer = new Collider[48];
        readonly HashSet<Transform> pulled = new HashSet<Transform>();

        public void Setup(Vector3 p, float r, float seconds, float damagePerSecond, string damageTag)
        {
            center = GroundAt(p); radius = r; life = seconds; dps = damagePerSecond; this.damageTag = damageTag;
            transform.position = center;
            Color wind = new Color(0.85f, 1f, 0.9f);
            // the funnel: streaks spiral up around the axis, wider at the top
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.duration = seconds; main.startLifetime = 1.1f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(wind, Color.white); main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 90f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 0.9f; sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(radius * 1.2f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(9f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            vel.radial = new ParticleSystem.MinMaxCurve(-radius * 0.5f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) }); col.color = g;
            var rend = gameObject.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch; rend.lengthScale = 3f; rend.velocityScale = 0.1f; rend.sharedMaterial = MagicFX.GlowMat;
            ps.Play(); funnel = ps;
            Ring(center, Vector3.up, radius * 1.2f, wind, 0.6f, 1f);
        }

        void Update()
        {
            time += Time.deltaTime;
            if (time >= life) { Destroy(gameObject, 1.2f); if (funnel != null) funnel.Stop(); enabled = false; return; }

            int n = Physics.OverlapSphereNonAlloc(center + Vector3.up * radius, radius * 1.6f, buffer, ~0, QueryTriggerInteraction.Ignore);
            pulled.Clear();
            bool tick = Time.time >= nextTick; if (tick) nextTick = Time.time + 0.5f;
            for (int i = 0; i < n; i++)
            {
                var col = buffer[i];
                if (!col.CompareTag(damageTag)) continue;
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead || !pulled.Add(h.transform)) continue;

                Vector3 to = center - h.transform.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                float dist = flat.magnitude;
                if (dist > radius * 1.5f) continue;
                var agent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    if (dist > 0.8f) agent.Move(flat.normalized * Mathf.Min(7f * Time.deltaTime, dist));
                }
                else
                {
                    // a flier: toward the axis and down to the ground
                    Vector3 step = Vector3.zero;
                    if (dist > 0.8f) step += flat.normalized * Mathf.Min(7f * Time.deltaTime, dist);
                    float dy = center.y + 1.2f - h.transform.position.y;
                    if (dy < 0f) step.y = Mathf.Max(-9f * Time.deltaTime, dy);
                    h.transform.position += step;
                }
                if (tick) { h.TakeDamage(dps * 0.5f, h.transform.position); }
            }
        }
    }
}
