// 입체 원뿔 빛줄기 셰이더. LightBeam3D 스크립트가 만든 원뿔 옆면에 쓴다. 값은 LightBeam3D 스크립트가 오브젝트마다 넣어준다.
// 원뿔의 위(uv.y = 1)가 광원, 아래(uv.y = 0)가 끝.  uv.x 는 둘레 (0~1 한 바퀴).
// 테두리 흐림: 면이 카메라를 정면으로 볼수록 진하고, 비스듬할수록(원뿔 가장자리) 옅어진다 → 어느 각도에서도 흐린 빛기둥.
//  "Haze"      단계: 빛 안개를 화면에 더함 (앞에 있는 3D 물체에 가려짐)
//  "LightMask" 단계: 어두운 방에서만 DarknessScreenFeature 가 빛 지도에 그림 → 닿은 곳이 밝아짐
Shader "EventHorizon/LightBeam3D"
{
    Properties
    {
        _Color ("빛 색", Color) = (1, 0.93, 0.78, 1)
        _LightIntensity ("밝히는 세기", Range(0, 3)) = 1
        _HazeIntensity ("빛 안개 세기", Range(0, 2)) = 0.2

        [Header(Softness)]
        _EdgeSoftness ("테두리 흐림", Range(0.05, 1)) = 0.8
        _StartFade ("시작 쪽 나타남 구간", Range(0, 1)) = 0.05
        _EndFade ("끝 쪽 사라짐 구간", Range(0, 1)) = 0.4
        _Falloff ("멀어질수록 약해짐", Range(0, 1)) = 0.5
        _NearFade ("카메라 가까이서 옅어지는 거리 (0 = 끔)", Float) = 0

        [Header(Shimmer)]
        _Shimmer ("결 무늬 진하기", Range(0, 1)) = 0.3
        _ShimmerSpeed ("결 무늬 속도", Float) = 0.15
        _ShimmerScale ("결 무늬 촘촘함", Range(0.5, 20)) = 3
        _Flicker ("깜빡임", Range(0, 1)) = 0
        _FlickerSpeed ("깜빡임 속도", Float) = 5
        [HideInInspector] _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _LightIntensity;
            float _HazeIntensity;
            float _EdgeSoftness;
            float _StartFade;
            float _EndFade;
            float _Falloff;
            float _NearFade;
            float _Shimmer;
            float _ShimmerSpeed;
            float _ShimmerScale;
            float _Flicker;
            float _FlickerSpeed;
            float _Seed;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct V2F
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
        };

        V2F vert (Attributes v)
        {
            V2F o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = v.uv;
            return o;
        }

        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // 부드러운 3차원 값 잡음 (0~1) — 둘레를 따라 이음매 없이 쓰려고 3차원
        float ValueNoise3(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);
            float n000 = Hash31(i);
            float n100 = Hash31(i + float3(1, 0, 0));
            float n010 = Hash31(i + float3(0, 1, 0));
            float n110 = Hash31(i + float3(1, 1, 0));
            float n001 = Hash31(i + float3(0, 0, 1));
            float n101 = Hash31(i + float3(1, 0, 1));
            float n011 = Hash31(i + float3(0, 1, 1));
            float n111 = Hash31(i + float3(1, 1, 1));
            float x00 = lerp(n000, n100, u.x), x10 = lerp(n010, n110, u.x);
            float x01 = lerp(n001, n101, u.x), x11 = lerp(n011, n111, u.x);
            return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
        }

        // 원뿔 빛 모양 (0~1)
        float ConeShape(V2F i)
        {
            float t = 1.0 - i.uv.y;                                  // 0 = 광원, 1 = 끝

            // 테두리 흐림: 면이 카메라를 얼마나 정면으로 보는지
            float3 n = normalize(i.normalWS);
            float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
            float facing = abs(dot(n, viewDir));
            float soft = max(_EdgeSoftness, 0.001);
            float edge = smoothstep(0.0, soft, facing);
            edge *= edge;

            // 길이 방향
            float along = smoothstep(0.0, max(_StartFade, 0.001), t)
                        * (1.0 - smoothstep(1.0 - max(_EndFade, 0.001), 1.0, t));
            along *= lerp(1.0, 1.0 - t, _Falloff);

            // 결 무늬: 둘레 각도를 원으로 펼쳐서 이음매 없이, 광원에서 끝 쪽으로 천천히 흐름
            float time = _Time.y;
            float a = i.uv.x * 6.2831853;
            float2 ring = float2(cos(a), sin(a)) * _ShimmerScale * 0.5;
            float nz = t * _ShimmerScale * 0.3 - time * _ShimmerSpeed;
            float nv = 0.6 * ValueNoise3(float3(ring + _Seed, nz))
                     + 0.4 * ValueNoise3(float3(ring * 2.1 - _Seed, nz * 1.9 + 3.7));
            float shimmer = 1.0 - _Shimmer * (1.0 - nv);

            float fl = ValueNoise3(float3(time * _FlickerSpeed + _Seed * 7.0, _Seed, 0.5));
            float flicker = 1.0 - _Flicker * fl;

            // 카메라가 아주 가까우면 옅게 (화면을 가득 덮지 않게)
            float nearFade = 1.0;
            if (_NearFade > 0.0)
                nearFade = saturate(distance(i.positionWS, GetCameraPositionWS()) / _NearFade);

            // 앞뒤 두 면이 겹쳐 보이므로 절반씩
            return saturate(edge * along) * shimmer * flicker * nearFade * 0.5;
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
                return float4(_Color.rgb * (_HazeIntensity * ConeShape(i)), 0);
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
                return float4(_Color.rgb * (_LightIntensity * ConeShape(i)), 0);
            }
            ENDHLSL
        }
    }
}
