// =====================================================================
//  ScreenSlash.shader  (URP / Full Screen Pass Renderer Feature 용)
//  화면을 대각선으로 가르는 참격 연출 풀스크린 쉐이더
//  - 참격 라인(발광 + 거친 가장자리)
//  - 라인 기준으로 화면 양쪽이 어긋나고(Shear) 벌어짐(Gap)
//  - 흑백 / 대비 / 반전 / 색수차 / 플래시
//  Unity 2022.2+ (URP 14+) / Unity 6 대응
// =====================================================================
Shader "GameEffect/ScreenSlash"
{
    Properties
    {
        [Header(Master)]
        _Intensity      ("Master Intensity (0 = Off)", Range(0, 1)) = 1

        [Header(Slash Line)]
        _SlashCenter    ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _SlashAngle     ("Angle (deg)", Range(-180, 180)) = 28
        _SlashProgress  ("Draw Progress", Range(0, 1)) = 1
        _SlashReverse   ("Draw From Right (0/1)", Range(0, 1)) = 0
        _LineWidth      ("Core Width", Range(0, 0.05)) = 0.006
        _GlowWidth      ("Glow Width", Range(0.001, 0.2)) = 0.012
        [HDR]_LineColor ("Line Color (HDR)", Color) = (4, 4, 4, 1)
        _LineIntensity  ("Line Intensity", Range(0, 1)) = 1
        _EdgeNoise      ("Edge Raggedness", Range(0, 1)) = 0.5
        _EdgeNoiseFreq  ("Edge Noise Frequency", Range(1, 200)) = 60
        _GlowStrength   ("Glow Strength", Range(0, 1)) = 0.25
        _Streaks        ("Side Streaks", Range(0, 1)) = 0.5

        [Header(Split)]
        _SplitShift     ("Shear Along Line", Range(-0.3, 0.3)) = 0
        _SplitGap       ("Gap Width", Range(0, 0.2)) = 0
        [HDR]_GapColor  ("Gap Color (HDR)", Color) = (3, 3, 3, 1)

        [Header(Grade)]
        _Desaturate     ("Desaturate", Range(0, 1)) = 0
        _Contrast       ("Contrast", Range(0.5, 3)) = 1
        _Invert         ("Invert", Range(0, 1)) = 0
        _Tint           ("Tint", Color) = (1, 1, 1, 1)
        _InvertSide     ("Invert Only One Side (0/1)", Range(0, 1)) = 0

        [Header(Chromatic Aberration)]
        _Chroma         ("Chroma Amount", Range(0, 0.05)) = 0
        _ChromaFalloff  ("Chroma Falloff From Line", Range(0.01, 1)) = 0.3

        [Header(Flash)]
        _FlashColor     ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount    ("Flash Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ScreenSlash"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // _BlitTexture, Vert(), Varyings 제공 (Full Screen Pass Renderer Feature 표준)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Intensity;

            float4 _SlashCenter;
            float  _SlashAngle;
            float  _SlashProgress;
            float  _SlashReverse;
            float  _LineWidth;
            float  _GlowWidth;
            float4 _LineColor;
            float  _LineIntensity;
            float  _EdgeNoise;
            float  _EdgeNoiseFreq;
            float  _GlowStrength;
            float  _Streaks;

            float  _SplitShift;
            float  _SplitGap;
            float4 _GapColor;

            float  _Desaturate;
            float  _Contrast;
            float  _Invert;
            float4 _Tint;
            float  _InvertSide;

            float  _Chroma;
            float  _ChromaFalloff;

            float4 _FlashColor;
            float  _FlashAmount;

            // ---------- 유틸 ----------
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u);
            }

            float3 SampleScene(float2 uv)
            {
                uv = clamp(uv, 0.0001, 0.9999);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float3 src = SampleScene(uv);
                if (_Intensity <= 0.0001)
                    return float4(src, 1);

                // ---- 화면비 보정 좌표계 (세로 1 기준) ----
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 asp   = float2(aspect, 1.0);
                float2 p     = (uv - _SlashCenter.xy) * asp;

                float  ang = radians(_SlashAngle);
                float2 dir = float2(cos(ang), sin(ang));   // 라인 진행 방향
                float2 nrm = float2(-dir.y, dir.x);        // 라인 법선

                float along = dot(p, dir);   // 라인 따라 거리
                float dist  = dot(p, nrm);   // 라인에서 수직 거리(부호 = 어느 쪽인지)
                float side  = dist >= 0 ? 1.0 : -1.0;

                // ---- 라인이 그려지는 진행도 (한쪽 끝 -> 반대쪽 끝) ----
                float halfLen = length(asp) * 0.5 + 0.2;
                float a01  = along / halfLen;                     // -1..1
                a01 = lerp(a01, -a01, _SlashReverse);
                float head = lerp(-1.05, 1.05, _SlashProgress);
                float drawn = 1.0 - smoothstep(head - 0.08, head, a01); // 라인이 지나간 구간 = 1

                // ---- 화면 가르기: 양쪽을 라인 방향으로 어긋나게 + 바깥으로 벌림 ----
                float halfGap = _SplitGap * 0.5 * drawn;
                float2 offs   = side * (dir * _SplitShift * 0.5 + nrm * halfGap) * drawn;
                float2 pS     = p - offs;
                float2 uvS    = _SlashCenter.xy + pS / asp;

                // ---- 색수차 (라인 가까울수록 강함) ----
                float nearLine = exp(-abs(dist) / _ChromaFalloff);
                float2 ca = nrm / asp * _Chroma * nearLine * side;
                float3 col;
                col.r = SampleScene(uvS + ca).r;
                col.g = SampleScene(uvS).g;
                col.b = SampleScene(uvS - ca).b;

                // ---- 그레이딩: 흑백 / 대비 / 반전 / 틴트 ----
                float l = Luma(col);
                col = lerp(col, l.xxx, _Desaturate);
                col = saturate((col - 0.5) * _Contrast + 0.5);
                float invMask = lerp(1.0, step(0.0, dist), _InvertSide);
                col = lerp(col, 1.0 - col, _Invert * invMask);
                col *= _Tint.rgb;

                // ---- 벌어진 틈 채우기 ----
                float edgeN  = Noise1D(along * _EdgeNoiseFreq + _Time.y * 30.0);
                float edgeN2 = Noise1D(along * _EdgeNoiseFreq * 0.23 - _Time.y * 7.0);
                float rag    = 1.0 + _EdgeNoise * ((edgeN - 0.5) * 1.2 + (edgeN2 - 0.5) * 0.8);

                float gapDist = abs(dist) - halfGap * rag;
                float inGap   = 1.0 - smoothstep(-0.002, 0.002, gapDist);
                col = lerp(col, _GapColor.rgb, inGap * step(0.0001, halfGap));

                // ---- 참격 라인 (코어 + 글로우 + 사이드 스트릭) ----
                float d = max(gapDist, 0.0);
                float w = _LineWidth * rag;
                float core = 1.0 - smoothstep(w * 0.5, w, d);
                float glow = exp(-d / max(_GlowWidth, 1e-4)) * _GlowStrength;

                // 라인 옆으로 흐르는 가는 스트릭 (레퍼런스의 잔선)
                float sN = Noise1D(dist * 900.0) ;
                float sMask = smoothstep(0.85, 1.0, sN) * exp(-abs(dist) / (_GlowWidth * 4.0));
                float streak = sMask * Noise1D(along * 8.0 + sN * 20.0 - _Time.y * 40.0) * _Streaks;

                // 라인 머리(진행 끝) 하이라이트
                float headGlow = exp(-abs(a01 - head) * 25.0) * step(_SlashProgress, 0.999) * exp(-d / 0.05);

                float lineMask = saturate(core + glow + streak + headGlow) * drawn * _LineIntensity;
                col += _LineColor.rgb * lineMask;

                // ---- 플래시 ----
                col = lerp(col, _FlashColor.rgb, _FlashAmount);

                col = lerp(src, col, _Intensity);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
