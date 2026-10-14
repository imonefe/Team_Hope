// 일시정지 메뉴(ESC)가 열려 있는 동안 뒤의 게임 화면을 흐리게 만드는 전체 화면 셰이더 (카와세 블러 한 번).
// HitGlitchScreenFeature(렌더러 기능)가 이 셰이더를 여러 번(반복 횟수) 겹쳐 그려서 점점 넓게 흐려지게 한다.
// _Offset(샘플 간격, 픽셀) / _DimAmount(어둡게 할 정도) 는 렌더러 기능이 반복마다 넣어준다.
// 메뉴 자체는 화면 위 UI 라서 흐려지지 않는다.
Shader "EventHorizon/MenuBlurScreen"
{
    Properties
    {
        _Offset ("샘플 간격 (렌더러 기능이 조절)", Float) = 1
        _DimAmount ("어둡게 적용 정도 (렌더러 기능이 조절)", Range(0, 1)) = 0
        _Dim ("최대로 어둡게 할 정도", Range(0, 1)) = 0.25
        _Tint ("물들일 색", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "MenuBlurScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Offset;
            float _DimAmount;
            float _Dim;
            float4 _Tint;

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 t = _Offset / _ScreenParams.xy;

                // 대각선 4곳을 섞음 (반복할 때마다 간격을 넓혀서 부드럽게 퍼지는 블러)
                float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( t.x,  t.y))
                         + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-t.x,  t.y))
                         + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( t.x, -t.y))
                         + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-t.x, -t.y));
                c *= 0.25;

                // 마지막 반복에서만 살짝 어둡게 + 물들임
                float3 dimmed = c.rgb * (1.0 - _Dim) * _Tint.rgb;
                c.rgb = lerp(c.rgb, dimmed, _DimAmount);
                return c;
            }
            ENDHLSL
        }
    }
}
