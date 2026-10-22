// =====================================================================
//  SlashSprite.shader — 불똥 / 섬광 같은 단순 이펙트 쿼드용 (URP 2D / Unlit)
//  _MainTex 를 원하는 이미지로 바꾸면 됨 (알파 있는 흰색 이미지 권장).
// =====================================================================
Shader "GameEffect/SlashSprite"
{
    Properties
    {
        _MainTex    ("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        _Intensity  ("Intensity", Range(0, 5)) = 1.5
        _Additive   ("Additive (0 = alpha, 1 = add)", Range(0, 1)) = 1
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
            Name "SlashSprite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Intensity;
                float  _Additive;
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float a = saturate(tex.a * _Color.a * i.color.a);
                float3 col = tex.rgb * _Color.rgb * i.color.rgb * _Intensity;
                return float4(col * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
