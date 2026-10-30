using System.Collections.Generic;
using UnityEngine;

// 3D models for the projectiles, one look for every spell: an arcane orb with orbiting stars, a flame, an ice crystal, a wind blade, a venom
// dart, a fire lance, a pinwheel... They are built from code (revolved profiles and extruded shapes), drawn with the toon shader so they glow in
// the spell's colour, and each one points along +Z, fitting a 1 m box before the projectile's scale is applied.
public static class ProjectileModels
{
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
    static Mesh sphere;

    // ---------- attaching a model to a projectile ----------

    // key: the spell's asset name (ArcaneBolt, FireBurst, ...), or "Enemy" / "Boss" for enemy shots
    public static void Attach(GameObject projectile, string key, Color color)
    {
        foreach (var r in projectile.GetComponents<Renderer>()) r.enabled = false;      // the plain sphere stays only as the hit shape
        var old = projectile.transform.Find("Model");
        if (old != null) Object.Destroy(old.gameObject);

        var root = new GameObject("Model").transform;
        root.SetParent(projectile.transform, false);

        var body = Material(color, 1.1f);
        var hot = Material(Color.Lerp(color, Color.white, 0.55f), 1.5f);
        var dark = Material(Color.Lerp(color, new Color(0.15f, 0.1f, 0.3f), 0.45f), 0.5f);
        var facet = Material(Color.Lerp(color, Color.white, 0.2f), 0.3f);                // little glow, so the facets and bands of crystals and blades show
        var bright = Material(Color.Lerp(color, Color.white, 0.2f), 1.4f);               // a glowing core that keeps the spell's colour

        switch (key)
        {
            case "ArcaneBolt": Arcane(root, body, hot); break;
            case "ScatterShot": Shard(root, facet, bright, 0.9f, 0.16f); break;
            case "FireBurst": Flame(root, body, hot, 1f); break;
            case "SparkStream": Spark(root, body, bright); break;
            case "FrostShard": Crystal(root, facet, bright, 1f, 0.2f); break;
            case "WindBlade": Blade(root, facet, bright, 1); break;
            case "VenomDart": Dart(root, body, hot, dark); break;
            case "InfernalBolt": Infernal(root, Material(new Color(0.6f, 0.08f, 0.05f), 0.9f), Material(Color.white, 1.8f), dark); break;
            case "CinderStorm": Ember(root, body, hot); break;
            case "FirestormLance": Lance(root, body, hot, dark); break;
            case "GlacierBolt": Glacier(root, facet, bright); break;
            case "Cyclone": Pinwheel(root, facet, bright); break;
            case "SunLance": Sun(root, Material(new Color(1f, 0.8f, 0.3f), 1.5f), Material(Color.white, 2f)); break;
            case "PlagueStorm": Plague(root, body, hot, dark); break;
            case "Boss": Orb(root, body, hot, dark, true, 0.55f); break;
            default: Orb(root, body, hot, dark, true, 0.45f); break;                    // enemy shots: a spiky orb
        }
    }

    // ---------- the models ----------

    static void Arcane(Transform root, Material body, Material hot)
    {
        Part(root, Sphere(), body, Vector3.zero, Vector3.one * 0.55f);
        Part(root, Sphere(), hot, Vector3.zero, Vector3.one * 0.3f);
        Part(root, Ring(0.5f, 0.035f), body, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 90f));             // a ring around it, spinning
        var orbit = Part(root, null, null, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, -200f));
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Part(orbit, Star(), hot, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.42f, Vector3.one * 0.2f, Quaternion.Euler(0f, 0f, i * 40f), new Vector3(60f, 180f, 0f));
        }
    }

    static void Shard(Transform root, Material body, Material hot, float length, float width)
    {
        Part(root, Crystal(5, length * 0.5f, width, 0.35f), body, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 220f));
        Part(root, Crystal(5, length * 0.62f, width * 0.45f, 0.35f), hot, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 220f));
    }

    static void Crystal(Transform root, Material body, Material hot, float length, float width)
    {
        Part(root, Crystal(6, length * 0.55f, width, 0.25f), body, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 90f));
        Part(root, Crystal(6, length * 0.68f, width * 0.45f, 0.25f), hot, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 90f));
        for (int i = 0; i < 2; i++)
            Part(root, Crystal(6, length * 0.2f, width * 0.35f, 0.3f), body, new Vector3(i == 0 ? 0.12f : -0.12f, 0.06f, -0.12f), Vector3.one, Quaternion.Euler(i == 0 ? 25f : -25f, 0f, 0f));
    }

    static void Flame(Transform root, Material body, Material hot, float size)
    {
        Part(root, Teardrop(), body, new Vector3(0f, 0f, -0.04f), Vector3.one * size, Quaternion.identity);
        Part(root, Teardrop(), hot, new Vector3(0f, 0f, 0f), Vector3.one * size * 0.55f, Quaternion.identity);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Part(root, Teardrop(), body, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.14f + new Vector3(0f, 0f, -0.2f), Vector3.one * size * 0.5f, Quaternion.Euler(Mathf.Sin(a) * 14f, -Mathf.Cos(a) * 14f, 0f));
        }
        Part(root, null, null, Vector3.zero, Vector3.one);
    }

    static void Spark(Transform root, Material body, Material hot)
    {
        // a thin tracer: a bright needle with a glowing head, like a machine-gun spark
        Part(root, Lathe("tracer", new[] { new Vector2(0f, 0.5f), new Vector2(0.03f, 0.3f), new Vector2(0.035f, -0.35f), new Vector2(0f, -0.55f) }, 8), hot, Vector3.zero, Vector3.one);
        Part(root, Sphere(), body, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.12f);
    }

    static void Blade(Transform root, Material body, Material hot, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Part(root, Crescent(0.5f, 0.34f), body, Vector3.zero, Vector3.one, Quaternion.Euler(0f, 0f, i * 360f / count));
            Part(root, Crescent(0.42f, 0.14f), hot, Vector3.zero, new Vector3(1f, 1f, 2.6f), Quaternion.Euler(0f, 0f, i * 360f / count));
        }
        // the blade lies across the direction of travel and spins about it, like a thrown disc
        root.GetChild(0).gameObject.AddComponent<ModelSpin>().rate = new Vector3(0f, 0f, 900f);
        if (root.childCount > 1) root.GetChild(1).gameObject.AddComponent<ModelSpin>().rate = new Vector3(0f, 0f, 900f);
    }

    static void Dart(Transform root, Material body, Material hot, Material dark)
    {
        Part(root, Lathe("dart", new[] { new Vector2(0f, 0.5f), new Vector2(0.05f, 0.3f), new Vector2(0.06f, -0.2f), new Vector2(0.1f, -0.3f), new Vector2(0f, -0.3f) }, 12), body, Vector3.zero, Vector3.one);
        Part(root, Lathe("dartTip", new[] { new Vector2(0f, 0.55f), new Vector2(0.045f, 0.4f), new Vector2(0f, 0.32f) }, 10), hot, Vector3.zero, Vector3.one);
        for (int i = 0; i < 3; i++)
            Part(root, Crescent(0.22f, 0.12f), dark, new Vector3(0f, 0f, -0.28f), Vector3.one * 0.8f, Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis(i * 120f, Vector3.right));
        Part(root, Sphere(), hot, new Vector3(0f, 0f, -0.32f), Vector3.one * 0.16f);
    }

    static void Infernal(Transform root, Material body, Material hot, Material dark)
    {
        Flame(root, body, hot, 1.1f);
        // a swirling sheath of dark flame around the white-hot core
        var swirl = Part(root, null, null, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 480f));
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Part(swirl, Teardrop(), dark, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.3f + new Vector3(0f, 0f, -0.1f), Vector3.one * 0.55f, Quaternion.Euler(Mathf.Sin(a) * 18f, -Mathf.Cos(a) * 18f, 0f));
        }
        Part(root, Ring(0.46f, 0.04f), hot, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 200f));
        Part(root, Star(), hot, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.22f, Quaternion.identity, new Vector3(90f, 160f, 0f));
    }

    static void Ember(Transform root, Material body, Material hot)
    {
        Part(root, Sphere(), body, Vector3.zero, Vector3.one * 0.5f);
        Part(root, Sphere(), hot, Vector3.zero, Vector3.one * 0.25f);
        Part(root, Star(), hot, new Vector3(0f, 0.18f, -0.1f), Vector3.one * 0.3f, Quaternion.identity, new Vector3(140f, 90f, 40f));
    }

    static void Lance(Transform root, Material body, Material hot, Material dark)
    {
        Part(root, Lathe("lance", new[] { new Vector2(0f, 0.55f), new Vector2(0.07f, 0.3f), new Vector2(0.1f, 0f), new Vector2(0.07f, -0.35f), new Vector2(0f, -0.5f) }, 12), body, Vector3.zero, Vector3.one);
        Part(root, Lathe("lanceCore", new[] { new Vector2(0f, 0.6f), new Vector2(0.035f, 0.3f), new Vector2(0.05f, 0f), new Vector2(0.035f, -0.3f), new Vector2(0f, -0.45f) }, 10), hot, Vector3.zero, Vector3.one);
        Part(root, Ring(0.2f, 0.03f), hot, new Vector3(0f, 0f, 0.1f), Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 300f));
        Part(root, Ring(0.16f, 0.03f), body, new Vector3(0f, 0f, -0.15f), Vector3.one, Quaternion.identity, new Vector3(0f, 0f, -300f));
        Part(root, Teardrop(), dark, new Vector3(0f, 0f, -0.5f), Vector3.one * 0.6f, Quaternion.identity);
        // a spiral of flame winding around the spear
        var spiral = Part(root, null, null, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 420f));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Part(spiral, Teardrop(), body, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.26f + new Vector3(0f, 0f, 0.3f - i * 0.2f), Vector3.one * 0.4f, Quaternion.Euler(Mathf.Sin(a) * 25f, -Mathf.Cos(a) * 25f, 0f));
        }
    }

    // a blinding mini sun: a white core in a gold glow, with rays all round
    static void Sun(Transform root, Material body, Material hot)
    {
        Part(root, Sphere(), body, Vector3.zero, Vector3.one * 0.7f);
        Part(root, Sphere(), hot, Vector3.zero, Vector3.one * 0.45f);
        var rays = Part(root, null, null, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 120f));
        for (int i = 0; i < 8; i++)
            Part(rays, Crystal(4, 0.5f, 0.07f, 0.5f), hot, Vector3.zero, Vector3.one, Quaternion.Euler(0f, 90f, i * 45f));
        Part(root, Ring(0.55f, 0.03f), hot, Vector3.zero, Vector3.one, Quaternion.identity, new Vector3(0f, 0f, 200f));
    }

    static void Glacier(Transform root, Material body, Material hot)
    {
        Crystal(root, body, hot, 1.1f, 0.26f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + 0.4f;
            Part(root, Crystal(6, 0.18f, 0.05f, 0.25f), body, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.22f + new Vector3(0f, 0f, -0.1f), Vector3.one, Quaternion.Euler(Mathf.Sin(a) * 40f, -Mathf.Cos(a) * 40f, 0f));
        }
    }

    static void Pinwheel(Transform root, Material body, Material hot)
    {
        var holder = Part(root, null, null, Vector3.zero, Vector3.one);
        for (int i = 0; i < 3; i++)
        {
            Part(holder, Crescent(0.5f, 0.3f), body, new Vector3(0f, 0f, i * 0.04f), Vector3.one, Quaternion.Euler(0f, 0f, i * 120f));
            Part(holder, Crescent(0.4f, 0.12f), hot, new Vector3(0f, 0f, i * 0.04f + 0.01f), new Vector3(1f, 1f, 2.6f), Quaternion.Euler(0f, 0f, i * 120f));
        }
        Part(holder, Sphere(), hot, Vector3.zero, Vector3.one * 0.2f);
        holder.gameObject.AddComponent<ModelSpin>().rate = new Vector3(0f, 0f, 700f);
    }

    static void Plague(Transform root, Material body, Material hot, Material dark)
    {
        Part(root, Sphere(), body, Vector3.zero, Vector3.one * 0.5f);
        Part(root, Sphere(), hot, new Vector3(0.05f, 0.05f, 0.08f), Vector3.one * 0.22f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 d = Random.onUnitSphere;
            var rng = new System.Random(i * 31 + 7);
            d = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
            Part(root, Lathe("thorn", new[] { new Vector2(0f, 0.2f), new Vector2(0.05f, 0.04f), new Vector2(0.05f, 0f) }, 8), dark, d * 0.22f, Vector3.one, Quaternion.LookRotation(d));
        }
        Part(root, Sphere(), hot, new Vector3(0.3f, 0.1f, -0.1f), Vector3.one * 0.12f);
        Part(root, Sphere(), hot, new Vector3(-0.28f, -0.12f, -0.2f), Vector3.one * 0.1f);
    }

    static void Orb(Transform root, Material body, Material hot, Material dark, bool spikes, float size)
    {
        Part(root, Sphere(), body, Vector3.zero, Vector3.one * size * 1.8f);
        Part(root, Sphere(), hot, Vector3.zero, Vector3.one * size * 0.9f);
        if (spikes)
            for (int i = 0; i < 8; i++)
            {
                var rng = new System.Random(i * 17 + 3);
                Vector3 d = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
                Part(root, Lathe("spike", new[] { new Vector2(0f, 0.28f), new Vector2(0.07f, 0.06f), new Vector2(0.07f, 0f) }, 8), dark, d * size * 0.7f, Vector3.one * (size * 2f), Quaternion.LookRotation(d));
            }
        Part(root, Star(), hot, Vector3.zero, Vector3.one * size * 0.8f, Quaternion.identity, new Vector3(40f, 200f, 90f));
    }

    // ---------- placing parts ----------

    // a child with a mesh and a material (or an empty holder when mesh is null); spin = degrees per second about each axis
    static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null, Vector3? spin = null)
    {
        var go = new GameObject(mesh != null ? mesh.name : "Holder");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        if (mesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (spin.HasValue) go.AddComponent<ModelSpin>().rate = spin.Value;
        return go.transform;
    }

    // ---------- materials ----------

    static Material Material(Color color, float glow)
    {
        int key = ((int)(color.r * 15) << 12) | ((int)(color.g * 15) << 8) | ((int)(color.b * 15) << 4) | Mathf.RoundToInt(glow * 2f);
        if (materials.TryGetValue(key, out var m) && m != null) return m;

        var shader = Shader.Find("Magic/ToonCharacter");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(color, Color.white, 0.12f));
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", color);
            m.SetFloat("_EmissionStrength", glow);
            m.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.5f));
            m.SetFloat("_RimStrength", 0.7f);
            m.SetColor("_OutlineColor", Color.Lerp(color, new Color(0.15f, 0.1f, 0.3f), 0.6f));
            m.SetFloat("_OutlineWidth", 0.012f);
            m.SetFloat("_SparkleStrength", 0f);
            m.SetColor("_ShadeColor", Color.Lerp(color, Color.white, 0.5f));
        }
        materials[key] = m;
        return m;
    }

    // ---------- meshes ----------

    static Mesh Sphere()
    {
        if (sphere != null) return sphere;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return sphere;
    }

    static Mesh Star() => MagicFX.StarMesh();

    // revolves a profile of (radius, z) points around the z axis, with smooth normals
    static Mesh Lathe(string name, Vector2[] profile, int segs, bool flat = false)
    {
        string key = name + segs + flat;
        if (meshes.TryGetValue(key, out var cached)) return cached;

        var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        int rows = profile.Length;
        for (int i = 0; i < rows; i++)
        {
            Vector2 p = profile[i];
            Vector2 prev = profile[Mathf.Max(i - 1, 0)], next = profile[Mathf.Min(i + 1, rows - 1)];
            Vector2 tan = (next - prev).normalized;                       // (dr, dz)
            Vector2 nrm = new Vector2(-tan.y, tan.x);                     // outward in (r, z)
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(dir * p.x + Vector3.forward * p.y);
                n.Add((dir * nrm.x + Vector3.forward * nrm.y).normalized);
                c.Add(new Color(1f, 1f, 1f, 0f));
            }
        }
        for (int i = 0; i < rows - 1; i++)
            for (int s = 0; s < segs; s++)
            {
                int a = i * (segs + 1) + s, b = a + 1, d = a + segs + 1, e = d + 1;
                t.AddRange(new[] { a, b, d, b, e, d });
            }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetTriangles(t, 0);
        if (flat) Flatten(mesh);
        mesh.RecalculateBounds();
        FixWinding(mesh);
        meshes[key] = mesh;
        return mesh;
    }

    // splits every triangle into its own vertices so each face gets one flat normal (the faceted crystal look)
    static void Flatten(Mesh m)
    {
        var ov = m.vertices; var ot = m.triangles;
        var v = new Vector3[ot.Length]; var n = new Vector3[ot.Length]; var c = new Color[ot.Length]; var t = new int[ot.Length];
        for (int i = 0; i < ot.Length; i += 3)
        {
            Vector3 a = ov[ot[i]], b = ov[ot[i + 1]], d = ov[ot[i + 2]];
            Vector3 fn = -Vector3.Cross(b - a, d - a).normalized;                 // the revolved triangles wind inward, so the face normal is flipped
            for (int k = 0; k < 3; k++) { v[i + k] = ov[ot[i + k]]; n[i + k] = fn; c[i + k] = new Color(1f, 1f, 1f, 0f); t[i + k] = i + k; }
        }
        m.Clear();
        m.vertices = v; m.normals = n; m.colors = c; m.triangles = t;
    }

    // makes every triangle face the way its normals point
    static void FixWinding(Mesh m)
    {
        var v = m.vertices; var n = m.normals; var t = m.triangles;
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            Vector3 avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
            if (Vector3.Dot(fn, avg) < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
        m.triangles = t;
    }

    // a faceted double-pointed crystal along z: length is the half length, width the radius
    static Mesh Crystal(int sides, float halfLength, float width, float shoulder)
    {
        string key = "crystal" + sides + halfLength.ToString("0.00") + width.ToString("0.00") + shoulder;
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var m = Lathe(key + "_l", new[]
        {
            new Vector2(0f, halfLength), new Vector2(width, halfLength * shoulder * 0.5f), new Vector2(width, -halfLength * shoulder * 0.5f), new Vector2(0f, -halfLength),
        }, sides, true);
        m.name = key;
        meshes[key] = m;
        return m;
    }

    static Mesh Teardrop()
    {
        const string key = "teardrop";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        // a round head at +z and a tail stretching back
        var prof = new List<Vector2>();
        for (int i = 0; i <= 8; i++) { float a = i / 8f * Mathf.PI * 0.5f; prof.Add(new Vector2(Mathf.Sin(a) * 0.24f, 0.2f + Mathf.Cos(a) * 0.24f)); }
        prof.Add(new Vector2(0.2f, 0.06f)); prof.Add(new Vector2(0.12f, -0.15f)); prof.Add(new Vector2(0.05f, -0.35f)); prof.Add(new Vector2(0f, -0.52f));
        // the profile runs head to tail; Lathe wants nothing special about the order
        var m = Lathe(key + "_l", prof.ToArray(), 14);
        m.name = key;
        meshes[key] = m;
        return m;
    }

    static Mesh Ring(float radius, float thickness)
    {
        string key = "ring" + radius.ToString("0.00") + thickness.ToString("0.000");
        if (meshes.TryGetValue(key, out var cached)) return cached;
        const int major = 28, minor = 8;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        for (int i = 0; i <= major; i++)
        {
            float a = i / (float)major * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= minor; j++)
            {
                float b = j / (float)minor * Mathf.PI * 2f;
                Vector3 nrm = dir * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(dir * radius + nrm * thickness); n.Add(nrm); c.Add(new Color(1f, 1f, 1f, 0f));
            }
        }
        for (int i = 0; i < major; i++)
            for (int j = 0; j < minor; j++)
            {
                int a = i * (minor + 1) + j, b = a + 1, d = a + minor + 1, e = d + 1;
                t.AddRange(new[] { a, d, b, b, d, e });
            }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        FixWinding(m);
        meshes[key] = m;
        return m;
    }

    // a thick crescent in the xy plane, its tips pointing back (-y no: toward -z is the way it flies, so the bulge faces +x... it is built flat and rotated by the parts)
    static Mesh Crescent(float radius, float thickness)
    {
        string key = "crescent" + radius.ToString("0.00") + thickness.ToString("0.00");
        if (meshes.TryGetValue(key, out var cached)) return cached;

        const int steps = 16;
        var outer = new List<Vector2>(); var inner = new List<Vector2>();
        for (int i = 0; i <= steps; i++)
        {
            float f = i / (float)steps;
            float a = Mathf.Lerp(-120f, 120f, f) * Mathf.Deg2Rad;
            outer.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            float width = thickness * Mathf.Sin(f * Mathf.PI);                // pointed tips, thickest in the middle
            inner.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius - width));
        }

        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        float half = thickness * 0.18f;
        // front and back strips, and the outer and inner edges
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i <= steps; i++)
            {
                v.Add(new Vector3(outer[i].x, outer[i].y, half * side)); nrm.Add(new Vector3(0f, 0f, side));
                v.Add(new Vector3(inner[i].x, inner[i].y, half * side)); nrm.Add(new Vector3(0f, 0f, side));
            }
        int perSide = (steps + 1) * 2;
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < steps; i++)
            {
                int a = side * perSide + i * 2, b = a + 1, d = a + 2, e = a + 3;
                t.AddRange(side == 0 ? new[] { a, b, d, b, e, d } : new[] { a, d, b, b, d, e });
            }
        int edge = v.Count;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 o = outer[i], inn = inner[i];
            Vector3 on = new Vector3(o.x, o.y, 0f).normalized, inN = -new Vector3(inn.x, inn.y, 0f).normalized;
            v.Add(new Vector3(o.x, o.y, half)); nrm.Add(on);
            v.Add(new Vector3(o.x, o.y, -half)); nrm.Add(on);
            v.Add(new Vector3(inn.x, inn.y, half)); nrm.Add(inN);
            v.Add(new Vector3(inn.x, inn.y, -half)); nrm.Add(inN);
        }
        for (int i = 0; i < steps; i++)
        {
            int o0 = edge + i * 4, o1 = o0 + 4;
            t.AddRange(new[] { o0, o0 + 1, o1, o0 + 1, o1 + 1, o1 });
            int i0 = edge + i * 4 + 2, i1 = i0 + 4;
            t.AddRange(new[] { i0, i1, i0 + 1, i0 + 1, i1, i1 + 1 });
        }
        for (int i = 0; i < v.Count; i++) c.Add(new Color(1f, 1f, 1f, 0f));

        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(nrm); m.SetColors(c); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        FixWinding(m);
        meshes[key] = m;
        return m;
    }
}

// spins the object about its own axes at a steady rate (degrees per second)
public class ModelSpin : MonoBehaviour
{
    public Vector3 rate;
    void Update() { transform.Rotate(rate * Time.deltaTime, Space.Self); }
}
