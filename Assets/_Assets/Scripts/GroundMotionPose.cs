using UnityEngine;

// A placeholder pose layer for the new ground movement: it leans the witch's spine into her run and her turns, lunges on a magic dash,
// leans back on a slide, hunches on landing and reaches up while climbing. It works on the spine bone after the animator, like the broom's
// dash lean, so it does not depend on any clip. When the full-anime locomotion set arrives (clips for the Dashing, Sliding, Climbing and
// AirJump animator parameters the controller already sets), lower the strengths here or remove this component.
[DefaultExecutionOrder(150)]
public class GroundMotionPose : MonoBehaviour
{
    [Header("Strength (degrees)")]
    public float runLean = 12f;
    public float turnRoll = 12f;
    public float dashLean = 32f;
    public float slideLean = -34f;
    public float climbLean = 10f;
    public float landingDip = 18f;

    StarterAssets.PlayerThirdPersonController controller;
    Animator animator;
    Transform spine;
    Vector3 prevVel;
    bool wasGrounded = true;
    float lastFallSpeed;
    float landingTime;
    float forward, roll;
    float forwardVel, rollVel;

    void Awake()
    {
        controller = GetComponent<StarterAssets.PlayerThirdPersonController>();
        animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    void LateUpdate()
    {
        if (controller == null || spine == null || !controller.IsNewMotion) return;
        if (controller.IsFlying) { prevVel = Vector3.zero; forward = roll = 0f; return; }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 vel = controller.GroundVelocity;
        Vector3 accel = (vel - prevVel) / dt;
        prevVel = vel;

        // landing: remember how hard she came down and dip for a moment
        if (!controller.Grounded) lastFallSpeed = Mathf.Min(lastFallSpeed, controller.VerticalVelocity);
        if (controller.Grounded && !wasGrounded)
        {
            if (lastFallSpeed < -6f) landingTime = Time.time + 0.22f;
            lastFallSpeed = 0f;
        }
        wasGrounded = controller.Grounded;

        float speed01 = Mathf.Clamp01(vel.magnitude / Mathf.Max(controller.SprintRunSpeed, 0.01f));
        float targetForward = speed01 * runLean + Mathf.Clamp(Vector3.Dot(accel, transform.forward) * 0.04f, -6f, 8f);
        float targetRoll = Mathf.Clamp(-Vector3.Dot(accel, transform.right) * 0.05f, -turnRoll, turnRoll) * speed01;

        if (controller.IsMagicDashing) targetForward = dashLean;
        else if (controller.IsSliding) targetForward = slideLean;
        else if (controller.IsClimbing) targetForward = climbLean;
        if (Time.time < landingTime) targetForward += landingDip * ((landingTime - Time.time) / 0.22f);

        forward = Mathf.SmoothDamp(forward, targetForward, ref forwardVel, 0.07f);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVel, 0.09f);

        Quaternion lean = Quaternion.AngleAxis(forward, transform.right) * Quaternion.AngleAxis(roll, transform.forward);
        spine.rotation = lean * spine.rotation;
    }
}
