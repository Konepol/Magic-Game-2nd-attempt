using UnityEngine;

// Turns the wand hand toward where a tap spell is being aimed: the arm is held out by the animation, and the wrist and forearm swing the wand
// toward the aim direction for a moment after the cast. Runs after the animation each frame.
public class WandAim : MonoBehaviour
{
    public float holdTime = 0.45f;
    public float maxAngle = 80f;

    Animator animator;
    PlayerSpellCaster caster;
    Transform hand, lowerArm, wand;
    float until, weight;

    void Awake()
    {
        animator = GetComponent<Animator>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        if (hand != null) wand = hand.Find("Wand");
    }

    void OnEnable() { if (caster != null) caster.Casted += OnCasted; }
    void OnDisable() { if (caster != null) caster.Casted -= OnCasted; }

    void OnCasted(bool wasHold)
    {
        if (!wasHold) until = Time.time + holdTime;
    }

    void LateUpdate()
    {
        if (caster == null || hand == null || wand == null) return;

        float target = Time.time < until ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, target, Time.deltaTime * (target > weight ? 14f : 4f));
        if (weight <= 0.001f) return;

        Vector3 desired = caster.AimDirection;

        // the forearm takes a part of the turn, the hand the rest
        Vector3 now = wand.forward;
        Quaternion turn = Quaternion.FromToRotation(now, desired);
        turn.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > maxAngle) turn = Quaternion.AngleAxis(maxAngle, axis);
        if (lowerArm != null) lowerArm.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight * 0.35f) * lowerArm.rotation;

        Quaternion rest = Quaternion.FromToRotation(wand.forward, desired);
        rest.ToAngleAxis(out angle, out axis);
        if (angle > maxAngle) rest = Quaternion.AngleAxis(maxAngle, axis);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, rest, weight) * hand.rotation;
    }
}

// While she dashes on the broom: lets the animation know (it stretches her out along the broom) and leans her spine into the dash,
// forward for a forward dash and sideways for a sideways one.
public class BroomDashLean : MonoBehaviour
{
    static readonly int Dashing = Animator.StringToHash("Dashing");

    public float maxLean = 38f;
    public float holdAfter = 0.35f;

    Animator animator;
    BroomFlight broom;
    Transform spine;
    float until, weight;
    Vector3 dir;

    void Awake()
    {
        animator = GetComponent<Animator>();
        broom = GetComponent<BroomFlight>();
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    void Update()
    {
        if (broom == null || animator == null) return;

        if (broom.IsDashing)
        {
            until = Time.time + holdAfter;
            dir = broom.DashDirection;
        }
        animator.SetBool(Dashing, Time.time < until);
    }

    void LateUpdate()
    {
        if (spine == null) return;

        float target = Time.time < until ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, target, Time.deltaTime * (target > weight ? 14f : 5f));
        if (weight <= 0.001f) return;

        float forward = Vector3.Dot(dir, transform.forward);
        float side = Vector3.Dot(dir, transform.right);
        Quaternion lean = Quaternion.AngleAxis(forward * maxLean * weight, transform.right) * Quaternion.AngleAxis(-side * maxLean * weight, transform.forward);
        spine.rotation = lean * spine.rotation;
    }
}
