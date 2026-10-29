using UnityEngine;

// Drives the witch's wand and upper-body animation layers: ready pose, tap flick, chanting pose, release thrust and the flinch when hit.
public class PlayerCastAnimator : MonoBehaviour
{
    static readonly int CastHold = Animator.StringToHash("CastHold");
    static readonly int CastTap = Animator.StringToHash("CastTap");
    static readonly int CastRelease = Animator.StringToHash("CastRelease");
    static readonly int Hit = Animator.StringToHash("Hit");

    Animator animator;
    PlayerSpellCaster caster;
    Health health;
    bool chanting;

    void Awake()
    {
        animator = GetComponent<Animator>();
        caster = GetComponentInChildren<PlayerSpellCaster>();
        if (caster != null) health = caster.GetComponentInParent<Health>();
    }

    void OnEnable()
    {
        if (caster != null) caster.Casted += OnCasted;
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (caster != null) caster.Casted -= OnCasted;
        if (health != null) health.Damaged -= OnDamaged;
    }

    void Update()
    {
        if (animator == null || caster == null) return;

        bool now = caster.IsChanting;
        if (now == chanting) return;
        chanting = now;

        animator.SetBool(CastHold, now);
        if (now) { animator.ResetTrigger(CastRelease); animator.ResetTrigger(CastTap); }
    }

    void OnCasted(bool wasHold)
    {
        if (animator == null) return;
        if (wasHold) animator.SetTrigger(CastRelease);
        else if (!chanting) animator.SetTrigger(CastTap);
    }

    void OnDamaged(float amount)
    {
        if (animator != null && amount > 0f) animator.SetTrigger(Hit);
    }
}
