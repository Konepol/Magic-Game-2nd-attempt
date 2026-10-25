using UnityEngine;

public class EnemyMeleeZone : MonoBehaviour
{
    [SerializeField] string targetTag = "Player";
    [SerializeField] float damage = 10f;
    [SerializeField] float interval = 1f;
    [SerializeField] bool debugLogs = true;

    float nextHitTime;

    void OnTriggerStay(Collider other)
    {
        // cheapest checks first: runs every physics step while the player is inside
        if (Time.time < nextHitTime) return;
        if (!other.CompareTag(targetTag)) return;

        var health = other.GetComponentInParent<Health>();
        if (health == null)
        {
            if (debugLogs) Debug.LogWarning("[Melee] '" + other.name + "' has the tag but no Health", this);
            nextHitTime = Time.time + interval;   // avoid spamming the warning
            return;
        }
        if (health.IsDead) return;

        nextHitTime = Time.time + interval;
        if (debugLogs) Debug.Log("[Melee] Hit " + other.name + " for " + damage, this);
        health.TakeDamage(damage, other.transform.position);
    }
}