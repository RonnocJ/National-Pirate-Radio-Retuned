Shader "NPR/TerrainRoadBlend"
{
    Properties
    {
        _BaseMap ("Base (Grass)", 2D) = "white" {}
        _RoadMap ("Road (Asphalt)", 2D) = "gray" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _RoadColor ("Road Color", Color) = (1,1,1,1)
        _BaseTiling ("Base Tiling (m)", Float) = 4
        _RoadTiling ("Road Tiling (m)", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // URP fog keywords
            #pragma multi_compile_fragment _ FOG_LINEAR FOG_EXP FOG_EXP2
            // Lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RoadColor;
                float _BaseTiling;
                float _RoadTiling;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RoadMap); SAMPLER(sampler_RoadMap);

            // Per-renderer road data (set via MaterialPropertyBlock)
            #define ROAD_MAX_SEGS 32
            float4 _RoadSegA[ROAD_MAX_SEGS]; // xy = start (world XZ)
            float4 _RoadSegB[ROAD_MAX_SEGS]; // xy = end   (world XZ)
            float _RoadSegCount;             // number of valid segments in arrays
            float _RoadHalfWidth;
            float _RoadShoulder;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float  fogFactor  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // World-space planar mapping for consistent tiling across tiles
                float2 baseUV = i.positionWS.xz / max(1e-3, _BaseTiling);
                float2 roadUV = i.positionWS.xz / max(1e-3, _RoadTiling);
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
                float4 roadTex = SAMPLE_TEXTURE2D(_RoadMap, sampler_RoadMap, roadUV);

                // Compute per-fragment road mask by distance to nearest segment
                float2 P = i.positionWS.xz;
                float bestW = 0.0;
                int count = (int)_RoadSegCount;
                for (int s = 0; s < count; s++)
                {
                    float2 A = _RoadSegA[s].xy;
                    float2 B = _RoadSegB[s].xy;
                    float2 AB = B - A;
                    float abLen2 = max(1e-6, dot(AB, AB));
                    float t = saturate(dot(P - A, AB) / abLen2);
                    float2 C = A + AB * t;
                    float d = distance(P, C);
                    float influence = _RoadHalfWidth + _RoadShoulder;
                    float w = 1.0 - smoothstep(0.0, influence, d);
                    bestW = max(bestW, w);
                }
                float m = saturate(bestW);
                float3 albedo = lerp(baseTex.rgb * _BaseColor.rgb, roadTex.rgb * _RoadColor.rgb, m);

                // Main directional light
                Light mainLight = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 L = normalize(-mainLight.direction);
                float NdotL = saturate(dot(n, L));

                // Shadows
                #if defined(_MAIN_LIGHT_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                float shadow = MainLightRealtimeShadow(shadowCoord);
                #else
                float shadow = 1.0;
                #endif

                float3 color = albedo * (NdotL * mainLight.color.rgb * shadow);

                // Ambient from SH
                color += albedo * SampleSH(n);

                // Additional lights
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light lgt = GetAdditionalLight(li, i.positionWS);
                    float3 L2 = normalize(-lgt.direction);
                    float NdotL2 = saturate(dot(n, L2));
                    color += albedo * (NdotL2 * lgt.color.rgb * lgt.distanceAttenuation);
                }
                #endif

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
