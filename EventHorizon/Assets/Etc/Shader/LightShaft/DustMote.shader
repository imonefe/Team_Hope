// [2026-10-23] 먼지 알갱이 셰이더 (LightShaftFX 의 파티클용). 그림 파일 없이 둥글고 부드러운 점을 그림
Shader "EventHorizon/DustMote"
{
    Properties
    {
        _Intensity ("밝기", Range(0, 5)) = 1.5
        _Softness ("부드러움", Range(0.5, 4)) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), _Softness) * i.color.a * _Intensity;
                return float4(i.color.rgb * a, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
