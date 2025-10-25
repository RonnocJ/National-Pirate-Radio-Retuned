Shader "Custom/SHA_Outline"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [OutlineWidth] _OutlineWidth ("Outline Width", Float) = 0.02

        _Tolerance ("Color Tolerance", Range(0,1)) = 0.08
        _Softness ("Tolerance Softness", Range(0,0.5)) = 0.02
        _ClipThreshold ("Clip Threshold", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent" 
        }

        Pass
        {
            Name "OUTLINE"
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _OutlineWidth;
                float _Tolerance;
                float _Softness;
                float _ClipThreshold;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 normal = normalize(IN.normalOS);
                float3 offset = normal * _OutlineWidth;
                float4 pushed = IN.positionOS + float4(offset, 0.0);

                OUT.positionHCS = TransformObjectToHClip(pushed.xyz);
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;

                float3 r = float3(1, 0, 0);
                float3 g = float3(0, 1, 0);
                float3 b = float3(0, 0, 1);

                float rd = length(tex - r);
                float gd = length(tex - g);
                float bd = length(tex - b);

                float minDist = min(rd, min(gd, bd));

                float low = max(0.0, _Tolerance - _Softness);
                float high = _Tolerance + _Softness;
                float mask = 1.0 - smoothstep(low, high, minDist);

                if (mask < _ClipThreshold)
                    discard;

                float3 outlineRGB = _BaseColor.rgb * mask;
                float outlineA = _BaseColor.a * mask;

                return half4(outlineRGB, outlineA);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
