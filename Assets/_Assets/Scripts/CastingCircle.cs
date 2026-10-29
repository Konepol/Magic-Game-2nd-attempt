using System.Collections.Generic;
using UnityEngine;

// A magic circle made of layers (see CastingCircleData) that draws itself while a spell is being cast.
// Each layer follows its own anchor every frame, so layers can sit in different places and track the player and aim.
// Layers float apart, have a glowing thickness and slowly tilt, so the circle reads as a 3D object rather than a flat decal.
public class CastingCircle : MonoBehaviour
{
    // whatever knows where the anchors are (the spell caster)
    public interface IAnchorSource
    {
        Pose GetCirclePose(CircleAnchor anchor, CastingCircleData data, SpellVariant variant);
    }

    enum Mode { Building, Complete, Fading, Fizzling }

    class LayerInstance
    {
        public CircleLayer def;
        public int index;
        public int groupIndex;            // position among the layers that share its kind (upright or on the ground)
        public Transform root;            // moved, rotated and scaled as one
        public Renderer face;
        public float angle;
    }

    static readonly Dictionary<Texture2D, Material> materials = new Dictionary<Texture2D, Material>();
    static Material baseMaterial;

    readonly List<LayerInstance> layers = new List<LayerInstance>();
    MaterialPropertyBlock block;
    CastingCircleData data;
    IAnchorSource source;
    SpellVariant variant;
    Color spellColor = Color.white;
    float scale = 1f;

    Mode mode = Mode.Building;
    float progress;
    float modeTime;
    float modeDuration = 0.35f;
    float autoDrawSeconds;      // > 0: draws itself on a timer and then fades out (quick tap circles)

    // where this circle sits on the ring around the player when several casts share the aim (0 degrees = straight ahead)
    float placementAngle, targetAngle;
    float placementRadius = 1f, targetRadius = 1f;      // multiplies the distance from the player
    float placementScale = 1f, targetScale = 1f;
    bool placementSet;

    // ---------- creating ----------

    // a circle you drive yourself with SetProgress, then Complete / FadeOut / Fizzle
    public static CastingCircle Create(CastingCircleData data, Color color, IAnchorSource source, SpellVariant variant, float scale)
    {
        if (data == null || data.layers == null || data.layers.Length == 0 || source == null) return null;

        var go = new GameObject("CastingCircle_" + data.name);
        var c = go.AddComponent<CastingCircle>();
        c.data = data;
        c.spellColor = color;
        c.source = source;
        c.variant = variant;
        c.scale = scale;
        c.block = new MaterialPropertyBlock();
        c.Build();
        return c;
    }

    // a small circle that draws itself quickly and fades away (for tapped, instant spells)
    public static void PlayOneShot(CastingCircleData data, Color color, IAnchorSource source, SpellVariant variant, float scale, float drawSeconds = 0.22f)
    {
        var c = Create(data, color, source, variant, scale);
        if (c != null) c.autoDrawSeconds = Mathf.Max(drawSeconds, 0.05f);
    }

    void Build()
    {
        int index = 0, upright = 0, ground = 0;

        foreach (var def in data.layers)
        {
            if (def == null || def.texture == null) continue;

            var root = new GameObject(def.name).transform;
            root.SetParent(transform, false);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Face";
            Kill(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);

            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFor(def.texture);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;

            layers.Add(new LayerInstance { def = def, index = index++, groupIndex = def.anchor == CircleAnchor.Front ? upright++ : ground++, root = root, face = r });
        }
    }

    // Destroy does not work in edit mode (the editor preview window uses circles there)
    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    static Material MaterialFor(Texture2D tex)
    {
        if (materials.TryGetValue(tex, out var m) && m != null) return m;

        if (baseMaterial == null) baseMaterial = Resources.Load<Material>("CastingCircleMaterial");
        Material mat;
        if (baseMaterial != null) mat = new Material(baseMaterial);
        else mat = new Material(Shader.Find("Magic/CastingCircle"));

        mat.SetTexture("_BaseMap", tex);
        materials[tex] = mat;
        return mat;
    }

    // ---------- driving ----------

    public void SetProgress(float p) { if (mode == Mode.Building) progress = Mathf.Clamp01(p); }

    // puts this circle on a ring around the player: angle in degrees (+ is to the right), the ring's size relative to the normal
    // distance, and a size multiplier. Used to share the aim between several casts; changes glide.
    public void SetPlacement(float angle, float radiusMultiplier, float scaleMultiplier)
    {
        targetAngle = angle;
        targetRadius = radiusMultiplier;
        targetScale = scaleMultiplier;
        if (!placementSet)
        {
            placementAngle = angle;         // a new circle appears where it belongs; later changes glide
            placementRadius = radiusMultiplier;
            placementScale = scaleMultiplier;
            placementSet = true;
        }
    }

    // fully drawn; holds with a gentle pulse until faded out
    public void Complete()
    {
        if (mode != Mode.Building) return;
        progress = 1f;
        mode = Mode.Complete;
        modeTime = 0f;
    }

    public void FadeOut(float seconds = 0.35f)
    {
        if (mode == Mode.Fading || mode == Mode.Fizzling) return;
        progress = 1f;
        mode = Mode.Fading;
        modeTime = 0f;
        modeDuration = Mathf.Max(seconds, 0.05f);
    }

    // the spell failed: the circle flickers red, shrinks and breaks apart
    public void Fizzle()
    {
        if (mode == Mode.Fizzling) return;
        mode = Mode.Fizzling;
        modeTime = 0f;
        modeDuration = 0.4f;
    }

    void Update() => Tick(Time.deltaTime);

    // advances the circle; the editor preview window calls this itself in edit mode
    public void Tick(float dt)
    {
        modeTime += dt;

        if (autoDrawSeconds > 0f && mode == Mode.Building)
        {
            progress += dt / autoDrawSeconds;
            if (progress >= 1f) FadeOut(0.25f);
        }

        // glide toward the placement we were asked for
        float glide = 1f - Mathf.Exp(-9f * dt);
        placementAngle = Mathf.Lerp(placementAngle, targetAngle, glide);
        placementRadius = Mathf.Lerp(placementRadius, targetRadius, glide);
        placementScale = Mathf.Lerp(placementScale, targetScale, glide);

        // whole-circle effects for the current mode
        float alphaMul = 1f, scaleMul = 1f;
        Color tintOverride = Color.clear;
        float overrideAmount = 0f;

        switch (mode)
        {
            case Mode.Complete:
            {
                // the moment it completes: a flash of light, then it settles into the ready colour.
                // The swell / pulse is an optional animation (Ready Pulse on the circle asset), off by default.
                float flash = Mathf.Clamp01(1f - modeTime / 0.35f) * data.readyFlash;
                float pulse = data.readyPulse * (0.03f * Mathf.Sin(modeTime * 10f) + 0.10f * flash);
                scaleMul = 1f + pulse;
                alphaMul = 1f + 0.8f * flash;
                tintOverride = Color.Lerp(data.readyColor, Color.white, flash);
                overrideAmount = Mathf.Max(Mathf.Lerp(0f, data.readyColorBlend, Mathf.Clamp01(modeTime / 0.15f)), flash * 0.9f);
                break;
            }
            case Mode.Fading:
                alphaMul = 1f - Mathf.Clamp01(modeTime / modeDuration);
                scaleMul = 1f + 0.12f * Mathf.Clamp01(modeTime / modeDuration);
                if (modeTime >= modeDuration) { Kill(gameObject); return; }
                break;
            case Mode.Fizzling:
            {
                float k = Mathf.Clamp01(modeTime / modeDuration);
                alphaMul = (Mathf.Sin(modeTime * 60f) > 0f ? 1f : 0.25f) * (1f - k);     // flicker, then gone
                scaleMul = Mathf.Lerp(1f, 0.55f, k);
                tintOverride = new Color(1f, 0.2f, 0.15f, 1f);
                overrideAmount = 0.8f;
                if (modeTime >= modeDuration) { Kill(gameObject); return; }
                break;
            }
        }

        float placeScale = placementScale * scale;
        foreach (var l in layers)
        {
            var def = l.def;
            float t = Mathf.Clamp01((progress - def.startAt) / Mathf.Max(def.drawTime, 0.02f));
            if (t <= 0f) { SetEnabled(l, false); continue; }
            SetEnabled(l, true);

            l.angle += def.rotationSpeed * dt;

            Pose pose = source.GetCirclePose(def.anchor, data, variant);

            // depth: layers float apart (away from the viewer for upright circles, upward for flat ones)
            float depth = def.anchor == CircleAnchor.Front ? l.groupIndex * data.depthSpacing : -l.groupIndex * data.groundLayerSpacing;
            Vector3 local = def.offset * placeScale + new Vector3(0f, 0f, depth);

            // each layer wobbles on its own rhythm, which makes the layers shift against each other
            Quaternion wobble = Quaternion.identity;
            bool onGround = def.anchor != CircleAnchor.Front;
            bool highEnough = !onGround || -def.offset.z * placeScale >= data.groundTiltMinHeight;      // flat circles stay on the floor unless raised
            if (data.tiltDegrees > 0.01f && highEnough)
            {
                float ph = l.index * 1.7f;
                wobble = Quaternion.Euler(Mathf.Sin(Time.time * data.tiltSpeed + ph) * data.tiltDegrees,
                                          Mathf.Cos(Time.time * data.tiltSpeed * 0.8f + ph) * data.tiltDegrees, 0f);
            }

            Vector3 worldPos = pose.position + pose.rotation * local;
            Quaternion worldRot = pose.rotation * wobble * Quaternion.Euler(0f, 0f, l.angle);

            // upright layers swing around the player on the ring; the parts on the ground stay centred on the player / the target
            if (def.anchor == CircleAnchor.Front && (Mathf.Abs(placementAngle) > 0.01f || Mathf.Abs(placementRadius - 1f) > 0.001f))
            {
                Vector3 feet = source.GetCirclePose(CircleAnchor.Feet, data, variant).position;
                Vector3 pivot = new Vector3(feet.x, worldPos.y, feet.z);
                Vector3 v = worldPos - pivot;
                v.x *= placementRadius;
                v.z *= placementRadius;
                Quaternion swing = Quaternion.AngleAxis(placementAngle, Vector3.up);
                worldPos = pivot + swing * v;
                worldRot = swing * worldRot;
            }

            l.root.position = worldPos;
            l.root.rotation = worldRot;

            float diameter = def.scaleToAoeRadius && variant != null ? variant.aoeRadius * 2f * def.size : def.size * placeScale;

            float reveal = 1f, sizeMul = 1f, styleAlpha = 1f;
            switch (def.appear)
            {
                case CircleAppear.RadialWipe: reveal = t; styleAlpha = Mathf.Min(1f, t * 6f); break;
                case CircleAppear.ScaleUp: sizeMul = Mathf.Lerp(0.2f, 1f, 1f - (1f - t) * (1f - t)); styleAlpha = Mathf.Min(1f, t * 4f); break;
                case CircleAppear.FadeIn: styleAlpha = t; break;
            }

            l.root.localScale = Vector3.one * (diameter * sizeMul * scaleMul);

            Color c = def.useSpellColor ? spellColor * def.tint : def.tint;
            if (overrideAmount > 0f) c = Color.Lerp(c, tintOverride, overrideAmount);
            float baseAlpha = def.opacity * styleAlpha * alphaMul;

            c.a = baseAlpha;
            block.SetColor("_Color", c);
            block.SetFloat("_Reveal", reveal);
            l.face.SetPropertyBlock(block);
        }
    }

    static void SetEnabled(LayerInstance l, bool on)
    {
        l.face.enabled = on;
    }
}
