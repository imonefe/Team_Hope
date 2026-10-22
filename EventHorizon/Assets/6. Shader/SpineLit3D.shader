// [2026-10-21 추가] 스파인 캐릭터(보스·궁수 등)가 어두운 방(RoomLighting)에서 빛을 받게 하는 셰이더.
// 스파인 기본 셰이더(Spine/Skeleton)는 빛을 계산하지 않아서, RoomLighting 이 방에 들어갈 때
// 스파인의 원래 머티리얼 대신 이 셰이더를 쓴 머티리얼로 잠시 바꿔 끼운다(SkeletonRenderer.CustomMaterialOverride). 나가면 되돌린다.
// 빛 계산은 SpriteLit3D 와 같다: 환경광 + 주 빛 + 점광원·스포트라이트, 최소 밝기, 전역값으로 켜고 끔.
//  _EH_SpriteLightBlend (전역): 0 = 원래 그대로(빛 무시), 1 = 빛을 받음
//  _EH_SpineMinLight    (전역): 빛이 없을 때의 최소 밝기 (스파인 전용, RoomLighting.spineMinBrightness)
//  _EH_SpineKeepColor   (전역): 1 = 빛의 색은 무시하고 밝기만 곱함(캐릭터 색·채도 유지), 0 = 빛 색까지 곱함
// 그림 방식은 스파인 기본과 같다: 미리 곱한 알파(PMA) 블렌딩, 정점 색(스킨 색·투명도) 반영.
Shader "EventHorizon/SpineLit3D"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture", Int) = 0
        _Cutoff ("Shadow alpha cutoff", Range(0,1)) = 0.1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpineLit3D"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ _STRAIGHT_ALPHA_INPUT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _EH_SpriteLightBlend;
            float _EH_SpineMinLight;
            float _EH_SpineKeepColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct V2F
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            // 스파인 정점 색은 감마 공간 + 미리 곱한 알파 → 선형 색 공간이면 변환 (Spine-Common 의 PMAGammaToTargetSpace 와 같음)
            half4 PMAGammaToTarget(half4 c)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                if (c.a <= 0.0001) return half4(0, 0, 0, c.a);
                return half4(SRGBToLinear(c.rgb / c.a) * c.a, c.a);
            #endif
            }

            V2F vert (Attributes v)
            {
                V2F o = (V2F)0;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = PMAGammaToTarget(v.color);
                return o;
            }

            half4 frag (V2F i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            #if defined(_STRAIGHT_ALPHA_INPUT)
                tex.rgb *= tex.a;
            #endif
                half4 c = tex * i.color;

                // 2D 그림은 카메라를 보고 있다고 보고 계산 (SpriteLit3D 와 같음)
                half3 n = half3(0, 0, -1);
                half3 light = SampleSH(n);

                Light mainLight = GetMainLight();
                light += mainLight.color * mainLight.distanceAttenuation * saturate(dot(n, mainLight.direction) * 0.5 + 0.5);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint li = 0u; li < count; ++li)
                    {
                        Light l = GetAdditionalLight(li, i.positionWS);
                        light += l.color * l.distanceAttenuation;
                    }
                #endif

                // [2026-10-21] 색 유지: 빛의 밝기(휘도)만 써서 파란 환경광 등에 캐릭터 색이 물들지 않게
                half lum = dot(light, half3(0.2126, 0.7152, 0.0722));
                light = lerp(light, lum.xxx, saturate(_EH_SpineKeepColor));
                light = max(light, (half3)_EH_SpineMinLight);
                half3 lightMul = lerp(half3(1, 1, 1), min(light, half3(2, 2, 2)), saturate(_EH_SpriteLightBlend));
                c.rgb *= lightMul;   // PMA 라 밝기만 곱하면 됨
                return c;
            }
            ENDHLSL
        }
    }
}
