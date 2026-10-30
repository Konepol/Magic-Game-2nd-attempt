using System.Collections.Generic;
using UnityEngine;

// Per-spell casting animations and hand effects. Each spell can have a CastStyle: where the arms point while chanting, after a tap and at the
// release, plus a glowing orb in the hands. The driver below turns the arm bones toward those directions after the animator has run (the humanoid
// muscle axes of this character are not intuitive, so the arms are aimed directly). A spell without a style keeps the shared wand animation.
// See VFX_SPEC.md for what each spell should look like.

// where the arms point, as directions in the caster's own space (x right, y up, z forward): the upper arm and the forearm of each arm
public struct ArmPose
{
    public Vector3 rUpper, rFore, lUpper, lFore;
    public float lean;                                  // spine lean forward (+) in degrees

    // the left arm mirrors the right one (x flipped)
    public static ArmPose Both(Vector3 upper, Vector3 fore, float lean = 0f)
    {
        return new ArmPose { rUpper = upper, rFore = fore, lUpper = Mirror(upper), lFore = Mirror(fore), lean = lean };
    }

    // only the wand arm moves
    public static ArmPose Right(Vector3 upper, Vector3 fore, float lean = 0f)
    {
        return new ArmPose { rUpper = upper, rFore = fore, lean = lean };
    }

    static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    public static ArmPose Lerp(ArmPose a, ArmPose b, float t)
    {
        return new ArmPose
        {
            rUpper = Vector3.Slerp(a.rUpper, b.rUpper, t), rFore = Vector3.Slerp(a.rFore, b.rFore, t),
            lUpper = a.lUpper == Vector3.zero || b.lUpper == Vector3.zero ? (t < 0.5f ? a.lUpper : b.lUpper) : Vector3.Slerp(a.lUpper, b.lUpper, t),
            lFore = a.lFore == Vector3.zero || b.lFore == Vector3.zero ? (t < 0.5f ? a.lFore : b.lFore) : Vector3.Slerp(a.lFore, b.lFore, t),
            lean = Mathf.Lerp(a.lean, b.lean, t),
        };
    }
}

public class CastStyle
{
    public Color color = Color.white;
    public ArmPose chantStart, chantEnd;              // the chant pose at the start and the end of the cast time
    public bool hasChant;
    public (float t, ArmPose pose)[] tap, release;    // timelines after a tap cast and after a held cast (seconds)
    public float orbTap = 0.25f, orbHold = 0.8f;      // size of the hand orb: for a tap flick and at the end of a chant
    public bool orbBetweenHands;                      // the chant orb floats between the hands (two-handed) instead of at the wand hand
    public bool handOrb = true;

    public float Duration((float t, ArmPose pose)[] line) => line != null && line.Length > 0 ? line[line.Length - 1].t : 0f;

    public ArmPose Eval((float t, ArmPose pose)[] line, float time)
    {
        if (time <= line[0].t) return line[0].pose;
        for (int i = 1; i < line.Length; i++)
            if (time <= line[i].t) return ArmPose.Lerp(line[i - 1].pose, line[i].pose, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(line[i - 1].t, line[i].t, time)));
        return line[line.Length - 1].pose;
    }
}

public static class CastStyles
{
    static Dictionary<string, CastStyle> table;

    public static CastStyle For(SpellData spell)
    {
        if (spell == null) return null;
        if (table == null) Build();
        table.TryGetValue(spell.name, out var s);
        return s;
    }

    static Vector3 D(float x, float y, float z) => new Vector3(x, y, z).normalized;

    static void Build()
    {
        table = new Dictionary<string, CastStyle>();

        // Arcane Bolt: a hand thrust forward with a violet orb at the palm; held, a slower two-handed charge with the orb swelling between the hands
        var ready = ArmPose.Right(D(0.45f, -0.55f, 0.55f), D(0.1f, 0.3f, 1f));
        var pulledIn = ArmPose.Right(D(0.5f, -0.2f, 0.4f), D(-0.1f, 0.5f, 0.9f));
        var thrust = ArmPose.Right(D(0.2f, 0.15f, 1f), D(0.05f, 0.1f, 1f), 6f);
        table["ArcaneBolt"] = new CastStyle
        {
            color = new Color(0.6f, 0.45f, 1f),
            orbTap = 0.3f, orbHold = 0.6f, orbBetweenHands = true,
            hasChant = true,
            chantStart = ArmPose.Both(D(0.35f, -0.15f, 0.9f), D(-0.1f, 0.1f, 1f)),
            chantEnd = ArmPose.Both(D(0.2f, 0.2f, 1f), D(-0.4f, 0.15f, 1f), 4f),
            tap = new[] { (0f, ready), (0.07f, pulledIn), (0.17f, thrust), (0.4f, thrust), (0.6f, ready) },
            release = new[]
            {
                (0f, ArmPose.Both(D(0.2f, 0.2f, 1f), D(-0.4f, 0.15f, 1f), 4f)),
                (0.1f, ArmPose.Both(D(0.4f, -0.1f, 0.6f), D(-0.1f, 0.5f, 0.9f), 0f)),
                (0.24f, ArmPose.Both(D(0.15f, 0.15f, 1f), D(0.0f, 0.1f, 1f), 12f)),
                (0.55f, ArmPose.Both(D(0.2f, 0.1f, 1f), D(0.05f, 0.05f, 1f), 8f)),
                (0.85f, ArmPose.Both(D(0.45f, -0.55f, 0.55f), D(0.1f, 0.3f, 1f), 0f)),
            },
        };
    }
}

// Aims the arms of the active CastStyle each frame (after the animator) and shows the orb in the hands.
public class CastPoseDriver : MonoBehaviour
{
    Animator animator;
    PlayerSpellCaster caster;
    Transform rUpper, rLower, rHand, lUpper, lLower, lHand, spine;
    ChargeOrb orb;

    CastStyle playStyle; (float t, ArmPose pose)[] playLine; float playTime; bool playHold;
    float weight;

    void Awake()
    {
        animator = GetComponent<Animator>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }

    void OnEnable() { if (caster != null) caster.SpellCasted += OnCast; }
    void OnDisable() { if (caster != null) caster.SpellCasted -= OnCast; }

    void OnCast(SpellData spell, bool wasHold)
    {
        var style = CastStyles.For(spell);
        if (style == null) return;
        var line = wasHold ? style.release : style.tap;
        if (line == null) return;
        playStyle = style; playLine = line; playTime = 0f; playHold = wasHold;
    }

    void LateUpdate()
    {
        if (rUpper == null || caster == null) return;

        if (playLine != null) { playTime += Time.deltaTime; if (playTime >= playStyle.Duration(playLine)) playLine = null; }

        CastStyle style = null; ArmPose? target = null; float progress = 0f;
        if (playLine != null) { style = playStyle; target = style.Eval(playLine, playTime); }
        else if (caster.IsChanting)
        {
            style = CastStyles.For(caster.ChantSpell);
            if (style != null && style.hasChant)
            {
                progress = caster.ChantProgress;
                target = ArmPose.Lerp(style.chantStart, style.chantEnd, Mathf.SmoothStep(0f, 1f, progress));
            }
        }

        float want = target.HasValue ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, want, Time.deltaTime * (want > weight ? 9f : 5f));

        if (target.HasValue && weight > 0.001f)
        {
            var t = target.Value;
            if (spine != null && Mathf.Abs(t.lean) > 0.01f) spine.rotation = Quaternion.AngleAxis(t.lean * weight, transform.right) * spine.rotation;
            Aim(rUpper, rLower, rHand, t.rUpper, t.rFore);
            if (t.lUpper != Vector3.zero) Aim(lUpper, lLower, lHand, t.lUpper, t.lFore);
        }

        UpdateOrb(style, progress);
    }

    // turns the upper arm so it points along `upperDir`, then the forearm along `foreDir` (both in the caster's space), blended by the weight
    void Aim(Transform upper, Transform lower, Transform hand, Vector3 upperDir, Vector3 foreDir)
    {
        if (upper == null || lower == null || hand == null) return;
        Vector3 want = transform.TransformDirection(upperDir);
        Vector3 now = (lower.position - upper.position).normalized;
        upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), weight) * upper.rotation;

        want = transform.TransformDirection(foreDir);
        now = (hand.position - lower.position).normalized;
        lower.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), weight) * lower.rotation;
    }

    // the glowing ball in the hands: grows while chanting, flashes on a tap, shrinks away at the release
    void UpdateOrb(CastStyle style, float progress)
    {
        float size = 0f;
        if (style != null && style.handOrb)
        {
            if (playLine != null)
            {
                float t = playTime;
                if (playHold) size = style.orbHold * Mathf.Clamp01(1f - t / 0.22f);
                else size = style.orbTap * Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI));
            }
            else size = Mathf.Lerp(style.orbTap * 0.5f, style.orbHold, Mathf.SmoothStep(0f, 1f, progress));
        }

        if (size <= 0.01f) { if (orb != null) orb.SetSize(0f); return; }
        if (orb == null) orb = ChargeOrb.Create(style.color);
        else orb.SetColor(style.color);

        Vector3 pos = rHand != null ? rHand.position : transform.position + Vector3.up;
        bool twoHanded = style.orbBetweenHands && (playLine == null || playHold);
        if (twoHanded && lHand != null && rHand != null) pos = (rHand.position + lHand.position) * 0.5f;
        pos += caster.AimDirection * (twoHanded ? 0.3f : 0.2f);
        orb.transform.position = pos;
        orb.SetSize(size);
    }
}

// a soft glowing ball with a few little stars circling it, held in the caster's hands
public class ChargeOrb : MonoBehaviour
{
    Material mat; Color color; Transform core, halo, stars; float size, shown;

    public static ChargeOrb Create(Color color)
    {
        var o = new GameObject("ChargeOrb").AddComponent<ChargeOrb>();
        o.Build(color);
        return o;
    }

    void Build(Color c)
    {
        color = c;
        mat = new Material(MagicFX.CoreMat);
        for (int i = 0; i < 2; i++)
        {
            var part = new GameObject(i == 0 ? "Core" : "Halo");
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = MagicFX.OrbMesh();
            var r = part.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (i == 0) core = part.transform; else halo = part.transform;
        }
        stars = new GameObject("Stars").transform;
        stars.SetParent(transform, false);
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Star");
            s.transform.SetParent(stars, false);
            s.AddComponent<MeshFilter>().sharedMesh = MagicFX.StarMesh();
            var r = s.AddComponent<MeshRenderer>();
            r.sharedMaterial = MagicFX.StarMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float a = i * Mathf.PI * 2f / 3f;
            s.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, Mathf.Sin(a)) * 0.75f;
            s.transform.localScale = Vector3.one * 0.22f;
        }
        SetSize(0f);
    }

    public void SetColor(Color c) { color = c; }

    public void SetSize(float s) { size = s; }

    void Update()
    {
        shown = Mathf.MoveTowards(shown, size, Time.deltaTime * (size > shown ? 4f : 6f));
        bool on = shown > 0.01f;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = on;
        if (!on) return;

        var c = Color.Lerp(color, Color.white, 0.1f); c.a = 0.85f;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.05f;
        core.localScale = Vector3.one * shown * 0.55f * pulse;
        halo.localScale = Vector3.one * shown * pulse;
        stars.localScale = Vector3.one * shown * 0.5f;
        stars.Rotate(new Vector3(40f, 260f, 0f) * Time.deltaTime, Space.Self);
        foreach (Transform s in stars) s.Rotate(new Vector3(120f, 0f, 90f) * Time.deltaTime, Space.Self);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
