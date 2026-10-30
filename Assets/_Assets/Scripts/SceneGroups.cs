using System.Collections.Generic;
using UnityEngine;

// Empty parent objects that keep everything created while playing tidy in the hierarchy, instead of hundreds of loose objects at the root:
//   VFX / Impacts, Casting, Beams, Ground, Status, Boss      Sfx      Enemies      Barriers      Projectiles / Projectile Pools      Systems
// A path like "VFX/Impacts" creates the groups the first time it is asked for, and again after a scene change took them away.
public static class SceneGroups
{
    static readonly Dictionary<string, Transform> cache = new Dictionary<string, Transform>();

    public static Transform Get(string path)
    {
        if (cache.TryGetValue(path, out var t) && t != null) return t;

        Transform parent = null;
        string name = path;
        int slash = path.LastIndexOf('/');
        if (slash >= 0)
        {
            parent = Get(path.Substring(0, slash));
            name = path.Substring(slash + 1);
        }

        t = new GameObject(name).transform;
        if (parent != null) t.SetParent(parent, false);
        cache[path] = t;
        return t;
    }

    // puts an object under a group, keeping where it is in the world; returns it so a call can wrap a creation
    public static T Put<T>(T item, string path) where T : Component
    {
        item.transform.SetParent(Get(path), true);
        return item;
    }

    public static GameObject Put(GameObject go, string path)
    {
        go.transform.SetParent(Get(path), true);
        return go;
    }
}
