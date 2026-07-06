using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PaintFillLevel : MonoBehaviour
{
    public BucketBody bucket;
    public Color paintColor = new Color(0.1f, 0.35f, 1f, 0.9f);
    public float surfaceInset = 0.08f;
    public int segments = 64;
    public float minVisibleFill = 0.01f;

    Mesh mesh;
    MeshRenderer meshRenderer;
    Material material;

    void Awake()
    {
        if (bucket == null) bucket = GetComponentInParent<BucketBody>();

        mesh = new Mesh { name = "Paint Fill Surface" };
        GetComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        material = new Material(Shader.Find("Unlit/Color"));
        if (GameManager.Instance != null) paintColor = GameManager.Instance.selectedColor;
        material.color = paintColor;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (bucket == null) return;

        float fill01 = bucket.Fill01;
        meshRenderer.enabled = fill01 > minVisibleFill;
        if (!meshRenderer.enabled) return;

        transform.SetPositionAndRotation(bucket.transform.position, bucket.transform.rotation);
        transform.localScale = bucket.transform.lossyScale;

        float localY = Mathf.Lerp(bucket.bottomY, bucket.topY, fill01);
        float radius = Mathf.Max(0.01f, bucket.GetRadiusAtLocalY(localY) - surfaceInset);
        RebuildDisc(localY, radius);

        if (GameManager.Instance != null) paintColor = GameManager.Instance.selectedColor;

        if (material != null && material.color != paintColor)
            material.color = paintColor;
    }

    void RebuildDisc(float y, float radius)
    {
        int segmentCount = Mathf.Max(12, segments);
        Vector3[] vertices = new Vector3[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];

        vertices[0] = new Vector3(0, y, 0);
        for (int i = 0; i < segmentCount; i++)
        {
            float a = i / (float)segmentCount * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            int tri = i * 3;
            triangles[tri] = 0;
            triangles[tri + 1] = i + 1;
            triangles[tri + 2] = i == segmentCount - 1 ? 1 : i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
