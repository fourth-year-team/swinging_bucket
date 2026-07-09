using UnityEngine;

public class BucketBody : MonoBehaviour
{
    [Header("Measured in bucket local space")]
    public float bottomY = 0.25f;
    public float topY = 5.25f;
    public float bottomRadius = 1.75f;
    public float topRadius = 1.95f;

    [Header("Hole Settings")]
    public bool holeEnabled = true;
    public float holeRadius = 0.3f;
    public Vector2 holeLocalOffset;

    [Header("Paint Settings")]
    public float emptyMass = 10f;
    public float paintLeakRate = 1f;
    public float paintMass = 10f;
    public float particlesPerKg = 231f;

    [Header("Computed")]
    public int targetParticleCount;

    // Linear simulation state (handle/rope-attachment point)
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 previousPosition;
    [HideInInspector] public float mass;

    // Velocities (read by FluidSim and Rope for tilt)
    [HideInInspector] public Vector3 linearVelocity;
    [HideInInspector] public Vector3 previousLinearVelocity;
    [HideInInspector] public Vector3 angularVelocity;

    // Visual offset from handle to visual pivot (handle-local space)
    [HideInInspector] public Vector3 localVisualOffset = Vector3.zero;

    private float defaultPaintMass;

    public void SetPaintMass(float mass)
    {
        paintMass = mass;
        this.mass = emptyMass + paintMass;
        targetParticleCount = Mathf.RoundToInt(paintMass * particlesPerKg);
    }

    void OnValidate()
    {
        targetParticleCount = Mathf.RoundToInt(paintMass * particlesPerKg);
        mass = emptyMass + paintMass;
    }

    public float Fill01 => emptyMass <= 0f ? 0f : Mathf.Clamp01(paintMass / emptyMass);

    public Vector3 GetHoleWorldPosition()
    {
        return transform.TransformPoint(new Vector3(holeLocalOffset.x, bottomY, holeLocalOffset.y));
    }

    public Vector3 GetHoleWorldDirection()
    {
        return -transform.up;
    }

    public float GetRadiusAtLocalY(float localY)
    {
        float t = Mathf.InverseLerp(bottomY, topY, localY);
        return Mathf.Lerp(bottomRadius, topRadius, t);
    }

    void Awake()
    {
        defaultPaintMass = paintMass;
    }

    public void ResetState()
    {
        SetPaintMass(defaultPaintMass);
        position = Vector3.zero;
        previousPosition = Vector3.zero;
        linearVelocity = Vector3.zero;
        previousLinearVelocity = Vector3.zero;
    }

    public void Integrate(Vector3 gravityAccel, float dt, float damping)
    {
        Vector3 velocity = (position - previousPosition);
        Vector3 currentPos = position;
        position = currentPos + velocity * (1f - damping) + gravityAccel * dt * dt;
        previousPosition = currentPos;
        previousLinearVelocity = linearVelocity;
        linearVelocity = (position - previousPosition) / dt;

        if (paintMass > 0f && holeEnabled)
        {
            float head = Mathf.Max(paintMass / (emptyMass + paintMass), 0.001f);
            float holeArea = holeRadius * holeRadius;
            float baseFlow = Mathf.Sqrt(head) * holeArea * paintLeakRate;
            float motion = linearVelocity.magnitude;
            float motionFactor = 1f + motion * 0.5f;
            paintMass -= baseFlow * motionFactor * dt;
            if (paintMass < 0f) paintMass = 0f;
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
            Vector3 p0 = transform.TransformPoint(new Vector3(holeLocalOffset.x + Mathf.Cos(a0) * holeRadius, bottomY, holeLocalOffset.y + Mathf.Sin(a0) * holeRadius));
            Vector3 p1 = transform.TransformPoint(new Vector3(holeLocalOffset.x + Mathf.Cos(a1) * holeRadius, bottomY, holeLocalOffset.y + Mathf.Sin(a1) * holeRadius));
            Gizmos.DrawLine(p0, p1);
        }
    }
}
