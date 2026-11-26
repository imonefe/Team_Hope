// [2026-11-27] 가열 블레이드 검기 꼬리 — 칼날 전체가 끌리는 꼬리 (시안 "길게 3m · 끝 높이 75%")
//  메쉬 = 빌더가 만든 띠 (uv.x = 칼날 뒷면에서 뒤로 0 → 1(=_Len), uv.y = 칼날 아래 끝 0 → 위 끝 1). 앞 테두리가 칼날 뒷면 곡선과 정확히 맞음
//  어느 점이 보이는가: 칼날 뒷면이 그 점을 지나간 뒤 흐른 시간(age) < _Life, 그리고 날아온 거리 안쪽
//  높이: 칼날 높이 그대로 시작 → 오래될수록 조금씩 좁아짐 (끝에서 1 - _Shrink)
//  색: 칼날 쪽 주황 → 짙은 빨강, 끝부터 월드 기준 노이즈로 녹아 사라지며 녹는 가장자리가 빛남. 값은 FxToonFireDriver 가 넣음
Shader "EventHorizon/FX/ToonFireTail"
{
    Properties
    {
        _Ratio ("색 비율 (선형)", Vector) = (1, 0.11, 0.03, 1)
        _DarkCol ("짙은 빨강 (선형)", Vector) = (0.35, 0.035, 0.01, 1)
        _Len ("띠 길이 (오브젝트 단위)", Float) = 3
        _Speed ("검기 속도 (오브젝트 단위/초)", Float) = 20
        _Travel ("날아온 거리 (오브젝트 단위)", Float) = 0
        _Since ("멈춘 뒤 흐른 시간 (초)", Float) = 0
        _Life ("꼬리 수명 (초)", Float) = 0.14
        _Shrink ("끝에서 줄어드는 높이 비율", Float) = 0.25
        _MidPart ("주황 구간 (수명 비율)", Float) = 0.35
        _Alpha ("전체 진하기", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ToonFireTail"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Ratio; float4 _DarkCol;
            float _Len, _Speed, _Travel, _Since, _Life, _Shrink, _MidPart, _Alpha;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; };

            Vary vert(Attr i)
            {
                Vary o; o.wpos = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wpos); o.uv = i.uv; return o;
            }

            float h21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + 1.0), f.x), f.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.5714 + vnoise(p * 2.0) * 0.2857 + vnoise(p * 4.0) * 0.1429; }

            half4 frag(Vary i) : SV_Target
            {
                float d = i.uv.x * _Len;                                   // 칼날 뒷면에서 뒤로 떨어진 거리
                if (d > _Travel) return 0;                                 // 아직 지나가지 않은 곳 (출발점보다 뒤)
                float age = d / max(_Speed, 0.01) + _Since;
                float k = age / max(_Life, 0.001);
                if (k >= 1.0) return 0;
                float hy = abs(i.uv.y - 0.5) * 2.0;                       // 칼날 높이 기준 0(가운데) ~ 1(끝)
                if (hy > 1.0 - _Shrink * k + 0.015) return 0;              // 조금씩 좁아짐
                float n = fbm(i.wpos.xy * 3.2 * 1.0);                      // 월드 기준 → 무늬가 따라 미끄러지지 않음
                float thr = pow(k, 1.5) * 1.05;
                if (n <= thr) return 0;
                float glow = saturate(1.0 - (n - thr) / 0.06) * (k > 0.3 ? 1.0 : 0.0);
                float mid = k < _MidPart ? 1.0 : 0.0;
                float da = _Alpha * (mid > 0.5 ? 0.45 : 0.9);
                float3 add = _Ratio.rgb * _Alpha * (mid * 1.7 + glow * 3.5);
                return half4(_DarkCol.rgb * da + add, da);
            }
            ENDHLSL
        }
    }
}
