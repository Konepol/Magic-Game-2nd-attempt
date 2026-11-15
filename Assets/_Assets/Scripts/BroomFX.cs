using UnityEngine;

// The look of the broom in flight, in the same style as the spell effects (soft glow, little tumbling stars,
// arcane lavender warming to gold):
//  - a glowing ribbon and a trickle of star dust from the tail of the broom, growing with speed and with the boost
//  - a small star pop at the tail on every dash
//  - the rider tucks forward over the broom the faster she flies
// The ribbon and the star dust are real objects in the scene (a "BroomTail" under the broom model, with a "StarDust" child):
// select them in the hierarchy and change the TrailRenderer and the Particle System like any other. They are made by
// Tools > Magic > Build Rig Parts, or by this component's menu "Rebuild Parts". If they are missing the game makes them itself.
public class BroomFX : MonoBehaviour
{
    [Header("Tail Trail")]
    [SerializeField] Color color = new Color(0.72f, 0.62f, 1f, 1f);
    [SerializeField] Color colorWarm = new Color(1f, 0.86f, 0.45f, 1f);
    [SerializeField] float ribbonWidth = 0.55f;        // at the boost top speed
    [SerializeField] float ribbonTime = 0.45f;         // seconds the ribbon lingers
    [SerializeField] float starsPerMetre = 0.7f;
    [SerializeField] float starSize = 0.16f;

    [Header("Dash Pop")]
    [SerializeField] float popRadius = 0.9f;
    [SerializeField] int popStars = 6;

    [Header("Rider")]
    [SerializeField, Tooltip("Degrees she leans forward over the broom at the boost top speed.")]
    float speedTuck = 22f;

    [Header("Parts in the scene (made by Build Rig Parts; edit them directly)")]
    [SerializeField] Transform tail;
    [SerializeField] TrailRenderer ribbon;
    [SerializeField] ParticleSystem dust;
    [SerializeField] Material ribbonMaterial;
    [SerializeField] Material dustMaterial;
    [SerializeField] Mesh starMesh;

    BroomFlight broom;
    Transform spine;
    ParticleSystem.EmissionModule dustEmission;
    int seenDashes;
    bool wasFlying;
    float tuck;

    void Awake()
    {
        broom = GetComponent<BroomFlight>();
        var animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (tail != null) AttachParts();
    }

    [ContextMenu("Rebuild Parts")]
    void RebuildParts()
    {
        if (broom == null) broom = GetComponent<BroomFlight>();
        if (tail != null) { if (Application.isPlaying) Destroy(tail.gameObject); else DestroyImmediate(tail.gameObject); }
        tail = null; ribbon = null; dust = null;
        BuildTail();
        if (!Application.isPlaying) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void AttachParts()
    {
        if (dust == null) return;
        dustEmission = dust.emission;
        if (ribbon != null) ribbon.Clear();
        dust.Play();
    }

    // The tail is found from the broom model itself: the end of it that points backward. It can only be measured
    // once the model is switched on, so this runs on the first mount.
    void BuildTail()
    {
        Transform model = broom.BroomModel;
        if (model == null) return;

        // measured in the model's own space (its nose is +Z), so it does not matter where the broom is right now
        bool any = false;
        Bounds local = new Bounds();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || (Application.isPlaying && !mr.enabled)) continue;
            Bounds b = mf.sharedMesh.bounds;
            Matrix4x4 toModel = model.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)   // the mesh's box, corner by corner
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = toModel.MultiplyPoint3x4(corner);
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                else local.Encapsulate(p);
            }
        }
        if (!any) return;

        tail = new GameObject("BroomTail").transform;
        tail.SetParent(model, false);
        tail.localPosition = new Vector3(local.center.x, local.center.y, local.min.z);

        // the ribbon: a soft glow that tapers away behind the broom
        ribbon = tail.gameObject.AddComponent<TrailRenderer>();
        ribbon.sharedMaterial = ribbonMaterial != null ? ribbonMaterial : MagicFX.Unlit(true, MagicFX.SoftDot(32, 1.5f));
        ribbon.time = ribbonTime;
        ribbon.minVertexDistance = 0.15f;
        ribbon.numCapVertices = 4;
        ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ribbon.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(colorWarm, 0f), new GradientColorKey(color, 0.35f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        ribbon.colorGradient = gradient;

        // star dust: small 3D stars left hanging in the air behind the broom
        var dustGo = new GameObject("StarDust");
        dustGo.transform.SetParent(tail, false);
        dust = dustGo.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(starSize * 0.5f, starSize);
        main.startColor = new ParticleSystem.MinMaxGradient(color, colorWarm);
        main.startRotation3D = true;
        main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 200;

        dustEmission = dust.emission;
        dustEmission.rateOverTime = 0f;
        dustEmission.rateOverDistance = 0f;

        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var spin = dust.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        var shrink = dust.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

        var rend = dust.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.mesh = starMesh != null ? starMesh : MagicFX.StarMesh();
        rend.sharedMaterial = dustMaterial != null ? dustMaterial : MagicFX.Unlit(false);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        dust.Play();
    }

    // the same, for the builder: also called outside play mode
    public void BuildInScene(Material ribbonMat, Material dustMat, Mesh star, BroomFlight b)
    {
        ribbonMaterial = ribbonMat; dustMaterial = dustMat; starMesh = star; broom = b;
        RebuildParts();
    }

    void Update()
    {
        if (broom == null) return;

        // the trail follows the broom, not the rider: it also runs while the broom flies on without her
        bool flying = broom.IsFlying;
        bool shown = flying || broom.BroomAirborne;
        if (shown && !wasFlying)
        {
            if (tail == null) BuildTail();
            if (ribbon != null) ribbon.Clear();
            seenDashes = broom.DashCount;
        }
        wasFlying = shown;
        if (tail == null) return;
        if (!shown)
        {
            ribbon.widthMultiplier = 0f;
            dustEmission.rateOverDistance = 0f;
            return;
        }

        // nothing while hovering, a thin thread at cruise speed, a wide bright ribbon at full boost
        float fast = broom.BroomSpeedFraction;
        float moving = Mathf.Clamp01(broom.BroomSpeed / 6f);
        float boost = flying && broom.IsBoosting ? 1.5f : 1f;
        ribbon.widthMultiplier = moving * Mathf.Lerp(0.14f, ribbonWidth, fast) * boost;
        dustEmission.rateOverDistance = starsPerMetre * Mathf.Lerp(0.5f, 1f, fast) * boost;

        // every dash: a little burst of stars where the broom kicked off
        if (flying && broom.DashCount != seenDashes)
        {
            seenDashes = broom.DashCount;
            MagicFX.Pop(tail.position, popRadius, color, popStars);
        }
    }

    // after the animator, so the tuck sits on top of the riding pose
    void LateUpdate()
    {
        if (spine == null || broom == null) return;

        tuck = Mathf.MoveTowards(tuck, broom.IsFlying ? broom.SpeedFraction * speedTuck : 0f, 60f * Time.deltaTime);
        if (tuck > 0.01f) spine.rotation = Quaternion.AngleAxis(tuck, transform.right) * spine.rotation;
    }
}
