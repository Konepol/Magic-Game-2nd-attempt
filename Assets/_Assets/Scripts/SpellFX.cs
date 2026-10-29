using System;
using UnityEngine;

public enum Sfx { Cast, Laser, Explosion, Hit, BarrierHit, BarrierPlace, PlayerHurt, Death, Wave, Charge, Fizzle, Ready }

// Placeholder particles and synthesized sounds, so spells have feedback before real assets exist.
public static class SpellFX
{
    static Material particleMat;
    static SpellFXLibrary library;
    static bool libraryLoaded;

    // your own sounds and effects, if the library asset exists; anything missing falls back to the generated placeholders
    static SpellFXLibrary Library
    {
        get
        {
            if (!libraryLoaded)
            {
                library = Resources.Load<SpellFXLibrary>("SpellFXLibrary");
                libraryLoaded = true;
            }
            return library;
        }
    }
    static readonly AudioClip[] clips = new AudioClip[16];
    static readonly float[] lastPlayed = new float[16];
    const int SampleRate = 44100;

    // ---------- particles ----------

    public static void Burst(Vector3 pos, float radius, Color color, int count = 40)
    {
        var go = new GameObject("FXBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // it auto-plays with defaults, so stop before configuring

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startColor = color;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.4f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetParticleMaterial();
        ps.Play();
    }

    // ---------- effect prefabs (fall back to the generated bursts when the library has none) ----------

    public static void Explosion(Vector3 pos, float radius, Color color)
    {
        var fx = Library != null ? Library.explosionFx : null;
        if (fx != null) SpawnFx(fx, pos, radius / 3f, color);   // the prefab is designed for a radius of about 3
        else Burst(pos, radius, color, 30 + Mathf.RoundToInt(radius * 8f));
    }

    public static void Sparks(Vector3 pos, Color color)
    {
        var fx = Library != null ? Library.hitSparkFx : null;
        if (fx != null) SpawnFx(fx, pos, 1f, color);
        else Burst(pos, 0.6f, color, 10);
    }

    public static void Shatter(Vector3 pos, Color color)
    {
        var fx = Library != null ? Library.barrierBreakFx : null;
        if (fx != null) SpawnFx(fx, pos, 1f, color);
        else Burst(pos, 2.5f, color, 45);
    }

    public static void CastFlash(Vector3 pos, Color color)
    {
        var fx = Library != null ? Library.castFlashFx : null;
        if (fx != null) SpawnFx(fx, pos, 1f, color);
    }

    static void SpawnFx(GameObject prefab, Vector3 pos, float scale, Color tint)
    {
        var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        go.transform.localScale = Vector3.one * Mathf.Max(scale, 0.2f);

        // tint everything except smoke, keeping the alpha each system was designed with
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.name.StartsWith("Smoke")) continue;
            var main = ps.main;
            Color c = tint;
            c.a = main.startColor.color.a;
            main.startColor = c;
        }
    }

    static Material GetParticleMaterial()
    {
        if (particleMat != null) return particleMat;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        particleMat = new Material(sh);

        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();

        if (particleMat.HasProperty("_BaseMap")) particleMat.SetTexture("_BaseMap", tex);
        particleMat.mainTexture = tex;

        if (particleMat.HasProperty("_Surface"))
        {
            particleMat.SetFloat("_Surface", 1f);
            particleMat.SetFloat("_Blend", 2f);       // additive
            particleMat.SetFloat("_SrcBlend", 5f);
            particleMat.SetFloat("_DstBlend", 1f);
            particleMat.SetFloat("_ZWrite", 0f);
            particleMat.SetOverrideTag("RenderType", "Transparent");
            particleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particleMat.renderQueue = 3000;
        }
        return particleMat;
    }

    // ---------- sound ----------

    public static void Play(Sfx sfx, Vector3 pos, float volume = 1f)
    {
        int i = (int)sfx;
        if (Time.unscaledTime - lastPlayed[i] < 0.04f) return;   // stops many hits at once from stacking up
        lastPlayed[i] = Time.unscaledTime;

        // your own clip if there is one, with a little random pitch so repeats don't sound identical
        var lib = Library;
        var custom = lib != null ? lib.Get(sfx) : null;
        if (custom != null)
        {
            PlayClip(custom, pos, volume * lib.volume * PlayerProfile.EffectVolume, 1f + UnityEngine.Random.Range(-lib.pitchVariation, lib.pitchVariation));
            return;
        }

        if (clips[i] == null) clips[i] = Generate(sfx);
        AudioSource.PlayClipAtPoint(clips[i], pos, volume * PlayerProfile.EffectVolume);
    }

    static void PlayClip(AudioClip clip, Vector3 pos, float volume, float pitch)
    {
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.pitch = pitch;
        src.spatialBlend = 1f;
        src.minDistance = 6f;
        src.maxDistance = 70f;
        src.Play();
        UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(pitch, 0.1f) + 0.1f);
    }

    static AudioClip Make(string name, float seconds, Func<float, float, float> gen)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(gen(i / (float)SampleRate, seconds), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Generate(Sfx sfx)
    {
        var rng = new System.Random(sfx.GetHashCode() + 7);
        Func<float> noise = () => (float)(rng.NextDouble() * 2.0 - 1.0);
        const float Tau = Mathf.PI * 2f;
        float lp = 0f;      // low-pass state
        float phase = 0f;   // sweep phase

        switch (sfx)
        {
            case Sfx.Cast:
                return Make("cast", 0.25f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.15f;
                    float env = Mathf.Sin(Mathf.PI * t / d);
                    return (lp * 1.6f + 0.25f * Mathf.Sin(Tau * (300f + 500f * t / d) * t)) * env * 0.5f;
                });
            case Sfx.Laser:
                return Make("laser", 0.25f, (t, d) =>
                {
                    phase += Tau * Mathf.Lerp(2400f, 300f, t / d) / SampleRate;
                    return Mathf.Sin(phase) * Mathf.Exp(-t * 8f) * 0.5f + noise() * 0.1f * Mathf.Exp(-t * 20f);
                });
            case Sfx.Explosion:
                return Make("explosion", 0.8f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.06f;
                    return (lp * 3f * Mathf.Exp(-t * 4.5f) + Mathf.Sin(Tau * 60f * t) * Mathf.Exp(-t * 6f)) * 0.8f;
                });
            case Sfx.Hit:
                return Make("hit", 0.12f, (t, d) =>
                    (noise() * Mathf.Exp(-t * 40f) + Mathf.Sin(Tau * 160f * t) * Mathf.Exp(-t * 30f)) * 0.5f);
            case Sfx.BarrierHit:
                return Make("barrierHit", 0.4f, (t, d) =>
                    (Mathf.Sin(Tau * 880f * t) + 0.6f * Mathf.Sin(Tau * 1320f * t)) * Mathf.Exp(-t * 9f) * 0.35f);
            case Sfx.BarrierPlace:
                return Make("barrierPlace", 0.5f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.1f;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / d));
                    return (Mathf.Sin(Tau * (180f + 260f * t / d) * t) * 0.5f + lp) * env * 0.5f;
                });
            case Sfx.PlayerHurt:
                return Make("playerHurt", 0.3f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.2f;
                    return (lp * 1.5f + Mathf.Sin(Tau * 100f * t)) * Mathf.Exp(-t * 10f) * 0.6f;
                });
            case Sfx.Death:
                return Make("death", 1.2f, (t, d) =>
                {
                    phase += Tau * Mathf.Lerp(220f, 40f, t / d) / SampleRate;
                    return Mathf.Sin(phase) * Mathf.Exp(-t * 2.5f) * 0.7f;
                });
            case Sfx.Wave:
                return Make("wave", 0.6f, (t, d) =>
                {
                    float f = t < 0.25f ? 440f : 660f;
                    float local = t < 0.25f ? t : t - 0.25f;
                    return Mathf.Sin(Tau * f * t) * Mathf.Exp(-local * 6f) * 0.4f;
                });
            case Sfx.Fizzle:
                return Make("fizzle", 0.3f, (t, d) =>
                {
                    phase += Tau * Mathf.Lerp(600f, 90f, t / d) / SampleRate;
                    lp += (noise() - lp) * 0.4f;
                    return (Mathf.Sin(phase) * 0.5f + lp * 0.6f) * Mathf.Exp(-t * 9f);
                });
            case Sfx.Ready:
                return Make("ready", 0.5f, (t, d) =>
                {
                    float f = t < 0.12f ? 880f : 1320f;
                    float local = t < 0.12f ? t : t - 0.12f;
                    return (Mathf.Sin(Tau * f * t) + 0.4f * Mathf.Sin(Tau * f * 2f * t)) * Mathf.Min(1f, local * 80f) * Mathf.Exp(-local * 9f) * 0.5f;
                });
            default: // Charge
                return Make("charge", 0.5f, (t, d) =>
                {
                    phase += Tau * Mathf.Lerp(200f, 520f, t / d) / SampleRate;
                    return Mathf.Sin(phase) * (0.6f + 0.4f * Mathf.Sin(Tau * 18f * t)) * Mathf.Sin(Mathf.PI * t / d) * 0.35f;
                });
        }
    }
}

// Global reactions to damage: hit sounds, sparks, and screen shake when the player is hurt.
public static class GameFeedback
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Health.AnyDamaged -= OnDamaged;   // safe if play-mode domain reload is off
        Health.AnyDamaged += OnDamaged;
    }

    static void OnDamaged(Health h, float amount, Vector3 point)
    {
        if (h == null) return;

        if (h.CompareTag("Player"))
        {
            SpellFX.Play(Sfx.PlayerHurt, point);
            CameraShake.Add(Mathf.Clamp01(0.2f + amount * 0.012f));
        }
        else if (h.GetComponent<Barrier>() != null)
        {
            SpellFX.Play(Sfx.BarrierHit, point);
            SpellFX.Sparks(point, new Color(0.4f, 0.8f, 1f));
        }
        else
        {
            SpellFX.Play(Sfx.Hit, point);
            SpellFX.Sparks(point, new Color(1f, 0.8f, 0.4f));
            CameraShake.AddAt(point, Mathf.Clamp01(amount * 0.004f), 30f);
        }
    }
}
