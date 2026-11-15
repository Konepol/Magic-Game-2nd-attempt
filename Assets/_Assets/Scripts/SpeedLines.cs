using UnityEngine;

// Streaks that rush past the camera, against the direction of travel, when flying fast.
// They are anchored to the camera (not the player), so they always frame the edge of the screen whatever the zoom,
// and they are driven by real speed: the faster you fly, the faster, longer and denser the streaks.
// The two particle systems (streaks, and a "Stars" child) are real objects in the scene: select them in the hierarchy and edit
// them like any Particle System. They are made by Tools > Magic > Build Rig Parts or this component's menu "Rebuild Parts";
// if they are missing the game makes them itself. BroomFlight feeds it every frame.
public class SpeedLines : MonoBehaviour
{
    [SerializeField] float maxRate = 140f;             // streaks per second at full intensity
    [SerializeField] float innerRadius = 2.5f;         // the streaks pass the camera between these two distances,
    [SerializeField] float outerRadius = 7f;           // so the middle of the screen stays clear
    [SerializeField] float distance = 18f;             // they start this far ahead of the camera and end this far behind
    [SerializeField] float streakSpeedFactor = 1.6f;   // how fast they rush past, as a multiple of the flight speed
    [SerializeField] float minStreakSpeed = 30f;
    [SerializeField] float lengthPerSpeed = 0.05f;     // metres of streak per m/s it travels
    [SerializeField] float turnRate = 10f;             // how quickly the lines swing round to a new flight direction
    [SerializeField] float fadeRate = 5f;              // how quickly they thicken and thin out
    // the look: soft glowing streaks in the spell colours (arcane lavender and warm gold), with a few of the
    // chubby little stars the spell effects use tumbling past among them
    [SerializeField] Color color = new Color(0.78f, 0.72f, 1f, 0.7f);
    [SerializeField] Color colorWarm = new Color(1f, 0.9f, 0.62f, 0.7f);
    [SerializeField] float streakWidth = 0.16f;        // soft-edged, so wider than a hard line would be
    [SerializeField, Range(0f, 0.5f)] float starShare = 0.12f;   // stars per streak
    [SerializeField] float starSize = 0.2f;

    [Header("Parts in the scene (made by Build Rig Parts; edit them directly)")]
    [SerializeField] ParticleSystem ps;
    [SerializeField] ParticleSystem stars;
    [SerializeField] Material streakMaterial;
    [SerializeField] Material starMaterial;
    [SerializeField] Mesh starMesh;

    ParticleSystem.EmissionModule starEmission;
    ParticleSystem.VelocityOverLifetimeModule starDrift;
    ParticleSystem.MainModule starMain;
    ParticleSystem.MainModule main;
    ParticleSystem.EmissionModule emission;
    ParticleSystem.VelocityOverLifetimeModule drift;
    Vector3 direction = Vector3.forward;
    float shown;   // smoothed intensity

    // the one made in the scene, if there is one; otherwise a new one is made now
    public static SpeedLines Create()
    {
        var inScene = FindAnyObjectByType<SpeedLines>(FindObjectsInactive.Include);
        if (inScene != null) { inScene.gameObject.SetActive(true); return inScene; }
        return SceneGroups.Put(new GameObject("SpeedLines"), "Systems").AddComponent<SpeedLines>();
    }

    void Awake()
    {
        if (ps == null || stars == null) { BuildParts(); Attach(); }
        else Attach();
    }

    // the parts made in the scene are ready; only the modules this script drives need picking up
    void Attach()
    {
        main = ps.main; emission = ps.emission; drift = ps.velocityOverLifetime;
        starMain = stars.main; starEmission = stars.emission; starDrift = stars.velocityOverLifetime;
        SetStreakSpeed(minStreakSpeed);
        ps.Play();
        stars.Play();
    }

    public void BuildInScene(Material streakMat, Material starMat, Mesh star)
    {
        streakMaterial = streakMat; starMaterial = starMat; starMesh = star;
        BuildParts();
    }

    [ContextMenu("Rebuild Parts")]
    void BuildParts()
    {
        if (stars != null) { if (Application.isPlaying) Destroy(stars.gameObject); else DestroyImmediate(stars.gameObject); stars = null; }
        if (ps != null) { if (Application.isPlaying) Destroy(ps); else DestroyImmediate(ps); ps = null; }
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // so the main module can be changed

        main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;       // streaks travel with the camera
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(streakWidth * 0.5f, streakWidth);
        main.startColor = new ParticleSystem.MinMaxGradient(color, colorWarm);
        main.maxParticles = 400;

        emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = outerRadius;
        shape.radiusThickness = 1f - Mathf.Clamp01(innerRadius / Mathf.Max(outerRadius, 0.01f));

        drift = ps.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.Local;
        drift.x = 0f;
        drift.y = 0f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 1f;
        rend.velocityScale = lengthPerSpeed;                               // length follows how fast the streak moves
        rend.cameraVelocityScale = 0f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = streakMaterial != null ? streakMaterial : MagicFX.Unlit(true, MagicFX.SoftDot(32, 1.5f));    // a soft dot, stretched into a glowing streak

        BuildStars(fade.color);
        SetStreakSpeed(minStreakSpeed);
    }

    // a second, sparse system inside the same ring: small 3D stars that tumble as they rush past
    void BuildStars(ParticleSystem.MinMaxGradient fadeColor)
    {
        var go = new GameObject("Stars");
        go.transform.SetParent(transform, false);
        stars = go.AddComponent<ParticleSystem>();
        stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        starMain = stars.main;
        starMain.playOnAwake = false;
        starMain.loop = true;
        starMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        starMain.startSpeed = 0f;
        starMain.startSize = new ParticleSystem.MinMaxCurve(starSize * 0.5f, starSize);
        starMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(color.r, color.g, color.b, 1f), new Color(colorWarm.r, colorWarm.g, colorWarm.b, 1f));
        starMain.startRotation3D = true;
        starMain.startRotationX = starMain.startRotationY = starMain.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        starMain.maxParticles = 80;

        starEmission = stars.emission;
        starEmission.rateOverTime = 0f;

        var shape = stars.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = outerRadius;
        shape.radiusThickness = 1f - Mathf.Clamp01(innerRadius / Mathf.Max(outerRadius, 0.01f));

        starDrift = stars.velocityOverLifetime;
        starDrift.enabled = true;
        starDrift.space = ParticleSystemSimulationSpace.Local;
        starDrift.x = 0f;
        starDrift.y = 0f;

        var spin = stars.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var fade = stars.colorOverLifetime;
        fade.enabled = true;
        fade.color = fadeColor;

        var rend = stars.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.mesh = starMesh != null ? starMesh : MagicFX.StarMesh();
        rend.sharedMaterial = starMaterial != null ? starMaterial : MagicFX.Unlit(false);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    // local -Z is against the flight direction; the lifetime is whatever carries a streak from front to back
    void SetStreakSpeed(float streakSpeed)
    {
        drift.z = -streakSpeed;
        main.startLifetime = 2f * distance / streakSpeed;
        starDrift.z = -streakSpeed;
        starMain.startLifetime = 2f * distance / streakSpeed;
    }

    // intensity: 0 = no lines, 1 = full. velocity: how the player is moving. view: the camera the lines wrap around.
    public void SetState(float intensity, Vector3 velocity, Transform view)
    {
        shown = Mathf.MoveTowards(shown, Mathf.Clamp01(intensity), fadeRate * Time.deltaTime);
        emission.rateOverTime = shown * maxRate;
        starEmission.rateOverTime = shown * maxRate * starShare;
        if (shown <= 0f && ps.particleCount == 0 && stars.particleCount == 0) return;   // nothing on screen: leave it where it is

        float speed = velocity.magnitude;
        if (speed > 0.1f)
            direction = Vector3.Slerp(direction, velocity / speed, 1f - Mathf.Exp(-turnRate * Time.deltaTime));

        SetStreakSpeed(Mathf.Max(minStreakSpeed, speed * streakSpeedFactor));
        float a = Mathf.Lerp(0.45f, 1f, shown);   // faint at first, solid at full speed
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(color.r, color.g, color.b, color.a * a), new Color(colorWarm.r, colorWarm.g, colorWarm.b, colorWarm.a * a));

        transform.SetPositionAndRotation(view.position + direction * distance, Quaternion.LookRotation(direction));
    }
}
