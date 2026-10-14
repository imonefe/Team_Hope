// 빛줄기 속 먼지 셰이더. LightBeam 스크립트가 재생 중에 만드는 입자(파티클)에 쓴다.
// 그림 없이 둥글고 흐릿한 점을 직접 그린다. 색과 투명도는 입자 색(빛줄기 색 + 나타남/사라짐)을 따른다.
// 어두운 방에서는 화면 어둠이 먼지에도 적용되므로, 빛 밖의 먼지는 자연히 어둡고 빛 안의 먼지만 반짝인다.
Shader "EventHorizon/LightDust"
{
    Properties
    {
        _Intensity ("밝기", Range(0, 3)) = 1
        _Sharpness ("점 선명도", Range(0.5, 6)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Dust"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Sharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V2F { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (V2F i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);
                float a = pow(saturate(1.0 - d), _Sharpness);
                return float4(i.color.rgb * (i.color.a * a * _Intensity), 0);
            }
            ENDHLSL
        }
    }
}
