// 노드 캔버스 아이콘에 마우스를 올렸을 때 아이콘 바깥 윤곽에 번지는 발광 효과 (UI 셰이더).
// MapButton 이 아이콘보다 조금 큰 "HoverGlow" 이미지를 만들어 같은 스프라이트로 이 셰이더를 씌운다.
//  - 이미지가 아이콘보다 _Pad 배 크므로, 스프라이트 좌표를 다시 맞춰서 아이콘 모양 그대로 바깥쪽에만 빛을 그린다
//  - 세기는 이미지 색의 알파값 (MapButton 이 0 ↔ 1 로 부드럽게 바꿈)
//  - 더하기 방식이라 어두운 배경 위에서 빛처럼 보인다
Shader "EventHorizon/UINodeGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("스프라이트 (자동)", 2D) = "white" {}
        _Color ("색 (알파 = 세기, 스크립트가 조절)", Color) = (1, 1, 1, 1)
        _GlowColor ("발광 색", Color) = (1, 0.8, 0.28, 1)
        _Pad ("발광 이미지 크기 배율 (MapButton 의 glowPadding 과 같게)", Float) = 1.8
        _ShapeScale ("빛나는 테두리 모양 크기 (아이콘 대비 배율, 1 = 아이콘에 딱 붙음)", Range(1, 1.4)) = 1.18
        _GlowRadius ("빛이 퍼지는 거리 (아이콘 크기 대비)", Range(0, 0.4)) = 0.17
        _GlowStrength ("가장자리 빛의 진하기 (높을수록 가장자리 근처가 꽉 참)", Range(0, 8)) = 2.6
        _GlowOpacity ("빛 전체 불투명도", Range(0, 1)) = 0.55
        _OutlineWidth ("가는 외곽선 두께 (아이콘 크기 대비)", Range(0, 0.06)) = 0.012
        _OutlineStrength ("아이콘 테두리 바로 바깥의 밝은 선 세기 (0 = 없음)", Range(0, 2)) = 0.6
        _InnerGlow ("아이콘 안쪽을 밝히는 정도 (0 = 안쪽은 전혀 안 밝힘)", Range(0, 1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]
        Blend SrcAlpha One

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            fixed4 _GlowColor;
            float _Pad;
            float _ShapeScale;
            float _GlowRadius;
            float _OutlineWidth;
            float _GlowStrength;
            float _GlowOpacity;
            float _OutlineStrength;
            float _InnerGlow;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 스프라이트 바깥은 비어 있는 것으로 취급
            float SpriteAlpha (float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).a * inside;
            }

            // 아이콘 모양을 _ShapeScale 배로 키운 모양 (빛나는 테두리는 이 큰 모양을 따라 그림)
            float ShapeAlpha (float2 uv)
            {
                return SpriteAlpha((uv - 0.5) / _ShapeScale + 0.5);
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = (IN.texcoord - 0.5) * _Pad + 0.5;   // 큰 이미지 → 아이콘 좌표
                float center = SpriteAlpha(uv);    // 실제 아이콘 (빛이 절대 안 들어가는 곳)
                float shape = ShapeAlpha(uv);      // 키운 테두리 모양

                // 12방향 × 6거리로 아이콘 모양을 넓게 흐려서, 가장자리에서 진하고 멀어질수록 서서히 사라지는 빛을 만든다
                float blur = 0.0;     // 넓게 흐린 모양 (빛)
                float near = 0.0;     // 아주 가까이만 흐린 모양 (안쪽 밝힘용)
                float dilate = 0.0;   // 가는 외곽선용
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    float ang = (i + 0.5) * 0.52359878;   // 2π / 12
                    float2 dir = float2(cos(ang), sin(ang)) * _GlowRadius;
                    blur += ShapeAlpha(uv + dir * 0.10) * 0.30
                          + ShapeAlpha(uv + dir * 0.22) * 0.22
                          + ShapeAlpha(uv + dir * 0.36) * 0.17
                          + ShapeAlpha(uv + dir * 0.52) * 0.13
                          + ShapeAlpha(uv + dir * 0.70) * 0.10
                          + ShapeAlpha(uv + dir * 0.90) * 0.08;
                    near += SpriteAlpha(uv + dir * 0.10);
                    dilate = max(dilate, ShapeAlpha(uv + float2(cos(ang), sin(ang)) * _OutlineWidth));
                }
                blur /= 12.0;
                near /= 12.0;

                float outside = 1.0 - shape;
                // 1 - e^(-x): 가장자리 근처는 부드럽게 차고, 멀어질수록 자연스럽게 사라지는 빛의 감쇠
                float glow = (1.0 - exp(-blur * _GlowStrength)) * outside;
                float outlineA = saturate(dilate - shape) * _OutlineStrength;   // (line 은 예약어라 이름 변경)
                float inner = center * _InnerGlow * saturate(1.3 - near);         // 아이콘 안쪽, 특히 가장자리를 살짝 밝힘
                // 키운 테두리 바깥으로만 빛, 실제 아이콘 위에는 절대 안 겹치게 (아이콘~테두리 사이는 비어 있음)
                float a = saturate((glow + outlineA) * _GlowOpacity * (1.0 - center) + inner) * IN.color.a;

                // [리니어 색공간 보정] 프로젝트가 리니어 색공간이라 더하기 빛이 감마(눈에 보이는) 기준보다 훨씬 진하게 보여서
                // 아이콘 전체가 노랗게 차 보였다. 어두운~중간 밝기 배경 위에서 감마 기준과 비슷하게 보이도록 세기를 눌러 준다.
                #ifndef UNITY_COLORSPACE_GAMMA
                a = a * (0.35 + 0.65 * a);
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif
                return fixed4(_GlowColor.rgb, a);
            }
            ENDCG
        }
    }
}
