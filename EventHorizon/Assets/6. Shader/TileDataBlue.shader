// [2026-11-18 추가] 튜토리얼 타일맵 — "데이터로 만들어진 벽"
// - 타일 그림의 밝기만 살려 푸른색 두 가지(어두운색 ~ 밝은색) 사이로 다시 칠한다 (원래 색은 _KeepOriginal 로 섞을 수 있음)
// - 타일 위에만 (타일 그림의 투명한 곳은 그대로 투명) 배경 시안 E "데이터 비" + F "격자 맥박" 을 얹는다. 네모 패널은 없음.
//   · 데이터 비: 가는 세로 빛줄기가 천천히 흘러내림 (세로 줄마다 속도·위치 다름)
//   · 격자 맥박: 가는 격자선 + 일부 선을 따라 짧은 빛이 흘러감
// - 월드 좌표 기준이라 타일이 이어져도 무늬가 끊기지 않고, 카메라가 움직여도 벽에 붙어 있다.
// - 빛(2D 라이트)은 받지 않는다 (스스로 빛나는 데이터 벽). 전체 밝기는 _Intensity 로.
// - 색은 전부 재질 값 → 중간 보스 방 등에서 재질만 복사해 색만 바꿔 쓰면 됨.
// - 계산은 배경 미리보기와 같은 '감마 색'에서 하고 마지막에 선형으로 바꾼다.
Shader "EventHorizon/TileDataBlue"
{
    Properties
    {
        [PerRendererData] _MainTex ("타일 그림 (자동)", 2D) = "white" {}

        [Header(Tile Color)]
        _DarkColor ("타일 어두운 부분 색", Color) = (0.03, 0.07, 0.16, 1)
        _LightColor ("타일 밝은 부분 색", Color) = (0.30, 0.58, 0.95, 1)
        _Contrast ("명암 대비", Range(0.3, 3)) = 1.1
        _KeepOriginal ("원래 타일 색 섞기 (0 = 완전 푸른색)", Range(0, 1)) = 0
        _TileBright ("타일 밝기", Range(0, 2)) = 0.9

        [Header(Data Rain)]
        _RainAmount ("데이터 비 밝기 (0 = 끔)", Range(0, 3)) = 1
        _RainColor ("데이터 비 색", Color) = (0.55, 0.85, 1, 1)
        _RainWidth ("세로 줄 간격 (월드)", Range(0.05, 1)) = 0.22
        _RainDensity ("빛줄기가 있는 줄 비율", Range(0, 1)) = 0.45
        _RainSpeed ("떨어지는 속도", Range(0, 5)) = 0.8
        _RainLength ("빛줄기 길이 (월드)", Range(1, 40)) = 12.5

        [Header(Grid Pulse)]
        _GridAmount ("격자 맥박 밝기 (0 = 끔)", Range(0, 3)) = 1
        _GridCell ("격자 칸 크기 (월드)", Range(0.1, 4)) = 0.5
        _GridLines ("고정 격자선 보이기 (0 = 끔, 움직이는 빛만)", Range(0, 1)) = 0
        _GridLineColor ("격자선 색", Color) = (0.07, 0.16, 0.30, 1)
        _PulseColor ("흐르는 빛 색", Color) = (0.70, 0.92, 1, 1)
        _PulseSpeed ("흐르는 빛 속도", Range(0, 2)) = 0.12
        _PulseRows ("빛이 흐르는 줄 비율", Range(0, 1)) = 0.35

        [Header(Circuit Outline)]
        _EdgeAmount ("테두리 회로 빛 밝기 (0 = 끔)", Range(0, 3)) = 1
        _EdgeColor ("테두리 선 색", Color) = (0.25, 0.65, 1, 1)
        _EdgeWidth ("테두리 선 두께 (월드)", Range(0.005, 0.2)) = 0.04
        _EdgeGlow ("테두리 안쪽 번짐 (월드)", Range(0.01, 1)) = 0.18
        _TraceInset ("안쪽 회로선 거리 (월드)", Range(0.05, 0.6)) = 0.16
        _TraceAmount ("안쪽 회로선 밝기", Range(0, 2)) = 0.6
        _TraceSeg ("회로선 끊김 간격 (월드)", Range(0.3, 6)) = 1.5
        _EdgePulseColor ("테두리 따라 흐르는 빛 색", Color) = (0.75, 0.95, 1, 1)
        _EdgePulseSpeed ("흐르는 빛 속도", Range(0, 2)) = 0.25
        _EdgePulseLength ("흐르는 빛 간격 (월드)", Range(1, 30)) = 7

        [Header(Overall)]
        _Intensity ("전체 밝기", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Pass
        {
            Name "TileDataBlue"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DarkColor, _LightColor;
                float _Contrast, _KeepOriginal, _TileBright;
                float _RainAmount; float4 _RainColor; float _RainWidth, _RainDensity, _RainSpeed, _RainLength;
                float _GridAmount, _GridCell; float4 _GridLineColor, _PulseColor; float _PulseSpeed, _PulseRows;
                float _GridLines;
                float _EdgeAmount; float4 _EdgeColor; float _EdgeWidth, _EdgeGlow, _TraceInset, _TraceAmount, _TraceSeg;
                float4 _EdgePulseColor; float _EdgePulseSpeed, _EdgePulseLength;
                float _Intensity;
            CBUFFER_END

            // 타일이 있는 칸 지도 — TilemapOutlineMask 컴포넌트가 렌더러마다 넣어 줌 (없으면 테두리 효과 없음)
            TEXTURE2D(_CellMask); SAMPLER(sampler_CellMask);
            float4 _CellMaskRect;   // xy = 지도 왼쪽 아래 월드 좌표, zw = 지도 월드 크기
            float4 _CellMaskSize;   // xy = 칸 수

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V2F { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 color : COLOR; };

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float3 G(float3 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return c;
                #else
                    return LinearToSRGB(c);
                #endif
            }
            float H1(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            float4 frag (V2F i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (tex.a <= 0.001) discard;
                float T = _Time.y;
                float2 w = i.positionWS.xy;

                // ── 타일 → 푸른색 (밝기만 사용)
                float3 src = G(tex.rgb);
                float lum = dot(src, float3(0.299, 0.587, 0.114));
                float3 c = lerp(G(_DarkColor.rgb), G(_LightColor.rgb), pow(saturate(lum), _Contrast));
                c = lerp(c, src, _KeepOriginal) * _TileBright;

                // ── 데이터 비 (시안 E): 세로 줄마다 가는 빛줄기가 흘러내림
                if (_RainAmount > 0)
                {
                    float cw = _RainWidth;
                    float cx = floor(w.x / cw);
                    float fx = w.x / cw - cx;
                    float on = step(1 - _RainDensity, H1(cx));
                    float spd = _RainSpeed * (0.5 + H1(cx * 6.03 + 1.7));
                    float y = frac(w.y / _RainLength + T * spd * 0.08 + H1(cx * 0.29 + 3.1));
                    float trail = pow(y, 6) * smoothstep(1, 0.97, y);
                    float lineM = 1 - smoothstep(0, 0.18, abs(fx - 0.5));
                    c += G(_RainColor.rgb) * trail * lineM * on * _RainAmount * 0.7;
                }

                // ── 격자 맥박 (시안 F): 가는 격자선 + 일부 선을 따라 흐르는 짧은 빛
                if (_GridAmount > 0)
                {
                    float2 g = w / _GridCell;
                    float2 gf = abs(frac(g) - 0.5);
                    float2 ga = fwidth(g);
                    float lx = 1 - smoothstep(0, ga.y * 1.2, 0.5 - gf.y);   // 가로선
                    float ly = 1 - smoothstep(0, ga.x * 1.2, 0.5 - gf.x);   // 세로선
                    float rowH = H1(floor(g.y) * 7.13 + 0.5), colH = H1(floor(g.x) * 3.71 + 0.5);
                    float px = pow(frac(g.x * 0.07 - T * _PulseSpeed + rowH), 24) * step(1 - _PulseRows, rowH);
                    float py = pow(frac(g.y * 0.07 - T * _PulseSpeed * 0.85 + colH), 24) * step(1 - _PulseRows * 0.85, colH);
                    c += G(_GridLineColor.rgb) * saturate(lx + ly) * _GridAmount * _GridLines;
                    c += G(_PulseColor.rgb) * (lx * px + ly * py) * _GridAmount * 1.6;
                }

                // ── 테두리 회로 빛: 빈 칸과 맞닿은 타일 가장자리를 따라 선 + 안쪽 끊긴 회로선 + 흐르는 빛
                if (_EdgeAmount > 0 && _CellMaskSize.x > 0)
                {
                    float2 cellF = (w - _CellMaskRect.xy) / _CellMaskRect.zw * _CellMaskSize.xy;
                    float2 ci = floor(cellF), f = cellF - ci;
                    float2 cs = _CellMaskRect.zw / _CellMaskSize.xy;   // 칸 크기 (월드)
                    #define OCC(o) SAMPLE_TEXTURE2D_LOD(_CellMask, sampler_CellMask, (ci + (o) + 0.5) / _CellMaskSize.xy, 0).r
                    float L = OCC(float2(-1, 0)), R = OCC(float2(1, 0)), D = OCC(float2(0, -1)), U = OCC(float2(0, 1));
                    float d = 100;
                    if (L < 0.5) d = min(d, f.x * cs.x);
                    if (R < 0.5) d = min(d, (1 - f.x) * cs.x);
                    if (D < 0.5) d = min(d, f.y * cs.y);
                    if (U < 0.5) d = min(d, (1 - f.y) * cs.y);
                    // 안쪽으로 꺾인 모서리 (옆 두 칸은 차 있고 대각선만 빔)
                    if (L > 0.5 && D > 0.5 && OCC(float2(-1, -1)) < 0.5) d = min(d, length(f * cs));
                    if (R > 0.5 && D > 0.5 && OCC(float2(1, -1)) < 0.5) d = min(d, length(float2(1 - f.x, f.y) * cs));
                    if (L > 0.5 && U > 0.5 && OCC(float2(-1, 1)) < 0.5) d = min(d, length(float2(f.x, 1 - f.y) * cs));
                    if (R > 0.5 && U > 0.5 && OCC(float2(1, 1)) < 0.5) d = min(d, length((1 - f) * cs));
                    #undef OCC

                    if (d < 50)
                    {
                        float aa = max(max(fwidth(w.x), fwidth(w.y)), 1e-4);   // 화면 한 픽셀 크기 (d 의 미분은 칸 경계에서 튀므로 쓰지 않음)
                        float along = w.x + w.y;   // 가장자리를 따라가는 좌표 (가로·세로 모서리 모두 이어짐)
                        float edgeLine = 1 - smoothstep(_EdgeWidth, _EdgeWidth + aa, d);
                        float glow = exp(-d / _EdgeGlow);
                        float seg = step(0.35, H1(floor(along / _TraceSeg) * 1.37 + 0.2));
                        float trace = (1 - smoothstep(aa * 0.6, aa * 1.6, abs(d - _TraceInset))) * seg;
                        float pulse = pow(frac(along / _EdgePulseLength - T * _EdgePulseSpeed), 18);
                        c += G(_EdgeColor.rgb) * (edgeLine + glow * 0.3 + trace * _TraceAmount) * _EdgeAmount;
                        float tightGlow = exp(-d / (_EdgeGlow * 0.3));
                        c += G(_EdgePulseColor.rgb) * pulse * (edgeLine + tightGlow * 0.5 + trace * _TraceAmount) * _EdgeAmount * 1.5;
                    }
                }

                c = max(c * _Intensity, 0);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c = SRGBToLinear(c);
                #endif
                return float4(c, tex.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
