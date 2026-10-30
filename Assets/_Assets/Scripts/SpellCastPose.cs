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

    public static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

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
    public (float t, ArmPose pose)[] tapAir;          // optional: the tap animation used while flying on the broom
    public float orbTap = 0.25f, orbHold = 0.8f;      // size of the hand orb: for a tap flick and at the end of a chant
    public bool orbBetweenHands;                      // the chant orb floats between the hands (two-handed) instead of at the wand hand
    public bool handOrb = true;
    public float shake;                               // degrees of jitter in the arms while casting (Spark Stream's trembling hand)

    // a fusion's style: the right arm does the first parent's move and the left arm the second parent's, at the same time, with both colours in the orb
    public static CastStyle Fuse(CastStyle a, CastStyle b)
    {
        var lineA = a.release ?? a.tap; var lineB = b.release ?? b.tap;
        float end = Mathf.Max(a.Duration(lineA), b.Duration(lineB));
        var times = new SortedSet<float> { 0f, end };
        foreach (var k in lineA) times.Add(k.t);
        foreach (var k in lineB) times.Add(k.t);
        var mix = new List<(float, ArmPose)>();
        foreach (float t in times)
        {
            var pa = a.Eval(lineA, Mathf.Min(t, a.Duration(lineA))); var pb = b.Eval(lineB, Mathf.Min(t, b.Duration(lineB)));
            mix.Add((t, new ArmPose { rUpper = pa.rUpper, rFore = pa.rFore, lUpper = ArmPose.Mirror(pb.rUpper), lFore = ArmPose.Mirror(pb.rFore), lean = Mathf.Max(pa.lean, pb.lean) }));
        }
        return new CastStyle
        {
            color = Color.Lerp(a.color, b.color, 0.5f),
            orbTap = Mathf.Max(a.orbTap, b.orbTap), orbHold = Mathf.Max(a.orbHold, b.orbHold) * 1.2f, orbBetweenHands = true,
            hasChant = true, chantStart = Moves.TwoHandStart, chantEnd = Moves.TwoHandEnd,
            tap = mix.ToArray(), release = mix.ToArray(),
            shake = Mathf.Max(a.shake, b.shake),
        };
    }

    public float Duration((float t, ArmPose pose)[] line) => line != null && line.Length > 0 ? line[line.Length - 1].t : 0f;

    public ArmPose Eval((float t, ArmPose pose)[] line, float time)
    {
        if (time <= line[0].t) return line[0].pose;
        for (int i = 1; i < line.Length; i++)
            if (time <= line[i].t) return ArmPose.Lerp(line[i - 1].pose, line[i].pose, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(line[i - 1].t, line[i].t, time)));
        return line[line.Length - 1].pose;
    }
}

// Reusable arm moves. Every spell builds its own cast style from these pieces (with its own reach, speed, angles and orb), so each spell keeps its
// own traits, and a fusion can later take one move from each parent (see CastStyle.Fuse).
public static class Moves
{
    public static Vector3 D(float x, float y, float z) => new Vector3(x, y, z).normalized;

    // the wand arm resting low at the side, and the same pose for both arms
    public static readonly ArmPose Rest = ArmPose.Right(D(0.45f, -0.55f, 0.55f), D(0.1f, 0.3f, 1f));
    public static readonly ArmPose RestBoth = ArmPose.Both(D(0.45f, -0.55f, 0.55f), D(0.1f, 0.3f, 1f));

    // scales a timeline's speed (2 = twice as fast)
    public static (float t, ArmPose pose)[] Fast((float t, ArmPose pose)[] line, float speed)
    {
        var r = new (float, ArmPose)[line.Length];
        for (int i = 0; i < line.Length; i++) r[i] = (line[i].t / speed, line[i].pose);
        return r;
    }

    // one hand pulled in, then thrust forward (side: -1 left .. 1 right shifts the reach, lift raises it)
    public static (float t, ArmPose pose)[] Thrust(float side = 0f, float lift = 0f, float lean = 6f)
    {
        var pull = ArmPose.Right(D(0.5f, -0.2f, 0.4f), D(-0.1f, 0.5f, 0.9f));
        var hit = ArmPose.Right(D(0.2f + side, 0.15f + lift, 1f), D(0.05f + side * 0.5f, 0.1f + lift, 1f), lean);
        return new[] { (0f, Rest), (0.07f, pull), (0.17f, hit), (0.4f, hit), (0.6f, Rest) };
    }

    // the arm swings across the body from one side to the other
    public static (float t, ArmPose pose)[] Sweep(float wide, float lean)
    {
        var a = ArmPose.Right(D(0.9f * wide, 0.05f, 0.1f), D(0.8f * wide, 0.2f, 0.5f), -lean * 0.6f);
        var b = ArmPose.Right(D(-0.55f * wide, 0.05f, 0.85f), D(-0.8f * wide, 0.1f, 0.7f), lean);
        return new[] { (0f, a), (0.06f, a), (0.2f, b), (0.35f, b), (0.55f, a) };
    }

    // the arm held straight out and kept there for the given time
    public static (float t, ArmPose pose)[] Extend(float seconds, float lift = 0f)
    {
        var a = ArmPose.Right(D(0.25f, 0.1f + lift, 1f), D(0.1f, 0.1f + lift, 1f), 3f);
        return new[] { (0f, a), (seconds, a) };
    }

    // both hands together: cocked at the chest, then pushed forward, then back to rest
    public static (float t, ArmPose pose)[] TwoHandPush(float lean)
    {
        return new[]
        {
            (0f, ArmPose.Both(D(0.2f, 0.2f, 1f), D(-0.4f, 0.15f, 1f), 4f)),
            (0.1f, ArmPose.Both(D(0.4f, -0.1f, 0.6f), D(-0.1f, 0.5f, 0.9f), 0f)),
            (0.24f, ArmPose.Both(D(0.15f, 0.15f, 1f), D(0f, 0.1f, 1f), lean)),
            (0.55f, ArmPose.Both(D(0.2f, 0.1f, 1f), D(0.05f, 0.05f, 1f), lean * 0.6f)),
            (0.85f, RestBoth),
        };
    }

    // both hands held forward and drawn in as the chant goes on
    public static ArmPose TwoHandStart => ArmPose.Both(D(0.35f, -0.15f, 0.9f), D(-0.1f, 0.1f, 1f));
    public static ArmPose TwoHandEnd => ArmPose.Both(D(0.2f, 0.2f, 1f), D(-0.4f, 0.15f, 1f), 4f);

    // ---- more moves, used by the rest of the spells ----

    public static readonly ArmPose ArmUp = ArmPose.Right(D(0.3f, 0.95f, -0.2f), D(0.2f, 0.9f, -0.35f), -8f);
    public static readonly ArmPose ArmsUp = ArmPose.Both(D(0.25f, 0.97f, 0.05f), D(0.1f, 0.95f, 0.1f), -6f);

    // the wand arm swings up and back, then throws forward (overhead throw); speed 1 = normal
    public static (float t, ArmPose pose)[] Overhead(float speed, float lean)
    {
        var thrown = ArmPose.Right(D(0.15f, 0.1f, 1f), D(0.05f, 0.05f, 1f), lean);
        return Fast(new[] { (0f, Rest), (0.12f, ArmUp), (0.22f, ArmUp), (0.36f, thrown), (0.6f, thrown), (0.85f, Rest) }, speed);
    }

    // the arm comes up then drives down into the ground (a fist slam)
    public static (float t, ArmPose pose)[] Slam(float lean, bool both)
    {
        ArmPose up = both ? ArmsUp : ArmUp;
        ArmPose down = both ? ArmPose.Both(D(0.3f, -0.85f, 0.45f), D(0.15f, -0.9f, 0.3f), lean) : ArmPose.Right(D(0.3f, -0.85f, 0.45f), D(0.15f, -0.9f, 0.3f), lean);
        return new[] { (0f, Rest), (0.18f, up), (0.3f, up), (0.42f, down), (0.8f, down), (1.05f, Rest) };
    }

    // both arms reach for the sky, hold while the glow gathers, then swing down in front
    public static (float t, ArmPose pose)[] SkyCall(float holdSeconds, float lean)
    {
        var down = ArmPose.Both(D(0.2f, -0.25f, 1f), D(0.1f, -0.3f, 1f), lean);
        return new[] { (0f, RestBoth), (0.2f, ArmsUp), (0.2f + holdSeconds, ArmsUp), (0.34f + holdSeconds, down), (0.7f + holdSeconds, down), (1f + holdSeconds, RestBoth) };
    }

    // one hand pulled back to the shoulder, then snapped forward
    public static (float t, ArmPose pose)[] Snap(float lean)
    {
        var back = ArmPose.Right(D(0.6f, 0.1f, -0.45f), D(0.2f, 0.7f, 0.4f), -6f);
        var fwd = ArmPose.Right(D(0.15f, 0.1f, 1f), D(0.05f, 0.05f, 1f), lean);
        return new[] { (0f, Rest), (0.14f, back), (0.24f, back), (0.3f, fwd), (0.55f, fwd), (0.8f, Rest) };
    }

    // the arm points straight ahead and kicks back a little as the bolt leaves
    public static (float t, ArmPose pose)[] Recoil()
    {
        var out1 = ArmPose.Right(D(0.1f, 0.12f, 1f), D(0.05f, 0.1f, 1f), 3f);
        var kick = ArmPose.Right(D(0.25f, 0.2f, 0.8f), D(0.1f, 0.3f, 0.95f), -3f);
        return new[] { (0f, Rest), (0.1f, out1), (0.3f, out1), (0.36f, kick), (0.5f, out1), (0.8f, Rest) };
    }

    // both arms held forward for as long as the spell is held
    public static (float t, ArmPose pose)[] BothExtend(float seconds, float lift = 0.05f)
    {
        var a = ArmPose.Both(D(0.22f, 0.1f + lift, 1f), D(-0.05f, 0.1f + lift, 1f), 4f);
        return new[] { (0f, a), (seconds, a) };
    }

    // the palm held out flat in front, like a stop sign
    public static (float t, ArmPose pose)[] PalmOut(float seconds)
    {
        var palm = ArmPose.Right(D(0.15f, 0.3f, 1f), D(0.05f, 0.5f, 0.9f), 0f);
        return new[] { (0f, Rest), (0.15f, palm), (0.15f + seconds, palm), (0.35f + seconds, Rest) };
    }

    // both hands sweep round in a circle, building a funnel, then press down
    public static (float t, ArmPose pose)[] Spin(float seconds)
    {
        var line = new System.Collections.Generic.List<(float, ArmPose)> { (0f, RestBoth) };
        const int steps = 8;
        for (int i = 0; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 4f;
            line.Add((0.1f + seconds * i / steps, ArmPose.Both(D(Mathf.Cos(a) * 0.9f, 0.25f, Mathf.Sin(a) * 0.9f + 0.1f), D(Mathf.Cos(a) * 0.9f, 0.1f, Mathf.Sin(a) * 0.9f + 0.1f), 6f * Mathf.Sin(a))));
        }
        line.Add((0.3f + seconds, ArmPose.Both(D(0.3f, -0.8f, 0.5f), D(0.2f, -0.9f, 0.4f), 14f)));
        line.Add((0.7f + seconds, RestBoth));
        return line.ToArray();
    }

    // both arms spread wide, then brought together in a clap that sends the spell out
    public static (float t, ArmPose pose)[] SpreadClap(float spreadSeconds, float lean)
    {
        var wide = ArmPose.Both(D(1f, 0.25f, 0f), D(1f, 0.3f, -0.1f), -10f);
        var clap = ArmPose.Both(D(0.25f, 0.15f, 1f), D(-0.35f, 0.1f, 1f), lean);
        return new[] { (0f, RestBoth), (0.3f, wide), (0.3f + spreadSeconds, wide), (0.42f + spreadSeconds, clap), (0.8f + spreadSeconds, clap), (1.1f + spreadSeconds, RestBoth) };
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

    static Vector3 D(float x, float y, float z) => Moves.D(x, y, z);

    // the rest of the base spells and the fusions: each picks its own moves, so they keep their own traits
    static void BuildMore()
    {
        CastStyle S(Color color, (float t, ArmPose pose)[] tap, (float t, ArmPose pose)[] release = null, ArmPose? start = null, ArmPose? end = null, float orbTap = 0.2f, float orbHold = 0.5f, bool between = false, float shake = 0f)
        {
            var st = new CastStyle { color = color, tap = tap, release = release, orbTap = orbTap, orbHold = orbHold, orbBetweenHands = between, shake = shake };
            if (start.HasValue) { st.hasChant = true; st.chantStart = start.Value; st.chantEnd = end ?? start.Value; }
            return st;
        }
        var C = new System.Func<float, float, float, Color>((r, g, b) => new Color(r, g, b));

        // ---- base spells ----
        table["FireBurst"] = S(C(1f, 0.5f, 0.15f), Moves.Overhead(1.3f, 8f), Moves.Overhead(0.8f, 16f), Moves.ArmUp, ArmPose.Right(D(0.35f, 1f, -0.3f), D(0.25f, 0.95f, -0.45f), -12f), 0.28f, 0.65f);
        table["FrostShard"] = S(C(0.78f, 0.96f, 1f), Moves.Snap(8f), Moves.Snap(14f), ArmPose.Right(D(0.6f, 0.1f, -0.2f), D(0.2f, 0.7f, 0.4f)), ArmPose.Right(D(0.6f, 0.1f, -0.55f), D(0.2f, 0.8f, 0.35f), -8f), 0.2f, 0.55f);
        table["WindBlade"] = S(C(0.7f, 1f, 0.85f), Moves.Fast(Moves.Sweep(1.1f, 12f), 1.4f), Moves.Sweep(1.3f, 16f), ArmPose.Right(D(0.9f, 0.05f, 0.1f), D(0.8f, 0.2f, 0.5f)), ArmPose.Right(D(1f, 0.2f, -0.2f), D(0.9f, 0.4f, 0.2f), -12f), 0.18f, 0.4f);
        table["VenomDart"] = S(C(0.55f, 0.9f, 0.3f), Moves.Fast(Moves.Thrust(0.35f, 0f, 3f), 1.4f), Moves.Thrust(0.4f, 0.05f, 6f), ArmPose.Right(D(0.8f, -0.1f, 0.2f), D(0.5f, 0.3f, 0.6f)), ArmPose.Right(D(0.85f, 0f, 0.05f), D(0.5f, 0.4f, 0.5f)), 0.16f, 0.4f);
        table["LightningLance"] = S(C(0.75f, 0.85f, 1f), Moves.Recoil(), Moves.Recoil(), ArmPose.Right(D(0.1f, 0.12f, 1f), D(0.05f, 0.1f, 1f)), ArmPose.Right(D(0.1f, 0.12f, 1f), D(0.05f, 0.1f, 1f), 4f), 0.2f, 0.45f, false, 3f);
        table["RadiantBeam"] = S(C(1f, 0.9f, 0.5f), Moves.BothExtend(0.3f), null, null, null, 0.35f, 0.5f, true, 1.2f);
        table["EarthSpike"] = S(C(0.8f, 0.6f, 0.35f), Moves.Slam(16f, false), Moves.Slam(22f, true), Moves.ArmUp, ArmPose.Both(D(0.25f, 0.97f, 0.05f), D(0.1f, 0.95f, 0.1f), -6f), 0.16f, 0.5f);
        table["MeteorStrike"] = S(C(1f, 0.4f, 0.15f), Moves.SkyCall(0.1f, 16f), Moves.SkyCall(0.5f, 22f), Moves.ArmsUp, Moves.ArmsUp, 0.3f, 0.9f, true);
        table["ArcaneWard"] = S(C(0.5f, 0.45f, 1f), Moves.PalmOut(0.4f), Moves.PalmOut(0.6f), null, null, 0f, 0f);
        table["ArcaneWard"].handOrb = false;

        // ---- fusions ----
        table["InfernalBolt"] = S(C(0.9f, 0.2f, 0.1f), Moves.TwoHandPush(18f), Moves.TwoHandPush(18f), Moves.TwoHandStart, Moves.TwoHandEnd, 0.4f, 0.75f, true);
        table["CinderStorm"] = S(C(1f, 0.6f, 0.25f), Moves.TwoHandPush(14f), Moves.TwoHandPush(14f), ArmPose.Both(D(0.9f, 0.4f, 0.2f), D(0.8f, 0.6f, 0.2f)), ArmPose.Both(D(0.8f, 0.8f, 0.1f), D(0.6f, 0.9f, 0.1f), -6f), 0.35f, 0.7f, true);
        table["FirestormLance"] = S(C(1f, 0.7f, 0.25f), Moves.Overhead(0.85f, 16f), Moves.Overhead(0.85f, 16f), Moves.ArmUp, ArmPose.Right(D(0.4f, 0.8f, -0.55f), D(0.25f, 0.75f, -0.6f), -14f), 0.35f, 0.7f);
        table["GlacierBolt"] = S(C(0.6f, 0.9f, 1f), Moves.TwoHandPush(18f), Moves.TwoHandPush(18f), Moves.TwoHandStart, Moves.TwoHandEnd, 0.4f, 0.8f, true);
        table["Cyclone"] = S(C(0.8f, 1f, 0.9f), Moves.Spin(0.8f), Moves.Spin(0.8f), null, null, 0.4f, 0.4f, true);
        table["PlagueStorm"] = S(C(0.55f, 0.85f, 0.35f), Moves.TwoHandPush(10f), Moves.TwoHandPush(10f), Moves.TwoHandStart, Moves.TwoHandEnd, 0.4f, 0.8f, true, 1.5f);
        table["BlizzardLance"] = S(C(0.7f, 0.95f, 1f), Moves.BothExtend(0.3f, 0.02f), null, null, null, 0.3f, 0.5f, true, 1f);
        table["SunLance"] = S(C(1f, 0.85f, 0.4f), Moves.SpreadClap(0.1f, 20f), Moves.SpreadClap(0.1f, 20f), null, null, 0.5f, 1f, true);
        table["ThunderMeteor"] = S(C(1f, 0.95f, 0.5f), Moves.SkyCall(0.25f, 20f), Moves.SkyCall(0.25f, 20f), Moves.ArmUp, Moves.ArmUp, 0.3f, 0.7f, false, 4f);
        table["TremorStorm"] = S(C(0.8f, 0.6f, 0.35f), Moves.Slam(26f, true), Moves.Slam(26f, true), null, null, 0.2f, 0.5f);
        table["AbsoluteZero"] = S(C(0.7f, 0.9f, 1f), Moves.SpreadClap(0.5f, 18f), Moves.SpreadClap(0.5f, 18f), null, null, 0.5f, 1f, true);
        table["AbsoluteZero"].tapAir = Moves.SkyCall(0.3f, 10f);           // on the broom: raised arms and a downward sweep instead
    }

    static void Build()
    {
        table = new Dictionary<string, CastStyle>();

        // Arcane Bolt: a hand thrust forward with a violet orb at the palm; held, a slower two-handed charge with the orb swelling between the hands
        table["ArcaneBolt"] = new CastStyle
        {
            color = new Color(0.6f, 0.45f, 1f),
            orbTap = 0.3f, orbHold = 0.6f, orbBetweenHands = true,
            hasChant = true, chantStart = Moves.TwoHandStart, chantEnd = Moves.TwoHandEnd,
            tap = Moves.Thrust(),
            release = Moves.TwoHandPush(12f),
        };

        // Scatter Shot: the hand sweeps sideways in an arc and violet sparks spray out in a fan; held, a wider sweep from further out
        var wideStart = ArmPose.Right(D(1f, 0.3f, -0.25f), D(0.9f, 0.5f, 0.2f), -12f);
        table["ScatterShot"] = new CastStyle
        {
            color = new Color(0.8f, 0.5f, 1f),
            orbTap = 0.18f, orbHold = 0.4f,
            hasChant = true, chantStart = ArmPose.Right(D(0.85f, 0.05f, 0.25f), D(0.7f, 0.2f, 0.6f)), chantEnd = wideStart,
            tap = Moves.Sweep(1f, 8f),
            release = new[] { (0f, wideStart), (0.05f, wideStart), (0.24f, ArmPose.Right(D(-0.9f, 0.1f, 0.6f), D(-1f, 0.1f, 0.4f), 14f)), (0.5f, ArmPose.Right(D(-0.9f, 0.1f, 0.6f), D(-1f, 0.1f, 0.4f), 14f)), (0.8f, Moves.Rest) },
        };

        // Spark Stream: the arm stays extended and the hand shakes slightly, a small spark flickers at the fingertips, no wind-up
        table["SparkStream"] = new CastStyle
        {
            color = new Color(1f, 0.93f, 0.5f),
            orbTap = 0.13f, orbHold = 0.2f, shake = 5f,
            tap = Moves.Extend(0.32f, 0.02f),
        };

        BuildMore();
    }
}

// Aims the arms of the active CastStyle each frame (after the animator) and shows the orb in the hands.
public class CastPoseDriver : MonoBehaviour
{
    Animator animator;
    PlayerSpellCaster caster;
    Transform rUpper, rLower, rHand, lUpper, lLower, lHand, spine;
    ChargeOrb orb;
    BroomFlight broom;

    CastStyle playStyle; (float t, ArmPose pose)[] playLine; float playTime; bool playHold;
    float weight;

    void Awake()
    {
        animator = GetComponent<Animator>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        broom = GetComponent<BroomFlight>();
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
        var line = wasHold ? style.release : (broom != null && broom.IsFlying && style.tapAir != null ? style.tapAir : style.tap);
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
            if (style != null && style.shake > 0f)
            {
                float k = Time.time * 38f;
                Quaternion j = Quaternion.Euler((Mathf.PerlinNoise(k, 0.3f) - 0.5f) * style.shake * 2f, (Mathf.PerlinNoise(0.7f, k) - 0.5f) * style.shake * 2f, 0f);
                t.rUpper = j * t.rUpper; t.rFore = j * t.rFore;
            }
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
