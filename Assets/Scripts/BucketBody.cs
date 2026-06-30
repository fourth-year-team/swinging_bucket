using UnityEngine;

public class BucketBody : MonoBehaviour
{
    [Header("Measured in bucket local space")]
    public float bottomY = 0.39f;
    public float topY = 5.25f;
    public float bottomRadius = 1.57f;
    public float topRadius = 1.9f;

    [Header("Hole Settings")]
    public bool holeEnabled = true;
    public float holeRadius = 0.3f;

    [Header("Paint Settings")]
    public float emptyMass = 10f;
    public float paintLeakRate = 1f;

    [HideInInspector] public Vector3 linearVelocity;
    [HideInInspector] public Vector3 angularVelocity;
    [HideInInspector] public Matrix4x4 prevWorldToLocalMatrix;

    // Physics simulation state (used by Rope)
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 previousPosition;
    [HideInInspector] public float mass;
    [HideInInspector] public float theta;
    [HideInInspector] public float phi;
    [HideInInspector] public float paintMass = 10f;

    Vector3 prevPos;
    Quaternion prevRot;
    Matrix4x4 prevMatrix;

    void Awake()
    {
        prevPos = transform.position;
        prevRot = transform.rotation;
        prevWorldToLocalMatrix = transform.worldToLocalMatrix;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        linearVelocity = (transform.position - prevPos) / dt;

        Quaternion delta = transform.rotation * Quaternion.Inverse(prevRot);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (angleDeg > 180f) angleDeg -= 360f;

        angularVelocity =
            (axis.sqrMagnitude < 0.0001f)
            ? Vector3.zero
            : axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);

        prevPos = transform.position;
        prevRot = transform.rotation;

        prevWorldToLocalMatrix = prevMatrix;
        prevMatrix = transform.worldToLocalMatrix;
    }

    public void Integrate(Vector3 gravity, float dt, float damping)
    {
        Vector3 velocity = (position - previousPosition);

        Vector3 current = position;
        position = current + velocity * (1f - damping) + gravity * dt * dt;

        previousPosition = current;

        if (paintMass > 0f && holeEnabled)
        {
            // Torricelli-like flow: rate ~ sqrt(head) * holeArea * leakRate
            float head = Mathf.Max(paintMass / (emptyMass + paintMass), 0.001f);
            float holeArea = holeRadius * holeRadius;
            float baseFlow = Mathf.Sqrt(head) * holeArea * paintLeakRate;

            // Motion increases spillage
            float motion = angularVelocity.magnitude;
            float motionFactor = 1f + motion * 0.5f;

            paintMass -= baseFlow * motionFactor * dt;
            if (paintMass < 0f) paintMass = 0f;

            // Update total mass for rope dynamics
            mass = emptyMass + paintMass;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!holeEnabled) return;

        Gizmos.color = Color.red;
        int segs = 32;
        float step = Mathf.PI * 2f / segs;

        for (int i = 0; i < segs; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Vector3 p0 = transform.TransformPoint(new Vector3(Mathf.Cos(a0) * holeRadius, bottomY, Mathf.Sin(a0) * holeRadius));
            Vector3 p1 = transform.TransformPoint(new Vector3(Mathf.Cos(a1) * holeRadius, bottomY, Mathf.Sin(a1) * holeRadius));
            Gizmos.DrawLine(p0, p1);
        }
    }
}
