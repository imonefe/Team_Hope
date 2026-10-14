// 어두운 방(RoomLighting)에서만 기본 스프라이트가 빛을 받게 하는 셰이더.
// RoomLighting 이 방에 들어갈 때 Sprite-Lit-Default 스프라이트들의 머티리얼을 이걸로 잠시 바꾸고, 나가면 되돌린다.
//  _EH_SpriteLightBlend (전역, 스크립트가 조절): 0 = 원래 그대로(빛 무시), 1 = 빛을 받음
//  _EH_SpriteMinLight   (전역, 스크립트가 조절): 빛이 없을 때의 최소 밝기
// 받는 빛: 환경광 + 주 빛(태양광) + 점광원·스포트라이트. 2D 그림이라 방향은 크게 따지지 않고 거리만 따진다.
// 피격 반짝임(_FlashColor, _FlashAmount)도 이 셰이더 안에서 처리 → 반짝여도 조명이 벗겨지지 않음
//  _FlashLit: 0 = 반짝임 색은 조명과 상관없이 그대로 보임(피격 흰색), 1 = 반짝임 색도 조명을 받음(동결 틴트)
Shader "EventHorizon/SpriteLit3D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("색", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [HideInInspector] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        [HideInInspector] _FlashLit ("Flash Lit", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpriteLit3D"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            // 기본 스프라이트 셰이더(Sprite-Unlit-Default)와 같은 방식으로 색·뒤집기를 받음
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;
            float4 _FlashColor;
            float _FlashAmount;
            float _FlashLit;

            float _EH_SpriteLightBlend;
            float _EH_SpriteMinLight;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V2F
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            V2F vert (Attributes v)
            {
                V2F o = (V2F)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            #ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
            #endif
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color * _RendererColor;
            #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
            #endif
                return o;
            }

            half4 frag (V2F i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // 2D 그림은 카메라를 보고 있다고 보고 계산
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

                light = max(light, (half3)_EH_SpriteMinLight);
                half3 lightMul = lerp(half3(1, 1, 1), min(light, half3(2, 2, 2)), saturate(_EH_SpriteLightBlend));
                half3 flashRGB = lerp(_FlashColor.rgb, _FlashColor.rgb * lightMul, saturate(_FlashLit));
                c.rgb = lerp(c.rgb * lightMul, flashRGB, saturate(_FlashAmount));
                return c;
            }
            ENDHLSL
        }
    }
}
