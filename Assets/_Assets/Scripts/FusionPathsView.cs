using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The fusion paths diagram. The spells being held (or, while combining, the spells queued) sit in the middle, and on a ring around them
// every fusion that still includes them, with the spells it needs. Held spells show it small in the bottom-left; while queueing a combination
// (B) it moves to the middle of the screen and grows. It can be switched off in Options > Gameplay.
public class FusionPathsView
{
    class Node
    {
        public List<SpellData> set;           // the spells in the middle (root only)
        public string result;                 // the fusion a ring node makes
        public List<SpellData> missing;       // what a ring node still needs
        public Vector2 pos;
    }

    const float NodeW = 210f, NodeH = 62f, Pad = 18f, TitleH = 26f;
    const float QueueScale = 1.6f;

    readonly PlayerSpellCaster caster;
    readonly RectTransform area, content;
    readonly Text title;
    string key = "";

    public FusionPathsView(Transform parent, PlayerSpellCaster caster)
    {
        this.caster = caster;

        var box = UIKit.Box(parent, "FusionPaths", new Color(0.04f, 0.06f, 0.1f, 0.78f));
        area = box.rectTransform;
        area.sizeDelta = new Vector2(300f, 200f);
        area.gameObject.AddComponent<RectMask2D>();

        title = UIKit.Label(area, "Title", "FUSION PATHS", 16, new Color(0.6f, 0.65f, 0.75f), TextAnchor.UpperLeft, true);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(14f, -8f);
        title.rectTransform.sizeDelta = new Vector2(400f, 28f);

        content = UIKit.Child(area, "Content");
        UIKit.Stretch(content);

        area.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (caster == null) return;

        bool queue = caster.Queueing;
        var held = new List<SpellData>();
        var sig = new System.Text.StringBuilder(queue ? "Q" : "H");

        if (PlayerSettings.FusionPaths)
        {
            if (queue)
            {
                foreach (int slot in caster.QueuedSlots) { var sp = caster.GetSpellAt(slot); if (sp != null) held.Add(sp); }
            }
            else if (!caster.ComboActive)
            {
                for (int i = 0; i < caster.SlotCount; i++)
                {
                    var sp = caster.GetSpellAt(i);
                    if (sp != null && caster.IsHeld(i)) held.Add(sp);
                }
            }

            for (int i = 0; i < caster.SlotCount; i++)
            {
                var sp = caster.GetSpellAt(i);
                sig.Append('|').Append(sp != null ? sp.name : "-");
            }
            foreach (var hs in held) sig.Append((char)42).Append(hs.name);
        }

        if (!PlayerSettings.FusionPaths || (!queue && held.Count == 0))
        {
            if (area.gameObject.activeSelf) area.gameObject.SetActive(false);
            key = "";
            return;
        }

        string k = sig.ToString();
        if (k == key) return;
        key = k;
        Rebuild(held, queue);
    }

    // ---------- building the diagram ----------

    void Rebuild(List<SpellData> held, bool queue)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Object.Destroy(content.GetChild(i).gameObject);

        var root = new Node { set = held };
        var ring = new List<Node>();

        foreach (var combo in caster.Combos)
        {
            if (combo == null || combo.result == null || combo.ingredients == null) continue;

            var ingredients = new List<SpellData>();
            foreach (var ing in combo.ingredients) if (ing != null && !ingredients.Contains(ing)) ingredients.Add(ing);

            bool includesAll = true;
            foreach (var heldSpell in held) if (!ingredients.Contains(heldSpell)) includesAll = false;
            if (!includesAll) continue;

            var missing = new List<SpellData>();
            bool attainable = true;
            foreach (var ing in ingredients)
            {
                if (held.Contains(ing)) continue;
                if (SlotOf(ing) < 0) attainable = false;
                missing.Add(ing);
            }
            if (!attainable) continue;

            if (missing.Count == 0) { root.result = combo.result.displayName; continue; }
            ring.Add(new Node { result = combo.result.displayName, missing = missing });
        }

        ring.Sort((a, b) => a.missing.Count != b.missing.Count ? a.missing.Count.CompareTo(b.missing.Count) : string.CompareOrdinal(a.result, b.result));

        // held spells: nothing to show without a fusion ahead; queueing always shows the middle
        if (ring.Count == 0 && !queue) { area.gameObject.SetActive(false); return; }

        int n = ring.Count;
        string rootText = RootText(root, queue);
        float rootW = Mathf.Clamp(50f + 9f * Longest(rootText), 170f, 360f);

        // the group sits in the middle, the fusions on an ellipse around it, spaced so nothing overlaps
        float chord = n > 1 ? (NodeW + 20f) / (2f * Mathf.Sin(Mathf.PI / n)) : 0f;
        float rx = n == 0 ? 0f : Mathf.Max(chord, n > 2 ? rootW * 0.5f + NodeW * 0.5f + 20f : 0f);
        float ry = n == 0 ? 0f : Mathf.Max(chord * 0.8f, NodeH + 24f);
        float w = Mathf.Max(rootW, 2f * rx + NodeW) + 2f * Pad;
        float h = 2f * ry + NodeH + 2f * Pad + TitleH;
        area.sizeDelta = new Vector2(w, h);

        // small in the corner while holding; big in the middle of the screen while queueing a combination
        area.anchorMin = area.anchorMax = queue ? new Vector2(0.5f, 0.5f) : Vector2.zero;
        area.pivot = queue ? new Vector2(0.5f, 0.5f) : Vector2.zero;
        area.anchoredPosition = queue ? new Vector2(0f, 60f) : new Vector2(20f, 20f);
        area.localScale = Vector3.one * (queue ? QueueScale : 1f);
        title.text = queue ? "COMBINE" : "FUSION PATHS";

        Vector2 centre = new Vector2(w * 0.5f, (h - TitleH) * 0.5f);
        root.pos = centre;
        for (int i = 0; i < n; i++)
        {
            float angle = Mathf.PI * 0.5f - i * 2f * Mathf.PI / n;        // first one at the top, then clockwise
            ring[i].pos = centre + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        DrawNode(root.pos, rootW, NodeH, rootText, new Color(0.32f, 0.26f, 0.1f, 0.95f), Color.white);
        foreach (var node in ring)
        {
            var parts = new List<string>();
            foreach (var m in node.missing)
                parts.Add("<color=#9FD8FF>[" + caster.GetKeyLabel(SlotOf(m)) + "]</color> " + m.displayName);
            DrawNode(node.pos, NodeW, NodeH, "<b><color=#FFD15A>" + node.result + "</color></b>\n" + string.Join("  ", parts),
                new Color(0.12f, 0.17f, 0.26f, 0.95f), Color.white);
        }

        area.gameObject.SetActive(true);
    }

    string RootText(Node root, bool queue)
    {
        if (root.set.Count == 0) return "Press the spells to combine,\nthen B to cast";

        var names = new List<string>();
        foreach (var s in root.set) names.Add(s.displayName);
        if (queue && root.result != null) return "<b><color=#FFD15A>" + root.result + "</color></b>\n<color=#9a9a9a>press B to cast</color>";
        return string.Join(" + ", names);
    }

    static int Longest(string text)
    {
        int best = 0;
        foreach (var line in System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Split('\n')) best = Mathf.Max(best, line.Length);
        return best;
    }

    int SlotOf(SpellData spell)
    {
        for (int i = 0; i < caster.SlotCount; i++) if (caster.GetSpellAt(i) == spell) return i;
        return -1;
    }

    void DrawNode(Vector2 p, float width, float height, string text, Color fill, Color textColor)
    {
        var box = UIKit.Box(content, "Node", fill);
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        rt.sizeDelta = new Vector2(width, height);
        var bob = box.gameObject.AddComponent<FloatAround>();
        bob.home = p;
        bob.phase = p.x * 0.013f + p.y * 0.021f;

        var label = UIKit.Label(box.transform, "Text", text, 18, textColor, TextAnchor.MiddleCenter, true);
        label.supportRichText = true;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 9;
        label.resizeTextMaxSize = 15;
        UIKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(5f, 3f);
        label.rectTransform.offsetMax = new Vector2(-5f, -3f);
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
