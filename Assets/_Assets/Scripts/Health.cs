using System;
using UnityEngine;
using UnityEngine.Events;

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint);
}

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float invulnerableTime = 0f;     // player: ~0.3 to avoid damage stacking in one frame
    [SerializeField] bool destroyOnDeath = true;      // enemies: true, player: false
    [SerializeField] float destroyDelay = 0.5f;
    [SerializeField] UnityEvent onDeath;              // hook animations, game over, disabling the controller...

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead { get; private set; }

    public event Action<float, float> Changed;        // current, max
    public event Action<float> Damaged;               // amount
    public event Action Died;

    // fires for every Health in the scene (health, amount, hit point), used by floating damage numbers
    public static event Action<Health, float, Vector3> AnyDamaged;

    float invulnerableUntil;

    void Awake() => Current = maxHealth;

    void Start() => Changed?.Invoke(Current, maxHealth);   // so the bars show the initial value

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (IsDead || amount <= 0f || Time.time < invulnerableUntil) return;

        invulnerableUntil = Time.time + invulnerableTime;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(amount);
        AnyDamaged?.Invoke(this, amount, hitPoint);
        Changed?.Invoke(Current, maxHealth);

        if (Current <= 0f) Die();
    }

    // lets code that spawns a Health at runtime (barriers) set it up
    public void Configure(float max, bool destroyWhenDead, float delay)
    {
        maxHealth = max;
        destroyOnDeath = destroyWhenDead;
        destroyDelay = delay;
        Current = max;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }

    void Die()
    {
        IsDead = true;
        onDeath?.Invoke();
        Died?.Invoke();
        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }
}