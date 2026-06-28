using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class SPHSimulation : MonoBehaviour
{
    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Rendering")]
    public Material particleMaterial;
    public float particleRadius = 0.05f;

    [Header("Spawn Grid (x * y * z = particle count)")]
    public int spawnX = 16;
    public int spawnY = 16;
    public int spawnZ = 16;

    [Header("SPH Parameters")]
    public float smoothingRadius = 0.13f;  // 1.3 * spacing (~50 neighbors)
    public float restDensity = 1000f;
    public float stiffness = 50f;  // Lowered to match old version
    public float viscosity = 0.01f;  // Light viscosity - prevents clumping while maintaining flow
    public float cohesion = 0.0003f; // Lowered to match old version
    public float gravity = -9.8f;
    public float damping = 0.995f;

    [Header("Bounds")]
    public Vector3 boundsMin = new Vector3(-10f, -0f, -10f);
    public Vector3 boundsMax = new Vector3(10f, 20f, 10f);

    [Header("Bucket Obstacle")]
    public BucketObstacle bucket;
    Matrix4x4 prevWorldToLocal;

    [Header("Drawing Board")]
    public DrawingBoard drawingBoard;

    private ComputeBuffer particleBuffer, sortedIndices, cellHash, cellStart, cellEnd, argsBuffer;
    private int numParticles, sortSize, tableSize;
    private float bucketSweep;

    private int kClearGrid, kHash, kBitonicSort, kCellBounds, kForcesAndIntegrate, kCollideObstacles;

    [StructLayout(LayoutKind.Sequential)]
    struct Particle
    {
        public Vector4 posAndDensity;
        public Vector4 velAndPressure;
    }

    void Start()
    {
        if (computeShader == null)
        {
            Debug.LogError("SPHSimulation: computeShader is not assigned.");
            enabled = false;
            return;
        }
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("Compute shaders not supported on this device.");
            enabled = false;
            return;
        }

        numParticles = spawnX * spawnY * spawnZ;

        tableSize = 1048576;  // 2^20
        sortSize = Mathf.NextPowerOfTwo(numParticles);

        CacheKernels();
        CreateBuffers();
        SpawnParticles();
        SetupArgs();
    }

    void CacheKernels()
    {
        kClearGrid = computeShader.FindKernel("CSClearGrid");
        kHash = computeShader.FindKernel("CSHash");
        kBitonicSort = computeShader.FindKernel("CSBitonicSort");
        kCellBounds = computeShader.FindKernel("CSCellBounds");
        kForcesAndIntegrate = computeShader.FindKernel("CSForcesAndIntegrate");
        kCollideObstacles = computeShader.FindKernel("CSCollideObstacles");
    }

    void CreateBuffers()
    {
        particleBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf<Particle>());
        sortedIndices = new ComputeBuffer(sortSize, sizeof(uint));
        cellHash = new ComputeBuffer(sortSize, sizeof(uint));
        cellStart = new ComputeBuffer(tableSize, sizeof(int));
        cellEnd = new ComputeBuffer(tableSize, sizeof(int));
        argsBuffer = new ComputeBuffer(1, 4 * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    void SpawnParticles()
    {
        Particle[] data = new Particle[numParticles];
        float spacing = particleRadius * 1.2f;

        Vector3 bMin = boundsMin + Vector3.one * spacing;
        Vector3 bMax = boundsMax - Vector3.one * spacing;
        Vector3 boundsCenter = (bMin + bMax) * 0.5f;
        Vector3 totalSize = new Vector3(
            (spawnX - 1) * spacing,
            (spawnY - 1) * spacing,
            (spawnZ - 1) * spacing);
        Vector3 spawnOffset = boundsCenter - totalSize * 0.5f;

        int idx = 0;
        for (int y = 0; y < spawnY; y++)
            for (int x = 0; x < spawnX; x++)
                for (int z = 0; z < spawnZ; z++)
                {
                    Vector3 p = spawnOffset + new Vector3(x * spacing, y * spacing, z * spacing);
                    data[idx].posAndDensity = new Vector4(p.x, p.y, p.z, 0);
                    data[idx].velAndPressure = Vector4.zero;
                    idx++;
                }
        particleBuffer.SetData(data);
    }
    void SetupArgs()
    {
        argsBuffer.SetData(new uint[]
        {
            6,                     // vertex count per instance (2 triangles)
            (uint)numParticles,    // instance count
            0,                     // start vertex
            0                      // start instance
        });
    }

    void FixedUpdate()
    {
        float dt = Mathf.Min(Time.fixedDeltaTime, 0.01f);
        SetParams(dt);
        SetBucketParams();
        DispatchAll();
    }

    void SetParams(float dt)
    {
        computeShader.SetInt("_NumParticles", numParticles);
        computeShader.SetInt("_SortSize", sortSize);

        computeShader.SetFloat("_CellSize", particleRadius * 2f);
        computeShader.SetFloat("_Gravity", gravity);
        computeShader.SetFloat("_DeltaTime", dt);
        computeShader.SetFloat("_Damping", damping);
        computeShader.SetFloat("_ParticleRadius", particleRadius);
        computeShader.SetFloat("_Viscosity", viscosity);
        computeShader.SetFloat("_Cohesion", cohesion);
        computeShader.SetFloat("_PressureStrength", 50f);
        computeShader.SetVector("_BoundsMin", boundsMin);
        computeShader.SetVector("_BoundsMax", boundsMax);

        // Drawing board parameters
        if (drawingBoard != null && drawingBoard.boardTexture != null)
        {
            computeShader.SetInt("_HasBoard", 1);
            computeShader.SetFloat("_BoardTopY", drawingBoard.GetTopSurfaceY());
            Vector2 half = drawingBoard.GetHalfSizeXZ();
            computeShader.SetVector("_BoardCenter", drawingBoard.transform.position);
            computeShader.SetVector("_BoardHalfSize", new Vector4(half.x, 0f, half.y, 0f));
            computeShader.SetInt("_BoardTexRes", drawingBoard.textureResolution);

            SurfaceMaterial mat = drawingBoard.GetComponent<SurfaceMaterial>();
            if (mat != null)
            {
                computeShader.SetFloat("_BoardFriction", mat.friction);
                computeShader.SetFloat("_BoardStickiness", mat.stickiness);
                computeShader.SetFloat("_BoardAbsorption", mat.absorption);
            }
            else
            {
                computeShader.SetFloat("_BoardFriction", 0.55f);
                computeShader.SetFloat("_BoardStickiness", 0.92f);
                computeShader.SetFloat("_BoardAbsorption", 0.95f);
            }
        }
        else
        {
            computeShader.SetInt("_HasBoard", 0);
        }
    }

    void SetBucketParams()
    {
        if (bucket == null) return;

        computeShader.SetMatrix("_PrevBucketWorldToLocal", bucket.prevWorldToLocalMatrix);
        computeShader.SetMatrix("_BucketWorldToLocal", bucket.transform.worldToLocalMatrix);
        computeShader.SetMatrix("_BucketLocalToWorld", bucket.transform.localToWorldMatrix);
        computeShader.SetVector("_BucketLinearVelocity", bucket.linearVelocity);
        computeShader.SetVector("_BucketAngularVelocity", bucket.angularVelocity);

        bucketSweep = bucket.linearVelocity.magnitude * Time.fixedDeltaTime; 
        computeShader.SetFloat("_BucketSweep", bucketSweep);
        
        computeShader.SetFloat("_BucketBottomRadius", bucket.bottomRadius * bucket.transform.lossyScale.x);
        computeShader.SetFloat("_BucketTopRadius", bucket.topRadius * bucket.transform.lossyScale.x);
        computeShader.SetFloat("_BucketBottomY", bucket.bottomY * bucket.transform.lossyScale.y);
        computeShader.SetFloat("_BucketTopY", bucket.topY * bucket.transform.lossyScale.y);
    }

    void Bind(int kernel)
    {
        computeShader.SetBuffer(kernel, "_Particles", particleBuffer);
        computeShader.SetBuffer(kernel, "_SortedIndices", sortedIndices);
        computeShader.SetBuffer(kernel, "_CellHash", cellHash);
        computeShader.SetBuffer(kernel, "_CellStart", cellStart);
        computeShader.SetBuffer(kernel, "_CellEnd", cellEnd);
    }

    void DispatchAll()
    {
        int t256 = Mathf.Max(1, Mathf.CeilToInt(numParticles / 256f));
        int tTbl = Mathf.Max(1, Mathf.CeilToInt(tableSize / 256f));
        int sortGroups = Mathf.Max(1, Mathf.CeilToInt(sortSize / 512f));

        Bind(kClearGrid);
        computeShader.Dispatch(kClearGrid, tTbl, 1, 1);

        Bind(kHash);
        computeShader.Dispatch(kHash, t256, 1, 1);

        computeShader.SetBuffer(kBitonicSort, "_SortedIndices", sortedIndices);
        computeShader.SetBuffer(kBitonicSort, "_CellHash", cellHash);

        for (int k = 2; k <= sortSize; k <<= 1)
        {
            for (int j = k >> 1; j > 0; j >>= 1)
            {
                computeShader.SetInt("_BitonicK", k);
                computeShader.SetInt("_BitonicJ", j);
                computeShader.Dispatch(kBitonicSort, sortGroups, 1, 1);
            }
        }

        Bind(kCellBounds);
        computeShader.Dispatch(kCellBounds, t256, 1, 1);

        Bind(kForcesAndIntegrate);
        computeShader.Dispatch(kForcesAndIntegrate, t256, 1, 1);

        Bind(kCollideObstacles);
        if (drawingBoard != null && drawingBoard.boardTexture != null)
        {
            computeShader.SetTexture(kCollideObstacles, "_BoardTexture", drawingBoard.boardTexture);
        }
        computeShader.Dispatch(kCollideObstacles, t256, 1, 1);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        DrawParticles();
    }

    //void OnRenderObject()
    //{
    //    DrawParticles();
    //}

    void DrawParticles()
    {
        if (particleMaterial == null || particleBuffer == null) return;

        particleMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
        particleMaterial.SetFloat("_ParticleSize", particleRadius * 2f);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        Graphics.DrawProceduralIndirect(particleMaterial, bounds, MeshTopology.Triangles, argsBuffer);
    }

    void OnDestroy()
    {
        if (particleBuffer != null) particleBuffer.Release();
        if (sortedIndices != null) sortedIndices.Release();
        if (cellHash != null) cellHash.Release();
        if (cellStart != null) cellStart.Release();
        if (cellEnd != null) cellEnd.Release();
        if (argsBuffer != null) argsBuffer.Release();
    }
}
