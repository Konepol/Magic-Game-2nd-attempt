using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The fusion paths diagram in the bottom-left of the screen. While spells are held, the held group sits at the bottom, and above it the
// spells that can be added next branch upward, chain by chain, toward the fusion each chain leads to. The area clips whatever does not fit,
// so a long chain simply runs off its top edge. It can be switched off in Options > Gameplay.
public class FusionPathsView
{
    class Node
    {
        public SpellData spell;               // the spell added on the way here (null for the root)
        public List<SpellData> set;           // everything combined at this node
        public Vector2 pos;
        public string result;                 // the fusion this chain finishes (null if it goes on)
        public readonly List<Node> children = new List<Node>();
        public float x;                       // 0..1 across the area
        public int depth;
    }

    const float NodeW = 230f, NodeH = 72f, Pad = 24f, TitleH = 30f;

    readonly PlayerSpellCaster caster;
    readonly RectTransform area, content;
    string key = "";

    public FusionPathsView(Transform parent, PlayerSpellCaster caster)
    {
        this.caster = caster;

        var box = UIKit.Box(parent, "FusionPaths", new Color(0.04f, 0.06f, 0.1f, 0.72f));
        area = box.rectTransform;
        area.anchorMin = area.anchorMax = Vector2.zero;
        area.pivot = Vector2.zero;
        area.anchoredPosition = new Vector2(20f, 190f);        // above the combined-spell box in the corner
        area.sizeDelta = new Vector2(300f, 200f);
        area.gameObject.AddComponent<RectMask2D>();            // anything past the edge is cut

        var title = UIKit.Label(area, "Title", "FUSION PATHS", 20, new Color(0.6f, 0.65f, 0.75f), TextAnchor.UpperLeft, true);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(14f, -8f);
        title.rectTransform.sizeDelta = new Vector2(300f, 28f);

        content = UIKit.Child(area, "Content");
        UIKit.Stretch(content);

        area.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (caster == null) return;

        var held = new List<SpellData>();
        var sig = new System.Text.StringBuilder();
        bool on = PlayerSettings.FusionPaths;
        if (on)
        {
            for (int i = 0; i < caster.SlotCount; i++)
            {
                var sp = caster.GetSpellAt(i);
                sig.Append('|').Append(sp != null ? sp.name : "-");
                if (sp != null && caster.IsHeld(i)) { held.Add(sp); sig.Append('*'); }
            }
        }

        if (!on || held.Count == 0)
        {
            if (area.gameObject.activeSelf) area.gameObject.SetActive(false);
            key = "";
            return;
        }

        string k = sig.ToString();
        if (k == key) return;
        key = k;
        Rebuild(held);
    }

    // ---------- building the diagram ----------

    void Rebuild(List<SpellData> held)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Object.Destroy(content.GetChild(i).gameObject);

        // each step adds one more spell to the group: the node moves up to that spell and takes the name of what the group has become
        var root = new Node { set = new List<SpellData>(held) };
        root.result = ResultOf(root.set);
        Grow(root);
        root.children.RemoveAll(c => c.result == null || SlotOf(c.spell) < 0);      // only real fusions you can make with the spells you carry

        if (root.children.Count == 0) { area.gameObject.SetActive(false); return; }

        // the group sits in the middle and every spell that can join it is placed on a ring around it
        int n = root.children.Count;
        float rootW = Mathf.Clamp(60f + 14f * RootTitle(root).Length, 200f, 420f);
        float chord = n > 1 ? (NodeW + 20f) / (2f * Mathf.Sin(Mathf.PI / n)) : 0f;
        float rx = n == 0 ? 0f : Mathf.Max(chord, rootW * 0.5f + NodeW * 0.5f + 20f);
        float ry = n == 0 ? 0f : Mathf.Max(chord * 0.8f, NodeH + 24f);
        float w = Mathf.Max(rootW, 2f * rx + NodeW) + 2f * Pad;
        float h = 2f * ry + NodeH + 2f * Pad + TitleH;
        area.sizeDelta = new Vector2(w, h);

        Vector2 centre = new Vector2(w * 0.5f, (h - TitleH) * 0.5f);
        root.pos = centre;
        for (int i = 0; i < n; i++)
        {
            float angle = Mathf.PI * 0.5f - i * 2f * Mathf.PI / n;        // first one at the top, then clockwise
            root.children[i].pos = centre + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        DrawNode(root, held, rootW, true);
        area.gameObject.SetActive(true);
    }

    int SlotOf(SpellData spell)
    {
        for (int i = 0; i < caster.SlotCount; i++) if (caster.GetSpellAt(i) == spell) return i;
        return -1;
    }

    string RootTitle(Node node)
    {
        if (node.result != null) return node.result;
        var names = new List<string>();
        foreach (var h in node.set) names.Add(h.displayName);
        return string.Join(" + ", names);
    }

    // the fusion whose recipe is exactly this group, if any
    string ResultOf(List<SpellData> set)
    {
        foreach (var combo in caster.Combos)
        {
            if (combo == null || combo.result == null || combo.ingredients == null) continue;
            var ing = new List<SpellData>();
            foreach (var i in combo.ingredients) if (i != null && !ing.Contains(i)) ing.Add(i);
            if (ing.Count != set.Count) continue;
            bool same = true;
            foreach (var x in set) if (!ing.Contains(x)) same = false;
            if (same) return combo.result.displayName;
        }
        return null;
    }

    // children: every spell that still appears in a recipe containing the whole group (in a fixed order so each group shows once)
    void Grow(Node node)
    {
        if (node.depth >= 1) return;
        var options = new List<SpellData>();
        foreach (var combo in caster.Combos)
        {
            if (combo == null || combo.result == null || combo.ingredients == null) continue;
            bool all = true;
            foreach (var h in node.set) if (System.Array.IndexOf(combo.ingredients, h) < 0) all = false;
            if (!all) continue;
            foreach (var i in combo.ingredients) if (i != null && !node.set.Contains(i) && !options.Contains(i)) options.Add(i);
        }
        options.Sort((x, y) => string.CompareOrdinal(x.displayName, y.displayName));

        for (int i = 0; i < options.Count; i++)
        {
            if (node.spell != null && string.CompareOrdinal(options[i].displayName, node.spell.displayName) <= 0) continue;
            var set = new List<SpellData>(node.set) { options[i] };
            var child = new Node { spell = options[i], set = set, depth = node.depth + 1 };
            child.result = ResultOf(set);
            node.children.Add(child);
            Grow(child);
        }
    }

    Vector2 PositionOf(Node n) => n.pos;

    void DrawNode(Node node, List<SpellData> held, float nodeW, bool isRoot)
    {
        Vector2 p = PositionOf(node);
        float width = nodeW;

        string text;
        Color fill, textColor = Color.white;
        var names = new List<string>();
        foreach (var h in node.set) names.Add(h.displayName);
        string title = node.result != null ? "<b><color=#FFD15A>" + node.result + "</color></b>" : string.Join(" + ", names);
        if (isRoot)
        {
            text = title + (node.result != null ? "\n<color=#9a9a9a>ready</color>" : "");
            fill = new Color(0.32f, 0.26f, 0.1f, 0.95f);
        }
        else
        {
            int slot = SlotOf(node.spell);
            string add = slot >= 0
                ? "<color=#9FD8FF>+ [" + caster.GetKeyLabel(slot) + "]</color> " + node.spell.displayName
                : "<color=#808080>+ " + node.spell.displayName + " (no key)</color>";
            text = title + "\n" + add;
            fill = slot >= 0 ? new Color(0.12f, 0.17f, 0.26f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.95f);
        }

        var box = UIKit.Box(content, "Node", fill);
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        var bob = box.gameObject.AddComponent<FloatAround>();
        bob.home = p;
        bob.phase = p.x * 0.013f + p.y * 0.021f;
        rt.sizeDelta = new Vector2(width, NodeH);

        var label = UIKit.Label(box.transform, "Text", text, 18, textColor, TextAnchor.MiddleCenter, true);
        label.supportRichText = true;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 11;
        label.resizeTextMaxSize = 17;
        UIKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(5f, 3f);
        label.rectTransform.offsetMax = new Vector2(-5f, -3f);

        foreach (var c in node.children) DrawNode(c, held, NodeW, false);
    }
}

// Lets a node drift a little around its place, like it is hovering.
public class FloatAround : MonoBehaviour
{
    public Vector2 home;
    public float phase;

    void Update()
    {
        float t = Time.unscaledTime * 1.6f + phase;
        ((RectTransform)transform).anchoredPosition = home + new Vector2(Mathf.Sin(t * 0.8f), Mathf.Sin(t)) * 5f;
    }
}
