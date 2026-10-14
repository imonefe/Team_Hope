// 피격 시 "화면 가장자리만" 실제 게임 화면을 일그러뜨리는 전체 화면 셰이더.
// 가로 줄 밀림 + 큰 찢김 + RGB 색 분리 + 붉은 물들임 + 가끔 하얀 줄.  화면 가운데는 그대로 둔다.
// HitGlitchScreenFeature(렌더러 기능)가 마지막 카메라의 화면을 복사해서 이 셰이더로 다시 그린다.
// _Intensity / _Seed 는 HitGlitchEffect 스크립트가 매 프레임 넣어준다.
Shader "EventHorizon/HitGlitchScreen"
{
    Properties
    {
        _Intensity ("세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0

        [Header(Edge)]
        _EdgeWidth ("효과가 들어오는 가장자리 폭", Range(0.02, 0.6)) = 0.24
        _EdgeNoise ("가장자리 경계 들쭉날쭉", Range(0, 0.3)) = 0.12

        [Header(Distortion)]
        _LineCount ("가는 줄 개수 (세로)", Range(20, 600)) = 220
        _Shift ("가는 줄 밀림 거리", Range(0, 0.2)) = 0.035
        _BandCount ("큰 찢김 띠 개수 (세로)", Range(2, 60)) = 14
        _BandAmount ("큰 찢김 띠 양", Range(0, 1)) = 0.45
        _BandShift ("큰 찢김 밀림 거리", Range(0, 0.3)) = 0.09
        _Wave ("물결 흔들림", Range(0, 0.05)) = 0.008

        [Header(Color)]
        _Split ("RGB 색 분리 거리", Range(0, 0.06)) = 0.02
        _TintColor ("물들일 색", Color) = (1, 0.12, 0.16, 1)
        _Tint ("붉은 물들임 정도", Range(0, 1)) = 0.6
        _WhiteAmount ("하얀 줄 양", Range(0, 1)) = 0.12
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
            Name "HitGlitchScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _Seed;
            float _EdgeWidth;
            float _EdgeNoise;
            float _LineCount;
            float _Shift;
            float _BandCount;
            float _BandAmount;
            float _BandShift;
            float _Wave;
            float _Split;
            float4 _TintColor;
            float _Tint;
            float _WhiteAmount;

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            float3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Intensity <= 0.001) return src;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = floor(_Seed);

                // 줄 번호 (가는 줄 / 큰 띠)
                float lineId = floor(uv.y * _LineCount);
                float bandId = floor(uv.y * _BandCount);
                float lineRand = Hash11(lineId + seed * 7.13);
                float bandRand = Hash11(bandId * 3.7 + seed * 1.91);

                // 1) 가장자리 마스크: 가장자리 1 → 안쪽 0. 줄마다 경계를 들쭉날쭉하게
                float dx = min(uv.x, 1.0 - uv.x) * aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float d = min(dx, dy) + (lineRand - 0.5) * _EdgeNoise + (bandRand - 0.5) * _EdgeNoise;
                float width = _EdgeWidth * (0.7 + 0.3 * _Intensity);
                float e = (1.0 - smoothstep(0.0, width, d)) * _Intensity;

                // 2) 가로 밀림: 가는 줄 떨림 + 가끔 큰 띠가 통째로 밀림 + 약한 물결
                float jitter = (lineRand - 0.5) * 2.0 * _Shift;
                float bandOn = step(1.0 - _BandAmount, bandRand);
                float band = bandOn * (Hash11(bandId * 1.7 + seed) - 0.5) * 2.0 * _BandShift;
                float wave = sin(uv.y * 60.0 + _Time.y * 35.0) * _Wave;
                float offset = (jitter + band + wave) * e;

                // 3) RGB 분리: 빨강/초록/파랑을 서로 다른 곳에서 가져옴
                float split = _Split * e * (0.6 + 0.8 * bandRand);
                float r = SampleScreen(uv + float2(offset + split, 0)).r;
                float g = SampleScreen(uv + float2(offset, 0)).g;
                float b = SampleScreen(uv + float2(offset - split, 0)).b;
                float3 col = float3(r, g, b);

                // 4) 붉은 물들임 (가장자리일수록 진하게)
                float3 tinted = col * _TintColor.rgb * 1.7 + _TintColor.rgb * 0.06;
                col = lerp(col, tinted, saturate(e * _Tint));

                // 5) 가끔 가늘고 하얀 줄
                float white = step(1.0 - _WhiteAmount * 0.25, Hash11(lineId * 2.31 + seed * 5.3)) * step(0.35, e);
                col += white * e * 0.55;

                return float4(col, src.a);
            }
            ENDHLSL
        }
    }
}
