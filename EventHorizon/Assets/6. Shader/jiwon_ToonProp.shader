// 3D 프랍 전용 "2D처럼 보이게" 쉐이더 (URP, Unity 2022.3 이상 / Unity 6 기준)
// 횡스크롤 게임에서 2D 캐릭터·타일 뒤에 놓이는 근경/중경/원경 3D 프랍용
//
// 2D 느낌을 만드는 요소
//  1) 2톤 셀 라이팅 + 그림자 색 틴트 (검정이 아니라 팔레트에 맞는 색으로 어두워짐)
//  2) Light Flatten : 곡면의 명암 변화를 줄여서 "그려진 덩어리"처럼 납작하게
//  3) Edge Wobble   : 명암 경계를 손으로 그린 것처럼 살짝 일렁이게
//  4) 잉크 아웃라인  : 월드 두께 + 최소 픽셀 두께 (거리에 따라 원화처럼 얇아짐)
//  5) Depth Tint    : 거리가 멀수록 배경색으로 녹아드는 공기 원근 (근/중/원경 분리용)
//  6) Paint Overlay : 화면 고정 붓결/종이 질감 오버레이 (선택)
Shader "Custom/jiwon_ToonProp"
{
    Properties
    {
        [Header(Base)]
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Toon Lighting)]
        _ShadeColor ("Shade Color", Color) = (0.55, 0.47, 0.58, 1)
        _ShadeThreshold ("Shade Threshold", Range(0, 1)) = 0.4
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.03
        _AmbientInfluence ("Ambient Influence", Range(0, 1)) = 0.2
        _Saturation ("Saturation", Range(0, 2)) = 1.0

        [Header(2D Look)]
        _LightFlatten ("Light Flatten", Range(0, 1)) = 0.5
        _WobbleStrength ("Edge Wobble Strength", Range(0, 0.3)) = 0.06
        _WobbleScale ("Edge Wobble Scale", Range(0.1, 20)) = 2
        [NoScaleOffset] _PaintTex ("Paint Overlay (screen space)", 2D) = "white" {}
        _PaintScale ("Paint Overlay Scale", Range(0.1, 10)) = 1.5
        _PaintStrength ("Paint Overlay Strength", Range(0, 1)) = 0

        [Header(Depth Tint)]
        _DepthTintColor ("Depth Tint Color", Color) = (0.45, 0.40, 0.50, 1)
        _DepthTintStart ("Tint Start Distance", Float) = 10
        _DepthTintEnd ("Tint End Distance", Float) = 60
        _DepthTintStrength ("Tint Strength", Range(0, 1)) = 0

        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color (black = off)", Color) = (0, 0, 0, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.7
        _RimSoftness ("Rim Softness", Range(0.001, 0.5)) = 0.05

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.08, 0.05, 0.04, 1)
        _OutlineWidth ("Outline Width (world units)", Range(0, 0.2)) = 0.02
        _OutlineMinPixels ("Outline Min Pixels", Range(0, 6)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // 모든 패스가 공유하는 선언 (SRP Batcher 호환을 위해 CBUFFER는 하나로 통일)
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_PaintTex);
        SAMPLER(sampler_PaintTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half4  _ShadeColor;
            half   _ShadeThreshold;
            half   _ShadeSoftness;
            half   _AmbientInfluence;
            half   _Saturation;
            half   _LightFlatten;
            half   _WobbleStrength;
            float  _WobbleScale;
            float  _PaintScale;
            half   _PaintStrength;
            half4  _DepthTintColor;
            float  _DepthTintStart;
            float  _DepthTintEnd;
            half   _DepthTintStrength;
            half4  _RimColor;
            half   _RimAmount;
            half   _RimSoftness;
            half4  _OutlineColor;
            float  _OutlineWidth;
            half   _OutlineMinPixels;
        CBUFFER_END

        // 간단한 해시 / 밸류 노이즈 (텍스처 없이 경계 일렁임에 사용)
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // 카메라 거리(뷰 깊이)에 따라 배경색으로 섞어주는 공기 원근
        float3 ApplyDepthTint(float3 color, float viewDepth)
        {
            float range = max(_DepthTintEnd - _DepthTintStart, 1e-4);
            float t = saturate((viewDepth - _DepthTintStart) / range) * _DepthTintStrength;
            return lerp(color, _DepthTintColor.rgb, t);
        }
        ENDHLSL

        // ------------------------------------------------------------
        // 1) 메인 툰 라이팅 패스
        // ------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half   fogFactor   : TEXCOORD4;
            };

            // 하프 램버트 기준으로 밝음/어두움을 끊고, wobble로 경계를 흔들어줌
            float ToonStep(float NdotL, float wobble)
            {
                float h = NdotL * 0.5 + 0.5 + wobble;
                return smoothstep(_ShadeThreshold - _ShadeSoftness,
                                  _ShadeThreshold + _ShadeSoftness, h);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = vn.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(vp);
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(tex.a - _Cutoff);

                // 채도 조절
                float3 albedo = tex.rgb;
                float lum = dot(albedo, float3(0.2126, 0.7152, 0.0722));
                albedo = lerp(lum.xxx, albedo, _Saturation);

                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 Nraw = normalize(IN.normalWS);
                Nraw *= IS_FRONT_VFACE(facing, 1.0, -1.0);

                // Light Flatten: 노멀을 카메라 쪽으로 끌어당겨 곡면 명암을 줄임
                float3 N = normalize(lerp(Nraw, V, _LightFlatten));

                // 명암 경계 일렁임 (월드 좌표 기준이라 카메라가 움직여도 경계가 안 흔들림)
                float wobNoise = ValueNoise(IN.positionWS.xy * _WobbleScale + IN.positionWS.z * 0.37);
                float wobble = (wobNoise * 2.0 - 1.0) * _WobbleStrength;

                // 그림자 좌표 (캐스케이드는 픽셀 단위로 계산해야 경계가 정확함)
                float4 shadowCoord = IN.shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif

                // 메인 라이트
                Light mainLight = GetMainLight(shadowCoord);
                float band = ToonStep(dot(N, mainLight.direction), wobble);
                float shadowStep = smoothstep(0.5 - _ShadeSoftness + wobble,
                                              0.5 + _ShadeSoftness + wobble,
                                              mainLight.shadowAttenuation);
                float lit = band * shadowStep;

                float3 tone = lerp(_ShadeColor.rgb, float3(1, 1, 1), lit);
                float3 color = albedo * tone * mainLight.color * mainLight.distanceAttenuation;

                // 환경광 (너무 어두워지는 것 방지)
                color += albedo * SampleSH(N) * _AmbientInfluence;

                // 추가 라이트 (포인트/스팟)
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; ++i)
                    {
                        Light l = GetAdditionalLight(i, IN.positionWS);
                        float b = ToonStep(dot(N, l.direction), wobble);
                        color += albedo * l.color * l.distanceAttenuation * l.shadowAttenuation * b;
                    }
                #endif

                // 림 라이트 (실루엣 기준이라 Flatten 전의 노멀 사용, 빛 받는 쪽에만)
                float rimDot = 1.0 - saturate(dot(Nraw, V));
                float rim = smoothstep(_RimAmount - _RimSoftness, _RimAmount + _RimSoftness, rimDot) * lit;
                color += rim * _RimColor.rgb;

                // 화면 고정 붓결/종이 질감 오버레이
                float2 suv = IN.positionCS.xy / _ScreenParams.xy;
                suv.x *= _ScreenParams.x / _ScreenParams.y;
                float3 paint = SAMPLE_TEXTURE2D(_PaintTex, sampler_PaintTex, suv * _PaintScale).rgb;
                color *= lerp(float3(1, 1, 1), paint, _PaintStrength);

                // 거리 기반 색 섞기 -> 안개
                float viewDepth = abs(TransformWorldToView(IN.positionWS).z);
                color = ApplyDepthTint(color, viewDepth);
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // 2) 아웃라인 패스 (뒤집힌 메시 방식)
        //    - 두께 = 월드 단위 (원근에 따라 얇아짐) + 최소 픽셀 두께 보장
        //    - 하드 엣지(각진 로우폴리) 메시는 모서리에서 선이 끊길 수 있음
        // ------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  viewDepth  : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                // 두께가 모두 0이면 화면 밖으로 보내서 아무것도 그리지 않음
                if (_OutlineWidth < 0.0001 && _OutlineMinPixels < 0.01)
                {
                    OUT.positionCS = float4(2, 2, 2, 1);
                    return OUT;
                }

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                // 월드 단위로 노멀 방향 확장
                float4 posCS0 = TransformWorldToHClip(positionWS);
                float4 posCS = TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);

                // 화면에서 실제로 밀려난 픽셀 수가 최소치보다 작으면 모자란 만큼 더 밀어줌
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                dirCS = dirCS / max(length(dirCS), 1e-4);
                float2 movedPx = (posCS.xy / posCS.w - posCS0.xy / posCS0.w) * _ScreenParams.xy * 0.5;
                float extraPx = max(_OutlineMinPixels - length(movedPx), 0.0);
                posCS.xy += dirCS * (extraPx * 2.0 / _ScreenParams.xy) * posCS.w;

                OUT.positionCS = posCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.viewDepth = abs(TransformWorldToView(positionWS).z);
                OUT.fogFactor = ComputeFogFactor(posCS.z);
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(a - _Cutoff);

                // 먼 프랍의 선은 배경색에 녹아들도록 같은 Depth Tint 적용
                float3 color = ApplyDepthTint(_OutlineColor.rgb, IN.viewDepth);
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // 3) 그림자 캐스터 (알파 클립 반영)
        // ------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // [2026-10-10] Shadows.hlsl이 쓰는 LerpWhiteTo가 이 파일에 정의돼 있어 먼저 포함 (없으면 컴파일 오류)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // 4) 깊이 패스 (카메라 깊이 텍스처 / 깊이 프리패스용)
        // ------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half DepthFrag(Varyings IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }
}
