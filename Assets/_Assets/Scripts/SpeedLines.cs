using UnityEngine;

// Streaks that rush past the player against the direction of travel when flying fast.
// Built at runtime, so it needs no assets; BroomFlight feeds it the player position and velocity every frame.
public class SpeedLines : MonoBehaviour
{
    [SerializeField] float maxRate = 90f;       // streaks per second at full intensity
    [SerializeField] float ringRadius = 5f;     // streaks rush through a ring this wide around the flight path
    [SerializeField] float distance = 12f;      // streaks start this far ahead of the player and end this far behind
    [SerializeField] float streakSpeed = 40f;   // how fast they rush past
    [SerializeField] float streakLength = 3f;
    [SerializeField] float turnRate = 8f;       // how quickly the lines swing round to a new flight direction

    ParticleSystem ps;
    ParticleSystem.EmissionModule emission;
    Vector3 direction = Vector3.forward;

    public static SpeedLines Create()
    {
        return new GameObject("SpeedLines").AddComponent<SpeedLines>();
    }

    void Awake()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // so the main module can be changed

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;       // streaks travel with the player
        main.startLifetime = 2f * distance / streakSpeed;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(1f, 1f, 1f, 0.7f);
        main.maxParticles = 300;

        emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ringRadius;
        shape.radiusThickness = 0.3f;                                      // only the outer ring, so the player stays clear

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = 0f;
        velocity.y = 0f;
        velocity.z = -streakSpeed;                                         // local -Z is against the flight direction

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = streakLength;
        rend.velocityScale = 0.05f;
        rend.cameraVelocityScale = 0f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) rend.material = new Material(shader);

        ps.Play();
    }

    // intensity: 0 = no lines, 1 = full. center: the player. velocity: where the player is heading.
    public void SetState(float intensity, Vector3 center, Vector3 velocity)
    {
        emission.rateOverTime = Mathf.Clamp01(intensity) * maxRate;

        if (velocity.sqrMagnitude > 0.01f)
            direction = Vector3.Slerp(direction, velocity.normalized, 1f - Mathf.Exp(-turnRate * Time.deltaTime));

        transform.SetPositionAndRotation(center + direction * distance, Quaternion.LookRotation(direction));
    }
}
