using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoom : MonoBehaviour
{
    [Header("Cameras (each needs a Third Person Follow)")]
    [SerializeField] CinemachineCamera[] cameras;

    [Header("Input")]
    [SerializeField] InputActionReference zoomAction;

    [Header("Zoom (multiplier of each camera's starting distance)")]
    [SerializeField] float minZoom = .6f;     // closest
    [SerializeField] float maxZoom = 1.5f;     // farthest
    [SerializeField] float step = 0.25f;        // per scroll notch
    [SerializeField] float smoothTime = 0.12f;

    CinemachineThirdPersonFollow[] followers;
    float[] baseDistances;
    float targetZoom = 1f;
    float currentZoom = 1f;
    float velocity;

    void Awake()
    {
        followers = new CinemachineThirdPersonFollow[cameras.Length];
        baseDistances = new float[cameras.Length];

        for (int i = 0; i < cameras.Length; i++)
        {
            followers[i] = cameras[i].GetComponent<CinemachineThirdPersonFollow>();
            if (followers[i] == null)
                Debug.LogWarning("[Zoom] " + cameras[i].name + " has no Third Person Follow", this);
            else
                baseDistances[i] = followers[i].CameraDistance;
        }
    }

    void OnEnable()  { zoomAction.action.Enable(); }

    void Update()
    {
        // mouse scroll can report 120 per notch, so clamp it to -1..1
        float scroll = Mathf.Clamp(zoomAction.action.ReadValue<Vector2>().y, -1f, 1f);

        // scroll up = zoom in (smaller multiplier)
        if (Mathf.Abs(scroll) > 0.01f)
            targetZoom = Mathf.Clamp(targetZoom - scroll * step, minZoom, maxZoom);

        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref velocity, smoothTime);

        for (int i = 0; i < followers.Length; i++)
            if (followers[i] != null)
                followers[i].CameraDistance = baseDistances[i] * currentZoom;
    }
}