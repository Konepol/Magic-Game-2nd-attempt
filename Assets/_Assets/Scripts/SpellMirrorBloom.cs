using System.Collections.Generic;
using UnityEngine;

// Mirror Bloom. Glass petals open in front of the witch.
// Tap: a short window; an enemy shot that reaches the petals inside it is sent back and breaks into a shard storm (the first moments count
// as a perfect parry: a bigger storm and half the mana back). Miss and the petals close dull.
// Hold: a slow dome that bends shots around you while you move inside it.
// Output: bigger petals and dome, a bigger shard storm.

static class PetalMesh
{
    static Mesh cached;

    // a pointed lens shape standing in the XY plane, base at the origin and tip at +Y
    public static Mesh Get()
    {
        if (cached != null) return cached;
        const int n = 10;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float k = i / (float)n;
            float w = Mathf.Sin(k * Mathf.PI) * 0.32f * (1f - 0.25f * k);
            verts.Add(new Vector3(-w, k, 0f));
            verts.Add(new Vector3(w, k, 0f));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2;
            tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
        }
        cached = new Mesh { name = "Petal" };
        cached.SetVertices(verts);
        cached.SetTriangles(tris, 0);
        cached.RecalculateNormals();
        cached.RecalculateBounds();
        return cached;
    }
}

public class BloomParry : MonoBehaviour
{
    PlayerSpellCaster owner;
    SpellCastContext c;
    Transform petals;
    readonly List<Transform> blades = new List<Transform>();
    Material glass, edge;
    float t, window = 0.42f, perfectWindow = 0.16f, size;
    bool done;
    float costBack;
    Color color;

    public static void Cast(SpellCastContext c)
    {
        var go = new GameObject("MirrorBloomParry");
        var p = go.AddComponent<BloomParry>();
        p.c = c;
        p.owner = c.owner;
        p.color = c.v.effectColor;
        p.size = 2.2f * Mathf.Max(0.6f, c.v.projectileScale);
        p.costBack = c.v.manaCost * 0.5f;
        p.Build();
        SpellFX.Play(Sfx.BarrierPlace, c.origin, 0.8f);
    }

    void Build()
    {
        petals = new GameObject("Petals").transform;
        petals.SetParent(transform, false);
        glass = SpellUtil.Solid(new Color(color.r, color.g, color.b, 0.35f));
        edge = SpellUtil.Glow(new Color(1f, 1f, 1f, 0.9f));
        for (int i = 0; i < 7; i++)
        {
            var arm = new GameObject("Petal" + i).transform;
            arm.SetParent(petals, false);
            arm.localRotation = Quaternion.Euler(0f, 0f, i * (360f / 7f));
            var inner = new GameObject("Shape").transform;
            inner.SetParent(arm, false);
            inner.localRotation = Quaternion.Euler(-28f, 0f, 0f);     // tilted a little forward like a flower
            AddShape(inner, glass, 1f);
            AddShape(inner, edge, 1.04f);
            blades.Add(arm);
        }
    }

    void AddShape(Transform parent, Material m, float s)
    {
        var go = new GameObject("Mesh");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(s, s, s);
        go.AddComponent<MeshFilter>().sharedMesh = PetalMesh.Get();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Vector3 Center()
    {
        Vector3 aim = owner.AimDirection;
        return c.root.position + Vector3.up * 1.2f + aim * 2.4f;
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;

        Vector3 aim = owner.AimDirection;
        petals.position = Center();
        petals.rotation = Quaternion.LookRotation(aim);

        float open = Mathf.Clamp01(t / 0.12f);
        float eased = 1f - (1f - open) * (1f - open);
        float closing = done ? Mathf.Clamp01((t - closeStart) / 0.25f) : (t > window ? Mathf.Clamp01((t - window) / 0.25f) : 0f);
        float s = size * eased * (1f - closing);
        petals.localScale = Vector3.one * s;
        petals.Rotate(0f, 0f, 120f * Time.deltaTime, Space.Self);

        if (!done && t <= window) TryReflect();

        if (!done && t > window)
        {
            done = true;
            closeStart = t;
            owner.Notify("Missed");
            var dull = glass.color; dull.a = 0.12f; SpellVisuals.SetColor(glass, dull);
        }
        if (done && t - closeStart > 0.3f) Destroy(gameObject);
    }

    float closeStart;

    void TryReflect()
    {
        Vector3 center = Center();
        float reach = size * 1.9f;
        bool perfect = t <= perfectWindow;
        bool any = false;

        for (int i = BossProjectile.Active.Count - 1; i >= 0; i--)
        {
            var b = BossProjectile.Active[i];
            if (b == null || b.Friendly || Vector3.Distance(b.transform.position, center) > reach) continue;
            Vector3 from = b.transform.position;
            Vector3 dir = ReturnDirection(from, -b.Velocity.normalized);
            b.Reflect(dir * Mathf.Max(18f, b.Velocity.magnitude * 1.4f));
            ShardStorm.Spawn(from, dir, c, perfect);
            any = true;
        }
        for (int i = Projectile.EnemyShots.Count - 1; i >= 0; i--)
        {
            var p = Projectile.EnemyShots[i];
            if (p == null || Vector3.Distance(p.transform.position, center) > reach) continue;
            Vector3 from = p.transform.position;
            Vector3 dir = ReturnDirection(from, -p.transform.forward);
            p.Reflect(dir);
            ShardStorm.Spawn(from, dir, c, perfect);
            any = true;
        }

        if (!any) return;
        done = true;
        closeStart = t;
        window = 0f;
        if (c.mana != null) c.mana.Restore(perfect ? costBack * 2f : costBack);
        owner.Notify(perfect ? "Perfect parry!" : "Parry!");
        SpellFX.Play(Sfx.BarrierHit, center, 1f);
        SpellFX.Burst(center, 2.5f, perfect ? new Color(1f, 0.9f, 0.5f) : color, perfect ? 60 : 30);
        CameraShake.Add(perfect ? 0.5f : 0.3f);
    }

    // back toward the nearest enemy, otherwise straight back where it came from
    Vector3 ReturnDirection(Vector3 from, Vector3 fallback)
    {
        Health best = null;
        float bestD = 60f;
        foreach (var h in SpellUtil.Enemies(from, 60f, c.damageTag))
        {
            float d = Vector3.Distance(from, h.transform.position);
            if (d < bestD) { bestD = d; best = h; }
        }
        return best != null ? (SpellUtil.CenterOf(best) - from).normalized : fallback;
    }

    void OnDestroy()
    {
        if (glass != null) Destroy(glass);
        if (edge != null) Destroy(edge);
    }
}

// the reflected shot breaks into shards that fly out toward enemies
public class ShardStorm : MonoBehaviour
{
    class Shard { public Transform t; public Vector3 vel; public Health target; public float life; }
    readonly List<Shard> shards = new List<Shard>();
    string enemyTag;
    float damage;
    Material mat;
    Color color;

    public static void Spawn(Vector3 position, Vector3 dir, SpellCastContext c, bool perfect)
    {
        var go = new GameObject("ShardStorm");
        go.transform.position = position;
        var s = go.AddComponent<ShardStorm>();
        s.enemyTag = c.damageTag;
        s.damage = c.v.damage * (perfect ? 1.5f : 1f);
        s.color = c.v.effectColor;
        s.mat = SpellUtil.Glow(new Color(0.85f, 0.97f, 1f, 0.95f));

        int count = Mathf.Max(6, 8 + c.v.projectileCount * 2) + (perfect ? 6 : 0);
        var enemies = SpellUtil.Enemies(position, 50f, c.damageTag);
        for (int i = 0; i < count; i++)
        {
            var sh = new Shard { life = 1.6f };
            var g = new GameObject("Shard");
            g.transform.SetParent(go.transform, false);
            g.transform.position = position;
            g.AddComponent<MeshFilter>().sharedMesh = PetalMesh.Get();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = s.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var trail = g.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.widthMultiplier = 0.12f; trail.sharedMaterial = s.mat;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

            Vector3 scatter = Random.onUnitSphere * 0.55f;
            if (enemies.Count > 0)
            {
                sh.target = enemies[i % enemies.Count];
                sh.vel = (SpellUtil.CenterOf(sh.target) - position).normalized;
            }
            else sh.vel = dir;
            sh.vel = (sh.vel + scatter).normalized * Random.Range(30f, 42f);
            sh.t = g.transform;
            s.shards.Add(sh);
        }
        SpellFX.Play(Sfx.Explosion, position, 0.7f);
    }

    void Update()
    {
        for (int i = shards.Count - 1; i >= 0; i--)
        {
            var s = shards[i];
            s.life -= Time.deltaTime;
            if (s.target != null && !s.target.IsDead)
            {
                Vector3 want = (SpellUtil.CenterOf(s.target) - s.t.position).normalized * s.vel.magnitude;
                s.vel = Vector3.RotateTowards(s.vel, want, 5f * Time.deltaTime, 0f);
            }
            Vector3 to = s.t.position + s.vel * Time.deltaTime;
            s.t.rotation = Quaternion.LookRotation(Vector3.Cross(s.vel, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(s.vel, Vector3.up) : Vector3.forward, s.vel);

            bool gone = s.life <= 0f;
            if (!gone && Physics.Linecast(s.t.position, to, out RaycastHit hit, ~((1 << 2) | (1 << LayerMask.NameToLayer("Player"))), QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && h.gameObject.CompareTag(enemyTag)) { h.TakeDamage(damage * 0.35f, hit.point); SpellFX.Sparks(hit.point, color); }
                gone = true;
            }
            s.t.position = to;
            if (gone) { Destroy(s.t.gameObject); shards.RemoveAt(i); }
        }
        if (shards.Count == 0) Destroy(gameObject);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

// the held form: a slow dome that bends shots around the witch
public class BendDome : MonoBehaviour
{
    PlayerSpellCaster owner;
    Transform root;
    float radius, life, t;
    GameObject shell;
    Material glass, lineMat;
    readonly List<LineRenderer> rings = new List<LineRenderer>();
    Color color;

    public static void Cast(SpellCastContext c)
    {
        var go = new GameObject("MirrorBloomDome");
        var d = go.AddComponent<BendDome>();
        d.owner = c.owner;
        d.root = c.root;
        d.radius = Mathf.Max(2.5f, c.v.barrierWidth * 0.5f);
        d.life = Mathf.Max(2f, c.v.barrierDuration);
        d.color = c.v.effectColor;
        d.Build();
        SpellFX.Play(Sfx.BarrierPlace, c.root.position, 1f);
    }

    void Build()
    {
        shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.transform.SetParent(transform, false);
        Destroy(shell.GetComponent<Collider>());
        glass = SpellUtil.Solid(new Color(color.r, color.g, color.b, 0.12f));
        var r = shell.GetComponent<Renderer>();
        r.sharedMaterial = glass;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        lineMat = SpellUtil.Glow(new Color(0.9f, 1f, 1f, 0.7f));
        for (int i = 0; i < 3; i++) rings.Add(SpellVisuals.CreateRing(transform, lineMat, 0.04f));
    }

    void Update()
    {
        if (owner == null || root == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        Vector3 center = root.position + Vector3.up * 1.1f;
        transform.position = center;

        float open = Mathf.Clamp01(t / 0.35f);
        float fade = Mathf.Clamp01((life - t) / 0.6f);
        float scale = radius * 2f * (1f - Mathf.Pow(1f - open, 2f)) * (0.9f + 0.1f * fade);
        shell.transform.localScale = Vector3.one * scale;
        var g = glass.color; g.a = 0.12f * fade; SpellVisuals.SetColor(glass, g);
        var l = lineMat.color; l.a = 0.7f * fade; SpellVisuals.SetColor(lineMat, l);

        // slowly turning rings give the glass its petal-like look
        for (int i = 0; i < rings.Count; i++)
        {
            var lr = rings[i];
            float a = t * (0.5f + i * 0.2f) + i * 1.05f;
            Quaternion q = Quaternion.Euler(60f * i, a * Mathf.Rad2Deg, 30f * i);
            for (int p = 0; p < lr.positionCount; p++)
            {
                float ang = p / (float)lr.positionCount * Mathf.PI * 2f;
                lr.SetPosition(p, center + q * new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (scale * 0.5f));
            }
        }

        Bend(center, scale * 0.5f);
        if (t >= life) Destroy(gameObject);
    }

    // shots that reach the glass slide around it instead of getting through
    void Bend(Vector3 center, float r)
    {
        foreach (var b in BossProjectile.Active)
        {
            if (b == null || b.Friendly) continue;
            Vector3 radial = b.transform.position - center;
            float d = radial.magnitude;
            if (d > r + 0.6f || d < 0.01f) continue;
            radial /= d;
            Vector3 vel = b.Velocity;
            if (Vector3.Dot(vel, radial) < 0f)
            {
                float speed = vel.magnitude;
                b.Velocity = (Vector3.ProjectOnPlane(vel, radial).normalized + radial * 0.45f).normalized * speed * 0.9f;
            }
            if (d < r) b.transform.position = center + radial * r;
        }
        foreach (var p in Projectile.EnemyShots)
        {
            if (p == null) continue;
            Vector3 radial = p.transform.position - center;
            float d = radial.magnitude;
            if (d > r + 0.6f || d < 0.01f) continue;
            radial /= d;
            Vector3 fwd = p.transform.forward;
            if (Vector3.Dot(fwd, radial) < 0f)
                p.transform.rotation = Quaternion.LookRotation((Vector3.ProjectOnPlane(fwd, radial).normalized + radial * 0.45f).normalized);
            if (d < r) p.transform.position = center + radial * r;
        }
    }

    void OnDestroy()
    {
        if (glass != null) Destroy(glass);
        if (lineMat != null) Destroy(lineMat);
    }
}

// a simple target that fires slow balls at the player, to test parries and the dome (spawn from Tools > Magic)
public class ProjectileDummy : MonoBehaviour
{
    public float interval = 2.2f;
    public float speed = 16f;
    public float damage = 6f;
    float next = 1f;
    Transform target;

    void Update()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) target = go.transform;
            return;
        }
        if (Time.time < next) return;
        next = Time.time + interval;
        Vector3 from = transform.position + Vector3.up * 1.5f;
        Vector3 aim = target.position + Vector3.up * 1.1f;
        BossProjectile.Spawn(from, aim - from, speed, damage, new Color(1f, 0.55f, 0.3f), 0.8f, 6f);
    }
}
