using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The casting chant: while a spell is being cast, its sentences appear one by one above the player's head. Each new sentence is a row that
// types itself out under the older ones, and the whole stack drifts slowly upward. When the spell is released the rows rise and fade;
// when the cast fails they flicker red and break up. Several spells cast at once each get their own stack, side by side.
public class ChantDisplay : MonoBehaviour
{
    const float LineHeight = 62f;          // canvas pixels between rows
    const float Drift = 20f;               // slow upward drift of the whole stack, pixels per second
    const float Scale = 0.0031f;           // metres per canvas pixel
    const float HeadHeight = 2.7f;         // above the player's feet
    const float StackSpacing = 2.4f;       // distance between stacks when several spells are cast at once

    enum End { Running, Finish, Fail, Drop }

    class Row
    {
        public Text text;
        public RectTransform rect;
        public string full;
        public float born, y;
        public float fade = 1f;          // how visible it is now: the newest row is clear, the one before it half faded, the one before that gone
    }

    class Chant
    {
        public int key;
        public GameObject root;
        public Transform transform;
        public readonly List<Row> rows = new List<Row>();
        public Color color;
        public float start, endTime;
        public int lineIndex;            // how many sentences have been started (rows that faded out are removed, so the row count cannot be used)
        public End end = End.Running;
    }

    static ChantDisplay instance;
    static ChantDisplay Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("ChantDisplay").AddComponent<ChantDisplay>();
            return instance;
        }
    }

    readonly Dictionary<int, Chant> chants = new Dictionary<int, Chant>();
    readonly List<Chant> order = new List<Chant>();
    readonly List<int> finished = new List<int>();
    Transform player;
    Camera cam;

    // ---------- what the caster calls ----------

    // keep this chant showing the first `reveal` sentences (call it every frame while casting)
    public static void Show(int key, string[] lines, int reveal, Color color)
    {
        if (lines == null || lines.Length == 0 || reveal <= 0) return;
        Instance.ShowInternal(key, lines, Mathf.Min(reveal, lines.Length), color);
    }

    public static void Finish(int key) { if (instance != null) instance.EndInternal(key, End.Finish); }     // the spell was released
    public static void Fail(int key) { if (instance != null) instance.EndInternal(key, End.Fail); }         // the cast broke
    public static void Drop(int key) { if (instance != null) instance.EndInternal(key, End.Drop); }         // abandoned quietly

    // ---------- internals ----------

    void ShowInternal(int key, string[] lines, int reveal, Color color)
    {
        if (!chants.TryGetValue(key, out var c) || c.end != End.Running)
        {
            if (c != null) Destroy(c.root);
            c = NewChant(key, color);
            chants[key] = c;
        }

        c.color = color;
        while (c.lineIndex < reveal) c.rows.Add(NewRow(c, lines[c.lineIndex++]));
    }

    Chant NewChant(int key, Color color)
    {
        var go = new GameObject("Chant" + key, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * Scale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(1000f, 400f);

        return new Chant { key = key, root = go, transform = go.transform, color = color, start = Time.time };
    }

    Row NewRow(Chant c, string line)
    {
        var go = new GameObject("Line", typeof(RectTransform));
        go.transform.SetParent(c.transform, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(1000f, 56f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);

        var t = go.AddComponent<Text>();
        t.font = UIKit.Font;
        t.fontSize = 40;
        t.fontStyle = FontStyle.BoldAndItalic;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.text = "";

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // a new row starts at the bottom of the stack; the older ones are pushed up
        return new Row { text = t, rect = rect, full = line, born = Time.time, y = -LineHeight * 0.6f };
    }

    void EndInternal(int key, End how)
    {
        if (!chants.TryGetValue(key, out var c) || c.end != End.Running) return;
        c.end = how;
        c.endTime = Time.time;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform.root;
        }
        if (cam == null) cam = Camera.main;
        if (player == null || cam == null) return;

        // stacks side by side, oldest on the left
        order.Clear();
        foreach (var c in chants.Values) order.Add(c);
        order.Sort((a, b) => a.start.CompareTo(b.start));

        Vector3 head = player.position + Vector3.up * HeadHeight;
        float dt = Time.deltaTime;
        finished.Clear();

        int standing = 0;
        foreach (var c in order) if (c.end == End.Running) standing++;
        int slot = 0;

        foreach (var c in order)
        {
            float fade = 1f, extraRise = 0f, shake = 0f;
            if (c.end != End.Running)
            {
                float e = Time.time - c.endTime;
                float length = c.end == End.Finish ? 0.9f : c.end == End.Fail ? 0.5f : 0.25f;
                if (e >= length) { finished.Add(c.key); continue; }
                fade = 1f - e / length;
                if (c.end == End.Finish) extraRise = e * 320f;
                if (c.end == End.Fail) shake = Mathf.Sin(e * 90f) * 10f;
            }

            // position: above the head, facing the camera; running stacks are spread sideways, a finishing stack stays where it was
            float offset = (c.end == End.Running ? slot++ - (standing - 1) * 0.5f : 0f) * StackSpacing;
            c.transform.rotation = cam.transform.rotation;
            c.transform.position = head + cam.transform.right * offset;

            float age = Time.time - c.start;

            // from the newest row backward, so rows that have faded out can be removed on the way
            for (int i = c.rows.Count - 1; i >= 0; i--)
            {
                var r = c.rows[i];
                float rowAge = Time.time - r.born;
                int rank = c.rows.Count - 1 - i;                 // 0 = newest sentence

                // when the next sentence arrives the older ones fade: the second row to half, the third row away completely, and then it is gone
                float targetFade = rank == 0 ? 1f : rank == 1 ? 0.5f : 0f;
                r.fade = Mathf.MoveTowards(r.fade, targetFade, dt * 2.2f);
                if (rank >= 2 && r.fade <= 0.01f)
                {
                    Destroy(r.rect.gameObject);
                    c.rows.RemoveAt(i);
                    continue;
                }

                // newest row at the bottom, older ones above it; the whole stack drifts upward
                float target = rank * LineHeight + age * Drift + extraRise;
                r.y = Mathf.Lerp(r.y, target, 1f - Mathf.Exp(-7f * dt));
                r.rect.anchoredPosition = new Vector2(shake, r.y);

                // it types itself out
                int shown = Mathf.CeilToInt(r.full.Length * Mathf.Clamp01(rowAge / 0.5f));
                r.text.text = r.full.Substring(0, shown) + "<color=#00000000>" + r.full.Substring(shown) + "</color>";

                Color col = c.end == End.Fail ? Color.Lerp(new Color(1f, 0.3f, 0.25f), Color.white, 0.2f) : Color.Lerp(c.color, Color.white, 0.55f);
                col.a = Mathf.Clamp01(rowAge / 0.25f) * r.fade * fade * (c.end == End.Fail && Mathf.Sin(Time.time * 70f) < 0f ? 0.4f : 1f);
                r.text.color = col;
            }
        }

        foreach (int key in finished)
        {
            if (chants.TryGetValue(key, out var c)) { Destroy(c.root); chants.Remove(key); }
        }
    }
}
