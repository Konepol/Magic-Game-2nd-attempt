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

    public Health Health => health;

    // true if a shot with this damage tag (the attacker's target tag) is stopped by this barrier
    public bool Blocks(string attackerDamageTag) => attackerDamageTag == protectsTag;

    // returns true if the barrier was destroyed
    public bool Hit(float damage, bool breaksInstantly, Vector3 point)
    {
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
        float hp = Mathf.Clamp01(health.Current / health.Max);
        float fade = Mathf.Clamp01(remaining / FadeTime);
        Color c = Color.Lerp(new Color(1f, 0.35f, 0.2f, baseColor.a), baseColor, hp);
        c.a = Mathf.Lerp(0.18f, baseColor.a, hp) * fade;
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
        if (frontMat != null) Destroy(frontMat);
        if (backMat != null) Destroy(backMat);
        if (frontTex != null) Destroy(frontTex);
        if (backTex != null) Destroy(backTex);
    }
}
