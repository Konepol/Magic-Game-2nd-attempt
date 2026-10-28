using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A wall with health. Blocks projectiles and beams from the side it protects against.
// Takes visible cracks where it is hit, shifts toward red as it weakens, and shows a health bar once damaged.
public class Barrier : MonoBehaviour
{
    const int TexSize = 256;
    const float FadeTime = 1f;

    string protectsTag = "Player";
    Health health;
    Material mat;
    Color baseColor;
    float expireTime;
    float flashUntil;

    // crack overlays, one per face
    Transform frontQuad, backQuad;
    Texture2D frontTex, backTex;
    Color32[] frontPixels, backPixels;
    Material frontMat, backMat;

    // a barrier backed by the player's mana: damage drains mana instead of health, it never expires, and it breaks when the mana runs out
    Mana manaSource;
    float manaPerDamage = 1f;
    bool broken;

    // the player's barrier is a cluster of hexagon panels; each can be cracked, and some can be switched off (see SetCoverage)
    enum TileState { Up, Broken, Absent }     // Broken = a hole left by a hit, fixed first when mana comes back; Absent = not grown yet

    class Tile
    {
        public TileState state = TileState.Up;
        public Transform root;
        public Renderer fill;
        public LineRenderer edge;
        public BoxCollider collider;
        public float width, height;          // size of the hexagon's bounding box, in metres
        public float scale = 1f;             // 0 = switched off, 1 = fully shown
        public float flashUntil;

        // crack layers, made the first time the tile is hit
        public Transform frontQuad, backQuad;
        public Texture2D frontTex, backTex;
        public Color32[] frontPixels, backPixels;
        public Material frontMat, backMat;
    }

    List<Tile> tiles;                  // nearest the middle first
    float coverage = 1f;
    float lastMana;                    // to notice mana spent on spells and mana coming back
    float pendingLoss, pendingSpend, pendingGain;     // fractions of a panel not yet turned into a whole one
    Material edgeMat;
    MaterialPropertyBlock block;
    static Mesh hexFront, hexBoth;
    const float TileGap = 0.95f;
    const float BreakDamageMultiplier = 3f;       // shots that break barriers instantly drain this many times as much

    public Health Health => health;
    public event System.Action Broken;

    // true if a shot with this damage tag (the attacker's target tag) is stopped by this barrier
    public bool Blocks(string attackerDamageTag) => attackerDamageTag == protectsTag;

    // returns true if the barrier was destroyed
    public bool Hit(float damage, bool breaksInstantly, Vector3 point)
    {
        if (manaSource != null) return HitMana(damage, breaksInstantly, point);
        if (health == null || health.IsDead) return false;

        float before = health.Current;
        health.TakeDamage(breaksInstantly ? health.Max * 100f : damage, point);
        float lost = (before - health.Current) / health.Max;

        flashUntil = Time.time + 0.12f;
        AddCrack(point, breaksInstantly ? 1f : lost);

        if (health.IsDead)
        {
            SpellFX.Shatter(point, baseColor);
            SpellFX.Play(Sfx.BarrierHit, point, 1f);
        }
        return health.IsDead;
    }

    bool HitMana(float damage, bool breaksInstantly, Vector3 point)
    {
        if (broken) return false;

        float taken = manaSource.Drain(damage * manaPerDamage * (breaksInstantly ? BreakDamageMultiplier : 1f));
        flashUntil = Time.time + 0.12f;
        lastMana = manaSource.Current;                      // this loss is the hit's, not spell spending

        if (tiles != null)
        {
            // the mana the hit took away is panels lost: the panels nearest the hit are the ones that break
            CrackNearestTile(point, taken / Mathf.Max(manaSource.Max, 1f));
            pendingLoss += taken / Mathf.Max(manaSource.Max, 1f) * tiles.Count;
            while (pendingLoss >= 1f && BreakNearestTile(point)) pendingLoss -= 1f;
            if (pendingLoss >= 1f) pendingLoss = 0f;
        }
        else AddCrack(point, taken / Mathf.Max(manaSource.Max, 1f));

        if (manaSource.Current <= 0.01f) Break(point);
        return broken;
    }

    void Break(Vector3 point)
    {
        if (broken) return;
        broken = true;
        SpellFX.Shatter(point, baseColor);
        SpellFX.Play(Sfx.BarrierHit, point, 1f);
        Broken?.Invoke();
        Destroy(gameObject);
    }

    // the player's own barrier: a honeycomb of hexagon panels, no expiry, health is the player's mana (see PlayerBarrier).
    // width / height are the size of the cluster; hexRadius the size of one panel (centre to corner).
    public static Barrier SpawnFromMana(Vector3 position, Quaternion rotation, float width, float height, float hexRadius, float thickness,
                                        Mana mana, float manaPerDamage, string protectsTag, Color color)
    {
        var go = new GameObject("Barrier");
        go.transform.SetPositionAndRotation(position, rotation);

        var b = go.AddComponent<Barrier>();
        b.protectsTag = protectsTag;
        b.baseColor = color;
        b.expireTime = float.MaxValue;
        b.manaSource = mana;
        b.manaPerDamage = manaPerDamage;
        b.block = new MaterialPropertyBlock();
        b.mat = SpellVisuals.Make(color, true);
        b.edgeMat = SpellVisuals.Make(new Color(0.9f, 0.98f, 1f, 0.95f), true);

        b.health = go.AddComponent<Health>();          // only so area damage can tell barriers apart; the mana is the real health
        b.health.Configure(mana.Max, false, 0.05f);

        var body = go.AddComponent<Rigidbody>();       // it moves every frame: a kinematic body keeps that cheap
        body.isKinematic = true;

        b.BuildTiles(width, height, hexRadius, thickness);

        // the player walks through it
        var protectedBody = GameObject.FindGameObjectWithTag(protectsTag);
        var cc = protectedBody != null ? protectedBody.GetComponentInParent<CharacterController>() : null;
        if (cc != null)
            foreach (var t in b.tiles) Physics.IgnoreCollision(t.collider, cc);

        // ground enemies path around it; it moves with the player, so the navmesh is only carved while it holds still
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(width, height, thickness);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        return b;
    }

    // ---------- hexagon panels ----------

    // flat-topped hexagon filling a unit box (scaled to the panel's size); the front faces -z like a Unity quad
    static Mesh HexMesh(bool bothSides)
    {
        ref Mesh cache = ref (bothSides ? ref hexBoth : ref hexFront);
        if (cache != null) return cache;

        var verts = new Vector3[7];
        var uvs = new Vector2[7];
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            verts[i + 1] = new Vector3(0.5f * Mathf.Cos(a), Mathf.Sin(a) / Mathf.Sqrt(3f), 0f);
        }
        for (int i = 0; i < 7; i++) uvs[i] = new Vector2(verts[i].x + 0.5f, verts[i].y + 0.5f);

        var tris = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            int c0 = 1 + i, c1 = 1 + (i + 1) % 6;
            tris.Add(0); tris.Add(c1); tris.Add(c0);                       // clockwise seen from -z
            if (bothSides) { tris.Add(0); tris.Add(c0); tris.Add(c1); }
        }

        cache = new Mesh { name = bothSides ? "HexBoth" : "HexFront", vertices = verts, uv = uvs, triangles = tris.ToArray() };
        cache.RecalculateNormals();
        cache.RecalculateBounds();
        return cache;
    }

    void BuildTiles(float width, float height, float radius, float thickness)
    {
        tiles = new List<Tile>();
        float w = 2f * radius, h = Mathf.Sqrt(3f) * radius;           // bounding box of one hexagon
        float dx = 1.5f * radius;                                       // distance between columns

        for (int col = -8; col <= 8; col++)
        {
            float x = col * dx;
            float offset = (col % 2 != 0) ? h * 0.5f : 0f;           // every other column sits half a panel higher
            for (int row = -8; row <= 8; row++)
            {
                float y = row * h + offset;
                float nx = x / (width * 0.5f), ny = y / (height * 0.5f);
                if (nx * nx + ny * ny > 1.1f) continue;                // the cluster is roughly oval
                tiles.Add(MakeTile(new Vector2(x, y), w, h, thickness));
            }
        }

        tiles.Sort((p, q) => p.root.localPosition.sqrMagnitude.CompareTo(q.root.localPosition.sqrMagnitude));

        // as many panels as the mana supports, starting from the middle
        int up = Mathf.Clamp(Mathf.CeilToInt(tiles.Count * manaSource.Current / Mathf.Max(manaSource.Max, 1f)), 1, tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].state = i < up ? TileState.Up : TileState.Absent;
            tiles[i].scale = i < up ? 1f : 0f;
        }
        lastMana = manaSource.Current;
    }

    Tile MakeTile(Vector2 position, float w, float h, float thickness)
    {
        var t = new Tile { width = w, height = h };

        var go = new GameObject("Hex");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        t.root = go.transform;

        go.AddComponent<MeshFilter>().sharedMesh = HexMesh(true);
        t.fill = go.AddComponent<MeshRenderer>();
        t.fill.sharedMaterial = mat;
        t.fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.fill.receiveShadows = false;

        // a bright outline, like the glowing edges of the panels
        var edgeGo = new GameObject("Edge");
        edgeGo.transform.SetParent(go.transform, false);
        t.edge = edgeGo.AddComponent<LineRenderer>();
        t.edge.useWorldSpace = false;
        t.edge.loop = true;
        t.edge.positionCount = 6;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            t.edge.SetPosition(i, new Vector3(0.5f * Mathf.Cos(a), Mathf.Sin(a) / Mathf.Sqrt(3f), 0f));
        }
        t.edge.widthMultiplier = 0.045f;
        t.edge.sharedMaterial = edgeMat;
        t.edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.edge.receiveShadows = false;

        // a box that roughly fills the hexagon (the local box is measured in the panel's own scaled space)
        t.collider = go.AddComponent<BoxCollider>();
        t.collider.size = new Vector3(0.6f, 0.92f, thickness);

        go.transform.localScale = new Vector3(w * TileGap, h * TileGap, 1f);
        return t;
    }

    // how much of the cluster is up: 1 = every panel, 0.5 = the half nearest the middle (the outer panels shrink away)
    public void SetCoverage(float fraction) => coverage = Mathf.Clamp01(fraction);

    int UpCount()
    {
        int n = 0;
        foreach (var t in tiles) if (t.state == TileState.Up) n++;
        return n;
    }

    // mana spent on spells takes panels from the outside; mana coming back fixes the holes first, then adds panels from the middle out
    void TrackMana()
    {
        float cur = manaSource.Current;
        float panels = (cur - lastMana) / Mathf.Max(manaSource.Max, 1f) * tiles.Count;
        lastMana = cur;

        if (panels > 0f)
        {
            pendingGain += panels;
            while (pendingGain >= 1f && GrowOrRepair()) pendingGain -= 1f;
            if (pendingGain >= 1f) pendingGain = 0f;               // nothing left to fix or add
        }
        else if (panels < 0f)
        {
            pendingSpend -= panels;
            while (pendingSpend >= 1f && DropOutermost()) pendingSpend -= 1f;
            if (pendingSpend >= 1f) pendingSpend = 0f;
        }
    }

    bool GrowOrRepair()
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var wanted = pass == 0 ? TileState.Broken : TileState.Absent;
            foreach (var t in tiles)               // nearest the middle first
                if (t.state == wanted)
                {
                    t.state = TileState.Up;
                    t.flashUntil = Time.time + 0.2f;
                    ClearCracks(t);
                    return true;
                }
        }
        return false;
    }

    bool DropOutermost()
    {
        if (UpCount() <= 1) return false;
        for (int i = tiles.Count - 1; i >= 0; i--)
            if (tiles[i].state == TileState.Up) { tiles[i].state = TileState.Absent; return true; }
        return false;
    }

    // the panel nearest the hit breaks and leaves a hole
    bool BreakNearestTile(Vector3 point)
    {
        if (UpCount() <= 1) return false;

        Tile best = null;
        float bestDist = float.MaxValue;
        foreach (var t in tiles)
        {
            if (t.state != TileState.Up) continue;
            float d = (t.root.position - point).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; }
        }
        if (best == null) return false;

        best.state = TileState.Broken;
        SpellFX.Shatter(best.root.position, baseColor);
        return true;
    }

    void ClearCracks(Tile t)
    {
        if (t.frontTex == null) return;
        System.Array.Clear(t.frontPixels, 0, t.frontPixels.Length);
        System.Array.Clear(t.backPixels, 0, t.backPixels.Length);
        t.frontTex.SetPixels32(t.frontPixels);
        t.frontTex.Apply();
        t.backTex.SetPixels32(t.backPixels);
        t.backTex.Apply();
    }

    void UpdateTiles(Color panel, float hp)
    {
        TrackMana();

        float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
        int activeUp = Mathf.Max(1, Mathf.CeilToInt(UpCount() * coverage));     // fewer shown while charging
        int seen = 0;

        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            bool on = false;
            if (t.state == TileState.Up) { on = seen < activeUp; seen++; }

            t.scale = Mathf.Lerp(t.scale, on ? 1f : 0f, t.state == TileState.Broken ? 1f : k);      // a broken panel is gone at once

            bool visible = t.scale > 0.02f;
            t.fill.enabled = visible;
            t.edge.enabled = visible;
            t.collider.enabled = on && t.scale > 0.6f;
            t.root.localScale = new Vector3(t.width * TileGap * t.scale, t.height * TileGap * t.scale, 1f);

            Color c = panel;
            float f = Mathf.Clamp01((t.flashUntil - Time.time) / 0.15f);
            if (f > 0f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.75f), f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            t.fill.SetPropertyBlock(block);
        }

        SpellVisuals.SetColor(edgeMat, new Color(0.9f, 0.98f, 1f, 0.95f));
    }

    void CrackNearestTile(Vector3 point, float lost)
    {
        Tile best = null;
        float bestDist = float.MaxValue;
        foreach (var t in tiles)
        {
            if (t.state != TileState.Up || t.scale < 0.5f) continue;
            float d = (t.root.position - point).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; }
        }
        if (best == null) return;

        best.flashUntil = Time.time + 0.15f;
        if (best.frontQuad == null)
        {
            MakeTileLayer(best, -0.02f, 0f, out best.frontQuad, out best.frontTex, out best.frontPixels, out best.frontMat);
            MakeTileLayer(best, 0.02f, 180f, out best.backQuad, out best.backTex, out best.backPixels, out best.backMat);
        }

        // the same crack on both faces, at the same spot in the world
        var state = Random.state;
        DrawCrackOn(best.frontQuad, best.frontPixels, best.frontTex, point, Mathf.Max(lost, 0.05f));
        Random.state = state;
        DrawCrackOn(best.backQuad, best.backPixels, best.backTex, point, Mathf.Max(lost, 0.05f));
    }

    void MakeTileLayer(Tile tile, float z, float yRotation, out Transform quad, out Texture2D tex, out Color32[] pixels, out Material material)
    {
        var q = new GameObject(z < 0f ? "CracksFront" : "CracksBack");
        q.transform.SetParent(tile.root, false);
        q.transform.localPosition = new Vector3(0f, 0f, z);
        q.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        q.AddComponent<MeshFilter>().sharedMesh = HexMesh(false);

        tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[TexSize * TexSize];
        tex.SetPixels32(pixels);
        tex.Apply();

        material = SpellVisuals.Make(Color.white, true);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
        material.mainTexture = tex;

        var r = q.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad = q.transform;
    }

    public static Barrier Spawn(Vector3 position, Quaternion rotation, Vector3 size, float maxHealth,
                                float duration, string protectsTag, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Barrier";
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = size;

        var b = go.AddComponent<Barrier>();
        b.protectsTag = protectsTag;
        b.baseColor = color;
        b.expireTime = Time.time + duration;
        b.mat = SpellVisuals.Make(color, true);
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = b.mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // the side it protects walks through it: no standing on a tilted wall, no getting trapped inside one
        var protectedBody = GameObject.FindGameObjectWithTag(protectsTag);
        if (protectedBody != null)
        {
            var cc = protectedBody.GetComponentInParent<CharacterController>();
            if (cc != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), cc);
        }

        // ground enemies path around the wall (the player is unaffected: the wall does not collide with them)
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;          // the wall's own scale makes it the right size
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        b.health = go.AddComponent<Health>();
        b.health.Configure(maxHealth, true, 0.05f);
        // bar above the wall, shown while it is being damaged (same component and style as the enemy bars)
        var barGo = new GameObject("HealthBar");
        barGo.transform.SetParent(go.transform, false);
        barGo.AddComponent<WorldHealthBar>();

        b.BuildCrackLayers();
        return b;
    }

    // ---------- cracks ----------

    void BuildCrackLayers()
    {
        MakeLayer("CracksFront", -0.5f - 0.02f, 0f, out frontQuad, out frontTex, out frontPixels, out frontMat);
        MakeLayer("CracksBack", 0.5f + 0.02f, 180f, out backQuad, out backTex, out backPixels, out backMat);
    }

    // a transparent quad just outside one face of the cube that holds the crack texture
    void MakeLayer(string name, float z, float yRotation, out Transform quad, out Texture2D tex, out Color32[] pixels, out Material material)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        q.transform.localPosition = new Vector3(0f, 0f, z);
        q.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[TexSize * TexSize];
        tex.SetPixels32(pixels);
        tex.Apply();

        material = SpellVisuals.Make(Color.white, true);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
        material.mainTexture = tex;

        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad = q.transform;
    }

    // lost = fraction of max health this hit removed; bigger hits make longer cracks
    void AddCrack(Vector3 worldPoint, float lost)
    {
        if (frontQuad == null) return;

        // the same crack goes on both faces, at the same spot in the world, so it can also be seen through the wall from behind
        var state = Random.state;
        DrawCrackOn(frontQuad, frontPixels, frontTex, worldPoint, lost);
        Random.state = state;                                   // same random pattern on the other face
        DrawCrackOn(backQuad, backPixels, backTex, worldPoint, lost);
    }

    void DrawCrackOn(Transform quad, Color32[] pixels, Texture2D tex, Vector3 worldPoint, float lost)
    {
        Vector3 local = quad.InverseTransformPoint(worldPoint);
        Vector2 origin = new Vector2(
            Mathf.Clamp01(local.x + 0.5f) * (TexSize - 1),
            Mathf.Clamp01(local.y + 0.5f) * (TexSize - 1));

        float length = Mathf.Lerp(30f, 100f, Mathf.Clamp01(lost * 4f));
        int branches = Random.Range(3, 6);
        for (int i = 0; i < branches; i++)
            DrawCrack(pixels, origin, Random.value * Mathf.PI * 2f, length * Random.Range(0.6f, 1f), 2, true);

        tex.SetPixels32(pixels);
        tex.Apply();
    }

    // jagged line that wanders a little and sometimes forks
    void DrawCrack(Color32[] pixels, Vector2 from, float angle, float length, int thickness, bool canFork)
    {
        Vector2 p = from;
        float travelled = 0f;
        while (travelled < length)
        {
            float step = Random.Range(8f, 16f);
            angle += Random.Range(-0.6f, 0.6f);
            Vector2 next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
            DrawLine(pixels, p, next, thickness);

            if (canFork && Random.value < 0.25f)
                DrawCrack(pixels, next, angle + Random.Range(-1.2f, 1.2f), (length - travelled) * 0.5f, 1, false);

            p = next;
            travelled += step;
        }
    }

    static void DrawLine(Color32[] pixels, Vector2 a, Vector2 b, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : i / (float)steps);
            for (int dx = -thickness + 1; dx < thickness; dx++)
                for (int dy = -thickness + 1; dy < thickness; dy++)
                {
                    int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                    if (x < 0 || y < 0 || x >= TexSize || y >= TexSize) continue;
                    pixels[y * TexSize + x] = new Color32(235, 250, 255, 255);
                }
        }
    }

    // ---------- per-frame look ----------

    void Update()
    {
        if (health == null) return;

        float remaining = expireTime - Time.time;
        if (remaining <= 0f) { Destroy(gameObject); return; }

        // fainter and redder as it takes damage, brief bright flash on a hit, fades out before it expires
        if (manaSource != null && manaSource.Current <= 0.01f) { Break(transform.position); return; }     // spells used up the mana it runs on

        float hp = Mathf.Clamp01(manaSource != null ? manaSource.Current / manaSource.Max : health.Current / health.Max);
        float fade = Mathf.Clamp01(remaining / FadeTime);
        Color c = Color.Lerp(new Color(1f, 0.35f, 0.2f, baseColor.a), baseColor, hp);
        c.a = Mathf.Lerp(0.18f, baseColor.a, hp) * fade;
        if (tiles != null) { UpdateTiles(baseColor, hp); return; }      // the player's barrier keeps its colour however low the mana is

        if (Time.time < flashUntil) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.7f), (flashUntil - Time.time) / 0.12f);
        SpellVisuals.SetColor(mat, c);

        // cracks fade with the wall
        if (frontMat != null)
        {
            var cc = new Color(1f, 1f, 1f, fade);
            SpellVisuals.SetColor(frontMat, cc);
            SpellVisuals.SetColor(backMat, cc);
        }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (edgeMat != null) Destroy(edgeMat);
        if (tiles != null)
            foreach (var t in tiles)
            {
                if (t.frontMat != null) Destroy(t.frontMat);
                if (t.backMat != null) Destroy(t.backMat);
                if (t.frontTex != null) Destroy(t.frontTex);
                if (t.backTex != null) Destroy(t.backTex);
            }
        if (frontMat != null) Destroy(frontMat);
        if (backMat != null) Destroy(backMat);
        if (frontTex != null) Destroy(frontTex);
        if (backTex != null) Destroy(backTex);
    }
}
