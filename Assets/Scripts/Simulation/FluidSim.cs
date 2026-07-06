using System;
using UnityEngine;
using Seb.GPUSorting;
using Unity.Mathematics;
using System.Collections.Generic;
using Seb.Helpers;

namespace Seb.Fluid.Simulation
{
	public class FluidSim : MonoBehaviour
	{
		public event Action<FluidSim> SimulationInitCompleted;

		[Header("Time Step")] public float normalTimeScale = 1;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;

		[Header("Simulation Settings")] public float gravity = -10;
		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float viscosityStrength = 0;
		[Range(0, 1)] public float collisionDamping = 0.95f;

		[HideInInspector] [Header("Foam Settings")] public bool foamActive;
        [HideInInspector] public int maxFoamParticleCount = 1000;
        [HideInInspector] public float trappedAirSpawnRate = 70;
        [HideInInspector] public float spawnRateFadeInTime = 0.5f;
        [HideInInspector] public float spawnRateFadeStartTime = 0;
        [HideInInspector] public Vector2 trappedAirVelocityMinMax = new(5, 25);
        [HideInInspector] public Vector2 foamKineticEnergyMinMax = new(15, 80);
        [HideInInspector] public float bubbleBuoyancy = 1.5f;
        [HideInInspector] public int sprayClassifyMaxNeighbours = 5;
        [HideInInspector] public int bubbleClassifyMinNeighbours = 15;
        [HideInInspector] public float bubbleScale = 0.5f;
        [HideInInspector] public float bubbleChangeScaleSpeed = 7;

		[Header("Environmental Forces")]
		public float rho_air = 1.225f;
		public float Cd_fluid = 0.47f;
		[Range(0f, 1f)] public float humidity = 0f;
		public Vector3 windDirection = Vector3.right;
		[Range(0f, 50f)] public float windStrength = 0f;

		[HideInInspector] [Header("Volumetric Render Settings")] public bool renderToTex3D;
		public int densityTextureRes;

		[Header("References")] public ComputeShader compute;
		public Spawner3D spawner;
		public BucketBody bucketBody;
		public DrawingBoard drawingBoard;

		[Header("World Bounds (fallback when particles escape bucket)")]
		public Vector3 boundsMin = new Vector3(-10, -1, -10);
		public Vector3 boundsMax = new Vector3(10, 20, 10);

		[HideInInspector] public RenderTexture DensityMap;
		public Vector3 Scale => transform.localScale;
        public int NumParticles => positionBuffer?.count ?? 0;

        // Buffers
        public ComputeBuffer foamBuffer { get; private set; }
		public ComputeBuffer foamSortTargetBuffer { get; private set; }
		public ComputeBuffer foamCountBuffer { get; private set; }
		public ComputeBuffer positionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer predictedPositionsBuffer;
		public ComputeBuffer debugBuffer { get; private set; }

		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;
		ComputeBuffer escapedThroughHoleBuffer;
		ComputeBuffer sortTarget_escapedThroughHoleBuffer;

		// Kernel IDs
		int externalForcesKernel;
		int spatialHashKernel;
		int reorderKernel;
		int reorderCopybackKernel;
		int densityKernel;
		int pressureKernel;
		int viscosityKernel;
		int updatePositionsKernel;
		int renderKernel;
		int foamUpdateKernel;
		int foamReorderCopyBackKernel;

		SpatialHash spatialHash;

		// State
		bool isPaused;
		float smoothRadiusOld;
		float simTimer;
		Spawner3D.SpawnData spawnData;
		Dictionary<ComputeBuffer, string> bufferNameLookup;
		public bool HasSpawned { get; private set; }

		void Start()
		{
			isPaused = true;
			Initialize();
		}

		void Initialize()
		{
			spawnData = spawner.GetSpawnData();
			int numParticles = spawnData.points.Length;

			CacheKernels();

			spatialHash = new SpatialHash(numParticles);
			
			// Create buffers
			positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
			foamBuffer = ComputeHelper.CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamSortTargetBuffer = ComputeHelper.CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamCountBuffer = ComputeHelper.CreateStructuredBuffer<uint>(4096);
			debugBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);

			sortTarget_positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
			escapedThroughHoleBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);
			sortTarget_escapedThroughHoleBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{
				{ positionBuffer, "Positions" },
				{ predictedPositionsBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ densityBuffer, "Densities" },
				{ spatialHash.SpatialKeys, "SpatialKeys" },
				{ spatialHash.SpatialOffsets, "SpatialOffsets" },
				{ spatialHash.SpatialIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ escapedThroughHoleBuffer, "EscapedThroughHole" },
				{ sortTarget_escapedThroughHoleBuffer, "SortTarget_EscapedThroughHole" },
				{ foamCountBuffer, "WhiteParticleCounters" },
				{ foamBuffer, "WhiteParticles" },
				{ foamSortTargetBuffer, "WhiteParticlesCompacted" },
				{ debugBuffer, "Debug" }
			};

			// External forces kernel
			ComputeHelper.SetBuffers(compute, externalForcesKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictedPositionsBuffer,
				velocityBuffer,
				escapedThroughHoleBuffer
			});

			// Spatial hash kernel
			ComputeHelper.SetBuffers(compute, spatialHashKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				predictedPositionsBuffer,
				spatialHash.SpatialIndices,
				escapedThroughHoleBuffer
			});

			// Reorder kernel
			ComputeHelper.SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				spatialHash.SpatialIndices,
				escapedThroughHoleBuffer,
				sortTarget_escapedThroughHoleBuffer
			});

			// Reorder copyback kernel
			ComputeHelper.SetBuffers(compute, reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				spatialHash.SpatialIndices,
				escapedThroughHoleBuffer,
				sortTarget_escapedThroughHoleBuffer
			});

			// Density kernel
			ComputeHelper.SetBuffers(compute, densityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				escapedThroughHoleBuffer
			});

			// Pressure kernel
			ComputeHelper.SetBuffers(compute, pressureKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				foamBuffer,
				foamCountBuffer,
				debugBuffer,
				escapedThroughHoleBuffer
			});

			// Viscosity kernel
			ComputeHelper.SetBuffers(compute, viscosityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				escapedThroughHoleBuffer
			});

			// Update positions kernel
			ComputeHelper.SetBuffers(compute, updatePositionsKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				velocityBuffer,
				predictedPositionsBuffer,
				escapedThroughHoleBuffer
			});

			// Bind drawing board texture (needed by ResolveCollisions called from UpdatePositions kernel)
			if (drawingBoard != null && drawingBoard.boardTexture != null)
			{
				compute.SetTexture(updatePositionsKernel, "_BoardTexture", drawingBoard.boardTexture);
				if (drawingBoard.paintAmountTexture != null)
				{
					compute.SetTexture(updatePositionsKernel, "_BoardPaintAmountTexture", drawingBoard.paintAmountTexture);
				}
				if (drawingBoard.paintColorTexture != null)
				{
					compute.SetTexture(updatePositionsKernel, "_BoardPaintColorTexture", drawingBoard.paintColorTexture);
				}
			}

			// Render to 3d tex kernel
			ComputeHelper.SetBuffers(compute, renderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
			});

			// Foam update kernel
			ComputeHelper.SetBuffers(compute, foamUpdateKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamCountBuffer,
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets,
				foamSortTargetBuffer,
				//debugBuffer
			});


			// Foam reorder copyback kernel
			ComputeHelper.SetBuffers(compute, foamReorderCopyBackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamSortTargetBuffer,
				foamCountBuffer,
			});

			compute.SetInt("numParticles", positionBuffer.count);
			compute.SetInt("MaxWhiteParticleCount", maxFoamParticleCount);

			UpdateSmoothingConstants();

			SimulationInitCompleted?.Invoke(this);
		}

		void CacheKernels()
		{
			externalForcesKernel = compute.FindKernel("ExternalForces");
			spatialHashKernel = compute.FindKernel("UpdateSpatialHash");
			reorderKernel = compute.FindKernel("Reorder");
			reorderCopybackKernel = compute.FindKernel("ReorderCopyBack");
			densityKernel = compute.FindKernel("CalculateDensities");
			pressureKernel = compute.FindKernel("CalculatePressureForce");
			viscosityKernel = compute.FindKernel("CalculateViscosity");
			updatePositionsKernel = compute.FindKernel("UpdatePositions");
			renderKernel = compute.FindKernel("UpdateDensityTexture");
			foamUpdateKernel = compute.FindKernel("UpdateWhiteParticles");
			foamReorderCopyBackKernel = compute.FindKernel("WhiteParticlePrepareNextFrame");
		}

		void Update()
		{
			// Run simulation
			if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * normalTimeScale, maxDeltaTime);
				RunSimulationFrame(dt);
			}
		}

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				simTimer += subStepDeltaTime;
				RunSimulationStep();
			}

			// Foam and spray particles
			if (foamActive)
			{
				Dispatch1D(foamUpdateKernel, maxFoamParticleCount);
				Dispatch1D(foamReorderCopyBackKernel, maxFoamParticleCount);
			}

			// 3D density map
			if (renderToTex3D)
			{
				UpdateDensityMap();
			}
		}

		void UpdateDensityMap()
		{
			float maxAxis = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			int w = Mathf.RoundToInt(transform.localScale.x / maxAxis * densityTextureRes);
			int h = Mathf.RoundToInt(transform.localScale.y / maxAxis * densityTextureRes);
			int d = Mathf.RoundToInt(transform.localScale.z / maxAxis * densityTextureRes);
			ComputeHelper.CreateRenderTexture3D(ref DensityMap, w, h, d, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, TextureWrapMode.Clamp);
			//Debug.Log(w + " " + h + "  " + d);
			compute.SetTexture(renderKernel, "DensityMap", DensityMap);
			compute.SetInts("densityMapSize", DensityMap.width, DensityMap.height, DensityMap.volumeDepth);
			Dispatch3D(renderKernel, DensityMap.width, DensityMap.height, DensityMap.volumeDepth);
		}

		void RunSimulationStep()
		{
			Dispatch1D(externalForcesKernel, positionBuffer.count);

			Dispatch1D(spatialHashKernel, positionBuffer.count);
			spatialHash.Run();
			
			Dispatch1D(reorderKernel, positionBuffer.count);
			Dispatch1D(reorderCopybackKernel, positionBuffer.count);

			Dispatch1D(densityKernel, positionBuffer.count);
			Dispatch1D(pressureKernel, positionBuffer.count);
			float effectiveViscosity = viscosityStrength * (1f + 0.1f * humidity);
			if (effectiveViscosity != 0)
			{
				compute.SetFloat("viscosityStrength", effectiveViscosity);
				Dispatch1D(viscosityKernel, positionBuffer.count);
				compute.SetFloat("viscosityStrength", viscosityStrength);
			}
			Dispatch1D(updatePositionsKernel, positionBuffer.count);
		}

		void Dispatch1D(int kernel, int count)
		{
			if (kernel < 0)
				return;

			int groups = Mathf.Max(1, Mathf.CeilToInt(count / 256f));
			compute.Dispatch(kernel, groups, 1, 1);
		}

		void Dispatch3D(int kernel, int width, int height, int depth)
		{
			if (kernel < 0)
				return;

			int groupsX = Mathf.Max(1, Mathf.CeilToInt(width / 8f));
			int groupsY = Mathf.Max(1, Mathf.CeilToInt(height / 8f));
			int groupsZ = Mathf.Max(1, Mathf.CeilToInt(depth / 8f));
			compute.Dispatch(kernel, groupsX, groupsY, groupsZ);
		}

		void UpdateSmoothingConstants()
		{
			float r = smoothingRadius;
			float spikyPow2 = 15 / (2 * Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3 = 15 / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyPow2Grad = 15 / (Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3Grad = 45 / (Mathf.PI * Mathf.Pow(r, 6));

			compute.SetFloat("K_SpikyPow2", spikyPow2);
			compute.SetFloat("K_SpikyPow3", spikyPow3);
			compute.SetFloat("K_SpikyPow2Grad", spikyPow2Grad);
			compute.SetFloat("K_SpikyPow3Grad", spikyPow3Grad);
		}

		void UpdateSettings(float stepDeltaTime, float frameDeltaTime)
		{
			if (smoothingRadius != smoothRadiusOld)
			{
				smoothRadiusOld = smoothingRadius;
				UpdateSmoothingConstants();
			}

			Vector3 simBoundsSize = transform.localScale;
			Vector3 simBoundsCentre = transform.position;

			compute.SetFloat("deltaTime", stepDeltaTime);
			compute.SetFloat("whiteParticleDeltaTime", frameDeltaTime);
			compute.SetFloat("simTime", simTimer);
			compute.SetFloat("gravity", gravity);
			compute.SetFloat("collisionDamping", collisionDamping);
			compute.SetFloat("smoothingRadius", smoothingRadius);
			compute.SetFloat("targetDensity", targetDensity);
			compute.SetFloat("pressureMultiplier", pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
			compute.SetFloat("viscosityStrength", viscosityStrength);
			compute.SetVector("boundsSize", simBoundsSize);
			compute.SetVector("centre", simBoundsCentre);
			compute.SetVector("_BoundsMin", boundsMin);
			compute.SetVector("_BoundsMax", boundsMax);

			compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
			compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

			// Foam settings
			float fadeInT = (spawnRateFadeInTime <= 0) ? 1 : Mathf.Clamp01((simTimer - spawnRateFadeStartTime) / spawnRateFadeInTime);
			compute.SetVector("trappedAirParams", new Vector3(trappedAirSpawnRate * fadeInT * fadeInT, trappedAirVelocityMinMax.x, trappedAirVelocityMinMax.y));
			compute.SetVector("kineticEnergyParams", foamKineticEnergyMinMax);
			compute.SetFloat("bubbleBuoyancy", bubbleBuoyancy);
			compute.SetInt("sprayClassifyMaxNeighbours", sprayClassifyMaxNeighbours);
			compute.SetInt("bubbleClassifyMinNeighbours", bubbleClassifyMinNeighbours);
			compute.SetFloat("bubbleScaleChangeSpeed", bubbleChangeScaleSpeed);
			compute.SetFloat("bubbleScale", bubbleScale);

			// Bucket collision parameters
			if (bucketBody != null)
			{
				Vector3 scale = bucketBody.transform.lossyScale;
				compute.SetMatrix("_BucketWorldToLocal", bucketBody.transform.worldToLocalMatrix);
				compute.SetMatrix("_BucketLocalToWorld", bucketBody.transform.localToWorldMatrix);
				compute.SetVector("_BucketLinearVelocity", bucketBody.linearVelocity);
				compute.SetVector("_BucketAngularVelocity", bucketBody.angularVelocity);
				compute.SetFloat("_BucketBottomY", bucketBody.bottomY * scale.y);
				compute.SetFloat("_BucketTopY", bucketBody.topY * scale.y);
				compute.SetFloat("_BucketBottomRadius", bucketBody.bottomRadius * scale.x);
				compute.SetFloat("_BucketTopRadius", bucketBody.topRadius * scale.x);
				compute.SetFloat("_HoleRadius", bucketBody.holeEnabled ? bucketBody.holeRadius * scale.x : 0);
				compute.SetInt("_HoleEnabled", bucketBody.holeEnabled ? 1 : 0);
			}

			// Drawing board parameters
			if (drawingBoard != null && drawingBoard.boardTexture != null)
			{
				compute.SetInt("_HasBoard", 1);
				compute.SetFloat("_BoardTopY", drawingBoard.GetTopSurfaceY());
				Vector2 half = drawingBoard.GetHalfSizeXZ();
				compute.SetVector("_BoardCenter", drawingBoard.transform.position);
				compute.SetVector("_BoardHalfSize", new Vector4(half.x, 0f, half.y, 0f));
				compute.SetInt("_BoardTexRes", drawingBoard.textureResolution);
				if (drawingBoard.paintAmountTexture != null)
				{
					compute.SetTexture(updatePositionsKernel, "_BoardPaintAmountTexture", drawingBoard.paintAmountTexture);
				}
				if (drawingBoard.paintColorTexture != null)
				{
					compute.SetTexture(updatePositionsKernel, "_BoardPaintColorTexture", drawingBoard.paintColorTexture);
				}

				SurfaceMaterial mat = drawingBoard.GetComponent<SurfaceMaterial>();
				if (mat != null)
				{
					compute.SetFloat("_BoardFriction", mat.friction);
					compute.SetFloat("_BoardStickiness", mat.stickiness);
					compute.SetFloat("_BoardAbsorption", mat.absorption);
				}
				else
				{
					compute.SetFloat("_BoardFriction", 0.55f);
					compute.SetFloat("_BoardStickiness", 0.92f);
					compute.SetFloat("_BoardAbsorption", 0.95f);
				}

				// Use selected paint color from GameManager, or fall back to white
				Color paintCol = (GameManager.Instance != null) ? GameManager.Instance.selectedColor : Color.white;
				compute.SetVector("_PaintColor", paintCol);
			}
			else
			{
				compute.SetInt("_HasBoard", 0);
			}

			// Particle radius (derive from smoothing radius)
			compute.SetFloat("_ParticleRadius", smoothingRadius * 0.5f);

			// Environmental forces
			compute.SetFloat("rho_air", rho_air);
			compute.SetFloat("Cd_fluid", Cd_fluid);
			compute.SetFloat("humidity", humidity);
			compute.SetVector("windVector", windDirection.normalized * windStrength);
		}

		void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictedPositionsBuffer.SetData(spawnData.points);
			velocityBuffer.SetData(spawnData.velocities);

			foamBuffer.SetData(new FoamParticle[foamBuffer.count]);

			debugBuffer.SetData(new float3[debugBuffer.count]);
			foamCountBuffer.SetData(new uint[foamCountBuffer.count]);
			escapedThroughHoleBuffer.SetData(new uint[escapedThroughHoleBuffer.count]);
			simTimer = 0;
		}

		public void Spawn()
		{
			spawnData = spawner.GetSpawnData();
			SetInitialBufferData(spawnData);
			HasSpawned = true;
			isPaused = false;
		}

        public void ResetSimulation()
        {
            SetInitialBufferData(spawnData);
            if (drawingBoard != null)
                drawingBoard.ClearBoard();
            if (renderToTex3D)
            {
                RunSimulationFrame(0);
            }
        }

        public void FullReset()
        {
            spawnData = spawner.GetSpawnData();
            SetInitialBufferData(spawnData);
            if (drawingBoard != null)
                drawingBoard.ClearBoard();
            if (renderToTex3D)
            {
                RunSimulationFrame(0);
            }
            HasSpawned = false;
            isPaused = true;
            simTimer = 0;
        }

		//public void FullReset()
		//{
		//	isPaused = true;
		//	HasSpawned = false;
		//	simTimer = 0;

		//	ReleaseResources();
		//	Initialize();

		//	if (drawingBoard != null)
		//		drawingBoard.ClearBoard();
		//}

		public void ReleaseResources()
		{
			if (bufferNameLookup != null)
			{
				foreach (var kvp in bufferNameLookup)
				{
					ComputeHelper.Release(kvp.Key);
				}
				bufferNameLookup = null;
			}

			if (spatialHash != null)
			{
				spatialHash.Release();
				spatialHash = null;
			}
		}

		void OnDestroy()
		{
			ReleaseResources();
		}


		public struct FoamParticle
		{
			public float3 position;
			public float3 velocity;
			public float lifetime;
			public float scale;
		}

		void OnDrawGizmos()
		{
			// Draw cylinder bounds
			var m = Gizmos.matrix;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0, 1, 0, 0.5f);
			DrawWireCylinder(0.5f, 0.5f, 24);
			Gizmos.matrix = m;
		}

		void DrawWireCylinder(float radius, float halfHeight, int segments)
		{
			float angleStep = 2f * Mathf.PI / segments;

			// Top and bottom circles
			for (int i = 0; i < segments; i++)
			{
				float a1 = i * angleStep;
				float a2 = (i + 1) * angleStep;
				Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, halfHeight, Mathf.Sin(a1) * radius);
				Vector3 p2 = new Vector3(Mathf.Cos(a2) * radius, halfHeight, Mathf.Sin(a2) * radius);
				Gizmos.DrawLine(p1, p2);

				p1.y = -halfHeight;
				p2.y = -halfHeight;
				Gizmos.DrawLine(p1, p2);
			}

			// Vertical lines (every 4th segment)
			for (int i = 0; i < segments; i += 4)
			{
				float a = i * angleStep;
				Vector3 top = new Vector3(Mathf.Cos(a) * radius, halfHeight, Mathf.Sin(a) * radius);
				Vector3 bottom = new Vector3(Mathf.Cos(a) * radius, -halfHeight, Mathf.Sin(a) * radius);
				Gizmos.DrawLine(top, bottom);
			}
		}
	}
}
