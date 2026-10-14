// 피격 시 화면 가장자리에 붉은 글리치를 그리는 전체 화면 셰이더 (UI 오버레이용).
// 화면 전체를 덮는 UI Image 에 이 셰이더 머티리얼을 넣고, 스크립트(HitGlitchEffect)가 _Intensity 를 0~1 로 조절한다.
// 장면 그림을 읽지 않고 가장자리 무늬만 직접 그리므로 카메라/렌더러 설정과 상관없이 모든 화면 위에 덮인다.
Shader "EventHorizon/UI/HitGlitchEdge"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("붉은 색", Color) = (1, 0.08, 0.12, 1)
        _GhostColor ("색 번짐(반대편) 색", Color) = (0, 0.9, 1, 1)
        _Intensity ("세기 (스크립트가 조절)", Range(0, 1)) = 0
        _EdgeWidth ("가장자리 두께", Range(0.02, 0.6)) = 0.12
        _EdgeNoise ("가장자리 울퉁불퉁함", Range(0, 0.2)) = 0.06
        _BlockCount ("글리치 줄 개수 (세로)", Range(4, 120)) = 46
        _BlockStretch ("글리치 조각 가로 길이 (배)", Range(1, 20)) = 7
        _BlockAmount ("글리치 조각 양", Range(0, 1)) = 0.45
        _WhiteColor ("하얀 글리치 색", Color) = (1, 1, 1, 1)
        _WhiteAmount ("하얀 글리치 양", Range(0, 1)) = 0.3
        _TearAmount ("가로 찢김 양", Range(0, 1)) = 0.5
        _TearShift ("가로 찢김 거리", Range(0, 0.2)) = 0.06
        _Split ("색 번짐 거리", Range(0, 0.05)) = 0.012
        _Scanline ("가로줄 진하기", Range(0, 1)) = 0.35
        _Seed ("무작위 씨앗 (스크립트가 조절)", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _GhostColor;
            float _Intensity;
            float _EdgeWidth;
            float _EdgeNoise;
            float _BlockCount;
            float _BlockAmount;
            float _BlockStretch;
            fixed4 _WhiteColor;
            float _WhiteAmount;
            float _TearAmount;
            float _TearShift;
            float _Split;
            float _Scanline;
            float _Seed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 0~1 무작위 값 (같은 입력이면 같은 값)
            float hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 가장자리까지 거리 (화면 비율 보정) → 가장자리일수록 1
            float EdgeMask(float2 uv, float aspect, float noise, float width)
            {
                float dx = min(uv.x, 1.0 - uv.x) * aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float d = min(dx, dy) + noise;
                return 1.0 - smoothstep(0.0, width, d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Intensity <= 0.001) return fixed4(0, 0, 0, 0);

                float2 uv = i.uv;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = floor(_Seed);

                // 1) 가로 찢김: 몇몇 가로 줄을 통째로 옆으로 밀어냄
                float row = floor(uv.y * _BlockCount);
                float tearOn = step(1.0 - _TearAmount * 0.5, hash11(row + seed * 13.0));
                float tear = tearOn * (hash11(row * 1.7 + seed) - 0.5) * 2.0 * _TearShift * _Intensity;
                float2 guv = uv + float2(tear, 0);

                // 2) 가로로 긴 직사각형 조각: 줄마다 조각 길이와 시작 위치를 다르게 해서 바둑판처럼 줄이 맞지 않게
                float rowRand = hash11(row * 3.7 + seed * 0.37);
                float xCount = max(1.0, _BlockCount * aspect / _BlockStretch * (0.5 + rowRand));
                float xIndex = floor(guv.x * xCount + rowRand * 7.0);
                float2 cell = float2(xIndex, row);
                float cellRand = hash21(cell + seed);
                float noise = (cellRand - 0.5) * _EdgeNoise;
                float width = _EdgeWidth * (0.6 + 0.4 * _Intensity);

                // 3) 색 번짐: 붉은 층 바깥쪽으로 반대색 층이 살짝 삐져나옴 (좌우 양쪽)
                float mRed   = EdgeMask(guv, aspect, noise, width);
                float mWide  = max(EdgeMask(guv + float2(_Split, 0), aspect, noise, width),
                                   EdgeMask(guv - float2(_Split, 0), aspect, noise, width));
                float mGhost = mWide;

                // 4) 글리치 칸: 가장자리 바로 안쪽에서만 사각 조각이 깜빡임
                float nearEdge = smoothstep(0.25, 0.75, EdgeMask(guv, aspect, 0, width * 1.6));
                float block = step(1.0 - _BlockAmount * 0.35, cellRand) * nearEdge;

                // 5) 가로줄 (오래된 모니터 느낌)
                float scan = 1.0 - _Scanline * 0.5 * (1.0 + sin(uv.y * _ScreenParams.y * 1.2 + _Time.y * 60.0));

                // 6) 하얀 글리치: 가끔 가늘고 긴 하얀 줄이 번쩍 (줄 높이의 일부만 칠해서 더 가늘게)
                float rowFrac = frac(guv.y * _BlockCount);
                float thin = step(rowFrac, 0.45);
                float whiteNear = smoothstep(0.1, 0.6, EdgeMask(guv, aspect, 0, width * 2.0));
                float white = step(1.0 - _WhiteAmount * 0.18, hash21(cell * 1.31 + seed + 17.0)) * thin * whiteNear;

                float red = saturate(max(mRed, block)) * scan * (1.0 - white);
                float ghost = saturate(mGhost - mRed) * 0.6 * (1.0 - white);

                float total = red + ghost + white;
                fixed3 col = (_Color.rgb * red + _GhostColor.rgb * ghost + _WhiteColor.rgb * white) / max(total, 0.0001);
                float a = saturate(red * _Color.a + ghost * _GhostColor.a + white * _WhiteColor.a * 0.9) * _Intensity * i.color.a;
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
