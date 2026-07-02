Shader "Custom/ParticleBillboard"
{
    Properties
    {
        _ColorLow ("Color Low Density", Color) = (0.2, 0.5, 1.0, 0.95)
        _ColorHigh ("Color High Density", Color) = (0.05, 0.15, 0.6, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        // Reverted to standard alpha blending to remove additive glow
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
            float _RestDensity;
            float4 _ColorLow;
            float4 _ColorHigh;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float density : TEXCOORD1;
            };

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float3 particlePos = _ParticleBuffer[instanceID].posAndDensity.xyz;
                float density = _ParticleBuffer[instanceID].posAndDensity.w;

                // Map 6 vertex IDs (2 triangles) to 4 unique quad corners
                // T1: 0→(0,0), 1→(1,0), 2→(1,1)
                // T2: 3→(0,0), 4→(1,1), 5→(0,1)
                float2 uv = float2(
                    (vertexID == 1 || vertexID == 2 || vertexID == 4) ? 1 : 0,
                    (vertexID == 2 || vertexID == 4 || vertexID == 5) ? 1 : 0
                );
                float2 offset = uv * 2.0 - 1.0;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up    = UNITY_MATRIX_V[1].xyz;

                float3 worldPos = particlePos + (offset.x * right + offset.y * up) * _ParticleSize;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = uv;
                o.density = density;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Make particles effectively larger and softer for merging
                float2 offset = i.uv * 2.0 - 1.0;
                
                // Increase visual radius by scaling the offset
                float scale = 2.0;
                float2 blendedOffset = offset * scale;
                float r2 = dot(blendedOffset, blendedOffset);
                
                // Very soft falloff to blend overlapping particles
                // Mapping the range [0, scale^2] to [1, 0]
                float alpha = smoothstep(scale * scale, 0.0, r2);
                if (alpha <= 0.01) discard; 

                // Simple flat shading for matte paint
                float3 lightDir = normalize(float3(0.3, 1.0, 0.5));
                float diffuse = 0.8; // Flat matte

                float density01 = saturate((i.density - _RestDensity * 0.6) / max(_RestDensity * 0.8, 1.0));
                float4 baseColor = lerp(_ColorLow, _ColorHigh, density01);
                
                return float4(baseColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
