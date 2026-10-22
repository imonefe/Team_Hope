// 플레이어 HP 가 낮을 때(기본 20% 이하) 화면 "가장자리만" 신호가 깨진 것처럼 만드는 전체 화면 셰이더.
// [2026-10-21 시안 2A — 신호 손상 + 반투명 노이즈 + 화면 왜곡]
//  1) 화면 왜곡 (가장자리만): 천천히 흐르는 일렁임 + 가장자리가 바깥으로 휘는 렌즈 휨 + 박동마다 퍼지는 물결
//  2) 신호 손상 띠: 일부 가로 띠가 옆으로 밀리며 RGB 가 갈라짐 (_BandRate 초마다 바뀜)
//  3) 붉은 물들임 + 바깥쪽 살짝 어둡게
//  4) 반투명 노이즈 (_NoiseOpacity): 가로로 끊긴 줄무늬 + 얇은 찢김 선 + 아주 옅은 알갱이 (_NoiseRate 초마다 바뀜 — 자글자글하지 않게)
// 픽셀 크기 값은 화면 높이 1080 기준 (해상도가 달라도 비율이 같음).
// HitGlitchScreenFeature(렌더러 기능)가 화면을 복사해서 이 셰이더로 다시 그린다.
// _Intensity / _EffectTime 은 LowHpGlitchEffect 가 넣어주고, 박동 위상 _EH_LowHpBeatPhase 는 전역 값으로 넣어준다.
Shader "EventHorizon/LowHpGlitchScreen"
{
    Properties
    {
        _Intensity ("세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Seed ("무작위 씨앗 (스크립트가 조절, 지금은 안 씀)", Float) = 0
        _EffectTime ("시간 (스크립트가 조절)", Float) = 0

        [Header(Edge)]
        _EdgeWidth ("테두리 폭", Range(0.01, 0.3)) = 0.07
        _EdgeFalloff ("테두리 안쪽으로 사라지는 빠르기", Range(0.5, 4)) = 1.6
        _VignetteStart ("비네트 시작 (클수록 바깥쪽만)", Range(0.3, 1.4)) = 0.82
        _VignetteEnd ("비네트 끝", Range(0.6, 2)) = 1.32
        _VignetteDarken ("바깥쪽 어둡게", Range(0, 1)) = 0.3

        [Header(Distortion)]
        _WarpAmount ("일렁임 세기", Range(0, 0.06)) = 0.014
        _WarpSpeed ("일렁임 흐르는 속도", Range(0, 3)) = 1
        _Barrel ("렌즈 휨 (가장자리가 바깥으로 휨)", Range(0, 0.15)) = 0.035
        _Ripple ("박동 물결 세기", Range(0, 0.04)) = 0.006
        _RippleWidth ("박동 물결 두께", Range(0.01, 0.2)) = 0.05

        [Header(Signal Bands)]
        _BandCount ("띠 개수 (세로)", Range(4, 80)) = 22
        _BandChance ("밀리는 띠 비율", Range(0, 1)) = 0.45
        _BandShift ("띠가 밀리는 최대 거리", Range(0, 0.3)) = 0.078
        _BandSplitMin ("띠 RGB 갈라짐 최소", Range(0, 0.05)) = 0.006
        _BandSplitMax ("띠 RGB 갈라짐 최대", Range(0, 0.05)) = 0.016
        _BandRate ("띠가 바뀌는 간격 (초)", Range(0.05, 2)) = 0.3
        _BandLine ("띠 윗선 밝기", Range(0, 1)) = 0.5

        [Header(Color)]
        _TintColor ("물들일 색", Color) = (1, 0.12, 0.16, 1)
        _Tint ("물들임 정도", Range(0, 1)) = 0.85

        [Header(Noise)]
        _NoiseOpacity ("노이즈 불투명도", Range(0, 1)) = 0.5
        _NoiseRate ("노이즈 무늬가 바뀌는 간격 (초)", Range(0.03, 1)) = 0.15
        _StreakAmount ("줄무늬 양", Range(0, 1)) = 0.1
        _StreakBright ("줄무늬 밝기", Range(0, 2)) = 0.35
        _StreakWhite ("줄무늬 중 하얀 것 비율", Range(0, 1)) = 0.2
        _StreakHeight ("줄무늬 두께 (픽셀)", Range(1, 40)) = 9
        _StreakLength ("줄무늬 길이 (픽셀)", Range(10, 800)) = 180
        _TearAmount ("찢김 선 양", Range(0, 0.3)) = 0.03
        _TearHeight ("찢김 선 두께 (픽셀)", Range(1, 30)) = 6
        _TearColor ("찢김 선 색", Color) = (1, 0.8, 0.8, 1)
        _Grain ("알갱이 세기", Range(0, 0.5)) = 0.05
        _GrainWidth ("알갱이 가로 (픽셀)", Range(1, 60)) = 18
        _GrainHeight ("알갱이 세로 (픽셀)", Range(1, 30)) = 6
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
            float _EH_LowHpBeatPhase;   // 전역 값: 0 = 박동 순간, 1 까지 늘어났다가 다음 박동에서 0

            float _EdgeWidth;
            float _EdgeFalloff;
            float _VignetteStart;
            float _VignetteEnd;
            float _VignetteDarken;
            float _WarpAmount;
            float _WarpSpeed;
            float _Barrel;
            float _Ripple;
            float _RippleWidth;
            float _BandCount;
            float _BandChance;
            float _BandShift;
            float _BandSplitMin;
            float _BandSplitMax;
            float _BandRate;
            float _BandLine;
            float4 _TintColor;
            float _Tint;
            float _NoiseOpacity;
            float _NoiseRate;
            float _StreakAmount;
            float _StreakBright;
            float _StreakWhite;
            float _StreakHeight;
            float _StreakLength;
            float _TearAmount;
            float _TearHeight;
            float4 _TearColor;
            float _Grain;
            float _GrainWidth;
            float _GrainHeight;

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 부드러운 값 노이즈 (-1 ~ 1)
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * 2.0 - 1.0;
            }

            float3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Intensity <= 0.001) return src;

                float I = _Intensity;
                float t = _EffectTime;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float pxScale = _ScreenParams.y / 1080.0;
                float2 px = uv * _ScreenParams.xy / pxScale;          // 1080 기준 픽셀 좌표

                // ── 가장자리 마스크 ──
                float2 c = uv - 0.5;
                float rv = length(c / 0.5);                              // 타원 반지름 (가운데 0, 모서리 약 1.41)
                float VG = smoothstep(_VignetteStart, _VignetteEnd, rv) * I;
                float d = min(min(uv.x, 1.0 - uv.x) * aspect, min(uv.y, 1.0 - uv.y));
                float EG = pow(saturate(1.0 - smoothstep(0.0, _EdgeWidth, d)), _EdgeFalloff) * I;

                // ── 1) 화면 왜곡 ──
                float wm = saturate(VG * 1.2 + EG * 0.8);
                float ws = t * _WarpSpeed;
                float n1 = VNoise(float2(uv.x * 38.0 + ws * 0.77, uv.y * 23.0 + ws * 0.38));
                float n2 = VNoise(float2(uv.x * 50.0 - ws * 0.9, uv.y * 39.0 + 7.3));
                float2 warp = float2((sin(uv.y * 14.0 + ws * 2.4) * 0.6 + n1),
                                     (sin(uv.x * 9.0 - ws * 1.9) * 0.3 + n2 * 0.6) * 0.6) * _WarpAmount * wm;

                float2 barrel = c * _Barrel * VG;

                float r = length(float2(c.x * aspect, c.y)) + 1e-5;
                float ph = saturate(_EH_LowHpBeatPhase);
                float front = 0.35 + ph * 0.9;
                float rd = (r - front) / _RippleWidth;
                float rip = exp(-rd * rd) * exp(-ph * 2.5) * _Ripple * saturate(VG * 1.5 + EG);
                float2 ripple = c / r * rip;

                float2 duv = uv + warp - barrel + ripple;
                float3 col = SampleScreen(duv);

                // ── 2) 신호 손상 띠 ──
                float m = saturate(EG * 1.2);
                float bandSeed = floor(t / _BandRate) * 17.31;
                float bandId = floor(uv.y * _BandCount);
                float bandOn = step(1.0 - _BandChance, Hash21(float2(bandId, bandSeed)));
                float shift = (Hash21(float2(bandId * 1.7, bandSeed + 3.1)) - 0.5) * 2.0 * _BandShift;
                float sp = lerp(_BandSplitMin, _BandSplitMax, Hash21(float2(bandId * 2.3, bandSeed + 5.7)));
                float3 bandCol = float3(SampleScreen(duv + float2(shift + sp, 0)).r,
                                        SampleScreen(duv + float2(shift, 0)).g,
                                        SampleScreen(duv + float2(shift - sp, 0)).b);
                float kb = bandOn * saturate(m * 3.0);
                col = lerp(col, bandCol, kb);
                // 띠 맨 위 2픽셀에 밝은 선
                float bandTop = step(1.0 - (2.0 * pxScale) / (_ScreenParams.y / _BandCount), frac(uv.y * _BandCount));
                col += float3(1.0, 0.55, 0.55) * bandOn * bandTop * saturate(m * 2.5) * _BandLine * _NoiseOpacity;

                // ── 3) 붉은 물들임 ──
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                float3 tinted = (col * 0.5 + luma * 0.5) * _TintColor.rgb * 1.7 + _TintColor.rgb * 0.05;
                col = lerp(col, tinted, saturate(m * _Tint));

                // ── 4) 반투명 노이즈 ──
                float ns = floor(t / _NoiseRate) * 13.7;
                float row = floor(px.y / _StreakHeight);
                float rowShift = Hash21(float2(row * 1.31, ns)) * _StreakLength;
                float cellX = floor((px.x + rowShift) / _StreakLength);
                float on = step(1.0 - _StreakAmount, Hash21(float2(cellX + ns, row * 0.77 + ns)));
                float fx = frac((px.x + rowShift) / _StreakLength);
                float taper = smoothstep(0.0, 0.2, fx) * (1.0 - smoothstep(0.75, 1.0, fx));
                float sv = on * taper * (0.5 + 0.5 * Hash21(float2(cellX * 1.7, row + ns)));
                float isWhite = step(1.0 - _StreakWhite, Hash21(float2(row + ns * 2.3, cellX)));
                float3 sc = lerp(_TintColor.rgb * 1.6, float3(1, 1, 1), isWhite);
                col += sc * sv * _StreakBright * _NoiseOpacity * saturate(m * 1.6);

                float tearRow = floor(px.y / _TearHeight);
                float tear = step(1.0 - _TearAmount, Hash21(float2(tearRow * 2.31, ns * 5.3))) * step(0.3, m);
                float ta = tear * m * 0.6 * _NoiseOpacity;
                col = lerp(col, _TearColor.rgb, ta);

                float g = (Hash21(float2(floor(px.x / _GrainWidth), floor(px.y / _GrainHeight) + ns)) - 0.5) * _Grain * _NoiseOpacity;
                col += g * saturate(m * 1.5) * float3(1.0, 0.6, 0.6);

                col *= 1.0 - _VignetteDarken * VG;
                return float4(col, src.a);
            }
            ENDHLSL
        }
    }
}
