// 빛받는 먼지 (3D 파티클용)
// 먼지 하나하나가 그 자리에 실제로 닿는 빛(스포트라이트·점광원, 그림자 포함)만큼 밝아진다.
// 빛이 닿지 않는 곳의 먼지는 검정 = 더하기 합성이라 보이지 않음 → 빛줄기 안에서만 먼지가 반짝인다.
// 텍스처 없이 셰이더가 동그란 점(심 + 은은한 번짐)을 그린다.
// 반짝임: 공간에 흐르는 잡음으로 먼지마다 밝기가 천천히 오르내림 (빛을 받은 먼지가 돌며 반짝이는 느낌)
Shader "EventHorizon/LitDust3D"
{
    Properties
    {
        _Color ("먼지 색", Color) = (1, 0.95, 0.85, 1)
        _Intensity ("밝기", Float) = 1
        _LightGain ("빛 반응 (빛 세기가 아주 세면 낮추기)", Float) = 0.15
        _MaxBrightness ("최대 밝기", Float) = 2.5
        _Saturation ("채도 (1 = 그대로, 0 = 흑백)", Range(0, 1.5)) = 1
        _LightThreshold ("이보다 약한 빛에선 먼지 안 보임 (0~1)", Range(0, 0.9)) = 0.15
        _CoreRadius ("심 크기 (0~1)", Range(0.05, 1)) = 0.45
        _Softness ("심 가장자리 부드러움", Range(0.05, 1)) = 0.6
        _HaloStrength ("번짐 세기", Range(0, 1)) = 0.35
        _HaloWidth ("번짐 넓이", Range(0.5, 8)) = 3
        _TwinkleAmount ("반짝임 정도 (0 = 없음)", Range(0, 1)) = 0.75
        _TwinkleSpeed ("반짝임 속도", Float) = 0.6
        _TwinkleScale ("반짝임 무늬 크기 (클수록 먼지마다 따로)", Float) = 1.2
        _ForwardScatter ("빛 쪽을 볼 때 더 밝게 (0 = 없음)", Range(0, 0.9)) = 0.35
        _MainLightWeight ("태양광 반영 (0 = 무시)", Range(0, 1)) = 0
        _AmbientWeight ("환경광 반영 (0 = 무시)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LitDust3D"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _LightGain;
                half _MaxBrightness;
                half _Saturation;
                half _LightThreshold;
                half _Softness;
                half _CoreRadius;
                half _HaloStrength;
                half _HaloWidth;
                half _TwinkleAmount;
                half _TwinkleSpeed;
                half _TwinkleScale;
                half _ForwardScatter;
                half _MainLightWeight;
                half _AmbientWeight;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;          // 파티클 색 (수명에 따른 투명도 포함)
                float2 uv : TEXCOORD0;
            };
            struct V2F
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            // 간단한 3D 값 잡음 (반짝임용)
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }
            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash13(i),                    Hash13(i + float3(1, 0, 0)), f.x);
                float b = lerp(Hash13(i + float3(0, 1, 0)),  Hash13(i + float3(1, 1, 0)), f.x);
                float c = lerp(Hash13(i + float3(0, 0, 1)),  Hash13(i + float3(1, 0, 1)), f.x);
                float d = lerp(Hash13(i + float3(0, 1, 1)),  Hash13(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }
            // 빛 쪽을 바라볼수록 밝아지는 정도 (헨예이-그린스타인 방식)
            // 빛과 직각으로 볼 때 = 1, 빛을 마주 볼수록 더 밝고, 등지면 조금 어두움
            half Scatter(half3 lightDir, half3 viewDir, half g)
            {
                half cosT = dot(-lightDir, viewDir);
                half gg = g * g;
                return pow((1.0 + gg) / max(1.0 + gg - 2.0 * g * cosT, 1e-3), 1.5);
            }

            V2F vert (Attributes v)
            {
                V2F o;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag (V2F i) : SV_Target
            {
                // 점 모양: 또렷한 심 + 은은하게 퍼지는 번짐
                float r = length(i.uv * 2.0 - 1.0);
                half core = 1.0 - smoothstep(_CoreRadius * (1.0 - _Softness), _CoreRadius, r);
                half halo = exp(-r * r * _HaloWidth) * _HaloStrength * (1.0 - smoothstep(0.85, 1.0, r));
                half shape = core + halo;
                if (shape <= 0.001) discard;

                // 반짝임: 먼지가 떠다니며 잡음 무늬를 지나갈 때마다 밝기가 오르내림
                float t = _Time.y * _TwinkleSpeed;
                float n = ValueNoise(i.positionWS * _TwinkleScale + float3(t * 0.37, t, t * 0.71));
                half twinkle = lerp(1.0, 0.12 + 2.2 * pow(saturate(n * 1.5 - 0.25), 2.0), _TwinkleAmount);

                half3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));

                // 이 자리에 닿는 빛의 양
                half3 light = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light l = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                    light += l.color * (l.distanceAttenuation * l.shadowAttenuation) * Scatter(l.direction, viewDir, _ForwardScatter);
                }
            #endif
                if (_MainLightWeight > 0)
                {
                    Light m = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                    light += m.color * (m.shadowAttenuation * _MainLightWeight);
                }
                light += SampleSH(half3(0, 0, -1)) * _AmbientWeight;

                // 빛이 아무리 세도 하얗게 타지 않도록 부드럽게 눌러 줌
                half3 lit01 = 1.0 - exp(-light * _LightGain);
                // 약하게 새어 나온 빛(빛줄기 바깥)에서는 먼지를 완전히 감춤
                half3 lit = _MaxBrightness * saturate((lit01 - _LightThreshold) / (1.0 - _LightThreshold));

                half3 rgb = _Color.rgb * i.color.rgb * lit * (_Intensity * shape * twinkle * i.color.a * _Color.a);
                // 채도 조절 (빛 색이 섞인 최종 색 기준)
                half luma = dot(rgb, half3(0.2126, 0.7152, 0.0722));
                rgb = max(0, lerp(luma.xxx, rgb, _Saturation));
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
