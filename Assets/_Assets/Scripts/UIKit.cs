using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Small helpers for building uGUI screens from code (the main menu and the stage results), in the same dark style as the HUD.
public static class UIKit
{
    public static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.13f, 0.94f);
    public static readonly Color ButtonColor = new Color(0.14f, 0.2f, 0.3f, 1f);
    public static readonly Color ButtonHover = new Color(0.24f, 0.4f, 0.58f, 1f);
    public static readonly Color ButtonOff = new Color(0.12f, 0.14f, 0.19f, 0.8f);
    public static readonly Color Accent = new Color(0.45f, 0.82f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    public static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);

    static Font font;
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas MakeCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // a UI object with a RectTransform under parent
    public static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // anchor: where in the parent (0..1), pos: offset from there in reference pixels
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static Image Box(Transform parent, string name, Color color)
    {
        var rt = Child(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
    {
        var rt = Child(parent, name);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button MakeButton(Transform parent, string text, Vector2 size, Action onClick, int fontSize = 30)
    {
        var img = Box(parent, text, Color.white);
        img.rectTransform.sizeDelta = size;

        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = Accent;
        colors.selectedColor = ButtonColor;
        colors.disabledColor = ButtonOff;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        var label = Label(img.transform, "Text", text, fontSize, Color.white, TextAnchor.MiddleCenter, true);
        Stretch(label.rectTransform);
        return button;
    }

    // a vertically scrolling list: add children to content (they keep their own height); the mouse wheel and dragging scroll it
    public static ScrollRect MakeScroll(Transform parent, string name, Vector2 size, out RectTransform content)
    {
        var root = Box(parent, name, new Color(0f, 0f, 0f, 0.0001f));
        root.rectTransform.sizeDelta = size;
        var scroll = root.gameObject.AddComponent<ScrollRect>();

        var viewport = Child(root.transform, "Viewport");
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewImage = viewport.gameObject.AddComponent<Image>();
        viewImage.color = new Color(0f, 0f, 0f, 0.0001f);

        content = Child(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        return scroll;
    }

    // a button that shows it is "on" (the chosen one in a row of choices)
    public static void SetChosen(Button button, bool chosen)
    {
        var colors = button.colors;
        colors.normalColor = chosen ? new Color(0.5f, 0.38f, 0.1f, 1f) : ButtonColor;
        colors.highlightedColor = chosen ? new Color(0.65f, 0.5f, 0.15f, 1f) : ButtonHover;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    public static Slider MakeSlider(Transform parent, Vector2 size, float value, Action<float> onChanged)
    {
        var root = Child(parent, "Slider");
        root.sizeDelta = size;

        var back = Box(root, "Background", new Color(0.12f, 0.16f, 0.24f, 1f));
        Stretch(back.rectTransform);

        var fillArea = Child(root, "Fill Area");
        Stretch(fillArea);
        var fill = Box(fillArea, "Fill", Accent);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        var handleArea = Child(root, "Handle Slide Area");
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);
        var handle = Box(handleArea, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(28f, size.y + 20f);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
        return slider;
    }

    // the UI needs an EventSystem with the Input System's UI module (this project uses the new Input System)
    public static void EnsureEventSystem()
    {
        var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                var old = existing.GetComponent<BaseInputModule>();
                if (old != null) UnityEngine.Object.Destroy(old);
                existing.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            return;
        }

        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
