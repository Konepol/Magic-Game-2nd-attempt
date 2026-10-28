using UnityEditor;
using UnityEngine;

// Tools > Magic > Casting Circle Preview
// Shows a casting circle in the Scene and Game views without entering Play mode, around the Player in the open scene.
// Edit the circle asset (layer order, offsets, sizes, speeds, ready colour) and watch it update live.
public class CastingCirclePreviewWindow : EditorWindow
{
    // where the anchors are, taken from the Player in the open scene
    class PreviewAnchors : CastingCircle.IAnchorSource
    {
        public Transform player;
        public float aimDistance = 8f;
        public float handHeight = 1.5f;

        public Pose GetCirclePose(CircleAnchor anchor, CastingCircleData data, SpellVariant variant)
        {
            Vector3 origin = player != null ? player.position : Vector3.zero;
            Vector3 forward = player != null ? Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            switch (anchor)
            {
                case CircleAnchor.Front:
                    return new Pose(origin + Vector3.up * handHeight + forward * data.frontDistance, Quaternion.LookRotation(forward));
                case CircleAnchor.Feet:
                    return new Pose(Ground(origin) + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
                default:
                    return new Pose(Ground(origin + forward * aimDistance) + Vector3.up * 0.06f, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        static Vector3 Ground(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 100f) ? hit.point : new Vector3(p.x, 0f, p.z);
        }
    }

    CastingCircleData circle;
    SpellData spell;
    bool useHoldVariant = true;
    Color color = new Color(0.4f, 0.8f, 1f, 1f);
    float scale = 1f;
    float aoeRadius = 4f;
    float castTime = 1.5f;

    float progress;
    bool playing = true;
    bool loop = true;
    float holdAtEnd = 0.9f;
    float endTimer;

    CastingCircle preview;
    CastingCircleData previewBuiltFrom;
    int previewLayerCount;
    readonly PreviewAnchors anchors = new PreviewAnchors();
    double lastTime;

    [MenuItem("Tools/Magic/Casting Circle Preview")]
    static void Open()
    {
        var w = GetWindow<CastingCirclePreviewWindow>("Circle Preview");
        w.minSize = new Vector2(320f, 380f);
    }

    void OnEnable()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += DestroyPreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        lastTime = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= DestroyPreview;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        DestroyPreview();
    }

    void OnPlayModeChanged(PlayModeStateChange state) => DestroyPreview();

    // ---------- window ----------

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Previews the circle around the Player in the open scene (edit mode only). Edit the circle asset and it updates live.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        circle = (CastingCircleData)EditorGUILayout.ObjectField("Casting Circle", circle, typeof(CastingCircleData), false);
        spell = (SpellData)EditorGUILayout.ObjectField("Or take it from a spell", spell, typeof(SpellData), false);
        if (EditorGUI.EndChangeCheck() && spell != null) LoadFromSpell();

        if (spell != null)
        {
            EditorGUI.BeginChangeCheck();
            useHoldVariant = EditorGUILayout.Toggle("Use hold version", useHoldVariant);
            if (EditorGUI.EndChangeCheck()) LoadFromSpell();
        }

        EditorGUILayout.Space();
        color = EditorGUILayout.ColorField("Spell colour", color);
        scale = EditorGUILayout.Slider("Circle scale", scale, 0.3f, 2f);
        aoeRadius = EditorGUILayout.FloatField("Ground area radius", aoeRadius);
        castTime = Mathf.Max(0.2f, EditorGUILayout.FloatField("Cast time (seconds)", castTime));
        anchors.aimDistance = EditorGUILayout.Slider("Aim point distance", anchors.aimDistance, 2f, 30f);
        anchors.handHeight = EditorGUILayout.Slider("Hand height", anchors.handHeight, 0.5f, 2.5f);

        EditorGUILayout.Space();
        float newProgress = EditorGUILayout.Slider("Progress", progress, 0f, 1f);
        if (!Mathf.Approximately(newProgress, progress)) { progress = newProgress; playing = false; Apply(); }

        loop = EditorGUILayout.Toggle("Loop", loop);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "Pause" : "Play")) { playing = !playing; if (playing && progress >= 1f) { progress = 0f; RebuildPreview(); } }
            if (GUILayout.Button("Restart")) { progress = 0f; endTimer = 0f; playing = true; RebuildPreview(); }
            if (GUILayout.Button("Complete")) { progress = 1f; playing = false; Apply(); }
            if (GUILayout.Button("Fizzle")) { if (preview != null) preview.Fizzle(); playing = false; }
        }

        if (GUILayout.Button("Frame the Player in the Scene view"))
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(player.transform.position + Vector3.up, Vector3.one * 8f), false);
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Exit Play mode to use the preview.", MessageType.Info);
    }

    void LoadFromSpell()
    {
        if (spell == null) return;
        circle = useHoldVariant ? spell.holdCircle : spell.tapCircle;
        var v = useHoldVariant && spell.hasHoldVariant ? spell.hold : spell.tap;
        color = v.effectColor;
        scale = useHoldVariant ? spell.circleScale : spell.tapCircleScale;
        aoeRadius = v.aoeRadius;
        castTime = useHoldVariant ? Mathf.Max(spell.holdThreshold, 0.2f) : 0.3f;
        RebuildPreview();
    }

    // ---------- the preview circle ----------

    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastTime), 0.1f);
        lastTime = now;

        if (Application.isPlaying || circle == null)
        {
            if (preview != null) DestroyPreview();
            return;
        }

        // keep the preview matching the asset if layers were added or removed
        int count = circle.layers != null ? circle.layers.Length : 0;
        if (preview == null || previewBuiltFrom != circle || previewLayerCount != count) RebuildPreview();
        if (preview == null) return;

        if (playing)
        {
            if (progress < 1f) progress = Mathf.Min(1f, progress + dt / castTime);
            else if (loop)
            {
                endTimer += dt;
                if (endTimer >= holdAtEnd) { endTimer = 0f; progress = 0f; RebuildPreview(); }
            }
        }

        Apply();
        preview.Tick(dt);
        SceneView.RepaintAll();
        Repaint();
    }

    void Apply()
    {
        if (preview == null) return;
        preview.SetProgress(progress);
        if (progress >= 1f) preview.Complete();
    }

    void RebuildPreview()
    {
        DestroyPreview();
        if (circle == null || Application.isPlaying) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        anchors.player = player != null ? player.transform : null;

        var variant = new SpellVariant { aoeRadius = aoeRadius, effectColor = color };
        preview = CastingCircle.Create(circle, color, anchors, variant, scale);
        if (preview == null) return;

        foreach (var t in preview.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;

        previewBuiltFrom = circle;
        previewLayerCount = circle.layers != null ? circle.layers.Length : 0;
        Apply();
    }

    void DestroyPreview()
    {
        if (preview != null) DestroyImmediate(preview.gameObject);
        preview = null;
        previewBuiltFrom = null;
    }
}
