using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Magic > Build FX Assets
// Writes real sound files (.wav) and particle effect prefabs into the project and links them in the FX library,
// so they can be opened, edited or replaced with your own. Running it again overwrites the generated files.
public static class FxAssetBuilder
{
    const string AudioDir = "Assets/_Assets/Audio";
    const string FxDir = "Assets/_Assets/Prefabs/FX";
    const string ImageDir = "Assets/_Assets/Images/FX";
    const string MaterialDir = "Assets/_Assets/Materials";
    const string ResourcesDir = "Assets/_Assets/Resources";
    const int Rate = 44100;

    [MenuItem("Tools/Magic/Build FX Assets")]
    public static void Build()
    {
        foreach (var d in new[] { AudioDir, FxDir, ImageDir, MaterialDir, ResourcesDir })
            Directory.CreateDirectory(d);

        // ---------- sounds ----------
        var clips = new[]
        {
            Wav("cast", Cast()), Wav("laser", Laser()), Wav("explosion", Explosion()), Wav("hit", Hit()),
            Wav("barrier_hit", BarrierHit()), Wav("barrier_place", BarrierPlace()), Wav("player_hurt", PlayerHurt()),
            Wav("death", Death()), Wav("wave", Wave()), Wav("charge", Charge()), Wav("fizzle", Fizzle()), Wav("ready", Ready()),
        };
        AssetDatabase.Refresh();
        foreach (var path in clips) ConfigureClip(path);

        // ---------- particles ----------
        var soft = SoftTexture();
        var additive = ParticleMaterial("FX_Additive", soft, true);
        var alpha = ParticleMaterial("FX_Smoke", soft, false);

        var explosion = SaveFx("FX_Explosion", MakeExplosion(additive, alpha), 3.5f);
        var spark = SaveFx("FX_HitSpark", MakeHitSpark(additive), 1.5f);
        var shatter = SaveFx("FX_BarrierBreak", MakeBarrierBreak(additive), 2.5f);
        var flash = SaveFx("FX_CastFlash", MakeCastFlash(additive), 1.5f);

        // ---------- library ----------
        string libPath = ResourcesDir + "/SpellFXLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<SpellFXLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SpellFXLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }
        lib.cast = Clip("cast"); lib.laser = Clip("laser"); lib.explosion = Clip("explosion"); lib.hit = Clip("hit");
        lib.barrierHit = Clip("barrier_hit"); lib.barrierPlace = Clip("barrier_place"); lib.playerHurt = Clip("player_hurt");
        lib.death = Clip("death"); lib.wave = Clip("wave"); lib.charge = Clip("charge"); lib.fizzle = Clip("fizzle"); lib.ready = Clip("ready");
        lib.explosionFx = explosion; lib.hitSparkFx = spark; lib.barrierBreakFx = shatter; lib.castFlashFx = flash;
        EditorUtility.SetDirty(lib);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("FX assets built: sounds in " + AudioDir + ", effects in " + FxDir + ", linked in " + libPath);
    }

    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + name + ".wav");

    // =====================================================================
    // sound synthesis (mono, written to real .wav files)
    // =====================================================================

    delegate float Gen(float t, float dur);

    static float[] Render(float seconds, Gen gen, float echoDelay = 0f, float echoGain = 0f)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = gen(i / (float)Rate, seconds);

        if (echoDelay > 0f)   // a simple echo gives the bigger sounds some space
        {
            int off = Mathf.RoundToInt(echoDelay * Rate);
            for (int i = off; i < n; i++) d[i] += d[i - off] * echoGain;
        }

        float peak = 0.0001f;
        foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        float norm = 0.9f / peak;
        for (int i = 0; i < n; i++) d[i] *= norm;
        return d;
    }

    static string Wav(string name, float[] samples)
    {
        string path = AudioDir + "/" + name + ".wav";
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataLen = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
        return path;
    }

    static void ConfigureClip(string path)
    {
        var imp = (AudioImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        var settings = imp.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        imp.defaultSampleSettings = settings;
        imp.forceToMono = true;
        imp.SaveAndReimport();
    }

    static System.Random rng = new System.Random(1234);
    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    const float Tau = Mathf.PI * 2f;

    static float[] Cast()
    {
        float lp = 0f, ph = 0f;
        return Render(0.35f, (t, d) =>
        {
            float k = t / d;
            lp += (Noise() - lp) * Mathf.Lerp(0.04f, 0.3f, k);
            ph += Tau * Mathf.Lerp(380f, 760f, k) / Rate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
            return (lp * 1.8f + 0.35f * Mathf.Sin(ph) + 0.12f * Mathf.Sin(ph * 2f)) * env;
        });
    }

    static float[] Laser()
    {
        float ph = 0f, lp = 0f;
        return Render(0.4f, (t, d) =>
        {
            float k = t / d;
            ph += Tau * Mathf.Lerp(3200f, 180f, Mathf.Pow(k, 0.5f)) / Rate;
            lp += (Noise() - lp) * 0.5f;
            float saw = (ph / Tau % 1f) * 2f - 1f;
            return (Mathf.Sin(ph) * 0.7f + saw * 0.25f) * Mathf.Exp(-k * 5f) + lp * 0.25f * Mathf.Exp(-k * 14f);
        }, 0.07f, 0.25f);
    }

    static float[] Explosion()
    {
        float lp = 0f, lp2 = 0f, ph = 0f;
        return Render(1.4f, (t, d) =>
        {
            lp += (Noise() - lp) * 0.07f;
            lp2 += (Noise() - lp2) * 0.5f;
            ph += Tau * Mathf.Lerp(90f, 38f, Mathf.Min(1f, t * 2.5f)) / Rate;
            float boom = Mathf.Sin(ph) * Mathf.Exp(-t * 3.5f) * 1.1f;
            float rumble = lp * 3f * Mathf.Exp(-t * 2.6f);
            float crackle = lp2 * Mathf.Exp(-t * 18f) * 1.2f;
            return boom + rumble + crackle;
        }, 0.11f, 0.4f);
    }

    static float[] Hit()
    {
        float ph = 0f;
        return Render(0.18f, (t, d) =>
        {
            ph += Tau * Mathf.Lerp(220f, 90f, t / d) / Rate;
            return Noise() * Mathf.Exp(-t * 60f) * 0.8f + Mathf.Sin(ph) * Mathf.Exp(-t * 28f);
        });
    }

    static float[] BarrierHit()
    {
        return Render(0.7f, (t, d) =>
            (Mathf.Sin(Tau * 740f * t) + 0.7f * Mathf.Sin(Tau * 1310f * t) + 0.5f * Mathf.Sin(Tau * 1980f * t) + 0.3f * Mathf.Sin(Tau * 2730f * t))
            * Mathf.Exp(-t * 8f) + Noise() * Mathf.Exp(-t * 80f) * 0.5f, 0.05f, 0.3f);
    }

    static float[] BarrierPlace()
    {
        float lp = 0f;
        return Render(0.7f, (t, d) =>
        {
            float k = t / d;
            lp += (Noise() - lp) * 0.08f;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * (1f - k * 0.3f);
            float chord = Mathf.Sin(Tau * Mathf.Lerp(220f, 330f, k) * t) + 0.6f * Mathf.Sin(Tau * Mathf.Lerp(330f, 495f, k) * t) + 0.4f * Mathf.Sin(Tau * Mathf.Lerp(440f, 660f, k) * t);
            return (chord * (0.7f + 0.3f * Mathf.Sin(Tau * 14f * t)) * 0.5f + lp * 1.2f) * env;
        }, 0.09f, 0.3f);
    }

    static float[] PlayerHurt()
    {
        float lp = 0f, ph = 0f;
        return Render(0.4f, (t, d) =>
        {
            lp += (Noise() - lp) * 0.18f;
            ph += Tau * Mathf.Lerp(140f, 70f, t / d) / Rate;
            return (lp * 1.4f + Mathf.Sin(ph) * 1.0f) * Mathf.Exp(-t * 9f);
        }, 0.06f, 0.25f);
    }

    static float[] Death()
    {
        float ph = 0f, lp = 0f;
        return Render(1.8f, (t, d) =>
        {
            float k = t / d;
            ph += Tau * Mathf.Lerp(260f, 36f, Mathf.Pow(k, 0.6f)) / Rate;
            lp += (Noise() - lp) * 0.05f;
            return (Mathf.Sin(ph) * 0.8f + Mathf.Sin(ph * 1.5f) * 0.25f + lp * 1.2f) * Mathf.Exp(-k * 2.2f);
        }, 0.15f, 0.35f);
    }

    static float[] Wave()
    {
        return Render(1.0f, (t, d) =>
        {
            float f = t < 0.32f ? 392f : 523f;
            float local = t < 0.32f ? t : t - 0.32f;
            float horn = Mathf.Sin(Tau * f * t) + 0.5f * Mathf.Sin(Tau * f * 2f * t) + 0.25f * Mathf.Sin(Tau * f * 3f * t);
            return horn * Mathf.Min(1f, local * 40f) * Mathf.Exp(-local * 4.5f);
        }, 0.1f, 0.3f);
    }

    // a spell that fails: a quick falling blip with a crackle
    static float[] Fizzle()
    {
        float ph = 0f, lp = 0f;
        return Render(0.45f, (t, d) =>
        {
            float k = t / d;
            ph += Tau * Mathf.Lerp(700f, 70f, Mathf.Pow(k, 0.5f)) / Rate;
            lp += (Noise() - lp) * 0.45f;
            float stutter = Mathf.Sin(Tau * 38f * t) > -0.2f ? 1f : 0.3f;
            return (Mathf.Sin(ph) * 0.55f + lp * 0.6f * Mathf.Exp(-k * 6f)) * Mathf.Exp(-k * 5f) * stutter;
        }, 0.05f, 0.2f);
    }

    // a cast is ready: a short rising two-note chime
    static float[] Ready()
    {
        return Render(0.55f, (t, d) =>
        {
            float f = t < 0.12f ? 880f : 1320f;
            float local = t < 0.12f ? t : t - 0.12f;
            float tone = Mathf.Sin(Tau * f * t) + 0.4f * Mathf.Sin(Tau * f * 2f * t) + 0.15f * Mathf.Sin(Tau * f * 3f * t);
            return tone * Mathf.Min(1f, local * 80f) * Mathf.Exp(-local * 8f);
        }, 0.08f, 0.3f);
    }

    static float[] Charge()
    {
        float ph = 0f;
        return Render(0.8f, (t, d) =>
        {
            float k = t / d;
            ph += Tau * Mathf.Lerp(180f, 540f, k * k) / Rate;
            return (Mathf.Sin(ph) + 0.4f * Mathf.Sin(ph * 2f)) * (0.65f + 0.35f * Mathf.Sin(Tau * 16f * t)) * Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.8f));
        });
    }

    // =====================================================================
    // particle assets
    // =====================================================================

    static Texture2D SoftTexture()
    {
        string path = ImageDir + "/Soft.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ParticleMaterial(string name, Texture2D tex, bool additive)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }

        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
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
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject SaveFx(string name, GameObject root, float lifetime)
    {
        root.name = name;
        var auto = root.AddComponent<FxAutoDestroy>();
        new SerializedObject(auto).FindProperty("lifetime").floatValue = lifetime;
        var so = new SerializedObject(auto);
        so.FindProperty("lifetime").floatValue = lifetime;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = FxDir + "/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // one particle system as a child of the effect, set up as a single burst
    static ParticleSystem Layer(Transform parent, string name, Material mat, float duration, int count, Color color,
        float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity = 0f, float shapeRadius = 0.2f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = shapeRadius;

        // fade out over the particle's life (colour comes from the start colour so it can be tinted)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static GameObject MakeExplosion(Material additive, Material smokeMat)
    {
        var root = new GameObject("FX_Explosion");

        // bright flash, glowing fireball, flying embers, then smoke (designed for a blast radius of about 3)
        var flash = Layer(root.transform, "Flash", additive, 0.3f, 1, new Color(1f, 0.85f, 0.5f, 1f), 0.15f, 0.2f, 0f, 0f, 5f, 5.5f, 0f, 0.01f);
        var fire = Layer(root.transform, "Fire", additive, 0.5f, 28, new Color(1f, 0.55f, 0.15f, 0.9f), 0.4f, 0.8f, 2f, 7f, 0.8f, 1.8f, -0.15f, 0.4f);
        var embers = Layer(root.transform, "Embers", additive, 0.5f, 26, new Color(1f, 0.7f, 0.25f, 1f), 0.6f, 1.2f, 4f, 11f, 0.12f, 0.25f, 1.4f, 0.3f);
        var smoke = Layer(root.transform, "Smoke", smokeMat, 0.6f, 10, new Color(0.18f, 0.18f, 0.2f, 0.4f), 1.0f, 1.8f, 0.8f, 2.8f, 1.6f, 2.8f, -0.05f, 0.5f);

        // smoke swells instead of shrinking
        var ss = smoke.sizeOverLifetime;
        ss.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.2f));
        return root;
    }

    static GameObject MakeHitSpark(Material additive)
    {
        var root = new GameObject("FX_HitSpark");
        var sparks = Layer(root.transform, "Sparks", additive, 0.3f, 12, new Color(1f, 0.85f, 0.45f, 1f), 0.15f, 0.32f, 3f, 8f, 0.06f, 0.14f, 0.6f, 0.05f);
        var r = sparks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 2.2f;
        Layer(root.transform, "Pop", additive, 0.2f, 1, new Color(1f, 0.95f, 0.8f, 1f), 0.08f, 0.12f, 0f, 0f, 0.5f, 0.7f, 0f, 0.01f);
        return root;
    }

    static GameObject MakeBarrierBreak(Material additive)
    {
        var root = new GameObject("FX_BarrierBreak");
        Layer(root.transform, "Glow", additive, 0.3f, 1, new Color(0.4f, 0.85f, 1f, 0.8f), 0.25f, 0.35f, 0f, 0f, 3.5f, 4f, 0f, 0.01f);
        var shards = Layer(root.transform, "Shards", additive, 0.5f, 38, new Color(0.55f, 0.9f, 1f, 1f), 0.5f, 1.0f, 3f, 9f, 0.1f, 0.26f, 1.1f, 0.6f);
        var r = shards.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.8f;
        return root;
    }

    static GameObject MakeCastFlash(Material additive)
    {
        var root = new GameObject("FX_CastFlash");
        Layer(root.transform, "Flash", additive, 0.2f, 1, new Color(1f, 1f, 1f, 0.9f), 0.1f, 0.15f, 0f, 0f, 0.7f, 0.9f, 0f, 0.01f);
        Layer(root.transform, "Motes", additive, 0.3f, 12, new Color(1f, 1f, 1f, 0.9f), 0.2f, 0.4f, 1f, 3.5f, 0.08f, 0.18f, 0f, 0.15f);
        return root;
    }
}
