// 퍼즈 발동 중 화면 가장자리에 "푸른 노이즈 + 번개"를 씌우는 전체 화면 셰이더.
// 피격용(HitGlitchScreen)보다 약한 줄 밀림·색 분리 + 푸른 물들임 + 지글거리는 알갱이 + 가장자리에서 뻗는 번개.
// HitGlitchScreenFeature(렌더러 기능)가 화면을 복사해서 이 셰이더로 다시 그린다.
// _Intensity / _Seed / _BoltSeed / _BoltIntensity 는 PauseGlitchEffect 스크립트가 매 프레임 넣어준다.
Shader "EventHorizon/PauseGlitchScreen"
{
    Properties
    {
        _Intensity ("세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Seed ("노이즈 씨앗 (스크립트가 조절)", Float) = 0
        _BoltSeed ("번개 씨앗 (스크립트가 조절)", Float) = 0
        _BoltIntensity ("번개 세기 (스크립트가 조절)", Range(0, 1)) = 0

        [Header(Edge)]
        _EdgeWidth ("효과가 들어오는 가장자리 폭", Range(0.02, 0.6)) = 0.2
        _EdgeNoise ("가장자리 경계 들쭉날쭉", Range(0, 0.3)) = 0.1

        [Header(Distortion)]
        _LineCount ("가는 줄 개수 (세로)", Range(20, 600)) = 260
        _Shift ("가는 줄 밀림 거리", Range(0, 0.2)) = 0.016
        _BandCount ("큰 찢김 띠 개수 (세로)", Range(2, 60)) = 18
        _BandAmount ("큰 찢김 띠 양", Range(0, 1)) = 0.3
        _BandShift ("큰 찢김 밀림 거리", Range(0, 0.3)) = 0.04
        _Split ("RGB 색 분리 거리", Range(0, 0.06)) = 0.01

        [Header(Blue Noise)]
        _TintColor ("물들일 색", Color) = (0.2, 0.55, 1, 1)
        _Tint ("푸른 물들임 정도", Range(0, 1)) = 0.45
        _Grain ("지글지글 알갱이 양", Range(0, 1)) = 0.35
        _GrainSize ("알갱이 크기 (픽셀)", Range(1, 8)) = 2
        _Scanline ("가로줄 진하기", Range(0, 1)) = 0.25

        [Header(Lightning)]
        _BoltCount ("번개 최대 개수", Range(0, 6)) = 5
        _BoltChance ("번개가 보일 확률", Range(0, 1)) = 0.55
        _BoltLength ("번개 길이", Range(0.05, 0.8)) = 0.3
        _BoltJag ("번개 지그재그 정도", Range(0, 0.6)) = 0.22
        _BoltWidth ("번개 심지 두께", Range(0.0005, 0.01)) = 0.0025
        _BoltGlowWidth ("번개 번짐 폭", Range(0.002, 0.08)) = 0.02
        _BoltCoreColor ("번개 심지 색", Color) = (0.85, 0.95, 1, 1)
        _BoltColor ("번개 번짐 색", Color) = (0.25, 0.6, 1, 1)
        _BoltGlow ("번개 번짐 밝기", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "PauseGlitchScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _Seed;
            float _BoltSeed;
            float _BoltIntensity;
            float _EdgeWidth;
            float _EdgeNoise;
            float _LineCount;
            float _Shift;
            float _BandCount;
            float _BandAmount;
            float _BandShift;
            float _Split;
            float4 _TintColor;
            float _Tint;
            float _Grain;
            float _GrainSize;
            float _Scanline;
            float _BoltCount;
            float _BoltChance;
            float _BoltLength;
            float _BoltJag;
            float _BoltWidth;
            float _BoltGlowWidth;
            float4 _BoltCoreColor;
            float4 _BoltColor;
            float _BoltGlow;

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 꺾은선 노이즈 (부드럽게 잇지 않고 직선으로 이어서 번개처럼 각지게)
            float LineNoise(float x)
            {
                float i = floor(x);
                return lerp(Hash11(i), Hash11(i + 1.0), frac(x)) - 0.5;
            }

            // 번개 경로의 옆으로 벗어난 정도 (t: 0=시작, 1=끝)
            float BoltOffset(float t, float s)
            {
                return LineNoise(t * 7.0 + s) * 1.0
                     + LineNoise(t * 17.0 + s * 1.37) * 0.45
                     + LineNoise(t * 41.0 + s * 2.11) * 0.2;
            }

            // 번개 한 줄기: a 에서 dir 방향으로 len 만큼. 심지(core)와 번짐(glow)을 더해 준다
            void Bolt(float2 p, float2 a, float2 dir, float len, float s, float bright, inout float core, inout float glow)
            {
                float2 nrm = float2(-dir.y, dir.x);
                float2 ap = p - a;
                float along = dot(ap, dir);
                float t = along / len;
                if (t < -0.05 || t > 1.05) return;

                float tc = saturate(t);
                float off = BoltOffset(tc, s) * _BoltJag * len * saturate(tc * 6.0);   // 시작점은 고정
                float d = abs(dot(ap, nrm) - off);

                float fade = 1.0 - smoothstep(0.65, 1.0, tc);       // 끝으로 갈수록 사라짐
                float w = _BoltWidth * lerp(1.0, 0.35, tc);          // 끝으로 갈수록 가늘어짐
                core += (1.0 - smoothstep(w * 0.5, w, d)) * fade * bright;
                glow += exp(-d / _BoltGlowWidth) * fade * bright;
            }

            // 갈래 시작점: 본 줄기의 t 위치
            float2 BoltPoint(float2 a, float2 dir, float len, float s, float t)
            {
                float2 nrm = float2(-dir.y, dir.x);
                float off = BoltOffset(t, s) * _BoltJag * len * saturate(t * 6.0);
                return a + dir * (t * len) + nrm * off;
            }

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Intensity <= 0.001 && _BoltIntensity <= 0.001) return src;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = floor(_Seed);

                // 1) 가장자리 마스크
                float lineId = floor(uv.y * _LineCount);
                float bandId = floor(uv.y * _BandCount);
                float lineRand = Hash11(lineId + seed * 7.13);
                float bandRand = Hash11(bandId * 3.7 + seed * 1.91);

                float dx = min(uv.x, 1.0 - uv.x) * aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float d = min(dx, dy) + (lineRand - 0.5) * _EdgeNoise + (bandRand - 0.5) * _EdgeNoise;
                float width = _EdgeWidth * (0.7 + 0.3 * _Intensity);
                float e = (1.0 - smoothstep(0.0, width, d)) * _Intensity;

                // 2) 가로 밀림 (피격보다 약하게)
                float jitter = (lineRand - 0.5) * 2.0 * _Shift;
                float bandOn = step(1.0 - _BandAmount, bandRand);
                float band = bandOn * (Hash11(bandId * 1.7 + seed) - 0.5) * 2.0 * _BandShift;
                float offset = (jitter + band) * e;

                // 3) RGB 분리 (파랑 쪽을 조금 더 벌림)
                float split = _Split * e * (0.6 + 0.8 * bandRand);
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset + split, 0)).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset, 0)).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset - split * 1.5, 0)).b;
                float3 col = float3(r, g, b);

                // 4) 푸른 물들임 + 가로줄
                float3 tinted = col * lerp(float3(1, 1, 1), _TintColor.rgb * 1.6, 0.8) + _TintColor.rgb * 0.05;
                col = lerp(col, tinted, saturate(e * _Tint));
                float scan = 1.0 - _Scanline * 0.5 * (1.0 + sin(uv.y * _ScreenParams.y * 1.1 + _Time.y * 40.0));
                col *= lerp(1.0, scan, saturate(e));

                // 5) 지글지글 푸른 알갱이 (가장자리에서만)
                float2 px = floor(uv * _ScreenParams.xy / _GrainSize);
                float grain = Hash21(px + seed * 13.1) - 0.5;
                col += grain * _Grain * e * _TintColor.rgb;

                // 6) 번개: 가장자리에서 안쪽으로 뻗는 줄기 + 갈래
                float core = 0.0;
                float glow = 0.0;
                if (_BoltIntensity > 0.001)
                {
                    float2 p = float2(uv.x * aspect, uv.y);
                    float bs = floor(_BoltSeed);
                    [loop]
                    for (int i = 0; i < 6; i++)
                    {
                        if (i >= (int)_BoltCount) break;
                        float k = i * 17.31 + bs * 3.17;
                        if (Hash11(k) > _BoltChance) continue;

                        // 어느 변에서 시작할지 (0 왼쪽, 1 오른쪽, 2 아래, 3 위) 와 변 위의 위치
                        float sideR = Hash11(k + 1.1);
                        float posR = Hash11(k + 2.3);
                        float ang = (Hash11(k + 3.7) - 0.5) * 1.2;
                        float2 a; float2 inward;
                        if (sideR < 0.3)      { a = float2(0.0, posR);           inward = float2(1, 0); }
                        else if (sideR < 0.6) { a = float2(aspect, posR);        inward = float2(-1, 0); }
                        else if (sideR < 0.8) { a = float2(posR * aspect, 0.0);  inward = float2(0, 1); }
                        else                  { a = float2(posR * aspect, 1.0);  inward = float2(0, -1); }
                        float ca = cos(ang), sa = sin(ang);
                        float2 dir = float2(inward.x * ca - inward.y * sa, inward.x * sa + inward.y * ca);

                        float len = _BoltLength * (0.5 + Hash11(k + 4.9));
                        float s = Hash11(k + 6.1) * 100.0;
                        float bright = 0.6 + 0.4 * Hash11(k + 7.7);

                        Bolt(p, a, dir, len, s, bright, core, glow);

                        // 갈래 하나
                        float ft = 0.3 + 0.35 * Hash11(k + 8.3);
                        float2 fa = BoltPoint(a, dir, len, s, ft);
                        float fang = ang + (Hash11(k + 9.1) > 0.5 ? 0.7 : -0.7);
                        float fc = cos(fang), fs = sin(fang);
                        float2 fdir = float2(inward.x * fc - inward.y * fs, inward.x * fs + inward.y * fc);
                        Bolt(p, fa, fdir, len * 0.45, s + 31.7, bright * 0.7, core, glow);
                    }
                }
                core = saturate(core) * _BoltIntensity;
                glow = saturate(glow) * _BoltIntensity;
                col += _BoltColor.rgb * glow * _BoltGlow;
                col = lerp(col, _BoltCoreColor.rgb * 1.5, core);

                return float4(col, src.a);
            }
            ENDHLSL
        }
    }
}
