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
        public Renderer[] pieces;        // arrow ring: 4 diamonds then 4 V's
        public float angle;
        public float hold;               // seconds this layer has been fully drawn
        public float ringAngle;          // arrow ring: degrees it has turned since it settled
    }

    const float QuadPadding = 1.16f;      // must match _Pad in the Magic/CastingCircle shader

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
    float clock;                // seconds this circle has existed (drives the hand-drawn variants)
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

        var go = SceneGroups.Put(new GameObject("CastingCircle_" + data.name), "VFX/Casting");
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
            quad.transform.localScale = Vector3.one * QuadPadding;      // a little bigger than the image so the stroke glow is not cut off at its edge

            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFor(def.texture);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;

            Renderer[] pieces = null;
            if (def.arrowRing && def.arrowDiamond != null && def.arrowV != null)
            {
                pieces = new Renderer[8];
                for (int p = 0; p < 8; p++)
                {
                    var pq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    pq.name = p < 4 ? "Diamond" : "V";
                    Kill(pq.GetComponent<Collider>());
                    pq.transform.SetParent(root, false);
                    var pr = pq.GetComponent<Renderer>();
                    pr.sharedMaterial = MaterialFor(p < 4 || !def.pointToCenter ? def.arrowDiamond : def.arrowV);
                    pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pr.receiveShadows = false;
                    pr.enabled = false;
                    pieces[p] = pr;
                }
            }

            layers.Add(new LayerInstance { def = def, index = index++, groupIndex = def.anchor == CircleAnchor.Front ? upright++ : ground++, root = root, face = r, pieces = pieces });
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
        modeDuration = Mathf.Max(data.exitDuration > 0f ? data.exitDuration : seconds, 0.05f);      // the circle asset can set its own exit length
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
        clock += dt;

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
        float exitK = 0f, exitSpin = 0f, exitDissolve = 0f, exitLaunch = 0f, exitStretch = 0f;

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
            {
                // the way the circle leaves is chosen on the circle asset (Exit Style)
                float k = Mathf.Clamp01(modeTime / modeDuration);
                float k2 = k * k;
                switch (data.exitStyle)
                {
                    case CircleExit.Fade:          // plain fade with a slight swell
                        alphaMul = 1f - k; scaleMul = 1f + 0.12f * k;
                        break;
                    case CircleExit.Implode:       // sucked into the middle, spinning faster and faster
                        alphaMul = 1f - k2; scaleMul = Mathf.Lerp(1f, 0.02f, k2); exitSpin = k2 * 300f;
                        break;
                    case CircleExit.Shockwave:     // flashes white and rushes outward like a ripple
                        alphaMul = 1f - k; scaleMul = 1f + 1.1f * (1f - (1f - k) * (1f - k));
                        tintOverride = Color.white; overrideAmount = (1f - k) * 0.6f;
                        break;
                    case CircleExit.SpinAway:      // whips round and shrinks away
                        alphaMul = 1f - k2; scaleMul = Mathf.Lerp(1f, 0.15f, k2); exitSpin = k2 * 720f;
                        break;
                    case CircleExit.Unwind:        // un-draws itself, last drawn part first
                        alphaMul = 1f - Mathf.Clamp01((k - 0.85f) / 0.15f);
                        break;
                    case CircleExit.Dissolve:      // breaks into specks
                        exitDissolve = k;
                        break;
                    case CircleExit.Shatter:       // white flash, then the lines are pulled outward and break into specks
                        alphaMul = 1f - k2; scaleMul = 1f + 0.3f * k;
                        tintOverride = Color.white; overrideAmount = Mathf.Clamp01(1f - k * 4f) * 0.9f;
                        exitDissolve = Mathf.Clamp01((k - 0.12f) / 0.88f); exitStretch = k2;
                        break;
                    case CircleExit.Overload:      // the lines hang on a moment while they stretch longer and thinner, then fade
                        alphaMul = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.3f) / 0.45f));
                        scaleMul = 1f + 0.25f * k; exitStretch = Mathf.Clamp01(k * 1.4f);
                        break;
                    case CircleExit.Launch:        // shoots forward toward where the spell is going
                        alphaMul = 1f - k2; scaleMul = Mathf.Lerp(1f, 0.45f, k); exitLaunch = k2;
                        break;
                }
                exitK = k;
                if (modeTime >= modeDuration) { Kill(gameObject); return; }
                break;
            }
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
            if (def.exitOnly)
            {
                if (mode != Mode.Fading) { SetEnabled(l, false); continue; }
                t = Mathf.Clamp01((exitK - def.startAt) / Mathf.Max(def.drawTime, 0.02f));      // a fraction of the exit
            }
            if (t <= 0f) { SetEnabled(l, false); continue; }
            SetEnabled(l, true);

            float spinF = def.spinSpeed * (def.reverseSpin ? -1f : 1f);      // Spin Speed and Reverse Spin on the layer
            float curveF = SpinCurve(def, t);
            l.angle += def.rotationSpeed * spinF * curveF * dt;

            // how this layer comes in (each layer can have its own)
            float reveal = 1f, sizeMul = 1f, styleAlpha = 1f, enterSpin = 0f, flip = 0f;
            float dir = Mathf.Abs(def.rotationSpeed) > 0.01f ? Mathf.Sign(def.rotationSpeed) : (l.index % 2 == 0 ? 1f : -1f);      // layers that spin the same way also come in the same way
            if (def.exitOnly)
            {
                // swells and brightens quickly, then fades as it grows
                float a = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                styleAlpha = a * a;
                sizeMul = Mathf.Lerp(def.exitGrow, 1f, 1f - (1f - t) * (1f - t));
            }
            else switch (def.appear)
            {
                case CircleAppear.RadialWipe: reveal = t; styleAlpha = Mathf.Min(1f, t * 6f); break;
                case CircleAppear.ScaleUp: sizeMul = Mathf.Lerp(0.2f, 1f, 1f - (1f - t) * (1f - t)); styleAlpha = Mathf.Min(1f, t * 4f); break;
                case CircleAppear.FadeIn: styleAlpha = t; break;
                case CircleAppear.Burst:
                {
                    // ease out with an overshoot: opens fast from a small size, swells slightly past full, settles
                    float k = t - 1f;
                    float back = 1f + 2.2f * k * k * k + 1.2f * k * k;
                    sizeMul = Mathf.LerpUnclamped(0.08f, 1f, back);
                    styleAlpha = Mathf.Min(1f, t * 8f);
                    break;
                }
                case CircleAppear.Sparkle: case CircleAppear.Cascade: reveal = t; break;
                case CircleAppear.RadialOut: reveal = t; styleAlpha = Mathf.Min(1f, t * 6f); break;
                case CircleAppear.Pop: reveal = t; styleAlpha = Mathf.Min(1f, t * 10f); break;
                case CircleAppear.Bloom:
                {
                    // opens from the middle outward, growing and whirling, the spin dying away as it settles
                    float u = 1f - t;
                    reveal = t;
                    sizeMul = Mathf.Lerp(0.12f, 1f, 1f - u * u);
                    enterSpin = u * u * 360f * dir;
                    styleAlpha = Mathf.Min(1f, t * 5f);
                    break;
                }
                case CircleAppear.Flash:
                {
                    // rises to a peak and is gone again, swelling as it goes
                    sizeMul = Mathf.Lerp(0.3f, 1.25f, 1f - (1f - t) * (1f - t));
                    styleAlpha = Mathf.Sin(t * Mathf.PI);
                    break;
                }
                case CircleAppear.SpinIn:
                {
                    // grows while whirling, the spin slowing to a stop as it settles
                    float u = 1f - t;
                    sizeMul = Mathf.Lerp(0.25f, 1f, 1f - u * u);
                    enterSpin = u * u * 540f * dir;
                    styleAlpha = Mathf.Min(1f, t * 4f);
                    break;
                }
                case CircleAppear.Stamp:
                {
                    // starts big and faint, then slams down onto its place
                    float u = 1f - t;
                    sizeMul = 1f + 0.9f * u * u * u;
                    styleAlpha = Mathf.Min(1f, t * 5f);
                    break;
                }
                case CircleAppear.FlipIn:
                {
                    // lies edge-on and swings up to face you
                    float u = 1f - t;
                    flip = u * u * 85f;
                    sizeMul = Mathf.Lerp(0.7f, 1f, 1f - u * u);
                    styleAlpha = Mathf.Min(1f, t * 3f);
                    break;
                }
            }

            enterSpin *= spinF;

            // leaving by un-drawing: the layer drawn last is erased first
            if (mode == Mode.Fading && data.exitStyle == CircleExit.Unwind)
            {
                float rank = layers.Count - 1 - l.index;
                float lk = Mathf.Clamp01((exitK - rank * 0.5f / layers.Count) / 0.5f);
                reveal = 1f - lk;
            }

            Pose pose = source.GetCirclePose(def.anchor, data, variant);

            // depth: layers float apart (away from the viewer for upright circles, upward for flat ones)
            float depth = def.anchor == CircleAnchor.Front ? l.groupIndex * data.depthSpacing : -l.groupIndex * data.groundLayerSpacing;
            Vector3 local = def.offset * placeScale + new Vector3(0f, 0f, depth);
            if (exitLaunch > 0f && def.anchor == CircleAnchor.Front) local.z += exitLaunch * 3.5f * placeScale;

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

            // spin drawn 'on twos': it moves in jumps instead of smoothly
            float shownAngle = l.angle;
            if (def.stepFps > 0f && Mathf.Abs(def.rotationSpeed * spinF) > 0.01f)
            {
                float stepDeg = Mathf.Abs(def.rotationSpeed * spinF) / def.stepFps;
                shownAngle = Mathf.Round(l.angle / stepDeg) * stepDeg;
            }

            Vector3 worldPos = pose.position + pose.rotation * local;
            Quaternion spinQ = Quaternion.Euler(0f, 0f, shownAngle + enterSpin + exitSpin * dir);
            Quaternion worldRot = pose.rotation * wobble * Quaternion.Euler(flip, 0f, 0f) * spinQ;

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

            // while held: slow outward drift and shimmer
            if (t >= 1f) { l.hold = Mathf.Min(l.hold + dt, 4f); l.ringAngle += def.ringSpin * spinF * curveF * dt; }
            if (def.holdDrift != 0f) sizeMul *= 1f + def.holdDrift * l.hold;
            if (def.shimmer > 0f) styleAlpha *= 1f - def.shimmer * 0.5f * (1f + 0.5f * Mathf.Sin(Time.time * 11f + l.index * 2.3f) + 0.5f * Mathf.Sin(Time.time * 17.3f + l.index * 5.1f));

            l.root.localScale = Vector3.one * (diameter * sizeMul * (def.exitOnly ? 1f : scaleMul));

            // Spin Origin: the layer spins around this point instead of its middle (the point stays where it would be unspun)
            if (def.spinOrigin != Vector2.zero)
            {
                Vector3 pv = new Vector3(def.spinOrigin.x, def.spinOrigin.y, 0f) * l.root.localScale.x;
                Quaternion baseQ = worldRot * Quaternion.Inverse(spinQ);
                l.root.position = worldPos + baseQ * pv - worldRot * pv;
            }

            Color c = def.useSpellColor ? spellColor * def.tint : def.tint;
            if (overrideAmount > 0f && !def.exitOnly) c = Color.Lerp(c, tintOverride, overrideAmount);
            float baseAlpha = def.opacity * styleAlpha * (def.exitOnly ? 1f : alphaMul);

            c.a = baseAlpha;
            block.Clear();
            if (def.variants != null && def.variants.Length > 0)
            {
                // hand-drawn wiggle: the layer swaps between its drawings
                int vi = (Mathf.FloorToInt(clock * Mathf.Max(def.variantFps, 1f)) + l.index * 3) % def.variants.Length;
                if (def.variants[vi] != null) block.SetTexture("_BaseMap", def.variants[vi]);
            }
            block.SetColor("_Color", c);
            block.SetFloat("_Reveal", reveal);
            block.SetFloat("_Dissolve", def.exitOnly ? 0f : exitDissolve);
            block.SetFloat("_Glow", data.glow);
            block.SetFloat("_CoreWhite", data.coreWhite);
            block.SetFloat("_Stretch", def.exitOnly ? 0f : exitStretch);
            block.SetFloat("_Mode", RevealMode(def.appear));
            l.face.SetPropertyBlock(block);

            if (def.arrowRing && l.pieces != null)
            {
                Color accent = def.arrowAccent;
                if (overrideAmount > 0f) accent = Color.Lerp(accent, tintOverride, overrideAmount);
                UpdateArrowRing(l, t, l.ringAngle, c, accent, def.opacity * Mathf.Min(1f, t * 8f) * alphaMul);
            }
        }
    }

    // which reveal the shader uses: 0 clockwise draw-on, 1 random glyphs, 2 glyphs from the middle, 3 travelling ring from the middle
    static float RevealMode(CircleAppear a)
    {
        switch (a)
        {
            case CircleAppear.Sparkle: return 1f;
            case CircleAppear.Cascade: return 2f;
            case CircleAppear.RadialOut: case CircleAppear.Bloom: case CircleAppear.Pop: return 3f;
            default: return 0f;
        }
    }

    // the layer's Spin Over Time graph at this point of its drawing (a flat 1 if the graph is empty)
    static float SpinCurve(CircleLayer def, float t)
    {
        var c = def.spinOverTime;
        return c == null || c.length == 0 ? 1f : c.Evaluate(Mathf.Clamp01(t));
    }

    static void SetEnabled(LayerInstance l, bool on)
    {
        l.face.enabled = on && l.pieces == null;
        if (l.pieces != null) foreach (var pr in l.pieces) pr.enabled = on;
    }

    // ---------- the arrow ring ----------
    // Positions are in units of the layer's diameter (the ring starts at radius 0.5), y up, angles in degrees counter-clockwise.
    // One after the other: each piece turns a little on its own -> V's close up on their diamonds -> each group turns to face the
    // middle (the shape is complete) -> the whole ring scales down and moves in -> locked small, it circles the middle.
    const float ArrowL = 0.30f;      // wing length of the V in the arrow images, as a fraction of the image width

    static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // orbit-only ring: 4 V's alternate with 4 second pieces (triangles) on the ring, tips pointing counter-clockwise along it.
    // The layer's own rotation and its Appear (growth) do the rest, so the pieces just sit on the ring here.
    void UpdateOrbitRing(LayerInstance l, Color main, Color accent, float alpha)
    {
        float wing = l.def.pieceWing;
        int count = Mathf.Clamp(l.def.pieceCount, 1, 8);
        main.a = alpha;
        for (int k = 0; k < 8; k++)
        {
            if (k >= count) { SetPiece(l.pieces[k], Vector2.zero, 0f, 0f, main, l.def); continue; }
            float psi = 360f * k / count + 45f;
            float sgn = l.def.reverseSpin ? -1f : 1f;
            float axis = psi + sgn * (90f + l.def.pieceTurn);          // along the ring, the way it spins, turned by Piece Turn (+ = toward the middle)
            // the middle of the piece sits on the ring and the piece runs along it (the image's tip is at its front end, about 0.38 of the image behind it is its back end)
            float half = 0.5f * 0.38f * (wing / ArrowL);
            Vector2 tip = l.def.ringRadius * Dir(psi) + Dir(axis) * half * (1f - l.def.piecePivot);      // Piece Pivot: the point of the piece that sits on the ring (0 middle, +1 tip, -1 back)
            SetPiece(l.pieces[k], tip, axis, wing / ArrowL, main, l.def);
        }
    }

    void UpdateArrowRing(LayerInstance l, float s, float spinDeg, Color main, Color accent, float alpha)
    {
        var def = l.def;
        if (!def.pointToCenter) { UpdateOrbitRing(l, main, accent, alpha); return; }
        float fuse = Smooth((s - 0.2f) / 0.25f);
        float turn = Smooth((s - 0.45f) / 0.2f);
        float shrink = Smooth((s - 0.72f) / 0.28f);
        float ringR = 0.5f * (1f - 0.62f * shrink);
        float wing = (30f + 22f * fuse) * (1f - 0.5f * shrink);
        float dW = 0.78f * wing / 300f;                         // the diamond's wing length (the V is 1.6 times that)
        float wob = s < 0.25f ? 30f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, s / 0.25f)) : 0f;
        float nose = 2.07f * dW * 0.5f + 0.55f * dW;            // how far the V's tip sits ahead of the diamond's middle once fused
        float baseDeg = Mathf.Rad2Deg * (nose / ringR);

        // the whole ring spins around the middle while the pieces appear, slowing to a stop as the shape completes
        float eo = 1f - Mathf.Pow(1f - Mathf.Clamp01(s / 0.65f), 3f);
        spinDeg += -360f * def.orbitTurns * (1f - eo) * def.spinSpeed * (def.reverseSpin ? -1f : 1f);

        main.a = alpha;
        accent.a = alpha;

        for (int k = 0; k < 4; k++)
        {
            float psi = 90f * k + 20f + spinDeg;
            float ad = psi - 14f * (1f - fuse);                 // the diamond sits behind its V on the ring
            Vector2 dc = ringR * Dir(ad);
            float axis = Mathf.LerpAngle(ad + 90f, ad + 180f, turn);     // along the ring (tips point counter-clockwise), then toward the middle
            Vector2 vRing = ringR * Dir(ad + baseDeg + 20f * (1f - fuse));
            Vector2 vAxis = dc + Dir(axis) * nose;
            Vector2 vTip = Vector2.Lerp(vRing, vAxis, turn);

            SetPiece(l.pieces[k], dc, axis + wob, dW / ArrowL, main, def);
            SetPiece(l.pieces[4 + k], vTip, axis - wob, 1.6f * dW / ArrowL, accent, def);
        }
    }

    void SetPiece(Renderer r, Vector2 pos, float angle, float size, Color col, CircleLayer def)
    {
        var t = r.transform;
        t.localPosition = new Vector3(pos.x, pos.y, 0f);
        t.localRotation = Quaternion.Euler(0f, 0f, angle);
        t.localScale = Vector3.one * (size * QuadPadding);
        block.Clear();
        block.SetColor("_Color", col);
        block.SetFloat("_Reveal", 1f);
        block.SetFloat("_Glow", data.glow);
        block.SetFloat("_CoreWhite", data.coreWhite);
        block.SetFloat("_Mode", 0f);
        r.SetPropertyBlock(block);
    }
}
