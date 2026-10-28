using UnityEngine;

// Removes a one-shot effect prefab after its particles have finished.
public class FxAutoDestroy : MonoBehaviour
{
    [SerializeField] float lifetime = 3f;

    void Start() => Destroy(gameObject, lifetime);
}
