// [2026-11-27] 가열 블레이드 검기 — 만화풍 3톤 불 초승달 + 노이즈 소멸 (시안 "C 몸통 + A 방식 소멸")
//  메쉬 = SM_Crescent (uv.x = 호 방향, uv.y = 안쪽 0 → 앞날 1)
//  뒷날은 불꽃 혀처럼 너울거림 · 앞날 쪽 하얀노랑 / 가운데 주황 / 뒷날 띠 짙은 빨강(불투명)
//  소멸: 잔 노이즈 + 꼬리(안쪽)부터 녹아 구멍 나며 사라지고, 녹는 가장자리가 빛남. 값은 FxToonFireDriver 가 매 프레임 넣음
//  합성: 어두운 층(반투명) + 밝은 층(가산)을 한 번에 — Blend One OneMinusSrcAlpha (rgb 에 어두운 색 × 진하기 + 밝은 색)
Shader "EventHorizon/FX/ToonFireCrescent"
{
    Properties
    {
        _Ratio ("색 비율 (선형, 가장 큰 채널 = 1)", Vector) = (1, 0.11, 0.03, 1)
        _DarkCol ("뒷날 띠 짙은 빨강 (선형)", Vector) = (0.35, 0.035, 0.01, 1)
        _Thr ("소멸 문턱 (-0.1 = 온전 · 1.05 = 다 사라짐)", Float) = -0.1
        _Fin ("나타남 0~1", Float) = 1
        _Alpha ("전체 진하기 (잔상용)", Float) = 1
        _Flow ("흐름 시간", Float) = 0
        _HotMult ("하얀노랑 배수", Float) = 7
        _MidMult ("주황 배수", Float) = 1.6
        _GlowMult ("녹는 가장자리 배수", Float) = 5
        _HotBand ("하얀노랑 시작 (띠 비율)", Float) = 0.62
        _MidBand ("주황 시작 (띠 비율)", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ToonFire"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Ratio; float4 _DarkCol;
            float _Thr, _Fin, _Alpha, _Flow, _HotMult, _MidMult, _GlowMult, _HotBand, _MidBand;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Vary vert(Attr i) { Vary o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            float h21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + 1.0), f.x), f.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.5714 + vnoise(p * 2.0) * 0.2857 + vnoise(p * 4.0) * 0.1429; }

            half4 frag(Vary i) : SV_Target
            {
                float U = i.uv.x, V = i.uv.y, s = _Flow * 3.0;
                float tongue = fbm(float2(U * 5.0 + s * 0.3, s) * 3.0);          // 뒷날 불꽃 혀
                float edge = 0.12 + 0.55 * tongue;
                float n = fbm(float2(U * 4.0, V * 2.0) * 6.0) + (1.0 - V) * 0.35;  // 소멸: 꼬리(안쪽)부터
                if (V <= edge || n <= _Thr) return 0;
                float glow = saturate(1.0 - (n - _Thr) / 0.07);                    // 녹는 가장자리 빛
                float band = (V - edge) / max(1.0 - edge, 1e-3);
                float hot = band > _HotBand ? 1.0 : 0.0;
                float mid = (band > _MidBand && hot < 0.5) ? 1.0 : 0.0;
                float a = saturate(_Fin) * _Alpha;
                float da = a * ((hot + mid) > 0.5 ? 0.35 : 0.95);
                float3 add = _Ratio.rgb * a * (hot * _HotMult + mid * _MidMult + glow * _GlowMult);
                return half4(_DarkCol.rgb * da + add, da);
            }
            ENDHLSL
        }
    }
}
