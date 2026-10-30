using UnityEngine;

// A soft round shadow on the ground under this object (the "blob shadow" style). It finds the ground below every frame, lies flat on it,
// and gets smaller and fainter the higher the object is.
public class BlobShadow : MonoBehaviour
{
    public float radius = 0.6f;
    public float fadeHeight = 12f;
    public LayerMask groundMask = ~0;

    static Texture2D falloff;
    static Mesh quad;
    static Material material;

    Transform blob;
    MeshRenderer blobRenderer;
    MaterialPropertyBlock block;
    Color[] colors;

    void OnEnable()
    {
        if (falloff == null) falloff = MakeFalloff();
        if (quad == null) quad = MakeQuad();
        if (material == null)
        {
            var shader = Shader.Find("Magic/BlobShadow");
            if (shader == null) { enabled = false; return; }
            material = new Material(shader);
            material.SetTexture("_BaseMap", falloff);
        }

        if (blob == null)
        {
            var go = SceneGroups.Put(new GameObject(name + " BlobShadow"), "VFX/Shadows");
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            blobRenderer = go.AddComponent<MeshRenderer>();
            blobRenderer.sharedMaterial = material;
            blobRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blobRenderer.receiveShadows = false;
            blob = go.transform;
        }
        blob.gameObject.SetActive(true);
    }

    void OnDisable() { if (blob != null) blob.gameObject.SetActive(false); }
    void OnDestroy() { if (blob != null) Destroy(blob.gameObject); }

    void LateUpdate()
    {
        if (blob == null) return;

        Vector3 start = transform.position + Vector3.up * 0.5f;
        var hits = Physics.RaycastAll(start, Vector3.down, fadeHeight + 1f, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit ground = default;
        foreach (var h in hits)
            if (!h.collider.transform.IsChildOf(transform) && h.distance < best) { best = h.distance; ground = h; }

        if (best == float.MaxValue) { blobRenderer.enabled = false; return; }
        blobRenderer.enabled = true;

        float height = Mathf.Max(0f, best - 0.5f);
        float k = Mathf.Clamp01(1f - height / fadeHeight);
        blob.position = ground.point + ground.normal * 0.02f;
        blob.rotation = Quaternion.FromToRotation(Vector3.up, ground.normal);
        blob.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.55f, 1f, k);

        if (block == null) block = new MaterialPropertyBlock();
        block.SetFloat("_Strength", 0.85f * k);
        blobRenderer.SetPropertyBlock(block);
    }

    static Texture2D MakeFalloff()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n * 0.5f + 0.5f, y - n * 0.5f + 0.5f).magnitude / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        t.Apply();
        return t;
    }

    static Mesh MakeQuad()
    {
        var m = new Mesh { name = "BlobQuad" };
        m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
