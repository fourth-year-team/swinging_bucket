using Seb.Helpers;
using UnityEngine;
using Seb.Fluid.Simulation;

namespace Seb.Fluid.Rendering
{

	public class ParticleDisplay3D : MonoBehaviour
	{
		public enum DisplayMode
		{
			None,
			Shaded3D,
			Billboard
		}

		[Header("Settings")] public DisplayMode mode;
		public float scale;
		public Gradient colourMap;
		public int gradientResolution;
		public float velocityDisplayMax;
		public int meshResolution;

		[Header("References")] public FluidSim sim;
		public Shader shaderShaded;
		public Shader shaderBillboard;

		Mesh mesh;
		Material mat;
		ComputeBuffer argsBuffer;
		Texture2D gradientTexture;
		DisplayMode modeOld = (DisplayMode)(-1);
		Shader shaderOld;
		int meshResolutionOld = -1;
		bool needsUpdate;

		void Update()
		{
			if (Input.GetKeyDown(KeyCode.Alpha1)) mode = DisplayMode.None;
			if (Input.GetKeyDown(KeyCode.Alpha2)) mode = DisplayMode.Shaded3D;
			if (Input.GetKeyDown(KeyCode.Alpha3)) mode = DisplayMode.Billboard;
		}

		void LateUpdate()
		{
			UpdateSettings();

			if (mode != DisplayMode.None && mesh != null && mat != null && argsBuffer != null)
			{
				Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
				Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
			}
		}

		void UpdateSettings()
		{
			Shader activeShader = mode == DisplayMode.Shaded3D ? shaderShaded : shaderBillboard;
			if (modeOld != mode || shaderOld != activeShader || meshResolutionOld != meshResolution)
			{
				modeOld = mode;
				shaderOld = activeShader;
				meshResolutionOld = meshResolution;
				ComputeHelper.Release(argsBuffer);
				argsBuffer = null;
				DestroyMaterial();

				if (mode != DisplayMode.None)
				{
					if (mode == DisplayMode.Billboard) mesh = QuadGenerator.GenerateQuadMesh();
					else mesh = SphereGenerator.GenerateSphereMesh(meshResolution);
					ComputeHelper.CreateArgsBuffer(ref argsBuffer, mesh, sim.positionBuffer.count);

					mat = new Material(activeShader);
					mat.enableInstancing = true;


					mat.SetBuffer("Positions", sim.positionBuffer);
					mat.SetBuffer("Velocities", sim.velocityBuffer);
					mat.SetBuffer("DebugBuffer", sim.debugBuffer);
				}
			}

			if (mat != null)
			{
				if (needsUpdate)
				{
					needsUpdate = false;
					TextureFromGradient(ref gradientTexture, gradientResolution, colourMap);
					mat.SetTexture("ColourMap", gradientTexture);
				}

				mat.SetFloat("scale", scale * 0.01f);
				mat.SetFloat("velocityMax", velocityDisplayMax);
				mat.SetColor("_ParticleColor", GameManager.Instance != null ? GameManager.Instance.selectedColor : Color.white);

				Vector3 s = transform.localScale;
				transform.localScale = Vector3.one;
				var localToWorld = transform.localToWorldMatrix;
				transform.localScale = s;

				mat.SetMatrix("localToWorld", localToWorld);
			}
		}

		public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
		{
			if (texture == null)
			{
				texture = new Texture2D(width, 1);
			}
			else if (texture.width != width)
			{
				texture.Reinitialize(width, 1);
			}

			if (gradient == null)
			{
				gradient = new Gradient();
				gradient.SetKeys(
					new GradientColorKey[] { new(Color.black, 0), new(Color.black, 1) },
					new GradientAlphaKey[] { new(1, 0), new(1, 1) }
				);
			}

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = filterMode;

			Color[] cols = new Color[width];
			for (int i = 0; i < cols.Length; i++)
			{
				float t = i / (cols.Length - 1f);
				cols[i] = gradient.Evaluate(t);
			}

			texture.SetPixels(cols);
			texture.Apply();
		}

		private void OnValidate()
		{
			needsUpdate = true;
		}

		void OnDestroy()
		{
			ComputeHelper.Release(argsBuffer);
			DestroyMaterial();
		}

		void DestroyMaterial()
		{
			if (mat == null) return;
			if (Application.isPlaying) Destroy(mat);
			else DestroyImmediate(mat);
			mat = null;
		}
	}
}
