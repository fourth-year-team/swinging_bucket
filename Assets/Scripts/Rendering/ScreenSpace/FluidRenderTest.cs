using Seb.Helpers;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Seb.Fluid.Simulation;

namespace Seb.Fluid.Rendering
{
	public class FluidRenderTest : MonoBehaviour
	{
		[Header("Main Settings")] public bool useFullSizeThicknessTex;
		public Vector3 extinctionCoefficients;
		public float extinctionMultiplier;
		public float depthParticleSize;
		public float thicknessParticleScale;
		public float refractionMultiplier;
		public Vector3 testParams;

		[Header("Smoothing Settings")] public BlurType smoothType;
		public BilateralSmooth2D.BilateralFilterSettings bilateralSettings;
		public GaussSmooth.GaussianBlurSettings gaussSmoothSettings;

		[Header("Environment")] public GaussSmooth.GaussianBlurSettings shadowSmoothSettings;
		public EnvironmentSettings environmentSettings;

		[Header("Debug Settings")] public DisplayMode displayMode;
		public float depthDisplayScale;
		public float thicknessDisplayScale;

		[Header("References")] public Shader renderA;
		public Shader depthDownsampleCopyShader;
		public Shader depthShader;
		public Shader normalShader;
		public Shader thicknessShader;
		public Shader smoothThickPrepareShader;
		public FluidSim sim;
		public Camera shadowCam;
		public Light sun;

		Mesh quadMesh;
		Material matDepth;
		Material matThickness;
		Material matNormal;
		Material matComposite;
		Material smoothPrepareMat;
		Material depthDownsampleCopyMat;
		ComputeBuffer argsBuffer;

		RenderTexture compRt;
		RenderTexture depthRt;
		RenderTexture normalRt;
		RenderTexture shadowRt;
		RenderTexture thicknessRt;
		RenderTexture sceneRt;

		Bilateral1D bilateral1D = new();
		BilateralSmooth2D bilateral2D = new();
		GaussSmooth gaussSmooth = new();

		Camera activeCamera;
		GameObject shadowCamGO;
		bool initialized;

		void OnEnable()
		{
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
		}

		void OnDisable()
		{
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
		}

		void Start()
		{
			if (sim == null) sim = FindObjectOfType<FluidSim>();
			if (sun == null) sun = FindObjectOfType<Light>();

			// Always create a dedicated hidden shadow camera (don't reuse a scene camera)
			if (shadowCam != null && shadowCam.gameObject.hideFlags != HideFlags.HideAndDontSave)
			{
				shadowCam = null;
			}
			if (shadowCam == null)
			{
				shadowCamGO = new GameObject("Fluid Shadow Camera");
				shadowCamGO.hideFlags = HideFlags.HideAndDontSave;
				shadowCam = shadowCamGO.AddComponent<Camera>();
				shadowCam.enabled = false;
			}



			var particleDisplay = FindObjectOfType<ParticleDisplay3D>();
			if (particleDisplay != null) particleDisplay.mode = ParticleDisplay3D.DisplayMode.None;
		}

		Camera GetActiveCamera()
		{
			Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.InstanceID);
			Camera best = null;
			float maxDepth = float.MinValue;
			foreach (var c in cams)
			{
				if (c.isActiveAndEnabled && c.depth > maxDepth && c != shadowCam)
				{
					best = c;
					maxDepth = c.depth;
				}
			}
			return best;
		}

		void Update()
		{
			if (sim == null) sim = FindObjectOfType<FluidSim>();
			if (sim == null || !sim.HasSpawned) return;

			activeCamera = GetActiveCamera();
			if (activeCamera == null) return;
			activeCamera.depthTextureMode |= DepthTextureMode.Depth;

			Init();
			if (!initialized) return;

			UpdateShadowCam();
			UpdateSettings();
			HandleDebugDisplayInput();
		}

		void UpdateShadowCam()
		{
			if (sun == null || shadowCam == null) return;
			Vector3 dirToSun = -sun.transform.forward;
			shadowCam.transform.position = dirToSun * 50;
			shadowCam.transform.rotation = sun.transform.rotation;
			shadowCam.orthographicSize = FrameBoundsOrtho(sim.Scale, shadowCam.worldToCameraMatrix) + 0.5f;
		}

		void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (sim == null || !sim.HasSpawned) return;
			if (!initialized || camera != activeCamera || camera == shadowCam) return;
			if (matComposite == null || matDepth == null || matThickness == null || matNormal == null || smoothPrepareMat == null) return;
			if (argsBuffer == null || !argsBuffer.IsValid()) return;

			var cmd = CommandBufferPool.Get("Fluid Screen Space Render");

			// Keep the completed URP scene so the composite shader can refract/reflect it.
			cmd.Blit(BuiltinRenderTextureType.CameraTarget, sceneRt);

			cmd.SetRenderTarget(shadowRt);
			cmd.ClearRenderTarget(true, true, Color.black);
			cmd.DrawMeshInstancedIndirect(quadMesh, 0, matThickness, 0, argsBuffer);
			gaussSmooth.Smooth(cmd, shadowRt, shadowRt, shadowRt.descriptor, shadowSmoothSettings, Vector3.one);


			cmd.SetRenderTarget(depthRt);
			cmd.ClearRenderTarget(true, true, Color.white * 10000000, 1);
			cmd.DrawMeshInstancedIndirect(quadMesh, 0, matDepth, 0, argsBuffer);

			cmd.SetRenderTarget(thicknessRt);
			cmd.ClearRenderTarget(true, true, Color.black);
			cmd.DrawMeshInstancedIndirect(quadMesh, 0, matThickness, 0, argsBuffer);

			cmd.Blit(null, compRt, smoothPrepareMat);
			ApplyActiveSmoothingType(cmd, compRt, compRt, compRt.descriptor, new Vector3(1, 1, 0));
			cmd.Blit(compRt, normalRt, matNormal);

			cmd.Blit(null, BuiltinRenderTextureType.CameraTarget, matComposite);

			context.ExecuteCommandBuffer(cmd);
			context.Submit();
			CommandBufferPool.Release(cmd);
		}



		void Init()
		{
			if (sim == null || sim.positionBuffer == null || sim.foamBuffer == null || sim.foamCountBuffer == null) return;
			if (!quadMesh) quadMesh = QuadGenerator.GenerateQuadMesh();
			ComputeHelper.CreateArgsBuffer(ref argsBuffer, quadMesh, sim.positionBuffer.count);

			InitTextures();
			InitMaterials();
			initialized = true;

			void InitMaterials()
			{
				if (!depthDownsampleCopyMat) depthDownsampleCopyMat = new Material(depthDownsampleCopyShader);
				if (!matDepth) matDepth = new Material(depthShader);
				if (!matNormal) matNormal = new Material(normalShader);
				if (!matThickness) matThickness = new Material(thicknessShader);
				if (!smoothPrepareMat) smoothPrepareMat = new Material(smoothThickPrepareShader);
				if (!matComposite) matComposite = new Material(renderA);
				matComposite.SetTexture("_MainTex", Texture2D.blackTexture);
			}

			void InitTextures()
			{
				int width = Screen.width;
				int height = Screen.height;

				float aspect = height / (float)width;
				int thicknessTexMaxWidth = Mathf.Min(1280, width);
				int thicknessTexMaxHeight = Mathf.Min((int)(1280 * aspect), height);
				int thicknessTexWidth = Mathf.Max(thicknessTexMaxWidth, width / 2);
				int thicknessTexHeight = Mathf.Max(thicknessTexMaxHeight, height / 2);

				if (useFullSizeThicknessTex)
				{
					thicknessTexWidth = width;
					thicknessTexHeight = height;
				}

				const int shadowTexSizeReduction = 4;
				int shadowTexWidth = width / shadowTexSizeReduction;
				int shadowTexHeight = height / shadowTexSizeReduction;

				GraphicsFormat fmtRGBA = GraphicsFormat.R32G32B32A32_SFloat;
				GraphicsFormat fmtR = GraphicsFormat.R32_SFloat;
				ComputeHelper.CreateRenderTexture(ref depthRt, width, height, FilterMode.Bilinear, fmtR, depthMode: DepthMode.Depth16);
				ComputeHelper.CreateRenderTexture(ref thicknessRt, thicknessTexWidth, thicknessTexHeight, FilterMode.Bilinear, fmtR, depthMode: DepthMode.Depth16);
				ComputeHelper.CreateRenderTexture(ref normalRt, width, height, FilterMode.Bilinear, fmtRGBA, depthMode: DepthMode.None);
				ComputeHelper.CreateRenderTexture(ref compRt, width, height, FilterMode.Bilinear, fmtRGBA, depthMode: DepthMode.None);
				ComputeHelper.CreateRenderTexture(ref shadowRt, shadowTexWidth, shadowTexHeight, FilterMode.Bilinear, fmtR, depthMode: DepthMode.None);
				ComputeHelper.CreateRenderTexture(ref sceneRt, width, height, FilterMode.Bilinear, fmtRGBA, depthMode: DepthMode.None);
			}
		}


		void ApplyActiveSmoothingType(CommandBuffer cmd, RenderTargetIdentifier src, RenderTargetIdentifier target, RenderTextureDescriptor desc, Vector3 smoothMask)
		{
			if (smoothType == BlurType.Bilateral1D)
			{
				bilateral1D.Smooth(cmd, src, target, desc, bilateralSettings, smoothMask);
			}
			else if (smoothType == BlurType.Bilateral2D)
			{
				bilateral2D.Smooth(cmd, src, target, desc, bilateralSettings, smoothMask);
			}
			else if (smoothType == BlurType.Gaussian)
			{
				gaussSmooth.Smooth(cmd, src, target, desc, gaussSmoothSettings, smoothMask);
			}
		}

		float FrameBoundsOrtho(Vector3 boundsSize, Matrix4x4 worldToView)
		{
			Vector3 halfSize = boundsSize * 0.5f;
			float maxX = 0;
			float maxY = 0;

			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = new Vector3(
					(i & 1) == 0 ? -halfSize.x : halfSize.x,
					(i & 2) == 0 ? -halfSize.y : halfSize.y,
					(i & 4) == 0 ? -halfSize.z : halfSize.z
				);

				Vector3 viewCorner = worldToView.MultiplyPoint(corner);
				maxX = Mathf.Max(maxX, Mathf.Abs(viewCorner.x));
				maxY = Mathf.Max(maxY, Mathf.Abs(viewCorner.y));
			}

			float aspect = Screen.height / (float)Screen.width;
			float targetOrtho = Mathf.Max(maxY, maxX * aspect);
			return targetOrtho;
		}




		void UpdateSettings()
		{
			smoothPrepareMat.SetTexture("Depth", depthRt);
			smoothPrepareMat.SetTexture("Thick", thicknessRt);
			
			matThickness.SetBuffer("Positions", sim.positionBuffer);
			matThickness.SetFloat("scale", thicknessParticleScale);
			
			matDepth.SetBuffer("Positions", sim.positionBuffer);
			matDepth.SetFloat("scale", depthParticleSize);

			matNormal.SetInt("useSmoothedDepth", Input.GetKey(KeyCode.LeftControl) ? 0 : 1);

			matComposite.SetTexture("_MainTex", Texture2D.blackTexture);
			matComposite.SetInt("debugDisplayMode", (int)displayMode);
			matComposite.SetTexture("Comp", compRt);
			matComposite.SetTexture("Normals", normalRt);
			matComposite.SetTexture("ShadowMap", shadowRt);
			matComposite.SetTexture("_SceneTex", sceneRt);
			
			matComposite.SetVector("testParams", testParams);
			matComposite.SetVector("extinctionCoefficients", extinctionCoefficients * extinctionMultiplier);
			matComposite.SetVector("boundsSize", sim.Scale);
			matComposite.SetFloat("refractionMultiplier", refractionMultiplier);
			matComposite.SetFloat("_DepthParticleSize", depthParticleSize);

			matComposite.SetMatrix("shadowVP", GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, false) * shadowCam.worldToCameraMatrix);
			matComposite.SetVector("dirToSun", -sun.transform.forward);
			matComposite.SetFloat("depthDisplayScale", depthDisplayScale);
			matComposite.SetFloat("thicknessDisplayScale", thicknessDisplayScale);
			matComposite.SetBuffer("foamCountBuffer", sim.foamCountBuffer);
			matComposite.SetInt("foamMax", sim.foamBuffer.count);

			BucketBody bucketBody = sim.bucketBody;
			matComposite.SetInt("_HasBucket", bucketBody != null ? 1 : 0);
			if (bucketBody != null)
			{
				matComposite.SetMatrix("_BucketWorldToLocal", bucketBody.transform.worldToLocalMatrix);
				matComposite.SetFloat("_BucketBottomY", bucketBody.bottomY);
				matComposite.SetFloat("_BucketTopY", bucketBody.topY);
				matComposite.SetFloat("_BucketBottomRadius", bucketBody.bottomRadius);
				matComposite.SetFloat("_BucketTopRadius", bucketBody.topRadius);
			}

			Color paintCol = GameManager.Instance != null ? GameManager.Instance.selectedColor : Color.white;
			matComposite.SetColor("_ParticleColor", paintCol);

			Vector3 floorSize = new Vector3(30, 0.05f, 30);
			float floorHeight = -sim.Scale.y / 2 + sim.transform.position.y - floorSize.y / 2;
			matComposite.SetVector("floorPos", new Vector3(0, floorHeight, 0));
			matComposite.SetVector("floorSize", floorSize);
			matComposite.SetColor("tileCol1", environmentSettings.tileCol1);
			matComposite.SetColor("tileCol2", environmentSettings.tileCol2);
			matComposite.SetColor("tileCol3", environmentSettings.tileCol3);
			matComposite.SetColor("tileCol4", environmentSettings.tileCol4);
			matComposite.SetVector("tileColVariation", environmentSettings.tileColVariation);
			matComposite.SetFloat("tileScale", environmentSettings.tileScale);
			matComposite.SetFloat("tileDarkOffset", environmentSettings.tileDarkOffset);
			matComposite.SetFloat("sunIntensity", environmentSettings.sunIntensity);
			matComposite.SetFloat("sunInvSize", environmentSettings.sunInvSize);
		}

		void HandleDebugDisplayInput()
		{
			for (int i = 0; i <= 9; i++)
			{
				if (Input.GetKeyDown(KeyCode.Alpha0 + i))
				{
					displayMode = (DisplayMode)i;
					Debug.Log("Set display mode: " + displayMode);
				}
			}
		}

		[System.Serializable]
		public struct EnvironmentSettings
		{
			public Color tileCol1;
			public Color tileCol2;
			public Color tileCol3;
			public Color tileCol4;
			public Vector3 tileColVariation;
			public float tileScale;
			public float tileDarkOffset;
			public float sunIntensity;
			public float sunInvSize;
		}

		public enum DisplayMode
		{
			Composite,
			Depth,
			SmoothDepth,
			Normal,
			Thickness,
			SmoothThickness
		}

		public enum BlurType
		{
			Gaussian,
			Bilateral2D,
			Bilateral1D
		}


		void OnDestroy()
		{
			ComputeHelper.Release(argsBuffer);
			if (shadowCamGO != null) DestroyImmediate(shadowCamGO);
		}
	}
}
