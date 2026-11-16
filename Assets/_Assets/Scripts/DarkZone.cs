using UnityEngine;

// A dark test area (a cave or hall): while the player is inside, the ambient light drops, so a lantern's light can be judged.
// Needs a trigger collider on the same object. Walls and a roof (which also block the sun) are built by Tools > Magic > Spawn Dark Test Area.
[RequireComponent(typeof(Collider))]
public class DarkZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float ambientInside = 0.03f;
    [SerializeField, Range(0f, 1f)] float reflectionInside = 0.05f;

    float savedAmbient, savedReflection;
    bool inside;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (inside || !other.CompareTag("Player")) return;
        inside = true;
        savedAmbient = RenderSettings.ambientIntensity;
        savedReflection = RenderSettings.reflectionIntensity;
        RenderSettings.ambientIntensity = ambientInside;
        RenderSettings.reflectionIntensity = reflectionInside;
    }

    void OnTriggerExit(Collider other)
    {
        if (!inside || !other.CompareTag("Player")) return;
        Restore();
    }

    void OnDisable() { if (inside) Restore(); }

    void Restore()
    {
        inside = false;
        RenderSettings.ambientIntensity = savedAmbient;
        RenderSettings.reflectionIntensity = savedReflection;
    }
}
