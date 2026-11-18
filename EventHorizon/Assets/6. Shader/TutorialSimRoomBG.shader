// [2026-11-18 추가] 튜토리얼 배경 — "시뮬레이션 방 + 부팅 글리프" (미리보기 시안의 혼합 3: 벽 + 바닥)
// - 바닥·천장 격자가 화면 가운데 지평선 빛 한 줄로 모이고, 격자는 천천히 앞으로 다가온다.
// - 지평선 쪽 먼 벽에 작은 비트맵 글자가 위로 흐른다 (지평선에 가까울수록 진함 = 먼 곳이 밝음).
// - 바닥·천장 격자 칸 안에도 작은 글자가 새겨져 격자와 함께 다가온다 (먼 칸일수록 많고 밝음, 뭉개질 만큼 먼 곳은 사라짐).
// - 화면 좌표 기준으로 그린다 (판 크기·위치와 상관없이 화면에 고정). 카메라가 옆으로 움직이면 바닥 격자가 따라 살짝 밀린다.
// - 계산은 미리보기와 같은 '감마 색'에서 하고 마지막에 선형으로 바꾼다 → 브라우저 미리보기와 같은 밝기.
// - 노이즈: 가끔 가는 가로 띠가 흐리게 지나감 + 아주 옅은 입자.
// - 기존 TutorialCircuitBG 셰이더는 다른 재질도 쓰므로 건드리지 않고 새 파일로 만듦.
Shader "EventHorizon/TutorialSimRoomBG"
{
    Properties
    {
        [Header(Room)]
        _Horizon ("지평선 높이 (화면 비율, 0 = 아래)", Range(0.1, 0.9)) = 0.5
        _Perspective ("원근 깊이", Range(0.05, 0.6)) = 0.22
        _GridDensity ("격자 가로 촘촘함", Range(2, 20)) = 9
        _DepthDensity ("격자 깊이 촘촘함", Range(0.5, 5)) = 2.2
        _Speed ("격자가 다가오는 속도", Range(0, 3)) = 0.6
        _CamFollow ("카메라 따라 격자 밀림", Range(0, 1)) = 0.15
        _LineBright ("격자선 밝기", Range(0, 2)) = 0.7
        _CeilBright ("천장 밝기 (바닥 대비)", Range(0, 1)) = 0.45
        _FogDist ("먼 곳 흐려짐", Range(0.01, 0.5)) = 0.09
        _RunChance ("빛이 흐르는 세로선 비율", Range(0, 0.5)) = 0.14
        _RunBright ("흐르는 빛 밝기", Range(0, 2)) = 0.35

        [Header(Horizon)]
        _HorizonGlow ("지평선 번짐", Range(0, 1)) = 0.28
        _HorizonCore ("지평선 가운데 흰 줄", Range(0, 1)) = 0.1
        _HorizonWide ("지평선 넓은 번짐", Range(0, 0.5)) = 0.06

        [Header(Glyph Wall)]
        _WallAmount ("글자 벽 밝기 (0 = 끔)", Range(0, 2)) = 0.8
        _WallBand ("글자 벽 높이 (지평선 위아래)", Range(0.01, 0.3)) = 0.07
        _WallSize ("글자 크기 (화면 높이 비율)", Range(0.005, 0.04)) = 0.011
        _WallSpeed ("글자 흐르는 속도", Range(0, 0.05)) = 0.006
        _WallDensity ("글자 줄 비율", Range(0, 1)) = 0.55

        [Header(Glyph Floor)]
        _FloorGlyph ("바닥 글자 밝기 (0 = 끔)", Range(0, 2)) = 0.8
        _FloorGlyphChance ("바닥 글자 비율", Range(0, 0.5)) = 0.14

        [Header(Color)]
        _BgColor ("바탕", Color) = (0.028, 0.034, 0.044, 1)
        _LineColor ("선·글자", Color) = (0.5, 0.58, 0.68, 1)
        _HotColor ("흰 빛", Color) = (0.86, 0.9, 0.95, 1)
        _Vignette ("가장자리 어둡게", Range(0, 1)) = 0.5
        _Intensity ("전체 밝기", Range(0, 2)) = 1

        [Header(Noise)]
        _GlitchInterval ("노이즈 주기 (초)", Range(0.5, 20)) = 6
        _GlitchChance ("주기마다 노이즈가 날 확률", Range(0, 1)) = 0.4
        _GlitchStrength ("노이즈 밝기", Range(0, 0.5)) = 0.06
        _Grain ("입자", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "SimRoomBG"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Horizon, _Perspective, _GridDensity, _DepthDensity, _Speed, _CamFollow, _LineBright, _CeilBright, _FogDist, _RunChance, _RunBright;
                float _HorizonGlow, _HorizonCore, _HorizonWide;
                float _WallAmount, _WallBand, _WallSize, _WallSpeed, _WallDensity;
                float _FloorGlyph, _FloorGlyphChance;
                float4 _BgColor, _LineColor, _HotColor;
                float _Vignette, _Intensity;
                float _GlitchInterval, _GlitchChance, _GlitchStrength, _Grain;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V2F { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; float4 color : COLOR; };

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                o.color = v.color;
                return o;
            }

            // 재질 색은 선형으로 들어오므로 미리보기와 같은 감마 색으로 되돌려 계산
            float3 G(float4 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return c.rgb;
                #else
                    return LinearToSRGB(c.rgb);
                #endif
            }

            float H21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(H21(i), H21(i + float2(1, 0)), f.x), lerp(H21(i + float2(0, 1)), H21(i + float2(1, 1)), f.x), f.y);
            }

            // 5×5 비트맵 글자 (좌우 대칭). f = 칸 안 좌표 0~1
            float Glyph(float2 f, float2 id)
            {
                float2 b = floor(f * 5);
                if (b.x < 0 || b.y < 0 || b.x > 4 || b.y > 4) return 0;
                float bx = b.x > 2 ? 4 - b.x : b.x;
                return step(0.5, H21(id * 13.7 + float2(bx, b.y) * 1.31));
            }

            float4 frag (V2F i) : SV_Target
            {
                float T = _Time.y;
                float2 uv = i.screen.xy / max(i.screen.w, 1e-4);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float camX = _WorldSpaceCameraPos.x * _CamFollow;

                float3 BG = G(_BgColor), LINE = G(_LineColor), HOT = G(_HotColor);
                float3 c = BG;

                // ── 바닥·천장 격자
                float dy = uv.y - _Horizon;
                float ay = abs(dy) + 1e-4;
                float z = _Perspective / ay;                                   // 깊이
                float2 g = float2((uv.x - 0.5) * z * _GridDensity + camX, z * _DepthDensity + T * _Speed);
                float2 fw = fwidth(g);
                float2 gl = abs(frac(g) - 0.5);
                float lx = 1 - smoothstep(0, fw.x * 1.3, 0.5 - gl.x);
                float l = max(lx, 1 - smoothstep(0, fw.y * 1.3, 0.5 - gl.y));
                float fog = exp(-z * _FogDist);
                float farK = 1 - fog;                                           // 멀수록 1
                float ceilK = dy > 0 ? _CeilBright : 1;
                c += LINE * l * _LineBright * ceilK * lerp(0.3, 1, farK) * smoothstep(70, 6, z);

                // 일부 세로선을 따라 지평선 쪽으로 흐르는 빛
                float colI = floor(g.x);
                float on = step(1 - _RunChance, H21(float2(colI, 3)));
                float run = pow(frac(-g.y * 0.05 + H21(float2(colI, 7)) + T * 0.05), 30);
                c += HOT * run * on * step(0.5, lx) * _RunBright * ceilK * farK;

                // ── 바닥·천장 글자 (격자 한 칸에 3×3 자리)
                if (_FloorGlyph > 0)
                {
                    float2 gq = g * 3;
                    float2 id = floor(gq), f = frac(gq);
                    float2 fq = fwidth(gq);
                    float side = dy > 0 ? 1 : 0;
                    float gOn = step(1 - _FloorGlyphChance - 0.22 * farK, H21(id + float2(5, side * 9)));
                    float gly = Glyph((f - 0.12) / 0.76, id * 1.7 + side * 3);
                    float sharp = 1 - smoothstep(0.14, 0.32, max(fq.x, fq.y));     // 뭉개질 만큼 먼 곳은 사라짐
                    float tw = 0.7 + 0.3 * sin(T * 1.3 + H21(id) * 6.28);
                    c += LINE * gly * gOn * sharp * lerp(0.16, 0.55, farK) * ceilK * tw * _FloorGlyph;
                }

                // ── 지평선 빛
                c += LINE * exp(-ay * ay / 0.0008) * _HorizonGlow + HOT * exp(-ay * ay / 0.00006) * _HorizonCore;
                c += LINE * exp(-ay / 0.06) * _HorizonWide;

                // ── 지평선 쪽 글자 벽 (두 겹)
                if (_WallAmount > 0)
                {
                    float2 p = float2(uv.x * aspect, uv.y);
                    float band = exp(-ay / _WallBand);
                    [unroll] for (int k = 0; k < 2; k++)
                    {
                        float fk = k;
                        float cell = _WallSize * lerp(1, 1.55, fk);
                        float2 q = p / cell;
                        q.y -= T * _WallSpeed * lerp(1, 2, fk) / cell;
                        float colW = floor(q.x);
                        q.y += H21(float2(colW, fk + 4)) * 40;
                        float2 id = floor(q), f = frac(q);
                        float lineOn = step(1 - _WallDensity + fk * 0.15, H21(float2(id.y, colW * 0.13 + fk + 2)));
                        float blockOn = step(0.5, VNoise(float2(colW * 0.3, id.y * 0.1) + fk * 9 + 3));
                        float gg = Glyph((f - 0.1) / 0.8, id + fk * 31 + 7) * lineOn * blockOn;
                        float cur = step(0.985, H21(float2(id.y, floor(T * 0.5) + fk + 5)));   // 가끔 한 줄이 반짝
                        c += LINE * gg * band * lerp(0.55, 0.3, fk) * (1 + cur * 1.4) * _WallAmount;
                    }
                }

                // ── 노이즈 살짝
                float gi = max(_GlitchInterval, 0.1);
                float bph = frac(T / gi) * gi;
                float burst = step(H21(float2(floor(T / gi), 3)), _GlitchChance) * smoothstep(0, 0.04, bph) * (1 - smoothstep(0.25, 0.4, bph));
                float fid = floor(T * 30);
                float by = H21(float2(fid, 1));
                float bandN = burst * (1 - smoothstep(0.02, 0.035, abs(uv.y - by)));
                c += LINE * bandN * _GlitchStrength * step(0.5, H21(float2(floor(uv.x * 40), fid)));
                c += (H21(uv * _ScreenParams.xy + frac(T) * 100) - 0.5) * _Grain;

                // ── 마감
                c *= 1 - _Vignette * saturate(dot(uv - 0.5, uv - 0.5) * 2);
                c = max(c, 0) * _Intensity;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c = SRGBToLinear(c);
                #endif
                return float4(c * i.color.rgb, i.color.a);
            }
            ENDHLSL
        }
    }
}
