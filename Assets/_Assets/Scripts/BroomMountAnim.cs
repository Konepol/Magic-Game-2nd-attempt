using UnityEngine;

// How she gets on and off the broom, laid on top of the animator's poses (like the cast poses, the bones are turned
// directly after the animator has run). It follows what the broom really does, so it always matches the mechanic:
//  - mounting from her back: her left hand takes the broom and carries it round under her while she tucks her legs
//    up in the hop, her wand arm out for balance; then she drops onto it and broom and rider dip and spring back
//  - a called broom: she holds her left hand out for it as it comes in, and it scoops her up (legs kick up, she is
//    thrown back, then pulls herself forward)
//  - stepping off slowly: she pushes up off it and hands it over her shoulder to her back, with a small nudge when it
//    settles there
//  - stepping off fast: the broom shoots on, she lets go reaching after it and falls leaning forward, legs trailing
//  - stepping off at any angle (banked, upside down): she turns upright over a moment instead of snapping
// Built at runtime, so it needs no assets; BroomFlight adds it.
[DefaultExecutionOrder(50)]   // after the broom has moved and the cast poses are set, before the aim on the broom
public class BroomMountAnim : MonoBehaviour
{
    [Header("Mounting from her back")]
    [SerializeField, Tooltip("Degrees her legs tuck up during the hop.")]
    float legTuck = 35f;
    [SerializeField, Tooltip("Degrees she leans back as she swings the broom round.")]
    float mountLeanBack = 10f;

    [Header("Settling onto the broom")]
    [SerializeField, Tooltip("How far broom and rider dip when she lands on it, in metres.")]
    float settleDip = 0.14f;
    [SerializeField, Tooltip("Degrees she folds forward as she lands on it.")]
    float settleLean = 12f;
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

    BroomFlight broom;
    BroomBody body;
    PlayerSpellCaster caster;
    Transform hips, spine;
    Transform lUpper, lLower, lHand, rUpper, rLower, rHand;
    Transform lThigh, lShin, rThigh, rShin;

    BroomBody.Phase lastState;
    float settleTime = Off, scoopTime = Off, offTime = Off, dockTime = Off;
    float settleScale = 1f, callReach;
    bool hopMount, offFast, bobbed;
    Quaternion residual = Quaternion.identity;   // from upright back to how she sat when she stepped off

    void Awake()
    {
        broom = GetComponent<BroomFlight>();
        body = GetComponent<BroomBody>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        var animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) return;

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
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
        if (!broomArrived) return;   // from her back, the landing comes when the broom has swung in under her

        scoopTime = settleTime = 0f;
        settleScale = scoopStrength;
    }

    void OnDismounted(Quaternion sittingRotation, bool broomFliesOn)
    {
        residual = sittingRotation * Quaternion.Inverse(transform.rotation);
        offTime = 0f;
        offFast = broomFliesOn;
        settleTime = scoopTime = Off;
        if (body != null && body.Model != null) MagicFX.Pop(body.Model.position, 0.5f, Lavender, 4);
    }

    void LateUpdate()
    {
        if (broom == null || body == null || body.Model == null || hips == null) return;

        float dt = Time.deltaTime;
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
        settleTime += dt; scoopTime += dt; offTime += dt; dockTime += dt;

        bool flying = broom.IsFlying;
        Vector3 broomPos = body.Model.position;
        float lean = 0f, thigh = 0f, knee = 0f;      // lean: + forward. thigh: - lifts the knee. knee: + folds the shin back
        float leftReach = 0f, rightFling = 0f;
        Vector3 flingUpper = new Vector3(0.9f, 0.25f, 0.1f).normalized, flingFore = new Vector3(0.8f, 0.5f, 0.2f).normalized;

        // ---------- stepping off banked or upside down: turn upright over a moment ----------
        if (!flying && offTime < uprightTime)
        {
            float w = 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(uprightTime, 0.01f));
            Quaternion q = Quaternion.Slerp(Quaternion.identity, residual, w);
            hips.SetPositionAndRotation(transform.position + q * (hips.position - transform.position), q * hips.rotation);
        }

        // ---------- mounting from her back ----------
        if (state == BroomBody.Phase.Mounting)
        {
            float p = body.MountProgress;
            float arc = Mathf.Sin(p * Mathf.PI);
            float hop = hopMount ? 1f : 0.5f;         // already in the air, the animator's riding pose does most of it
            leftReach = Mathf.SmoothStep(0f, 1f, p / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.3f));
            thigh -= legTuck * arc * hop;
            knee += legTuck * 1.3f * arc * hop;
            lean -= mountLeanBack * Mathf.Sin(Mathf.Clamp01(p / 0.6f) * Mathf.PI);
            rightFling = arc * 0.7f;
        }

        // ---------- landing on the broom: a dip and a spring back ----------
        float bob = 0f;
        if (flying && settleTime < 0.8f)
        {
            float s = 2f * Mathf.Sin(settleTime * 12f) * Mathf.Exp(-settleTime * 6f) * settleScale;   // about 1 at its deepest
            bob = -settleDip * s;
            lean += settleLean * s;
            knee += 10f * s;
        }

        // ---------- a called broom scoops her up ----------
        if (flying && scoopTime < 0.6f)
        {
            float k = Mathf.Exp(-scoopTime * 7f);
            lean -= 16f * k * Mathf.Cos(scoopTime * 9f);
            thigh -= 22f * k;
            rightFling = Mathf.Max(rightFling, k);
            flingUpper = new Vector3(0.7f, 0.7f, -0.2f).normalized;
            flingFore = new Vector3(0.5f, 0.85f, -0.1f).normalized;
        }

        // waiting for a called broom: a hand held out for it as it comes close
        float wantReach = 0f;
        if (!flying && body.CallQueued)
            wantReach = 1f - Mathf.Clamp01((Vector3.Distance(broomPos, hips.position) - 1.5f) / Mathf.Max(callReachDistance, 0.01f));
        callReach = Mathf.MoveTowards(callReach, wantReach, 5f * dt);
        leftReach = Mathf.Max(leftReach, callReach);

        // ---------- stepping off ----------
        if (!flying && !offFast && offTime < handOffTime)
        {
            // slowly: a push up off the broom, and her hand goes with it toward her back
            if (Vector3.Distance(broomPos, hips.position) < 2.5f)
                leftReach = Mathf.Max(leftReach, 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(handOffTime, 0.01f)));
            lean -= 8f * Mathf.Sin(Mathf.Clamp01(offTime / Mathf.Max(handOffTime, 0.01f)) * Mathf.PI);
        }
        if (!flying && offFast && offTime < flingTime)
        {
            // fast: the broom is gone from under her, she reaches after it and falls forward with her legs trailing
            float k = 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(flingTime, 0.01f));
            lean += flingLean * k;
            thigh += 22f * k;
            knee += 35f * k;
            leftReach = Mathf.Max(leftReach, 1f - Mathf.SmoothStep(0f, 1f, offTime / Mathf.Max(flingTime * 0.6f, 0.01f)));
            rightFling = Mathf.Max(rightFling, k * 0.6f);
            flingUpper = new Vector3(0.6f, 0.6f, 0.4f).normalized;
            flingFore = new Vector3(0.4f, 0.8f, 0.4f).normalized;
        }

        // the broom settles on her back: a small nudge forward
        if (dockTime < 0.3f) lean += 5f * Mathf.Sin(dockTime / 0.3f * Mathf.PI);

        // ---------- lay it on the bones ----------
        // broom and rider dip together
        if (state == BroomBody.Phase.Ridden && (bob != 0f || bobbed))
        {
            body.Model.localPosition = body.SeatPosition + Vector3.up * bob;
            hips.position += transform.up * bob;
            bobbed = bob != 0f;
        }

        Vector3 right = transform.right;
        if (spine != null && Mathf.Abs(lean) > 0.01f) spine.rotation = Quaternion.AngleAxis(lean, right) * spine.rotation;
        if (Mathf.Abs(thigh) > 0.01f || Mathf.Abs(knee) > 0.01f)
        {
            Leg(lThigh, lShin, thigh, knee, right);
            Leg(rThigh, rShin, thigh, knee, right);
        }

        // the arms stay with a spell that is being cast
        bool chanting = caster != null && caster.IsChanting;
        bool casting = chanting || (caster != null && caster.IsCasting);
        if (leftReach > 0.01f && !chanting) Reach(lUpper, lLower, lHand, broomPos, leftReach);
        if (rightFling > 0.01f && !casting) Aim(rUpper, rLower, rHand, flingUpper, flingFore, rightFling);
    }

    static void Leg(Transform upper, Transform lower, float thigh, float knee, Vector3 right)
    {
        if (upper == null || lower == null) return;
        upper.rotation = Quaternion.AngleAxis(thigh, right) * upper.rotation;
        lower.rotation = Quaternion.AngleAxis(knee, right) * lower.rotation;
    }

    // the hand goes to a point in the world: the upper arm toward it with the elbow dropped a little, then the forearm onto it
    void Reach(Transform upper, Transform lower, Transform hand, Vector3 point, float weight)
    {
        if (upper == null || lower == null || hand == null) return;
        Vector3 to = point - upper.position;
        if (to.sqrMagnitude < 0.0001f) return;
        Vector3 dir = to.normalized;
        Turn(upper, lower.position - upper.position, (dir - transform.up * 0.3f).normalized, weight);

        Vector3 foreTo = point - lower.position;
        Turn(lower, hand.position - lower.position, foreTo.sqrMagnitude > 0.01f ? foreTo.normalized : dir, weight);
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
        if (now.sqrMagnitude < 0.000001f) return;
        bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), Mathf.Clamp01(weight)) * bone.rotation;
    }
}
