using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class SPHSimulation : MonoBehaviour
{
    [Header("Compute Shader")]
    public ComputeShader computeShader;

    [Header("Rendering")]
    public Material particleMaterial;
    public Shader particleThicknessShader;
    public float particleRadius = 0.045f;
    public float thicknessScale = 3.0f;
    public float thicknessOpacity = 0.22f;

    [Header("Paint Volume")]
    [Min(0f)] public float paintLiters = 12f;
    public float paintDensityKgPerLiter = 1.25f;
    public bool sealPaintInBucket = false;

    [Header("Spawn Grid (x * y * z = particle count)")]
    public int spawnX = 16;
    public int spawnY = 16;
    public int spawnZ = 16;

    [Header("SPH Parameters")]
    public float smoothingRadius = 0.115f;  // tighter neighbor radius for a denser paint look
    public float restDensity = 1250f;
    public float stiffness = 90f;  // stronger pressure response for a thicker body
    public float viscosity = 0.06f;  // higher internal drag to reduce watery motion
    public float cohesion = 0.0012f; // slightly stronger pull so particles stay together
    public float gravity = -9.8f;
    public float damping = 0.992f;

    [Header("Bounds")]
    public Vector3 boundsMin = new Vector3(-10f, -0f, -10f);
    public Vector3 boundsMax = new Vector3(10f, 20f, 10f);

    [Header("Bucket Obstacle")]
    public BucketBody bucket;
    Matrix4x4 prevWorldToLocal;

    [Header("Drawing Board")]
    public DrawingBoard drawingBoard;

    private ComputeBuffer particleBuffer, sortedIndices, cellHash, cellStart, cellEnd, argsBuffer;
    private int numParticles, sortSize, tableSize;
    private float bucketSweep;
    private float particleMass;
    private Material thicknessMaterial;

    private int kClearGrid, kHash, kBitonicSort, kCellBounds, kDensityPressure, kForcesAndIntegrate, kCollideObstacles;

    [StructLayout(LayoutKind.Sequential)]
    struct Particle
    {
        public Vector4 posAndDensity;
        public Vector4 velAndPressure;
    }

    private Color currentPaintColor = Color.blue;

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

        RecalculatePaintSettings();

        tableSize = 1048576;  // 2^20
        sortSize = Mathf.NextPowerOfTwo(numParticles);

        CacheKernels();
        CreateBuffers();
        SpawnParticles();
        SetupArgs();

        if (GameManager.Instance != null)
        {
            currentPaintColor = GameManager.Instance.selectedColor;
            GameManager.OnColorChanged += OnPaintColorChanged;
            GameManager.OnGameStarted += OnGameStarted;
        }

        if (particleThicknessShader == null)
            particleThicknessShader = Shader.Find("Custom/ParticleThickness");

        if (particleThicknessShader != null)
            thicknessMaterial = new Material(particleThicknessShader);

        UpdateParticleMaterialColor(currentPaintColor);
    }

    void OnValidate()
    {
        if (paintLiters < 0f) paintLiters = 0f;
        if (paintDensityKgPerLiter < 0.01f) paintDensityKgPerLiter = 0.01f;
        RecalculatePaintSettings();
    }

    void RecalculatePaintSettings()
    {
        float spacing = Mathf.Max(particleRadius * 1.2f, 0.0001f);
        numParticles = Mathf.Max(1, Mathf.RoundToInt(paintLiters * 200f));

        int cubeSide = Mathf.Max(1, Mathf.RoundToInt(Mathf.Pow(numParticles, 1f / 3f)));
        spawnX = cubeSide;
        spawnY = cubeSide;
        spawnZ = Mathf.Max(1, Mathf.CeilToInt(numParticles / (float)(spawnX * spawnY)));

        float totalPaintMass = paintLiters * paintDensityKgPerLiter;
        particleMass = totalPaintMass / numParticles;
    }

    void CacheKernels()
    {
        kClearGrid = computeShader.FindKernel("CSClearGrid");
        kHash = computeShader.FindKernel("CSHash");
        kBitonicSort = computeShader.FindKernel("CSBitonicSort");
        kCellBounds = computeShader.FindKernel("CSCellBounds");
        kDensityPressure = computeShader.FindKernel("CSComputeDensityPressure");
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

        bool spawnInBucket = bucket != null;
        Vector3 totalSize = new Vector3(
            (spawnX - 1) * spacing,
            (spawnY - 1) * spacing,
            (spawnZ - 1) * spacing);

        Vector3 spawnOffset;
        Vector3 spawnOrigin;

        if (spawnInBucket)
        {
            float bucketHeight = Mathf.Max(bucket.topY - bucket.bottomY, spacing);
            float bucketCapacity = Mathf.PI * bucketHeight * (bucket.bottomRadius * bucket.bottomRadius + bucket.bottomRadius * bucket.topRadius + bucket.topRadius * bucket.topRadius) / 3f;
            float fillRatio = Mathf.Clamp01(paintLiters / Mathf.Max(bucketCapacity, 0.0001f));

            float localBottomY = bucket.bottomY + particleRadius * 1.25f;
            float localTopY = Mathf.Min(bucket.topY - particleRadius * 1.25f, localBottomY + bucketHeight * Mathf.Max(fillRatio, 0.08f));
            Vector3 localCenter = new Vector3(0f, (localBottomY + localTopY) * 0.5f, 0f);

            Matrix4x4 spawnMatrix = bucket.initialLocalToWorldMatrix;
            spawnOrigin = spawnMatrix.MultiplyPoint3x4(localCenter);
            spawnOffset = -totalSize * 0.5f;
        }
        else
        {
            Vector3 bMin = boundsMin + Vector3.one * spacing;
            Vector3 bMax = boundsMax - Vector3.one * spacing;
            Vector3 boundsCenter = (bMin + bMax) * 0.5f;
            spawnOrigin = boundsCenter;
            spawnOffset = -totalSize * 0.5f;
        }

        int idx = 0;
        for (int y = 0; y < spawnY && idx < numParticles; y++)
        {
            float yT = spawnY > 1 ? y / (float)(spawnY - 1) : 0f;
            for (int x = 0; x < spawnX && idx < numParticles; x++)
            {
                float xT = spawnX > 1 ? x / (float)(spawnX - 1) : 0f;
                for (int z = 0; z < spawnZ && idx < numParticles; z++)
                {
                    float zT = spawnZ > 1 ? z / (float)(spawnZ - 1) : 0f;

                    float localY = Mathf.Lerp(bucket != null ? bucket.bottomY + particleRadius * 1.25f : boundsMin.y + spacing, bucket != null ? Mathf.Min(bucket.topY - particleRadius * 1.25f, bucket.bottomY + particleRadius * 1.25f + (bucket.topY - bucket.bottomY)) : boundsMax.y - spacing, yT);
                    float radiusAtY = bucket != null
                        ? Mathf.Lerp(bucket.bottomRadius, bucket.topRadius, yT) * 0.78f - particleRadius
                        : Mathf.Min(totalSize.x, totalSize.z) * 0.5f;

                    float localX = Mathf.Lerp(-radiusAtY, radiusAtY, xT);
                    float localZ = Mathf.Lerp(-radiusAtY, radiusAtY, zT);

                    Vector3 p = bucket != null
                        ? bucket.initialLocalToWorldMatrix.MultiplyPoint3x4(new Vector3(localX, localY, localZ))
                        : spawnOrigin + spawnOffset + new Vector3(x * spacing, y * spacing, z * spacing);

                    data[idx].posAndDensity = new Vector4(p.x, p.y, p.z, 0);
                    data[idx].velAndPressure = Vector4.zero;
                    idx++;
                }
            }
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
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        float dt = Mathf.Min(Time.fixedDeltaTime, 0.01f);
        SetParams(dt);
        SetBucketParams();
        DispatchAll();
    }

    void SetParams(float dt)
    {
        computeShader.SetInt("_NumParticles", numParticles);
        computeShader.SetInt("_SortSize", sortSize);

        computeShader.SetFloat("_CellSize", smoothingRadius);
        computeShader.SetFloat("_SmoothingRadius", smoothingRadius);
        computeShader.SetFloat("_Gravity", gravity);
        computeShader.SetFloat("_DeltaTime", dt);
        computeShader.SetFloat("_Damping", damping);
        computeShader.SetFloat("_ParticleRadius", particleRadius);
        computeShader.SetFloat("_Viscosity", viscosity);
        computeShader.SetFloat("_Cohesion", cohesion);
        computeShader.SetFloat("_RestDensity", restDensity);
        computeShader.SetFloat("_ParticleMass", particleMass);
        computeShader.SetFloat("_PressureStrength", stiffness);
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

        computeShader.SetVector("_PaintColor", currentPaintColor);
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
        computeShader.SetInt("_HoleEnabled", sealPaintInBucket ? 0 : (bucket.holeEnabled ? 1 : 0));
        computeShader.SetFloat("_HoleRadius", bucket.holeRadius * bucket.transform.lossyScale.x);
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

        Bind(kDensityPressure);
        computeShader.Dispatch(kDensityPressure, t256, 1, 1);

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

        if (thicknessMaterial != null)
        {
            thicknessMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
            thicknessMaterial.SetFloat("_ParticleSize", particleRadius * thicknessScale);
            thicknessMaterial.SetFloat("_ThicknessOpacity", thicknessOpacity);
            if (particleMaterial != null)
            {
                thicknessMaterial.SetColor("_ColorLow", particleMaterial.GetColor("_ColorLow"));
                thicknessMaterial.SetColor("_ColorHigh", particleMaterial.GetColor("_ColorHigh"));
                thicknessMaterial.SetFloat("_RestDensity", restDensity);
            }
            Graphics.DrawProceduralIndirect(thicknessMaterial, new Bounds(Vector3.zero, Vector3.one * 100f), MeshTopology.Triangles, argsBuffer);
        }

        particleMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
        particleMaterial.SetFloat("_ParticleSize", particleRadius * 2f);
        particleMaterial.SetFloat("_RestDensity", restDensity);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        Graphics.DrawProceduralIndirect(particleMaterial, bounds, MeshTopology.Triangles, argsBuffer);
    }

    void OnPaintColorChanged(Color color)
    {
        currentPaintColor = color;
        UpdateParticleMaterialColor(color);
    }

    void OnGameStarted()
    {
        if (drawingBoard != null)
            drawingBoard.ClearBoard();

        SpawnParticles();
    }

    void UpdateParticleMaterialColor(Color color)
    {
        if (particleMaterial == null) return;
        particleMaterial.SetColor("_ColorLow", Color.Lerp(color, Color.white, 0.35f));
        particleMaterial.SetColor("_ColorHigh", Color.Lerp(color, Color.black, 0.30f));
    }

    void OnDestroy()
    {
        GameManager.OnColorChanged -= OnPaintColorChanged;
        GameManager.OnGameStarted -= OnGameStarted;

        if (particleBuffer != null) particleBuffer.Release();
        if (sortedIndices != null) sortedIndices.Release();
        if (cellHash != null) cellHash.Release();
        if (cellStart != null) cellStart.Release();
        if (cellEnd != null) cellEnd.Release();
        if (argsBuffer != null) argsBuffer.Release();

        if (thicknessMaterial != null) Destroy(thicknessMaterial);
    }
}
