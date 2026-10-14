// 퍼즈(Q) 중 주인공이 남기는 "멈춘 잔상" 셰이더.
//  - 주인공 그림을 푸른 단색 + 옅은 원래 명암 + 밝은 윤곽선 + 고정된 가로줄로 그린다 (깜빡임 없음)
//  - 퍼즈가 끝나면 _Dissolve 를 0→1 로 올려서 금 → 조각 단위로 깨지며 사라진다
// 조명의 영향을 받지 않는다. PauseAfterimage 스크립트가 스프라이트 렌더러에 이 재질을 씌우고
// _Dissolve / _Seed 를 렌더러마다 따로 넣어준다.
Shader "EventHorizon/PauseAfterimage"
{
    Properties
    {
        [PerRendererData] _MainTex ("스프라이트 (자동)", 2D) = "white" {}
        _Color ("잔상 색", Color) = (0.45, 0.85, 1, 1)
        _Fill ("단색으로 채우는 정도 (0 = 원래 명암, 1 = 완전 단색)", Range(0, 1)) = 0.7
        _RimWidth ("윤곽선 두께 (원본 그림 픽셀)", Range(0, 40)) = 14
        _RimColor ("윤곽선 색", Color) = (0.85, 0.97, 1, 1)
        _ScanCount ("가로줄 촘촘함 (월드 1칸당)", Range(0, 80)) = 18
        _ScanAmount ("가로줄 진하기", Range(0, 1)) = 0.25
        _ShardScale ("깨지는 조각 촘촘함 (월드 1칸당 개수)", Range(1, 30)) = 4.5
        _CrackWidth ("깨지기 직전 금 두께", Range(0, 0.3)) = 0.07
        _EdgeGlow ("깨질 때 조각 테두리 빛 세기", Range(0, 4)) = 1.6
        _EdgeColor ("깨질 때 테두리 색", Color) = (0.75, 0.95, 1, 1)
        [HideInInspector] _Dissolve ("깨짐 진행 (스크립트가 조절)", Range(0, 1)) = 0
        [HideInInspector] _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PauseAfterimage"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _Fill;
                float _RimWidth;
                float4 _RimColor;
                float _ScanCount;
                float _ScanAmount;
                float _ShardScale;
                float _CrackWidth;
                float _EdgeGlow;
                float4 _EdgeColor;
                float _Dissolve;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Hash22(float2 p)
            {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }

            Varyings Vert (Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            float SampleAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

            float4 Frag (Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float alpha = tex.a * input.color.a;
                clip(alpha - 0.002);

                // ── 1) 윤곽선: 주변 4방향 중 하나라도 비어 있으면 가장자리 ──
                float2 o = _MainTex_TexelSize.xy * _RimWidth;
                float nearMin = min(min(SampleAlpha(input.uv + float2(o.x, 0)), SampleAlpha(input.uv - float2(o.x, 0))),
                                    min(SampleAlpha(input.uv + float2(0, o.y)), SampleAlpha(input.uv - float2(0, o.y))));
                float rim = saturate(tex.a - nearMin);

                // ── 2) 푸른 단색 + 옅은 원래 명암 ──
                float luma = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float3 col = lerp(_Color.rgb * (0.35 + luma * 1.3), _Color.rgb, _Fill);
                col = lerp(col, _RimColor.rgb, rim);

                // ── 3) 고정된 가로줄 (월드 좌표 기준이라 움직이지 않음) ──
                float scan = 1.0 - _ScanAmount * step(0.5, frac(input.positionWS.y * _ScanCount));
                col *= scan;

                // ── 4) 깨짐: 월드 좌표 보로노이 조각마다 사라지는 순서를 무작위로 ──
                if (_Dissolve > 0.0001)
                {
                    float2 p = input.positionWS.xy * _ShardScale + _Seed;
                    float2 cell = floor(p);
                    float2 f = frac(p);
                    float d1 = 8.0, d2 = 8.0;
                    float2 nearest = 0;
                    for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 pt = g + Hash22(cell + g);
                        float d = length(pt - f);
                        if (d < d1) { d2 = d1; d1 = d; nearest = cell + g; }
                        else if (d < d2) { d2 = d; }
                    }
                    float edgeDist = d2 - d1;                        // 조각 경계까지 거리
                    float order = Hash21(nearest + _Seed * 0.37);   // 이 조각이 사라지는 순서 (0~1)

                    // 앞부분(0~0.3): 금만 빛나며 생김 / 뒷부분: 조각이 순서대로 사라짐
                    float crackOn = saturate(_Dissolve / 0.3);
                    float threshold = saturate((_Dissolve - 0.15) / 0.85) * 1.1;
                    float visible = step(threshold, order);
                    float nearGone = (1.0 - smoothstep(0.0, 0.15, order - threshold)) * visible * step(0.001, threshold);
                    float crack = (1.0 - smoothstep(0.0, _CrackWidth, edgeDist)) * crackOn;

                    col += _EdgeColor.rgb * _EdgeGlow * max(crack, nearGone);
                    alpha *= visible;
                    alpha = saturate(alpha + crack * visible * 0.5 * tex.a * input.color.a);   // 금은 그림 안쪽에만
                }

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}
