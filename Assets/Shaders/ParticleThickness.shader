Shader "Custom/ParticleThickness"
{
    Properties
    {
        _ThicknessOpacity("Thickness Opacity", Range(0, 1)) = 0.14
        _ColorLow("Color Low Density", Color) = (0.2, 0.5, 1.0, 0.95)
        _ColorHigh("Color High Density", Color) = (0.05, 0.15, 0.6, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Particle
            {
                float4 posAndDensity;
                float4 velAndPressure;
            };

            StructuredBuffer<Particle> _ParticleBuffer;
            float _ParticleSize;
            float _ThicknessOpacity;
            float _RestDensity;
            float4 _ColorLow;
            float4 _ColorHigh;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float density : TEXCOORD1;
                float depthFade : TEXCOORD2;
            };

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float3 particlePos = _ParticleBuffer[instanceID].posAndDensity.xyz;

                float2 uv = float2(
                    (vertexID == 1 || vertexID == 2 || vertexID == 4) ? 1 : 0,
                    (vertexID == 2 || vertexID == 4 || vertexID == 5) ? 1 : 0
                );
                float2 offset = uv * 2.0 - 1.0;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;

                float3 worldPos = particlePos + (offset.x * right + offset.y * up) * _ParticleSize;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = uv;
                o.density = _ParticleBuffer[instanceID].posAndDensity.w;
                o.depthFade = saturate((-viewPos.z) * 0.04);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 offset = i.uv * 2.0 - 1.0;
                float r2 = dot(offset, offset);
                float thickness = smoothstep(1.0, 0.0, r2);
                thickness *= _ThicknessOpacity;
                thickness *= lerp(0.45, 1.0, i.depthFade);
                float density01 = saturate((i.density - _RestDensity * 0.6) / max(_RestDensity * 0.8, 1.0));
                float3 tint = lerp(_ColorLow.rgb, _ColorHigh.rgb, density01);
                return float4(tint * thickness, 1.0);
            }
            ENDCG
        }
    }
}