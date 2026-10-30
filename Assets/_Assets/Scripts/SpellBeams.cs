using System.Collections.Generic;
using UnityEngine;

// The look of the beam spells. Lightning Lance is a jagged, flickering, slightly forking bolt; Radiant Beam is a thick smooth beam of golden-white
// light with a starburst where it lands; Blizzard Lance is an icy beam with snow and ice crystals swirling around it. Radiant Beam and Blizzard Lance
// are fired again every tick while the button is held, so a beam that is refreshed in time stays as one steady beam instead of starting over.
public class BeamFX : MonoBehaviour
{
    static readonly Dictionary<string, BeamFX> live = new Dictionary<string, BeamFX>();
    static Texture2D softLine;

    string key; Vector3 a, b; float until, width, nextJitter, nextBurst, born; Color color;
    LineRenderer core, glow; readonly List<LineRenderer> forks = new List<LineRenderer>(); Material glowMat, coreMat; ParticleSystem snow; Transform snowT;

    public static bool Handles(string key) => key == "LightningLance" || key == "RadiantBeam" || key == "BlizzardLance" || key == "SunLance";

    public static void Spawn(Vector3 from, Vector3 to, float width, Color color, float duration, string key)
    {
        if (live.TryGetValue(key, out var existing) && existing != null && Time.time < existing.until + 0.04f && key != "LightningLance")
        {
            existing.Refresh(from, to, duration);
            return;
        }
        var go = SceneGroups.Put(new GameObject("BeamFX_" + key), "VFX/Beams");
        var beam = go.AddComponent<BeamFX>();
        beam.Build(key, width, color);
        beam.Refresh(from, to, duration);
        live[key] = beam;
    }

    static Texture2D SoftLine()
    {
        if (softLine != null) return softLine;
        const int h = 32;
        softLine = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs(y / (h - 1f) * 2f - 1f);
            float al = Mathf.Pow(Mathf.Clamp01(1f - v), 1.6f);
            for (int x = 0; x < 4; x++) softLine.SetPixel(x, y, new Color(1f, 1f, 1f, al));
        }
        softLine.Apply();
        return softLine;
    }

    LineRenderer MakeLine(string name, Material m, float w, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<LineRenderer>();
        l.useWorldSpace = true; l.sharedMaterial = m; l.numCapVertices = 6; l.numCornerVertices = 4;
        l.textureMode = LineTextureMode.Stretch; l.alignment = LineAlignment.View;
        l.startColor = l.endColor = c; l.startWidth = l.endWidth = w;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false;
        return l;
    }

    void Build(string beamKey, float w, Color c)
    {
        key = beamKey; width = Mathf.Max(w, 0.1f); color = c; born = Time.time;
        glowMat = new Material(Shader.Find("Magic/ParticleGlow")); glowMat.SetTexture("_BaseMap", SoftLine());
        coreMat = new Material(glowMat);

        Color coreColor = key == "LightningLance" ? new Color(1f, 1f, 0.8f) : Color.white;
        Color glowColor = key == "LightningLance" ? new Color(0.45f, 0.65f, 1f, 0.9f) : new Color(c.r, c.g, c.b, 0.8f);
        float glowMul = key == "LightningLance" ? 5f : 1.7f, coreMul = key == "LightningLance" ? 1.2f : 0.5f;
        glow = MakeLine("Glow", glowMat, width * glowMul, glowColor);
        core = MakeLine("Core", coreMat, width * coreMul, coreColor);

        if (key == "LightningLance")
            for (int i = 0; i < 3; i++) forks.Add(MakeLine("Fork", coreMat, width * 0.8f, new Color(0.8f, 0.9f, 1f, 0.9f)));

        if (key == "BlizzardLance") BuildSnow();
    }

    // snow and ice crystals drifting and swirling around the whole length of the beam
    void BuildSnow()
    {
        snowT = new GameObject("Snow").transform;
        snowT.SetParent(transform, false);
        snow = snowT.gameObject.AddComponent<ParticleSystem>();
        snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = snow.main; main.loop = true; main.playOnAwake = false; main.startLifetime = 0.6f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.92f, 1f), Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f); main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f); main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        var em = snow.emission; em.rateOverTime = 70f;
        var sh = snow.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width * 3f, width * 3f, 1f);
        var vel = snow.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); vel.z = new ParticleSystem.MinMaxCurve(1f, 3f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(5f);
        var rot = snow.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = new ParticleSystem.MinMaxCurve(-5f, 5f); rot.y = new ParticleSystem.MinMaxCurve(-5f, 5f); rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var size = snow.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));
        var r = snowT.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = SpellImpact.ShardMesh(); r.sharedMaterial = MagicFX.StarMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        snow.Play();
    }

    void Refresh(Vector3 from, Vector3 to, float duration)
    {
        a = from; b = to;
        until = Time.time + Mathf.Max(duration, 0.05f);
        Shape();
    }

    void Shape()
    {
        Vector3 dir = b - a; float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;
        if (snowT != null)
        {
            snowT.position = (a + b) * 0.5f; snowT.rotation = Quaternion.LookRotation(dir);
            var sh = snow.shape; sh.scale = new Vector3(width * 3f, width * 3f, len);
            var vel = snow.velocityOverLifetime; vel.z = new ParticleSystem.MinMaxCurve(len * 0.1f, len * 0.4f);
        }
        if (key == "LightningLance") { Jag(core, 0.18f, 10); Jag(glow, 0.18f, 10, true); }
        else
        {
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * (key == "RadiantBeam" ? 0.06f : 0.04f);
            core.positionCount = 2; core.SetPosition(0, a); core.SetPosition(1, b); core.startWidth = core.endWidth = width * 0.5f * pulse;
            glow.positionCount = 2; glow.SetPosition(0, a); glow.SetPosition(1, b); glow.startWidth = glow.endWidth = width * 1.7f * pulse;
        }
    }

    // a bolt: a polyline along the beam with random sideways kinks, thinner at the far end
    void Jag(LineRenderer l, float amount, int segs, bool copyCore = false)
    {
        if (copyCore && core.positionCount > 1)
        {
            l.positionCount = core.positionCount;
            for (int i = 0; i < l.positionCount; i++) l.SetPosition(i, core.GetPosition(i));
            return;
        }
        Vector3 dir = (b - a); float len = dir.magnitude; dir /= len;
        Vector3 s1 = Vector3.Cross(dir, Vector3.up); if (s1.sqrMagnitude < 0.01f) s1 = Vector3.right; s1.Normalize();
        Vector3 s2 = Vector3.Cross(dir, s1);
        l.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            float f = i / (float)segs;
            float k = (i == 0 || i == segs) ? 0f : amount * Mathf.Min(1f, len * 0.05f + 0.3f);
            l.SetPosition(i, Vector3.Lerp(a, b, f) + (s1 * Random.Range(-1f, 1f) + s2 * Random.Range(-1f, 1f)) * k);
        }
    }

    void Update()
    {
        if (Time.time >= until)
        {
            if (live.TryGetValue(key, out var cur) && cur == this) live.Remove(key);
            if (snow != null) { snowT.SetParent(null); snow.Stop(); Destroy(snowT.gameObject, 1f); }
            Destroy(glowMat); Destroy(coreMat); Destroy(gameObject);
            return;
        }

        float life = Mathf.Clamp01((until - Time.time) / 0.12f);        // the beam thins out as it ends
        if (key == "LightningLance")
        {
            // the bolt flickers: new kinks and a new brightness several times a second, and short forks off the main line
            if (Time.time >= nextJitter)
            {
                nextJitter = Time.time + 0.035f;
                Jag(core, 0.2f, 10); Jag(glow, 0.2f, 10, true);
                foreach (var f in forks)
                {
                    float at = Random.Range(0.15f, 0.85f);
                    Vector3 start = Vector3.Lerp(a, b, at);
                    Vector3 dir = ((b - a).normalized + Random.onUnitSphere * 0.8f).normalized;
                    f.positionCount = 4;
                    for (int i = 0; i < 4; i++) f.SetPosition(i, start + dir * i * Random.Range(0.4f, 0.8f) + Random.insideUnitSphere * 0.15f);
                    f.startWidth = width * 0.7f; f.endWidth = 0.01f;
                    f.enabled = Random.value > 0.25f;
                }
            }
            float flick = Random.Range(0.55f, 1f) * life;
            core.startColor = core.endColor = new Color(1f, 1f, 0.85f, flick);
            glow.startColor = glow.endColor = new Color(0.45f, 0.65f, 1f, flick * 0.8f);
        }
        else
        {
            Shape();
            core.startColor = core.endColor = new Color(1f, 1f, 1f, life);
            glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, 0.75f * life);
            if (Time.time >= nextBurst)
            {
                nextBurst = Time.time + 0.22f;
                SpellImpact.Play(key, b, (a - b).normalized, 1.0f, color);
            }
        }
    }
}
