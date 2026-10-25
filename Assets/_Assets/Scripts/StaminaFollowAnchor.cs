using UnityEngine;

public class StaminaAnchorFollow : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Vector3 cameraSpaceOffset = new Vector3(0.7f, 1.4f, 0f); // x right, y up
    [SerializeField] Camera cam;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void LateUpdate()
    {
        if (player == null || cam == null) return;

        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();

        transform.position = player.position
            + right * cameraSpaceOffset.x
            + Vector3.up * cameraSpaceOffset.y;
    }
}