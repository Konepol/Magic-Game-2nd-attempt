using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Held cast: the starter wand's unique ability. Pause a chant partway (Tab while the key is held) and the spell stays ready on the wand;
// press its key later to release it. The mana is paid when it is paused. Pouring more output in before the release makes it come out stronger
// (the difference is paid then), and a held spell can be fused (queue it with B), or a ready fusion can be held without its 15 s limit.
//
// How many spells the wand can hold is a value kept in the save, so quests can raise it later: HeldCast.RaiseBank().
public static class HeldCast
{
    const string BankKey = "heldBank";
    const string WandKey = "starterWand";

    // the starter wand is the only wand with held cast; a later wand system turns this off for the other wands
    public static bool Enabled
    {
        get => PlayerSave.GetInt(WandKey, 1) == 1;
        set { PlayerSave.SetInt(WandKey, value ? 1 : 0); PlayerSave.Save(); }
    }

    // how many spells (or one ready fusion each) can be held at once; starts at one
    public static int BankSize
    {
        get => Mathf.Clamp(PlayerSave.GetInt(BankKey, 1), 0, 8);
        set { PlayerSave.SetInt(BankKey, Mathf.Clamp(value, 0, 8)); PlayerSave.Save(); }
    }

    public static void RaiseBank(int by = 1) => BankSize = BankSize + by;
}

// one spell kept ready on the wand
public class HeldSpell
{
    public int slot;
    public SpellData spell;
    public SpellVariant variant;      // the form the chant had reached when it was paused
    public int tier;
    public float outputAtHold;        // the output level it was paid at
    public float prepaid;             // mana already paid
    public Color color;
    public string name;
}

// the glowing orbs that circle the wand tip, one per held spell (the state is shown on the witch, not only in the HUD)
public class HeldOrbs : MonoBehaviour
{
    class Orb { public Transform t; public Material mat; public Light light; }
    readonly List<Orb> orbs = new List<Orb>();
    PlayerSpellCaster caster;

    public static HeldOrbs Create(PlayerSpellCaster caster)
    {
        var go = new GameObject("HeldOrbs");
        var h = go.AddComponent<HeldOrbs>();
        h.caster = caster;
        return h;
    }

    Orb Make(Color color)
    {
        var o = new Orb();
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "HeldOrb";
        g.transform.SetParent(transform, false);
        Destroy(g.GetComponent<Collider>());
        o.t = g.transform;
        o.mat = SpellUtil.Glow(new Color(color.r, color.g, color.b, 1f));
        g.GetComponent<Renderer>().sharedMaterial = o.mat;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.type = LightType.Point; l.range = 3f; l.intensity = 1.2f; l.color = color; l.shadows = LightShadows.None;
        o.light = l;
        return o;
    }

    void LateUpdate()
    {
        if (caster == null) { Destroy(gameObject); return; }
        var held = caster.HeldSpells;
        int count = held.Count + (caster.PendingHeld ? 1 : 0);

        while (orbs.Count < count) orbs.Add(Make(Color.white));
        while (orbs.Count > count) { Destroy(orbs[orbs.Count - 1].t.gameObject); Destroy(orbs[orbs.Count - 1].mat); orbs.RemoveAt(orbs.Count - 1); }

        Vector3 center = caster.LaunchPoint;
        for (int i = 0; i < orbs.Count; i++)
        {
            Color c = i < held.Count ? held[i].color : caster.PendingColor;
            var m = orbs[i].mat; c.a = 1f; SpellVisuals.SetColor(m, c);
            orbs[i].light.color = c;
            float angle = Time.time * 2.6f + i * Mathf.PI * 2f / Mathf.Max(1, orbs.Count);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 6f + i);
            orbs[i].t.position = center + new Vector3(Mathf.Cos(angle), 0.15f * Mathf.Sin(angle * 2f), Mathf.Sin(angle)) * 0.32f;
            orbs[i].t.localScale = Vector3.one * 0.16f * pulse;
        }
    }

    void OnDestroy() { foreach (var o in orbs) if (o.mat != null) Destroy(o.mat); }
}

// the HUD box for held spells: what is held, how many slots the wand has, and the keys
public class HeldCastView
{
    readonly PlayerSpellCaster caster;
    readonly RectTransform box;
    readonly Text title, list;

    public HeldCastView(Transform parent, PlayerSpellCaster caster)
    {
        this.caster = caster;
        var bg = UIKit.Box(parent, "HeldSpells", new Color(0.06f, 0.07f, 0.12f, 0.92f));
        box = bg.rectTransform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.anchoredPosition = new Vector2(0f, 350f);
        box.sizeDelta = new Vector2(470f, 104f);

        title = UIKit.Label(box, "Title", "", 24, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter, true);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        title.rectTransform.sizeDelta = new Vector2(0f, 32f);

        list = UIKit.Label(box, "List", "", 22, Color.white, TextAnchor.MiddleCenter);
        list.supportRichText = true;
        list.rectTransform.anchorMin = new Vector2(0f, 0f);
        list.rectTransform.anchorMax = new Vector2(1f, 1f);
        list.rectTransform.offsetMin = new Vector2(8f, 6f);
        list.rectTransform.offsetMax = new Vector2(-8f, -38f);
        box.gameObject.SetActive(false);
    }

    public void Update()
    {
        bool show = caster != null && HeldCast.Enabled && (caster.HeldSpells.Count > 0 || caster.PendingHeld);
        if (box.gameObject.activeSelf != show) box.gameObject.SetActive(show);
        if (!show) return;

        int used = caster.HeldSpells.Count + (caster.PendingHeld ? 1 : 0);
        title.text = "HELD  " + used + " / " + HeldCast.BankSize;

        var sb = new System.Text.StringBuilder();
        foreach (var h in caster.HeldSpells)
        {
            string hex = ColorUtility.ToHtmlStringRGB(h.color);
            sb.Append("<color=#").Append(hex).Append(">").Append(h.name).Append("</color>  [").Append(caster.GetKeyLabel(h.slot)).Append("]  x")
              .Append(h.outputAtHold.ToString("0.#")).Append('\n');
        }
        if (caster.PendingHeld) sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(caster.PendingColor)).Append(">").Append(caster.PendingName).Append("</color>  [B]\n");
        sb.Append("<color=#808080>spell key releases   Tab drops</color>");
        list.text = sb.ToString();
    }
}
