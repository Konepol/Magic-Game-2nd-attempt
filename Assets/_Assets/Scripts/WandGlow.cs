using UnityEngine;

// Makes the gem of the wand glow: softly while idle, brighter and pulsing while the player is chanting or holding a combined spell ready.
public class WandGlow : MonoBehaviour
{
    public int gemIndex = 1;
    public float idleGlow = 0.9f, chantGlow = 3.2f;

    PlayerSpellCaster caster;
    MeshRenderer rend;
    MaterialPropertyBlock block;
    float level;

    void Awake()
    {
        rend = GetComponent<MeshRenderer>();
        caster = GetComponentInParent<PlayerSpellCaster>();
        if (caster == null) caster = transform.root.GetComponentInChildren<PlayerSpellCaster>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (rend == null) return;

        bool chanting = caster != null && (caster.IsChanting || caster.Queueing);
        bool held = caster != null && caster.HeldCount > 0;
        float target = chanting ? chantGlow : (held ? chantGlow * 0.75f : (caster != null && caster.HasPending ? chantGlow * 0.6f : idleGlow));
        level = Mathf.MoveTowards(level <= 0f ? idleGlow : level, target, Time.deltaTime * 6f);

        float pulse = chanting ? 0.25f * Mathf.Sin(Time.time * 14f) : (held ? 0.35f * Mathf.Sin(Time.time * 4.5f) : 0.1f * Mathf.Sin(Time.time * 3f));
        rend.GetPropertyBlock(block, gemIndex);
        block.SetFloat("_EmissionStrength", Mathf.Max(0f, level + pulse));
        rend.SetPropertyBlock(block, gemIndex);
    }
}
