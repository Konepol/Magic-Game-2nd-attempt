using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [SerializeField] Camera cam;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // match the camera's rotation so the bar always faces the screen
        transform.rotation = cam.transform.rotation;
    }
}