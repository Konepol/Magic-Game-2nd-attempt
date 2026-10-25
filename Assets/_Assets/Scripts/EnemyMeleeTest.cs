using UnityEngine;

public class EnemyMeleeTest : MonoBehaviour
{
    [SerializeField] float range = 2f;
    [SerializeField] float damage = 10f;
    [SerializeField] float interval = 1f;
    float next;
    Health player;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.GetComponent<Health>();
    }

    void Update()
    {
        if (player == null || player.IsDead || Time.time < next) return;
        if ((player.transform.position - transform.position).sqrMagnitude > range * range) return;

        next = Time.time + interval;
        player.TakeDamage(damage, player.transform.position);
    }
}