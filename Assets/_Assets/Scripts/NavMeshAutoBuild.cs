using Unity.AI.Navigation;
using UnityEngine;

// Bakes the NavMesh when the scene starts if no baked data is assigned, so ground enemies can walk.
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshAutoBuild : MonoBehaviour
{
    void Awake()
    {
        var surface = GetComponent<NavMeshSurface>();
        if (surface.navMeshData == null) surface.BuildNavMesh();
    }
}
