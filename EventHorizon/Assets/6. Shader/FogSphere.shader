// [2026-11-24 추가] 3D 구에 씌우는 안개 셰이더 — FX_FogSphere_Left 프리팹(1.Prefab/Effect/VFX)이 씀.
//  - 구의 정면일수록 진하고 테두리로 갈수록 투명 → 경계선 없는 안개 공
//  - 3D 노이즈 3겹이 시간에 따라 월드 기준 _Flow 방향(기본 왼쪽)으로 흐름
//  - 빛을 받지 않음(색 그대로), 반투명, 깊이 기록 안 함
Shader "EventHorizon/FogSphere"
{
    Properties
    {
        [HDR] _Color ("안개 색 (알파 = 최대 진하기, HDR 가능)", Color) = (0.78, 0.78, 0.82, 0.6)
        _Density ("진하기 배율", Range(0, 3)) = 1
        _EdgeSoftness ("가장자리 부드러움 (클수록 가운데만 진함)", Range(0.3, 6)) = 1.6
        _NoiseScale ("무늬 크기 (클수록 잘게)", Range(0.2, 10)) = 1.8
        _NoiseContrast ("무늬 대비 (0 = 고르게, 1 = 덩어리 뚜렷)", Range(0, 1)) = 0.65
        _Flow ("흐름 방향·속도 (월드 기준, 초당)", Vector) = (-0.6, 0.05, 0, 0)
        _WaveMix ("물결 비율 (0 = 덩어리 안개, 1 = 물결)", Range(0, 1)) = 0
        _WaveFrequency ("물결 촘촘함", Range(0.2, 6)) = 1.4
        _WaveDistortion ("물결 일렁임 (클수록 굽이침)", Range(0, 3)) = 1.2
        _NoiseThreshold ("무늬 경계 위치 (클수록 덩어리가 작아짐)", Range(0, 1)) = 0.55
        _NoiseSoftness ("무늬 경계 부드러움 (작을수록 선명)", Range(0.01, 0.5)) = 0.3
        [HDR] _RimColor ("림라이트 색 (알파 = 림 진하기)", Color) = (2, 0.9, 0.4, 1)
        _RimIntensity ("림라이트 세기 (0 = 끔)", Range(0, 5)) = 0
        _RimPower ("림라이트 굵기 (클수록 가늘게)", Range(0.5, 10)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            // [2026-11-24] 씬 창(2D 렌더러)과 게임 카메라(3D 렌더러) 양쪽에서 모두 그려지도록 기본 그리기 방식 사용
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density, _EdgeSoftness, _NoiseScale, _NoiseContrast;
                float4 _Flow;
                half _NoiseThreshold, _NoiseSoftness;
                half _WaveMix, _WaveFrequency, _WaveDistortion;
                half4 _RimColor;
                half _RimIntensity, _RimPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float Hash (float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise (float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                                 lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                                 lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            float Fbm (float3 p)
            {
                float n = 0.5 * Noise(p) + 0.25 * Noise(p * 2.03 + 11.7) + 0.125 * Noise(p * 4.01 + 37.3);
                return n / 0.875;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(n, v));
                float body = pow(ndv, _EdgeSoftness);                            // 가운데 진하고 테두리 투명
                float rim = pow(1.0 - ndv, _RimPower) * _RimIntensity;            // [2026-11-24] 테두리에서만 빛나는 림라이트

                // 무늬가 _Flow 방향으로 움직이게: 샘플 위치를 반대로 민다
                float3 p = (i.positionWS - _Flow.xyz * _Time.y) * _NoiseScale;
                float fog = Fbm(p);
                // [2026-11-24] 물결: 흐르는 방향과 수직인 물결선이 노이즈로 휘며 흘러감
                float warp = Fbm(p * 0.7 + 5.3) * _WaveDistortion;
                const float TAU = 6.2831853;
                float w1 = sin((p.x + warp) * _WaveFrequency * TAU + sin(p.y * 1.5) * 0.8) * 0.5 + 0.5;
                float w2 = sin((p.x * 0.8 + p.y * 0.6 - warp * 0.7) * _WaveFrequency * 1.7 * TAU) * 0.5 + 0.5;
                fog = lerp(fog, w1 * 0.6 + w2 * 0.4, _WaveMix);
                // [2026-11-24] 경계 폭을 조절 가능하게 (예전: 0.25~0.85 고정 → 흐릿)
                fog = lerp(1.0, smoothstep(_NoiseThreshold - _NoiseSoftness, _NoiseThreshold + _NoiseSoftness, fog), _NoiseContrast);

                half baseA = saturate(_Color.a * _Density * body * fog);
                half rimA = saturate(rim * _RimColor.a);
                half alpha = saturate(baseA + rimA);
                half3 col = _Color.rgb * (0.85 + 0.3 * fog);
                // 림은 안개 위에 얹히는 빛: 알파 비율로 섞고 밝기를 더함
                col = (col * baseA + _RimColor.rgb * rimA) / max(alpha, 1e-4);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
