using UnityEngine;

// While she casts at something ahead of the broom, the broom keeps its course and she aims instead:
// the upper body twists toward the aim (spread over the spine, chest and upper chest) and the wand arm points the wand at it.
// Runs after the animation, before WandAim, so the arm is laid on top of the turned body.
// Built at runtime; BroomFlight adds it.
[DefaultExecutionOrder(150)]
public class RiderAim : MonoBehaviour
{
    BroomFlight flight;
    PlayerSpellCaster caster;
    WandAim wandAim;
    Transform[] chain;            // spine up to the upper chest
    float[] share;                // how much of the turn each takes (adds up to 1)
    float maxTwist;
    float weight;

    public void Init(BroomFlight flight, PlayerSpellCaster caster, Animator animator, float maxTwist)
    {
        this.flight = flight;
        this.caster = caster;
        this.maxTwist = maxTwist;
        wandAim = GetComponent<WandAim>();
        if (animator == null || !animator.isHuman) return;

        var bones = new System.Collections.Generic.List<Transform>();
        var shares = new System.Collections.Generic.List<float>();
        Add(animator, HumanBodyBones.Spine, 0.35f, bones, shares);
        Add(animator, HumanBodyBones.Chest, 0.35f, bones, shares);
        Add(animator, HumanBodyBones.UpperChest, 0.30f, bones, shares);

        float total = 0f;
        foreach (float v in shares) total += v;
        for (int i = 0; i < shares.Count; i++) shares[i] /= Mathf.Max(total, 0.0001f);
        chain = bones.ToArray();
        share = shares.ToArray();
    }

    static void Add(Animator animator, HumanBodyBones bone, float amount, System.Collections.Generic.List<Transform> bones, System.Collections.Generic.List<float> shares)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (t == null) return;
        bones.Add(t);
        shares.Add(amount);
    }

    void LateUpdate()
    {
        if (flight == null || caster == null) return;

        bool on = flight.IsFlying && flight.FrontCast;
        weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime * (on ? 8f : 4f));

        // the wand arm keeps the wand on the aim for as long as she is casting ahead (WandAim runs after this)
        if (wandAim != null) wandAim.holdAim = on;

        if (weight <= 0.001f || chain == null || chain.Length == 0) return;

        // turn the upper body from where she faces to the aim, but never past the twist limit
        Quaternion turn = Quaternion.FromToRotation(transform.forward, caster.AimDirection);
        turn.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) > maxTwist) turn = Quaternion.AngleAxis(Mathf.Sign(angle) * maxTwist, axis);

        for (int i = 0; i < chain.Length; i++)
            chain[i].rotation = Quaternion.Slerp(Quaternion.identity, turn, share[i] * weight) * chain[i].rotation;
    }
}
