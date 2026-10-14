# Event Horizon SampleScene 라이팅 이전 가이드 (Claude 작업용)

이 폴더는 Unity 프로젝트 **Event Horizon**의 `SampleScene`의 라이팅 환경을 **다른 Unity 프로젝트의 씬**에 똑같이 옮기기 위한 묶음입니다. 라이팅 환경은 안개, 환경광, 반사, 스카이박스, 디렉셔널 라이트, 후처리 볼륨을 말합니다.
모든 수치는 원본 `SampleScene.unity`와 에셋 파일에서 **반올림 없이 그대로** 옮겼습니다.
사용자는 Unity 초보이고 한국어만 씁니다. 결과 보고도 한국어로 해 주세요.

---

## 0. 폴더 구성

| 경로 | 내용 | 대상 프로젝트에 복사 |
|---|---|---|
| `복사할_파일/Assets/EventHorizonLighting/Editor/EventHorizonLightingApplier.cs` | 메뉴 한 번으로 모든 값을 적용하는 에디터 스크립트 | **필수** |
| `복사할_파일/Assets/EventHorizonLighting/Skybox/UltraSkyboxFog.shader` (+.meta) | 스카이박스 셰이더. 셰이더 이름은 `Galactic Studios/Ultra Skybox Fog` | 조건부 (1-2 참고) |
| `복사할_파일/Assets/EventHorizonLighting/Skybox/rustig_koppie_4k.hdr` (+.meta) | 스카이박스 큐브맵. 텍스처 형태는 Cube | **필수** |
| `선택_HP연동_스크립트/*.cs` | 원본에서 플레이 중 HP에 따라 라이트·안개·하늘 색을 바꾸는 스크립트 | 선택 (3장 참고) |
| `lighting_spec.json` | 모든 원본 수치를 담은 기계용 명세 | 복사하지 않음 (참고용) |

`.meta` 파일의 고유번호(GUID)는 새로 발급한 것이라 대상 프로젝트의 기존 에셋과 겹치지 않습니다.

---

## 1. 적용 순서

### 1-1. 사전 확인
1. 대상 프로젝트가 **URP(Universal Render Pipeline)** 인지 확인합니다. `Packages/manifest.json`에 `com.unity.render-pipelines.universal`이 있어야 합니다. 없으면 볼륨, 후처리, 카메라 부분이 컴파일되지 않으니 사용자에게 먼저 알리세요.
2. 원본은 Unity 2022.3 / URP 14입니다. 스크립트는 Unity 6 / URP 17에서도 컴파일되도록 작성했습니다. 사용한 API는 `FindObjectsByType`, `SetLocalPositionAndRotation`, `LightingSettings`, `VolumeProfile.Add<T>`입니다.
3. 대상 프로젝트에 이미 같은 이름의 셰이더가 있는지 검색합니다. 검색 방법은 `Shader "Galactic Studios/Ultra Skybox Fog"` 문자열을 담은 `.shader` 파일을 찾는 것입니다.
   - **있으면**: `Skybox/UltraSkyboxFog.shader`와 그 `.meta`는 복사하지 **않습니다.** 셰이더 이름이 중복되기 때문입니다. `.hdr`과 그 `.meta`는 복사합니다.
   - **없으면**: Skybox 폴더 전체를 복사합니다.

### 1-2. 파일 복사
- `복사할_파일/Assets/EventHorizonLighting/` 폴더를 대상 프로젝트의 `Assets/` 바로 밑에 그대로 복사합니다. 결과 경로는 `Assets/EventHorizonLighting/...`입니다.
- `.meta` 파일도 반드시 함께 복사합니다. `.hdr.meta`에 큐브맵 설정이 들어 있습니다.
- Unity가 컴파일과 임포트를 마칠 때까지 기다립니다. 콘솔에 컴파일 에러가 없어야 합니다.
- `rustig_koppie_4k.hdr`의 인스펙터에서 **Texture Shape가 Cube**인지 확인합니다. 아니면 Cube로 바꾸고 Apply를 누릅니다.

### 1-3. 적용
1. 라이팅을 넣을 대상 씬을 엽니다.
2. 메뉴 **Tools > EventHorizon 라이팅** 에서 아래 둘 중 하나를 실행합니다.
   - **1. 에디터 값 그대로 적용**: 원본 씬 인스펙터에 저장된 값 그대로 넣습니다. 3장의 HP 연동 스크립트도 같이 옮길 때 이것을 씁니다.
   - **2. 플레이 화면 색(HP 100%)으로 적용**: 원본 게임을 실행했을 때 실제로 보이는 색으로 넣습니다. **HP 연동 스크립트를 옮기지 않으면 이쪽이 원본 게임 화면과 같습니다.** 사용자가 고르지 않았다면 이것을 기본으로 쓰세요.
3. 콘솔에 `[EventHorizon 라이팅]` 보고가 찍힙니다. `!!`로 시작하는 줄이 있으면 그 문제를 해결하고 다시 실행합니다. 다시 실행해도 안전합니다. 같은 이름의 오브젝트와 에셋을 갱신할 뿐 중복 생성하지 않습니다.
4. **씬을 저장합니다 (Ctrl+S).**
5. (선택) **3. 카메라 값 적용**은 사용자가 원할 때만 실행합니다. 4장을 참고하세요.

### 1-4. 스크립트가 자동으로 하는 일
- Lighting 창 Environment 값(안개, 환경광, 반사, 스카이박스, 기타)을 원본값으로 설정합니다.
- `Assets/EventHorizonLighting/EH_LightingSettings.lighting`을 만들어 씬에 연결합니다. 값은 Baked GI 끔, Realtime GI 끔, Auto Generate 끔입니다.
- `Assets/EventHorizonLighting/EH_Skybox_nomer.mat` 스카이박스 머티리얼을 만듭니다.
- 씬 루트에 `Light` 오브젝트를 만들고 그 아래에 다음 4개를 둡니다.
  - 디렉셔널 라이트 2개: `Naturallight`, `Reflectedlight`
  - 볼륨 2개: `Global Volume`, `LowHealthVolume`
- 볼륨 프로필 2개를 `Assets/EventHorizonLighting/Profiles/`에 만듭니다.
- **씬에 원래 있던 다른 디렉셔널 라이트와 다른 Global Volume은 끕니다.** 원본 씬에는 이 둘만 있기 때문입니다. Ctrl+Z로 되돌릴 수 있고, 무엇을 껐는지 콘솔에 보고됩니다.
- 태그가 `Player`인 오브젝트가 있으면 그 아래에 플레이어용 포인트 라이트 2개를 만듭니다.

---

## 2. 원본 수치 전체 (검증용)

색은 Unity 인스펙터 색상 선택기의 0~1 값입니다. HEX는 그 값에 255를 곱해 반올림한 것입니다.

### 2-1. Lighting 창 > Environment
| 항목 | 값 |
|---|---|
| Skybox Material | Ultra Skybox Fog 머티리얼 (2-3) |
| Sun Source | 없음 (None) |
| Environment Lighting Source | **Color** (Flat) |
| Ambient Color | (0.212, 0.227, 0.259, 1) `#363A42` |
| (참고) Equator / Ground | (0.114, 0.125, 0.133) / (0.047, 0.043, 0.035) — Color 모드에선 안 쓰임 |
| Intensity Multiplier | 1 |
| Environment Reflections Source | Skybox, Resolution 128, Compression 기본 |
| Reflection Intensity Multiplier | 1 |
| Reflection Bounces | 1 |
| Realtime Shadow Color | (0.42, 0.478, 0.627, 1) |
| **Fog** | **켬** |
| Fog Mode | **Linear** |
| Fog Color — 에디터 값 | (1, 0.8311249, 0.6839622, 1) `#FFD4AE` |
| Fog Color — 플레이 화면 (HP 100%) | (0.7647059, 0.72156864, 0.67058825, 0) `#C3B8AB` |
| Fog Start | **57** |
| Fog End | **193** |
| Fog Density | 0.01 (Linear에선 안 쓰임) |
| Halo Strength / Flare Strength / Flare Fade Speed | 0.5 / 1 / 3 |

### 2-2. Lighting 창 > Scene (라이트맵)
| 항목 | 값 |
|---|---|
| Realtime Global Illumination | 끔 |
| Baked Global Illumination | 끔 |
| Auto Generate | 끔 |
| 구워진 라이팅 데이터 | 없음 |

### 2-3. 스카이박스 머티리얼 (셰이더 `Galactic Studios/Ultra Skybox Fog`)
| 프로퍼티 | 값 |
|---|---|
| Tint Colour `_Tint` — 에디터 값 | (0.5, 0.5, 0.5, 0.5) |
| Tint Colour `_Tint` — 플레이 화면 (HP 100%) | (0.5019608, 0.49803922, 0.49019608, 1) |
| Exposure `_Exposure` | 1 |
| Rotation `_Rotation` | 90 |
| Cubemap `_Tex` | rustig_koppie_4k.hdr |
| Fog Colour `_FogCol` | (0.7169812, 0.6327133, 0.58508366, 0.5) |
| Fog Begin `_FogStart` | 0.15 |
| Fog End `_FogEnd` | 0.04 |
| Fog Density `_FogIntens` | 0.62 |
| Mie Intensity `_MieIntens` | 2 |
| Mie Tint `_MieTint` | (0.8207547, 0.77800876, 0.7549395, 0.5) |
| Mie Size `_MieSize` | 1 |
| Sun Direction `_SunDir` | 0 |
| Apply Fog To Bottom `_FogBottom` | 켬 (1) |
| Separate Top Fog `_TopFog` | 켬 (1) |
| Top Fog Colour `_TopFogCol` | (0.8113208, 0.8054348, 0.77687794, 0.5) |
| Top Fog Begin `_TopFogStart` | 0.62 |
| Top Fog End `_TopFogEnd` | 0.48 |
| Top Fog Density `_TopFogIntens` | 1 |

### 2-4. 라이트 (루트 `Light` 위치 (4.1873364, -2.369138, 0), 회전 0, 크기 1)
| 항목 | Naturallight | Reflectedlight |
|---|---|---|
| 부모 / 레이어 | Light / Default | Light / Default |
| Local Position | (-4.1873364, 2.369138, 0) | (-4.1873364, 2.369138, 0) |
| Local Rotation (쿼터니언 x,y,z,w) — **이 값이 기준** | (0.40821794, -0.23456973, 0.10938166, 0.8754261) | (-0.5954358, 0.047071338, -0.37489584, 0.709009) |
| (참고) 오일러각 X,Y,Z | (50, -30, 0) | (-54.003, 60.829, -89.043) |
| Type / Mode | Directional / Realtime | Directional / Realtime |
| Color — 에디터 값 | (0.8867924, 0.7862694, 0.6734603) `#E2C8AC` | (0.2551175, 0.39321536, 0.46226418) `#416476` |
| Color — 플레이 화면 (HP 100%) | (0.90588236, 0.7529412, 0.5803922) `#E7C094` | (0.25490198, 0.39215687, 0.4627451) `#416476` |
| Use Color Temperature | 끔 (온도값 6570) | 끔 (6570) |
| Intensity | **2** | **5** |
| Indirect Multiplier | 1 | 1 |
| Shadow Type | No Shadows | No Shadows |
| Render Mode / Culling Mask | Auto / Everything | Auto / Everything |
| URP 추가 설정 | 파이프라인 설정 사용, Rendering Layers 1 | 동일 |

※ 메인 라이트(Sun)는 지정하지 않았습니다. URP는 가장 밝은 디렉셔널 라이트인 Reflectedlight(세기 5)를 메인 라이트로 씁니다.

**플레이어 포인트 라이트** (원본 부모 `PlayerA`, 스케일 1.2, 레이어 Player)
| 항목 | Point Light | Point Light (1) |
|---|---|---|
| Local Position | (0, 1.473, -1.861) | (-0.58, 0.55, 0.001) |
| Color | (1, 1, 1) 흰색 | (0, 0.76471734, 1) `#00C3FF` |
| Intensity / Range | 7 / 10 | 7 / 10 |
| Culling Mask | **Player 레이어만** | Everything |
| Shadow / Mode | 없음 / Realtime | 없음 / Realtime |

### 2-5. 볼륨 (후처리)
| 항목 | Global Volume | LowHealthVolume |
|---|---|---|
| 부모 / 레이어 | Light / Default | Light / Default |
| Local Position | (-4.1873364, 2.369138, 0) | (-6.3183866, -4.014963, 12.695545) |
| Mode / Priority / Weight / Blend Distance | Global / **1** / **0.052** / 0 | Global / 2 / **0** / 0 |
| 프로필 내용 | **Bloom**: Threshold 100, Intensity 48.48, Scatter 0.133, Tint (1,1,1,1). 나머지 항목은 체크 꺼짐 | **Vignette**: Color (0.16153437, 0.20391288, 0.20754719), Intensity 0, Smoothness 0.2. **Color Adjustments**: Color Filter (0.14150941, 0.11414203, 0.11414203) |

※ LowHealthVolume은 원본에서도 weight를 바꾸는 스크립트가 붙어 있지 않아 항상 0입니다. 즉 화면에 영향이 없습니다. 원본과 같게 두기 위해 그대로 만듭니다.

### 2-6. URP 설정 (자동 적용 안 함 — 대상 프로젝트의 URP 에셋에서 확인만)
원본 `Assets/Settings/UniversalRP.asset`의 값입니다.
| 항목 | 값 |
|---|---|
| HDR | 켬 |
| MSAA | 끔 |
| Render Scale | 1 |
| Main Light | Per Pixel, 그림자 지원 켬, 해상도 2048 |
| Additional Lights | Per Pixel, 오브젝트당 4개, 그림자 지원 켬, 해상도 2048 |
| Shadow Distance / Cascade | 50 / 1개 |
| Depth Bias / Normal Bias / Soft Shadows | 1 / 1 / 끔 |
| Color Grading Mode / LUT | High Dynamic Range / 32 |
| SRP Batcher | 켬 |
| 렌더러 | 카메라가 쓰는 것은 Universal Renderer(Forward, 렌더러 기능 없음) |

대상 프로젝트 값이 다르면 **사용자에게 알리고 허락을 받은 뒤** 바꾸세요. URP 에셋은 프로젝트 전체에 영향을 줍니다. 특히 HDR이 꺼져 있으면 Bloom(Threshold 100)의 모습이 달라집니다.

---

## 3. (선택) HP 연동 스크립트

원본에서는 아래 스크립트가 **플레이 중** 라이트, 안개, 하늘 색을 HP에 따라 덮어씁니다. 그래서 에디터 값과 플레이 화면 값이 다릅니다.
- `LightHpColorizer.cs`: `HpColorStop` 구조체도 이 파일에 들어 있습니다. Naturallight와 Reflectedlight에 하나씩 붙어 있습니다.
- `FogHpColorizer.cs`: Naturallight에 붙어 있습니다.
- `SkyboxHpColorizer.cs`: Naturallight에 붙어 있습니다. 스카이박스 `_Tint`를 바꿉니다.

이 스크립트들을 쓰려면 대상 프로젝트에 다음 형태의 **`Stats` 클래스가 있어야 합니다**.
```csharp
public int maxHp; public int currentHp;
public event System.Action<int, int> OnHpChanged; // (현재HP, 최대HP)
```
없으면 컴파일 에러가 나므로 옮기지 말고 1-3의 **2번 메뉴(플레이 화면 색)** 를 쓰세요.
옮길 경우의 인스펙터 값은 다음과 같습니다. 구간은 (HP 비율, 색 RGBA)입니다. `targetStats`에는 플레이어의 Stats를 연결합니다.

| 붙일 곳 | 스크립트 | 구간 | 기타 |
|---|---|---|---|
| Reflectedlight | LightHpColorizer | (0.15, 0.4433962, 0.054155696, 0.03137237, 1) / (0.3, 0.43529412, 0.18039216, 0.26666668, 1) / (1, 0.25490198, 0.39215687, 0.4627451, 1) | Min Intensity Ratio 1, Lerp Speed 3 |
| Naturallight | LightHpColorizer | (0.15, 0.28627452, 0.3882353, 0.3254902, 1) / (0.3, 0.23313455, 0.26840487, 0.2924528, 1) / (1, 0.90588236, 0.7529412, 0.5803922, 1) | Min Intensity Ratio 0.1, Lerp Speed 0.5 |
| Naturallight | FogHpColorizer | (0.25, 0.14509805, 0.19215687, 0.16470589, 0) / (0.4, 0.13363296, 0.17278169, 0.1981132, 0.6156863) / (1, 0.7647059, 0.72156864, 0.67058825, 0) | Lerp Speed 0.2 |
| Naturallight | SkyboxHpColorizer | (0.2, 0.11764706, 0.1254902, 0.1254902, 0) / (0.4, 0.12156863, 0.28235295, 0.3882353, 0) / (1, 0.5019608, 0.49803922, 0.49019608, 1) | Lerp Speed 0.2 |

---

## 4. 카메라 (원본 값 — 메뉴 3은 표시한 항목만 바꿈)

| 항목 | Main Camera_3d (태그 MainCamera) | Camera_2D (Main Camera_3d의 자식) |
|---|---|---|
| 위치 | 부모 `CAMERA`(-1.1, 4.01, 0) 아래 Local (0, 0, -10) | Local (0, 0, 0) |
| Projection | Perspective, **FOV 25** ✱ | Orthographic, Size 5 |
| Clip Planes | 0.3 / 1000 ✱ | 0.3 / 1000 |
| Clear Flags / Background | **Skybox** ✱ / (0.19215687, 0.3019608, 0.4745098, 0) ✱ | Don't Clear / (0.15659486, 0.5259314, 0.6509434, 0) — 안 쓰임 |
| Depth | -1 | 0 |
| HDR / MSAA | 켬 / 끔 ✱ | 켬 / 끔 |
| Culling Mask | UI, 원경(10), 중경(11), 근경(13), 14번, Room3D(15) | Default, Player, Enemy, Item, Pickup, dropping, HitEffect, Box_sp, Box, Trap, Player_hitbox, Enemy_hitbox, platform, ground |
| URP Render Type | Base | Base |
| Post Processing | **켬** ✱ | 끔 |
| Anti-aliasing / Stop NaN / Dithering | None / 끔 / 끔 ✱ | None / 끔 / 끔 |
| Volume Mask | **Default** ✱ | Default |

✱ = 메뉴 3이 바꾸는 항목입니다. 위치, 컬링 마스크, 렌더러, 카메라 스택은 프로젝트마다 레이어 구성이 달라서 자동으로 바꾸지 않습니다. 필요하면 사용자와 상의하세요.

---

## 5. 똑같아 보이지 않을 때 확인할 것

1. **안개 거리**: Linear 57~193은 카메라로부터의 거리입니다. 원본은 카메라가 Z=-10에서 원근(FOV 25)으로 멀리 있는 배경(원경/중경/근경 레이어)을 봅니다. 대상 씬의 배경까지 거리가 다르면 같은 값이어도 안개 양이 다르게 보입니다. 이때 값을 바꾸지 말고 사용자에게 먼저 알리세요.
2. **셰이더가 안개를 지원하는지**: 3D 오브젝트 머티리얼 셰이더가 Unity 안개를 지원해야 안개가 보입니다. URP Lit, Simple Lit, Unlit은 지원합니다. 직접 만든 셰이더는 지원하지 않을 수 있습니다.
3. **후처리**: 카메라의 Post Processing이 켜져 있고 Volume Mask에 Default가 포함돼야 Bloom이 적용됩니다.
4. **2D 스프라이트**: 원본 2D 카메라는 Post Processing이 꺼져 있습니다. 2D 오브젝트에는 Bloom이 적용되지 않습니다.
5. **스카이박스**: 카메라 Clear Flags가 Skybox여야 하늘이 보입니다.

## 6. 작업 후 사용자에게 보고할 것
- 어느 메뉴(1/2/3)를 실행했는지
- 콘솔 보고 중 "기존 … 끔" 항목. 대상 씬에서 꺼진 라이트와 볼륨입니다.
- `!!` 경고가 있었다면 어떻게 해결했는지
- 2-6 URP 설정 중 원본과 다른 항목 (바꾸지 않았다면 다르다는 사실만)
