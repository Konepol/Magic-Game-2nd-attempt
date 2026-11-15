using UnityEngine;

// The visible mana aura around the witch (simple first version: two pulsing rings). Bigger and brighter with the output level,
// orange past the safe level, red past the lowest output limit of the equipped spells. Suppressing mana shrinks and dims it.
public class ManaAura : MonoBehaviour
{
    PlayerSpellCaster caster;
    LineRenderer low, high;
    Material mat;
    float smoothed = 1f;

    public static ManaAura Create(PlayerSpellCaster caster)
    {
        var go = new GameObject("ManaAura");
        go.transform.SetParent(caster.transform.root, false);
        var a = go.AddComponent<ManaAura>();
        a.caster = caster;
        a.mat = SpellVisuals.Make(Color.white, true);
        a.low = SpellVisuals.CreateRing(go.transform, a.mat, 0.07f);
        a.high = SpellVisuals.CreateRing(go.transform, a.mat, 0.05f);
        return a;
    }

    void LateUpdate()
    {
        if (caster == null) { Destroy(gameObject); return; }

        float eff = caster.EffectiveOutput;
        smoothed = Mathf.Lerp(smoothed, eff, 1f - Mathf.Exp(-8f * Time.deltaTime));

        Color c;
        if (eff > caster.LowestLimit) c = new Color(1f, 0.25f, 0.2f);
        else if (eff > caster.SafeOutput) c = new Color(1f, 0.65f, 0.25f);
        else c = Color.Lerp(new Color(0.55f, 0.8f, 1f), new Color(0.9f, 0.95f, 1f), Mathf.InverseLerp(0.5f, 1.5f, eff));

        float radius = 0.6f + 0.45f * smoothed;
        float alpha = Mathf.Clamp(0.2f + 0.25f * smoothed, 0.2f, 0.9f);
        if (caster.Suppressed) { radius *= 0.35f; alpha *= 0.25f; }

        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * (3f + smoothed * 2f));
        c.a = alpha;
        SpellVisuals.SetColor(mat, c);

        Vector3 p = caster.transform.root.position;
        SpellVisuals.SetRing(low, p + Vector3.up * 0.08f, radius * pulse);
        SpellVisuals.SetRing(high, p + Vector3.up * (0.9f + 0.2f * Mathf.Sin(Time.time * 1.7f)), radius * 0.8f * pulse);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
