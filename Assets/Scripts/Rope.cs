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

    public float theta;
    public float phi;

    [Header("Rope Setup")]
    public Transform anchor;
    public Transform bucketEnd;
    public int segmentCount = 20;
    public float ropeLength = 5f;
    public float ropeMass = 10f;
    public int constraintIterations = 40;
    public float gravity = -9.81f;
    public float damping = 0.02f;

    [Header("Twist")]
    public bool twistEnabled;
    public float twistSpeed = 360f;
    public float torsionalStiffness = 8f;
    public float torsionalDamping = 0.95f;
    public float twistAngle;
    [HideInInspector] public float twistVelocity;

    [Header("Bucket")]
    public BucketBody bucketBody;
    public Transform bucketVisual;
    public float bucketMass = 10f;
    public float paintMass = 40f;
    public float totalMass = 0f;

    private float[] particleMass;

    [Header("Environmental Forces")]
    public float rho_air = 1.225f;
    public float Cd = 1.0f;
    [Range(0f, 1f)] public float humidity = 0f;
    public float pivotFriction = 0.01f;
    public Vector3 windDirection = Vector3.right;
    [Range(0f, 50f)] public float windStrength = 0f;
    private Vector3 windVector;
    private float paintLeakRateOriginal;

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

    bool isHeld = true;

    void OnEnable()
    {
        Initialize();
        GameManager.OnGameStarted += ReleaseHold;
    }

    void OnDisable()
    {
        GameManager.OnGameStarted -= ReleaseHold;
    }

    void ReleaseHold()
    {
        isHeld = false;
    }

    void Start()
    {
        ResetSimulation();
    }

    public void Restart()
    {
        theta = startTheta * Mathf.Deg2Rad;
        phi = startPhi * Mathf.Deg2Rad;
        twistAngle = 0f;
        twistVelocity = 0f;

        if (currentPos == null || currentPos.Length != segmentCount)
        {
            Initialize();
            return;
        }

        ApplyPoseFromAngles(true);

        if (bucketVisual != null)
            bucketVisual.position = bucketBody.position;
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

        if (bucketBody != null) paintLeakRateOriginal = bucketBody.paintLeakRate;

        ApplyPoseFromAngles(true);

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

		if (mesh == null)
		{
			meshFilter = GetComponent<MeshFilter>();
			mesh = new Mesh();
			mesh.name = "RopeMesh";
			mesh.MarkDynamic();
			meshFilter.sharedMesh = mesh;
		}

		int vertCount = segmentCount * radialSegments;
		vertices = new Vector3[vertCount];
		uvs = new Vector2[vertCount];
		triangles = new int[(segmentCount - 1) * radialSegments * 6];

		BuildTriangles();
		BuildUVs();
	}

	public void ResetSimulation()
	{
		if (anchor == null || bucketBody == null) return;

		// Straight down from anchor (theta=0, phi=0), not startTheta/startPhi
		Vector3 bucketTarget = anchor.position + new Vector3(0, -ropeLength, 0);
		Vector3 dir = (bucketTarget - anchor.position).normalized;
		float segLen = ropeLength / (segmentCount - 1);

		// (Re)initialize rope arrays
		if (currentPos == null || currentPos.Length != segmentCount)
		{
			currentPos = new Vector3[segmentCount];
			previousPos = new Vector3[segmentCount];
			isLocked = new bool[segmentCount];
			particleMass = new float[segmentCount];
		}

		float segmentMass = ropeMass / segmentCount;
		for (int i = 0; i < segmentCount; i++)
			particleMass[i] = segmentMass;
		totalMass = bucketMass + paintMass;
		particleMass[segmentCount - 1] += totalMass;

		for (int i = 0; i < segmentCount; i++)
		{
			Vector3 pos = anchor.position + dir * segLen * i;
			currentPos[i] = pos;
			previousPos[i] = pos;
		}

		isLocked[0] = true;

		// Reset bucket body to straight-down position
		bucketBody.transform.position = bucketTarget;
		bucketBody.position = bucketTarget;
		bucketBody.previousPosition = bucketTarget;
		bucketBody.emptyMass = bucketMass;
		bucketBody.paintMass = paintMass;
		bucketBody.mass = bucketMass + paintMass;
		bucketBody.theta = 0;
		bucketBody.phi = 0;

		// Reset twist
		twistAngle = 0;
		twistVelocity = 0;

		// Rebuild rope visual mesh at straight-down position
		if (mesh == null)
		{
			meshFilter = GetComponent<MeshFilter>();
			mesh = new Mesh();
			mesh.name = "RopeMesh";
			mesh.MarkDynamic();
			meshFilter.sharedMesh = mesh;
		}

		int vertCount = segmentCount * radialSegments;
		vertices = new Vector3[vertCount];
		uvs = new Vector2[vertCount];
		triangles = new int[(segmentCount - 1) * radialSegments * 6];
		BuildTriangles();
		BuildUVs();
		UpdateMesh();

		// Snap bucket visual to match
		if (bucketVisual != null && bucketEnd != null && segmentCount >= 2)
		{
			Vector3 hookOffset = bucketVisual.position - bucketEnd.position;
			bucketVisual.position = bucketBody.position + hookOffset;

			Vector3 ropeDir = (currentPos[segmentCount - 1] - currentPos[segmentCount - 2]).normalized;
			Quaternion ropeRot = Quaternion.FromToRotation(Vector3.up, -ropeDir);
			bucketVisual.rotation = ropeRot;
		}

		// Reset paint leak rate
		if (bucketBody != null)
			bucketBody.paintLeakRate = paintLeakRateOriginal * (1f + 0.05f * humidity);

		// Update HUD-facing fields
		theta = 0;
		phi = 0;
		isHeld = true;
	}

    public void SetThetaPhiDegrees(float thetaDegrees, float phiDegrees)
    {
        startTheta = thetaDegrees;
        startPhi = phiDegrees;
        theta = thetaDegrees * Mathf.Deg2Rad;
        phi = phiDegrees * Mathf.Deg2Rad;

        if (anchor == null || bucketBody == null || currentPos == null || previousPos == null)
            return;

        ApplyPoseFromAngles(true);

        // Update rope and bucket visuals immediately (not waiting for next FixedUpdate)
        if (bucketVisual != null && bucketEnd != null && segmentCount >= 2)
        {
            Vector3 hookOffset = bucketVisual.position - bucketEnd.position;
            bucketVisual.position = bucketBody.position + hookOffset;

            Vector3 ropeDir = (currentPos[segmentCount - 1] - currentPos[segmentCount - 2]).normalized;
            Quaternion ropeRot = Quaternion.FromToRotation(Vector3.up, -ropeDir);
            bucketVisual.rotation = ropeRot;
        }

        if (mesh != null)
            UpdateMesh();
    }

    void ApplyPoseFromAngles(bool resetVelocities)
    {
        if (anchor == null || bucketBody == null || currentPos == null || previousPos == null)
            return;

        Vector3 bucketTarget = GetBucketTargetFromAngles(theta, phi);
        Vector3 dir = (bucketTarget - anchor.position).normalized;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 pos = anchor.position + dir * segmentLength * i;
            currentPos[i] = pos;
            previousPos[i] = resetVelocities ? pos : previousPos[i];
        }

        bucketBody.transform.position = bucketTarget;
        bucketBody.position = bucketTarget;
        bucketBody.previousPosition = resetVelocities ? bucketTarget : bucketBody.previousPosition;
        bucketBody.emptyMass = bucketMass;
        bucketBody.paintMass = paintMass;
        bucketBody.mass = bucketMass + paintMass;
        bucketBody.theta = theta;
        bucketBody.phi = phi;
    }

    Vector3 GetBucketTargetFromAngles(float thetaRadians, float phiRadians)
    {
        return anchor.position + new Vector3(
            ropeLength * Mathf.Sin(thetaRadians) * Mathf.Cos(phiRadians),
            -ropeLength * Mathf.Cos(thetaRadians),
            ropeLength * Mathf.Sin(thetaRadians) * Mathf.Sin(phiRadians));
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        Vector3 hookOffset = bucketVisual.position - bucketEnd.position;

        if (!isHeld)
        {
            if (currentPos == null || currentPos.Length != segmentCount)
            {
                Initialize();
                if (currentPos == null) return;
            }

            Simulate();

            for (int i = 0; i < constraintIterations; i++)
                ApplyConstraints();
        }

        if (bucketBody == null) return;

        if (bucketVisual != null)
        {
            bucketVisual.position = bucketBody.position + hookOffset;
        }

        // Update real-time theta/phi from rope geometry
        Vector3 pendulumVec = currentPos[segmentCount - 1] - currentPos[0];
        theta = Mathf.Atan2(new Vector2(pendulumVec.x, pendulumVec.z).magnitude, -pendulumVec.y);
        phi = Mathf.Atan2(pendulumVec.z, pendulumVec.x);

        // Torsional twist — wind up when enabled, spring back when released
        float dtFixed = Time.fixedDeltaTime;
        if (twistEnabled)
        {
            twistAngle += twistSpeed * Mathf.Deg2Rad * dtFixed;
        }
        else
        {
            float torque = -torsionalStiffness * twistAngle;
            twistVelocity += torque * dtFixed;
            twistVelocity *= torsionalDamping;
            twistAngle += twistVelocity * dtFixed;
            if (Mathf.Abs(twistAngle) < 0.001f && Mathf.Abs(twistVelocity) < 0.001f)
            {
                twistAngle = 0f;
                twistVelocity = 0f;
            }
        }

        // Apply twist to bucket visual
        if (bucketVisual != null)
        {
            Vector3 ropeDir = (currentPos[segmentCount - 1] - currentPos[segmentCount - 2]).normalized;
            Quaternion ropeRot = Quaternion.FromToRotation(Vector3.up, -ropeDir);
            Quaternion twistRot = Quaternion.AngleAxis(twistAngle * Mathf.Rad2Deg, -ropeDir);
            bucketVisual.rotation = twistRot * ropeRot;
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
        windVector = windDirection.normalized * windStrength;
        float rho_humid = rho_air * (1f + 0.015f * humidity);
        float b_total = damping * (1f + 0.1f * humidity);
        float segLen = ropeLength / segmentCount;
        float segMass_i = ropeMass / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            if (isLocked[i]) continue;

            Vector3 velocity = (currentPos[i] - previousPos[i]);
            Vector3 relVel = velocity - windVector;


            Vector3 current = currentPos[i];
            Vector3 acceleration = Vector3.up * gravity;

            // Air drag on rope segment (using relative velocity for wind)
            float segArea = ropeRadius * 2f * segLen;
            float dragForceMag = 0.5f * rho_humid * Cd * segArea * relVel.sqrMagnitude;
            Vector3 dragAccel = -relVel.normalized * (dragForceMag / segMass_i);
            acceleration += dragAccel;

            // Pivot friction on first non-locked segment
            if (i == 1)
            {
                Vector3 ropeDir = (currentPos[i] - currentPos[0]).normalized;
                Vector3 tangentialVel = velocity - Vector3.Project(velocity, ropeDir);
                float omega = tangentialVel.magnitude / (segLen * i);
                float tau_friction = -pivotFriction * (omega / (Mathf.Abs(omega) + 0.01f));
                float frictionForce = tau_friction / (segLen * i);
                Vector3 frictionAccel = -tangentialVel.normalized * (frictionForce / segMass_i);
                acceleration += frictionAccel;
            }

            currentPos[i] = current + velocity * (1f - b_total) + acceleration * dt * dt;
            previousPos[i] = current;

        }

        Vector3 gravityVec = Vector3.up * gravity;

        // Air drag on bucket (using relative velocity for wind)
        Vector3 bucketVel = bucketBody.position - bucketBody.previousPosition;
        Vector3 bucketRelVel = bucketVel - windVector;
        float bucketArea = Mathf.PI * bucketBody.bottomRadius * bucketBody.bottomRadius;
        float bucketDragMag = 0.5f * rho_humid * Cd * bucketArea * bucketRelVel.sqrMagnitude;
        Vector3 bucketDragAccel = -bucketRelVel.normalized * (bucketDragMag / bucketBody.mass);
        gravityVec += bucketDragAccel;

        // Pivot friction at bucket tip
        Vector3 ropeDir2 = (currentPos[segmentCount - 1] - currentPos[0]).normalized;
        Vector3 bucketTangentialVel = bucketVel - Vector3.Project(bucketVel, ropeDir2);
        float omegaBucket = bucketTangentialVel.magnitude / ropeLength;
        float tau_frictionBucket = -pivotFriction * (omegaBucket / (Mathf.Abs(omegaBucket) + 0.01f));
        Vector3 frictionForceBucket = -bucketTangentialVel.normalized * (tau_frictionBucket / ropeLength);
        gravityVec += frictionForceBucket / bucketBody.mass;

        // Humidity-modified paint discharge
        bucketBody.paintLeakRate = paintLeakRateOriginal * (1f + 0.05f * humidity);

        // integrate bucket
        bucketBody.Integrate(gravityVec, dt, damping);

        // Update rope-end mass as paint leaks from the bucket
        float segMass = ropeMass / segmentCount;
        particleMass[segmentCount - 1] = segMass + bucketBody.mass;
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
                float localTwist = twistAngle * seg / (segmentCount - 1);
                float angle = (float)r / radialSegments * Mathf.PI * 2f + localTwist;
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
