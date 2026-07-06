Shader "Fluid/FluidRender"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ParticleColor ("Paint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Textures
            sampler2D _MainTex;
            sampler2D _SceneTex;
            sampler2D _CameraOpaqueTexture;
            sampler2D_float _CameraDepthTexture;
            sampler2D Normals;
            sampler2D Comp;
            sampler2D ShadowMap;

            const float3 extinctionCoefficients;
            const float3 dirToSun;
            const float3 boundsSize;
            const float refractionMultiplier;

            // Paint color
            float4 _ParticleColor;

            // Debug values
            float3 testParams;
            int debugDisplayMode;
            float depthDisplayScale;
            float thicknessDisplayScale;
            StructuredBuffer<uint> foamCountBuffer;
            uint foamMax;

            int _HasBucket;
            float4x4 _BucketWorldToLocal;
            float _BucketBottomY;
            float _BucketTopY;
			float _BucketBottomRadius;
			float _BucketTopRadius;
			float _DepthParticleSize;


            struct HitInfo
            {
                bool didHit;
                bool isInside;
                float dst;
                float3 hitPoint;
                float3 normal;
            };


            struct LightResponse
            {
                float3 reflectDir;
                float3 refractDir;
                float reflectWeight;
                float refractWeight;
            };


            float3 WorldViewDir(float2 uv)
            {
                float3 viewVector = mul(unity_CameraInvProjection, float4(uv.xy * 2 - 1, 0, -1));
                return normalize(mul(unity_CameraToWorld, viewVector));
            }

            // Project world position to screen UV and sample scene
            float3 SampleSceneAtWorldPos(float3 worldPos)
            {
                float4 clipPos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                float2 uv = clipPos.xy / clipPos.w * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
                #endif
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
                return tex2D(_CameraOpaqueTexture, uv).rgb;
            }

            float BucketRadiusAtY(float y)
            {
                float h = max(_BucketTopY - _BucketBottomY, 1e-4);
                float t = saturate((y - _BucketBottomY) / h);
                return lerp(_BucketBottomRadius, _BucketTopRadius, t);
            }

            bool IsInsideBucketLocal(float3 p)
            {
                if (_HasBucket == 0) return true;
                if (p.y < _BucketBottomY || p.y > _BucketTopY) return false;
                return length(p.xz) <= BucketRadiusAtY(p.y);
            }

			bool IsInsideBucket(float3 worldPos)
			{
			    return IsInsideBucketLocal(mul(_BucketWorldToLocal, float4(worldPos, 1)).xyz);
			}

			bool RayCanSeeInsideBucket(float3 worldPos)
			{
			    if (_HasBucket == 0) return true;

			    float3 camLocal = mul(_BucketWorldToLocal, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
			    float3 hitLocal = mul(_BucketWorldToLocal, float4(worldPos, 1)).xyz;

			    if (!IsInsideBucketLocal(hitLocal)) return false;
			    if (IsInsideBucketLocal(camLocal)) return true;

			    float3 ray = hitLocal - camLocal;
			    if (abs(ray.y) < 1e-4) return false;

			    // The visible interior is reachable only through the open top disc.
			    float topT = (_BucketTopY - camLocal.y) / ray.y;
			    if (topT < 0 || topT > 1) return false;

			    float3 topHit = camLocal + ray * topT;
			    return length(topHit.xz) <= _BucketTopRadius;
			}

			// Expanded version: treats positions within _DepthParticleSize of the bucket wall as "inside"
			// so that protruding particle sphere surfaces are properly occluded.
			bool RayCanSeeNearBucket(float3 worldPos)
			{
			    if (_HasBucket == 0) return true;

			    float3 camLocal = mul(_BucketWorldToLocal, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
			    float3 hitLocal = mul(_BucketWorldToLocal, float4(worldPos, 1)).xyz;

			    // If camera is inside the bucket, can definitely see it
			    if (IsInsideBucketLocal(camLocal)) return true;

			    // Trace ray to see if it passes through the top opening
			    float3 ray = hitLocal - camLocal;
			    if (abs(ray.y) < 1e-4) return false;

			    float topT = (_BucketTopY - camLocal.y) / ray.y;
			    if (topT < 0 || topT > 1) return false;

			    float3 topHit = camLocal + ray * topT;
			    return length(topHit.xz) <= _BucketTopRadius;
			}

            float3 CalculateClosestFaceNormal(float3 boxSize, float3 p)
            {
                float3 halfSize = boxSize * 0.5;
                float3 o = (halfSize - abs(p));
                return (o.x < o.y && o.x < o.z) ? float3(sign(p.x), 0, 0) : (o.y < o.z) ? float3(0, sign(p.y), 0) : float3(0, 0, sign(p.z));
            }

            float4 SmoothEdgeNormals(float3 normal, float3 pos, float3 boxSize)
            {
                float3 o = boxSize / 2 - abs(pos);
                float faceWeight = max(0, min(o.x, o.z));
                float3 faceNormal = CalculateClosestFaceNormal(boxSize, pos);
                const float smoothDst = 0.01;
                float cornerWeight = 1 - saturate(abs(o.x - o.z) * 6);
                faceWeight = 1 - smoothstep(0, smoothDst, faceWeight);
                faceWeight *= (1 - cornerWeight);

                return float4(normalize(normal * (1 - faceWeight) + faceNormal * (faceWeight)), faceWeight);
            }

            // Calculate the proportion of light that is reflected at the boundary between two media (via the fresnel equations)
            float CalculateReflectance(float3 inDir, float3 normal, float iorA, float iorB)
            {
                float refractRatio = iorA / iorB;
                float cosAngleIn = -dot(inDir, normal);
                float sinSqrAngleOfRefraction = refractRatio * refractRatio * (1 - cosAngleIn * cosAngleIn);
                if (sinSqrAngleOfRefraction >= 1) return 1;

                float cosAngleOfRefraction = sqrt(1 - sinSqrAngleOfRefraction);
                float rPerpendicular = (iorA * cosAngleIn - iorB * cosAngleOfRefraction) / (iorA * cosAngleIn + iorB * cosAngleOfRefraction);
                rPerpendicular *= rPerpendicular;
                float rParallel = (iorB * cosAngleIn - iorA * cosAngleOfRefraction) / (iorB * cosAngleIn + iorA * cosAngleOfRefraction);
                rParallel *= rParallel;

                return (rPerpendicular + rParallel) / 2;
            }


            float3 Refract(float3 inDir, float3 normal, float iorA, float iorB)
            {
                float refractRatio = iorA / iorB;
                float cosAngleIn = -dot(inDir, normal);
                float sinSqrAngleOfRefraction = refractRatio * refractRatio * (1 - cosAngleIn * cosAngleIn);
                if (sinSqrAngleOfRefraction > 1) return 0;

                float3 refractDir = refractRatio * inDir + (refractRatio * cosAngleIn - sqrt(1 - sinSqrAngleOfRefraction)) * normal;
                return refractDir;
            }

            float3 Reflect(float3 inDir, float3 normal)
            {
                return inDir - 2 * dot(inDir, normal) * normal;
            }


            LightResponse CalculateReflectionAndRefraction(float3 inDir, float3 normal, float iorA, float iorB)
            {
                LightResponse result;
                result.reflectWeight = CalculateReflectance(inDir, normal, iorA, iorB);
                result.refractWeight = 1 - result.reflectWeight;
                result.reflectDir = Reflect(inDir, normal);
                result.refractDir = Refract(inDir, normal, iorA, iorB);
                return result;
            }

            float4 DebugModeDisplay(float depthSmooth, float depth, float thicknessSmooth, float thickness, float3 normal)
            {
                float3 col = 0;
                switch (debugDisplayMode)
                {
                case 1:
                    col = depth / depthDisplayScale;
                    break;
                case 2:
                    col = depthSmooth / depthDisplayScale;
                    break;
                case 3:
                    if (dot(normal, normal) == 0) col = 0;
                    else col = normal * 0.5 + 0.5;
                    break;
                case 4:
                    col = thickness / thicknessDisplayScale;
                    break;
                case 5:
                    col = thicknessSmooth / thicknessDisplayScale;
                    break;
                default:
                    col = float3(1, 0, 1);
                    break;
                }
                return float4(col, 1);
            }

            float4 frag(v2f i) : SV_Target
            {
                if (i.uv.y < 0.005)
                {
                    return i.uv.x < (foamCountBuffer[0] / (float)foamMax);
                }

                // ---- Read data from texture ----
                float3 normal = tex2D(Normals, i.uv).xyz;

                float4 packedData = tex2D(Comp, i.uv);
                float depthSmooth = packedData.r;
                float thickness = packedData.g;
                float thickness_hard = packedData.b;
                float depth_hard = packedData.a;

                float4 bg = tex2D(_MainTex, i.uv);
                float foam = bg.r;
                float foamDepth = bg.b;

                // ---- Scene colour (from real scene rendering) ----
                float3 viewDirWorld = WorldViewDir(i.uv);
                float3 sceneCol = tex2D(_CameraOpaqueTexture, i.uv).rgb;

                // ---- Calculate fluid hit point and smooth out normals along edges of bounding box ----
                float3 hitPos = _WorldSpaceCameraPos.xyz + viewDirWorld * depthSmooth;
                {
                    float3 hitLocal = mul(_BucketWorldToLocal, float4(hitPos, 1)).xyz;
                    bool inBucketExp = _HasBucket != 0
                        && hitLocal.y >= _BucketBottomY
                        && hitLocal.y <= _BucketTopY
                        && length(hitLocal.xz) <= BucketRadiusAtY(hitLocal.y) + _DepthParticleSize;
                    if ((IsInsideBucket(hitPos) || inBucketExp) && !RayCanSeeNearBucket(hitPos)) return float4(0, 0, 0, 0);
                }

                float3 smoothEdgeNormal = SmoothEdgeNormals(normal, hitPos, boundsSize).xyz;
                normal = normalize(normal + smoothEdgeNormal * 6 * max(0, dot(normal, smoothEdgeNormal.xyz)));

                // ---- Debug display mode ----
                if (debugDisplayMode != 0)
                {
                    return DebugModeDisplay(depthSmooth, depth_hard, thickness, thickness_hard, normal);
                }

                // If no fluid is present, leave the already-rendered camera image untouched.
                if (depthSmooth > 1000) return float4(0, 0, 0, 0);

                float sceneEyeDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
                float fluidEyeDepth = -mul(UNITY_MATRIX_V, float4(hitPos, 1)).z;
                if (sceneEyeDepth < fluidEyeDepth - 0.03)
                {
                    return float4(0, 0, 0, 0);
                }

                // ---- Calculate shading ----
                const float ambientLight = 0.3;
                float shading = dot(normal, dirToSun) * 0.5 + 0.5;
                shading = shading * (1 - ambientLight) + ambientLight;

                // ---- Calculate reflection and refraction ----
                LightResponse lightResponse = CalculateReflectionAndRefraction(viewDirWorld, normal, 1, 1.33);

                float3 exitPos = hitPos + lightResponse.refractDir * thickness * refractionMultiplier;

                // ---- Paint colour ----
                float3 paintCol = _ParticleColor.rgb;
                float3 transmission = exp(-thickness * extinctionCoefficients);
                float paintOpacity = saturate(1 - (transmission.r + transmission.g + transmission.b) / 3);
                float3 paintBody = paintCol * shading;

                // Scene reflection (sampled from scene at reflected position)
                float3 reflectCol = SampleSceneAtWorldPos(hitPos + lightResponse.reflectDir * 2);

                // Scene background seen through fluid (refraction)
                float3 refractCol = SampleSceneAtWorldPos(exitPos);

                // In URP fallback paths, the opaque texture can be unavailable/black.
                // Keep the paint visible instead of darkening the whole screen.
                if (dot(reflectCol, reflectCol) < 0.0001) reflectCol = paintBody;
                if (dot(refractCol, refractCol) < 0.0001) refractCol = paintBody;

                // Blend foam into refraction
                refractCol = refractCol * (1 - foam) + foam;

                // Mix background with paint based on thickness
                refractCol = lerp(refractCol, paintBody, paintOpacity);

                // Foam overlay on reflection
                if (foamDepth < depthSmooth)
                {
                    reflectCol = reflectCol * (1 - foam) + foam;
                }

                // Fresnel blend between reflection and paint body
                float3 col = lerp(reflectCol, refractCol, lightResponse.refractWeight);
                float alpha = saturate(0.45 + paintOpacity * 0.55);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
