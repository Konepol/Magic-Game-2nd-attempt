using System;
using System.Collections.Generic;
using UnityEngine;

// A cast sound and an impact sound for every element, synthesized like the other placeholder sounds: arcane chimes, fire whooshes and booms,
// frost glass, wind swishes, venom splats, lightning cracks, radiant shimmer, earth rumble, meteor thunder. A spell picks its element from its
// asset name (the same key that picks its impact effect); a key that is not known returns false so the caller keeps the generic sound.
public static class SpellSounds
{
    enum Element { Arcane, Fire, Frost, Wind, Venom, Lightning, Radiant, Earth, Meteor }

    const int SampleRate = 44100;
    const float Tau = Mathf.PI * 2f;

    static readonly Dictionary<string, Element> elements = new Dictionary<string, Element>
    {
        { "ArcaneBolt", Element.Arcane }, { "ScatterShot", Element.Arcane }, { "SparkStream", Element.Arcane },
        { "FireBurst", Element.Fire }, { "InfernalBolt", Element.Fire }, { "CinderStorm", Element.Fire }, { "FirestormLance", Element.Fire },
        { "FrostShard", Element.Frost }, { "GlacierBolt", Element.Frost }, { "BlizzardLance", Element.Frost }, { "AbsoluteZero", Element.Frost },
        { "WindBlade", Element.Wind }, { "Cyclone", Element.Wind },
        { "VenomDart", Element.Venom }, { "PlagueStorm", Element.Venom },
        { "LightningLance", Element.Lightning }, { "ThunderMeteor", Element.Lightning },
        { "RadiantBeam", Element.Radiant }, { "SunLance", Element.Radiant },
        { "EarthSpike", Element.Earth }, { "TremorStorm", Element.Earth },
        { "MeteorStrike", Element.Meteor },
    };

    // fusions sound lower and fuller than the single spells
    static readonly HashSet<string> fusions = new HashSet<string>
    {
        "InfernalBolt", "CinderStorm", "FirestormLance", "GlacierBolt", "BlizzardLance", "AbsoluteZero", "Cyclone", "PlagueStorm",
        "ThunderMeteor", "SunLance", "TremorStorm",
    };

    static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
    static readonly Dictionary<int, float> lastPlayed = new Dictionary<int, float>();

    public static bool Has(string key) => !string.IsNullOrEmpty(key) && elements.ContainsKey(key);

    public static bool Cast(string key, Vector3 pos, float volume = 0.8f) => Play(key, false, pos, volume, 0f);

    // radius: the size of the blast (0 for a small direct hit); bigger blasts are louder and lower
    public static bool Impact(string key, Vector3 pos, float radius = 0f, float volume = 0.9f) => Play(key, true, pos, volume, radius);

    static bool Play(string key, bool impact, Vector3 pos, float volume, float radius)
    {
        if (!Has(key)) return false;
        var element = elements[key];
        int id = (int)element * 2 + (impact ? 1 : 0);

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < 0.05f) return true;   // many at once must not stack into a roar
        lastPlayed[id] = now;

        if (!clips.TryGetValue(id, out var clip) || clip == null)
            clips[id] = clip = Generate(element, impact);

        bool big = fusions.Contains(key);
        float pitch = (big ? 0.85f : 1f) * (1f - Mathf.Clamp(radius, 0f, 8f) * 0.025f) * (1f + UnityEngine.Random.Range(-0.07f, 0.07f));
        float vol = volume * (big ? 1.15f : 1f) * (1f + Mathf.Clamp(radius, 0f, 8f) * 0.04f) * PlayerProfile.EffectVolume;
        SpellFX.PlayClip(clip, pos, vol, pitch);
        return true;
    }

    static AudioClip Make(string name, float seconds, Func<float, float, float> gen)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float fadeOut = Mathf.Clamp01((seconds - t) / 0.01f);   // no click at the end
            data[i] = gen(t, seconds) * fadeOut;
        }
        float peak = 0.001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] *= 0.6f / peak;   // every element is equally loud and nothing clips
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Generate(Element element, bool impact)
    {
        var rng = new System.Random((int)element * 31 + (impact ? 5 : 3));
        Func<float> noise = () => (float)(rng.NextDouble() * 2.0 - 1.0);
        float lp = 0f, lp2 = 0f, phase = 0f, phase2 = 0f;
        string name = element + (impact ? "Impact" : "Cast");

        switch (element)
        {
            case Element.Arcane:
                if (!impact)
                    return Make(name, 0.24f, (t, d) =>
                    {
                        phase += Tau * Mathf.Lerp(520f, 1300f, t / d) / SampleRate;
                        return (Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f)) * Mathf.Sin(Mathf.PI * t / d) * 0.35f;
                    });
                return Make(name, 0.3f, (t, d) =>
                    (Mathf.Sin(Tau * 1100f * t) + 0.5f * Mathf.Sin(Tau * 1650f * t)) * Mathf.Exp(-t * 16f) * 0.3f + noise() * Mathf.Exp(-t * 60f) * 0.2f);

            case Element.Fire:
                if (!impact)
                    return Make(name, 0.4f, (t, d) =>
                    {
                        lp += (noise() - lp) * Mathf.Lerp(0.04f, 0.3f, t / d);   // the whoosh brightens as it leaves
                        return lp * 3f * Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.7f)) * 0.55f;
                    });
                return Make(name, 0.9f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.07f;
                    float crackle = rng.NextDouble() < 0.0025 ? noise() * 0.9f : 0f;
                    return (lp * 3f * Mathf.Exp(-t * 4f) + Mathf.Sin(Tau * 70f * t) * Mathf.Exp(-t * 7f) + crackle * Mathf.Exp(-t * 3f)) * 0.7f;
                });

            case Element.Frost:
                if (!impact)
                    return Make(name, 0.35f, (t, d) =>
                    {
                        float v = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            float start = k * 0.04f;
                            if (t > start) v += Mathf.Sin(Tau * (2200f + k * 650f) * t) * Mathf.Exp(-(t - start) * 14f);
                        }
                        return v * 0.16f;
                    });
                return Make(name, 0.6f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.5f;
                    float hiss = (noise() - lp) * Mathf.Exp(-t * 30f);   // high-passed burst: the glass cracking
                    float v = 0f;
                    for (int k = 0; k < 5; k++)
                    {
                        float start = 0.02f + k * 0.045f;
                        if (t > start) v += Mathf.Sin(Tau * (1800f + k * 770f) * t) * Mathf.Exp(-(t - start) * 10f);
                    }
                    return hiss * 0.5f + v * 0.12f;
                });

            case Element.Wind:
                if (!impact)
                    return Make(name, 0.35f, (t, d) =>
                    {
                        float n = noise();
                        lp += (n - lp) * 0.12f;
                        lp2 += (n - lp2) * 0.02f;
                        float band = lp - lp2;                                   // band-pass: a thin swish, not a rumble
                        return band * 5f * Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.6f)) * 0.5f;
                    });
                return Make(name, 0.7f, (t, d) =>
                {
                    float n = noise();
                    lp += (n - lp) * Mathf.Lerp(0.2f, 0.05f, t / d);
                    lp2 += (n - lp2) * 0.015f;
                    float swirl = 0.6f + 0.4f * Mathf.Sin(Tau * 7f * t);
                    return (lp - lp2) * 5f * swirl * Mathf.Exp(-t * 4f) * 0.5f;
                });

            case Element.Venom:
                if (!impact)
                    return Make(name, 0.3f, (t, d) =>
                    {
                        phase += Tau * (380f + 260f * Mathf.Sin(Tau * 22f * t)) / SampleRate;   // wobbling pitch: wet and bubbly
                        return Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t / d) * 0.4f;
                    });
                return Make(name, 0.4f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.18f;
                    phase += Tau * Mathf.Lerp(420f, 120f, t / d) / SampleRate;
                    return (lp * 2f * Mathf.Exp(-t * 18f) + Mathf.Sin(phase) * Mathf.Exp(-t * 10f)) * 0.5f;
                });

            case Element.Lightning:
                if (!impact)
                    return Make(name, 0.25f, (t, d) =>
                    {
                        phase += Tau * Mathf.Lerp(3200f, 220f, Mathf.Pow(t / d, 0.5f)) / SampleRate;
                        float saw = (phase / Tau) % 1f * 2f - 1f;
                        return (saw * 0.5f + noise() * 0.25f) * Mathf.Exp(-t * 11f) * 0.7f;
                    });
                return Make(name, 0.7f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.1f;
                    float crack = noise() * Mathf.Exp(-t * 45f);                    // the sharp crack first
                    float rumble = lp * 3f * Mathf.Exp(-t * 6f);                    // then the rolling thunder
                    return (crack * 0.9f + rumble + Mathf.Sin(Tau * 55f * t) * Mathf.Exp(-t * 8f) * 0.6f) * 0.6f;
                });

            case Element.Radiant:
                if (!impact)
                    return Make(name, 0.5f, (t, d) =>
                    {
                        float swell = Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.8f));
                        float v = Mathf.Sin(Tau * 660f * t) + 0.7f * Mathf.Sin(Tau * 990f * t) + 0.5f * Mathf.Sin(Tau * 1320f * t * (1f + 0.004f * Mathf.Sin(Tau * 6f * t)));
                        return v * swell * 0.18f;
                    });
                return Make(name, 0.8f, (t, d) =>
                {
                    float v = Mathf.Sin(Tau * 880f * t) + 0.7f * Mathf.Sin(Tau * 1320f * t) + 0.5f * Mathf.Sin(Tau * 1760f * t) + 0.3f * Mathf.Sin(Tau * 2640f * t);
                    return v * Mathf.Exp(-t * 5f) * 0.2f;
                });

            case Element.Earth:
                if (!impact)
                    return Make(name, 0.35f, (t, d) =>
                    {
                        lp += (noise() - lp) * 0.05f;
                        return (Mathf.Sin(Tau * 75f * t) * 0.9f + lp * 3f) * Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.5f)) * 0.55f;
                    });
                return Make(name, 0.8f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.08f;
                    float debris = rng.NextDouble() < 0.0012 ? noise() : 0f;      // pebbles landing
                    lp2 += (debris - lp2) * 0.3f;
                    return (Mathf.Sin(Tau * 62f * t) * Mathf.Exp(-t * 7f) + lp * 2.5f * Mathf.Exp(-t * 5f) + lp2 * 1.5f * Mathf.Exp(-t * 2.5f)) * 0.65f;
                });

            default:    // Meteor
                if (!impact)
                    return Make(name, 0.6f, (t, d) =>
                    {
                        lp += (noise() - lp) * 0.04f;
                        phase += Tau * Mathf.Lerp(50f, 130f, t / d) / SampleRate;
                        return (Mathf.Sin(phase) + lp * 3f) * Mathf.Sin(Mathf.PI * Mathf.Pow(t / d, 0.7f)) * 0.5f;
                    });
                return Make(name, 1.5f, (t, d) =>
                {
                    lp += (noise() - lp) * 0.05f;
                    phase += Tau * Mathf.Lerp(90f, 32f, Mathf.Clamp01(t / 0.6f)) / SampleRate;
                    return (Mathf.Sin(phase) * Mathf.Exp(-t * 2.5f) + lp * 4f * Mathf.Exp(-t * 3f)) * 0.8f;
                });
        }
    }
}
