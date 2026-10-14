// 화면 어둠 합치기 셰이더. DarknessScreenFeature(렌더러 기능)가 쓴다. 값은 전부 스크립트가 넣어준다.
//  0번 단계 "DarknessComposite": 화면색 × (어두운 바탕색 + 빛 지도) — 마지막 카메라 화면 전체에 한 번
//  1번 단계 "PlayerLight"      : 플레이어 주변 빛을 빛 지도에 둥글고 흐릿하게 더함
Shader "EventHorizon/DarknessComposite"
{
    Properties
    {
        [HideInInspector] _Ambient ("어두운 바탕색 (스크립트가 조절)", Vector) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _Fade ("켜짐 정도 (스크립트가 조절)", Float) = 0
        [HideInInspector] _MaxBright ("최대 밝기 (스크립트가 조절)", Float) = 1.3
        [HideInInspector] _Color ("플레이어 빛 색 (스크립트가 조절)", Color) = (1, 1, 1, 1)
        [HideInInspector] _Softness ("플레이어 빛 흐림 (스크립트가 조절)", Float) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DarknessComposite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_LightMaskTex);
            float4 _Ambient;
            float _Fade;
            float _MaxBright;

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 light = SAMPLE_TEXTURE2D(_LightMaskTex, sampler_LinearClamp, uv).rgb;

                // 어두운 바탕 + 빛이 닿은 만큼 밝기 되살림 (최대 밝기까지)
                float3 k = min(_Ambient.rgb + light, _MaxBright.xxx);
                k = lerp(float3(1, 1, 1), k, saturate(_Fade));

                return float4(src.rgb * k, src.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PlayerLight"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;
            float _Softness;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V2F { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 frag (V2F i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);                 // 0 = 가운데, 1 = 반경 끝
                float soft = max(_Softness, 0.001);
                float f = 1.0 - smoothstep(1.0 - soft, 1.0, d);
                f *= f;                                              // 가운데는 은은하게 넓게, 끝은 더 부드럽게
                return float4(_Color.rgb * f, 0);
            }
            ENDHLSL
        }
    }
}
