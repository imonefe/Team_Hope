// 2D 스프라이트/타일맵과 어울리는 3D 프랍용 툰 쉐이더 - 초안 (URP, Unity 2022.3 이상 / Unity 6)
//
//  1) 질감 정리     : 나무결/돌 무늬 같은 약한 무늬는 평평한 색으로, 큰 대비는 선명하게 (블러 없음)
//  2) 2D처럼 보이기 : 얇은 잉크 아웃라인(텍스처 색을 어둡게), 평평한 색면, 채도/색조 맞춤
//  3) 라이팅 연동   : 색 = 텍스처 x (환경광 + 빛 색 x 세기)  -> 맵을 어둡게 하면 색감이 자연스럽게 따라감
//  4) 경계 그림자   : 그라데이션이 아니라 계단식 3톤 (밝음 / 중간 그림자 / 깊은 그림자), 드리운 그림자도 딱 끊김
//
// 머티리얼 Shader 목록에서 "0_Jiwon" 항목은 이름 맨 앞이 숫자 0이라 2D 카테고리보다 위쪽에 보입니다.
Shader "0_Jiwon/Toon 2D Draft"
{
    Properties
    {
        [Header(Base)]
        [MainColor] _BaseColor ("Base Color (tint)", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Clean Flatten (no blur))]
        _FlattenAmount ("Flatten Amount (1 = flat color)", Range(0, 1)) = 0.85
        _FlatMip ("Flat Color Mip (higher = more uniform)", Range(3, 10)) = 7
        _DetailThreshold ("Keep Detail Above (contrast)", Range(0, 0.5)) = 0.10
        _DetailSoftness ("Detail Softness", Range(0.005, 0.3)) = 0.06

        [Header(2D Color Match)]
        _Saturation ("Saturation", Range(0, 2)) = 0.85
        _ColorTint ("Color Tint (multiply)", Color) = (1, 1, 1, 1)

        [Header(Stepped Shadow)]
        _Threshold1 ("Deep Shade Threshold", Range(0, 1)) = 0.30
        _Threshold2 ("Shade Threshold", Range(0, 1)) = 0.55
        _MidLevel ("Mid Shade Light Level", Range(0, 1)) = 0.35
        _EdgeSoftness ("Edge Softness (0 = hard)", Range(0, 0.2)) = 0.01
        _NormalSoften ("Normal Soften (reduce faceted lighting)", Range(0, 1)) = 0.3

        [Header(Lighting Response)]
        _LightIntensity ("Light Intensity", Range(0, 3)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1

        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color (black = off)", Color) = (0, 0, 0, 1)
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.6
        _RimSoftness ("Rim Softness", Range(0.01, 0.5)) = 0.2

        [Header(Outline)]
        _OutlineColor ("Outline Color (fixed)", Color) = (0.03, 0.03, 0.03, 1)
        _OutlineFromTexture ("Outline Color From Texture", Range(0, 1)) = 1
        _OutlineDarkness ("Outline Darkness", Range(0.05, 0.8)) = 0.35
        _OutlineColorMip ("Outline Color Blur Mip", Range(0, 8)) = 4
        _OutlineWidth ("Outline Width (world units)", Range(0, 0.2)) = 0.01
        _OutlineMinPixels ("Outline Min Pixels", Range(0, 6)) = 1
        _OutlineMaxPixels ("Outline Max Pixels", Range(0, 8)) = 2
        [Toggle(_OUTLINE_SMOOTH_NORMAL)] _OutlineSmoothNormal ("Use Baked Smooth Normals (UV4)", Float) = 0

        [Header(Depth Tint)]
        _DepthTintColor ("Depth Tint Color", Color) = (0.12, 0.09, 0.08, 1)
        _DepthTintStart ("Tint Start Distance", Float) = 10
        _DepthTintEnd ("Tint End Distance", Float) = 60
        _DepthTintStrength ("Tint Strength (0 = off)", Range(0, 1)) = 0
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

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half   _FlattenAmount;
            float  _FlatMip;
            half   _DetailThreshold;
            half   _DetailSoftness;
            half   _Saturation;
            half4  _ColorTint;
            half   _Threshold1;
            half   _Threshold2;
            half   _MidLevel;
            half   _EdgeSoftness;
            half   _NormalSoften;
            half   _LightIntensity;
            half   _AmbientStrength;
            half4  _RimColor;
            half   _RimThreshold;
            half   _RimSoftness;
            half4  _OutlineColor;
            half   _OutlineFromTexture;
            half   _OutlineDarkness;
            float  _OutlineColorMip;
            float  _OutlineWidth;
            half   _OutlineMinPixels;
            half   _OutlineMaxPixels;
            half4  _DepthTintColor;
            float  _DepthTintStart;
            float  _DepthTintEnd;
            half   _DepthTintStrength;
        CBUFFER_END

        // JiwonPropLight 컴포넌트가 채워주는 전역 값 (프랍 전용 라이트, 없으면 w = 0)
        float4 _JiwonPropLightDir;     // xyz = 빛이 오는 쪽 방향, w = 1이면 사용 중
        float4 _JiwonPropLightColor;   // rgb = 주광 색 x 세기
        float4 _JiwonPropShadeColor;   // rgb = 그림자 면 환경광 (밝기가 곧 그림자의 어두운 정도)
        float4 _JiwonPropRimColor;     // rgb = 가장자리 빛 색 x 세기
        float4 _JiwonPropHeight;       // x = 바닥 Y, y = 윗부분 Y, z = 바닥 어두움(0~1)

        float JiwonLuma(float3 c)
        {
            return dot(c, float3(0.2126, 0.7152, 0.0722));
        }

        // 블러 없이 질감 정리하기:
        //  - 매우 낮은 밉맵(거의 평균색)을 "평평한 기본색"으로 쓰고
        //  - 원본과의 차이(디테일) 중 대비가 큰 것만 원본 그대로 선명하게 남김
        //  => 나뭇결/돌 무늬 같은 약한 무늬는 평평한 색으로, 책 등 큰 대비는 또렷하게 유지 (번짐 없음)
        float3 FlattenColor(float2 uv, float3 full)
        {
            float3 baseCol = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, _FlatMip).rgb;
            float3 d = full - baseCol;
            float mag = max(abs(d.r), max(abs(d.g), abs(d.b)));
            float keep = smoothstep(_DetailThreshold, _DetailThreshold + _DetailSoftness, mag);
            return baseCol + d * keep;
        }

        // 카메라 거리(뷰 깊이)에 따라 배경색으로 섞어주는 공기 원근 (Strength 0이면 꺼짐)
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

            // 계단식 명암: 0 = 깊은 그림자, Mid Level = 중간 그림자, 1 = 밝음
            float ToonLevel(float NdotL)
            {
                float h = NdotL * 0.5 + 0.5;
                float s = max(_EdgeSoftness, 0.002);
                float b1 = smoothstep(_Threshold1 - s, _Threshold1 + s, h);
                float b2 = smoothstep(_Threshold2 - s, _Threshold2 + s, h);
                return b1 * _MidLevel + b2 * (1.0 - _MidLevel);
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
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);

                // 1) 질감 정리: 약한 무늬는 평평하게, 큰 대비는 선명하게 (블러 아님)
                float3 flatCol = FlattenColor(IN.uv, tex.rgb);
                float3 albedo = lerp(tex.rgb, flatCol, _FlattenAmount) * _BaseColor.rgb * _ColorTint.rgb;

                // 2) 2D 팔레트에 맞추기 위한 채도 조절
                float lum = JiwonLuma(albedo);
                albedo = lerp(lum.xxx, albedo, _Saturation);

                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 Nraw = normalize(IN.normalWS);
                Nraw *= IS_FRONT_VFACE(facing, 1.0, -1.0);

                // 각진 로우폴리의 면 단위 명암 차이를 줄여서 평평하게
                float3 N = normalize(lerp(Nraw, V, _NormalSoften));

                // 그림자 좌표 (캐스케이드는 픽셀 단위로 계산해야 경계가 정확함)
                float4 shadowCoord = IN.shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif

                // 프랍 전용 라이트(JiwonPropLight)가 켜져 있으면 유니티 메인 라이트 대신 그걸 사용
                bool usePropLight = _JiwonPropLightDir.w > 0.5;

                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = usePropLight ? _JiwonPropLightDir.xyz : (float3)mainLight.direction;
                float shadowAtten = usePropLight ? 1.0 : (float)mainLight.shadowAttenuation;

                // 드리운 그림자도 부드럽게 번지지 않고 딱 끊기게
                float edge = max(_EdgeSoftness, 0.002);
                float shadowStep = smoothstep(0.5 - edge, 0.5 + edge, shadowAtten);
                float level = ToonLevel(dot(N, lightDir)) * shadowStep;

                // 3) 라이팅 연동: 받는 빛 = 환경광 + 빛 색 x 세기 x 단계
                float3 radiance;
                if (usePropLight)
                {
                    radiance = _JiwonPropShadeColor.rgb + _JiwonPropLightColor.rgb * level;

                    // 아래쪽은 어둡고 위쪽은 밝게 (높이 그라데이션)
                    float hgt = saturate((IN.positionWS.y - _JiwonPropHeight.x) / max(_JiwonPropHeight.y - _JiwonPropHeight.x, 1e-3));
                    radiance *= lerp(1.0 - _JiwonPropHeight.z, 1.0, hgt);
                }
                else
                {
                    // 환경광은 방향과 상관없이 평균값을 써서 그림자 면이 평평하게 유지됨
                    float3 ambient = SampleSH(float3(0, 0, 0)) * _AmbientStrength;
                    radiance = ambient + mainLight.color * (mainLight.distanceAttenuation * _LightIntensity) * level;
                }

                float3 color = albedo * radiance;

                // 추가 라이트 (포인트/스팟): 같은 계단식 명암 적용
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; ++i)
                    {
                        Light l = GetAdditionalLight(i, IN.positionWS);
                        float lv = ToonLevel(dot(N, l.direction));
                        color += albedo * l.color * (l.distanceAttenuation * l.shadowAttenuation * _LightIntensity * lv);
                    }
                #endif

                // 림 라이트 (실루엣 기준이라 Soften 전의 노멀 사용, 빛 받는 쪽에만)
                float rimDot = 1.0 - saturate(dot(Nraw, V));
                float rim = smoothstep(_RimThreshold, _RimThreshold + _RimSoftness, rimDot) * level;
                color += rim * (_RimColor.rgb + _JiwonPropRimColor.rgb);

                // 거리 기반 색 섞기(기본 꺼짐) -> 안개
                float viewDepth = abs(TransformWorldToView(IN.positionWS).z);
                color = ApplyDepthTint(color, viewDepth);
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // 2) 아웃라인 패스 (뒤집힌 메시 방식)
        //    - 얇은 선: 월드 두께를 쓰되 화면에서 Min~Max 픽셀 안으로 제한
        //    - 선 색 = 그 부분 텍스처 색을 어둡게 한 색 (갈색은 짙은 갈색, 파랑은 짙은 파랑)
        //    - 각진 로우폴리는 OutlineNormalBaker로 UV4에 구운 노멀을 쓰면 모서리가 끊기지 않음
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
            #pragma shader_feature_local_vertex _OUTLINE_SMOOTH_NORMAL

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                #if defined(_OUTLINE_SMOOTH_NORMAL)
                float3 smoothNormalOS : TEXCOORD3;
                #endif
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

                // 구워둔 스무스 노멀(UV4)이 있으면 사용, 없으면 원래 노멀
                float3 outlineNormalOS = IN.normalOS;
                #if defined(_OUTLINE_SMOOTH_NORMAL)
                    if (dot(IN.smoothNormalOS, IN.smoothNormalOS) > 1e-6)
                        outlineNormalOS = IN.smoothNormalOS;
                #endif

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(outlineNormalOS);

                // 월드 두께가 화면에서 몇 픽셀이 되는지 계산 -> Min~Max 픽셀로 제한
                float4 posCS0 = TransformWorldToHClip(positionWS);
                float4 posCSW = TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
                float2 movedPx = (posCSW.xy / posCSW.w - posCS0.xy / posCS0.w) * _ScreenParams.xy * 0.5;
                float px = clamp(length(movedPx), _OutlineMinPixels, max(_OutlineMinPixels, _OutlineMaxPixels));

                // 클립 공간에서 노멀 방향으로 px 만큼 밀어냄
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                dirCS = dirCS / max(length(dirCS), 1e-4);

                float4 posCS = posCS0;
                posCS.xy += dirCS * (px * 2.0 / _ScreenParams.xy) * posCS.w;

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

                // 텍스처 색(흐리게 평균낸 값)을 어둡게 -> 색마다 다른 선 색
                float3 texCol = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, IN.uv, _OutlineColorMip).rgb * _BaseColor.rgb;
                float l = JiwonLuma(texCol);
                texCol = max(lerp(l.xxx, texCol, 1.25), 0.0);   // 어둡게 해도 색감이 죽지 않게 채도 살짝 보정
                float3 derived = texCol * _OutlineDarkness;
                float3 color = lerp(_OutlineColor.rgb, derived, _OutlineFromTexture);

                color = ApplyDepthTint(color, IN.viewDepth);
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
