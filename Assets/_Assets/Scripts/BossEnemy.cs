using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// The Warden of Ruins: a huge stone giant that walks after the player and fights in three phases.
//   Phase 1: ground slam (cannot be blocked: jump or fly over the ring, or stand in its gap) and a fan of slow shots.
//   Phase 2 (66% health): adds a targeting beam and meteor rain, and speeds up.
//   Phase 3 (33% health): faster again; the slam also switches the broom off arena-wide (grounding aura), and damaging floor patches
//   keep appearing under the player.
// The whole body is built from primitives, so a real model can replace it later. The boss bar and warnings are drawn from code.
public class BossEnemy : MonoBehaviour
{
    [SerializeField] string bossName = "The Warden of Ruins";
    [SerializeField] float maxHealth = 4500f;
    [SerializeField] float walkSpeed = 3.4f;
    [SerializeField] float introDelay = 3.5f;
    [SerializeField, Tooltip("Multiplies the damage of every attack.")] float damageScale = 1f;

    [Header("Damage (before the multiplier)")]
    [SerializeField] float slamDamage = 30f;
    [SerializeField] float shotDamage = 12f;
    [SerializeField] float beamDamage = 25f;
    [SerializeField] float meteorDamage = 28f;
    [SerializeField] float hazardTickDamage = 6f;

    public event Action Defeated;
    public Health Health => health;
    public int Phase { get; private set; } = 1;
    public float FightStartedAt { get; private set; } = -1f;
    public string BossName => bossName;

    static readonly Color[] PhaseColors =
    {
        new Color(1f, 0.8f, 0.3f), new Color(1f, 0.5f, 0.2f), new Color(1f, 0.2f, 0.3f),
    };

    Health health;
    NavMeshAgent agent;
    Transform player;
    Health playerHealth;
    Transform visual, head, armL, armR, legL, legR;
    float legPhase;
    readonly List<Material> glowMats = new List<Material>();
    Material stoneMat;
    GameObject fxRoot;
    float floorY;
    int solidMask;
    bool dead, pendingRoar;
    float tempoBoost;

    // animation targets
    float armRaise, armSwing, lift, headGlow;
    float armRaiseShown, liftShown;

    // ui
    GameObject barRoot;
    RectTransform barFill, barChip;
    Text nameText, warnText, bannerText;
    float shownFill = 1f, chipFill = 1f, warnUntil, bannerUntil;

    float Tempo => 1f + 0.22f * (Phase - 1) + tempoBoost;

    // ---------- setup ----------

    void Awake()
    {
        gameObject.name = bossName;
        floorY = transform.position.y;
        solidMask = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << 2));

        gameObject.layer = LayerMask.NameToLayer("Enemy");
        gameObject.tag = "Enemy";

        health = gameObject.AddComponent<Health>();
        health.Configure(maxHealth, false, 0f);
        health.Died += OnDied;

        var capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.radius = 2.7f;
        capsule.height = 9.5f;
        capsule.center = new Vector3(0f, 4.75f, 0f);

        BuildBody();
        BuildBar();
        fxRoot = new GameObject("BossFx");
    }

    void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) { player = go.transform; playerHealth = go.GetComponentInParent<Health>(); }

        // the walking agent is made now, after the scene's NavMesh has been built
        if (NavMesh.SamplePosition(transform.position, out var hit, 15f, NavMesh.AllAreas))
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 1.4f;
            agent.height = 9f;
            agent.speed = walkSpeed;
            agent.acceleration = 8f;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.stoppingDistance = 0f;
            agent.Warp(hit.position);
        }

        StartCoroutine(FightLoop());
    }

    void OnDestroy()
    {
        GroundingAura.Set(this, false);
        if (fxRoot != null) Destroy(fxRoot);
        foreach (var m in glowMats) if (m != null) Destroy(m);
        if (stoneMat != null) Destroy(stoneMat);
    }

    // ---------- the body (placeholder shapes, with glowing rune bands that change colour per phase) ----------

    Material Stone(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = sh != null ? new Material(sh) : SpellVisuals.Make(c, false);
        SpellVisuals.SetColor(m, c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    Material Glow(Color c)
    {
        var m = SpellVisuals.Make(c, false);
        glowMats.Add(m);
        return m;
    }

    Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // the toon model made by Tools > Magic > Build Boss Model: it keeps the same pivots (Head, ArmL, ArmR) the fight animation drives
    bool BuildFromModel()
    {
        var prefab = Resources.Load<GameObject>("Boss/WardenModel");
        if (prefab == null) return false;

        visual = Instantiate(prefab, transform).transform;
        visual.name = "Visual";
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;

        head = FindPart("Head"); armL = FindPart("ArmL"); armR = FindPart("ArmR"); legL = FindPart("LegL"); legR = FindPart("LegR");

        // the glowing parts get their own materials, so their colour can follow the fight phase
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("Boss_Glow"))
                {
                    var inst = new Material(mats[i]);
                    mats[i] = inst;
                    glowMats.Add(inst);
                }
            r.sharedMaterials = mats;
        }
        SetPhaseColor(1);
        return head != null && armL != null && armR != null;
    }

    Transform FindPart(string partName)
    {
        foreach (var t in visual.GetComponentsInChildren<Transform>(true)) if (t.name == partName) return t;
        return null;
    }

    void BuildBody()
    {
        if (BuildFromModel()) return;
        if (visual != null) Destroy(visual.gameObject);

        stoneMat = Stone(new Color(0.34f, 0.32f, 0.3f));
        var dark = stoneMat;
        var glow = Glow(PhaseColors[0]);

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        // legs and trunk
        Part(PrimitiveType.Cylinder, "LegL", visual, new Vector3(-1.5f, 1.4f, 0f), new Vector3(2.1f, 1.4f, 2.1f), dark);
        Part(PrimitiveType.Cylinder, "LegR", visual, new Vector3(1.5f, 1.4f, 0f), new Vector3(2.1f, 1.4f, 2.1f), dark);
        Part(PrimitiveType.Cylinder, "Trunk", visual, new Vector3(0f, 4.2f, 0f), new Vector3(4.6f, 2.0f, 3.6f), dark);
        Part(PrimitiveType.Cube, "Shoulders", visual, new Vector3(0f, 6.3f, 0f), new Vector3(8.6f, 1.7f, 3.4f), dark);

        // glowing rune bands around the trunk and a chest sigil
        Part(PrimitiveType.Cylinder, "BandLow", visual, new Vector3(0f, 2.9f, 0f), new Vector3(4.9f, 0.08f, 3.9f), glow);
        Part(PrimitiveType.Cylinder, "BandHigh", visual, new Vector3(0f, 5.2f, 0f), new Vector3(4.9f, 0.08f, 3.9f), glow);
        Part(PrimitiveType.Sphere, "Core", visual, new Vector3(0f, 4.2f, 1.75f), new Vector3(1.3f, 1.3f, 0.5f), glow);

        // head with an eye slit
        head = Part(PrimitiveType.Cube, "Head", visual, new Vector3(0f, 8.2f, 0.1f), new Vector3(3.0f, 2.4f, 2.8f), dark);
        Part(PrimitiveType.Cube, "Eye", head, new Vector3(0f, 0.1f, 0.52f), new Vector3(0.85f, 0.14f, 0.1f), glow);

        // arms hang from pivots at the shoulders so they can be swung
        armL = new GameObject("ArmL").transform;
        armL.SetParent(visual, false);
        armL.localPosition = new Vector3(-5.1f, 6.4f, 0f);
        Part(PrimitiveType.Cube, "ArmMesh", armL, new Vector3(0f, -2.6f, 0f), new Vector3(1.7f, 5.4f, 1.7f), dark);
        Part(PrimitiveType.Sphere, "Fist", armL, new Vector3(0f, -5.6f, 0f), new Vector3(2.6f, 2.6f, 2.6f), dark);
        Part(PrimitiveType.Cylinder, "Rune", armL, new Vector3(0f, -1.2f, 0f), new Vector3(1.85f, 0.07f, 1.85f), glow);

        armR = new GameObject("ArmR").transform;
        armR.SetParent(visual, false);
        armR.localPosition = new Vector3(5.1f, 6.4f, 0f);
        Part(PrimitiveType.Cube, "ArmMesh", armR, new Vector3(0f, -2.6f, 0f), new Vector3(1.7f, 5.4f, 1.7f), dark);
        Part(PrimitiveType.Sphere, "Fist", armR, new Vector3(0f, -5.6f, 0f), new Vector3(2.6f, 2.6f, 2.6f), dark);
        Part(PrimitiveType.Cylinder, "Rune", armR, new Vector3(0f, -1.2f, 0f), new Vector3(1.85f, 0.07f, 1.85f), glow);
    }

    void SetPhaseColor(int phase)
    {
        var c = PhaseColors[Mathf.Clamp(phase - 1, 0, PhaseColors.Length - 1)];
        foreach (var m in glowMats)
        {
            if (m == null) continue;
            if (m.HasProperty("_EmissionStrength")) { m.SetColor("_EmissionColor", c); m.SetColor("_OutlineColor", c * 0.4f); }
            else SpellVisuals.SetColor(m, c);
        }
    }

    void LateUpdate()
    {
        if (visual == null || dead) return;

        float k = 1f - Mathf.Exp(-9f * Time.deltaTime);
        armRaiseShown = Mathf.Lerp(armRaiseShown, armRaise, k);
        liftShown = Mathf.Lerp(liftShown, lift, k);

        float sway = Mathf.Sin(Time.time * 2.2f) * 4f;
        float swing = Mathf.Sin(Time.time * 3.2f) * armSwing;
        armL.localRotation = Quaternion.Euler(-armRaiseShown * 170f + swing, 0f, sway * 0.4f - 4f);
        armR.localRotation = Quaternion.Euler(-armRaiseShown * 170f - swing, 0f, -sway * 0.4f + 4f);
        visual.localPosition = new Vector3(0f, liftShown + Mathf.Sin(Time.time * 1.6f) * 0.08f, 0f);
        head.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -12f, headGlow), 0f, 0f);

        // the legs swing while it walks
        if (legL != null && legR != null)
        {
            float speed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            float w = Mathf.Clamp01(speed / 2.5f);
            legPhase += Time.deltaTime * (2f + speed * 1.1f);
            legL.localRotation = Quaternion.Euler(Mathf.Sin(legPhase) * 28f * w, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-Mathf.Sin(legPhase) * 28f * w, 0f, 0f);
            visual.localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(legPhase)) * 0.18f * w;
        }
    }

    // ---------- boss bar and warnings ----------

    void BuildBar()
    {
        var canvas = UIKit.MakeCanvas("BossBar", 90);
        canvas.transform.SetParent(transform, false);
        barRoot = canvas.gameObject;

        nameText = UIKit.Label(canvas.transform, "Name", bossName.ToUpper(), 34, Color.white, TextAnchor.MiddleCenter, true);
        UIKit.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(1000f, 50f));

        var back = UIKit.Box(canvas.transform, "Back", new Color(0.04f, 0.04f, 0.07f, 0.92f));
        UIKit.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1000f, 28f));

        var chip = UIKit.Box(back.transform, "Chip", new Color(1f, 0.95f, 0.8f, 0.9f));
        UIKit.Stretch(chip.rectTransform);
        barChip = chip.rectTransform;

        var fill = UIKit.Box(back.transform, "Fill", new Color(0.85f, 0.18f, 0.2f, 1f));
        UIKit.Stretch(fill.rectTransform);
        barFill = fill.rectTransform;

        // phase marks at 66% and 33%
        foreach (float mark in new[] { 0.66f, 0.33f })
        {
            var tick = UIKit.Box(back.transform, "Tick", new Color(1f, 1f, 1f, 0.8f));
            tick.rectTransform.anchorMin = new Vector2(mark, 0f);
            tick.rectTransform.anchorMax = new Vector2(mark, 1f);
            tick.rectTransform.sizeDelta = new Vector2(3f, 0f);
        }

        warnText = UIKit.Label(canvas.transform, "Warning", "", 36, new Color(1f, 0.7f, 0.25f), TextAnchor.MiddleCenter, true);
        UIKit.Place(warnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -165f), new Vector2(1500f, 60f));

        bannerText = UIKit.Label(canvas.transform, "Banner", bossName.ToUpper(), 96, new Color(1f, 0.82f, 0.35f, 0f), TextAnchor.MiddleCenter, true);
        UIKit.Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1900f, 140f));

        barRoot.SetActive(false);
    }

    void Say(string text, float seconds = 2.2f)
    {
        warnText.text = text;
        warnUntil = Time.time + seconds;
    }

    void Update()
    {
        if (dead) return;

        // phases follow the health
        float f = health.Current / Mathf.Max(health.Max, 1f);
        if (Phase == 1 && f <= 0.66f) EnterPhase(2);
        else if (Phase == 2 && f <= 0.33f) EnterPhase(3);

        // bar
        shownFill = Mathf.MoveTowards(shownFill, f, Time.deltaTime * 2f);
        chipFill = Mathf.MoveTowards(chipFill, f, Time.deltaTime * 0.25f);
        if (barFill != null) barFill.anchorMax = new Vector2(f, 1f);
        if (barChip != null) barChip.anchorMax = new Vector2(Mathf.Max(chipFill, f), 1f);
        if (warnText != null && Time.time > warnUntil) warnText.text = "";
        if (bannerText != null)
        {
            float a = bannerUntil > Time.time ? Mathf.Clamp01((bannerUntil - Time.time) / 0.8f) : 0f;
            var c = bannerText.color; c.a = a; bannerText.color = c;
        }
    }

    void EnterPhase(int phase)
    {
        Phase = phase;
        pendingRoar = true;
        SetPhaseColor(phase);
        if (phase == 3) StartCoroutine(HazardLoop());
    }

    // ---------- the fight ----------

    Vector3 PlayerChest => player.position + Vector3.up * 1.1f;
    float PlayerHeight => player.position.y - floorY;

    void Hurt(float amount, Vector3 point)
    {
        if (playerHealth != null && !playerHealth.IsDead) playerHealth.TakeDamage(amount * damageScale, point);
    }

    void FaceTarget(Vector3 target, float degreesPerSecond)
    {
        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), degreesPerSecond * Time.deltaTime);
    }

    void Stop()
    {
        if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
    }

    IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(introDelay);
        if (player == null) yield break;

        FightStartedAt = Time.time;
        barRoot.SetActive(true);
        bannerUntil = Time.time + 3f;
        SpellFX.Play(Sfx.Wave, transform.position, 1f);
        CameraShake.Add(0.5f);
        Say("Defeat the " + bossName + "!", 3f);

        while (!dead)
        {
            if (pendingRoar) { pendingRoar = false; yield return Roar(); }

            yield return Chase(UnityEngine.Random.Range(1.4f, 2.6f) / Tempo);
            if (dead) yield break;
            yield return PickAttack();
            yield return new WaitForSeconds(Mathf.Max(0.25f, 0.9f / Tempo));
        }
    }

    IEnumerator Roar()
    {
        Stop();
        Say(Phase == 2 ? "PHASE 2 - it uses new attacks!" : "PHASE 3 - the ground burns, the slam grounds you!", 3.2f);
        SpellFX.Play(Sfx.Wave, transform.position, 1f);
        armRaise = 1f;
        headGlow = 1f;
        CameraShake.Add(0.7f);
        yield return new WaitForSeconds(1.4f);
        armRaise = 0f;
        headGlow = 0f;
    }

    IEnumerator Chase(float seconds)
    {
        float end = Time.time + seconds;
        float next = 0f;
        armSwing = 14f;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;

        while (Time.time < end && !dead)
        {
            if (Time.time >= next)
            {
                next = Time.time + 0.3f;
                if (agent != null && agent.isOnNavMesh) agent.SetDestination(player.position);
            }

            if (agent == null)      // no NavMesh: walk straight at the player
            {
                Vector3 to = player.position - transform.position; to.y = 0f;
                if (to.magnitude > 4f) transform.position += to.normalized * walkSpeed * Time.deltaTime;
            }
            FaceTarget(player.position, 70f);
            yield return null;
        }

        armSwing = 0f;
        Stop();
    }

    IEnumerator PickAttack()
    {
        // what it may use grows with the phase
        var pool = new List<int> { 0, 1 };           // 0 slam, 1 fan of shots
        if (Phase >= 2) { pool.Add(2); pool.Add(3); }  // 2 beam, 3 meteor rain
        if (Phase >= 2) pool.Add(1);
        if (Phase >= 3) { pool.Add(0); pool.Add(4); }  // 4 ring / spiral of shots

        int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        switch (pick)
        {
            case 0: return Slam();
            case 1: return Barrage(0);
            case 2: return Beam(Phase >= 3 ? 2 : 1);
            case 3: return MeteorRain(Phase >= 3 ? 9 : 6);
            default: return Barrage(Phase >= 3 && UnityEngine.Random.value < 0.5f ? 2 : 1);
        }
    }

    // ---------- helpers for the telegraphs ----------

    LineRenderer MakeRing(Color color, float width)
    {
        var go = new GameObject("Telegraph");
        go.transform.SetParent(fxRoot.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.sharedMaterial = SpellVisuals.Make(color, true);
        lr.startWidth = lr.endWidth = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCapVertices = 2;
        return lr;
    }

    // an arc of a circle on the floor; the sector around gapCenter (degrees) is left open
    void SetArc(LineRenderer lr, Vector3 center, float radius, float gapCenter, float gapWidth)
    {
        const int count = 100;
        float start = gapCenter + gapWidth * 0.5f;
        float span = 360f - gapWidth;
        lr.positionCount = count + 1;
        for (int i = 0; i <= count; i++)
        {
            float a = (start + span * i / count) * Mathf.Deg2Rad;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, floorY + 0.12f, center.z + Mathf.Sin(a) * radius));
        }
    }

    // only the gap itself
    void SetSector(LineRenderer lr, Vector3 center, float radius, float gapCenter, float gapWidth)
    {
        const int count = 20;
        lr.positionCount = count + 1;
        for (int i = 0; i <= count; i++)
        {
            float a = (gapCenter - gapWidth * 0.5f + gapWidth * i / count) * Mathf.Deg2Rad;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, floorY + 0.14f, center.z + Mathf.Sin(a) * radius));
        }
    }

    static void SetColor(LineRenderer lr, Color c)
    {
        SpellVisuals.SetColor(lr.sharedMaterial, c);
    }

    void Kill(LineRenderer lr)
    {
        if (lr == null) return;
        if (lr.sharedMaterial != null) Destroy(lr.sharedMaterial);
        Destroy(lr.gameObject);
    }

    // ---------- attack 1: ground slam (not blockable: jump, fly or find the gap) ----------

    IEnumerator Slam()
    {
        Stop();
        float wind = 1.9f / Tempo;
        const float maxRadius = 30f;
        float gapWidth = Mathf.Lerp(62f, 44f, (Phase - 1) / 2f);

        // the safe gap opens toward the player's side, so it can be reached
        Vector3 c = transform.position;
        Vector3 to = player.position - c;
        float gapCenter = Mathf.Atan2(to.z, to.x) * Mathf.Rad2Deg + UnityEngine.Random.Range(-45f, 45f);

        var outline = MakeRing(new Color(1f, 0.25f, 0.2f, 0.55f), 0.4f);
        var fill = MakeRing(new Color(1f, 0.55f, 0.2f, 0.75f), 0.6f);
        var safe = MakeRing(new Color(0.3f, 1f, 0.45f, 0.95f), 1.1f);
        SetArc(outline, c, maxRadius, gapCenter, gapWidth);
        SetSector(safe, c, maxRadius - 0.6f, gapCenter, gapWidth);

        bool grounding = Phase >= 3;
        if (grounding) GroundingAura.Set(this, true);
        Say(grounding ? "SLAM - GROUNDED! stand in the green gap or jump" : "SLAM - jump, fly over it, or stand in the green gap", wind + 0.8f);
        SpellFX.Play(Sfx.Charge, transform.position, 1f);

        float t0 = Time.time;
        while (Time.time - t0 < wind && !dead)
        {
            float k = (Time.time - t0) / wind;
            armRaise = Mathf.SmoothStep(0f, 1f, k);
            lift = 1.2f * k;
            SetArc(fill, c, maxRadius * k, gapCenter, gapWidth);
            FaceTarget(player.position, 50f);
            yield return null;
        }

        // the hit
        armRaise = 0f;
        lift = 0f;
        CameraShake.Add(0.8f);
        SpellFX.Play(Sfx.Death, c, 0.9f);
        SpellFX.Explosion(c + Vector3.up * 0.3f, 4f, new Color(1f, 0.6f, 0.3f));
        Kill(fill);

        var wave = MakeRing(new Color(1f, 0.7f, 0.3f, 0.95f), 1.4f);
        float radius = 1f, speed = 17f;
        bool struck = false;
        while (radius < maxRadius && !dead)
        {
            radius += speed * Time.deltaTime;
            SetArc(wave, c, radius, gapCenter, gapWidth);

            if (!struck)
            {
                Vector3 d = player.position - c;
                d.y = 0f;
                float dist = d.magnitude;
                float angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
                bool inGap = Mathf.Abs(Mathf.DeltaAngle(angle, gapCenter)) < gapWidth * 0.5f;
                if (Mathf.Abs(dist - radius) < 1.8f && PlayerHeight < 0.9f && !inGap)
                {
                    struck = true;
                    Hurt(slamDamage, player.position);
                    CameraShake.Add(0.4f);
                }
            }
            yield return null;
        }

        Kill(wave);
        Kill(outline);
        Kill(safe);
        if (grounding)
        {
            yield return new WaitForSeconds(0.8f);
            GroundingAura.Set(this, false);
        }
    }

    // ---------- attack 2: shots (the barrier stops them) ----------

    IEnumerator Barrage(int kind)      // 0 fan, 1 ring, 2 spiral
    {
        Stop();
        armRaise = 0.55f;
        string label = kind == 0 ? "SHOTS - the barrier stops them" : kind == 1 ? "RING OF SHOTS - find a gap or use the barrier" : "SPIRAL - keep moving, or use the barrier";
        Say(label, 2.5f);
        SpellFX.Play(Sfx.Charge, transform.position, 0.8f);
        float wind = 0.9f / Tempo;
        float t0 = Time.time;
        while (Time.time - t0 < wind && !dead) { FaceTarget(player.position, 90f); yield return null; }

        Color color = PhaseColors[Mathf.Clamp(Phase - 1, 0, 2)];
        Vector3 chest = transform.position + Vector3.up * 3.6f;

        if (kind == 0)
        {
            for (int volley = 0; volley < 3 && !dead; volley++)
            {
                Vector3 aim = (PlayerChest - chest).normalized;
                for (int i = -3; i <= 3; i++)
                    BossProjectile.Spawn(chest + transform.forward * 2f, Quaternion.AngleAxis(i * 9f, Vector3.up) * aim, 17f, shotDamage * damageScale, color);
                SpellFX.Play(Sfx.Cast, chest, 0.9f);
                yield return new WaitForSeconds(0.6f / Tempo);
            }
        }
        else if (kind == 1)
        {
            for (int ring = 0; ring < 2 && !dead; ring++)
            {
                for (int i = 0; i < 18; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 20f + ring * 10f, 0f) * Vector3.forward;
                    BossProjectile.Spawn(transform.position + dir * 3.2f + Vector3.up * 1.3f, dir, 11f, shotDamage * damageScale, color);
                }
                SpellFX.Play(Sfx.Cast, chest, 0.9f);
                yield return new WaitForSeconds(0.95f / Tempo);
            }
        }
        else
        {
            for (int i = 0; i < 40 && !dead; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 23f, 0f) * Vector3.forward;
                BossProjectile.Spawn(transform.position + dir * 3.2f + Vector3.up * 1.3f, dir, 12f, shotDamage * damageScale, color);
                yield return new WaitForSeconds(0.07f);
            }
        }

        armRaise = 0f;
        yield return new WaitForSeconds(0.4f);
    }

    // ---------- attack 3: targeting beam (the barrier takes it, pillars block it) ----------

    IEnumerator Beam(int shots)
    {
        Stop();
        headGlow = 1f;
        Say("BEAM - break line of sight or raise the barrier", 3f);
        SpellFX.Play(Sfx.Charge, transform.position, 1f);

        for (int s = 0; s < shots && !dead; s++)
        {
            float wind = (s == 0 ? 2.3f : 1.0f) / Tempo;
            var line = MakeRing(new Color(1f, 0.2f, 0.2f, 0.8f), 0.18f);
            line.positionCount = 2;
            Vector3 aim = (PlayerChest - head.position).normalized;
            float t0 = Time.time;

            while (Time.time - t0 < wind && !dead)
            {
                float k = (Time.time - t0) / wind;
                if (k < 0.6f)       // it keeps following for most of the warning, then locks
                    aim = Vector3.RotateTowards(aim, (PlayerChest - head.position).normalized, 70f * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;

                Vector3 origin = head.position;
                Vector3 end = origin + aim * 90f;
                if (Physics.Raycast(origin, aim, out RaycastHit probe, 90f, solidMask, QueryTriggerInteraction.Ignore)) end = probe.point;
                line.SetPosition(0, origin);
                line.SetPosition(1, end);

                float w = Mathf.Lerp(0.12f, 0.5f, k * k);
                line.startWidth = line.endWidth = w;
                SetColor(line, Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.7f), new Color(1f, 1f, 1f, 1f), k * k));
                FaceTarget(origin + aim * 10f, 70f);
                yield return null;
            }

            // fire
            Vector3 from = head.position;
            Vector3 hitEnd = from + aim * 90f;
            if (Physics.SphereCast(from, 0.6f, aim, out RaycastHit hit, 90f, solidMask, QueryTriggerInteraction.Ignore))
            {
                hitEnd = from + aim * hit.distance;
                var barrier = hit.collider.GetComponentInParent<Barrier>();
                if (barrier != null) { if (barrier.Blocks("Player")) barrier.Hit(beamDamage * damageScale, false, hit.point); }
                else if (hit.collider.CompareTag("Player")) Hurt(beamDamage, hit.point);
            }
            LaserBeam.Spawn(from, hitEnd, 1.2f, new Color(1f, 0.35f, 0.3f), 0.45f);
            SpellFX.Play(Sfx.Laser, from, 1f);
            CameraShake.Add(0.35f);
            Kill(line);
            yield return new WaitForSeconds(0.45f);
        }

        headGlow = 0f;
    }

    // ---------- attack 4: meteor rain (telegraphed circles) ----------

    IEnumerator MeteorRain(int count)
    {
        Stop();
        armRaise = 1f;
        Say("METEORS - keep moving, leave the red circles", 3f);
        SpellFX.Play(Sfx.Charge, transform.position, 1f);
        yield return new WaitForSeconds(0.9f / Tempo);

        for (int i = 0; i < count && !dead; i++)
        {
            Vector3 target = player.position;
            if (i >= 2)      // the first ones land where you are, the rest scatter around you
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 11f;
                target += new Vector3(offset.x, 0f, offset.y);
            }
            target.y = floorY;
            StartCoroutine(MeteorStrike(target, 5.2f, 1.5f / Mathf.Min(Tempo, 1.4f)));
            yield return new WaitForSeconds(0.38f / Tempo);
        }

        yield return new WaitForSeconds(1.0f);
        armRaise = 0f;
        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator MeteorStrike(Vector3 pos, float radius, float delay)
    {
        var outline = MakeRing(new Color(1f, 0.25f, 0.2f, 0.75f), 0.35f);
        var fill = MakeRing(new Color(1f, 0.6f, 0.25f, 0.8f), 0.5f);
        SetArc(outline, pos, radius, 0f, 0f);

        Transform rock = null;
        float t0 = Time.time;
        while (Time.time - t0 < delay && !dead)
        {
            float k = (Time.time - t0) / delay;
            SetArc(fill, pos, radius * k, 0f, 0f);

            // the rock itself comes down for the last half second
            float fall = Mathf.Clamp01((k * delay - (delay - 0.5f)) / 0.5f);
            if (fall > 0f)
            {
                if (rock == null)
                {
                    rock = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    Destroy(rock.GetComponent<Collider>());
                    rock.SetParent(fxRoot.transform, false);
                    rock.localScale = Vector3.one * 3.2f;
                    rock.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 0.5f, 0.2f));
                }
                rock.position = pos + Vector3.up * Mathf.Lerp(40f, 1.5f, fall * fall);
            }
            yield return null;
        }

        Kill(outline);
        Kill(fill);
        if (rock != null) Destroy(rock.gameObject);
        if (dead) yield break;

        SpellFX.Explosion(pos + Vector3.up * 0.4f, radius, new Color(1f, 0.55f, 0.2f));
        CameraShake.Add(0.3f);
        Vector3 d = player.position - pos;
        d.y = 0f;
        if (d.magnitude < radius && PlayerHeight < 6f) Hurt(meteorDamage, player.position);
    }

    // ---------- phase 3: damaging floor patches ----------

    IEnumerator HazardLoop()
    {
        var active = new Queue<GameObject>();
        while (!dead)
        {
            yield return new WaitForSeconds(3.2f / Tempo);
            if (dead) yield break;

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 5f;
            Vector3 pos = player.position + new Vector3(jitter.x, 0f, jitter.y);
            pos.y = floorY;
            while (active.Count >= 6) { var old = active.Dequeue(); if (old != null) Destroy(old); }
            StartCoroutine(Patch(pos, active));
        }
    }

    IEnumerator Patch(Vector3 pos, Queue<GameObject> list)
    {
        const float radius = 4.6f;

        var warn = MakeRing(new Color(1f, 0.3f, 0.2f, 0.7f), 0.35f);
        SetArc(warn, pos, radius, 0f, 0f);
        yield return new WaitForSeconds(1.2f);
        Kill(warn);
        if (dead) yield break;

        // the burning patch
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "FloorHazard";
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(fxRoot.transform, false);
        disc.transform.position = pos + Vector3.up * 0.06f;
        disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        var mat = SpellVisuals.Make(new Color(1f, 0.25f, 0.15f, 0.45f), true);
        disc.GetComponent<Renderer>().sharedMaterial = mat;
        list.Enqueue(disc);

        float end = Time.time + 10f;
        float nextTick = Time.time + 0.4f;
        while (Time.time < end && !dead && disc != null)
        {
            float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 6f);
            SpellVisuals.SetColor(mat, new Color(1f, 0.25f, 0.15f, pulse));

            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.5f;
                Vector3 d = player.position - pos;
                d.y = 0f;
                if (d.magnitude < radius && PlayerHeight < 1.4f) Hurt(hazardTickDamage, player.position);
            }
            yield return null;
        }

        if (mat != null) Destroy(mat);
        if (disc != null) Destroy(disc);
    }

    // ---------- death ----------

    void OnDied()
    {
        if (dead) return;
        dead = true;

        StopAllCoroutines();
        GroundingAura.Set(this, false);
        if (fxRoot != null) { Destroy(fxRoot); fxRoot = null; }
        if (agent != null) agent.enabled = false;
        if (warnText != null) warnText.text = "";
        if (barRoot != null) barRoot.SetActive(false);

        Defeated?.Invoke();
        StartCoroutine(Collapse());
    }

    IEnumerator Collapse()
    {
        SpellFX.Play(Sfx.Death, transform.position, 1f);
        CameraShake.Add(1f);
        armRaise = 0f;
        float t0 = Time.time;

        while (Time.time - t0 < 2.6f)
        {
            float k = (Time.time - t0) / 2.6f;
            visual.localPosition = new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), -k * 5.5f, UnityEngine.Random.Range(-0.15f, 0.15f));
            visual.localRotation = Quaternion.Euler(k * 14f, 0f, k * -9f);
            if (UnityEngine.Random.value < 0.12f)
                SpellFX.Explosion(transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(1f, 8f), UnityEngine.Random.Range(-3f, 3f)), 2f, PhaseColors[2]);
            yield return null;
        }

        visual.gameObject.SetActive(false);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
