using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The forest stage's world, made when the scene starts (nothing heavy is stored in the scene or the project): rolling ground, lakes,
// a ring of mountains around the edge, thousands of trees, giant landmark trees, rocks, bushes and glowing mushrooms and crystals.
// Always the same forest for the same Seed.
//
// The map is cut into square chunks. Every chunk always shows its ground and a cheap far version of its trees; the full-detail trees,
// the small things and the colliders of a chunk are made the first time the camera comes close, a couple of chunks per frame.
// Everything is coloured through vertex colours, so the whole forest needs only the few materials below.
//
// Built by Tools > Magic > Build Forest Stage. Change the numbers here and press Play (or use the "Rebuild" entry of the component's
// menu to see it in the Scene view).
[ExecuteAlways]
public class ForestMap : MonoBehaviour
{
    [Header("Size (metres, a circle around this object)")]
    [Tooltip("The area the player may be in. Keep it the same as the radius on Map Bounds (the builder does).")]
    public float playRadius = 750f;
    [SerializeField, Tooltip("Where the world ends, behind the mountains. Must be well beyond the play radius.")]
    float worldRadius = 1350f;
    [SerializeField] int seed = 7;
    [SerializeField] float chunkSize = 100f;

    [Header("Ground")]
    [SerializeField] float hillHeight = 12f;
    [SerializeField] float hillSize = 280f;
    [SerializeField, Tooltip("The mountains start this far beyond the play radius.")]
    float mountainsBeyondEdge = 260f;
    [SerializeField] float mountainHeight = 240f;
    [SerializeField, Tooltip("Open, flat ground around the starting point.")]
    float clearingRadius = 34f;
    [SerializeField] int lakes = 5;

    [Header("Trees")]
    [SerializeField, Tooltip("Average metres between trees inside the woods.")]
    float treeSpacing = 15f;
    [SerializeField, Range(0.1f, 0.95f), Tooltip("How much of the land is woods; the rest is meadow.")]
    float forestCover = 0.62f;
    [SerializeField] int giantTrees = 28;

    [Header("Detail")]
    [SerializeField, Tooltip("Full-detail trees, small things and colliders exist within this distance of the camera.")]
    float nearDistance = 240f;
    [SerializeField, Tooltip("Most chunks made in one frame while flying.")]
    int chunksPerFrame = 2;
    [SerializeField] bool floatingMotes = true;
    [SerializeField, Tooltip("Switch off the drawing of every chunk the camera is not looking at (behind you, off to the side). Colliders stay.")]
    bool hideUnseen = true;
    [SerializeField, Tooltip("Chunks this close to the camera are always drawn, so shadows of unseen trees still fall into view.")]
    float alwaysDrawWithin = 90f;
    [SerializeField, Tooltip("Seconds between checks of what the camera sees.")]
    float seenCheckInterval = 0.12f;

    [Header("Materials (Magic/ToonStylized, made by the builder)")]
    [SerializeField] Material groundMaterial;
    [SerializeField] Material foliageMaterial;
    [SerializeField] Material glowMaterial;
    [SerializeField] Material waterMaterial;
    [SerializeField] Material motesMaterial;

    [Header("Colours")]
    [SerializeField] Color grassMeadow = new Color(0.62f, 0.84f, 0.50f);
    [SerializeField] Color grassSunny = new Color(0.78f, 0.88f, 0.52f);
    [SerializeField] Color grassForest = new Color(0.36f, 0.64f, 0.50f);
    [SerializeField] Color forestFloor = new Color(0.30f, 0.50f, 0.47f);
    [SerializeField] Color sand = new Color(0.93f, 0.86f, 0.68f);
    [SerializeField] Color lakeBed = new Color(0.40f, 0.66f, 0.70f);
    [SerializeField] Color rock = new Color(0.64f, 0.61f, 0.76f);
    [SerializeField] Color snow = new Color(0.95f, 0.95f, 1f);
    [SerializeField] Color[] flowerColors = { new Color(1f, 0.72f, 0.84f), new Color(1f, 0.92f, 0.55f), new Color(0.80f, 0.74f, 1f), new Color(1f, 1f, 1f) };
    [SerializeField] Color trunk = new Color(0.50f, 0.36f, 0.40f);
    [SerializeField] Color trunkPale = new Color(0.86f, 0.80f, 0.78f);
    [SerializeField] Color[] leafColors = { new Color(0.34f, 0.70f, 0.56f), new Color(0.48f, 0.78f, 0.48f), new Color(0.66f, 0.84f, 0.46f), new Color(0.30f, 0.62f, 0.62f) };
    [SerializeField] Color[] pineColors = { new Color(0.20f, 0.50f, 0.50f), new Color(0.25f, 0.57f, 0.46f), new Color(0.22f, 0.44f, 0.56f) };
    [SerializeField] Color[] blossomColors = { new Color(1f, 0.68f, 0.80f), new Color(0.76f, 0.66f, 1f), new Color(1f, 0.84f, 0.50f), new Color(1f, 0.90f, 0.94f) };
    [SerializeField] Color waterColor = new Color(0.50f, 0.84f, 0.90f);
    [SerializeField] Color[] glowColors = { new Color(0.45f, 1f, 0.95f), new Color(1f, 0.55f, 0.85f), new Color(1f, 0.90f, 0.45f), new Color(0.70f, 0.60f, 1f) };

    [Header("Editor")]
    [SerializeField, Tooltip("Show the forest in the Scene view while not playing (it is never saved into the scene).")]
    bool previewInEditor = true;

    public static ForestMap Active { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------------------

    const string GeneratedName = "Forest (generated)";

    enum Kind : byte { Round, Pine, Umbrella, Blossom, Giant }

    struct Tree
    {
        public Vector3 pos;      // relative to the map centre
        public Kind kind;
        public float height;
        public uint id;          // seeds everything else about this tree, so the near and the far version agree
    }

    struct Lake { public float x, z, radius, level; }

    class Chunk
    {
        public Vector3 center;
        public GameObject go;
        public Mesh groundMesh;
        public readonly List<Tree> trees = new List<Tree>();
        public MeshRenderer farRenderer;
        public GameObject near;
        public MeshRenderer groundRenderer;
        public MeshRenderer[] nearRenderers;
        public Bounds bounds;                      // in world space, for the camera's view check
        public bool canBeNear, built, showingNear, seen = true;
        public uint id;
    }

    readonly List<Chunk> chunks = new List<Chunk>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Lake> lakeList = new List<Lake>();
    readonly List<Tree> giants = new List<Tree>();
    readonly List<Vector3> stones = new List<Vector3>();
    Transform root;
    Vector2[] noiseOffset;
    bool linear;
    float forestThreshold;
    Transform cam;
    Transform motes;

    // ---------- life cycle ----------

    void OnEnable()
    {
        Active = this;
        if (Application.isPlaying || previewInEditor) Generate();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        Clear();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        Clear();
        Generate();
    }

    void Clear()
    {
        if (root != null) Kill(root.gameObject);
        root = null;
        foreach (var m in meshes) if (m != null) Kill(m);
        meshes.Clear();
        chunks.Clear();
        lakeList.Clear();
        giants.Clear();
        stones.Clear();
        if (motes != null) Kill(motes.gameObject);
        motes = null;
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        if (!Application.isPlaying) go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(parent, false);
        go.isStatic = false;
        return go;
    }

    // ---------- the land ----------

    float Noise(float x, float z, float size, int channel)
    {
        Vector2 o = noiseOffset[channel];
        return Mathf.PerlinNoise(x / size + o.x, z / size + o.y);
    }

    float LandHeight(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float h = (Noise(x, z, hillSize, 0) - 0.5f) * 2f * hillHeight
                + (Noise(x, z, hillSize * 0.31f, 1) - 0.5f) * hillHeight * 0.45f
                + (Noise(x, z, 37f, 2) - 0.5f) * 1.2f;
        h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearingRadius * 0.6f, clearingRadius * 2.4f, r));   // flat where she starts

        // the ring of mountains: it rises to a ridge and drops again behind it, so the end of the world is hidden
        float start = playRadius + mountainsBeyondEdge, ridge = worldRadius - 130f;
        if (r > start)
        {
            float t = Mathf.Clamp01((r - start) / Mathf.Max(ridge - start, 1f));
            t = t * t * (3f - 2f * t);
            float m = mountainHeight * t * (0.55f + 0.9f * Noise(x, z, 150f, 3));
            if (r > ridge) m *= Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((r - ridge) / 190f));
            h += m;
        }
        return h;
    }

    // how much a lake owns this spot (0 none, 1 fully) and which lake
    float LakeWeight(float x, float z, out Lake lake, out float q)
    {
        lake = default; q = 0f;
        float best = 0f;
        for (int i = 0; i < lakeList.Count; i++)
        {
            Lake l = lakeList[i];
            float dx = x - l.x, dz = z - l.z;
            float d2 = dx * dx + dz * dz, reach = l.radius * 1.7f;
            if (d2 > reach * reach) continue;
            float qq = Mathf.Sqrt(d2) / l.radius;
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.7f, 1.0f, qq));
            if (w > best) { best = w; lake = l; q = qq; }
        }
        return best;
    }

    /// Ground height at a point given relative to the map centre.
    public float HeightLocal(float x, float z)
    {
        float h = LandHeight(x, z);
        float w = LakeWeight(x, z, out Lake lake, out float q);
        if (w <= 0f) return h;
        float bowl = q < 1f ? 1.4f * (1f - q * q) : 0f;      // a shallow bowl: wading depth, never swimming
        return Mathf.Lerp(h, lake.level - bowl, w);
    }

    /// Ground height at a world position (the stage builder uses it to stand the player on the ground).
    public float HeightAt(Vector3 world)
    {
        EnsureSetup();
        Vector3 p = world - transform.position;
        return HeightLocal(p.x, p.z) + transform.position.y;
    }

    const float WaterBelowShore = 0.45f;

    float Woods(float x, float z) => Noise(x, z, 170f, 4) * 0.65f + Noise(x, z, 60f, 5) * 0.35f;

    // ---------- setting up ----------

    void EnsureSetup()
    {
        if (noiseOffset != null) return;
        linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        forestThreshold = Mathf.Lerp(0.72f, 0.28f, forestCover);

        var rng = new Rng((uint)seed * 2654435761u + 17u);
        noiseOffset = new Vector2[10];
        for (int i = 0; i < noiseOffset.Length; i++) noiseOffset[i] = new Vector2(rng.Range(3000f, 9000f), rng.Range(3000f, 9000f));

        // lakes: anywhere in the play area but not on the starting clearing, and not on top of each other
        lakeList.Clear();
        for (int tries = 0; tries < 400 && lakeList.Count < lakes; tries++)
        {
            float a = rng.Range(0f, Mathf.PI * 2f), d = rng.Range(clearingRadius + 130f, playRadius - 60f);
            var l = new Lake { x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d, radius = rng.Range(38f, 78f) };
            bool ok = true;
            foreach (var o in lakeList)
                if ((new Vector2(o.x - l.x, o.z - l.z)).magnitude < (o.radius + l.radius) * 2.2f) ok = false;
            if (!ok) continue;
            l.level = LandHeight(l.x, l.z) - 1.5f;
            lakeList.Add(l);
        }

        // giant trees: landmarks to steer by and fly around
        giants.Clear();
        for (int tries = 0; tries < 2000 && giants.Count < giantTrees; tries++)
        {
            float a = rng.Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(rng.Next()) * (playRadius + 140f);
            float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
            if (d < clearingRadius + 60f || LakeWeight(x, z, out _, out _) > 0f) continue;
            bool ok = true;
            foreach (var g in giants) if ((g.pos - new Vector3(x, g.pos.y, z)).sqrMagnitude < 150f * 150f) ok = false;
            if (!ok) continue;
            giants.Add(new Tree { pos = new Vector3(x, HeightLocal(x, z), z), kind = Kind.Giant, height = rng.Range(58f, 88f), id = rng.NextUInt() });
        }

        // a ring of standing stones around the starting point
        stones.Clear();
        int count = 9;
        for (int i = 0; i < count; i++)
        {
            float a = (i + 0.5f) / count * Mathf.PI * 2f, d = clearingRadius * 0.66f;
            stones.Add(new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d));
        }
    }

    void Generate()
    {
        noiseOffset = null;
        EnsureSetup();

        // a preview left behind by an editor that did not shut down cleanly
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name == GeneratedName) Kill(transform.GetChild(i).gameObject);
        root = NewObject(GeneratedName, transform).transform;

        int n = Mathf.CeilToInt(worldRadius / chunkSize);
        float collideRadius = playRadius + 480f;
        for (int cz = -n; cz < n; cz++)
            for (int cx = -n; cx < n; cx++)
            {
                float minX = cx * chunkSize, minZ = cz * chunkSize;
                float nx = Mathf.Clamp(0f, minX, minX + chunkSize), nz = Mathf.Clamp(0f, minZ, minZ + chunkSize);
                if (nx * nx + nz * nz > worldRadius * worldRadius) continue;

                var c = new Chunk { center = new Vector3(minX + chunkSize * 0.5f, 0f, minZ + chunkSize * 0.5f), id = Hash(cx, cz, seed) };
                c.canBeNear = new Vector2(c.center.x, c.center.z).magnitude < collideRadius;
                c.go = NewObject("Chunk " + cx + " " + cz, root);
                c.go.transform.localPosition = c.center;
                BuildGround(c);
                PlaceTrees(c, minX, minZ);
                BuildFar(c);
                chunks.Add(c);
            }

        BuildWater();

        if (Application.isPlaying)
        {
            cam = Camera.main != null ? Camera.main.transform : null;
            UpdateDetail(true);                    // everything around the start is there before the first frame
            if (floatingMotes && motesMaterial != null) MakeMotes();
        }
        else
        {
            // Scene view: full detail around the middle, the far version everywhere else
            foreach (var c in chunks)
                if (c.canBeNear && new Vector2(c.center.x, c.center.z).magnitude < 330f) { BuildNear(c); Show(c, true); }
        }
    }

    // ---------- detail by distance ----------

    void Update()
    {
        if (!Application.isPlaying || root == null) return;
        if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; }
        UpdateDetail(false);
        if (hideUnseen && Time.unscaledTime >= nextSeenCheck)
        {
            nextSeenCheck = Time.unscaledTime + seenCheckInterval;
            UpdateSeen();
        }
        if (motes != null) motes.position = cam.position;
    }

    float nextSeenCheck;
    static readonly Plane[] viewPlanes = new Plane[6];

    // every chunk outside the camera's view stops being drawn; the ground collider and the trunk colliders are untouched
    void UpdateSeen()
    {
        var camera = Camera.main;
        if (camera == null) return;
        GeometryUtility.CalculateFrustumPlanes(camera, viewPlanes);
        Vector3 cp = camera.transform.position;
        float near2 = alwaysDrawWithin * alwaysDrawWithin;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk c = chunks[i];
            Vector3 d = c.bounds.ClosestPoint(cp) - cp;
            bool seen = d.sqrMagnitude < near2 || GeometryUtility.TestPlanesAABB(viewPlanes, c.bounds);
            if (seen == c.seen) continue;
            c.seen = seen;
            ApplyDrawing(c);
        }
    }

    static void ApplyDrawing(Chunk c)
    {
        if (c.groundRenderer != null) c.groundRenderer.enabled = c.seen;
        if (c.farRenderer != null) c.farRenderer.enabled = c.seen && !c.showingNear;
        if (c.nearRenderers != null)
            foreach (var r in c.nearRenderers) if (r != null) r.enabled = c.seen;
    }

    void UpdateDetail(bool all)
    {
        Vector3 p = (cam != null ? cam.position : transform.position) - transform.position;
        int budget = all ? int.MaxValue : Mathf.Max(1, chunksPerFrame);
        float near2 = nearDistance * nearDistance, far2 = (nearDistance + 40f) * (nearDistance + 40f);

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk c = chunks[i];
            if (!c.canBeNear) continue;
            float dx = c.center.x - p.x, dz = c.center.z - p.z, d2 = dx * dx + dz * dz;
            if (d2 < near2)
            {
                if (!c.built)
                {
                    if (budget <= 0) continue;
                    budget--;
                    BuildNear(c);
                }
                if (!c.showingNear) Show(c, true);
            }
            else if (d2 > far2 && c.showingNear) Show(c, false);
        }
    }

    static void Show(Chunk c, bool near)
    {
        c.showingNear = near;
        if (c.near != null) c.near.SetActive(near);
        if (c.farRenderer != null) c.farRenderer.enabled = !near && c.seen;
    }

    // ---------- ground ----------

    void BuildGround(Chunk c)
    {
        const int n = 20;
        float step = chunkSize / n, x0 = c.center.x - chunkSize * 0.5f, z0 = c.center.z - chunkSize * 0.5f;

        // heights with one extra row all round, so the normals match the neighbouring chunks
        int w = n + 3;
        var hs = new float[w * w];
        for (int j = 0; j < w; j++)
            for (int i = 0; i < w; i++)
                hs[j * w + i] = HeightLocal(x0 + (i - 1) * step, z0 + (j - 1) * step);

        var verts = new Vector3[(n + 1) * (n + 1)];
        var norms = new Vector3[verts.Length];
        var cols = new Color32[verts.Length];
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                int k = j * (n + 1) + i, g = (j + 1) * w + (i + 1);
                float x = x0 + i * step, z = z0 + j * step, h = hs[g];
                verts[k] = new Vector3(x - c.center.x, h, z - c.center.z);
                Vector3 nrm = new Vector3(hs[g - 1] - hs[g + 1], 2f * step, hs[g - w] - hs[g + w]).normalized;
                norms[k] = nrm;
                cols[k] = ToColor(GroundColor(x, z, h, nrm.y));
            }

        var tris = new int[n * n * 6];
        int t = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, d = a + n + 1, e = d + 1;
                tris[t++] = a; tris[t++] = d; tris[t++] = b;
                tris[t++] = b; tris[t++] = d; tris[t++] = e;
            }

        var mesh = NewMesh("Ground");
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.colors32 = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        c.groundMesh = mesh;

        c.go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = c.go.AddComponent<MeshRenderer>();
        r.sharedMaterial = groundMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        c.groundRenderer = r;
        // tall enough for the mountains, so a chunk is never judged unseen while its peak is in view
        c.bounds = new Bounds(c.center + transform.position + Vector3.up * 120f, new Vector3(chunkSize, mountainHeight * 2f + 80f, chunkSize));
    }

    Color GroundColor(float x, float z, float h, float up)
    {
        float woods = Woods(x, z);
        float inWoods = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(forestThreshold - 0.06f, forestThreshold + 0.08f, woods));
        Color c = Color.Lerp(grassMeadow, grassForest, inWoods);
        c = Color.Lerp(c, grassSunny, (1f - inWoods) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.8f, Noise(x, z, 42f, 6))) * 0.8f);
        c = Color.Lerp(c, forestFloor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(forestThreshold + 0.1f, forestThreshold + 0.32f, woods)) * 0.75f);

        // patches of flowers in the meadows
        float flowers = Noise(x, z, 11f, 7);
        if (flowers > 0.66f && inWoods < 0.5f && flowerColors.Length > 0)
        {
            Color f = flowerColors[Mathf.Min((int)(Noise(x, z, 75f, 8) * flowerColors.Length * 1.3f), flowerColors.Length - 1)];
            c = Color.Lerp(c, f, Mathf.InverseLerp(0.66f, 0.8f, flowers) * 0.55f * (1f - inWoods * 2f));
        }

        // shores and lake beds
        float lw = LakeWeight(x, z, out Lake lake, out _);
        if (lw > 0f)
        {
            float above = h - (lake.level - WaterBelowShore);
            c = Color.Lerp(c, sand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 0.25f, above)));
            c = Color.Lerp(c, lakeBed, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, -0.5f, above)));
        }

        // bare rock on steep ground and high up, snow on the peaks
        float rocky = Mathf.Max(Mathf.InverseLerp(0.86f, 0.70f, up), Mathf.InverseLerp(70f, 125f, h));
        c = Color.Lerp(c, rock * Mathf.Lerp(0.9f, 1.08f, Noise(x, z, 30f, 9)), Mathf.SmoothStep(0f, 1f, rocky));
        c = Color.Lerp(c, snow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 185f, h + (Noise(x, z, 60f, 6) - 0.5f) * 40f)));
        return c;
    }

    void BuildWater()
    {
        for (int i = 0; i < lakeList.Count; i++)
        {
            Lake l = lakeList[i];
            var b = new MeshBuilder();
            const int seg = 40;
            Color32 mid = ToColor(waterColor * 0.9f), rim = ToColor(Color.Lerp(waterColor, Color.white, 0.35f));
            int centre = b.Add(Vector3.zero, Vector3.up, mid);
            for (int s = 0; s < seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                b.Add(new Vector3(Mathf.Cos(a) * l.radius, 0f, Mathf.Sin(a) * l.radius), Vector3.up, rim);
            }
            for (int s = 0; s < seg; s++) b.Tri(centre, centre + 1 + (s + 1) % seg, centre + 1 + s);

            var go = NewObject("Lake " + i, root);
            go.transform.localPosition = new Vector3(l.x, l.level - WaterBelowShore, l.z);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(NewMesh("Lake"));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = waterMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ---------- where the trees stand ----------

    void PlaceTrees(Chunk c, float minX, float minZ)
    {
        float s = treeSpacing;
        float mountains = playRadius + mountainsBeyondEdge;
        int i0 = Mathf.FloorToInt(minX / s) - 1, i1 = Mathf.FloorToInt((minX + chunkSize) / s) + 1;
        int j0 = Mathf.FloorToInt(minZ / s) - 1, j1 = Mathf.FloorToInt((minZ + chunkSize) / s) + 1;

        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                var rng = new Rng(Hash(i, j, seed + 101));
                float x = (i + 0.12f + 0.76f * rng.Next()) * s, z = (j + 0.12f + 0.76f * rng.Next()) * s;
                if (x < minX || x >= minX + chunkSize || z < minZ || z >= minZ + chunkSize) continue;

                float r = Mathf.Sqrt(x * x + z * z);
                if (r < clearingRadius || r > worldRadius - 20f) continue;
                if (LakeWeight(x, z, out _, out float q) > 0f && q < 1.12f) continue;

                float woods = Woods(x, z);
                float chance = Mathf.InverseLerp(forestThreshold - 0.05f, forestThreshold + 0.1f, woods);   // thin at the edge of a wood
                chance = Mathf.Max(chance, 0.035f);                                                        // the odd lone tree in a meadow
                if (rng.Next() > chance) continue;

                float h = HeightLocal(x, z);
                if (h > 118f) continue;                                                                    // tree line
                bool tooClose = false;
                foreach (var g in giants) if ((g.pos.x - x) * (g.pos.x - x) + (g.pos.z - z) * (g.pos.z - z) < 14f * 14f) tooClose = true;
                if (tooClose) continue;

                var t = new Tree { pos = new Vector3(x, h, z), id = rng.NextUInt() };
                float biome = Noise(x, z, 420f, 8), grove = Noise(x, z, 130f, 9), pick = rng.Next();
                if (r > mountains || biome < 0.36f) t.kind = pick < 0.88f ? Kind.Pine : Kind.Round;
                else if (grove > 0.70f) t.kind = pick < 0.75f ? Kind.Blossom : Kind.Round;
                else t.kind = pick < 0.62f ? Kind.Round : pick < 0.80f ? Kind.Umbrella : pick < 0.92f ? Kind.Pine : Kind.Blossom;

                float size = rng.Next();
                t.height = t.kind == Kind.Pine ? Mathf.Lerp(12f, 25f, size)
                         : t.kind == Kind.Umbrella ? Mathf.Lerp(18f, 29f, size)
                         : t.kind == Kind.Blossom ? Mathf.Lerp(7f, 12.5f, size)
                         : Mathf.Lerp(9f, 17f, size);
                c.trees.Add(t);
            }

        foreach (var g in giants)
            if (g.pos.x >= minX && g.pos.x < minX + chunkSize && g.pos.z >= minZ && g.pos.z < minZ + chunkSize) c.trees.Add(g);
    }

    // ---------- the far version of a chunk's trees: a handful of triangles each ----------

    void BuildFar(Chunk c)
    {
        if (c.trees.Count == 0) return;
        var b = new MeshBuilder();
        foreach (var t in c.trees) AddTree(b, null, t, c.center, false);

        var go = NewObject("Trees (far)", c.go.transform);
        go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(NewMesh("TreesFar"));
        c.farRenderer = go.AddComponent<MeshRenderer>();
        c.farRenderer.sharedMaterial = foliageMaterial;
        c.farRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------- the near version: full trees, rocks, bushes, glowing things, and everything you can bump into ----------

    void BuildNear(Chunk c)
    {
        c.built = true;
        if (c.go.GetComponent<MeshCollider>() == null) c.go.AddComponent<MeshCollider>().sharedMesh = c.groundMesh;

        var solid = new MeshBuilder();
        var glow = new MeshBuilder();
        c.near = NewObject("Near", c.go.transform);

        foreach (var t in c.trees)
        {
            AddTree(solid, glow, t, c.center, true);

            // only the trunk is solid, so you can brush through the leaves
            var cap = c.near.AddComponent<CapsuleCollider>();
            bool giant = t.kind == Kind.Giant;
            float radius = giant ? TrunkRadius(t) * 0.92f : TrunkRadius(t) * 0.85f;
            float tall = t.height * (t.kind == Kind.Umbrella ? 0.8f : giant ? 0.62f : 0.5f);
            cap.radius = radius;
            cap.height = tall + 2f;
            cap.center = t.pos - c.center + Vector3.up * (tall * 0.5f - 1f);
        }

        AddSmallThings(c, solid, glow);

        if (solid.Count > 0)
        {
            c.near.AddComponent<MeshFilter>().sharedMesh = solid.ToMesh(NewMesh("TreesNear"));
            c.near.AddComponent<MeshRenderer>().sharedMaterial = foliageMaterial;
        }
        if (glow.Count > 0)
        {
            var g = NewObject("Glow", c.near.transform);
            g.AddComponent<MeshFilter>().sharedMesh = glow.ToMesh(NewMesh("Glow"));
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
        c.nearRenderers = c.near.GetComponentsInChildren<MeshRenderer>(true);
        if (!c.seen) foreach (var r in c.nearRenderers) r.enabled = false;
        c.near.SetActive(false);
    }

    static float TrunkRadius(Tree t) =>
        t.kind == Kind.Giant ? t.height * 0.058f : t.kind == Kind.Pine ? t.height * 0.022f + 0.14f : t.height * 0.03f + 0.14f;

    // ---------- trees ----------

    void AddTree(MeshBuilder b, MeshBuilder glow, Tree t, Vector3 origin, bool full)
    {
        var rng = new Rng(t.id);
        Vector3 p = t.pos - origin;
        float H = t.height, r0 = TrunkRadius(t);
        float tint = rng.Range(0.9f, 1.08f);
        Vector3 lean = new Vector3(rng.Range(-1f, 1f), 0f, rng.Range(-1f, 1f)) * (H * 0.035f);

        switch (t.kind)
        {
            case Kind.Pine:
            {
                Color leaf = Pick(pineColors, ref rng) * tint;
                b.Trunk(this, p + Vector3.down * 0.5f, r0, r0 * 0.5f, H * 0.45f, full ? 6 : 3, trunk * 0.92f, lean * 0.3f, full);
                if (full)
                {
                    int tiers = 4;
                    for (int i = 0; i < tiers; i++)
                    {
                        float f = i / (float)(tiers - 1);
                        float y = H * Mathf.Lerp(0.16f, 0.66f, f), rad = H * Mathf.Lerp(0.235f, 0.11f, f), tall = H * Mathf.Lerp(0.36f, 0.34f, f);
                        b.Cone(this, p + lean * f * 0.6f + Vector3.up * y, rad, tall, 8, leaf * Mathf.Lerp(0.9f, 1.08f, f), ref rng);
                    }
                }
                else b.Cone(this, p + Vector3.up * (H * 0.16f), H * 0.215f, H * 0.84f, 5, leaf, ref rng);
                break;
            }

            case Kind.Umbrella:
            {
                Color leaf = Pick(leafColors, ref rng) * tint;
                Color bark = rng.Next() < 0.35f ? trunkPale : trunk;
                b.Trunk(this, p + Vector3.down * 0.5f, r0, r0 * 0.55f, H * 0.84f, full ? 6 : 3, bark, lean, full);
                Vector3 top = p + lean + Vector3.up * (H * 0.86f);
                if (full)
                {
                    b.Blob(this, top, new Vector3(H * 0.30f, H * 0.10f, H * 0.30f), leaf, 1, ref rng, 0.16f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (i + rng.Next() * 0.6f) * Mathf.PI * 0.5f, d = H * rng.Range(0.18f, 0.26f);
                        b.Blob(this, top + new Vector3(Mathf.Cos(a) * d, H * rng.Range(-0.05f, 0.03f), Mathf.Sin(a) * d),
                            new Vector3(H * 0.17f, H * 0.075f, H * 0.17f) * rng.Range(0.85f, 1.15f), leaf * rng.Range(0.92f, 1.1f), 1, ref rng, 0.16f);
                    }
                }
                else b.Blob(this, top, new Vector3(H * 0.40f, H * 0.11f, H * 0.40f), leaf, 0, ref rng, 0f);
                break;
            }

            case Kind.Giant:
            {
                Color leaf = Pick(leafColors, ref rng) * tint;
                Color blossom = Pick(blossomColors, ref rng);
                b.Trunk(this, p + Vector3.down * 1.5f, r0, r0 * 0.5f, H * 0.78f, full ? 10 : 6, trunk * 0.95f, lean * 0.5f, true);
                int blobs = 11;
                for (int i = 0; i < blobs; i++)
                {
                    float a = i * 2.39996f + rng.Next(), f = i / (float)(blobs - 1);
                    float d = H * Mathf.Lerp(0.30f, 0.05f, f * f) * rng.Range(0.8f, 1.15f);
                    float rad = H * rng.Range(0.15f, 0.22f);
                    Vector3 at = p + lean * 0.5f + new Vector3(Mathf.Cos(a) * d, H * Mathf.Lerp(0.60f, 0.93f, f), Mathf.Sin(a) * d);
                    Color col = i % 4 == 3 ? Color.Lerp(leaf, blossom, 0.75f) : leaf * rng.Range(0.9f, 1.08f);   // a few flowering boughs
                    b.Blob(this, at, new Vector3(rad, rad * 0.72f, rad), col, full ? 1 : 0, ref rng, full ? 0.14f : 0f);
                }
                if (full && glow != null)
                {
                    // glowing shelf mushrooms winding up the trunk
                    Color g = Pick(glowColors, ref rng);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 0.9f + rng.Next(), y = 3f + i * H * 0.045f;
                        float rr = Mathf.Lerp(r0, r0 * 0.5f, y / (H * 0.78f)) * 1.02f;
                        Vector3 at = p + lean * 0.5f * (y / (H * 0.78f)) + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                        glow.Blob(this, at, new Vector3(1.5f, 0.28f, 1.5f) * rng.Range(0.7f, 1.2f), g, 0, ref rng, 0f);
                    }
                }
                break;
            }

            default:   // round and blossom trees
            {
                bool flowering = t.kind == Kind.Blossom;
                Color leaf = (flowering ? Pick(blossomColors, ref rng) : Pick(leafColors, ref rng)) * tint;
                Color second = flowering ? Color.Lerp(leaf, Color.white, 0.45f) : leaf * 1.08f;
                Color bark = flowering || rng.Next() < 0.25f ? trunkPale * 0.92f : trunk;
                b.Trunk(this, p + Vector3.down * 0.5f, r0, r0 * 0.6f, H * 0.6f, full ? 6 : 3, bark, lean, full);
                Vector3 mid = p + lean * 0.8f + Vector3.up * (H * 0.66f);
                if (full)
                {
                    b.Blob(this, mid, new Vector3(H * 0.29f, H * 0.26f, H * 0.29f), leaf, 1, ref rng, 0.18f);
                    int side = 3 + (int)(rng.Next() * 2f);
                    for (int i = 0; i < side; i++)
                    {
                        float a = (i + rng.Next() * 0.7f) / side * Mathf.PI * 2f, d = H * rng.Range(0.17f, 0.24f);
                        float rad = H * rng.Range(0.15f, 0.2f);
                        b.Blob(this, mid + new Vector3(Mathf.Cos(a) * d, H * rng.Range(-0.1f, 0.06f), Mathf.Sin(a) * d),
                            new Vector3(rad, rad * 0.85f, rad), i % 2 == 0 ? second : leaf * 0.94f, 1, ref rng, 0.18f);
                    }
                    b.Blob(this, mid + Vector3.up * (H * 0.17f), Vector3.one * (H * 0.17f), second, 1, ref rng, 0.18f);
                }
                else b.Blob(this, mid + Vector3.up * (H * 0.02f), new Vector3(H * 0.40f, H * 0.33f, H * 0.40f), leaf, 0, ref rng, 0f);
                break;
            }
        }
    }

    // ---------- rocks, bushes, mushrooms, crystals, standing stones ----------

    void AddSmallThings(Chunk c, MeshBuilder solid, MeshBuilder glow)
    {
        var rng = new Rng(c.id ^ 0x51ed270bu);
        float half = chunkSize * 0.5f;

        // spot: a random place in the chunk that is not under water; returns false when there is none here
        bool Spot(out Vector3 local, out float woods)
        {
            float x = c.center.x + rng.Range(-half, half), z = c.center.z + rng.Range(-half, half);
            woods = Woods(x, z);
            local = new Vector3(x - c.center.x, HeightLocal(x, z), z - c.center.z);
            if (new Vector2(x, z).magnitude < clearingRadius * 0.9f) return false;
            return !(LakeWeight(x, z, out _, out float q) > 0f && q < 1.05f);
        }

        // bushes, mostly in and around the woods
        for (int i = 0; i < 12; i++)
        {
            if (!Spot(out Vector3 p, out float woods) || woods < forestThreshold - 0.08f) continue;
            float s = rng.Range(0.7f, 1.7f);
            Color col = (rng.Next() < 0.2f ? Pick(blossomColors, ref rng) : Pick(leafColors, ref rng)) * rng.Range(0.82f, 1f);
            solid.Blob(this, p + Vector3.up * s * 0.35f, new Vector3(s, s * 0.7f, s), col, 0, ref rng, 0.2f);
            if (rng.Next() < 0.5f)
                solid.Blob(this, p + new Vector3(rng.Range(-1f, 1f), 0.25f, rng.Range(-1f, 1f)) * s * 0.8f, Vector3.one * s * 0.6f, col * 1.08f, 0, ref rng, 0.2f);
        }

        // rocks; the big ones are solid
        for (int i = 0; i < 4; i++)
        {
            if (!Spot(out Vector3 p, out _)) continue;
            float s = rng.Next() < 0.25f ? rng.Range(1.8f, 3.6f) : rng.Range(0.4f, 1.3f);
            solid.Blob(this, p + Vector3.up * s * 0.2f, new Vector3(s * rng.Range(0.8f, 1.3f), s * rng.Range(0.6f, 0.9f), s * rng.Range(0.8f, 1.3f)),
                rock * rng.Range(0.82f, 1f), 0, ref rng, 0.35f);
            if (s > 1.7f)
            {
                var sc = c.near.AddComponent<SphereCollider>();
                sc.radius = s * 0.72f;
                sc.center = p + Vector3.up * s * 0.1f;
            }
        }

        // clusters of glowing mushrooms under the trees
        for (int i = 0; i < 3; i++)
        {
            if (!Spot(out Vector3 p, out float woods) || woods < forestThreshold + 0.02f || rng.Next() < 0.45f) continue;
            Color cap = Pick(glowColors, ref rng);
            int count = 3 + (int)(rng.Next() * 4f);
            bool big = rng.Next() < 0.12f;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = p + new Vector3(rng.Range(-1.6f, 1.6f), 0f, rng.Range(-1.6f, 1.6f)) * (big ? 2.2f : 1f);
                at.y = HeightLocal(at.x + c.center.x, at.z + c.center.z);
                float tall = (big ? rng.Range(1.4f, 3.2f) : rng.Range(0.35f, 0.95f));
                solid.Trunk(this, at + Vector3.down * 0.1f, tall * 0.1f, tall * 0.07f, tall + 0.1f, 5, trunkPale, Vector3.zero, false);
                glow.Blob(this, at + Vector3.up * tall, new Vector3(tall * 0.5f, tall * 0.26f, tall * 0.5f), cap * rng.Range(0.85f, 1f), 0, ref rng, 0f);
            }
        }

        // now and then a cluster of crystals
        if (rng.Next() < 0.2f && Spot(out Vector3 cp, out _))
        {
            Color col = Pick(glowColors, ref rng);
            int count = 3 + (int)(rng.Next() * 3f);
            for (int k = 0; k < count; k++)
            {
                Vector3 tip = new Vector3(rng.Range(-1f, 1f), 2.4f, rng.Range(-1f, 1f)).normalized * rng.Range(1.6f, 4.2f);
                glow.Spike(this, cp + new Vector3(rng.Range(-0.8f, 0.8f), -0.3f, rng.Range(-0.8f, 0.8f)), tip, rng.Range(0.3f, 0.6f), col * rng.Range(0.8f, 1f));
            }
        }

        // the standing stones around the start
        foreach (var s in stones)
        {
            if (Mathf.Abs(s.x - c.center.x) > half || Mathf.Abs(s.z - c.center.z) > half) continue;
            var srng = new Rng(Hash((int)(s.x * 10f), (int)(s.z * 10f), seed));
            float tall = srng.Range(4.2f, 6.5f), wide = srng.Range(0.8f, 1.1f);
            Vector3 p = new Vector3(s.x - c.center.x, HeightLocal(s.x, s.z) - 0.4f, s.z - c.center.z);
            Vector3 tilt = new Vector3(srng.Range(-0.5f, 0.5f), 0f, srng.Range(-0.5f, 0.5f));
            solid.Trunk(this, p, wide, wide * 0.62f, tall, 5, rock * 0.95f, tilt, false, true);
            glow.Blob(this, p + tilt * 0.6f + Vector3.up * (tall * 0.62f), Vector3.one * 0.34f, glowColors.Length > 0 ? glowColors[0] : Color.white, 0, ref srng, 0f);
            var cap = c.near.AddComponent<CapsuleCollider>();
            cap.radius = wide * 0.8f;
            cap.height = tall;
            cap.center = p + Vector3.up * tall * 0.5f;
        }
    }

    // ---------- motes drifting in the air around the camera ----------

    void MakeMotes()
    {
        var go = new GameObject("Forest Motes");
        motes = go.transform;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.92f, 0.6f), 0f), new GradientColorKey(new Color(0.6f, 1f, 0.95f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };

        var emission = ps.emission;
        emission.rateOverTime = 34f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(70f, 26f, 70f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.45f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.25f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = motesMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    // ---------- helpers ----------

    Mesh NewMesh(string name)
    {
        var m = new Mesh { name = name };
        if (!Application.isPlaying) m.hideFlags = HideFlags.DontSave;
        meshes.Add(m);
        return m;
    }

    // vertex colours go to the shader as they are, so in a linear-colour project they must be stored linear
    internal Color32 ToColor(Color c)
    {
        c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 0f;   // alpha 0: no sparkles (the shader reads it)
        if (linear) c = c.linear;
        return c;
    }

    static Color Pick(Color[] list, ref Rng rng) => list == null || list.Length == 0 ? Color.white : list[Mathf.Min((int)(rng.Next() * list.Length), list.Length - 1)];

    static uint Hash(uint x)
    {
        x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
        return x;
    }

    static uint Hash(int a, int b, int s) => Hash((uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)s * 83492791u);

    internal struct Rng
    {
        uint state;
        public Rng(uint seed) { state = seed; }
        public uint NextUInt() { state += 0x9e3779b9u; return Hash(state); }
        public float Next() => (NextUInt() & 0xffffff) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Next();
    }

    // Collects triangles for one mesh. Shapes are soft, low-poly and shaded by their vertex colours (darker below, lighter on top).
    internal class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Color32> cols = new List<Color32>();
        readonly List<int> tris = new List<int>();

        public int Count => verts.Count;

        public int Add(Vector3 p, Vector3 n, Color32 c)
        {
            verts.Add(p); norms.Add(n); cols.Add(c);
            return verts.Count - 1;
        }

        public void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

        public Mesh ToMesh(Mesh m)
        {
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // a lumpy ball; level 0 = 12 points, level 1 = 42 points
        public void Blob(ForestMap map, Vector3 centre, Vector3 radii, Color color, int level, ref Rng rng, float lumpy)
        {
            Vector3[] dirs = level > 0 ? Ico.Verts1 : Ico.Verts0;
            int[] faces = level > 0 ? Ico.Tris1 : Ico.Tris0;
            int first = verts.Count;
            Vector3 inv = new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z);
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 d = dirs[i];
                float bump = 1f + lumpy * (rng.Next() - 0.5f) * 2f;
                Vector3 p = centre + Vector3.Scale(d, radii) * bump;
                Vector3 n = Vector3.Scale(d, inv).normalized;
                Add(p, n, map.ToColor(color * Mathf.Lerp(0.78f, 1.1f, d.y * 0.5f + 0.5f)));
            }
            for (int i = 0; i < faces.Length; i += 3) Tri(first + faces[i], first + faces[i + 1], first + faces[i + 2]);
        }

        // one tier of a pine: a cone with an uneven skirt and a dark underside
        public void Cone(ForestMap map, Vector3 baseCentre, float radius, float height, int sides, Color color, ref Rng rng)
        {
            int first = verts.Count;
            Color32 low = map.ToColor(color * 0.8f), high = map.ToColor(color * 1.08f), under = map.ToColor(color * 0.6f);
            float turn = rng.Next() * Mathf.PI;
            for (int i = 0; i < sides; i++)
            {
                float a = turn + i / (float)sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 n = (dir * height + Vector3.up * radius).normalized;
                float rr = radius * (0.88f + 0.24f * rng.Next());
                Add(baseCentre + dir * rr + Vector3.down * (height * 0.06f * rng.Next()), n, low);
                Add(baseCentre + Vector3.up * height, n, high);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = first + i * 2, b = first + (i + 1) % sides * 2;
                Tri(a, a + 1, b);
            }
            // underside
            int centre = Add(baseCentre + Vector3.up * (height * 0.08f), Vector3.down, under);
            int ring = verts.Count;
            for (int i = 0; i < sides; i++) Add(verts[first + i * 2], Vector3.down, under);
            for (int i = 0; i < sides; i++) Tri(centre, ring + i, ring + (i + 1) % sides);
        }

        // a tapering trunk (also mushroom stems and standing stones); it flares at the foot and may lean
        public void Trunk(ForestMap map, Vector3 foot, float r0, float r1, float height, int sides, Color color, Vector3 lean, bool flare, bool capped = false)
        {
            int first = verts.Count;
            int rings = flare ? 4 : 2;
            for (int k = 0; k < rings; k++)
            {
                float f = rings == 2 ? k : k == 0 ? 0f : k == 1 ? 0.07f : k == 2 ? 0.22f : 1f;
                float rad = flare && k == 0 ? r0 * 1.7f : flare && k == 1 ? r0 * 1.15f : Mathf.Lerp(r0, r1, f);
                Vector3 centre = foot + lean * f + Vector3.up * (height * f);
                Color32 col = map.ToColor(color * Mathf.Lerp(0.8f, 1.06f, f));
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Add(centre + dir * rad, (dir + Vector3.up * 0.15f).normalized, col);
                }
            }
            for (int k = 0; k < rings - 1; k++)
                for (int i = 0; i < sides; i++)
                {
                    int a = first + k * sides + i, b = first + k * sides + (i + 1) % sides, c = a + sides, d = b + sides;
                    Tri(a, c, b);
                    Tri(b, c, d);
                }
            if (capped)
            {
                Vector3 top = foot + lean + Vector3.up * (height + r1 * 0.5f);
                int centre = Add(top, Vector3.up, map.ToColor(color * 1.1f));
                int ring = first + (rings - 1) * sides;
                for (int i = 0; i < sides; i++) Tri(centre, ring + (i + 1) % sides, ring + i);
            }
        }

        // a crystal: a thin spike with a faceted look
        public void Spike(ForestMap map, Vector3 foot, Vector3 toTip, float radius, Color color)
        {
            const int sides = 5;
            Vector3 axis = toTip.normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized, other = Vector3.Cross(axis, side);
            Vector3 waist = foot + toTip * 0.72f, tip = foot + toTip;
            Color32 low = map.ToColor(color * 0.7f), mid = map.ToColor(color), high = map.ToColor(Color.Lerp(color, Color.white, 0.6f));
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = side * Mathf.Cos(a0) + other * Mathf.Sin(a0), d1 = side * Mathf.Cos(a1) + other * Mathf.Sin(a1);
                Vector3 n = (d0 + d1).normalized;
                int v = verts.Count;
                Add(foot + d0 * radius, n, low); Add(foot + d1 * radius, n, low);
                Add(waist + d0 * radius, n, mid); Add(waist + d1 * radius, n, mid);
                Add(tip, (n + axis).normalized, high);
                Tri(v, v + 1, v + 2); Tri(v + 1, v + 3, v + 2); Tri(v + 2, v + 3, v + 4);
            }
        }
    }

    // unit ball shapes shared by every blob
    static class Ico
    {
        public static readonly Vector3[] Verts0, Verts1;
        public static readonly int[] Tris0, Tris1;

        static Ico()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            Verts0 = v.ToArray();
            Tris0 = f.ToArray();

            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var f1 = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                f1.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            Verts1 = v.ToArray();
            Tris1 = f1.ToArray();
        }
    }
}
