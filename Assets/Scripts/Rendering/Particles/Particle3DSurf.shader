Shader "Fluid/Particle3DSurf"
{
    Properties
    {
        _ParticleColor("Particle Color", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float3> Positions;

            float scale;
            float4 _ParticleColor;
            float _Glossiness;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldPos = Positions[v.instanceID] + v.vertex.xyz * scale;

                o.pos = TransformWorldToHClip(worldPos);
                o.worldNormal = normalize(v.normal.xyz);
                o.worldPos = worldPos;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);

                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;

                float diffuse = saturate(dot(normal, lightDir));
                float3 viewDir = normalize(GetCameraPositionWS() - i.worldPos);
                float3 halfDir = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(normal, halfDir)), lerp(8.0, 64.0, _Glossiness)) * _Glossiness;

                float3 ambient = SampleSH(normal);
                float3 lighting = ambient + lightColor * (diffuse + specular);

                return half4(saturate(_ParticleColor.rgb * lighting), _ParticleColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            StructuredBuffer<float3> Positions;
            float scale;

            struct Attributes
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldPos = Positions[v.instanceID] + v.vertex.xyz * scale;
                o.pos = TransformWorldToHClip(worldPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> Positions;
            float scale;

            struct Attributes
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldPos = Positions[v.instanceID] + v.vertex.xyz * scale;
                o.pos = TransformWorldToHClip(worldPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
