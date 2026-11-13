using UnityEngine;
using UnityEngine.UI;

// A small horizon ball beside the crosshair, shown while flying free on the broom.
// The shaded half is the ground: it sits at the bottom when you are the right way up, tilts as you roll,
// fills the ball when you dive and comes round to the top when you are upside down. The little wings in the
// middle are you. The rim turns red while you are upside down.
// Built at runtime, so it needs no assets; BroomFlight creates it.
public class FlipIndicator : MonoBehaviour
{
    [SerializeField] Vector2 offset = new Vector2(64f, 0f);   // from the crosshair, in canvas units
    [SerializeField] float size = 46f;
    [SerializeField] Color skyColor = new Color(0.45f, 0.75f, 1f, 0.22f);
    [SerializeField] Color groundColor = new Color(0.36f, 0.25f, 0.15f, 0.8f);
    [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] Color markColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] Color invertedColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] float fadeSpeed = 6f;

    BroomFlight broom;
    TargetLockOn lockOn;
    Camera cam;
    RectTransform rect, ground;
    CanvasGroup group;
    Image ring;
    Vector2 groundDir = Vector2.down;   // where the ground is on screen

    public static FlipIndicator Create(BroomFlight broom, TargetLockOn lockOn, Camera cam)
    {
        var go = new GameObject("FlipIndicator", typeof(RectTransform));
        go.transform.SetParent(lockOn.Indicator.parent, false);
        go.transform.SetSiblingIndex(lockOn.Indicator.GetSiblingIndex() + 1);
        var indicator = go.AddComponent<FlipIndicator>();
        indicator.broom = broom;
        indicator.lockOn = lockOn;
        indicator.cam = cam;
        indicator.Build();
        return indicator;
    }

    void Build()
    {
        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Sprite disc = MakeCircle(64, 0f);

        // the ball: the sky is the disc itself, and it masks the ground half
        Image ball = Part("Ball", rect, skyColor, disc);
        Stretch(ball.rectTransform);
        ball.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        ground = Part("Ground", ball.rectTransform, groundColor, null).rectTransform;
        ground.pivot = new Vector2(0.5f, 1f);              // its top edge is the horizon
        ground.sizeDelta = new Vector2(size * 3f, size * 3f);

        RectTransform horizon = Part("Horizon", ground, lineColor, null).rectTransform;
        horizon.anchorMin = new Vector2(0f, 1f);
        horizon.anchorMax = new Vector2(1f, 1f);
        horizon.sizeDelta = new Vector2(0f, 2f);

        ring = Part("Rim", rect, lineColor, MakeCircle(64, 0.86f));
        Stretch(ring.rectTransform);

        // you: two wings and a dot, fixed in the middle
        for (int side = -1; side <= 1; side += 2)
        {
            RectTransform wing = Part("Wing", rect, markColor, null).rectTransform;
            wing.sizeDelta = new Vector2(size * 0.22f, 2.5f);
            wing.anchoredPosition = new Vector2(side * size * 0.24f, 0f);
        }
        Part("Dot", rect, markColor, disc).rectTransform.sizeDelta = new Vector2(5f, 5f);
    }

    static Image Part(string name, RectTransform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // a soft-edged white circle; inner > 0 hollows it into a ring (as a fraction of the radius)
    static Sprite MakeCircle(int pixels, float inner)
    {
        var tex = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var colors = new Color[pixels * pixels];
        float centre = (pixels - 1) * 0.5f, radius = pixels * 0.5f - 1f;
        for (int y = 0; y < pixels; y++)
            for (int x = 0; x < pixels; x++)
            {
                float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                if (inner > 0f) a *= Mathf.Clamp01(d - radius * inner + 0.5f);
                colors[y * pixels + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, pixels, pixels), new Vector2(0.5f, 0.5f), 100f);
    }

    void LateUpdate()
    {
        if (broom == null || lockOn == null || cam == null) { Destroy(gameObject); return; }

        // only in free flight: locked on, the view is always the right way up and the crosshair sits on the enemy
        bool show = broom.IsFlying && !lockOn.IsLocked;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        if (group.alpha <= 0f) return;

        rect.position = lockOn.Indicator.position + (Vector3)(offset * rect.lossyScale.x);

        // straight down, as seen from the camera: x/y say which way the ground is on screen, z how much we look at it
        Vector3 down = Quaternion.Inverse(cam.transform.rotation) * Vector3.down;
        Vector2 flat = new Vector2(down.x, down.y);
        if (flat.magnitude > 0.02f) groundDir = flat.normalized;   // looking straight up or down: keep the last side

        float dive = Mathf.Asin(Mathf.Clamp(down.z, -1f, 1f)) / (Mathf.PI * 0.5f);   // 1 = looking straight down, -1 = straight up
        ground.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(groundDir.x, -groundDir.y) * Mathf.Rad2Deg);
        ground.anchoredPosition = -groundDir * (size * 0.5f * dive);

        bool upsideDown = cam.transform.up.y < 0f;
        ring.color = Color.Lerp(ring.color, upsideDown ? invertedColor : lineColor, 10f * Time.unscaledDeltaTime);
    }
}
