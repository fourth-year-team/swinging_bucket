using UnityEngine;

public class BucketObstacle : MonoBehaviour
{
    [Header("Measured in bucket local space")]
    public float bottomY = 0.39f;
    public float topY = 5.25f;
    public float bottomRadius = 1.57f;
    public float topRadius = 1.9f;
    public float holeRadius = 0.1f;

    [HideInInspector] public Vector3 linearVelocity;
    [HideInInspector] public Vector3 angularVelocity;
    [HideInInspector] public Matrix4x4 prevWorldToLocalMatrix;

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

        // velocity from previous frame → current frame
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
}
