// =====================================================================
//  SlashArc.shader  — 검 참격 궤적(초승달) 쉐이더  (URP 2D / Unlit)
//  SwordSlashFX.cs 가 만드는 호(arc) 메쉬에 사용.
//  메쉬 UV: x = 휘두르는 방향(0 = 시작, 1 = 끝), y = 안쪽(0) -> 바깥쪽(1)
//
//  텍스처 교체 가이드
//   - Shape   : 참격 모양. 가로 = 휘두르는 방향(왼쪽=시작, 오른쪽=끝), 세로 = 아래 안쪽/위 바깥쪽
//               A(알파) = 모양, RGB = 밝기(열기).  컬러 이미지를 그대로 쓰려면 Use Texture Color 켜기
//   - Ramp    : 열기(밝기) -> 색 변환표. 왼쪽 = 식은 색, 오른쪽 = 가장 뜨거운 색
//   - Noise   : R = 불꽃 흔들림, G = 디졸브(사라지는 결)
// =====================================================================
Shader "GameEffect/SlashArc"
{
    Properties
    {
        [Header(Textures)]
        _MainTex        ("Shape (A = mask, RGB = heat or color)", 2D) = "white" {}
        [MaterialToggle] _UseTexColor ("Use Texture Color (off = Heat Ramp)", Float) = 0
        _RampTex        ("Heat Ramp (left = cold, right = hot)", 2D) = "white" {}
        _NoiseTex       ("Noise (R = wobble, G = dissolve)", 2D) = "gray" {}

        [Header(Color)]
        [HDR] _Color    ("Tint", Color) = (1, 1, 1, 1)
        _Intensity      ("Intensity", Range(0, 5)) = 1.4
        _HeatBoost      ("Heat Boost", Range(-1, 1)) = 0
        _CoolAmount     ("Cooling While Fading", Range(0, 1)) = 0.5
        _HeadGlow       ("Leading Edge Glow", Range(0, 2)) = 0.35
        _HeadSharp      ("Leading Edge Sharpness", Range(1, 60)) = 18
        _Additive       ("Additive (0 = alpha, 1 = add)", Range(0, 1)) = 0.5

        [Header(Motion)]
        _Wobble         ("Flame Wobble", Range(0, 0.3)) = 0.08
        _WobbleTS       ("Wobble Tiling XY / Speed ZW", Vector) = (3, 0.5, 0.8, -1.5)
        _RevealSoft     ("Reveal Softness", Range(0.001, 0.3)) = 0.06

        [Header(Dissolve)]
        _NoiseTiling    ("Dissolve Noise Tiling", Float) = 4
        _DissolveBias   ("Dissolve Inner First", Range(0, 1)) = 0.4
        _DissolveSoft   ("Dissolve Softness", Range(0.001, 0.3)) = 0.08
        [HDR] _EdgeColor("Burn Edge Color", Color) = (2, 0.7, 0.2, 1)
        _EdgeWidth      ("Burn Edge Width", Range(0, 0.3)) = 0.06
        _TailErase      ("Tail Erase", Range(0, 1)) = 1

        [Header(Runtime  script controlled)]
        _Progress       ("Progress", Range(0, 1)) = 1
        _Fade           ("Fade", Range(0, 1)) = 0
        _Cool           ("Cool", Range(0, 1)) = 0
        _LocalTime      ("Local Time", Float) = 0
        _Seed           ("Seed", Float) = 0
        _Alpha          ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SlashArc"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_RampTex);  SAMPLER(sampler_RampTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _UseTexColor;
                float4 _Color;
                float  _Intensity;
                float  _HeatBoost;
                float  _CoolAmount;
                float  _HeadGlow;
                float  _HeadSharp;
                float  _Additive;
                float  _Wobble;
                float4 _WobbleTS;
                float  _RevealSoft;
                float  _NoiseTiling;
                float  _DissolveBias;
                float  _DissolveSoft;
                float4 _EdgeColor;
                float  _EdgeWidth;
                float  _TailErase;
                float  _Progress;
                float  _Fade;
                float  _Cool;
                float  _LocalTime;
                float  _Seed;
                float  _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;   // 휘두르는 방향
                float v = i.uv.y;   // 안쪽 -> 바깥쪽
                float t = _LocalTime;

                // 불꽃 흔들림 (안쪽일수록 많이)
                float2 wuv = float2(u * _WobbleTS.x + t * _WobbleTS.z + _Seed, v * _WobbleTS.y + t * _WobbleTS.w);
                float wob = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wuv).r;
                float vw = saturate(v + (wob - 0.5) * _Wobble * (1.0 - v));

                float2 suv = float2(u, vw) * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(suv) * 0.998 + 0.001);

                // 휘두르기 진행 (머리 = _Progress)
                float head = _Progress;
                float reveal = smoothstep(head + 0.02, head - _RevealSoft, u);

                // 디졸브
                float2 duv = float2(u * _NoiseTiling + t * 0.3 + _Seed * 1.7, v);
                float nd = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, duv).g;
                float dv = lerp(nd, v, _DissolveBias);
                float dz = _Fade * 1.1;
                float alive = smoothstep(dz, dz + _DissolveSoft, dv);
                float edge = (1.0 - smoothstep(dz, dz + _EdgeWidth, dv)) * alive * step(0.001, _Fade);

                // 꼬리부터 지워짐
                float tf = _Fade * 1.2;
                float tail = lerp(1.0, smoothstep(tf - 0.15, tf, u + (nd - 0.5) * 0.2), _TailErase);

                float a = shape.a * reveal * alive * tail * _Alpha * i.color.a;

                float headGlow = exp(-abs(u - head) * _HeadSharp) * _HeadGlow * step(head, 0.999);
                float3 col;
                if (_UseTexColor > 0.5)
                {
                    col = shape.rgb * (1.0 - 0.4 * _Cool * _CoolAmount) + headGlow;
                }
                else
                {
                    float heat = saturate(shape.r * (1.0 - _CoolAmount * _Cool) + _HeatBoost + headGlow);
                    col = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(clamp(heat, 0.01, 0.99), 0.5)).rgb;
                    a *= smoothstep(0.04, 0.25, heat);   // 식은 부분은 투명하게 (검은 재 방지)
                }

                col = col * _Color.rgb * i.color.rgb * _Intensity + edge * _EdgeColor.rgb;
                a = saturate(a * _Color.a);

                return float4(col * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
