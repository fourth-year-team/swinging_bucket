using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
//[ExecuteAlways]
public class Rope : MonoBehaviour
{
    [Header("Initial Conditions")]
    public float startTheta = 20f;
    public float startPhi = 0f;
    public Vector3 inital_offset = new Vector3(10, 0, 0);

    public bool angularMotion = true;

    private float theta;
    private float phi;

    [Header("Rope Setup")]
    public Transform anchor;
    public Transform bucketEnd;
    public int segmentCount = 20;
    public float ropeLength = 5f;
    public float ropeMass = 10f;
    public int constraintIterations = 40;
    public float gravity = -9.81f;
    public float damping = 0.02f;

    [Header("Bucket")]
    public BucketBody bucketBody;
    public Transform bucketVisual;
    public float bucketMass = 10f;
    public float paintMass = 40f;
    public float totalMass = 0f;

    private float[] particleMass;

    [Header("Mesh Settings")]
    public float ropeRadius = 0.03f;
    public int radialSegments = 6;

    [HideInInspector] public Vector3[] currentPos;
    private Vector3[] previousPos;
    private bool[] isLocked;
    private float segmentLength;

    private Mesh mesh;
    private MeshFilter meshFilter;

    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;

    void OnEnable()
    {
        Initialize();
    }

    void Initialize()
    {
        if (anchor == null || bucketEnd == null) return;

        if (anchor == null || segmentCount < 2) return;

        // Initilize bucket position
        theta = startTheta * Mathf.Deg2Rad;
        phi = startPhi * Mathf.Deg2Rad;

        Vector3 bucketStart = anchor.position + new Vector3(
        ropeLength * Mathf.Sin(theta) * Mathf.Cos(phi),
       -ropeLength * Mathf.Cos(theta),
        ropeLength * Mathf.Sin(theta) * Mathf.Sin(phi));

        // Rope Data
        segmentLength = ropeLength / (segmentCount - 1);
        currentPos = new Vector3[segmentCount];
        previousPos = new Vector3[segmentCount];
        isLocked = new bool[segmentCount];

        particleMass = new float[segmentCount];

        float segmentMass = ropeMass / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            particleMass[i] = segmentMass;
        }

        totalMass = bucketMass + paintMass;

        // bucket mass is concentrated at the rope end
        particleMass[segmentCount - 1] += totalMass;

        Vector3 dir = (bucketStart - anchor.position).normalized;
       
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 pos = (anchor.position + dir * segmentLength * i);
            currentPos[i] = pos;
            previousPos[i] = pos;
        }

        if (bucketBody == null && bucketVisual != null)
            bucketBody = bucketVisual.GetComponent<BucketBody>();
        if (bucketBody == null && bucketEnd != null)
            bucketBody = bucketEnd.GetComponentInParent<BucketBody>();
        if (bucketBody == null && bucketEnd != null)
            bucketBody = bucketEnd.gameObject.AddComponent<BucketBody>();

        bucketBody.position = currentPos[segmentCount - 1];
        bucketBody.mass = totalMass;
        bucketBody.theta = theta;
        bucketBody.phi = phi;
        bucketBody.paintMass = 10f;

        if (angularMotion)
        {
            float omega = Mathf.Sqrt(Mathf.Abs(gravity) / (ropeLength * Mathf.Cos(theta)));
            float tangentSpeed = omega * ropeLength * Mathf.Sin(theta);

            Vector3 tangent = new Vector3(
                -Mathf.Sin(startPhi * Mathf.Deg2Rad),
                 0f,
                 Mathf.Cos(startPhi * Mathf.Deg2Rad)
            );

            Vector3 initialVelocity = tangent * tangentSpeed;

            float dt = Time.fixedDeltaTime;

            bucketBody.previousPosition = currentPos[segmentCount - 1] - initialVelocity * dt;
        }
        else
        {
            bucketBody.previousPosition = currentPos[segmentCount - 1];
        }

        isLocked[0] = true; // top point fixed to ceiling hook

        meshFilter = GetComponent<MeshFilter>();
        mesh = new Mesh();
        mesh.name = "RopeMesh";
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        int vertCount = segmentCount * radialSegments;
        vertices = new Vector3[vertCount];
        uvs = new Vector2[vertCount];
        triangles = new int[(segmentCount - 1) * radialSegments * 6];

        BuildTriangles();
        BuildUVs();
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        Vector3 hookOffset = bucketVisual.position - bucketEnd.position;

        if (currentPos == null || currentPos.Length != segmentCount)
        {
            Initialize();
            if (currentPos == null) return;
        }

        Simulate();

        for (int i = 0; i < constraintIterations; i++)
            ApplyConstraints();

        if (bucketBody == null) return;

        if (bucketVisual != null)
        {
            Vector3 ropeDir = (currentPos[segmentCount - 1] - currentPos[segmentCount - 2]).normalized;

            bucketVisual.position = bucketBody.position + hookOffset;
            bucketVisual.rotation = Quaternion.FromToRotation(Vector3.up, -ropeDir);
        }

        UpdateMesh();
    }

    void Simulate()
    {
        float dt = Time.fixedDeltaTime;

        if (angularMotion) 
        {
            float omega = Mathf.Sqrt(Mathf.Abs(gravity) / (ropeLength * Mathf.Cos(theta)));
            float tangentSpeed = omega * ropeLength * Mathf.Sin(theta);

            Vector3 tangent = new Vector3(
                -Mathf.Sin(startPhi * Mathf.Deg2Rad),
                 0f,
                 Mathf.Cos(startPhi * Mathf.Deg2Rad)
            );

            Vector3 initialVelocity = tangent * tangentSpeed;
        }
        

        // Rope Segmants Motion
        for (int i = 0; i < segmentCount; i++)
        {
            if (isLocked[i]) continue;

            Vector3 velocity = (currentPos[i] - previousPos[i]);


            Vector3 current = currentPos[i];
            Vector3 acceleration = Vector3.up * gravity;

            currentPos[i] = current + velocity * (1f - damping) + acceleration * dt * dt;
            previousPos[i] = current;

        }

        Vector3 gravityVec = Vector3.up * gravity;

        // integrate bucket
        bucketBody.Integrate(gravityVec, dt, damping);
    }

    void ApplyConstraints()
    {
        if (anchor != null)
            currentPos[0] = anchor.position;

        for (int i = 0; i < segmentCount - 1; i++)
        {
            Vector3 delta = currentPos[i + 1] - currentPos[i];
            float dist = delta.magnitude;
            if (dist < 0.0001f) continue;

            float error = (dist - segmentLength) / dist;
            Vector3 correction = delta * 0.5f * error;

            float m1 = particleMass[i];
            float m2 = particleMass[i + 1];

            float totalMass = m1 + m2;

            float moveA = m2 / totalMass;
            float moveB = m1 / totalMass;

            if (!isLocked[i])
                currentPos[i] += correction * moveA;

            if (!isLocked[i + 1])
                currentPos[i + 1] -= correction * moveB;
        }

        //Vector3 ropeEnd = currentPos[segmentCount - 1];

        //Vector3 correction_bucket =
        //    ropeEnd - bucketBody.position;

        //bucketBody.previousPosition += correction_bucket * 0.5f;
        //bucketBody.position += correction_bucket * 0.5f;

        Vector3 ropeEnd = currentPos[segmentCount - 1];
        Vector3 delt = ropeEnd - bucketBody.position;
        float dis = delt.magnitude;
        if (dis < 0.0001f) return;

        // mass-weighted split (match what you do for rope segments)
        float total_mass = particleMass[segmentCount - 1] + bucketBody.mass;
        float moveRope = bucketBody.mass / total_mass;
        float moveBucket = particleMass[segmentCount - 1] / total_mass;

        float eror = (dis - 0f) / dis;   // desired dist = 0 (they meet)
        Vector3 corr = delt * eror;

        if (!isLocked[segmentCount - 1])
            currentPos[segmentCount - 1] -= corr * moveRope;

        //bucketBody.previousPosition += corr * moveBucket;
        bucketBody.position += corr * moveBucket;
    }

    void BuildTriangles()
    {
        int triIndex = 0;
        for (int seg = 0; seg < segmentCount - 1; seg++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int current = seg * radialSegments + r;
                int next = seg * radialSegments + (r + 1) % radialSegments;
                int currentNext = (seg + 1) * radialSegments + r;
                int nextNext = (seg + 1) * radialSegments + (r + 1) % radialSegments;

                // two triangles per quad segment of the tube
                triangles[triIndex++] = current;
                triangles[triIndex++] = currentNext;
                triangles[triIndex++] = next;

                triangles[triIndex++] = next;
                triangles[triIndex++] = currentNext;
                triangles[triIndex++] = nextNext;
            }
        }
    }

    void BuildUVs()
    {
        for (int seg = 0; seg < segmentCount; seg++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int idx = seg * radialSegments + r;
                uvs[idx] = new Vector2((float)r / radialSegments, (float)seg / (segmentCount - 1));
            }
        }
    }

    void UpdateMesh()
    {
        for (int seg = 0; seg < segmentCount; seg++)
        {
            Vector3 forward;
            if (seg == 0)
                forward = (currentPos[1] - currentPos[0]).normalized;
            else if (seg == segmentCount - 1)
                forward = (currentPos[seg] - currentPos[seg - 1]).normalized;
            else
                forward = (currentPos[seg + 1] - currentPos[seg - 1]).normalized;

            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.up;

            // Build a stable rotation frame around the rope's centerline
            Vector3 normal = Vector3.Cross(forward, Vector3.right);
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.Cross(forward, Vector3.forward);
            normal.Normalize();
            Vector3 binormal = Vector3.Cross(forward, normal).normalized;

            for (int r = 0; r < radialSegments; r++)
            {
                float angle = (float)r / radialSegments * Mathf.PI * 2f;
                Vector3 mesh_offset = (Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal) * ropeRadius;
                int idx = seg * radialSegments + r;
                vertices[idx] = transform.InverseTransformPoint(currentPos[seg] + mesh_offset);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
