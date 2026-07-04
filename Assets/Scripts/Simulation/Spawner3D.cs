using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Seb.Fluid.Simulation
{
	public class Spawner3D : MonoBehaviour
	{
		public int particleSpawnDensity = 600;
		public float3 initialVel;
		public float jitterStrength;
		public bool showSpawnBounds;
		public SpawnRegion[] spawnRegions;

		[Header("Auto Spawn (inside bucket)")]
		public BucketBody bucketBody;
		public float bucketSpawnMargin = 0.3f;

		[Header("Debug Info")] public int debug_num_particles;
		public float debug_spawn_volume;


		public SpawnData GetSpawnData()
		{
			List<float3> allPoints = new();
			List<float3> allVelocities = new();

			if (bucketBody != null)
			{
				SpawnInBucket(allPoints, allVelocities);
			}
			else
			{
				foreach (SpawnRegion region in spawnRegions)
				{
					int particlesPerAxis = region.CalculateParticleCountPerAxis(particleSpawnDensity);
					(float3[] points, float3[] velocities) = SpawnCube(particlesPerAxis, region.centre, Vector3.one * region.size);
					allPoints.AddRange(points);
					allVelocities.AddRange(velocities);
				}
			}

			return new SpawnData() { points = allPoints.ToArray(), velocities = allVelocities.ToArray() };
		}

		void SpawnInBucket(List<float3> points, List<float3> velocities)
		{
			Vector3 scale = bucketBody.transform.lossyScale;

			float bottomRadius = bucketBody.bottomRadius * scale.x;
			float topRadius = bucketBody.topRadius * scale.x;
			float innerBottomY = (bucketBody.bottomY + bucketSpawnMargin) * scale.y;
			float innerTopY = (bucketBody.topY - bucketSpawnMargin) * scale.y;

			float height = innerTopY - innerBottomY;
			float narrowestRadius = Mathf.Min(bottomRadius, topRadius);
			float maxHalfSize = Mathf.Min(narrowestRadius / Mathf.Sqrt(2), height / 2);
			float cubeSize = Mathf.Max(0.1f, maxHalfSize * 2);

			float localCentreY = (bucketBody.bottomY + bucketBody.topY) / 2;
			Vector3 centre = bucketBody.transform.TransformPoint(new Vector3(0, localCentreY, 0));

			int particlesPerAxis = CalculateParticlesPerAxis(cubeSize, particleSpawnDensity);
			var (pts, vels) = SpawnCube(particlesPerAxis, centre, Vector3.one * cubeSize);
			points.AddRange(pts);
			velocities.AddRange(vels);
		}

		static int CalculateParticlesPerAxis(float cubeSize, int density)
		{
			float volume = cubeSize * cubeSize * cubeSize;
			int targetCount = (int)(volume * density);
			return Math.Max(1, (int)Math.Cbrt(targetCount));
		}

		(float3[] p, float3[] v) SpawnCube(int numPerAxis, Vector3 centre, Vector3 size)
		{
			int numPoints = numPerAxis * numPerAxis * numPerAxis;
			float3[] points = new float3[numPoints];
			float3[] velocities = new float3[numPoints];

			int i = 0;

			for (int x = 0; x < numPerAxis; x++)
			{
				for (int y = 0; y < numPerAxis; y++)
				{
					for (int z = 0; z < numPerAxis; z++)
					{
						float tx = x / (numPerAxis - 1f);
						float ty = y / (numPerAxis - 1f);
						float tz = z / (numPerAxis - 1f);

						float px = (tx - 0.5f) * size.x + centre.x;
						float py = (ty - 0.5f) * size.y + centre.y;
						float pz = (tz - 0.5f) * size.z + centre.z;
						float3 jitter = UnityEngine.Random.insideUnitSphere * jitterStrength;
						points[i] = new float3(px, py, pz) + jitter;
						velocities[i] = initialVel;
						i++;
					}
				}
			}

			return (points, velocities);
		}



		void OnValidate()
		{
			debug_spawn_volume = 0;
			debug_num_particles = 0;

			if (bucketBody != null)
			{
				Vector3 scale = bucketBody.transform.lossyScale;
				float bottomRadius = bucketBody.bottomRadius * scale.x;
				float topRadius = bucketBody.topRadius * scale.x;
				float height = ((bucketBody.topY - bucketBody.bottomY) - bucketSpawnMargin * 2) * scale.y;
				float narrowestRadius = Mathf.Min(bottomRadius, topRadius);
				float maxHalfSize = Mathf.Min(narrowestRadius / Mathf.Sqrt(2), height / 2);
				float cubeSize = Mathf.Max(0.1f, maxHalfSize * 2);
				debug_spawn_volume = cubeSize * cubeSize * cubeSize;
				debug_num_particles = CalculateParticlesPerAxis(cubeSize, particleSpawnDensity);
				debug_num_particles = debug_num_particles * debug_num_particles * debug_num_particles;
			}
			else if (spawnRegions != null)
			{
				foreach (SpawnRegion region in spawnRegions)
				{
					debug_spawn_volume += region.Volume;
					int numPerAxis = region.CalculateParticleCountPerAxis(particleSpawnDensity);
					debug_num_particles += numPerAxis * numPerAxis * numPerAxis;
				}
			}
		}

		void OnDrawGizmos()
		{
			if (showSpawnBounds && !Application.isPlaying)
			{
				if (bucketBody != null)
				{
					Vector3 scale = bucketBody.transform.lossyScale;
					float bottomRadius = bucketBody.bottomRadius * scale.x;
					float topRadius = bucketBody.topRadius * scale.x;
					float height = ((bucketBody.topY - bucketBody.bottomY) - bucketSpawnMargin * 2) * scale.y;
					float narrowestRadius = Mathf.Min(bottomRadius, topRadius);
					float maxHalfSize = Mathf.Min(narrowestRadius / Mathf.Sqrt(2), height / 2);
					float cubeSize = Mathf.Max(0.1f, maxHalfSize * 2);
					float localCentreY = (bucketBody.bottomY + bucketBody.topY) / 2;
					Vector3 centre = bucketBody.transform.TransformPoint(new Vector3(0, localCentreY, 0));
					Gizmos.color = Color.green;
					Gizmos.DrawWireCube(centre, Vector3.one * cubeSize);
				}

				foreach (SpawnRegion region in spawnRegions)
				{
					Gizmos.color = region.debugDisplayCol;
					Gizmos.DrawWireCube(region.centre, Vector3.one * region.size);
				}
			}
		}

		[System.Serializable]
		public struct SpawnRegion
		{
			public Vector3 centre;
			public float size;
			public Color debugDisplayCol;

			public float Volume => size * size * size;

			public int CalculateParticleCountPerAxis(int particleDensity)
			{
				int targetParticleCount = (int)(Volume * particleDensity);
				int particlesPerAxis = (int)Math.Cbrt(targetParticleCount);
				return particlesPerAxis;
			}
		}

		public struct SpawnData
		{
			public float3[] points;
			public float3[] velocities;
		}
	}
}