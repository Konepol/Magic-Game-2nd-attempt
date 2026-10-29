using UnityEngine;
using UnityEngine.AI;

// Walking enemy on the NavMesh. Attack types:
//  Melee      - rusher, swings when close
//  HeavyShot  - slow, telegraphed explosive shot along a line
//  Tackle     - attacks rarely: telegraphs a lane, then charges down it and is open to attack afterwards
//  HomingShot - fires a slow projectile that steers toward the player and fizzles out after a time limit
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyGround : MonoBehaviour
{
    public enum AttackType { Melee, HeavyShot, Tackle, HomingShot }
    enum State { Idle, Chase, Windup, Tackling, Recovering }

    [Header("References")]
    [SerializeField] Transform target;                 // found by tag if empty
    [SerializeField] string targetTag = "Player";
    [SerializeField] Transform muzzle;                 // shot origin, falls back to chest height
    [SerializeField] Projectile projectilePrefab;      // heavy / homing shots

    [Header("Aggro")]
    [SerializeField] float aggroRange = 25f;
    [SerializeField] float loseAggroRange = 45f;
    [SerializeField] bool requireLineOfSight = false;
    [SerializeField] LayerMask sightMask = ~0;
    [SerializeField] bool aggroWhenDamaged = true;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float turnSpeed = 8f;
    [SerializeField] float chaseStopDistance = 0f;     // keeps this far from the target while waiting to attack (0 = automatic)

    [Header("Idle Wandering")]
    [SerializeField] float wanderRadius = 8f;
    [SerializeField] Vector2 wanderPause = new Vector2(1.5f, 4f);
    [SerializeField] float wanderSpeedFactor = 0.4f;

    [Header("Attack")]
    [SerializeField] AttackType attackType = AttackType.Melee;
    [SerializeField] float attackRange = 2.2f;         // melee reach, or how close it must be to start a ranged / tackle attack
    [SerializeField] float damage = 12f;
    [SerializeField] float cooldown = 1.2f;
    [SerializeField] float windupTime = 0.6f;          // telegraph time before the attack lands
    [SerializeField] Color indicatorColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Shots (Heavy / Homing)")]
    [SerializeField] float projectileSpeed = 14f;
    [SerializeField] float projectileScale = 3f;
    [SerializeField] float explosionRadius = 2.5f;     // 0 = direct hit only
    [SerializeField] float projectileLifetime = 6f;    // time limit for the shot; homing shots shrink and fizzle out
    [SerializeField, Range(0.1f, 1f)] float aimTrackFraction = 0.5f;   // heavy shot / tackle aim follows the player for this part of the windup, then freezes
    [SerializeField] float homingTurnRate = 70f;       // degrees per second a homing shot can turn

    [Header("Tackle")]
    [SerializeField] float tackleDistance = 14f;
    [SerializeField] float tackleSpeed = 24f;
    [SerializeField] float tackleHitRadius = 1.4f;
    [SerializeField] float tackleLaneWidth = 2f;       // width of the warning lane
    [SerializeField] float tackleRecovery = 1.5f;      // stands still and open after the charge

    State state = State.Idle;

    public string StateName => state.ToString();
    NavMeshAgent agent;
    Health health;
    Health targetHealth;
    StatusEffects status;
    Vector3 home;
    float nextWanderTime;
    float nextPathTime;
    float nextAttackTime;
    float windupStart;
    float recoverUntil;
    Vector3 aimPoint;
    Vector3 tackleDir;
    float tackleTravelled;
    bool tackleHit;

    LineRenderer lineA, lineB, lineC;   // rings (melee / homing), lane or aim line (tackle / heavy)
    Material indicatorMat;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        home = transform.position;
        agent.speed = moveSpeed;
        agent.updateRotation = true;
        agent.stoppingDistance = 0f;
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null && aggroWhenDamaged) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void OnDestroy()
    {
        if (indicatorMat != null) Destroy(indicatorMat);
    }

    void Start() => FindTarget();

    void FindTarget()
    {
        var go = GameObject.FindGameObjectWithTag(targetTag);
        if (go == null) return;
        target = go.transform;
        targetHealth = go.GetComponentInParent<Health>();
        IgnorePlayerBody(go);
    }

    // attacks are distance checks, and a charging body overlapping the player's CharacterController would shove them (even through the floor)
    void IgnorePlayerBody(GameObject player)
    {
        var cc = player.GetComponentInParent<CharacterController>();
        var mine = GetComponent<Collider>();
        if (cc != null && mine != null) Physics.IgnoreCollision(mine, cc);
    }

    void OnDamaged(float amount)
    {
        if (target == null) FindTarget();
        if (target != null && state == State.Idle) state = State.Chase;
    }

    // called by the wave spawner so new enemies come straight for the player
    public void Alert()
    {
        if (target == null) FindTarget();
        if (target != null) state = State.Chase;
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            HideIndicator();
            if (agent.enabled) agent.enabled = false;
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (target == null) { FindTarget(); if (target == null) return; }

        if (status == null) status = GetComponent<StatusEffects>();
        agent.speed = moveSpeed * (status != null ? status.SpeedMultiplier : 1f);

        // stunned: stands still and loses any attack in progress
        if (status != null && status.IsStunned)
        {
            agent.isStopped = true;
            if (state == State.Windup || state == State.Tackling)
            {
                state = State.Chase;
                nextAttackTime = Time.time + cooldown;
                HideIndicator();
            }
            return;
        }

        bool targetDead = targetHealth != null && targetHealth.IsDead;
        float dist = Vector3.Distance(transform.position, target.position);

        switch (state)
        {
            case State.Idle:
                IdleBehaviour();
                if (!targetDead && dist <= aggroRange && (!requireLineOfSight || HasSight())) state = State.Chase;
                break;

            case State.Chase:
                if (targetDead || dist > loseAggroRange)
                {
                    state = State.Idle;
                    home = transform.position;
                    break;
                }
                ChaseBehaviour(dist);
                break;

            case State.Windup:
                WindupBehaviour(targetDead);
                break;

            case State.Tackling:
                TackleBehaviour();
                break;

            case State.Recovering:
                agent.isStopped = true;
                if (Time.time >= recoverUntil) state = State.Chase;
                break;
        }
    }

    void IdleBehaviour()
    {
        agent.isStopped = false;
        agent.speed = moveSpeed * wanderSpeedFactor;

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            if (Time.time < nextWanderTime) return;
            nextWanderTime = Time.time + Random.Range(wanderPause.x, wanderPause.y);

            Vector3 p = home + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }
    }

    void ChaseBehaviour(float dist)
    {
        float stopAt = chaseStopDistance > 0f
            ? chaseStopDistance
            : attackRange * (attackType == AttackType.Melee ? 1f : 0.85f);

        if (dist <= stopAt)
        {
            agent.isStopped = true;
            FaceTarget();
        }
        else
        {
            agent.isStopped = false;
            if (Time.time >= nextPathTime)
            {
                nextPathTime = Time.time + 0.15f;
                agent.SetDestination(target.position);
            }
        }

        bool canAttack = dist <= attackRange && Time.time >= nextAttackTime
                         && (attackType == AttackType.Melee || HasSight());
        if (canAttack) StartWindup();
    }

    void StartWindup()
    {
        state = State.Windup;
        windupStart = Time.time;
        agent.isStopped = true;
        aimPoint = target.position + Vector3.up;
        tackleDir = FlatDirectionToTarget();
        EnsureIndicator();
    }

    void WindupBehaviour(bool targetDead)
    {
        if (targetDead) { CancelWindup(); return; }

        FaceTarget();
        float t = (Time.time - windupStart) / Mathf.Max(windupTime, 0.01f);

        switch (attackType)
        {
            case AttackType.HeavyShot:
                if (t < aimTrackFraction) aimPoint = target.position + Vector3.up;   // then it freezes so there is time to dodge
                UpdateAimLine();
                break;

            case AttackType.Tackle:
                if (t < aimTrackFraction) tackleDir = FlatDirectionToTarget();       // lane follows, then locks
                FaceDirection(tackleDir);
                UpdateTackleLane();
                break;

            case AttackType.HomingShot:
                UpdateRings(1.5f, t);
                break;

            default:
                UpdateRings(attackRange, t);
                break;
        }

        if (t < 1f) return;

        HideIndicator();
        Execute();
    }

    void CancelWindup()
    {
        state = State.Chase;
        HideIndicator();
    }

    void Execute()
    {
        switch (attackType)
        {
            case AttackType.Melee:
                Vector3 flat = target.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= attackRange + 0.4f && targetHealth != null && !targetHealth.IsDead)
                    targetHealth.TakeDamage(damage, target.position);
                FinishAttack();
                break;

            case AttackType.HeavyShot:
            case AttackType.HomingShot:
                FireShot();
                FinishAttack();
                break;

            case AttackType.Tackle:
                state = State.Tackling;
                tackleTravelled = 0f;
                tackleHit = false;
                agent.isStopped = true;
                break;
        }
    }

    void FinishAttack()
    {
        nextAttackTime = Time.time + cooldown;
        state = State.Chase;
    }

    void FireShot()
    {
        if (projectilePrefab == null) return;

        Vector3 origin = MuzzlePosition();
        Vector3 toward = attackType == AttackType.HomingShot ? target.position + Vector3.up : aimPoint;
        Vector3 dir = (toward - origin).normalized;

        var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
        p.Init(damage, projectileSpeed, projectileLifetime, projectileScale, explosionRadius,
            0, false, false, default, attackType == AttackType.HomingShot ? homingTurnRate : 0f);
    }

    // charge down the locked lane; hits the player once if they are in the way
    void TackleBehaviour()
    {
        float step = tackleSpeed * (status != null ? status.SpeedMultiplier : 1f) * Time.deltaTime;
        Vector3 before = transform.position;
        agent.Move(tackleDir * step);
        float moved = Vector3.Distance(before, transform.position);
        tackleTravelled += moved;
        FaceDirection(tackleDir);

        if (!tackleHit && targetHealth != null && !targetHealth.IsDead)
        {
            Vector3 flat = target.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= tackleHitRadius)
            {
                tackleHit = true;
                targetHealth.TakeDamage(damage, target.position);
                CameraShake.Add(0.3f);
            }
        }

        // done: reached the end of the lane, or ran into the edge of the NavMesh / a wall
        if (tackleTravelled >= tackleDistance || moved < step * 0.3f)
        {
            state = State.Recovering;
            recoverUntil = Time.time + tackleRecovery;
            nextAttackTime = Time.time + cooldown;
        }
    }

    Vector3 MuzzlePosition() => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;

    Vector3 FlatDirectionToTarget()
    {
        Vector3 flat = target.position - transform.position;
        flat.y = 0f;
        return flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
    }

    void FaceTarget() => FaceDirection(FlatDirectionToTarget());

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    bool HasSight()
    {
        Vector3 from = transform.position + Vector3.up;
        Vector3 to = target.position + Vector3.up;
        Vector3 dir = to - from;
        var hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, sightMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<Barrier>() != null) continue;   // barriers are see-through
            if (h.collider.transform.IsChildOf(target) || h.collider.transform == target) continue;
            return false;
        }
        return true;
    }

    // ---------- telegraph ----------

    void EnsureIndicator()
    {
        if (indicatorMat == null) indicatorMat = SpellVisuals.Make(indicatorColor, false);

        switch (attackType)
        {
            case AttackType.Melee:
            case AttackType.HomingShot:
                if (lineA == null) lineA = SpellVisuals.CreateRing(null, indicatorMat, 0.05f);
                if (lineB == null) lineB = SpellVisuals.CreateRing(null, indicatorMat, 0.04f);
                lineA.enabled = lineB.enabled = true;
                break;

            case AttackType.HeavyShot:
                EnsureLine(0.12f, LineAlignment.View);
                break;

            case AttackType.Tackle:
                EnsureLine(tackleLaneWidth, LineAlignment.TransformZ);   // a flat lane on the ground
                break;
        }
    }

    void EnsureLine(float width, LineAlignment alignment)
    {
        if (lineC == null)
        {
            var go = new GameObject("AttackLine");
            go.transform.SetParent(transform, false);
            lineC = go.AddComponent<LineRenderer>();
            lineC.useWorldSpace = true;
            lineC.positionCount = 2;
            lineC.sharedMaterial = indicatorMat;
            lineC.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        lineC.startWidth = lineC.endWidth = width;
        lineC.alignment = alignment;
        lineC.enabled = true;
    }

    void HideIndicator()
    {
        if (lineA != null) lineA.enabled = false;
        if (lineB != null) lineB.enabled = false;
        if (lineC != null) lineC.enabled = false;
    }

    void UpdateRings(float radius, float t)
    {
        Vector3 c = transform.position + Vector3.up * 0.06f;
        SpellVisuals.SetRing(lineA, c, radius);
        SpellVisuals.SetRing(lineB, c, radius * Mathf.Clamp01(t));
    }

    void UpdateTackleLane()
    {
        Vector3 start = transform.position;
        start.y = GroundY() + 0.08f;
        lineC.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat on the ground
        lineC.SetPosition(0, start);
        lineC.SetPosition(1, start + tackleDir * tackleDistance);
    }

    float GroundY()
    {
        return Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, sightMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : transform.position.y - 1f;
    }

    void UpdateAimLine()
    {
        Vector3 origin = MuzzlePosition();
        Vector3 dir = (aimPoint - origin).normalized;
        float len = attackRange * 1.4f;
        Vector3 end = origin + dir * len;

        var hits = Physics.RaycastAll(origin, dir, len, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.transform.IsChildOf(target) || h.collider.transform == target) continue;
            if (h.distance < best) { best = h.distance; end = h.point; }
        }

        lineC.SetPosition(0, origin);
        lineC.SetPosition(1, end);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
