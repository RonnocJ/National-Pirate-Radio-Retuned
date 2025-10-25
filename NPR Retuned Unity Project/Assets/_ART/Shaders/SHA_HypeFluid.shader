Shader "Custom/SHA_HypeFluid"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Fill ("Fill Amount", Range(0, 1)) = 1
        _HalfWidth ("Mesh Half Width", Float) = 0.5
        _Feather ("Edge Feather (Fraction)", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Fill;
                float _HalfWidth;
                float _Feather;
            CBUFFER_END

            float4 _BaseMap_ST;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float fill = saturate(_Fill);
                if (fill <= 0.0001)
                {
                    clip(-1.0);
                }

                float width = max(abs(_HalfWidth), 1e-4);
                float fillEdge = lerp(-width, width, fill);
                float distanceToEdge = fillEdge - IN.positionOS.x;

                if (fill < 0.9999)
                {
                    clip(distanceToEdge);
                }

                float featherFrac = saturate(_Feather);
                if (featherFrac > 0.0 && fill < 0.9999)
                {
                    float featherDistance = max(featherFrac * (2.0 * width), 1e-4);
                    float edgeMask = saturate(distanceToEdge / featherDistance);
                    color.a *= edgeMask;
                }

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
