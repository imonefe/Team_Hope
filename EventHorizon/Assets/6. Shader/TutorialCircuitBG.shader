// [2026-11-16 추가 / 같은 날 2차 수정] 튜토리얼 배경 — "기기가 켜지기 전 시뮬레이션" 회로판.
// - 그림 파일 없이 코드로 회로선을 그린다. 바탕은 단색 어둠, 빛은 회로선(과 선 바로 옆 얕은 번짐)에만.
// - 빛의 물결이 위에서 아래로 내려가며 회로선을 켰다가 서서히 꺼뜨리기를 반복한다.
// - 회로 모양 4종 (_Style): 직교 격자 / 45° 사선 / 평행 버스 / 육각 벌집.
// - 회로 모양은 월드 좌표 기준(판을 늘려도 선이 늘어나지 않음), 물결 위치는 판의 세로 위치(uv.y) 기준.
// - 패스에 LightMode 를 적지 않아 2D 렌더러·3D 렌더러 양쪽에서 그려진다.
Shader "EventHorizon/TutorialCircuitBG"
{
    Properties
    {
        [Header(Pattern)]
        [KeywordEnum(Grid, Diagonal, Bus, Hex, Hybrid, Data)] _Style ("회로 모양", Float) = 0
        _CellSize ("칸 크기 (월드)", Float) = 1.5
        _LineWidth ("선 굵기 (칸 비율)", Range(0.005, 0.15)) = 0.04
        _Density ("회로 밀도", Range(0, 1)) = 0.45
        _Seed ("무늬 씨앗", Float) = 7
        _MainRatio ("두꺼운 주 회로 비율", Range(0, 1)) = 0.3
        _ChipChance ("칩 부품 비율", Range(0, 1)) = 0.3

        [Header(Hybrid Grid Plus Hex)]
        _HexScale ("벌집 크기 (격자 칸 배수)", Range(1, 8)) = 3
        _HexDensity ("벌집 변 남기는 비율", Range(0, 1)) = 0.55
        _HexGap ("(사용 안 함) 벌집 꼭짓점 끊김", Range(0, 0.3)) = 0
        _HexPatch ("벌집 덩어리 크기 (작을수록 큼)", Range(0.02, 0.6)) = 0.12
        _HexFade ("벌집 가장자리 흐려짐 폭", Range(0.02, 0.5)) = 0.18
        _MainWidth ("벌집 주 회로선 굵기 (배수)", Range(1, 4)) = 2.2
        _RailOffset ("양옆 평행선 거리 (배수)", Range(1.5, 6)) = 3
        _ThinStrength ("격자 보조선 밝기", Range(0, 1)) = 0.5

        [Header(Data Blocks)]
        _BlockDensity ("패널 밀도", Range(0, 1)) = 0.7
        _DotSpacing ("창문 점 간격 (월드)", Range(0.05, 0.5)) = 0.14
        _DotDensity ("창문 점 켜진 비율", Range(0, 1)) = 0.35
        _LitChance ("통째로 켜진 패널 비율", Range(0, 0.5)) = 0.07
        _FlickerRate ("켜진 패널 바뀌는 빠르기", Range(0, 5)) = 0.8
        _PanelGlow ("켜진 패널 번짐", Range(0, 2)) = 0.8
        _ScanGlitch ("스캔 띠 가로 찢어짐", Range(0, 1)) = 0.35
        _Parallax ("겹마다 시차", Range(0, 1)) = 0.6

        [Header(Depth)]
        _BackScale ("뒤 회로층 촘촘함", Range(1, 4)) = 1.9
        _BackStrength ("뒤 회로층 밝기", Range(0, 1)) = 0.3
        _BackDelay ("뒤 회로층 물결 늦음", Range(0, 0.3)) = 0.06

        [Header(Wave)]
        _Period ("물결 주기 (초)", Float) = 6
        _Fade ("꺼지는 속도 (클수록 빨리)", Range(0.5, 12)) = 8
        _Stagger ("회로마다 어긋남", Range(0, 0.5)) = 0.03
        _HeadWidth ("앞머리 빛줄기 굵기", Range(0.001, 0.1)) = 0.012
        _PacketSpeed ("데이터 흐름 속도", Range(0, 10)) = 3
        _PacketAmount ("데이터 흐름 밝기", Range(0, 3)) = 1.2
        _BootFlicker ("점화 깜빡임", Range(0, 1)) = 0.7
        [HideInInspector] _PhaseOverride ("캡처용 물결 위치 고정 (-1 = 끔)", Float) = -1
        [HideInInspector] _TimeOverride ("캡처용 시간 고정 (-1 = 끔)", Float) = -1

        [Header(Color)]
        _BgColor ("바탕 (단색)", Color) = (0.008, 0.012, 0.02, 1)
        _IdleColor ("꺼진 회로선", Color) = (0.03, 0.07, 0.09, 1)
        [HDR] _GlowColor ("켜진 회로선", Color) = (0.25, 1.4, 1.8, 1)
        [HDR] _HeadColor ("앞머리 빛", Color) = (1.2, 2.2, 2.6, 1)
        _Halo ("선 옆 번짐", Range(0, 1)) = 0.35
        _HotCore ("선 가운데 흰빛", Range(0, 1)) = 0.6
        _Vignette ("가장자리 어둡게", Range(0, 1)) = 0.35
        _Intensity ("전체 밝기", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "CircuitBG"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _STYLE_GRID _STYLE_DIAGONAL _STYLE_BUS _STYLE_HEX _STYLE_HYBRID _STYLE_DATA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Style, _CellSize, _LineWidth, _Density, _Seed;
                float _Period, _Fade, _Stagger, _HeadWidth, _PhaseOverride;
                float4 _BgColor, _IdleColor, _GlowColor, _HeadColor;
                float _Halo, _Intensity;
                float _MainRatio, _ChipChance, _BackScale, _BackStrength, _BackDelay;
                float _PacketSpeed, _PacketAmount, _BootFlicker, _TimeOverride, _HotCore, _Vignette;
                float _HexScale, _HexDensity, _HexGap, _MainWidth, _RailOffset, _ThinStrength, _HexPatch, _HexFade;
                float _BlockDensity, _DotSpacing, _DotDensity, _LitChance, _FlickerRate, _PanelGlow, _ScanGlitch, _Parallax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V2F { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; float4 color : COLOR; float4 screen : TEXCOORD2; };

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.world = TransformObjectToWorld(v.positionOS.xyz).xy;
                o.color = v.color;
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            static float gLayerSeed;   // 앞층 0, 뒤층 다른 값 → 같은 함수로 다른 무늬

            float Hash(float2 c, float k)
            {
                return frac(sin(dot(c + _Seed + gLayerSeed, float2(127.1, 311.7)) + k * 74.7) * 43758.5453);
            }

            float SdBox(float2 p, float2 b)
            {
                float2 d = abs(p) - b;
                return length(max(d, 0)) + min(max(d.x, d.y), 0);
            }

            // 칩 부품: 큰 칸(7×5)마다 확률로 사각 칩 하나. (외곽·다리까지 거리, 칩 안쪽 여부, 번호)
            float3 Chips(float2 p)
            {
                const float2 cs = float2(7, 5);
                float2 k = floor(p / cs);
                if (Hash(k, 20) > _ChipChance) return float3(10, 0, 0);
                float2 hs = float2(1.0 + floor(Hash(k, 21) * 3) * 0.5, 0.5 + floor(Hash(k, 22) * 2) * 0.5);
                float2 c = floor((k + 0.5) * cs + (float2(Hash(k, 23), Hash(k, 24)) - 0.5) * (cs - 2 * hs - 2)) + 0.5;
                float2 q = p - c;
                float sd = SdBox(q, hs) - 0.1;
                float outline = abs(sd);
                float pinY = abs(q.y) - (hs.y + 0.1);                       // 위·아래 변 바깥
                float px = abs(q.x - round(q.x * 2) * 0.5);                 // 다리 간격 0.5
                float pin = (abs(q.x) < hs.x - 0.15 && pinY > 0 && pinY < 0.45) ? max(px, 0) : 10;
                float mark = abs(SdBox(q, hs * 0.45));                      // 칩 위 안쪽 사각 표시
                return float3(min(min(outline, pin), sd < 0 ? mark + 0.02 : 10), sd < 0 ? 1 : 0, Hash(k, 25));
            }

            // 점 p 에서 선분 a-b 까지 거리
            float Seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // 가장 가까운 회로선의 방향 (데이터 흐름이 선을 따라 움직이게). 방향이 없는 곳(칩 외곽 등)은 0
            static float2 gTangent;
            static float gSegPerp, gSegH, gSegLen;   // 가장 가까운 변: 선까지 수직 거리 / 변 위 위치(0~1, 범위 밖이면 끝 바깥) / 변 길이

            // 선분 a-b 가 지금까지보다 가까우면 거리와 방향을 갱신
            void TrySeg(float2 f, float2 a, float2 b, inout float d)
            {
                float s = Seg(f, a, b);
                if (s < d) { d = s; gTangent = normalize(b - a); }
            }

            // 각 모양은 (회로선까지 거리[칸 단위], 회로 번호[0~1]) 를 돌려준다

            // A 직교 격자: 칸 가운데를 오른쪽·위쪽 이웃과 잇는다 (이웃도 같은 판단 → 이어짐)
            float2 StyleGrid(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                float d = 10;
                if (Hash(c, 1) < _Density) TrySeg(f, float2(0.5, 0.5), float2(1, 0.5), d);
                if (Hash(c - float2(1, 0), 1) < _Density) TrySeg(f, float2(0, 0.5), float2(0.5, 0.5), d);
                if (Hash(c, 2) < _Density) TrySeg(f, float2(0.5, 0.5), float2(0.5, 1), d);
                if (Hash(c - float2(0, 1), 2) < _Density) TrySeg(f, float2(0.5, 0), float2(0.5, 0.5), d);
                return float2(d, Hash(c, 4));
            }

            // B 45° 사선: 칸마다 변의 가운데끼리 잇는 조각 (사선 2종 · 가로 · 세로) → 기판처럼 45°로 꺾인다
            float2 StyleDiagonal(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                float2 L = float2(0, 0.5), R = float2(1, 0.5), T = float2(0.5, 1), B = float2(0.5, 0);
                float t = Hash(c, 5);
                bool k1 = Hash(c, 6) < _Density + 0.25, k2 = Hash(c, 7) < _Density;
                float d = 10;
                if (t < 0.28)      { if (k1) TrySeg(f, L, T, d); if (k2) TrySeg(f, B, R, d); }
                else if (t < 0.56) { if (k1) TrySeg(f, L, B, d); if (k2) TrySeg(f, T, R, d); }
                else if (t < 0.86) { if (k1) TrySeg(f, L, R, d); }
                else               { if (k1) TrySeg(f, B, T, d); }
                return float2(d, Hash(c, 4));
            }

            // C 평행 버스: 4가닥 묶음 가로 줄기 (가닥마다 길이가 다르게 끊김) + 묶음 사이 세로 연결선
            float2 StyleBus(float2 p)
            {
                const float bandH = 4.0, gap = 0.3, spacing = 0.45, first = 0.6, n = 4;
                float b = floor(p.y / bandH);
                float ly = p.y - b * bandH;
                float k = clamp(round((ly - first) / spacing), 0, n - 1);
                float dy = abs(ly - (first + k * spacing));
                float2 lineKey = float2(b, k);
                float len = 3 + floor(Hash(lineKey, 8) * 7);
                float off = Hash(lineKey, 9) * len;
                float s = floor((p.x + off) / len);
                float fx = (p.x + off) - s * len;
                float dx = max(0, max(gap - fx, fx - (len - gap)));
                float d = Hash(float2(b * 7 + k, s), 10) < _Density + 0.3 ? length(float2(dx, dy)) : 10;
                if (d < 10) gTangent = float2(1, 0);
                float id = Hash(lineKey, 13);

                // 세로 연결선: 이 묶음과 아래 묶음 양쪽 것을 모두 검사 (경계에서 끊기지 않게)
                for (int db = -1; db <= 0; db++)
                {
                    float bb = b + db;
                    float cx = floor(p.x / 3);
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        float2 key = float2(cx + dc, bb);
                        if (Hash(key, 12) > 0.3) continue;
                        float xr = (cx + dc) * 3 + 1.5 + (Hash(key, 11) - 0.5) * 1.5;
                        float y0 = bb * bandH + first + (n - 1) * spacing;
                        float y1 = (bb + 1) * bandH + first;
                        float dv = Seg(p, float2(xr, y0), float2(xr, y1));
                        if (dv < d) { d = dv; id = Hash(key, 14); gTangent = float2(0, 1); }
                    }
                }
                return float2(d, id);
            }

            // D 육각 벌집: 육각 격자의 변을 무작위로 남긴다 (맞닿은 두 칸이 같은 변을 같은 판단)
            float2 StyleHexD(float2 p, float density, float gap)
            {
                const float2 s = float2(1, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1)) / s.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float2 hl = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;   // 칸 가운데 기준 좌표
                float2 cen = p - hl;
                const float R = 0.57735;   // 꼭짓점까지 거리 (변까지 0.5)
                float d = 10, id = 0;
                [unroll] for (int e = 0; e < 6; e++)
                {
                    float a = radians(60.0 * e);
                    float2 nrm = float2(cos(a), sin(a));
                    float2 key = round((cen + nrm * 0.5) * 4);      // 변 가운데 (양쪽 칸이 같은 값)
                    if (Hash(key, 15) > density) continue;
                    float2 c0 = float2(cos(a - radians(30.0)), sin(a - radians(30.0))) * R;
                    float2 c1 = float2(cos(a + radians(30.0)), sin(a + radians(30.0))) * R;
                    float2 e0 = lerp(c0, c1, gap), e1 = lerp(c1, c0, gap);     // 꼭짓점에서 짧게 끊음 (마디감)
                    float de = Seg(hl, e0, e1);
                    if (de < d)
                    {
                        d = de; id = Hash(key, 16); gTangent = normalize(c1 - c0);
                        float2 ev = e1 - e0;
                        gSegLen = length(ev);
                        gSegH = dot(hl - e0, ev) / (gSegLen * gSegLen);
                        gSegPerp = abs(dot(hl - e0, float2(-ev.y, ev.x)) / gSegLen);
                    }
                }
                return float2(d, id);
            }

            // 부드러운 값 노이즈 (0~1)
            float VNoise(float2 x)
            {
                float2 i0 = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                float a = Hash(i0, 40), b = Hash(i0 + float2(1, 0), 40), c = Hash(i0 + float2(0, 1), 40), d = Hash(i0 + float2(1, 1), 40);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 변 하나의 세기: 노이즈 얼룩 안 = 1, 가장자리에서 서서히 0 (변을 무작위로 빼지 않음 → 끊김 없음)
            float EdgeWeight(float2 mid)
            {
                if (_HexDensity <= 0.001) return 0;   // 벌집 끔
                float n = VNoise(mid * _HexPatch) * 0.75 + VNoise(mid * _HexPatch * 2.7 + 5.1) * 0.25;
                float th = 1 - _HexDensity;
                return smoothstep(th - _HexFade * 0.5, th + _HexFade * 0.5, n);
            }

            // [2026-11-16 5차] 이어진 벌집 망: 내 칸의 변 6개 + 꼭짓점에서 바깥으로 뻗는 이웃 변 6개를 함께 계산
            // → 꼭짓점의 세 갈래가 매끈하게 이어지고 칸 경계에서 잘리지 않는다.
            // 반환: (망까지 거리[벌집 단위], 가장 가까운 변의 세기 0~1). gTangent = 그 변의 방향
            float2 HexNet(float2 p)
            {
                const float2 s = float2(1, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1)) / s.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float2 hl = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;
                float2 cen = p - hl;
                const float R = 0.57735;
                float d = 10, wgt = 0;
                [unroll] for (int e = 0; e < 6; e++)
                {
                    // 내 칸의 변 e (꼭짓점 c0-c1)
                    float a = radians(60.0 * e);
                    float2 c0 = float2(cos(a - radians(30.0)), sin(a - radians(30.0))) * R;
                    float2 c1 = float2(cos(a + radians(30.0)), sin(a + radians(30.0))) * R;
                    float we = EdgeWeight(cen + (c0 + c1) * 0.5);
                    if (we > 0.002)
                    {
                        float de = Seg(hl, c0, c1);
                        if (de < d) { d = de; wgt = we; gTangent = normalize(c1 - c0); }
                    }
                    // 꼭짓점 c0 에서 바깥으로 뻗는 이웃 칸의 변 (c0 → 2·c0)
                    float ws = EdgeWeight(cen + c0 * 1.5);
                    if (ws > 0.002)
                    {
                        float dsp = Seg(hl, c0, c0 * 2);
                        if (dsp < d) { d = dsp; wgt = ws; gTangent = normalize(c0); }
                    }
                }
                return float2(d, wgt);
            }

            float2 StyleHex(float2 p) { return StyleHexD(p, _Density, 0); }

            // 물결 밝기 (ph = 0 앞머리, 커질수록 오래전에 지나간 곳)
            float Wave(float ph) { return pow(1 - ph, _Fade) * smoothstep(0.0, 0.01, ph); }

            // ───────── [2026-11-16 9차] 데이터 패널 — 레퍼런스 정밀 분석 반영 ─────────
            // 레퍼런스 실측: 패널끼리 대비가 아주 낮음(테두리선 없음) / 큰 덩어리를 잘게 쪼갠 계단식 직사각형 + 어두운 옆면
            //               켜진 곳은 화면의 약 3%, 덩어리 단위(ㄱ자·계단)로 켜지고 안쪽에 어두운 홈 / 0.03~0.12초 짧은 반짝임이 수시로
            //               가로 띠는 1초에 한 번쯤 0.3~0.5초 동안 매 프레임 위치가 튀는 글리치 폭발 / 그림자는 짙은 남색, 빛은 푸른 흰색

            // 칸 하나를 최대 3번 쪼갠 조각 찾기. lo·hi = 조각 범위(칸 안 좌표), grp = 첫 번째로 쪼갠 덩어리 번호
            void Split(float2 c, float2 f, float cell, out float2 lo, out float2 hi, out float code, out float grp)
            {
                lo = 0; hi = cell; code = 0; grp = 0;
                for (int k = 0; k < 3; k++)
                {
                    if (k > 0 && Hash(c + code * 7.1, 80 + k) > (k == 1 ? 0.8 : 0.55)) break;     // 2·3번째 쪼개기는 확률로
                    float2 size = hi - lo;
                    bool alongX = size.x > size.y * 1.3 ? true : (size.y > size.x * 1.3 ? false : Hash(c + code * 3.3, 84 + k) < 0.5);
                    float r = lerp(0.3, 0.7, Hash(c + code * 5.7, 87 + k));
                    if (alongX)
                    {
                        float m = lo.x + size.x * r;
                        if (f.x < m) { hi.x = m; code = code * 2; } else { lo.x = m; code = code * 2 + 1; }
                    }
                    else
                    {
                        float m = lo.y + size.y * r;
                        if (f.y < m) { hi.y = m; code = code * 2; } else { lo.y = m; code = code * 2 + 1; }
                    }
                    if (k == 0) grp = code;
                }
            }

            // 패널 한 겹 (rgb = 덮는 정도를 미리 곱한 색, a = 덮는 정도)
            float4 DataLayer(float2 p, float cell, float seed, float T, float bright, float boost, float density)
            {
                gLayerSeed = seed;
                float2 c = floor(p / cell);
                float2 f = p - c * cell;
                float aa = fwidth(p.x) * 1.2;

                float2 lo, hi; float code, grp;
                Split(c, f, cell, lo, hi, code, grp);
                float2 id = c * 8 + code;                       // 조각 번호
                float2 gid = c * 2 + grp;                       // 덩어리 번호
                // 덩어리 단위로 켜짐 (몇 초마다 바뀜). 켜진 덩어리는 조각이 비지 않고 전부 켜짐 → 번짐(GroupGlow)과 위치가 정확히 맞음
                float slot = floor(T * _FlickerRate * 0.5 + Hash(gid, 59) * 13);
                float grpLit = step(1 - _LitChance, Hash(gid + slot * 0.173, 58));
                if (Hash(id, 50) > density && grpLit < 0.5) return 0;          // 빈 조각 → 뒤 겹이 보임

                float gap = 0.035;                              // 조각 사이 틈 (월드)
                float2 hs = max((hi - lo) * 0.5 - gap, 0.01);
                float2 ctr = (lo + hi) * 0.5;
                float2 q = f - ctr;
                float sd = SdBox(q, hs);
                float inside = 1 - smoothstep(-aa, aa, sd);

                // 기본 면: 남색 안에서 명도만 조금씩 다르게 (대비 낮게)
                float tone = 0.8 + 0.45 * Hash(id, 51);
                // 돌출감: 오른쪽·아래 띠를 어둡게 (옆면)
                float side = 0.07;
                float sideMask = saturate(step(hs.x - side, q.x) + step(q.y, -hs.y + side));
                tone *= lerp(1, 0.62, sideMask);
                float3 col = _IdleColor.rgb * tone;

                // 창문 점: 일부 조각에만 무리 지어
                if (Hash(id, 52) < 0.3)
                {
                    float2 dq = (q + hs) / _DotSpacing;
                    float2 dc = floor(dq), df = frac(dq) - 0.5;
                    float on = step(Hash(dc + id * 13.1, 55), _DotDensity);
                    float tw = 0.6 + 0.4 * sin(T * (0.8 + Hash(dc, 56) * 2) + Hash(dc, 57) * 6.28);
                    float daa = aa / _DotSpacing;
                    float dotM = (1 - smoothstep(0.22 - daa, 0.22 + daa, max(abs(df.x), abs(df.y)))) * on
                               * step(-hs.x + _DotSpacing, q.x) * step(q.x, hs.x - side - _DotSpacing * 0.5)
                               * step(-hs.y + side + _DotSpacing * 0.5, q.y) * step(q.y, hs.y - _DotSpacing);
                    col += lerp(_GlowColor.rgb, _HeadColor.rgb, 0.5) * 0.45 * dotM * tw;
                }
                // 얇은 가로 막대 조명 (LED)
                if (Hash(id, 53) < 0.07)
                {
                    float barY = hs.y - 0.09;
                    float bar = (1 - smoothstep(0.018 - aa, 0.018 + aa, abs(q.y - barY))) * step(abs(q.x), hs.x * 0.7);
                    col += _HeadColor.rgb * 0.5 * bar;
                }

                float lit = grpLit;
                // 짧은 반짝임 (약 0.1초)
                float flash = step(1 - _LitChance * 0.25, Hash(id + floor(T * 9) * 0.31, 61));
                lit = max(lit, flash * 0.8);
                // 켜진 조각 안쪽의 어두운 홈 (일부)
                float recess = 0;
                if (Hash(id, 62) < 0.4)
                {
                    float2 rh = hs * float2(0.35 + Hash(id, 63) * 0.3, 0.3 + Hash(id, 64) * 0.3);
                    float2 ro = (float2(Hash(id, 65), Hash(id, 66)) - 0.5) * (hs - rh);
                    recess = 1 - smoothstep(-aa, aa, SdBox(q - ro, rh));
                }
                float3 litCol = lerp(_GlowColor.rgb, _HeadColor.rgb, 0.6) * 1.35 * (1 - recess * 0.75) * (1 - sideMask * 0.3);   // 푸른 흰색으로 확실히 빛나게
                col = lerp(col, litCol, lit);

                col *= bright * (1 + boost);
                float a = inside;

                // 켜진 조각 둘레 번짐 (칸 경계에 닿기 전 0)
                float2 db2 = min(f, cell - f);
                float halo = lit * exp(-max(sd, 0) / 0.32) * step(0, sd) * smoothstep(0.0, 0.3, min(db2.x, db2.y));   // 번짐 범위 넓게
                return float4(col * a + _GlowColor.rgb * halo * _PanelGlow * bright * (1 + boost), a);
            }

            // 켜진 덩어리의 번짐 (주변 3×3칸의 덩어리를 직접 찾아서 거리만큼 빛을 더함 → 이웃 패널 위로도 번짐)
            // 게임 블룸은 기준값 100이라 배경은 안 번지므로 셰이더 안에서 만든다. 덩어리 모양은 DataLayer 의 첫 번째 쪼개기와 같음
            float GroupGlow(float2 p, float cell, float seed, float T, float radius)
            {
                gLayerSeed = seed;
                float2 c0 = floor(p / cell);
                float g = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    float2 c = c0 + float2(dx, dy);
                    bool alongX = Hash(c, 84) < 0.5;
                    float m = lerp(0.3, 0.7, Hash(c, 87)) * cell;
                    float2 base = c * cell;
                    [unroll] for (int gi = 0; gi < 2; gi++)
                    {
                        float2 gid = c * 2 + gi;
                        float slot = floor(T * _FlickerRate * 0.5 + Hash(gid, 59) * 13);
                        if (Hash(gid + slot * 0.173, 58) < 1 - _LitChance) continue;   // 꺼진 덩어리
                        float2 lo = alongX ? float2(gi == 0 ? 0 : m, 0) : float2(0, gi == 0 ? 0 : m);
                        float2 hi = alongX ? float2(gi == 0 ? m : cell, cell) : float2(cell, gi == 0 ? m : cell);
                        float d = SdBox(p - base - (lo + hi) * 0.5, (hi - lo) * 0.5 - 0.035);
                        g += exp(-max(d, 0) / radius) * (d > 0 ? 1 : 0.6);
                    }
                }
                return g;
            }

            float4 DataFrag(V2F i, float T, float t)
            {
                float2 w = i.world;
                float2 cam = _WorldSpaceCameraPos.xy;
                float2 sp = i.screen.xy / max(i.screen.w, 1e-4);

                // 글리치 폭발: 1초 단위로 확률 75% 로 0.3~0.45초 동안, 매 프레임(1/30초) 가로 띠 위치가 튐
                float bi = floor(T), bph = frac(T);
                float burst = step(Hash(float2(bi, 3), 90), 0.75) * smoothstep(0.0, 0.04, bph) * (1 - smoothstep(0.3, 0.45, bph));
                float fid = floor(T * 30);
                float bandY = Hash(float2(fid, 1), 91);
                float bandH = 0.05 + 0.05 * Hash(float2(fid, 2), 92);
                float inBand = burst * (1 - smoothstep(bandH, bandH + 0.02, abs(sp.y - bandY)));
                float rowK = floor(sp.y * 140);
                w.x += (Hash(float2(rowK, fid), 93) - 0.5) * _ScanGlitch * 2.5 * inBand;     // 줄마다 옆으로 밀림

                // 기획서의 위→아래 물결: 아주 약하게만
                float ph = frac(t + i.uv.y);
                float sweep = Wave(ph) * 0.35;

                float boost = sweep + inBand * 2.2;
                float3 col = _BgColor.rgb;
                float4 L;
                L = DataLayer(w - cam * (1 - 0.25) * _Parallax, 1.1, 11.1, T, 0.35, boost * 0.6, 0.95);   // 뒤 (작고 어둡게)
                col = col * (1 - L.a) + L.rgb;
                float2 pm = w - cam * (1 - 0.55) * _Parallax, pf = w - cam * (1 - 0.85) * _Parallax;
                L = DataLayer(pm, 2.0, 23.7, T, 0.62, boost * 0.8, _BlockDensity + 0.15);
                col = col * (1 - L.a) + L.rgb;
                col += _GlowColor.rgb * GroupGlow(pm, 2.0, 23.7, T, 0.3) * _PanelGlow * 0.15 * (1 + boost);   // 중간 겹 번짐
                L = DataLayer(pf, 3.4, 37.3, T, 1.0, boost, _BlockDensity);      // 앞 (크고 밝게)
                col = col * (1 - L.a) + L.rgb;
                col += _GlowColor.rgb * GroupGlow(pf, 3.4, 37.3, T, 0.45) * _PanelGlow * 0.22 * (1 + boost);    // 앞 겹 번짐

                // 띠 안 가로 번짐 줄 (레퍼런스의 가로로 끌린 빛)
                // 토막 난 가로 번짐: 줄마다 화면을 6~14 토막으로 나눠 일부만 빛남 (화면 끝까지 이어진 직선 X)
                float srow = floor(sp.y * 220);
                float segN = 6 + floor(Hash(float2(srow, fid), 95) * 8);
                float seg = floor(sp.x * segN + Hash(float2(srow, fid), 96) * 3);
                float streak = step(0.7, Hash(float2(srow, fid), 94)) * step(0.55, Hash(float2(seg, srow + fid), 97)) * inBand;
                col += _HeadColor.rgb * streak * 0.14;

                // 화면 마감
                col *= 1 - _Vignette * saturate(dot(sp - 0.5, sp - 0.5) * 2.2);
                col += (frac(sin(dot(sp * 1000 + T, float2(12.9898, 78.233))) * 43758.5453) - 0.5) * 0.006;
                col *= _Intensity;
                return float4(max(col, 0) * i.color.rgb, i.color.a);
            }

            // 모양 하나 고르기
            float2 Pattern(float2 p)
            {
                #if defined(_STYLE_DIAGONAL)
                    return StyleDiagonal(p);
                #elif defined(_STYLE_BUS)
                    return StyleBus(p);
                #elif defined(_STYLE_HEX)
                    return StyleHex(p);
                #else
                    return StyleGrid(p);
                #endif
            }


            float4 frag (V2F i) : SV_Target
            {
                float T = _TimeOverride >= 0 ? _TimeOverride : _Time.y;
                float t = _PhaseOverride >= 0 ? _PhaseOverride : T / max(_Period, 0.01);
                #if defined(_STYLE_DATA)
                    return DataFrag(i, T, t);
                #endif
                float2 p = i.world / _CellSize;
                float aa = fwidth(p.x);

                // ── 앞층 회로
                gLayerSeed = 0;
                gTangent = 0;
                float2 r = Pattern(p);
                float dist = r.x, id = r.y;
                float w = _LineWidth * (id < _MainRatio ? 1.9 : 1.0);          // 주 회로는 두껍게

                // 칩 부품 (육각 모양은 칩 없음)
                #if !defined(_STYLE_HEX) && !defined(_STYLE_HYBRID)
                    float3 chip = Chips(p);
                    if (chip.y > 0.5) dist = 10;                              // 칩 아래 회로는 가림
                    if (chip.x < dist) { dist = chip.x; id = chip.z; w = _LineWidth * 1.2; gTangent = 0; }
                    float chipBody = chip.y;
                #else
                    float chipBody = 0;
                #endif

                float core = 1 - smoothstep(w - aa, w + aa, dist);
                float hot = saturate(1 - dist / max(w, 1e-4)) * core;             // 선 가운데일수록 1
                float halo = exp(-max(dist - w, 0) / (w * 1.5 + aa)) * (1 - smoothstep(0.12, 0.24, dist));

                float ph = frac(t + i.uv.y + id * _Stagger);
                float glow = Wave(ph);
                // 점화 깜빡임: 물결이 막 닿은 짧은 순간 회로마다 불규칙하게 켜졌다 꺼짐
                float boot = step(0.4, frac(sin(id * 913.7 + floor(T * 24)) * 4375.85));
                glow *= lerp(1, boot, _BootFlicker * (1 - smoothstep(0.0, 0.05, ph)));
                float headPh = frac(t + i.uv.y);
                float head = exp(-min(headPh, 1 - headPh) / _HeadWidth);

                // 데이터 흐름: 선을 따라 움직이는 짧은 빛 조각 (켜져 있을 때 잘 보임)
                float along = dot(p, gTangent) * 0.35 - T * _PacketSpeed * 0.35 + id * 5.3;
                float packet = smoothstep(0.86, 0.95, frac(along)) * (1 - smoothstep(0.97, 1.0, frac(along)));
                packet *= _PacketAmount * glow * step(0.5, length(gTangent));      // 켜진 선 위에서만, 선을 따라

                float3 lit = _GlowColor.rgb * (glow + packet) + _HeadColor.rgb * head;

                // ── 뒤층 회로 (작고 흐리게, 물결이 조금 늦게)
                gLayerSeed = 37.31;
                float2 pb = p * _BackScale + 11.7;
                float2 rb = Pattern(pb);
                float aab = fwidth(pb.x);
                float coreB = 1 - smoothstep(_LineWidth - aab, _LineWidth + aab, rb.x);
                float glowB = Wave(frac(t + i.uv.y - _BackDelay + rb.y * _Stagger));
                float3 back = coreB * (_IdleColor.rgb * 0.5 + _GlowColor.rgb * glowB * 0.35) * _BackStrength;

                // ── 합치기 (바탕은 어둡게, 빛은 선에만)
                float3 col = _BgColor.rgb;
                #if defined(_STYLE_HYBRID)
                {
                    // 벌집 주 회로선 (격자 칸의 _HexScale 배 크기) — 이어진 망, 가장자리는 서서히 흐려짐
                    gLayerSeed = 71.3;
                    gTangent = 0;
                    float2 hp = p / _HexScale;
                    float2 rh = HexNet(hp);
                    float2 hTan = gTangent;
                    float dh = rh.x * _HexScale;                   // 격자 칸 단위
                    float fade = rh.y;                             // 망 가장자리 흐려짐

                    float wM = _LineWidth * _MainWidth;
                    float wC = wM * 0.45;                          // 가운데 빛줄기
                    float wBed = wM * 1.15;                        // 배선 바탕 반폭
                    float wR = _LineWidth * 0.4;                   // 테두리선
                    float bedM = (1 - smoothstep(wBed - aa, wBed + aa, dh)) * fade;
                    float coreM = (1 - smoothstep(wC - aa, wC + aa, dh)) * fade;
                    float hotM = saturate(1 - dh / wC) * coreM;
                    float railM = (1 - smoothstep(wR - aa, wR + aa, abs(dh - wBed - wR * 1.5))) * fade;   // 망 전체 윤곽을 따라 한 줄
                    float haloM = exp(-max(dh - wC, 0) / (wC * 1.6 + aa)) * (1 - smoothstep(wBed * 0.9, wBed, dh)) * fade;

                    float phM = frac(t + i.uv.y);                  // 벌집 전체가 같은 박자
                    float glowM = Wave(phM);
                    float bootM = step(0.4, frac(sin(floor(dot(floor(hp), float2(3.1, 7.7))) * 517.3 + floor(T * 24)) * 4375.85));
                    glowM *= lerp(1, bootM, _BootFlicker * (1 - smoothstep(0.0, 0.05, phM)));
                    float alongM = dot(p, hTan) * 0.35 - T * _PacketSpeed * 0.35;
                    float packetM = smoothstep(0.86, 0.95, frac(alongM)) * (1 - smoothstep(0.97, 1.0, frac(alongM)));
                    packetM *= _PacketAmount * glowM * step(0.5, length(hTan));
                    float3 litM = _GlowColor.rgb * (glowM + packetM) + _HeadColor.rgb * head;

                    // 격자 보조선: 가늘고 흐리게, 벌집 선 옆에서는 비켜감
                    float clear = 1 - (1 - smoothstep(wBed + wR * 3, wBed + wR * 8, dh)) * fade;
                    float wT = _LineWidth * 0.7;
                    float coreT = (1 - smoothstep(wT - aa, wT + aa, r.x)) * clear;

                    col += back * (1 - coreT) * (1 - bedM) * clear;
                    col += coreT * (_IdleColor.rgb * 0.8 + lit * _ThinStrength);
                    col = lerp(col, _IdleColor.rgb * 0.55 + litM * 0.12, bedM);                // 배선 바탕 (어둡게, 켜지면 은은하게)
                    col += railM * (_IdleColor.rgb * 1.2 + litM * 0.5);                       // 양옆 테두리선
                    col += (1 - coreM) * haloM * litM * _Halo;                               // 빛줄기 둘레 번짐 (바탕 안에서만)
                    col += coreM * (_IdleColor.rgb * 1.5 + litM);                            // 가운데 빛줄기
                    col += hotM * (glowM + head + packetM) * _HotCore;                        // 흰 심
                }
                #else
                col += back * (1 - core) * (1 - chipBody);
                col += chipBody * _BgColor.rgb * 1.5;                                // 칩 몸체는 바탕보다 살짝 밝은 어둠
                col += core * (_IdleColor.rgb + lit);
                col += hot * (glow + head + packet) * _HotCore;                      // 가운데 흰빛
                col += (1 - core) * halo * lit * _Halo;
                #endif

                // 화면 마감: 가장자리 어둡게 + 옅은 잡티(색 띠 방지)
                float2 sp = i.screen.xy / max(i.screen.w, 1e-4);
                col *= 1 - _Vignette * saturate(dot(sp - 0.5, sp - 0.5) * 2.2);
                col += (frac(sin(dot(sp * 1000 + T, float2(12.9898, 78.233))) * 43758.5453) - 0.5) * 0.006;
                col *= _Intensity;

                return float4(max(col, 0) * i.color.rgb, i.color.a);
            }
            ENDHLSL
        }
    }
}
