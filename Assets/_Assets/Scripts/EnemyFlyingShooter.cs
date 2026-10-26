using UnityEngine;

public class EnemyFlyingShooter : MonoBehaviour
{
    enum State { Idle, Chase }

    [Header("References")]
    [SerializeField] Transform target;                 // found by tag if empty
    [SerializeField] string targetTag = "Player";
    [SerializeField] Transform muzzle;                 // falls back to this transform

    [Header("Aggro")]
    [SerializeField] float aggroRange = 30f;
    [SerializeField] float loseAggroRange = 45f;       // larger than aggroRange so it doesn't flicker at the edge
    [SerializeField] bool requireLineOfSight = true;
    [SerializeField] LayerMask sightMask = ~0;         // exclude the Enemy and Player layers
    [SerializeField] bool aggroWhenDamaged = true;

    [Header("Ground / Flight Mode")]
    [SerializeField] LayerMask groundMask = ~0;        // exclude the Enemy and Player layers
    [SerializeField] float groundClearance = 0.15f;    // gap between the collider bottom and the ground when walking
    [SerializeField] float flyMinClearance = 1.5f;     // never flies lower than this above the ground
    [SerializeField] float groundSpeedFactor = 0.7f;   // walking (idle) is slower than flying
    [SerializeField] float verticalSpeed = 5f;         // takes off and lands at this rate
    [SerializeField] float groundProbeHeight = 3f;     // ground ray starts this far above the enemy

    [Header("Movement (player broom is 14)")]
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float acceleration = 6f;
    [SerializeField] float turnSpeed = 6f;
    [SerializeField] float preferredDistance = 14f;    // keeps this far from the target
    [SerializeField] float distanceTolerance = 3f;
    [SerializeField] float hoverHeightOffset = 2f;     // aims to hover this far above the target
    [SerializeField] float strafeAmount = 0.5f;        // 0 = straight line, 1 = full circling
    [SerializeField] float bobAmplitude = 0.4f;
    [SerializeField] float bobFrequency = 1.2f;

    [Header("Idle Wandering")]
    [SerializeField] float wanderRadius = 8f;
    [SerializeField] float wanderSpeedFactor = 0.4f;
    [SerializeField] Vector2 wanderPause = new Vector2(1f, 3f);

    [Header("Obstacle Avoidance")]
    [SerializeField] float avoidRadius = 0.4f;
    [SerializeField] float avoidDistance = 3f;
    [SerializeField] LayerMask obstacleMask = ~0;

    [Header("Shooting")]
    [SerializeField] float fireRange = 28f;
    [SerializeField] float fireRate = 1.2f;            // shots per second
    [SerializeField] float aimSpreadDegrees = 2f;   // random error added when the aim locks
    [SerializeField] float damage = 8f;
    [SerializeField] float beamRadius = 0.15f;         // thickness of the hit check, a bit forgiving
    [SerializeField] float beamDuration = 0.12f;       // how long the beam flash stays visible
    [SerializeField] float beamWidth = 0.25f;
    [SerializeField] LayerMask damageMask = ~0;        // exclude the Enemy layer; the first thing hit takes the damage

    [Header("Shot Indicator")]
    [SerializeField] float trackTime = 0.6f;           // aims at the player with no indicator shown
    [SerializeField] float warnTime = 0.6f;            // indicator shown, then it fires
    [SerializeField, Range(0f, 1f)] float warnTrackFraction = 0.5f;   // the line follows the player for this part of the warning, then freezes (0 = locked at once, 1 = tracks until it fires)
    [SerializeField] float trackTurnDegrees = 120f;    // how fast the line can swing per second while following (0 = instant)
    [SerializeField] LayerMask indicatorMask = ~0;     // exclude the Enemy and Player layers so the line passes through the player
    [SerializeField] Color indicatorColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] float lineWidth = 0.08f;
    [SerializeField] float markerSize = 0.8f;

    State state = State.Idle;
    Health health;
    float halfHeight = 0.5f;
    bool flying;
    Vector3 homePos;
    Vector3 wanderPoint;
    float wanderWaitUntil;
    Vector3 velocity;
    float nextFireTime;
    float strafeDir = 1f;
    bool winding;
    bool locked;
    float windupStart;
    float lockTime;
    Vector3 aimDir;
    Vector3 lockedPoint;
    Quaternion lockSpread = Quaternion.identity;
    LineRenderer line;
    Transform marker;
    Material indicatorMat;
    bool beamActive;
    float beamUntil;
    Vector3 beamOrigin;
    Vector3 beamEnd;
    float nextStrafeFlip;

    void Awake()
    {
        health = GetComponent<Health>();
        homePos = transform.position;
        wanderPoint = homePos;
        var col = GetComponent<Collider>();
        if (col != null) halfHeight = col.bounds.extents.y;
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

    void Start()
    {
        FindTarget();
    }

    void OnDamaged(float amount)
    {
        if (target == null) FindTarget();
        if (target != null) state = State.Chase;
    }

    void FindTarget()
    {
        var go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;

    }

    void Update()
    {
        UpdateBeam();
        if (health != null && health.IsDead) return;
        if (target == null) { FindTarget(); if (target == null) return; }

        float dt = Time.deltaTime;

        UpdateState();

        // walks while idle, takes off when it aggros and lands again when it loses interest
        flying = state == State.Chase;

        Vector3 desiredVelocity = state == State.Chase ? ChaseVelocity() : IdleVelocity();
        desiredVelocity = Avoid(desiredVelocity);

        // horizontal is smoothed, vertical is handled on its own below
        Vector3 desiredFlat = winding ? Vector3.zero : new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);   // hover in place while aiming
        Vector3 flatVel = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), desiredFlat, acceleration * dt);
        velocity = new Vector3(flatVel.x, VerticalVelocity(), flatVel.z);
        transform.position += velocity * dt;
        ClampAboveGround();

        FaceTowards(state == State.Chase ? target.position - transform.position : velocity);

        if (state == State.Chase) TryShoot();
    }

    void UpdateState()
    {
        float dist = Vector3.Distance(transform.position, target.position);

        if (state == State.Idle)
        {
            if (dist <= aggroRange && HasSight()) state = State.Chase;
        }
        else if (dist > loseAggroRange)
        {
            state = State.Idle;
            homePos = transform.position;   // wander around where it gave up
            wanderPoint = homePos;
        }
    }

    bool HasSight()
    {
        if (!requireLineOfSight) return true;
        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up;
        Vector3 dir = to - from;
        return !Physics.Raycast(from, dir.normalized, dir.magnitude, sightMask, QueryTriggerInteraction.Ignore);
    }

    Vector3 IdleVelocity()
    {
        Vector3 toPoint = wanderPoint - transform.position;
        toPoint.y = 0f;
        if (toPoint.magnitude < 0.5f)
        {
            if (Time.time >= wanderWaitUntil)
            {
                Vector2 c = Random.insideUnitCircle * wanderRadius;
                wanderPoint = homePos + new Vector3(c.x, 0f, c.y);
                wanderWaitUntil = Time.time + Random.Range(wanderPause.x, wanderPause.y);
            }
            return Vector3.zero;
        }
        return toPoint.normalized * (moveSpeed * wanderSpeedFactor);
    }

    Vector3 ChaseVelocity()
    {
        Vector3 toTarget = target.position - transform.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float flatDist = flat.magnitude;
        Vector3 flatDir = flatDist > 0.01f ? flat / flatDist : transform.forward;

        // approach / back off to keep the preferred distance
        float distError = flatDist - preferredDistance;
        float radial = Mathf.Abs(distError) < distanceTolerance ? 0f : Mathf.Sign(distError);

        // circle around the target, flipping direction now and then
        if (Time.time >= nextStrafeFlip)
        {
            strafeDir = Random.value < 0.5f ? -1f : 1f;
            nextStrafeFlip = Time.time + Random.Range(2f, 5f);
        }
        Vector3 strafe = Vector3.Cross(Vector3.up, flatDir) * strafeDir * strafeAmount;

        float speed = flying ? moveSpeed : moveSpeed * groundSpeedFactor;
        return Vector3.ClampMagnitude(flatDir * radial + strafe, 1f) * speed;
    }

    // finds the ground below the enemy, or false if there is none
    bool TryGetGround(Vector3 pos, out float groundY)
    {
        Vector3 origin = pos + Vector3.up * groundProbeHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }
        groundY = 0f;
        return false;
    }

    float VerticalVelocity()
    {
        if (!TryGetGround(transform.position, out float groundY)) return 0f;

        float wantedY;
        if (flying)
        {
            // hover above the target with a gentle bob, but never below the clearance over the ground
            wantedY = target.position.y + hoverHeightOffset + Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            wantedY = Mathf.Max(wantedY, groundY + halfHeight + flyMinClearance);
        }
        else
        {
            wantedY = groundY + halfHeight + groundClearance;   // walk / land
        }
        return Mathf.Clamp((wantedY - transform.position.y) * 3f, -1f, 1f) * verticalSpeed;
    }

    // hard floor so it can never end up inside the ground, whatever the state
    void ClampAboveGround()
    {
        if (!TryGetGround(transform.position, out float groundY)) return;
        float minY = groundY + halfHeight + groundClearance * 0.5f;
        if (transform.position.y < minY)
        {
            Vector3 p = transform.position;
            p.y = minY;
            transform.position = p;
        }
    }

    Vector3 Avoid(Vector3 desired)
    {
        desired.y = 0f;   // vertical is handled separately
        if (desired.sqrMagnitude < 0.01f) return desired;
        if (Physics.SphereCast(transform.position, avoidRadius, desired.normalized, out RaycastHit hit,
                avoidDistance, obstacleMask, QueryTriggerInteraction.Ignore)
            && hit.transform != target && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target))
        {
            // slide along the surface
            Vector3 slid = Vector3.ProjectOnPlane(desired, hit.normal);
            slid.y = 0f;
            return Vector3.ClampMagnitude(slid, desired.magnitude);
        }
        return desired;
    }

    void FaceTowards(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    void TryShoot()
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        float dist = Vector3.Distance(origin, target.position);
        bool canShoot = dist <= fireRange && HasSight();

        if (!winding)
        {
            if (!canShoot || Time.time < nextFireTime) return;
            winding = true;
            locked = false;
            windupStart = Time.time;
        }
        else if (!canShoot)
        {
            CancelWindup();
            return;
        }

        float elapsed = Time.time - windupStart;

        if (!locked)
        {
            if (elapsed < trackTime) return;   // still tracking, nothing is shown

            // lock the shot at where the player is right now, plus a little spread
            Vector3 toPoint = (target.position + Vector3.up) - origin;
            // spread is baked into the locked world point, so it stays put while the enemy moves
            lockSpread = Quaternion.Euler(Random.Range(-aimSpreadDegrees, aimSpreadDegrees),
                                          Random.Range(-aimSpreadDegrees, aimSpreadDegrees), 0f);
            lockedPoint = origin + lockSpread * toPoint;
            aimDir = (lockedPoint - origin).normalized;
            locked = true;
            lockTime = Time.time;
            EnsureIndicator();
        }

        float t = (Time.time - lockTime) / Mathf.Max(warnTime, 0.01f);

        // the enemy keeps moving, so re-aim from its current position
        if (t < warnTrackFraction)
        {
            // keep following the player, swinging at a limited rate so there is still room to dodge
            Vector3 wanted = (lockSpread * ((target.position + Vector3.up) - origin)).normalized;
            float step = trackTurnDegrees <= 0f ? 360f : trackTurnDegrees * Time.deltaTime;
            aimDir = Vector3.RotateTowards(aimDir, wanted, step * Mathf.Deg2Rad, 0f).normalized;
            lockedPoint = origin + aimDir * Vector3.Distance(origin, target.position);
        }
        else
        {
            Vector3 toLocked = lockedPoint - origin;
            if (toLocked.sqrMagnitude > 0.0001f) aimDir = toLocked.normalized;
        }

        UpdateIndicator(origin, t);

        if (t >= 1f)
        {
            FireBeam(origin);
            nextFireTime = Time.time + 1f / fireRate;
            winding = false;
            locked = false;
            aimDir = Vector3.zero;
            if (marker != null) marker.gameObject.SetActive(false);
        }
    }

    // instant hit: the first thing along the locked line takes the damage
    void FireBeam(Vector3 origin)
    {
        float maxLen = fireRange * 1.5f;
        Vector3 end = origin + aimDir * maxLen;

        if (Physics.SphereCast(origin, beamRadius, aimDir, out RaycastHit hit, maxLen, damageMask, QueryTriggerInteraction.Ignore))
        {
            end = origin + aimDir * hit.distance;
            if (hit.collider.CompareTag(targetTag))
            {
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(damage, hit.point);
            }
        }

        beamOrigin = origin;
        beamEnd = end;
        beamUntil = Time.time + beamDuration;
        beamActive = true;
    }

    // short bright flash along the beam after it fires
    void UpdateBeam()
    {
        if (!beamActive || line == null) return;

        if (Time.time >= beamUntil)
        {
            beamActive = false;
            if (!winding) line.enabled = false;
            return;
        }

        float fade = Mathf.Clamp01((beamUntil - Time.time) / Mathf.Max(beamDuration, 0.01f));
        Color c = Color.Lerp(indicatorColor, Color.white, 0.6f); c.a = fade;
        indicatorMat.color = c;
        if (indicatorMat.HasProperty("_BaseColor")) indicatorMat.SetColor("_BaseColor", c);

        line.enabled = true;
        line.startWidth = beamWidth * fade;
        line.endWidth = beamWidth * fade;
        line.SetPosition(0, beamOrigin);
        line.SetPosition(1, beamEnd);
    }

    void CancelWindup()
    {
        winding = false;
        locked = false;
        aimDir = Vector3.zero;
        if (line != null) line.enabled = false;
        if (marker != null) marker.gameObject.SetActive(false);
    }

    void EnsureIndicator()
    {
        if (line == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            indicatorMat = new Material(sh) { color = indicatorColor };
            if (indicatorMat.HasProperty("_BaseColor")) indicatorMat.SetColor("_BaseColor", indicatorColor);

            var lineGo = new GameObject("ShotIndicatorLine");
            lineGo.transform.SetParent(transform, false);
            line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = indicatorMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 4;

            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = "ShotIndicatorMarker";
            Destroy(m.GetComponent<Collider>());
            m.GetComponent<Renderer>().sharedMaterial = indicatorMat;
            m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker = m.transform;
        }
        line.enabled = true;
        marker.gameObject.SetActive(true);
    }

    // line from the muzzle along the locked shot, with a marker where it will land
    void UpdateIndicator(Vector3 origin, float t)
    {
        float maxLen = fireRange * 1.5f;
        Vector3 end = origin + aimDir * maxLen;
        if (Physics.Raycast(origin, aimDir, out RaycastHit hit, maxLen, indicatorMask, QueryTriggerInteraction.Ignore))
            end = hit.point;

        // brighter and thicker as the shot gets close
        float pulse = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t));
        Color c = indicatorColor; c.a = pulse;
        indicatorMat.color = c;
        if (indicatorMat.HasProperty("_BaseColor")) indicatorMat.SetColor("_BaseColor", c);

        line.startWidth = lineWidth * pulse;
        line.endWidth = lineWidth * pulse;
        line.SetPosition(0, origin);
        line.SetPosition(1, end);

        marker.position = end;
        marker.localScale = Vector3.one * markerSize * Mathf.Lerp(1.4f, 0.7f, Mathf.Clamp01(t));
    }

    void OnDestroy()
    {
        if (marker != null) Destroy(marker.gameObject);
        if (indicatorMat != null) Destroy(indicatorMat);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseAggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fireRange);
    }
}
