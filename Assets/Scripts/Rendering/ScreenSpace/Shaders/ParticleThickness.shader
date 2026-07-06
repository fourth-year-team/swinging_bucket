Shader "Fluid/ParticleThickness" {
	SubShader {

		Tags { "Queue"="Transparent" }
		ZWrite Off
		ZTest LEqual
		Cull Off
		Blend One One

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			#include "UnityCG.cginc"
			
			StructuredBuffer<float3> Positions;
			float scale;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 posWorld : TEXCOORD1;
				float3 centreWorld : TEXCOORD2;
			};

			v2f vert (appdata_base v, uint instanceID : SV_InstanceID)
			{
				v2f o;
				
				float3 worldCentre = Positions[instanceID];
				float3 vertOffset = v.vertex * scale * 2;
				float3 camUp = unity_CameraToWorld._m01_m11_m21;
				float3 camRight = unity_CameraToWorld._m00_m10_m20;
				float3 vertPosWorld = worldCentre + camRight * vertOffset.x + camUp * vertOffset.y;
				o.pos = mul(UNITY_MATRIX_VP, float4(vertPosWorld, 1));
				o.posWorld = vertPosWorld;
				o.centreWorld = worldCentre;
				o.uv = v.texcoord;

				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float2 centreOffset = (i.uv.xy - 0.5) * 2;
				float sqrDst = dot(centreOffset, centreOffset);
				if (sqrDst >= 1) discard;

				float z = sqrt(1 - sqrDst);
				float3 sphereNormal = normalize(centreOffset.x * unity_CameraToWorld._m00_m10_m20 + centreOffset.y * unity_CameraToWorld._m01_m11_m21 + z * normalize(_WorldSpaceCameraPos - i.centreWorld));
				float3 viewDir = normalize(_WorldSpaceCameraPos - i.centreWorld);
				if (dot(sphereNormal, viewDir) <= 0) discard;

				const float contribution = 0.1;
				return contribution;
			}

			ENDCG
		}
	}
}
