using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The edge of a map: a circle around this object. Leaving it starts a countdown with a warning on screen. Coming back in time
// cancels it; when it runs out (or she gets very far out) the controls are taken away and she walks or flies back by herself until
// she is well inside again. A wall of light shows where the edge is when she is close to it.
// Put one in a stage scene; a scene without it has no edge.
[DefaultExecutionOrder(-50)]      // before the controllers read the input this writes while taking her back
public class MapBounds : MonoBehaviour
{
    [Header("Area (a circle around this object)")]
    public float radius = 750f;

    [Header("Leaving")]
    [SerializeField, Tooltip("Seconds she may stay outside before she is taken back.")]
    float graceTime = 4f;
    [SerializeField, Tooltip("This far outside she is taken back at once, whatever the countdown says.")]
    float hardLimit = 200f;
    [SerializeField, Tooltip("How fast the countdown refills once she is back inside (seconds of countdown per second).")]
    float recoverRate = 2f;

    [Header("Being Taken Back")]
    [SerializeField, Tooltip("She gets the controls back this far inside the edge, on the broom.")]
    float returnDepthFlying = 70f;
    [SerializeField, Tooltip("Same, on foot.")]
    float returnDepthWalking = 12f;
    [SerializeField, Tooltip("Degrees per second the view swings round to face the way back. 0 = the view is left alone.")]
    float viewTurnSpeed = 110f;

    [Header("Wall Of Light")]
    [SerializeField] Material veilMaterial;
    [SerializeField] float veilBottom = -80f;
    [SerializeField] float veilTop = 420f;
    [SerializeField, Tooltip("Size of one diamond of the lattice, in metres.")]
    float veilCell = 7f;

    [Header("Warning On Screen")]
    [SerializeField] string leavingText = "LEAVING THE FOREST";
    [SerializeField] string turnBackText = "Turn back";
    [SerializeField] string returningText = "RETURNING TO THE FOREST";
    [SerializeField] Color warnColor = new Color(1f, 0.45f, 0.62f);

    public static MapBounds Active { get; private set; }
    public bool IsOutside { get; private set; }
    public bool IsReturning { get; private set; }
    public float TimeLeft => timeLeft;

    Transform player;
    BroomFlight broom;
    StarterAssetsInputs input;
    PlayerThirdPersonController controller;
    PlayerInput playerInput;
    Health health;
    Transform cam;
    float timeLeft;
    bool walkForced;
    Vector3 stuckFrom;
    float stuckTimer, veerTimer, veerSign = 1f;

    Material veil;
    CanvasGroup group;
    Text title, sub;
    Image vignette;
    float shown;
    static readonly int AlertId = Shader.PropertyToID("_Alert");

    void OnEnable() { Active = this; timeLeft = graceTime; }

    void OnDisable()
    {
        if (Active == this) Active = null;
        Release();
    }

    void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            broom = go.GetComponentInParent<BroomFlight>();
            input = go.GetComponentInParent<StarterAssetsInputs>();
            controller = go.GetComponentInParent<PlayerThirdPersonController>();
            playerInput = go.GetComponentInParent<PlayerInput>();
            health = go.GetComponentInParent<Health>();
            player = input != null ? input.transform : go.transform;
        }
        BuildVeil();
        BuildWarning();
    }

    void Update()
    {
        if (player == null) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float dist = offset.magnitude;
        IsOutside = dist > radius;

        // paused: everything waits as it is. Dead, or a menu has taken the controls: let go of her.
        if (Time.timeScale <= 0f) { UpdateWarning(); return; }
        if ((health != null && health.IsDead) || (playerInput != null && !playerInput.enabled)) { Release(); UpdateWarning(); return; }

        if (IsReturning)
        {
            bool flying = broom != null && broom.IsFlying;
            if (dist < radius - (flying ? returnDepthFlying : returnDepthWalking)) Release();
            else TakeBack(-offset / Mathf.Max(dist, 0.001f), flying);
        }
        else if (IsOutside)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f || dist > radius + hardLimit)
            {
                IsReturning = true;
                stuckFrom = player.position;
                stuckTimer = veerTimer = 0f;
                SpellFX.Play(Sfx.Fizzle, player.position, 0.8f);
            }
        }
        else timeLeft = Mathf.MoveTowards(timeLeft, graceTime, recoverRate * Time.deltaTime);

        UpdateWarning();
    }

    // ---------- taking her back ----------

    void TakeBack(Vector3 inward, bool flying)
    {
        if (controller != null && viewTurnSpeed > 0f) controller.TurnViewTowards(inward, viewTurnSpeed);

        if (flying)
        {
            if (walkForced) ReleaseWalk();
            broom.ForcedCourse = inward;             // the broom swings round and flies in by itself
            return;
        }

        if (broom != null) broom.ForcedCourse = Vector3.zero;

        // walking into a tree or a rock: step round it for a moment
        stuckTimer += Time.deltaTime;
        if (stuckTimer > 0.8f)
        {
            if ((player.position - stuckFrom).magnitude < 0.8f) { veerTimer = 1.1f; veerSign = -veerSign; }
            stuckFrom = player.position;
            stuckTimer = 0f;
        }
        if (veerTimer > 0f)
        {
            veerTimer -= Time.deltaTime;
            inward = Quaternion.Euler(0f, 65f * veerSign, 0f) * inward;
        }

        // the controller walks relative to the camera, so turn the wanted direction into "keys" for the current view
        float camYaw = cam != null ? cam.eulerAngles.y : 0f;
        Vector3 local = Quaternion.Euler(0f, -camYaw, 0f) * inward;
        input.move = new Vector2(local.x, local.z).normalized;
        input.sprint = true;
        input.jump = false;
        walkForced = true;
    }

    void Release()
    {
        if (broom != null) broom.ForcedCourse = Vector3.zero;
        if (walkForced) ReleaseWalk();
        if (IsReturning) timeLeft = graceTime;
        IsReturning = false;
    }

    // hand the keys back as they are held right now
    void ReleaseWalk()
    {
        walkForced = false;
        if (input == null) return;
        var actions = playerInput != null ? playerInput.actions : null;
        var move = actions != null ? actions.FindAction("Move") : null;
        var sprint = actions != null ? actions.FindAction("Sprint") : null;
        input.move = move != null && move.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
        input.sprint = sprint != null && sprint.enabled && sprint.IsPressed();
    }

    // ---------- the wall of light ----------

    void BuildVeil()
    {
        if (veilMaterial == null) return;
        veil = new Material(veilMaterial);

        const int segments = 240;
        var verts = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * 6];
        float around = Mathf.Round(2f * Mathf.PI * radius / veilCell);      // whole cells, so the pattern closes
        for (int i = 0; i <= segments; i++)
        {
            float f = i / (float)segments, a = f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            verts[i * 2] = p + Vector3.up * veilBottom;
            verts[i * 2 + 1] = p + Vector3.up * veilTop;
            uvs[i * 2] = new Vector2(f * around, veilBottom / veilCell);
            uvs[i * 2 + 1] = new Vector2(f * around, veilTop / veilCell);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, t = i * 6;
            tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2;
            tris[t + 3] = a + 2; tris[t + 4] = a + 1; tris[t + 5] = a + 3;
        }

        var mesh = new Mesh { name = "MapEdge", vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateBounds();

        var go = new GameObject("Map Edge");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = veil;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // ---------- the warning ----------

    void BuildWarning()
    {
        var canvas = UIKit.MakeCanvas("MapBoundsWarning", -10);      // under the HUD, so the tint never covers the bars and slots
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        vignette = UIKit.Box(canvas.transform, "Vignette", warnColor);
        UIKit.Stretch(vignette.rectTransform);
        vignette.sprite = MakeVignette();
        vignette.raycastTarget = false;

        title = UIKit.Label(canvas.transform, "Title", leavingText, 46, warnColor, TextAnchor.MiddleCenter, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 64f));
        Shade(title);

        sub = UIKit.Label(canvas.transform, "Countdown", "", 38, Color.white, TextAnchor.MiddleCenter, true);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(1200f, 54f));
        Shade(sub);
    }

    static void Shade(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.05f, 0.03f, 0.12f, 0.85f);
        o.effectDistance = new Vector2(2f, -2f);
    }

    // clear in the middle, coloured toward the edges of the screen
    static Sprite MakeVignette()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.4f, Mathf.Sqrt(dx * dx + dy * dy)));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void UpdateWarning()
    {
        bool warn = IsReturning || (IsOutside && Time.timeScale > 0f && (health == null || !health.IsDead));
        shown = Mathf.MoveTowards(shown, warn ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (veil != null) veil.SetFloat(AlertId, shown);
        if (group == null) return;

        group.alpha = shown;
        if (shown <= 0f) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (IsReturning ? 3f : 7f));
        var c = warnColor;
        c.a = Mathf.Lerp(0.25f, 0.5f, pulse);
        vignette.color = c;

        if (IsReturning)
        {
            title.text = returningText;
            sub.text = "";
        }
        else if (IsOutside)
        {
            title.text = leavingText;
            sub.text = turnBackText + "   " + Mathf.Max(timeLeft, 0f).ToString("0.0");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.62f);
        const int n = 96;
        Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
