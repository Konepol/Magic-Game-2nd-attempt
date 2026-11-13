using UnityEngine;

// How she gets on and off the broom, laid on top of the animator's poses (like the cast poses, the bones are turned
// directly after the animator has run). It follows what the broom really does, so it always matches the mechanic:
//  - mounting from her back: she twists round to her left and takes the broom with her left hand, one leg tucks up
//    and the other swings out to let the broom in, then she unwinds as it comes under her, drops onto it, and broom
//    and rider dip and spring back
//  - a called broom: she holds her left hand out for it as it comes in, and it scoops her up (legs kick up, she is
//    thrown back, then pulls herself forward)
//  - stepping off slowly: she pushes up off it and hands it over her shoulder to her back, with a small nudge when it
//    settles there
//  - stepping off fast: the broom shoots on, she lets go reaching after it and falls leaning forward, legs trailing
//  - stepping off at any angle (banked, upside down): she turns upright over a moment instead of snapping
// Nothing is keyed straight onto a bone: every part of the body chases its pose on its own spring, each with its own
// weight, so the parts start and stop at different moments, overshoot a little and settle (the head last).
// Built at runtime, so it needs no assets; BroomFlight adds it.
[DefaultExecutionOrder(50)]   // after the broom has moved and the cast poses are set, before the aim on the broom
public class BroomMountAnim : MonoBehaviour
{
    [Header("Mounting from her back")]
    [SerializeField, Tooltip("Degrees her legs tuck up during the hop.")]
    float legTuck = 50f;
    [SerializeField, Tooltip("Degrees she leans back as she swings the broom round.")]
    float mountLeanBack = 14f;
    [SerializeField, Tooltip("Degrees her upper body turns to her left, toward the broom, as she takes it.")]
    float mountTwist = 32f;
    [SerializeField, Tooltip("Degrees her left leg swings out to let the broom in under her.")]
    float legSwingOut = 26f;
    [SerializeField, Range(0f, 1f), Tooltip("How loose the whole thing is: 0 = follows the poses tightly, 1 = soft, with more overshoot and a longer settle.")]
    float looseness = 0.5f;

    [Header("Settling onto the broom")]
    [SerializeField, Tooltip("How far broom and rider dip when she lands on it, in metres.")]
    float settleDip = 0.16f;
    [SerializeField, Tooltip("Degrees she folds forward as she lands on it.")]
    float settleLean = 14f;
    [SerializeField, Tooltip("How much stronger all of that is when a called broom scoops her up.")]
    float scoopStrength = 1.6f;
    [SerializeField, Tooltip("A called broom closer than this has her holding a hand out for it.")]
    float callReachDistance = 8f;

    [Header("Stepping off")]
    [SerializeField, Tooltip("Seconds she takes to turn upright after stepping off banked or upside down.")]
    float uprightTime = 0.35f;
    [SerializeField, Tooltip("Seconds her hand stays on the broom as it goes to her back.")]
    float handOffTime = 0.35f;
    [SerializeField, Tooltip("Degrees she leans into the fall when the broom flies on without her.")]
    float flingLean = 18f;
    [SerializeField] float flingTime = 0.6f;

    static readonly Color Lavender = new Color(0.72f, 0.62f, 1f, 1f);
    const float Off = 100f;   // a timer this high is not running

    // one value chasing its target on a spring
    struct Spring
    {
        public float v, vel;
        public float Step(float target, float stiffness, float damping, float dt)
        {
            vel += ((target - v) * stiffness - vel * damping) * dt;
            v += vel * dt;
            return v;
        }
    }

    BroomFlight broom;
    BroomBody body;
    PlayerSpellCaster caster;
    Transform hips, head;
    Transform[] spineChain;        // spine up to the upper chest
    float[] spineShare;
    Transform lUpper, lLower, lHand, rUpper, rLower, rHand;
    Transform lThigh, lShin, rThigh, rShin;

    BroomBody.Phase lastState;
    float mountClock = Off, settleTime = Off, scoopTime = Off, offTime = Off, dockTime = Off;
    float settleScale = 1f, callReach;
    bool hopMount, offFast, bobbed;
    Quaternion residual = Quaternion.identity;   // from upright back to how she sat when she stepped off

    // the body, part by part
    Spring lean, twist, roll, hipPitch;          // lean: + forward. twist: + to her right. roll: + toward her right side
    Spring headYaw, headPitch;
    Spring thighL, thighR, kneeL, kneeR, swingL; // thigh: - lifts the knee. knee: + folds the shin back. swing: + out to the side
    Spring reachL, flingR;
    Vector3 flingUpper = new Vector3(0.9f, 0.25f, 0.1f).normalized, flingFore = new Vector3(0.8f, 0.5f, 0.2f).normalized;

    void Awake()
    {
        broom = GetComponent<BroomFlight>();
        body = GetComponent<BroomBody>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        var animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) return;

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        lShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        // a bend is spread over the whole back instead of hinging at one bone
        var bones = new System.Collections.Generic.List<Transform>();
        var shares = new System.Collections.Generic.List<float>();
        AddSpine(animator, HumanBodyBones.Spine, 0.4f, bones, shares);
        AddSpine(animator, HumanBodyBones.Chest, 0.35f, bones, shares);
        AddSpine(animator, HumanBodyBones.UpperChest, 0.25f, bones, shares);
        float total = 0f;
        foreach (float v in shares) total += v;
        for (int i = 0; i < shares.Count; i++) shares[i] /= Mathf.Max(total, 0.0001f);
        spineChain = bones.ToArray();
        spineShare = shares.ToArray();
    }

    static void AddSpine(Animator animator, HumanBodyBones bone, float share, System.Collections.Generic.List<Transform> bones, System.Collections.Generic.List<float> shares)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (t == null) return;
        bones.Add(t);
        shares.Add(share);
    }

    void OnEnable()
    {
        if (broom == null) return;
        broom.Mounted += OnMounted;
        broom.Dismounted += OnDismounted;
    }

    void OnDisable()
    {
        if (broom == null) return;
        broom.Mounted -= OnMounted;
        broom.Dismounted -= OnDismounted;
    }

    void OnMounted(bool broomArrived, bool fromGround)
    {
        offTime = dockTime = Off;
        hopMount = fromGround;
        if (!broomArrived) { mountClock = 0f; return; }   // from her back, the landing comes when the broom has swung in under her

        scoopTime = settleTime = 0f;
        settleScale = scoopStrength;
    }

    void OnDismounted(Quaternion sittingRotation, bool broomFliesOn)
    {
        residual = sittingRotation * Quaternion.Inverse(transform.rotation);
        offTime = 0f;
        offFast = broomFliesOn;
        mountClock = settleTime = scoopTime = Off;
        if (body != null && body.Model != null) MagicFX.Pop(body.Model.position, 0.5f, Lavender, 4);
    }

    void LateUpdate()
    {
        if (broom == null || body == null || body.Model == null || hips == null) return;

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);   // the springs stay calm through a hitch
        BroomBody.Phase state = body.State;
        if (state != lastState)
        {
            // the broom has swung in under her: she lands on it
            if (state == BroomBody.Phase.Ridden && lastState == BroomBody.Phase.Mounting) { settleTime = 0f; settleScale = 1f; }
            // the broom has settled on her back
            if (state == BroomBody.Phase.Stowed && lastState == BroomBody.Phase.Docking)
            {
                dockTime = 0f;
                MagicFX.Pop(body.Model.position, 0.35f, Lavender, 3);
            }
            lastState = state;
        }
        mountClock += dt; settleTime += dt; scoopTime += dt; offTime += dt; dockTime += dt;

        bool flying = broom.IsFlying;
        // her hand goes for the handle, a little ahead of the broom's middle
        Vector3 grip = body.Model.position + body.Model.forward * 0.2f;

        // what each part of the body is asked to do this frame; the springs below take them there
        float wLean = 0f, wTwist = 0f, wRoll = 0f, wHip = 0f, wHeadYaw = 0f;
        float wThighL = 0f, wThighR = 0f, wKneeL = 0f, wKneeR = 0f, wSwingL = 0f;
        float wReach = 0f, wFling = 0f;

        // ---------- stepping off banked or upside down: turn upright over a moment ----------
        if (!flying && offTime < uprightTime)
        {
            float w = 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(uprightTime, 0.01f));
            Quaternion q = Quaternion.Slerp(Quaternion.identity, residual, w);
            hips.SetPositionAndRotation(transform.position + q * (hips.position - transform.position), q * hips.rotation);
        }

        // ---------- mounting from her back ----------
        // Three beats, timed against the broom's own swing (u = 1 is the moment it is under her):
        //   take it   she twists left and leans back toward the broom, right knee comes up, left leg swings out
        //   swing     she unwinds with it, leaning away from the side it comes in on, both legs tucked
        //   sit       legs come down around it, she tips forward
        float u = mountClock / body.MountTime;
        if (flying && u < 1.15f)
        {
            float hop = hopMount ? 1f : 0.6f;         // already in the air, the animator's riding pose does most of the legs
            if (u < 0.4f)
            {
                wTwist = -mountTwist; wHeadYaw = -mountTwist * 0.7f;
                wLean = -mountLeanBack; wHip = -6f;
                wThighR = -legTuck * hop; wKneeR = legTuck * 1.3f * hop;
                wThighL = 10f; wKneeL = 25f * hop; wSwingL = legSwingOut;
                wReach = 1f;
            }
            else if (u < 0.85f)
            {
                wTwist = mountTwist * 0.3f; wHeadYaw = mountTwist * 0.15f;
                wLean = -mountLeanBack * 0.3f; wRoll = 9f; wHip = 4f;
                wThighR = -legTuck * 0.8f * hop; wKneeR = legTuck * hop;
                wThighL = -legTuck * hop; wKneeL = legTuck * 1.3f * hop; wSwingL = legSwingOut * 0.5f;
                wReach = 1f;
            }
            else
            {
                wLean = 7f; wHip = 3f;
                wThighL = wThighR = -legTuck * 0.25f * hop;
            }
            wFling = u < 0.85f ? 0.8f : 0f;
        }

        // ---------- landing on the broom: a dip and a spring back ----------
        // (the dip is not on a spring: broom and rider must move as one)
        float bob = 0f;
        if (flying && settleTime < 1.2f)
        {
            float s = 2f * Mathf.Sin(settleTime * 9f) * Mathf.Exp(-settleTime * 4.5f) * settleScale;   // about 1 at its deepest
            bob = -settleDip * s;
            wLean += settleLean * s;
            wKneeL += 12f * s; wKneeR += 12f * s;
            wRoll += 4f * Mathf.Sin(settleTime * 6f) * Mathf.Exp(-settleTime * 4f) * settleScale;
        }

        // ---------- a called broom scoops her up ----------
        if (flying && scoopTime < 0.6f)
        {
            float k = Mathf.Exp(-scoopTime * 7f);
            wLean -= 18f * k * Mathf.Cos(scoopTime * 9f);
            wThighL -= 26f * k; wThighR -= 18f * k;
            wFling = Mathf.Max(wFling, k);
            flingUpper = new Vector3(0.7f, 0.7f, -0.2f).normalized;
            flingFore = new Vector3(0.5f, 0.85f, -0.1f).normalized;
        }
        else if (flying)
        {
            flingUpper = new Vector3(0.9f, 0.25f, 0.1f).normalized;
            flingFore = new Vector3(0.8f, 0.5f, 0.2f).normalized;
        }

        // waiting for a called broom: a hand held out for it as it comes close, and a look round at it
        float wantReach = 0f;
        if (!flying && body.CallQueued)
            wantReach = 1f - Mathf.Clamp01((Vector3.Distance(grip, hips.position) - 1.5f) / Mathf.Max(callReachDistance, 0.01f));
        callReach = Mathf.MoveTowards(callReach, wantReach, 5f * dt);
        wReach = Mathf.Max(wReach, callReach);
        if (callReach > 0.01f)
        {
            float side = Mathf.Clamp(Vector3.Dot(transform.right, (grip - hips.position).normalized), -1f, 1f);
            wHeadYaw += 35f * side * callReach;
            wTwist += 14f * side * callReach;
        }

        // ---------- stepping off ----------
        if (!flying && !offFast && offTime < handOffTime)
        {
            // slowly: a push up off the broom, and her hand goes with it toward her back
            float t = Mathf.Clamp01(offTime / Mathf.Max(handOffTime, 0.01f));
            if (Vector3.Distance(grip, hips.position) < 2.5f) wReach = Mathf.Max(wReach, 1f - Mathf.SmoothStep(0f, 1f, t));
            wLean -= 9f * Mathf.Sin(t * Mathf.PI);
            wTwist -= 12f * Mathf.Sin(t * Mathf.PI);
        }
        if (!flying && offFast && offTime < flingTime)
        {
            // fast: the broom is gone from under her, she reaches after it and falls forward with her legs trailing
            float k = 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(flingTime, 0.01f));
            wLean += flingLean * k;
            wThighL += 28f * k; wThighR += 16f * k;
            wKneeL += 30f * k; wKneeR += 42f * k;
            wReach = Mathf.Max(wReach, 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(flingTime * 0.6f, 0.01f)));
            wFling = Mathf.Max(wFling, k * 0.6f);
            flingUpper = new Vector3(0.6f, 0.6f, 0.4f).normalized;
            flingFore = new Vector3(0.4f, 0.8f, 0.4f).normalized;
        }

        // the broom settles on her back: a small nudge forward
        if (dockTime < 0.3f) wLean += 6f * Mathf.Sin(dockTime / 0.3f * Mathf.PI);

        // ---------- the springs: heavy parts slow, light parts quick, the head last ----------
        float soft = Mathf.Lerp(1.6f, 0.55f, looseness);          // stiffness
        float ring = Mathf.Lerp(1.25f, 0.75f, looseness);         // damping: lower rings on longer
        float bodyK = 170f * soft, bodyD = 15f * ring;
        float legK = 230f * soft, legD = 17f * ring;
        float headK = 85f * soft, headD = 10f * ring;

        lean.Step(wLean, bodyK, bodyD, dt);
        twist.Step(wTwist, bodyK * 0.85f, bodyD, dt);
        roll.Step(wRoll, bodyK, bodyD, dt);
        hipPitch.Step(wHip, bodyK * 0.7f, bodyD, dt);
        headYaw.Step(wHeadYaw, headK * 1.6f, headD * 1.3f, dt);       // the look leads the turn
        headPitch.Step(-lean.v * 0.55f, headK, headD, dt);            // and the head stays level, catching up late
        thighR.Step(wThighR, legK, legD, dt);
        kneeR.Step(wKneeR, legK, legD, dt);
        thighL.Step(wThighL, legK * 0.75f, legD * 0.9f, dt);          // the swinging leg trails the tucked one
        kneeL.Step(wKneeL, legK * 0.75f, legD * 0.9f, dt);
        swingL.Step(wSwingL, legK * 0.75f, legD, dt);
        reachL.Step(wReach, 200f, 24f, dt);
        flingR.Step(wFling, 120f, 17f, dt);

        // ---------- lay it on the bones ----------
        Vector3 right = transform.right, up = transform.up, fwd = transform.forward;

        // broom and rider dip together
        if (state == BroomBody.Phase.Ridden && (bob != 0f || bobbed))
        {
            body.Model.localPosition = body.SeatPosition + Vector3.up * bob;
            hips.position += up * bob;
            bobbed = bob != 0f;
        }

        // the whole body tips from the hips
        if (Mathf.Abs(hipPitch.v) > 0.01f) hips.rotation = Quaternion.AngleAxis(hipPitch.v, right) * hips.rotation;

        // the back bends, turns and leans as one curve
        if (spineChain != null && (Mathf.Abs(lean.v) > 0.01f || Mathf.Abs(twist.v) > 0.01f || Mathf.Abs(roll.v) > 0.01f))
        {
            for (int i = 0; i < spineChain.Length; i++)
            {
                float sh = spineShare[i];
                Quaternion q = Quaternion.AngleAxis(twist.v * sh, up) * Quaternion.AngleAxis(lean.v * sh, right) * Quaternion.AngleAxis(-roll.v * sh, fwd);
                spineChain[i].rotation = q * spineChain[i].rotation;
            }
        }
        if (head != null && (Mathf.Abs(headYaw.v) > 0.01f || Mathf.Abs(headPitch.v) > 0.01f))
            head.rotation = Quaternion.AngleAxis(headYaw.v, up) * Quaternion.AngleAxis(headPitch.v, right) * head.rotation;

        Leg(lThigh, lShin, thighL.v, kneeL.v, -swingL.v, right, fwd);
        Leg(rThigh, rShin, thighR.v, kneeR.v, 0f, right, fwd);

        // the arms stay with a spell that is being cast
        bool chanting = caster != null && caster.IsChanting;
        bool casting = chanting || (caster != null && caster.IsCasting);
        if (reachL.v > 0.01f && !chanting) Reach(lUpper, lLower, lHand, grip, -right * 0.6f - up * 0.7f - fwd * 0.3f, reachL.v);
        if (flingR.v > 0.01f && !casting) Aim(rUpper, rLower, rHand, flingUpper, flingFore, flingR.v);
    }

    static void Leg(Transform upper, Transform lower, float thigh, float knee, float side, Vector3 right, Vector3 fwd)
    {
        if (upper == null || lower == null) return;
        if (Mathf.Abs(thigh) < 0.01f && Mathf.Abs(knee) < 0.01f && Mathf.Abs(side) < 0.01f) return;
        upper.rotation = Quaternion.AngleAxis(side, fwd) * Quaternion.AngleAxis(thigh, right) * upper.rotation;
        lower.rotation = Quaternion.AngleAxis(knee, right) * lower.rotation;
    }

    // the hand goes to a point in the world with the elbow bent the way an arm bends: the elbow is placed off the line
    // from shoulder to hand, toward `elbowSide`, as far as the lengths of the arm ask for
    void Reach(Transform upper, Transform lower, Transform hand, Vector3 point, Vector3 elbowSide, float weight)
    {
        if (upper == null || lower == null || hand == null) return;
        Vector3 shoulder = upper.position;
        float a = Vector3.Distance(shoulder, lower.position), b = Vector3.Distance(lower.position, hand.position);
        Vector3 to = point - shoulder;
        if (to.sqrMagnitude < 0.0001f || a < 0.001f || b < 0.001f) return;

        Vector3 dir = to.normalized;
        float d = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 0.02f, (a + b) * 0.96f);   // never quite straight: a locked arm looks like a stick
        float along = (a * a - b * b + d * d) / (2f * d);
        float out_ = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        Vector3 side = Vector3.ProjectOnPlane(elbowSide, dir);
        side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.ProjectOnPlane(-transform.up, dir).normalized;
        Vector3 elbow = shoulder + dir * along + side * out_;

        Turn(upper, lower.position - shoulder, elbow - shoulder, weight);
        Turn(lower, hand.position - lower.position, shoulder + dir * d - elbow, weight);
    }

    // the arm points along directions given in her own space (x right, y up, z forward)
    void Aim(Transform upper, Transform lower, Transform hand, Vector3 upperDir, Vector3 foreDir, float weight)
    {
        if (upper == null || lower == null || hand == null) return;
        Turn(upper, lower.position - upper.position, transform.TransformDirection(upperDir), weight);
        Turn(lower, hand.position - lower.position, transform.TransformDirection(foreDir), weight);
    }

    static void Turn(Transform bone, Vector3 now, Vector3 want, float weight)
    {
        if (now.sqrMagnitude < 0.000001f || want.sqrMagnitude < 0.000001f) return;
        bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), Mathf.Clamp01(weight)) * bone.rotation;
    }
}
