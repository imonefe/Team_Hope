// [2026-10-23] 빛줄기 면 셰이더 (LightShaftFX 전용). 더하기 방식이라 겹칠수록 밝아짐
Shader "EventHorizon/LightShaft"
{
    Properties
    {
        [HDR] _Color ("색", Color) = (1, 0.86, 0.62, 1)
        _Intensity ("밝기", Range(0, 3)) = 0.35
        _EdgeSoftness ("가장자리 부드러움", Range(0.02, 1)) = 0.5
        _LengthFalloff ("끝으로 갈수록 흐려짐", Range(0.1, 5)) = 1.4
        _StartFade ("시작 부분 흐림", Range(0, 0.5)) = 0.1
        _StreakAmount ("줄무늬 일렁임", Range(0, 1)) = 0.5
        _StreakCount ("줄무늬 개수", Range(1, 20)) = 5
        _StreakSpeed ("일렁임 속도", Range(0, 2)) = 0.25
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
                float4 _Color;
                float _Intensity;
                float _EdgeSoftness;
                float _LengthFalloff;
                float _StartFade;
                float _StreakAmount;
                float _StreakCount;
                float _StreakSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float across = abs(i.uv.x * 2.0 - 1.0);           // 가운데 0, 가장자리 1
                float edge = saturate((1.0 - across) / _EdgeSoftness);
                edge = edge * edge * (3.0 - 2.0 * edge);

                float along = saturate(i.uv.y);                    // 시작 0, 끝 1
                float fall = pow(1.0 - along, _LengthFalloff);
                float start = smoothstep(0.0, _StartFade + 1e-4, along);

                float t = _Time.y * _StreakSpeed;
                float s1 = sin(i.uv.x * _StreakCount * 6.2831 + t * 1.7) * 0.5 + 0.5;
                float s2 = sin(i.uv.x * _StreakCount * 2.3 * 6.2831 - t * 1.1 + along * 2.0) * 0.5 + 0.5;
                float streak = lerp(1.0, 0.35 + 0.65 * lerp(s1, s2, 0.5), _StreakAmount);

                float a = edge * fall * start * streak * _Intensity;
                return float4(_Color.rgb * a, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
