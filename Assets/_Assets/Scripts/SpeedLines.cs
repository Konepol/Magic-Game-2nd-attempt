using UnityEngine;

// Streaks that rush past the camera, against the direction of travel, when flying fast.
// They are anchored to the camera (not the player), so they always frame the edge of the screen whatever the zoom,
// and they are driven by real speed: the faster you fly, the faster, longer and denser the streaks.
// Built at runtime, so it needs no assets; BroomFlight feeds it every frame.
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
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.65f);

    ParticleSystem ps;
    ParticleSystem.MainModule main;
    ParticleSystem.EmissionModule emission;
    ParticleSystem.VelocityOverLifetimeModule drift;
    Vector3 direction = Vector3.forward;
    float shown;   // smoothed intensity

    public static SpeedLines Create()
    {
        return SceneGroups.Put(new GameObject("SpeedLines"), "Systems").AddComponent<SpeedLines>();
    }

    void Awake()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // so the main module can be changed

        main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;       // streaks travel with the camera
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = color;
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
        SetStreakSpeed(minStreakSpeed);

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
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) rend.material = new Material(shader);

        ps.Play();
    }

    // local -Z is against the flight direction; the lifetime is whatever carries a streak from front to back
    void SetStreakSpeed(float streakSpeed)
    {
        drift.z = -streakSpeed;
        main.startLifetime = 2f * distance / streakSpeed;
    }

    // intensity: 0 = no lines, 1 = full. velocity: how the player is moving. view: the camera the lines wrap around.
    public void SetState(float intensity, Vector3 velocity, Transform view)
    {
        shown = Mathf.MoveTowards(shown, Mathf.Clamp01(intensity), fadeRate * Time.deltaTime);
        emission.rateOverTime = shown * maxRate;
        if (shown <= 0f && ps.particleCount == 0) return;   // nothing on screen: leave it where it is

        float speed = velocity.magnitude;
        if (speed > 0.1f)
            direction = Vector3.Slerp(direction, velocity / speed, 1f - Mathf.Exp(-turnRate * Time.deltaTime));

        SetStreakSpeed(Mathf.Max(minStreakSpeed, speed * streakSpeedFactor));
        main.startColor = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.45f, 1f, shown));   // faint at first, solid at full speed

        transform.SetPositionAndRotation(view.position + direction * distance, Quaternion.LookRotation(direction));
    }
}
