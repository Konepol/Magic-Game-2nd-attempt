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
        public bool hasMore;                  // more steps above, not drawn
        public string result;                 // the fusion this chain finishes (null if it goes on)
        public readonly List<Node> children = new List<Node>();
        public float x;                       // 0..1 across the area
        public int depth;
    }

    const float AreaW = 640f, AreaH = 470f;
    const float NodeH = 68f, LayerStep = 110f, FirstY = 34f;

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
        area.sizeDelta = new Vector2(AreaW, AreaH);
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
        Prune(root);

        if (root.children.Count == 0 && root.result == null) { area.gameObject.SetActive(false); return; }

        // layout: leaves are spread across the width, a parent sits over its children
        int leaves = 0;
        int maxPerLayer = 1;
        var perLayer = new Dictionary<int, int>();
        Place(root, ref leaves, perLayer);
        foreach (var kv in perLayer) maxPerLayer = Mathf.Max(maxPerLayer, kv.Value);
        if (leaves < 1) leaves = 1;
        Spread(root, leaves);

        float nodeW = Mathf.Clamp(AreaW / Mathf.Max(maxPerLayer, 1) - 12f, 110f, 200f);

        // edges first so the boxes sit on top of them
        DrawEdges(root, nodeW);
        DrawNode(root, held, nodeW, true);

        area.gameObject.SetActive(true);
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
        if (node.depth >= 4) return;
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

    // only three rows above the held spell are drawn; a node with more beyond them gets an arrow pointing up instead
    const int MaxRows = 3;

    static void Prune(Node node)
    {
        if (node.depth >= MaxRows) { node.hasMore = node.children.Count > 0; node.children.Clear(); return; }
        foreach (var c in node.children) Prune(c);
    }

    static void Place(Node node, ref int leaf, Dictionary<int, int> perLayer)
    {
        perLayer[node.depth] = perLayer.TryGetValue(node.depth, out int n) ? n + 1 : 1;
        if (node.children.Count == 0) { node.x = leaf++; return; }

        foreach (var c in node.children) Place(c, ref leaf, perLayer);
        float sum = 0f;
        foreach (var c in node.children) sum += c.x;
        node.x = sum / node.children.Count;
    }

    static void Spread(Node node, int leaves)
    {
        node.x = (node.x + 0.5f) / leaves;
        foreach (var c in node.children) Spread(c, leaves);
    }

    Vector2 PositionOf(Node n) => new Vector2(Mathf.Lerp(40f, AreaW - 40f, n.x), FirstY + n.depth * LayerStep + NodeH * 0.5f);

    void DrawEdges(Node node, float nodeW)
    {
        foreach (var c in node.children)
        {
            Vector2 from = PositionOf(node) + new Vector2(0f, NodeH * 0.5f);
            Vector2 to = PositionOf(c) - new Vector2(0f, NodeH * 0.5f);
            Line(from, to);
            DrawEdges(c, nodeW);
        }
    }

    void Line(Vector2 a, Vector2 b)
    {
        var color = new Color(1f, 0.82f, 0.35f, 0.75f);
        Vector2 d = b - a;

        var line = UIKit.Box(content, "Edge", color);
        var rt = line.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(d.magnitude, 3f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

        // arrowhead just below the box it points at
        var head = UIKit.Box(content, "Arrow", color);
        var hr = head.rectTransform;
        hr.anchorMin = hr.anchorMax = Vector2.zero;
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = b - d.normalized * 6f;
        hr.sizeDelta = new Vector2(13f, 13f);
        hr.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void DrawNode(Node node, List<SpellData> held, float nodeW, bool isRoot)
    {
        Vector2 p = PositionOf(node);
        float width = isRoot ? Mathf.Min(nodeW * 1.6f, 300f) : nodeW;

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
            int slot = -1;
            for (int i = 0; i < caster.SlotCount; i++) if (caster.GetSpellAt(i) == node.spell) slot = i;
            string add = slot >= 0
                ? "<color=#9FD8FF>+ [" + caster.GetKeyLabel(slot) + "]</color> " + node.spell.displayName
                : "<color=#808080>+ " + node.spell.displayName + " (not equipped)</color>";
            text = title + "\n" + add;
            fill = slot >= 0 ? new Color(0.12f, 0.17f, 0.26f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.95f);
        }

        var box = UIKit.Box(content, "Node", fill);
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        rt.sizeDelta = new Vector2(width, NodeH);

        var label = UIKit.Label(box.transform, "Text", text, 18, textColor, TextAnchor.MiddleCenter, true);
        label.supportRichText = true;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 11;
        label.resizeTextMaxSize = 17;
        UIKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(5f, 3f);
        label.rectTransform.offsetMax = new Vector2(-5f, -3f);

        if (node.hasMore)
        {
            Vector2 top = p + new Vector2(0f, NodeH * 0.5f);
            Line(top, top + new Vector2(0f, 26f));
        }

        foreach (var c in node.children) DrawNode(c, held, nodeW, false);
    }
}
