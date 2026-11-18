// [2026-11-19 추가] 중간 보스 방 전환용 회로 덮개 (UI 전체 화면 Image 에 씀)
// _Progress 0 → 1 : 화면 가장자리 칸부터 회로선이 먼저 뻗어 나오고, 이어서 칸이 남색으로 덮인다 (1이면 화면 전체가 가려짐)
// _Progress 1 → 0 : 가운데 칸부터 다시 걷힌다
// _UTime 은 TransitionCanvas 가 실제 시간(Time.unscaledTime)을 넣어 준다 (게임이 멈춰 있어도 회로 빛이 흐르게)
Shader "EventHorizon/UI/CircuitCover"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LineColor ("회로선 색", Color) = (0.35, 0.9, 1, 1)
        _BgColor ("덮는 바탕 색", Color) = (0.02, 0.05, 0.11, 1)
        _CellPx ("칸 크기 (화면 픽셀)", Float) = 56
        _LineWidth ("선 굵기 (칸 비율)", Range(0.02, 0.3)) = 0.08
        _Progress ("진행 (0~1)", Range(0, 1)) = 0
        _Edge ("칸 하나가 덮이는 데 걸리는 구간", Range(0.05, 0.5)) = 0.18
        _Spread ("칸마다 순서 섞임", Range(0, 1)) = 0.35
        _UTime ("시간 (스크립트가 넣음)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            fixed4 _Color, _LineColor, _BgColor;
            float _CellPx, _LineWidth, _Progress, _Edge, _Spread, _UTime;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float segDist(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 cells = max(_ScreenParams.xy / max(_CellPx, 4), 1);
                float2 g = i.uv * cells;
                float2 cid = floor(g);
                float2 f = frac(g);
                float r = hash21(cid);

                // 칸이 덮이는 순서: 가장자리(0) → 가운데(1), 칸마다 조금씩 섞음
                float2 cuv = (cid + 0.5) / cells;
                float edge = max(abs(cuv.x - 0.5), abs(cuv.y - 0.5)) * 2;
                float t = saturate(1 - edge) * (1 - _Spread) + r * _Spread;
                float P = _Progress * (1 + _Edge);
                float a = saturate((P - t) / _Edge);

                // 회로선: 이웃 칸과 맞닿는 변마다 같은 난수로 연결 여부를 정해 선이 끊기지 않게 함
                float cR = step(0.45, hash21(cid * 2 + float2(1, 0) + 17.0));
                float cL = step(0.45, hash21((cid - float2(1, 0)) * 2 + float2(1, 0) + 17.0));
                float cU = step(0.5, hash21(cid * 2 + float2(0, 1) + 31.0));
                float cD = step(0.5, hash21((cid - float2(0, 1)) * 2 + float2(0, 1) + 31.0));
                float2 c = float2(0.5, 0.5);
                float d = 10;
                if (cR > 0) d = min(d, segDist(f, c, float2(1.0, 0.5)));
                if (cL > 0) d = min(d, segDist(f, c, float2(0.0, 0.5)));
                if (cU > 0) d = min(d, segDist(f, c, float2(0.5, 1.0)));
                if (cD > 0) d = min(d, segDist(f, c, float2(0.5, 0.0)));
                float n = cR + cL + cU + cD;
                // 끝점·갈림길에는 동그란 단자
                if (n == 1 || n >= 3) d = min(d, abs(length(f - c) - 0.13));
                // 연결이 없는 칸 일부에는 작은 칩
                if (n == 0 && r > 0.6) { float2 q = abs(f - c) - float2(0.18, 0.12); d = min(d, abs(max(q.x, q.y))); }

                float aa = 1.5 / max(_CellPx, 4);
                float lineMask = 1 - smoothstep(_LineWidth * 0.5, _LineWidth * 0.5 + aa, d);

                // 선 위를 흐르는 빛
                float pulse = 0.5 + 0.5 * sin(_UTime * 5 - (cid.x + cid.y) * 0.8 + r * 6.283);

                float lineA = smoothstep(0.0, 0.35, a);   // 선이 먼저
                float fill = smoothstep(0.35, 1.0, a);    // 이어서 바탕이 덮임
                float lead = lineA * (1 - fill);          // 막 뻗어 나오는 선은 더 밝게

                float3 bg = _BgColor.rgb * (0.85 + 0.15 * r);
                float3 lc = _LineColor.rgb * (0.45 + 0.4 * pulse + lead * 1.6);
                float lm = lineMask * lineA;
                float3 rgb = lerp(bg * fill, lc, lm);
                float alpha = max(fill, lm);
                return fixed4(rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
