using UnityEngine;
using UnityEngine.UI;

// Turns the crosshair itself into a horizon while flying free on the broom.
// The whole crosshair rolls with the world, so its side arrows always lie along the horizon, and a half ring is
// added on the ground side: under the crosshair when you are the right way up, tilting as you roll, on top
// (and red) when you are upside down. The half ring also slides: up over the crosshair when you dive, away from
// it when you climb. On foot and while locked on, the crosshair is exactly as it was.
// Built at runtime, so it needs no assets; BroomFlight creates it.
public class FlipIndicator : MonoBehaviour
{
    [SerializeField] float radius = 78f;              // of the half ring, in the crosshair's own units
    [SerializeField] float pitchTravel = 46f;         // how far the half ring slides at a straight dive or climb
    [SerializeField] Color groundColor = new Color(1f, 0.82f, 0.45f, 0.85f);
    [SerializeField] Color invertedColor = new Color(1f, 0.35f, 0.3f, 0.95f);
    [SerializeField] float fadeSpeed = 6f;
    [SerializeField] float courseSize = 26f;          // the free-aim course marker
    [SerializeField] Color courseColor = new Color(0.78f, 0.72f, 1f, 0.95f);

    RectTransform course;
    Image courseImage;
    BroomFlight broom;
    TargetLockOn lockOn;
    Camera cam;
    RectTransform crosshair, horizon;
    CanvasGroup group;
    Graphic[] parts;
    Color tint;
    Vector2 groundDir = Vector2.down;   // where the ground is on screen
    bool rolled;

    public static FlipIndicator Create(BroomFlight broom, TargetLockOn lockOn, Camera cam)
    {
        var go = new GameObject("Horizon", typeof(RectTransform));
        go.transform.SetParent(lockOn.Indicator, false);
        var indicator = go.AddComponent<FlipIndicator>();
        indicator.broom = broom;
        indicator.lockOn = lockOn;
        indicator.cam = cam;
        indicator.crosshair = lockOn.Indicator;
        indicator.Build();
        return indicator;
    }

    void Build()
    {
        horizon = (RectTransform)transform;
        horizon.anchorMin = horizon.anchorMax = horizon.pivot = new Vector2(0.5f, 0.5f);
        horizon.sizeDelta = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        tint = groundColor;

        // the ground: the lower half of a thin ring
        Image arc = Part("Ground", MakeHalfRing(128, 0.94f));
        arc.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);

        // a short horizon tick at each end of it
        Image left = Part("Tick", null), right = Part("Tick", null);
        left.rectTransform.sizeDelta = right.rectTransform.sizeDelta = new Vector2(radius * 0.22f, 3f);
        left.rectTransform.anchoredPosition = new Vector2(-radius, 0f);
        right.rectTransform.anchoredPosition = new Vector2(radius, 0f);

        parts = new Graphic[] { arc, left, right };

        // free aim: a small ring that marks where the broom is heading while the crosshair looks elsewhere.
        // It sits beside the crosshair (not inside it), so it neither rolls nor moves with it.
        course = Part("Course", MakeHalfRing(48, 0.72f, false)).rectTransform;
        course.SetParent(crosshair.parent, false);
        course.SetSiblingIndex(crosshair.GetSiblingIndex());
        course.sizeDelta = new Vector2(courseSize, courseSize);
        courseImage = course.GetComponent<Image>();
        courseImage.color = Color.clear;
    }

    Image Part(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(horizon, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = groundColor;
        image.raycastTarget = false;
        return image;
    }

    // the lower half of a soft-edged white ring; inner is the hole, as a fraction of the radius
    static Sprite MakeHalfRing(int pixels, float inner, bool bottomOnly = true)
    {
        var tex = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var colors = new Color[pixels * pixels];
        float centre = (pixels - 1) * 0.5f, outer = pixels * 0.5f - 1f;
        for (int y = 0; y < pixels; y++)
            for (int x = 0; x < pixels; x++)
            {
                float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - outer * inner + 0.5f);
                if (bottomOnly) a *= Mathf.Clamp01(centre - y + 0.5f);   // texture rows run bottom to top: keep the bottom half
                colors[y * pixels + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, pixels, pixels), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        if (course != null) Destroy(course.gameObject);
    }

    // the course marker: on screen where the broom is heading, only while free aiming and only if that is in view
    void UpdateCourse()
    {
        Vector3 screen = cam.WorldToScreenPoint(cam.transform.position + broom.CourseDirection * 200f);
        bool visible = broom.IsFreeAim && screen.z > 0f;
        if (visible) course.position = new Vector3(screen.x, screen.y, 0f);

        Color target = visible ? courseColor : new Color(courseColor.r, courseColor.g, courseColor.b, 0f);
        courseImage.color = Color.Lerp(courseImage.color, target, 12f * Time.unscaledDeltaTime);
    }

    // after the crosshair's own animator, so the roll sits on top of it
    void LateUpdate()
    {
        if (broom == null || lockOn == null || cam == null) { Destroy(gameObject); return; }

        // only in free flight: locked on, the view is always the right way up and the crosshair marks the enemy
        bool show = broom.IsFlying && !lockOn.IsLocked;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        UpdateCourse();
        if (!show)
        {
            if (rolled) { crosshair.localRotation = Quaternion.identity; rolled = false; }   // hand the crosshair back untouched
            return;
        }

        // straight down, as seen from the camera: x/y say which way the ground is on screen, z how much we look at it
        Vector3 down = Quaternion.Inverse(cam.transform.rotation) * Vector3.down;
        Vector2 flat = new Vector2(down.x, down.y);
        if (flat.magnitude > 0.02f) groundDir = flat.normalized;   // looking straight up or down: keep the last side

        // roll the whole crosshair so its underside faces the ground
        crosshair.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(groundDir.x, -groundDir.y) * Mathf.Rad2Deg);
        rolled = true;

        // dive: the ground comes up over the crosshair. Climb: it drops away.
        float dive = Mathf.Asin(Mathf.Clamp(down.z, -1f, 1f)) / (Mathf.PI * 0.5f);   // 1 = looking straight down, -1 = straight up
        horizon.anchoredPosition = new Vector2(0f, dive * pitchTravel);

        bool upsideDown = cam.transform.up.y < 0f;
        tint = Color.Lerp(tint, upsideDown ? invertedColor : groundColor, 10f * Time.unscaledDeltaTime);
        for (int i = 0; i < parts.Length; i++) parts[i].color = tint;
    }
}
