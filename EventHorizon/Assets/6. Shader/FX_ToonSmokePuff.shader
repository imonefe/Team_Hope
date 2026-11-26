// [2026-11-27] 흡입식 강화제 JJW · 가스 분사 — 만화풍 2톤 연기 뭉치 (입자용)
//  입자 하나 = 연기 뭉치 하나. 모양은 입자 고유 무작위 값으로 골라 매번 다름:
//   한 덩이 / 붙은 덩어리(2~3개) / 길쭉 / 작은 조각 · 기울기 · 비율 무작위
//   테두리 흐림 · 끊김 흐림 · 면 경계 흐림도 슬라이더
//   테두리(울퉁불퉁 세기 · 촘촘함 · 갈래 수 · 깊이 · 그림자 두께 · 꿈틀거림)는 재질 M2_toonsmoke 인스펙터 슬라이더로 조절 — 뭉치마다 최소~최대 사이 무작위
//  수명 비율로: 처음 커지며 나타남 → 노이즈로 뚝뚝 끊기며 사라짐
//  색: 위쪽 밝은 면 / 어두운 면 2톤(불투명) + 속 달아오름 (처음 피어난 뭉치 = 입자 색 빨강 채널, 과열 박자 = _Pulse)
//  입자 정점 정보 (빌더가 설정): UV(xy) · 수명 비율(z) · 고유 무작위 xyz (TEXCOORD0.w, TEXCOORD1.xy) · 색
Shader "EventHorizon/FX/ToonSmokePuff"
{
    Properties
    {
        _Ratio ("달아오름 색 비율 (선형)", Vector) = (1, 0.11, 0.03, 1)
        _LightCol ("밝은 면 (선형)", Vector) = (0.17, 0.04, 0.016, 1)
        _CharCol ("어두운 면 (선형)", Vector) = (0.06, 0.02, 0.015, 1)
        _Opacity ("불투명도", Float) = 0.88
        _Pulse ("과열 박자 0~1 (스크립트)", Float) = 0
        _HotBase ("기본 달아오름", Float) = 0.03
        _HotMult ("달아오름 배수", Float) = 2
        _PulseAmt ("과열 박자 세기 (0 = 깜빡임 없음)", Float) = 0
        _FirstAmt ("처음 피어날 때 달아오름", Float) = 0.15
        _Time2 ("흐름 시간 (스크립트, 없으면 _Time)", Float) = -1
        [Space(12)] _EdgeMin ("울퉁불퉁 세기 최소", Range(0, 0.5)) = 0.03
        _EdgeMax ("울퉁불퉁 세기 최대", Range(0, 0.5)) = 0.08
        _EdgeFreq ("울퉁불퉁 촘촘함", Range(0.5, 8)) = 1.5
        _LobesMin ("갈래 수 최소", Range(2, 10)) = 3
        _LobesMax ("갈래 수 최대", Range(2, 10)) = 5
        _AmpMin ("갈래 깊이 최소", Range(0, 0.4)) = 0.04
        _AmpMax ("갈래 깊이 최대", Range(0, 0.4)) = 0.12
        _ShadowOffset ("그림자 두께 (아래쪽 어두운 띠)", Range(0, 0.6)) = 0.22
        _WiggleSpeed ("꿈틀거림 속도", Range(0, 4)) = 1.2
        [Space(12)] _EdgeSoft ("테두리 흐림 (0 = 딱 떨어짐)", Range(0, 0.8)) = 0.2
        _DissolveSoft ("사라질 때 끊김 흐림", Range(0, 0.5)) = 0.08
        _ShadowSoft ("밝은/어두운 면 경계 흐림", Range(0, 0.6)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ToonSmoke"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Ratio; float4 _LightCol; float4 _CharCol;
            float _Opacity, _Pulse, _HotBase, _HotMult, _Time2, _PulseAmt, _FirstAmt;
            float _EdgeMin, _EdgeMax, _EdgeFreq, _LobesMin, _LobesMax, _AmpMin, _AmpMax, _ShadowOffset, _WiggleSpeed, _EdgeSoft, _DissolveSoft, _ShadowSoft;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float4 col : COLOR; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct Vary { float4 pos : SV_POSITION; float4 col : COLOR; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; };

            Vary vert(Attr i) { Vary o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv0 = i.uv0; o.uv1 = i.uv1; return o; }

            float h11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float h21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + 1.0), f.x), f.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.5714 + vnoise(p * 2.0) * 0.2857 + vnoise(p * 4.0) * 0.1429; }

            // 뭉치 하나의 거리 (1 = 테두리)
            float lumpDist(float2 u, float lobes, float amp, float edge, float seed, float t)
            {
                float ang = atan2(u.y, u.x);
                float lump = 1.0 - amp * (sin(ang * lobes + seed + t * _WiggleSpeed) * 0.5 + 0.5);
                float rough = 1.0 - edge * fbm(u * _EdgeFreq + float2(seed, -t * 0.4 * _WiggleSpeed / 1.2));   // 울퉁불퉁 (재질에서 조절)
                return length(u) / max(lump * rough, 0.05);
            }

            half4 frag(Vary i) : SV_Target
            {
                float2 d = (i.uv0.xy * 2.0 - 1.0) * 2.0;           // 사각형 반지름 = 뭉치 반지름 × 2 (붙은 덩어리 여유)
                float age = i.uv0.z;
                float3 rnd = float3(i.uv0.w, i.uv1.x, i.uv1.y);
                float seed = rnd.x * 50.0;
                float t = _Time2 >= 0 ? _Time2 : _Time.y;
                float kind = floor(rnd.y * 5.0);                    // 0 한 덩이 · 1,2 붙은 덩어리 · 3 길쭉 · 4 작은 조각
                float lobes = floor(lerp(_LobesMin, _LobesMax + 0.999, h11(seed + 1.0)));
                float amp = lerp(_AmpMin, _AmpMax, h11(seed + 2.0));
                float edge = lerp(_EdgeMin, _EdgeMax, h11(seed + 3.0));
                float asp = kind == 3.0 ? lerp(1.3, 1.8, h11(seed + 4.0)) : lerp(0.8, 1.2, h11(seed + 4.0));
                float rot = rnd.z * 3.14159;
                float c = cos(rot), s = sin(rot);
                float2 u = float2((d.x * c - d.y * s) / asp, (d.x * s + d.y * c) * pow(asp, 0.3));
                if (kind == 4.0) u *= 1.8;                          // 작은 조각
                // 뭉치 전체 거리 (붙은 덩어리 포함)
                int nsub = (kind == 1.0 || kind == 2.0) ? 2 + (int)floor(h11(seed + 5.0) * 2.0) : 0;
                float r = lumpDist(u, lobes, amp, edge, seed, t);
                float rL = lumpDist(u - float2(0.0, _ShadowOffset), lobes, amp, edge, seed, t);    // 그림자 판정용: 위로 민 모양
                for (int k = 0; k < 3; k++)
                {
                    if (k >= nsub) break;
                    float2 o = float2(lerp(-0.55, 0.55, h11(seed + 10.0 + k)), lerp(-0.35, 0.35, h11(seed + 20.0 + k)));
                    float sr = lerp(0.5, 0.8, h11(seed + 30.0 + k));
                    r = min(r, lumpDist((u - o) / sr, lobes, amp, edge, seed + k, t));
                    rL = min(rL, lumpDist((u - float2(0.0, _ShadowOffset) - o) / sr, lobes, amp, edge, seed + k, t));
                }
                float fin = saturate(age / 0.07);                   // 처음 커지며 나타남
                float nz = fbm(d * 1.5 * 0.5 + float2(seed, t * 0.3));
                float thrD = pow(age, 1.4) * 1.05;
                if (r >= fin || nz <= thrD) return 0;
                float edgeA = 1.0 - smoothstep(fin * (1.0 - _EdgeSoft), fin, r);        // 테두리 흐림 (바깥으로 갈수록 옅어짐)
                float dissA = smoothstep(thrD, thrD + _DissolveSoft + 0.0001, nz);      // 사라질 때 끊긴 자리 흐림
                float sh = fin * 0.97;
                float lightK = 1.0 - smoothstep(sh * (1.0 - _ShadowSoft), sh + 0.0001, rL);   // 밝은 면 = 위로 민 모양 안 (경계 흐림 조절)
                float3 col = lerp(_CharCol.rgb, _LightCol.rgb, lightK);
                float fade = edgeA * dissA;
                float a = _Opacity * i.col.a * fade;
                float first = i.col.r > 0.5 ? saturate(1.0 - age / 0.2) * _FirstAmt : 0.0;   // 처음 피어난 뭉치만 살짝 (천천히 식음)
                float core = 1.0 - smoothstep(0.1, 0.75, r);          // 가운데서 부드럽게 옅어짐 (딱 잘린 점 X)
                float hot = (first + _Pulse * _PulseAmt + _HotBase) * core;
                float3 add = _Ratio.rgb * hot * _HotMult * i.col.a * fade;
                return half4(col * a + add, a);
            }
            ENDHLSL
        }
    }
}
