// 퍼즈 발동 중 화면을 푸르게 물들이고, 가장자리에 약한 노이즈 + 움직이는 번개를 씌우는 전체 화면 셰이더.
//  - 화면 전체: 푸른 물들임 + 가장자리 푸른 빛 번짐 (깜빡이지 않고 차분하게 유지)
//  - 가장자리(얇은 테두리만): 줄 밀림·색 분리·알갱이
//  - 퍼즈가 끝나는 순간: 화면 전체 섬광("팡!") + 화면 가운데서 퍼지는 충격파
//      맨 앞 번개 고리 → 그 뒤 디지털 파편 고리 → 맨 뒤 육각 격자 파동 (시차를 두고 퍼짐)
//  - 번개: 화면 바깥에서 안쪽으로 뻗어 나오고, 지그재그가 흐르며 떨리다가 사라짐 (칸마다 수명이 다름)
// HitGlitchScreenFeature(렌더러 기능)가 화면을 복사해서 이 셰이더로 다시 그린다.
// _Intensity / _ColorIntensity / _Seed / _BoltSeed / _BoltIntensity / _BoltTime / _Flash / _Shock / _ShockAmount 는
// PauseGlitchEffect 스크립트가 넣어준다.
Shader "EventHorizon/PauseGlitchScreen"
{
    Properties
    {
        _Intensity ("노이즈 세기 (스크립트가 조절)", Range(0, 1)) = 0
        _ColorIntensity ("푸른 색 세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Seed ("노이즈 씨앗 (스크립트가 조절)", Float) = 0
        _BoltSeed ("번개 씨앗 (스크립트가 조절)", Float) = 0
        _BoltIntensity ("번개 세기 (스크립트가 조절)", Range(0, 1)) = 0
        _BoltTime ("번개 시간 (스크립트가 조절)", Float) = 0
        _Flash ("끝 섬광 세기 (스크립트가 조절)", Range(0, 1)) = 0
        _Shock ("끝 충격파 진행 (스크립트가 조절)", Range(0, 1)) = 0
        _ShockAmount ("끝 충격파 세기 (스크립트가 조절)", Range(0, 1)) = 0

        [Header(Blue Screen)]
        _ScreenColor ("화면 물들일 색", Color) = (0.35, 0.6, 1, 1)
        _ScreenTint ("화면 전체 푸른 정도", Range(0, 1)) = 0.35
        _VignetteColor ("가장자리 빛 번짐 색", Color) = (0.15, 0.45, 1, 1)
        _Vignette ("가장자리 빛 번짐 밝기", Range(0, 1)) = 0.35
        _VignetteWidth ("가장자리 빛 번짐 폭", Range(0.05, 0.8)) = 0.35

        [Header(Edge Noise)]
        _EdgeWidth ("노이즈가 들어오는 가장자리 폭", Range(0.02, 0.6)) = 0.08
        _EdgeNoise ("가장자리 경계 들쭉날쭉", Range(0, 0.3)) = 0.04
        _LineCount ("가는 줄 개수 (세로)", Range(20, 600)) = 260
        _Shift ("가는 줄 밀림 거리", Range(0, 0.2)) = 0.03
        _BandCount ("큰 찢김 띠 개수 (세로)", Range(2, 60)) = 18
        _BandAmount ("큰 찢김 띠 양", Range(0, 1)) = 0.45
        _BandShift ("큰 찢김 밀림 거리", Range(0, 0.3)) = 0.07
        _Split ("RGB 색 분리 거리", Range(0, 0.06)) = 0.018
        _TintColor ("노이즈 부분 물들일 색", Color) = (0.2, 0.55, 1, 1)
        _Tint ("노이즈 부분 푸른 정도", Range(0, 1)) = 0.6
        _Grain ("지글지글 알갱이 양", Range(0, 1)) = 0.6
        _GrainSize ("알갱이 크기 (픽셀)", Range(1, 8)) = 2
        _Scanline ("가로줄 진하기", Range(0, 1)) = 0.35

        [Header(Lightning)]
        _BoltCount ("번개 칸 개수 (동시에 최대)", Range(0, 6)) = 5
        _BoltChance ("칸마다 번개가 나올 확률", Range(0, 1)) = 0.6
        _BoltLifeMin ("번개 수명 최소 (초)", Range(0.1, 2)) = 0.35
        _BoltLifeMax ("번개 수명 최대 (초)", Range(0.1, 2)) = 0.7
        _BoltGrow ("뻗어 나오는 데 걸리는 비율 (수명 대비)", Range(0.01, 1)) = 0.25
        _BoltFlow ("지그재그가 흐르는 빠르기", Range(0, 20)) = 4
        _BoltShake ("미세 떨림 정도", Range(0, 1)) = 0.35
        _BoltLength ("번개 길이", Range(0.05, 0.8)) = 0.32
        _BoltOutside ("화면 밖에서 시작하는 거리", Range(0, 0.2)) = 0.06
        _BoltJag ("번개 지그재그 정도", Range(0, 0.6)) = 0.22
        _BoltWidth ("번개 심지 두께", Range(0.0005, 0.01)) = 0.0025
        _BoltGlowWidth ("번개 번짐 폭", Range(0.002, 0.08)) = 0.02
        _BoltCoreColor ("번개 심지 색", Color) = (0.85, 0.95, 1, 1)
        _BoltColor ("번개 번짐 색", Color) = (0.25, 0.6, 1, 1)
        _BoltGlow ("번개 번짐 밝기", Range(0, 3)) = 1.2

        [Header(End Flash)]
        _FlashColor ("끝 섬광 색", Color) = (0.8, 0.92, 1, 1)
        _FlashAmount ("끝 섬광 밝기", Range(0, 1)) = 0.85
        _ShockColor ("충격파 고리 색", Color) = (0.5, 0.8, 1, 1)
        _ShockRadius ("충격파가 퍼지는 최대 반경", Range(0.2, 2)) = 1.1
        _ShockWidth ("충격파 고리 두께", Range(0.01, 0.3)) = 0.07
        _ShockDistort ("충격파가 화면을 미는 거리", Range(0, 0.15)) = 0.05
        _ShockSplit ("충격파 고리 색 갈라짐", Range(0, 0.05)) = 0.012
        _ShockGlow ("충격파 부드러운 빛 밝기", Range(0, 2)) = 0.2

        [Header(Shock Lightning Ring)]
        _RingLightning ("번개 고리 세기 (0 = 끔)", Range(0, 1)) = 1
        _RingJag ("번개 고리 지그재그 정도", Range(0, 0.15)) = 0.045
        _RingWidth ("번개 고리 심지 두께", Range(0.0005, 0.01)) = 0.003
        _RingGlowWidth ("번개 고리 번짐 폭", Range(0.002, 0.08)) = 0.02

        [Header(Shock Shards)]
        _RingShards ("파편 고리 세기 (0 = 끔)", Range(0, 1)) = 1
        _ShardCount ("파편 칸 개수 (한 바퀴)", Range(8, 96)) = 36
        _ShardLag ("파편이 번개보다 뒤처지는 거리", Range(0, 0.3)) = 0.06
        _ShardThickness ("파편 두께", Range(0.005, 0.08)) = 0.03
        _ShardShift ("파편 안 화면 밀림 거리", Range(0, 0.2)) = 0.08

        [Header(Shock Hex Grid)]
        _RingHex ("육각 격자 세기 (0 = 끔)", Range(0, 1)) = 1
        _HexScale ("육각 칸 크기 (클수록 촘촘)", Range(4, 60)) = 18
        _HexLag ("격자가 번개보다 뒤처지는 거리", Range(0, 0.4)) = 0.12
        _HexWidth ("격자가 보이는 띠 폭", Range(0.01, 0.3)) = 0.07
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
            float _ColorIntensity;
            float _Seed;
            float _BoltSeed;
            float _BoltIntensity;
            float _BoltTime;
            float _Flash;
            float _Shock;
            float _ShockAmount;

            float4 _ScreenColor;
            float _ScreenTint;
            float4 _VignetteColor;
            float _Vignette;
            float _VignetteWidth;

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
            float _BoltLifeMin;
            float _BoltLifeMax;
            float _BoltGrow;
            float _BoltFlow;
            float _BoltShake;
            float _BoltLength;
            float _BoltOutside;
            float _BoltJag;
            float _BoltWidth;
            float _BoltGlowWidth;
            float4 _BoltCoreColor;
            float4 _BoltColor;
            float _BoltGlow;

            float4 _FlashColor;
            float _FlashAmount;
            float4 _ShockColor;
            float _ShockRadius;
            float _ShockWidth;
            float _ShockDistort;
            float _ShockSplit;
            float _ShockGlow;

            float _RingLightning;
            float _RingJag;
            float _RingWidth;
            float _RingGlowWidth;
            float _RingShards;
            float _ShardCount;
            float _ShardLag;
            float _ShardThickness;
            float _ShardShift;
            float _RingHex;
            float _HexScale;
            float _HexLag;
            float _HexWidth;

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 꺾은선 노이즈 (직선으로 이어서 번개처럼 각지게)
            float LineNoise(float x)
            {
                float i = floor(x);
                return lerp(Hash11(i), Hash11(i + 1.0), frac(x)) - 0.5;
            }

            // 번개 경로가 옆으로 벗어난 정도. flow 로 지그재그가 줄기를 따라 흐르고, shake 로 매 프레임 잘게 떨림
            float BoltOffset(float t, float s, float flow, float shake)
            {
                return LineNoise(t * 7.0 + s + flow) * 1.0
                     + LineNoise(t * 17.0 + s * 1.37 + flow * 1.7) * 0.45
                     + LineNoise(t * 41.0 + s * 2.11 + shake) * 0.2 * (0.5 + _BoltShake);
            }

            // 번개 한 줄기: a 에서 dir 방향으로 len 만큼, 그중 grow(0~1) 까지만 보임
            void Bolt(float2 p, float2 a, float2 dir, float len, float grow, float s, float flow, float shake,
                      float bright, inout float core, inout float glow)
            {
                if (grow <= 0.001 || bright <= 0.001) return;
                float2 nrm = float2(-dir.y, dir.x);
                float2 ap = p - a;
                float t = dot(ap, dir) / len;
                if (t < -0.05 || t > grow + 0.05) return;

                float tc = clamp(t, 0.0, grow);
                float off = BoltOffset(tc, s, flow, shake) * _BoltJag * len;
                float d = abs(dot(ap, nrm) - off);

                // 시작·끝 모두 가늘고 흐리게 (잘린 단면이 안 보이도록)
                float startFade = smoothstep(0.0, 0.12, tc);
                float tipFade = 1.0 - smoothstep(grow - 0.25 * grow - 0.05, grow, tc);
                float fade = startFade * saturate(tipFade + 0.15);
                float w = _BoltWidth * lerp(0.4, 1.0, startFade) * lerp(1.0, 0.35, tc);

                core += (1.0 - smoothstep(w * 0.5, w, d)) * fade * bright;
                glow += exp(-d / _BoltGlowWidth) * fade * bright;
            }

            float2 BoltPoint(float2 a, float2 dir, float len, float s, float flow, float shake, float t)
            {
                float2 nrm = float2(-dir.y, dir.x);
                float off = BoltOffset(t, s, flow, shake) * _BoltJag * len;
                return a + dir * (t * len) + nrm * off;
            }

            // 한 바퀴(n 칸)마다 딱 맞게 반복되는 꺾은선 노이즈 → 고리에 이음매가 안 생김
            float LoopNoise(float a01, float n, float s)
            {
                float x = a01 * n;
                float i = floor(x);
                float i0 = fmod(i, n);
                float i1 = fmod(i + 1.0, n);
                return lerp(Hash11(i0 + s), Hash11(i1 + s), frac(x)) - 0.5;
            }

            // 육각 격자: p 가 속한 칸 안에서의 위치(돌려줌)와 칸 번호(id)
            float2 HexCell(float2 p, out float2 id)
            {
                const float2 k = float2(1.0, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / k.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * k, p - (hc.zw + 0.5) * k);
                bool useA = dot(h.xy, h.xy) < dot(h.zw, h.zw);
                id = useA ? hc.xy : hc.zw + 0.5;
                return useA ? h.xy : h.zw;
            }

            // 칸 가운데 0 → 육각 테두리 0.5
            float HexEdge(float2 q)
            {
                q = abs(q);
                return max(dot(q, float2(0.5, 0.8660254)), q.x);
            }

            float2 Rotate(float2 v, float ang)
            {
                float c = cos(ang), s = sin(ang);
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            float4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Intensity <= 0.001 && _BoltIntensity <= 0.001 && _ColorIntensity <= 0.001
                    && _Flash <= 0.001 && _ShockAmount <= 0.001) return src;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = floor(_Seed);

                // ── 0) 끝 충격파: 화면 가운데서 퍼지는 고리가 화면을 바깥으로 밀어냄 ──
                float shockRing = 0.0;
                float2 shockDir = float2(0, 0);
                float ringCore = 0.0;     // 번개 고리 심지
                float ringGlow = 0.0;     // 번개 고리 번짐
                float shardOn = 0.0;      // 파편 칸 안인지
                float shardFill = 0.0;    // 흰빛으로 채운 파편
                float shardEdge = 0.0;    // 파편 테두리 선
                float shardShift = 0.0;   // 파편 안 화면 옆으로 밀림
                float hexLight = 0.0;     // 육각 격자 빛
                if (_ShockAmount > 0.001)
                {
                    float2 c = uv - 0.5;
                    c.x *= aspect;
                    float dist = length(c);
                    float R = _Shock * _ShockRadius;
                    float x = (dist - R) / _ShockWidth;
                    shockRing = exp(-x * x) * _ShockAmount;
                    shockDir = dist > 0.0001 ? c / dist : float2(0, 0);
                    shockDir.x /= aspect;
                    uv -= shockDir * shockRing * _ShockDistort;

                    float a01 = atan2(c.y, c.x) / 6.2831853 + 0.5;   // 각도 0~1

                    // ① 맨 앞: 번개 고리 두 가닥 (퍼지는 동안 모양이 꿈틀거림)
                    if (_RingLightning > 0.001)
                    {
                        float wig = floor(_Shock * 30.0);
                        [unroll]
                        for (int k = 0; k < 2; k++)
                        {
                            float ph = k * 13.7;
                            float jag = LoopNoise(a01, 12.0, ph + wig * 0.11) * 1.0
                                      + LoopNoise(a01, 30.0, ph * 2.0 + wig * 0.23) * 0.5
                                      + LoopNoise(a01, 80.0, ph * 3.0 + wig * 0.71) * 0.25;
                            float rd = abs(dist - (R + jag * _RingJag));
                            float w = _RingWidth * (k == 0 ? 1.0 : 0.6);
                            ringCore += 1.0 - smoothstep(w * 0.5, w, rd);
                            ringGlow += exp(-rd / _RingGlowWidth) * (k == 0 ? 1.0 : 0.6);
                        }
                        ringCore = saturate(ringCore) * _RingLightning * _ShockAmount;
                        ringGlow = saturate(ringGlow) * _RingLightning * _ShockAmount;
                    }

                    // ② 그 뒤: 디지털 파편 고리 (칸마다 보임/두께/어긋남이 다름)
                    if (_RingShards > 0.001)
                    {
                        float n = floor(_ShardCount);
                        float seg = floor(a01 * n);
                        float flick = floor(_Shock * 8.0);   // 퍼지는 동안 몇 번 바뀜
                        float on = step(0.25, Hash11(seg * 3.1 + flick * 1.7 + 7.0));
                        float rr = R - _ShardLag + (Hash11(seg * 5.3 + 1.0) - 0.5) * 0.06;
                        float th = _ShardThickness * (0.35 + 0.65 * Hash11(seg * 9.7 + 2.0));
                        float inside = step(abs(dist - rr), th) * on;
                        float sk = _RingShards * _ShockAmount;
                        shardOn = inside * sk;
                        shardShift = (Hash11(seg * 2.2 + flick + 4.0) - 0.5) * _ShardShift * shardOn;
                        shardFill = step(0.6, Hash11(seg * 4.4 + floor(dist * 60.0))) * shardOn;
                        shardEdge = (1.0 - smoothstep(0.0015, 0.003, abs(abs(dist - rr) - th))) * on * sk;
                    }

                    // ③ 맨 뒤: 육각 격자 파동 (지나가는 띠 안에서만 보임)
                    if (_RingHex > 0.001)
                    {
                        float2 id;
                        float2 q = HexCell(c * _HexScale, id);
                        float hd = HexEdge(q);
                        float hx = (dist - (R - _HexLag)) / _HexWidth;
                        float hb = exp(-hx * hx) * _RingHex * _ShockAmount;
                        float lines = smoothstep(0.43, 0.48, hd);
                        float fill = step(0.75, Hash21(id + floor(_Shock * 20.0))) * 0.5;
                        hexLight = hb * (lines * 1.2 + fill);
                    }
                }

                // 가장자리까지 거리 (화면 비율 보정)
                float dx = min(uv.x, 1.0 - uv.x) * aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float edgeDist = min(dx, dy);

                // ── 1) 가장자리 노이즈 (약하게) ──
                float lineId = floor(uv.y * _LineCount);
                float bandId = floor(uv.y * _BandCount);
                float lineRand = Hash11(lineId + seed * 7.13);
                float bandRand = Hash11(bandId * 3.7 + seed * 1.91);
                float d = edgeDist + (lineRand - 0.5) * _EdgeNoise + (bandRand - 0.5) * _EdgeNoise;
                float e = (1.0 - smoothstep(0.0, _EdgeWidth * (0.7 + 0.3 * _Intensity), d)) * _Intensity;

                float jitter = (lineRand - 0.5) * 2.0 * _Shift;
                float band = step(1.0 - _BandAmount, bandRand) * (Hash11(bandId * 1.7 + seed) - 0.5) * 2.0 * _BandShift;
                float offset = (jitter + band) * e + shardShift;
                float split = _Split * e * (0.6 + 0.8 * bandRand) + 0.01 * shardOn;
                float2 ss = shockDir * shockRing * _ShockSplit;   // 충격파 고리에서 색이 갈라짐
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset + split, 0) + ss).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset, 0)).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset - split * 1.5, 0) - ss).b;
                float3 col = float3(r, g, b);

                // ── 2) 화면 전체 푸른 물들임 (밝기는 살리고 색만 푸르게) ──
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                float3 blue = luma * _ScreenColor.rgb * 1.5 + _ScreenColor.rgb * 0.03;
                col = lerp(col, blue, saturate(_ScreenTint * _ColorIntensity));

                // 가장자리 푸른 빛 번짐
                float vig = 1.0 - smoothstep(0.0, _VignetteWidth, edgeDist);
                col += _VignetteColor.rgb * vig * vig * _Vignette * _ColorIntensity;

                // ── 3) 노이즈 부분 물들임 + 가로줄 + 알갱이 ──
                float3 tinted = col * lerp(float3(1, 1, 1), _TintColor.rgb * 1.6, 0.8) + _TintColor.rgb * 0.04;
                col = lerp(col, tinted, saturate(e * _Tint));
                float scan = 1.0 - _Scanline * 0.5 * (1.0 + sin(uv.y * _ScreenParams.y * 1.1 + _BoltTime * 40.0));
                col *= lerp(1.0, scan, saturate(e));
                float2 px = floor(uv * _ScreenParams.xy / _GrainSize);
                col += (Hash21(px + seed * 13.1) - 0.5) * _Grain * e * _TintColor.rgb;

                // ── 4) 번개 ──
                float core = 0.0;
                float glow = 0.0;
                if (_BoltIntensity > 0.001)
                {
                    float2 p = float2(uv.x * aspect, uv.y);
                    float bs = floor(_BoltSeed);
                    float shake = floor(_BoltTime * 24.0) * 0.37;   // 초당 24번 잘게 떨림

                    [loop]
                    for (int i = 0; i < 6; i++)
                    {
                        if (i >= (int)_BoltCount) break;

                        // 칸마다 수명이 달라서 번개가 동시에 바뀌지 않고 제각각 생겼다 사라짐
                        float life = lerp(_BoltLifeMin, _BoltLifeMax, Hash11(i * 7.3 + bs));
                        float tt = _BoltTime / life + Hash11(i * 3.1 + bs);
                        float cycle = floor(tt);
                        float age = frac(tt);   // 0 → 1

                        float k = i * 17.31 + cycle * 3.17 + bs * 1.3;
                        if (Hash11(k) > _BoltChance) continue;

                        // 어느 변에서 들어올지 (0 왼쪽, 1 오른쪽, 2 아래, 3 위) 와 변 위의 위치
                        float sideR = Hash11(k + 1.1);
                        float posR = Hash11(k + 2.3);
                        float outDist = _BoltOutside * (0.4 + 0.6 * Hash11(k + 5.3));
                        float2 a; float2 inward;
                        if (sideR < 0.3)      { a = float2(-outDist, posR);              inward = float2(1, 0); }
                        else if (sideR < 0.6) { a = float2(aspect + outDist, posR);      inward = float2(-1, 0); }
                        else if (sideR < 0.8) { a = float2(posR * aspect, -outDist);     inward = float2(0, 1); }
                        else                  { a = float2(posR * aspect, 1.0 + outDist); inward = float2(0, -1); }
                        float ang = (Hash11(k + 3.7) - 0.5) * 1.2;
                        float2 dir = Rotate(inward, ang);

                        float len = _BoltLength * (0.5 + Hash11(k + 4.9)) + outDist;
                        float s = Hash11(k + 6.1) * 100.0;
                        float flow = _BoltTime * _BoltFlow * (Hash11(k + 8.8) > 0.5 ? 1.0 : -1.0);

                        // 뻗어 나옴 → 유지 → 흐려짐 + 프레임마다 밝기 흔들림
                        float grow = saturate(age / _BoltGrow);
                        float lifeFade = 1.0 - smoothstep(0.55, 1.0, age);
                        float flick = 0.65 + 0.35 * Hash11(floor(_BoltTime * 30.0) + k);
                        float bright = (0.6 + 0.4 * Hash11(k + 7.7)) * lifeFade * flick;

                        Bolt(p, a, dir, len, grow, s, flow, shake, bright, core, glow);

                        // 갈래 하나: 본 줄기가 그 지점까지 뻗은 뒤에 따라 뻗어 나옴
                        float ft = 0.3 + 0.35 * Hash11(k + 8.3);
                        float fgrow = saturate((grow - ft) / (1.0 - ft));
                        if (fgrow > 0.0)
                        {
                            float2 fa = BoltPoint(a, dir, len, s, flow, shake, ft);
                            float2 fdir = Rotate(inward, ang + (Hash11(k + 9.1) > 0.5 ? 0.7 : -0.7));
                            Bolt(p, fa, fdir, len * 0.45, fgrow, s + 31.7, flow, shake, bright * 0.7, core, glow);
                        }
                    }
                }
                core = saturate(core) * _BoltIntensity;
                glow = saturate(glow) * _BoltIntensity;
                col += _BoltColor.rgb * glow * _BoltGlow;
                col = lerp(col, _BoltCoreColor.rgb * 1.5, core);

                // ── 5) 끝 연출: 충격파 (육각 격자 → 파편 → 번개 고리) + 화면 전체 섬광 ──
                col += _ShockColor.rgb * shockRing * _ShockGlow;
                col += _ShockColor.rgb * hexLight;
                col = lerp(col, col * float3(0.6, 0.9, 1.4), shardOn * (1.0 - step(0.5, shardFill)));
                col = lerp(col, _BoltCoreColor.rgb, shardFill);
                col += _ShockColor.rgb * shardEdge;
                col += _BoltColor.rgb * ringGlow * 1.2;
                col = lerp(col, _BoltCoreColor.rgb * 1.4, ringCore);
                float flash = saturate(_Flash * _FlashAmount);
                col += _FlashColor.rgb * flash * 0.6;
                col = lerp(col, _FlashColor.rgb, flash * 0.65);

                return float4(col, src.a);
            }
            ENDHLSL
        }
    }
}
