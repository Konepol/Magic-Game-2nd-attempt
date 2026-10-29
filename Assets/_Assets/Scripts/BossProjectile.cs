using UnityEngine;

// A slow ball the boss throws. The player's barrier stops it (and pays for it with mana); a pillar or wall stops it; the player takes the hit.
public class BossProjectile : MonoBehaviour
{
    static Material sharedMat;

    Vector3 velocity;
    float damage, radius, life;
    int mask;

    public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage, Color color, float size = 0.9f, float life = 7f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BossProjectile";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * size;

        if (sharedMat == null) sharedMat = SpellVisuals.Make(Color.white, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = sharedMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        r.SetPropertyBlock(block);

        var p = go.AddComponent<BossProjectile>();
        p.velocity = direction.normalized * speed;
        p.damage = damage;
        p.radius = size * 0.5f;
        p.life = life;
        p.mask = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << 2));     // everything except the enemies themselves and Ignore Raycast
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector3 step = velocity * Time.deltaTime;
        float distance = step.magnitude;
        if (distance > 0f && Physics.SphereCast(transform.position, radius, step / distance, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
        {
            var barrier = hit.collider.GetComponentInParent<Barrier>();
            if (barrier != null)
            {
                if (barrier.Blocks("Player")) { barrier.Hit(damage, false, hit.point); Burst(hit.point); return; }
            }
            else if (hit.collider.CompareTag("Player"))
            {
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null) health.TakeDamage(damage, hit.point);
                Burst(hit.point);
                return;
            }
            else
            {
                Burst(hit.point);       // a pillar, a wall or the floor
                return;
            }
        }

        transform.position += step;
    }

    void Burst(Vector3 point)
    {
        SpellFX.Sparks(point, new Color(1f, 0.6f, 0.3f));
        Destroy(gameObject);
    }
}
