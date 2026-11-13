using System;
using UnityEngine;

// Tuning for the broom as a companion; lives on BroomFlight so it can be edited in the Inspector.
[Serializable]
public class BroomBodySettings
{
    [Header("Carried on the back")]
    [Tooltip("Where the broom's middle hangs, in the player's own space.")]
    public Vector3 stowPosition = new Vector3(0.05f, 1.1f, -0.3f);
    [Tooltip("Degrees the carried broom leans off vertical (bristles low on the left, star by the right shoulder).")]
    public float stowTilt = 30f;
    [Tooltip("Size of the carried broom, as a share of its riding size.")]
    public float stowScale = 0.7f;

    [Header("Mounting")]
    [Tooltip("Seconds the broom takes to swing from the back to under her.")]
    public float mountTime = 0.45f;
    [Tooltip("Little hop up when mounting from the ground.")]
    public float mountHop = 4f;

    [Header("Leaving the rider")]
    [Tooltip("Flying faster than this when she steps off, the broom flies on without her.")]
    public float leaveSpeed = 18f;
    [Tooltip("How quickly a broom that flew on loses its speed, per second.")]
    public float freeDrag = 0.9f;
    [Tooltip("Longest the broom keeps flying on before it turns back.")]
    public float freeTime = 0.8f;

    [Header("Coming back")]
    [Tooltip("Speed the broom homes in at when it is far away, on top of her own speed. Keep it above her flying speed so it always catches up.")]
    public float returnMinSpeed = 60f;
    public float returnMaxSpeed = 100f;
    [Tooltip("Return speed per metre of distance once it is far enough away, on top of the rider's own speed.")]
    public float returnGain = 4f;
    [Tooltip("How hard the broom swings round toward her (m/s per second).")]
    public float returnAccel = 160f;
    [Tooltip("The broom is always back within this many seconds of turning round: it speeds up as needed, and at the limit it is simply there.")]
    public float maxReturnTime = 1.2f;
    public float arriveDistance = 0.6f;
    public float dockTime = 0.25f;
}

// The broom as a thing of its own instead of a prop that appears when she flies:
//   Stowed   carried slung across the back
//   Mounting swings out from the back and in under her
//   Ridden   under her while she flies
//   Free     she stepped off while fast: the broom flies on with its speed
//   Returning heads back to her (to her back, or to her seat if she called it)
//   Docking  settles back onto the back
// Built at runtime; BroomFlight creates it and drives the transitions.
public class BroomBody : MonoBehaviour
{
    public enum Phase { Stowed, Mounting, Ridden, Free, Returning, Docking }

    public Phase State { get; private set; } = Phase.Stowed;
    public Vector3 Velocity => velocity;
    public bool IsAway => State == Phase.Free || State == Phase.Returning;
    public bool CallQueued { get; private set; }

    // she called the broom and it has reached her: the velocity it arrived with, so the flight carries on from it
    public event Action<Vector3> ReachedRider;

    static readonly Color Lavender = new Color(0.72f, 0.62f, 1f, 1f);

    BroomBodySettings s;
    Transform model, root, socket;
    Vector3 seatPos, seatScale, stowPos, stowScale;           // positions: seat local to the player, stow local to the socket
    Quaternion seatRot, stowRot;
    Vector3 velocity, startPos, startScale;
    Quaternion startRot;
    float timer, returnTimer;
    Func<Vector3> riderVelocity;

    public void Init(Transform player, Transform broomModel, Animator animator, BroomBodySettings settings, Func<Vector3> riderVelocity)
    {
        root = player;
        model = broomModel;
        s = settings;
        this.riderVelocity = riderVelocity;

        // the riding pose is wherever the model was placed under the player in the scene
        seatPos = root.InverseTransformPoint(model.position);
        seatRot = Quaternion.Inverse(root.rotation) * model.rotation;
        seatScale = model.localScale;

        // the carried pose is defined in the player's space, then handed to a spine bone so it follows the body's animation
        socket = root;
        if (animator != null && animator.isHuman)
        {
            socket = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (socket == null) socket = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (socket == null) socket = root;
        }
        Quaternion rootRot = Quaternion.Euler(0f, 0f, -s.stowTilt) * Quaternion.Euler(-90f, 0f, 0f);   // long axis up, leaning to the right
        stowPos = socket.InverseTransformPoint(root.TransformPoint(s.stowPosition));
        stowRot = Quaternion.Inverse(socket.rotation) * (root.rotation * rootRot);
        Vector3 worldScale = seatScale * s.stowScale;
        Vector3 lossy = socket.lossyScale;
        stowScale = new Vector3(worldScale.x / Mathf.Max(lossy.x, 0.0001f), worldScale.y / Mathf.Max(lossy.y, 0.0001f), worldScale.z / Mathf.Max(lossy.z, 0.0001f));

        model.gameObject.SetActive(true);
        model.SetParent(socket, false);
        model.localPosition = stowPos;
        model.localRotation = stowRot;
        model.localScale = stowScale;
        State = Phase.Stowed;
    }

    void OnDestroy()
    {
        // a broom that is off on its own would otherwise be left behind in the scene
        if (model != null && model.parent == null) Destroy(model.gameObject);
    }

    // ---------- transitions, called by BroomFlight ----------

    public void BeginMount()
    {
        CallQueued = false;
        model.SetParent(root, true);   // from wherever it is, the blend then runs in the player's space
        startPos = model.localPosition;
        startRot = model.localRotation;
        startScale = model.localScale;
        timer = 0f;
        State = Phase.Mounting;
        MagicFX.Pop(model.position, 0.5f, Lavender, 4);
    }

    // straight under her, at once (the broom arrived while she was waiting for it)
    public void SeatNow()
    {
        CallQueued = false;
        model.SetParent(root, true);
        model.localPosition = seatPos;
        model.localRotation = seatRot;
        model.localScale = seatScale;
        State = Phase.Ridden;
        MagicFX.Pop(model.position, 0.6f, Lavender, 5);
    }

    // she stepped off fast: the broom keeps its speed
    public void Release(Vector3 speed)
    {
        model.SetParent(null, true);
        velocity = speed;
        timer = 0f;
        State = Phase.Free;
    }

    // she stepped off slowly: the broom goes straight back to her back
    public void SendBack()
    {
        model.SetParent(null, true);
        velocity = Vector3.zero;
        returnTimer = 0f;
        State = Phase.Returning;
    }

    // the mount key while the broom is away: bring it to her seat (or send it back to her back with a second press)
    public void ToggleCall()
    {
        if (!IsAway) return;
        CallQueued = !CallQueued;
        returnTimer = 0f;                          // a new destination: the time limit starts again
        if (CallQueued) State = Phase.Returning;   // no more flying on, it turns now
    }

    public void CancelCall() => CallQueued = false;

    // ---------- the broom's own motion ----------

    void LateUpdate()
    {
        switch (State)
        {
            case Phase.Mounting: TickMount(); break;
            case Phase.Free: TickFree(); break;
            case Phase.Returning: TickReturn(); break;
            case Phase.Docking: TickDock(); break;
        }
    }

    void TickMount()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / Mathf.Max(s.mountTime, 0.01f));
        float e = t * t * (3f - 2f * t);

        // out to the side and up, then in under her
        Vector3 control = Vector3.Lerp(startPos, seatPos, 0.5f) + new Vector3(0.5f, 0.7f, 0f);
        Vector3 a = Vector3.Lerp(startPos, control, e), b = Vector3.Lerp(control, seatPos, e);
        model.localPosition = Vector3.Lerp(a, b, e);
        model.localRotation = Quaternion.Slerp(startRot, seatRot, e);
        model.localScale = Vector3.Lerp(startScale, seatScale, e);

        if (t >= 1f)
        {
            State = Phase.Ridden;
            MagicFX.Pop(model.position, 0.6f, Lavender, 5);
        }
    }

    void TickFree()
    {
        float dt = Time.deltaTime;
        timer += dt;
        velocity *= Mathf.Exp(-s.freeDrag * dt);

        // slide along whatever it meets instead of flying through it
        Vector3 step = velocity * dt;
        float dist = step.magnitude;
        if (dist > 0.0001f && Physics.SphereCast(model.position, 0.3f, step / dist, out RaycastHit hit, dist + 0.1f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(root))
        {
            velocity = Vector3.ProjectOnPlane(velocity, hit.normal) * 0.5f;
            step = velocity * dt;
        }

        model.position += step;
        Face(velocity, 10f);

        if (timer >= s.freeTime || velocity.magnitude < 5f)
        {
            returnTimer = 0f;
            State = Phase.Returning;
        }
    }

    void TickReturn()
    {
        float dt = Time.deltaTime;
        Vector3 rider = riderVelocity != null ? riderVelocity() : Vector3.zero;
        Vector3 target = CallQueued ? root.TransformPoint(seatPos) : socket.TransformPoint(stowPos);
        Vector3 to = target - model.position;
        float d = to.magnitude;

        // she may be falling or flying: chase at her speed plus some, swooping in with the speed it already had
        // fast while it is far away, easing off over the last few metres so it settles on her instead of flying past
        float chase = Mathf.Min(Mathf.Max(s.returnMinSpeed * Mathf.Clamp01(d / 8f), d * s.returnGain), s.returnMaxSpeed);

        // the time limit: whatever it takes to be there when the time is up (it may go faster than the usual top return speed)
        returnTimer += dt;
        float remaining = s.maxReturnTime - returnTimer;
        if (remaining > 0f) chase = Mathf.Max(chase, d / Mathf.Max(remaining, 0.05f));

        Vector3 wanted = (d > 0.001f ? to / d : Vector3.zero) * chase + rider;
        velocity = Vector3.MoveTowards(velocity, wanted, s.returnAccel * dt);
        model.position += velocity * dt;
        Face(velocity, 10f);

        if (remaining <= 0f && d > s.arriveDistance)   // out of time: it is simply there
        {
            MagicFX.Pop(model.position, 0.4f, Lavender, 3);
            model.position = target;
            d = 0f;
        }

        Vector3 size = CallQueued ? seatScale : seatScale * s.stowScale;
        model.localScale = Vector3.Lerp(model.localScale, size, 1f - Mathf.Exp(-6f * dt));

        if (d > s.arriveDistance) return;

        if (CallQueued)
        {
            CallQueued = false;
            ReachedRider?.Invoke(velocity);
            return;
        }

        // settle onto the back
        model.SetParent(socket, true);
        startPos = model.localPosition;
        startRot = model.localRotation;
        startScale = model.localScale;
        timer = 0f;
        State = Phase.Docking;
    }

    void TickDock()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / Mathf.Max(s.dockTime, 0.01f));
        float e = t * t * (3f - 2f * t);
        model.localPosition = Vector3.Lerp(startPos, stowPos, e);
        model.localRotation = Quaternion.Slerp(startRot, stowRot, e);
        model.localScale = Vector3.Lerp(startScale, stowScale, e);
        if (t >= 1f) State = Phase.Stowed;
    }

    // nose along the direction of travel
    void Face(Vector3 dir, float rate)
    {
        if (dir.sqrMagnitude < 1f) return;
        model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 1f - Mathf.Exp(-rate * Time.deltaTime));
    }
}
