using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoom : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] CinemachineCamera[] cameras;

    [Header("Input")]
    [SerializeField] InputActionReference zoomAction;

    [Header("Zoom (multiplier)")]
    [SerializeField] float minZoom = 0.6f;
    [SerializeField] float maxZoom = 1.5f;
    [SerializeField] float step = 0.1f;
    [SerializeField] float smoothTime = 0.12f;

    CinemachineThirdPersonFollow[] followers;
    CinemachineGroupFraming[] framings;
    float[] baseDistances;
    float[] baseFramingSizes;

    float targetZoom = 1f;
    float currentZoom = 1f;
    float velocity;

    public float Zoom => currentZoom;

    void Awake()
    {
        int n = cameras.Length;
        followers = new CinemachineThirdPersonFollow[n];
        framings = new CinemachineGroupFraming[n];
        baseDistances = new float[n];
        baseFramingSizes = new float[n];

        for (int i = 0; i < n; i++)
        {
            followers[i] = cameras[i].GetComponent<CinemachineThirdPersonFollow>();
            framings[i] = cameras[i].GetComponent<CinemachineGroupFraming>();

            if (followers[i] != null) baseDistances[i] = followers[i].CameraDistance;
            if (framings[i] != null) baseFramingSizes[i] = framings[i].FramingSize;

            if (followers[i] == null && framings[i] == null)
                Debug.LogWarning("[Zoom] " + cameras[i].name + " has nothing to zoom", this);
        }
    }

    void OnEnable() { zoomAction.action.Enable(); }

    // call to snap back to the default zoom
    public void ResetZoom(bool instant = false)
    {
        targetZoom = 1f;
        if (instant) { currentZoom = 1f; velocity = 0f; Apply(); }
    }

    void Update()
    {
        float scroll = Mathf.Clamp(zoomAction.action.ReadValue<Vector2>().y, -1f, 1f);
        if (Mathf.Abs(scroll) > 0.01f)
            targetZoom = Mathf.Clamp(targetZoom - scroll * step, minZoom, maxZoom);

        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref velocity, smoothTime);
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            if (framings[i] != null && framings[i].enabled)
            {
                // Group Framing owns the distance, so zoom through its size.
                // Larger framing size = group fills more of the screen = camera closer.
                framings[i].FramingSize = baseFramingSizes[i] / currentZoom;
            }
            else if (followers[i] != null)
            {
                followers[i].CameraDistance = baseDistances[i] * currentZoom;
            }
        }
    }
}