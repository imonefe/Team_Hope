// 플레이어 HP 가 낮을 때(기본 20% 이하) 화면 가장자리를 거칠게 일그러뜨리는 전체 화면 셰이더.
//  - 물결 왜곡 + 굵은 띠 찢김 + 가는 줄 밀림 + 사각 블록 튐 (피격용보다 훨씬 볼드하게)
//  - 색은 밝기만 남겨 채도가 낮은 회붉은색으로 누르고, 굵은 알갱이·진한 가로줄·회색 찢김 선을 얹음
// HitGlitchScreenFeature(렌더러 기능)가 화면을 복사해서 이 셰이더로 다시 그린다.
// _Intensity / _Seed / _EffectTime 은 LowHpGlitchEffect 스크립트가 넣어준다.
Shader "EventHorizon/LowHpGlitchScreen"
{
    Properties
    {
        _Intensity ("세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0
        _EffectTime ("시간 (스크립트가 조절)", Float) = 0

        [Header(Edge)]
        _EdgeWidth ("효과가 들어오는 가장자리 폭", Range(0.02, 0.6)) = 0.14
        _EdgeNoise ("가장자리 경계 들쭉날쭉", Range(0, 0.3)) = 0.07
        _EdgeBlock ("경계를 블록 모양으로 깎는 크기 (칸 수)", Range(4, 80)) = 22

        [Header(Distortion)]
        _Warp ("물결 왜곡 세기", Range(0, 0.1)) = 0.03
        _WarpFreq ("물결 촘촘함", Range(1, 60)) = 18
        _LineCount ("가는 줄 개수 (세로)", Range(20, 600)) = 180
        _Shift ("가는 줄 밀림 거리", Range(0, 0.3)) = 0.06
        _BandCount ("굵은 찢김 띠 개수 (세로)", Range(2, 60)) = 10
        _BandAmount ("굵은 찢김 띠 양", Range(0, 1)) = 0.55
        _BandShift ("굵은 찢김 밀림 거리", Range(0, 0.4)) = 0.16
        _BlockCountX ("튀는 블록 가로 칸 수", Range(2, 80)) = 24
        _BlockCountY ("튀는 블록 세로 칸 수", Range(2, 80)) = 14
        _BlockAmount ("튀는 블록 양", Range(0, 1)) = 0.35
        _BlockShift ("블록이 튀는 거리", Range(0, 0.3)) = 0.1
        _Split ("RGB 색 분리 거리", Range(0, 0.08)) = 0.025

        [Header(Color)]
        _TintColor ("물들일 색 (회붉은색)", Color) = (0.62, 0.36, 0.36, 1)
        _Tint ("물들임 정도", Range(0, 1)) = 0.85
        _Desaturate ("채도 빼기", Range(0, 1)) = 0.8
        _Darken ("어둡게", Range(0, 1)) = 0.1
        _Grain ("알갱이 양", Range(0, 1)) = 0.35
        _GrainSize ("알갱이 크기 (픽셀)", Range(1, 10)) = 3
        _Scanline ("가로줄 진하기", Range(0, 1)) = 0.5
        _TearLine ("회색 찢김 선 양", Range(0, 1)) = 0.2
        _TearColor ("찢김 선 색", Color) = (0.75, 0.68, 0.68, 1)
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
            Name "LowHpGlitchScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _Seed;
            float _EffectTime;
            float _EdgeWidth;
            float _EdgeNoise;
            float _EdgeBlock;
            float _Warp;
            float _WarpFreq;
            float _LineCount;
            float _Shift;
            float _BandCount;
            float _BandAmount;
            float _BandShift;
            float _BlockCountX;
            float _BlockCountY;
            float _BlockAmount;
            float _BlockShift;
            float _Split;
            float4 _TintColor;
            float _Tint;
            float _Desaturate;
            float _Darken;
            float _Grain;
            float _GrainSize;
            float _Scanline;
            float _TearLine;
            float4 _TearColor;

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Intensity <= 0.001) return src;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = floor(_Seed);

                // ── 1) 가장자리 마스크: 블록 모양으로 깎아서 경계도 거칠게 ──
                float2 eb = floor(uv * float2(_EdgeBlock * aspect, _EdgeBlock));
                float lineId = floor(uv.y * _LineCount);
                float bandId = floor(uv.y * _BandCount);
                float lineRand = Hash11(lineId + seed * 7.13);
                float bandRand = Hash11(bandId * 3.7 + seed * 1.91);
                float blockEdgeRand = Hash21(eb + seed * 0.37);

                float dx = min(uv.x, 1.0 - uv.x) * aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float d = min(dx, dy)
                        + (lineRand - 0.5) * _EdgeNoise
                        + (blockEdgeRand - 0.5) * _EdgeNoise * 1.5;
                float width = _EdgeWidth * (0.75 + 0.25 * _Intensity);
                float e = (1.0 - smoothstep(0.0, width, d)) * _Intensity;

                // ── 2) 물결 왜곡 (가로·세로로 크게 일렁임) ──
                float2 warp = float2(sin(uv.y * _WarpFreq + _EffectTime * 5.0 + seed * 0.01),
                                     sin(uv.x * _WarpFreq * 0.8 - _EffectTime * 4.0)) * _Warp * e;

                // ── 3) 굵은 띠 찢김 + 가는 줄 밀림 ──
                float jitter = (lineRand - 0.5) * 2.0 * _Shift;
                float bandOn = step(1.0 - _BandAmount, bandRand);
                float band = bandOn * (Hash11(bandId * 1.7 + seed) - 0.5) * 2.0 * _BandShift;
                float2 offset = float2((jitter + band) * e, 0.0) + warp;

                // ── 4) 사각 블록이 통째로 다른 자리로 튐 ──
                float2 bid = floor(uv * float2(_BlockCountX, _BlockCountY));
                float br = Hash21(bid + seed * 3.3);
                float blockOn = step(1.0 - _BlockAmount, br) * step(0.25, e);
                float2 bshift = (float2(Hash21(bid * 1.9 + seed), Hash21(bid * 2.7 + seed + 5.0)) - 0.5) * 2.0 * _BlockShift;
                offset += bshift * blockOn * e;

                // ── 5) RGB 분리 ──
                float split = _Split * e * (0.6 + 0.8 * bandRand) * (1.0 + blockOn);
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset + float2(split, 0)).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset - float2(split, 0)).b;
                float3 col = float3(r, g, b);

                // ── 6) 채도 빼고 회붉은색으로 물들이기 + 어둡게 ──
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                float3 desat = lerp(col, luma.xxx, _Desaturate);
                float3 tinted = lerp(desat, luma * _TintColor.rgb * 1.9 + _TintColor.rgb * 0.1, _Tint);
                col = lerp(col, tinted * (1.0 - _Darken), saturate(e * 1.2));

                // ── 7) 진한 가로줄 + 굵은 알갱이 + 회색 찢김 선 ──
                float scan = 1.0 - _Scanline * 0.5 * (1.0 + sin(uv.y * _ScreenParams.y * 0.9 + _EffectTime * 30.0));
                col *= lerp(1.0, scan, saturate(e));

                float2 gp = floor(uv * _ScreenParams.xy / _GrainSize);
                float grain = Hash21(gp + seed * 11.7) - 0.5;
                col += grain * _Grain * e * float3(0.9, 0.8, 0.8);

                float tear = step(1.0 - _TearLine * 0.2, Hash11(lineId * 2.31 + seed * 5.3)) * step(0.3, e);
                col = lerp(col, _TearColor.rgb, tear * e * 0.7);

                return float4(col, src.a);
            }
            ENDHLSL
        }
    }
}
