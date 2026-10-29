using UnityEngine;

// Animates the little creature models of the enemies without a skeleton: every limb is its own piece with a pivot, and this moves them.
// Walking, charging, winding up, hovering, flapping, taking a hit and dying are all worked out from what the enemy is doing.
// Pieces are found by name inside the "Model" child: Body, Head, LegL, LegR, ArmL, ArmR, Tail, WingL, WingR, Core.
public class CreatureAnimator : MonoBehaviour
{
    public enum Kind { Imp, Bruiser, Golem, Ghost, Flyer }

    public Kind kind = Kind.Imp;
    public float walkSpeedScale = 1f;

    Transform model, body, head, legL, legR, armL, armR, tail, wingL, wingR, core;
    Quaternion bodyRot, headRot, legLRot, legRRot, armLRot, armRRot, tailRot, wingLRot, wingRRot;
    Vector3 modelPos, modelScale, corePos, coreScale;
    Renderer coreRenderer;
    MaterialPropertyBlock block;

    Health health;
    EnemyGround ground;
    EnemyFlyingShooter flyer;

    Vector3 lastPos;
    float speed, phase, hitUntil, dieStart = -1f;
    float seed;

    void Awake()
    {
        model = transform.Find("Model");
        if (model == null) { enabled = false; return; }

        body = Find("Body"); head = Find("Head"); legL = Find("LegL"); legR = Find("LegR"); armL = Find("ArmL"); armR = Find("ArmR");
        tail = Find("Tail"); wingL = Find("WingL"); wingR = Find("WingR"); core = Find("Core");

        bodyRot = Rot(body); headRot = Rot(head); legLRot = Rot(legL); legRRot = Rot(legR); armLRot = Rot(armL); armRRot = Rot(armR);
        tailRot = Rot(tail); wingLRot = Rot(wingL); wingRRot = Rot(wingR);
        modelPos = model.localPosition;
        modelScale = model.localScale;
        if (core != null) { corePos = core.localPosition; coreScale = core.localScale; coreRenderer = core.GetComponent<Renderer>(); }
        block = new MaterialPropertyBlock();

        health = GetComponent<Health>();
        ground = GetComponent<EnemyGround>();
        flyer = GetComponent<EnemyFlyingShooter>();
        lastPos = transform.position;
        seed = Random.value * 10f;
        ShadowSettings.Attach(gameObject, 0.6f * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; }
    }

    void OnDisable()
    {
        if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
    }

    Transform Find(string name)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static Quaternion Rot(Transform t) => t != null ? t.localRotation : Quaternion.identity;

    void OnDamaged(float amount) { hitUntil = Time.time + 0.22f; }
    void OnDied() { if (dieStart < 0f) dieStart = Time.time; }

    void LateUpdate()
    {
        if (model == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // how fast it is moving along the ground
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        delta.y = 0f;
        speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
        float move = Mathf.Clamp01(speed / 6f);
        phase += dt * (3f + speed * 1.1f) * walkSpeedScale;

        string state = ground != null ? ground.StateName : (flyer != null ? flyer.StateName : "");
        bool windup = state == "Windup", tackle = state == "Tackling", recover = state == "Recovering";
        float t = Time.time + seed;

        // pose starts from rest every frame
        Vector3 pos = modelPos;
        Vector3 scale = modelScale;
        Quaternion bodyOffset = Quaternion.identity;
        Quaternion headOffset = Quaternion.identity;

        float stride = Mathf.Sin(phase);
        float bounce = Mathf.Abs(Mathf.Sin(phase));
        float squashY = 1f, squashXZ = 1f;

        switch (kind)
        {
            case Kind.Imp:
            {
                pos.y += bounce * 0.12f * move + Mathf.Sin(t * 3f) * 0.015f;
                Swing(legL, legLRot, stride * 55f * move, 0f);
                Swing(legR, legRRot, -stride * 55f * move, 0f);
                Swing(armL, armLRot, -stride * 50f * move - (windup ? 90f : 0f), 12f);
                Swing(armR, armRRot, stride * 50f * move - (windup ? 90f : 0f), -12f);
                if (tail != null) tail.localRotation = tailRot * Quaternion.Euler(0f, Mathf.Sin(t * 8f) * 25f, Mathf.Sin(t * 5f) * 8f);
                headOffset = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * 5f * move);
                bodyOffset = Quaternion.Euler(move * 14f + (tackle ? 25f : 0f), 0f, 0f);
                if (windup) { squashY = 0.85f; squashXZ = 1.1f; }
                break;
            }
            case Kind.Bruiser:
            {
                pos.y += bounce * 0.1f * move;
                Swing(legL, legLRot, stride * 45f * move + (tackle ? -50f : 0f), 0f);
                Swing(legR, legRRot, -stride * 45f * move + (tackle ? 50f : 0f), 0f);
                Swing(armL, armLRot, -stride * 30f * move, 0f);
                Swing(armR, armRRot, stride * 30f * move, 0f);
                // lowers its head and paws the ground before charging, then stretches out along the charge
                float lower = windup ? 1f : tackle ? 0.8f : recover ? 0.3f : 0f;
                headOffset = Quaternion.Euler(lower * 38f + (windup ? Mathf.Sin(t * 30f) * 4f : 0f), 0f, 0f);
                bodyOffset = Quaternion.Euler((tackle ? 18f : 0f) + (windup ? -8f : 0f) + (recover ? -10f : 0f), 0f, 0f);
                if (windup) { squashY = 0.9f; squashXZ = 1.06f; pos.y -= 0.06f; }
                if (tackle) { squashY = 0.92f; squashXZ = 0.96f; }
                if (tail != null) tail.localRotation = tailRot * Quaternion.Euler(0f, Mathf.Sin(t * 6f) * 18f, 0f);
                break;
            }
            case Kind.Golem:
            {
                float stomp = Mathf.Sin(phase * 0.7f);
                pos.y += Mathf.Abs(stomp) * 0.07f * move + Mathf.Sin(t * 1.6f) * 0.02f;
                Swing(legL, legLRot, stomp * 28f * move, 0f);
                Swing(armL, armLRot, -stomp * 25f * move + Mathf.Sin(t * 1.3f) * 4f, 8f);
                Swing(armR, armRRot, stomp * 25f * move + Mathf.Sin(t * 1.3f + 1f) * 4f, -8f);
                if (legR != null) legR.localRotation = legRRot * Quaternion.Euler(-stomp * 28f * move, 0f, 0f);
                bodyOffset = Quaternion.Euler(0f, 0f, stomp * 4f * move);
                headOffset = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f, 0f, 0f);
                squashY = 1f + Mathf.Sin(t * 1.8f) * 0.015f;
                break;
            }
            case Kind.Ghost:
            {
                pos.y += Mathf.Sin(t * 2.4f) * 0.12f;
                bodyOffset = Quaternion.Euler(move * 18f, 0f, Mathf.Sin(t * 1.7f) * 5f);
                headOffset = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 6f);
                if (tail != null) tail.localRotation = tailRot * Quaternion.Euler(0f, Mathf.Sin(t * 4f) * 20f, Mathf.Sin(t * 3f) * 12f);
                Swing(armL, armLRot, Mathf.Sin(t * 2f) * 14f, 20f);
                Swing(armR, armRRot, Mathf.Sin(t * 2f + 1.5f) * 14f, -20f);
                break;
            }
            case Kind.Flyer:
            {
                float flap = Mathf.Sin(t * 16f);
                pos.y += Mathf.Sin(t * 3f) * 0.18f;
                if (wingL != null) wingL.localRotation = wingLRot * Quaternion.Euler(0f, 0f, flap * 38f);
                if (wingR != null) wingR.localRotation = wingRRot * Quaternion.Euler(0f, 0f, -flap * 38f);
                bodyOffset = Quaternion.Euler(Mathf.Clamp(speed * 2.5f, 0f, 22f), 0f, Mathf.Sin(t * 1.4f) * 5f);
                if (tail != null) tail.localRotation = tailRot * Quaternion.Euler(Mathf.Sin(t * 5f) * 14f, 0f, 0f);
                break;
            }
        }

        // taking a hit: a quick squash and a flinch
        float hit = Mathf.Clamp01((hitUntil - Time.time) / 0.22f);
        if (hit > 0f)
        {
            squashY *= 1f - 0.18f * hit;
            squashXZ *= 1f + 0.12f * hit;
            bodyOffset = Quaternion.Euler(-16f * hit, 0f, 0f) * bodyOffset;
        }

        // dying: spins, puffs up a little and shrinks away
        if (dieStart >= 0f)
        {
            float k = Mathf.Clamp01((Time.time - dieStart) / 0.45f);
            float s = k < 0.2f ? 1f + k * 1.5f : Mathf.Lerp(1.3f, 0f, (k - 0.2f) / 0.8f);
            squashXZ *= s;
            squashY *= s;
            pos.y += k * 0.3f;
            model.localRotation = Quaternion.Euler(0f, k * 360f, 0f);
        }

        model.localPosition = pos;
        model.localScale = new Vector3(modelScale.x * squashXZ, modelScale.y * squashY, modelScale.z * squashXZ);
        if (body != null) body.localRotation = bodyRot * bodyOffset;
        if (head != null) head.localRotation = headRot * headOffset;

        // the glowing core pulses
        if (core != null && coreRenderer != null)
        {
            float pulse = 1.2f + 0.8f * Mathf.Sin(t * (windup ? 14f : 3f)) + (windup || tackle ? 1.5f : 0f);
            coreRenderer.GetPropertyBlock(block);
            block.SetFloat("_EmissionStrength", pulse);
            coreRenderer.SetPropertyBlock(block);
            core.localScale = coreScale * (1f + 0.06f * Mathf.Sin(t * 3f));
        }
    }

    // swings a limb about its pivot (forward/back) with a little outward splay
    static void Swing(Transform limb, Quaternion rest, float degrees, float splay)
    {
        if (limb == null) return;
        limb.localRotation = rest * Quaternion.Euler(degrees, 0f, splay);
    }
}
