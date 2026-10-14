// 빛줄기 셰이더. LightBeam 스크립트가 붙은 사각형 판에 쓴다. 값은 LightBeam 스크립트가 오브젝트마다 넣어준다.
// 판의 위쪽(uv.y = 1)이 광원, 아래쪽(uv.y = 0)이 빛의 끝.
//  "Haze"      단계: 빛줄기 자체를 옅은 빛 안개로 화면에 더함 (평소에도 보임)
//  "LightMask" 단계: 어두운 방에서만 DarknessScreenFeature 가 빛 지도에 그림 → 닿은 곳이 밝아짐
Shader "EventHorizon/LightBeam"
{
    Properties
    {
        _Color ("빛 색", Color) = (1, 0.93, 0.78, 1)
        _LightIntensity ("밝히는 세기", Range(0, 3)) = 1
        _HazeIntensity ("빛 안개 세기", Range(0, 2)) = 0.25

        [Header(Shape)]
        _StartWidth ("광원 쪽 폭 비율", Range(0.02, 1)) = 0.35
        _EdgeSoftness ("가장자리 흐림", Range(0.01, 1)) = 0.7
        _StartFade ("시작 쪽 나타남 구간", Range(0, 1)) = 0.08
        _EndFade ("끝 쪽 사라짐 구간", Range(0, 1)) = 0.45
        _Falloff ("멀어질수록 약해짐", Range(0, 1)) = 0.5

        [Header(Shimmer)]
        _Shimmer ("결 무늬 진하기", Range(0, 1)) = 0.3
        _ShimmerSpeed ("결 무늬 속도", Float) = 0.15
        _ShimmerScale ("결 무늬 촘촘함", Range(0.5, 20)) = 4
        _Flicker ("깜빡임", Range(0, 1)) = 0
        _FlickerSpeed ("깜빡임 속도", Float) = 5
        [HideInInspector] _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _LightIntensity;
            float _HazeIntensity;
            float _StartWidth;
            float _EdgeSoftness;
            float _StartFade;
            float _EndFade;
            float _Falloff;
            float _Shimmer;
            float _ShimmerSpeed;
            float _ShimmerScale;
            float _Flicker;
            float _FlickerSpeed;
            float _Seed;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct V2F { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        V2F vert (Attributes v)
        {
            V2F o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = v.uv;
            return o;
        }

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        // 부드러운 값 잡음 (0~1)
        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // 빛줄기 모양 (0~1): 가장자리·시작·끝이 흐릿하게, 결 무늬와 깜빡임 포함
        float BeamShape(float2 uv)
        {
            float t = 1.0 - uv.y;                                   // 0 = 광원, 1 = 끝
            float halfW = lerp(_StartWidth, 1.0, t);                // 아래로 갈수록 넓어짐
            float sx = (uv.x - 0.5) * 2.0 / max(halfW, 0.001);      // -1 ~ 1 (빛줄기 안쪽 기준 가로 위치)
            float x = abs(sx);

            float soft = max(_EdgeSoftness, 0.001);
            float across = 1.0 - smoothstep(1.0 - soft, 1.0, x);

            float along = smoothstep(0.0, max(_StartFade, 0.001), t)
                        * (1.0 - smoothstep(1.0 - max(_EndFade, 0.001), 1.0, t));
            along *= lerp(1.0, 1.0 - t, _Falloff);

            // 빛줄기 방향으로 길게 늘어진 결 무늬가 천천히 흐름
            float time = _Time.y;
            float n = ValueNoise(float2(sx * _ShimmerScale + _Seed, t * _ShimmerScale * 0.25 - time * _ShimmerSpeed));
            n = 0.6 * n + 0.4 * ValueNoise(float2(sx * _ShimmerScale * 2.3 - _Seed, t * _ShimmerScale * 0.5 - time * _ShimmerSpeed * 1.7));
            float shimmer = 1.0 - _Shimmer * (1.0 - n);

            float fl = ValueNoise(float2(time * _FlickerSpeed + _Seed * 7.0, _Seed));
            float flicker = 1.0 - _Flicker * fl;

            return saturate(across * along) * shimmer * flicker;
        }
        ENDHLSL

        Pass
        {
            Name "Haze"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (V2F i) : SV_Target
            {
                return float4(_Color.rgb * (_HazeIntensity * BeamShape(i.uv)), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "LightMask"
            Tags { "LightMode" = "LightMask" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag (V2F i) : SV_Target
            {
                return float4(_Color.rgb * (_LightIntensity * BeamShape(i.uv)), 0);
            }
            ENDHLSL
        }
    }
}
