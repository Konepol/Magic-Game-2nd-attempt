using System.Collections.Generic;
using UnityEngine;

// A flat marker on the ground that shows where an aimed spell will land or how far it reaches: a circle, a cone or a line
// (see GroundIndicatorData). It is built from the same glowing layer quads as the casting circles and uses their shader.
//
// How to drive one (PlayerSpellCaster does this):
//   var gi = GroundIndicator.Create(data, colour);
//   every frame while aiming:  gi.Show(); gi.Place(position, rotation, width, length); gi.SetCharge(0..1); if (ready) gi.Lock();
//   when aiming stops:         gi.Hide();
public class GroundIndicator : MonoBehaviour
{
    class LayerInstance
    {
        public IndicatorLayer def;
        public Transform root;        // moved, turned and scaled as one
        public Renderer face;         // the glowing layer
        public Renderer outline;      // optional soft dark copy underneath (readability on pale ground)
        public float angle;
    }

    // the shader's _Mode values (Magic/CastingCircle and Magic/GroundIndicatorOutline)
    const float ModeClockwise = 0f;
    const float ModeRadial = 3f;
    const float ModeBottomUp = 4f;

    const float QuadPadding = 1.16f;     // must match _Pad in both shaders

    // where things sit in the textures (1 = half the image): the circle's outer ring, the cone's rim, and the near and far ends of the line's strip
    const float CircleRim = 0.985f;
    const float ConeRim = 0.95f;
    const float LineNear = 0.93f;
    const float LineFar = 0.843f;
    const float LineHalfWidth = 0.10f;   // the strip is this wide on each side of the middle

    static readonly Dictionary<Texture2D, Material> faceMaterials = new Dictionary<Texture2D, Material>();
    static readonly Dictionary<Texture2D, Material> outlineMaterials = new Dictionary<Texture2D, Material>();
    static Material faceBase;
    static Material outlineBase;
    static bool basesLoaded;

    readonly List<LayerInstance> layers = new List<LayerInstance>();
    MaterialPropertyBlock block;
    GroundIndicatorData data;
    Color spellColor = Color.white;

    float age;                 // seconds since it appeared
    float charge;              // 0..1, set by the caster
    float shownCharge;         // eases toward charge so the fill does not jump
    float lockedAt = -1f;      // age when the spell became ready, -1 = not yet

    float width = 1f;          // circle: diameter; line: how wide the strip is; cone: not used
    float length = 1f;         // cone and line: how far it reaches (metres)

    // ---------- creating ----------

    public static GroundIndicator Create(GroundIndicatorData data, Color color)
    {
        if (data == null || data.layers == null || data.layers.Length == 0) return null;

        var go = SceneGroups.Put(new GameObject("GroundIndicator_" + data.name), "VFX/Casting");
        var gi = go.AddComponent<GroundIndicator>();
        gi.data = data;
        gi.spellColor = color;
        gi.block = new MaterialPropertyBlock();
        gi.Build();
        return gi;
    }

    static void LoadBases()
    {
        if (basesLoaded) return;
        basesLoaded = true;

        faceBase = Resources.Load<Material>("CastingCircleMaterial");
        if (faceBase == null)
        {
            var shader = Shader.Find("Magic/CastingCircle");
            if (shader != null) faceBase = new Material(shader);
        }

        outlineBase = Resources.Load<Material>("GroundIndicatorOutlineMaterial");
        if (outlineBase == null)
        {
            var shader = Shader.Find("Magic/GroundIndicatorOutline");
            if (shader != null) outlineBase = new Material(shader);
        }
    }

    static Material MaterialFor(Texture2D tex, Material baseMaterial, Dictionary<Texture2D, Material> cache)
    {
        if (baseMaterial == null) return null;
        if (cache.TryGetValue(tex, out var m) && m != null) return m;

        m = new Material(baseMaterial);
        m.SetTexture("_BaseMap", tex);
        cache[tex] = m;
        return m;
    }

    void Build()
    {
        LoadBases();

        foreach (var def in data.layers)
        {
            if (def == null || def.texture == null) continue;

            var root = new GameObject(def.name).transform;
            root.SetParent(transform, false);

            var inst = new LayerInstance { def = def, root = root };

            // soft dark copy first, so it sits under the glow
            if (def.outline && !def.chargeFill && data.outlineStrength > 0f)
            {
                var mat = MaterialFor(def.texture, outlineBase, outlineMaterials);
                if (mat != null) inst.outline = MakeQuad("Outline", root, mat);
            }

            var faceMat = MaterialFor(def.texture, faceBase, faceMaterials);
            if (faceMat == null) continue;
            inst.face = MakeQuad("Face", root, faceMat);

            layers.Add(inst);
        }
    }

    static Renderer MakeQuad(string name, Transform parent, Material material)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.transform.SetParent(parent, false);
        quad.transform.localScale = Vector3.one * QuadPadding;      // a little bigger than the image so the glow is not cut off at its edge

        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = false;
        return r;
    }

    // ---------- driving ----------

    // where it sits: position on the ground (the middle of a circle, the caster's end of a cone or line), the rotation from
    // GroundRotation, and the size. Circle: width = diameter. Cone: length. Line: width and length.
    public void Place(Vector3 position, Quaternion rotation, float widthMetres, float lengthMetres)
    {
        width = Mathf.Max(widthMetres, 0.1f);
        length = Mathf.Max(lengthMetres, 0.1f);
        transform.SetPositionAndRotation(position, rotation);
    }

    // lies flat on the ground with the far end pointing along 'forward' (a flat direction)
    public static Quaternion GroundRotation(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        return Quaternion.LookRotation(Vector3.down, forward.normalized);
    }

    public void SetColor(Color color) { spellColor = color; }

    public void SetCharge(float value01) { charge = Mathf.Clamp01(value01); }

    // the spell is fully charged and ready: a short flash, then it holds in the filled state
    public void Lock()
    {
        if (lockedAt < 0f) lockedAt = age;
    }

    public void Show()
    {
        if (gameObject.activeSelf) return;
        age = 0f;
        charge = 0f;
        shownCharge = 0f;
        lockedAt = -1f;
        foreach (var l in layers) l.angle = 0f;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void Update() { Tick(Time.deltaTime); }

    // ---------- drawing ----------

    // the size of the whole indicator quad and where its middle sits relative to the place point
    void Dimensions(out float w, out float h, out Vector3 shift)
    {
        switch (data.shape)
        {
            case IndicatorShape.Cone:
                w = h = 2f * length / ConeRim;                          // the apex is the middle of the texture
                shift = Vector3.zero;
                break;
            case IndicatorShape.Line:
                w = width / LineHalfWidth;                              // the strip spans LineHalfWidth of half the image on each side, so its width is LineHalfWidth of the image
                h = length / ((LineNear + LineFar) * 0.5f);
                shift = new Vector3(0f, LineNear * h * 0.5f, 0f);       // the strip's near end sits on the place point
                break;
            default:
                w = h = width / CircleRim;
                shift = Vector3.zero;
                break;
        }
    }

    static float ModeFor(IndicatorReveal reveal)
    {
        switch (reveal)
        {
            case IndicatorReveal.FromCentre: return ModeRadial;
            case IndicatorReveal.FromBottom: return ModeBottomUp;
            default: return ModeClockwise;
        }
    }

    void Tick(float dt)
    {
        if (data == null) return;
        age += dt;
        shownCharge = Mathf.MoveTowards(shownCharge, charge, dt * 3f);

        float progress = Mathf.Clamp01(age / Mathf.Max(data.drawOnSeconds, 0.05f));
        float flash = lockedAt >= 0f ? Mathf.Clamp01(1f - (age - lockedAt) / Mathf.Max(data.lockFlashSeconds, 0.05f)) : 0f;
        float alphaMul = 1f + data.lockFlash * flash;
        float swell = 1f + data.lockSwell * flash;
        float easedCharge = Mathf.SmoothStep(0f, 1f, shownCharge);

        Dimensions(out float w, out float h, out Vector3 shift);

        foreach (var l in layers)
        {
            var d = l.def;

            float reveal = 1f, sizeMul = 1f, alpha = 1f, mode = ModeClockwise;

            if (d.chargeFill)
            {
                // hidden until charging starts, then revealed as the charge grows
                if (easedCharge <= 0.002f) { SetVisible(l, false); continue; }
                reveal = easedCharge;
                mode = ModeFor(d.chargeReveal);
            }
            else
            {
                float t = Mathf.Clamp01((progress - d.startAt) / Mathf.Max(d.drawTime, 0.02f));
                if (t <= 0f) { SetVisible(l, false); continue; }

                switch (d.appear)
                {
                    case IndicatorReveal.FadeIn:
                        alpha = t;
                        break;
                    case IndicatorReveal.Clockwise:
                        reveal = t; alpha = Mathf.Min(1f, t * 6f); mode = ModeClockwise;
                        break;
                    case IndicatorReveal.FromCentre:
                        reveal = t; alpha = Mathf.Min(1f, t * 6f); mode = ModeRadial;
                        break;
                    case IndicatorReveal.FromBottom:
                        reveal = t; alpha = Mathf.Min(1f, t * 6f); mode = ModeBottomUp;
                        break;
                    case IndicatorReveal.Burst:
                    {
                        // opens fast from a small size, swells slightly past full, settles
                        float k = t - 1f;
                        float back = 1f + 2.2f * k * k * k + 1.2f * k * k;
                        sizeMul = Mathf.LerpUnclamped(0.08f, 1f, back);
                        alpha = Mathf.Min(1f, t * 8f);
                        break;
                    }
                }
            }

            // motion
            l.angle += d.spin * dt;
            float turn = l.angle;
            if (d.swingDegrees != 0f) turn += d.swingDegrees * Mathf.Sin(2f * Mathf.PI * age / Mathf.Max(d.swingPeriod, 0.1f));

            float slide = 0f;
            if (d.slideSpan > 0f)
            {
                float fr = Mathf.Repeat(age / Mathf.Max(d.slidePeriod, 0.1f) + d.slidePhase, 1f);
                slide = (fr - 0.5f) * d.slideSpan + d.slideShift;
                alpha = Mathf.Pow(Mathf.Sin(fr * Mathf.PI), 0.6f);
            }

            // place and size (the layer sits flat in the indicator's own space: x across, y away from the caster)
            l.root.localPosition = shift + new Vector3(d.offset.x * w, (d.offset.y + slide) * h, 0f);
            l.root.localRotation = Quaternion.Euler(0f, 0f, turn);
            float sx = d.absoluteSize ? d.size : w * d.size;
            float sy = d.absoluteSize ? d.size : h * d.size;
            l.root.localScale = new Vector3(sx * sizeMul * swell, sy * sizeMul * swell, 1f);

            // colour: the ready flash pushes everything toward white
            Color c = d.useSpellColor ? spellColor * d.tint : d.tint;
            if (flash > 0f) c = Color.Lerp(c, Color.white, flash * 0.8f);
            c.a = d.opacity * alpha * alphaMul;

            if (l.face == null) continue;
            SetVisible(l, true);

            block.Clear();
            block.SetColor("_Color", c);
            block.SetFloat("_Reveal", reveal);
            block.SetFloat("_Glow", data.glow);
            block.SetFloat("_CoreWhite", data.coreWhite);
            block.SetFloat("_Mode", mode);
            l.face.SetPropertyBlock(block);

            if (l.outline != null)
            {
                block.Clear();
                block.SetColor("_Color", new Color(0f, 0f, 0f, data.outlineStrength * d.opacity * alpha * Mathf.Min(alphaMul, 1f)));
                block.SetFloat("_Reveal", reveal);
                block.SetFloat("_Glow", 1.3f);
                block.SetFloat("_Mode", mode);
                l.outline.SetPropertyBlock(block);
            }
        }
    }

    static void SetVisible(LayerInstance l, bool on)
    {
        if (l.face != null) l.face.enabled = on;
        if (l.outline != null) l.outline.enabled = on;
    }
}
