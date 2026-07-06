using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PaintStreamRenderer : MonoBehaviour
{
    public BucketBody bucket;
    public Color paintColor = new Color(0.1f, 0.35f, 1f, 1f);
    public float dropRadius = 0.08f;
    public float spawnInterval = 0.025f;
    public float dropLifetime = 3f;
    public float exitSpeed = 2.5f;
    public float gravity = 9.81f;

    DrawingBoard drawingBoard;
    readonly List<Drop> drops = new();
    Mesh mesh;
    MeshRenderer meshRenderer;
    Material material;
    float spawnTimer;
    float previousPaintMass;
    Vector3 previousHolePos;

    struct Drop
    {
        public Vector3 position;
        public Vector3 previousPosition;
        public float age;
    }

    void Awake()
    {
        if (bucket == null) bucket = FindFirstObjectByType<BucketBody>();
        drawingBoard = FindFirstObjectByType<DrawingBoard>();

        mesh = new Mesh { name = "Manual Paint Stream" };
        GetComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        material = new Material(shader);
        if (GameManager.Instance != null) paintColor = GameManager.Instance.selectedColor;
        material.color = paintColor;
        material.renderQueue = 3000;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        previousPaintMass = bucket != null ? bucket.paintMass : 0f;
        if (bucket != null) previousHolePos = bucket.GetHoleWorldPosition();
    }

    public void ClearDrops()
    {
        drops.Clear();
        mesh.Clear();
        previousPaintMass = bucket != null ? bucket.paintMass : 0f;
        if (bucket != null) previousHolePos = bucket.GetHoleWorldPosition();
    }

    void LateUpdate()
    {
        if (bucket == null) return;

        float dt = Time.deltaTime;
        float drained = Mathf.Max(0f, previousPaintMass - bucket.paintMass);
        previousPaintMass = bucket.paintMass;

        if (bucket.holeEnabled && (drained > 0f || bucket.paintMass > 0f))
        {
            spawnTimer += dt;
            while (spawnTimer >= spawnInterval)
            {
                spawnTimer -= spawnInterval;
                SpawnDrop();
            }
        }

        if (GameManager.Instance != null) paintColor = GameManager.Instance.selectedColor;

        SimulateDrops(dt);
        // Pin the newest drop to the bucket hole so the stream stays attached
        if (drops.Count > 0)
        {
            Drop anchor = drops[^1];
            anchor.position = bucket.GetHoleWorldPosition();
            anchor.previousPosition = previousHolePos;
            drops[^1] = anchor;
        }
        previousHolePos = bucket.GetHoleWorldPosition();
        RebuildMesh();

        if (material != null && material.color != paintColor)
            material.color = paintColor;
    }

    void SpawnDrop()
    {
        Vector3 direction = bucket.GetHoleWorldDirection().normalized;
        Vector3 pos = bucket.GetHoleWorldPosition();
        Vector3 vel = bucket.linearVelocity + direction * exitSpeed;
        drops.Add(new Drop
        {
            position = pos,
            previousPosition = pos - vel * spawnInterval,
            age = 0f
        });
    }

    void SimulateDrops(float dt)
    {
        float boardTopY = drawingBoard != null ? drawingBoard.GetTopSurfaceY() : float.MinValue;
        Vector3 acceleration = Vector3.down * gravity;
        float damping = 0.98f;
        float dt2 = dt * dt;

        // Verlet integration
        for (int i = 0; i < drops.Count; i++)
        {
            Drop drop = drops[i];
            Vector3 vel = drop.position - drop.previousPosition;
            Vector3 newPos = drop.position + vel * damping + acceleration * dt2;
            drop.previousPosition = drop.position;
            drop.position = newPos;
            drop.age += dt;
            drops[i] = drop;
        }

        // Distance constraints between consecutive drops for cohesive stream
        float targetDist = exitSpeed * spawnInterval * 1.2f;
        for (int iter = 0; iter < 5; iter++)
        {
            for (int i = 0; i < drops.Count - 1; i++)
            {
                Drop a = drops[i];
                Drop b = drops[i + 1];
                Vector3 delta = b.position - a.position;
                float dist = delta.magnitude;
                if (dist > 0.0001f)
                {
                    float correction = (dist - targetDist) / dist * 0.5f;
                    Vector3 adjust = delta * correction;
                    a.position += adjust;
                    b.position -= adjust;
                    drops[i] = a;
                    drops[i + 1] = b;
                }
            }
        }

        // Clamp to board and remove old drops
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Drop drop = drops[i];
            if (drop.position.y < boardTopY)
            {
                drop.position.y = boardTopY;
                drop.previousPosition.y = boardTopY;
            }
            if (drop.age >= dropLifetime)
                drops.RemoveAt(i);
            else
                drops[i] = drop;
        }

        // Lock bottom drop at board surface and pin XZ to hole for straight-down fall
        if (drops.Count > 0)
        {
            Drop bottom = drops[0];
            if (bottom.position.y <= boardTopY)
            {
                bottom.position.y = boardTopY;
                bottom.previousPosition.y = boardTopY;
            }
            Vector3 holeXZ = bucket.GetHoleWorldPosition();
            bottom.position.x = Mathf.Lerp(bottom.position.x, holeXZ.x, 0.5f);
            bottom.position.z = Mathf.Lerp(bottom.position.z, holeXZ.z, 0.5f);
            drops[0] = bottom;
        }

        // Strong horizontal damping on all drops so stream falls straight
        Vector3 holePosXZ = bucket.GetHoleWorldPosition();
        for (int i = 1; i < drops.Count; i++)
        {
            Drop drop = drops[i];
            drop.position.x = Mathf.Lerp(drop.position.x, holePosXZ.x, 0.15f);
            drop.position.z = Mathf.Lerp(drop.position.z, holePosXZ.z, 0.15f);
            drops[i] = drop;
        }
    }

    void RebuildMesh()
    {
        if (drops.Count == 0)
        {
            mesh.Clear();
            return;
        }

        Camera cam = Camera.main;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;

        if (drops.Count >= 2)
        {
            int segs = drops.Count - 1;
            Vector3[] vertices = new Vector3[segs * 4];
            int[] triangles = new int[segs * 6];
            Vector2[] uv = new Vector2[segs * 4];

            int vi = 0;
            int ti = 0;
            for (int i = 0; i < segs; i++)
            {
                float ageT0 = Mathf.Clamp01(drops[i].age / dropLifetime);
                float ageT1 = Mathf.Clamp01(drops[i + 1].age / dropLifetime);
                float r0 = dropRadius * Mathf.Max(0.1f, 1f - ageT0 * 0.35f);
                float r1 = dropRadius * Mathf.Max(0.1f, 1f - ageT1 * 0.35f);

                Vector3 p0 = transform.InverseTransformPoint(drops[i].position);
                Vector3 p1 = transform.InverseTransformPoint(drops[i + 1].position);
                Vector3 dir0 = transform.InverseTransformDirection(camRight) * r0;
                Vector3 dir1 = transform.InverseTransformDirection(camRight) * r1;

                vertices[vi + 0] = p0 - dir0;
                vertices[vi + 1] = p0 + dir0;
                vertices[vi + 2] = p1 - dir1;
                vertices[vi + 3] = p1 + dir1;

                uv[vi + 0] = new Vector2(0, 1);
                uv[vi + 1] = new Vector2(1, 1);
                uv[vi + 2] = new Vector2(0, 0);
                uv[vi + 3] = new Vector2(1, 0);

                triangles[ti + 0] = vi + 0;
                triangles[ti + 1] = vi + 2;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + 2;
                triangles[ti + 5] = vi + 3;

                vi += 4;
                ti += 6;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
        }
        else
        {
            int segs = Mathf.Max(5, 8);
            Vector3[] vertices = new Vector3[segs + 1];
            int[] triangles = new int[segs * 3];

            float radius = dropRadius;
            Vector3 center = transform.InverseTransformPoint(drops[0].position);
            Vector3 up = transform.InverseTransformDirection(cam != null ? cam.transform.up : Vector3.up) * radius;
            Vector3 right = transform.InverseTransformDirection(camRight) * radius;

            vertices[0] = center;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                vertices[i + 1] = center + Mathf.Cos(a) * right + Mathf.Sin(a) * up;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i == segs - 1 ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
