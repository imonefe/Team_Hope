# Event Horizon 프로젝트 지도

> Claude 가 파일을 매번 처음부터 뒤지지 않도록 "어디에 무엇이 있는지"를 정리한 문서. (2026-11-24 처음 작성)
> **규칙: 파일을 추가 · 삭제 · 이동 · 이름 변경하면 이 문서의 해당 줄도 같은 작업에서 고친다.** (CLAUDE.md 참고)
> 경로는 모두 `EventHorizon/Assets/` 기준.

## 1. 씬

| 씬 | 역할 |
|---|---|
| `0.Scenes/intro.unity` | 첫 화면. `MainMenuController`(새 게임 → SampleScene), `SceneBGM`(인트로 곡) |
| `0.Scenes/SampleScene.unity` | 본 게임 전부. 방 프리팹을 바꿔 끼우는 방식. `BGM` 오브젝트에 `RoomBGM` · `StageSkyboxColor` |
| `_Test/**` | 시험용 씬 · 방 · 데이터. **단, `_Test/Room/Room_BossTest.prefab` 은 실제 1스테이지 보스방** (`3. Data/MapData/1S/bosstest.asset` 이 참조). 보스 코어 3개 겉모습 = 검사 `FX_FogSphere_Left`(참격 · 주황 림) · 궁수 `FX_FogSphere_Pierce`(관통 · 청보라) · 마법사 `FX_FogSphere_Strike`(타격 · 진홍). 임시 사각형 그림은 꺼 둠. 부서지면 `BossCore.visual` 꺼짐 + `breakFx`(FX_CoreBreak_*) 생성 |

## 2. 게임 진행 흐름

```
StagePlan.asset (3. Data/MapData)        순서표: 튜토리얼 → 1S(로비 · 전투 7 · 중간보스 3 · 상점 2(6번째 · 12번째) · 보스) → 2S(로비 · 전투 3 · 상점 · 보스 · 엔딩)
  └ StageFlow (StagePlan.cs)             지금 몇 번째 칸인지 (CurrentEntry.group = "1S" / "2S")
MapManager (Map/Node)                    노드 지도 열기 → 칸 고르기 → MapRoomData 에서 방 하나 뽑기 (한 번 나온 방은 그 스테이지에서 다시 안 나옴)
RoomManager (Map/Room)                   방 프리팹 생성 · 플레이어 배치 · OnRoomLoaded 알림  ← BGM · 하늘색 · 증강 감독이 이걸 들음
Room → MonsterSpawner → Enemy            방 정리 → 문(Portal) 열림 → 다음 노드
BossController.OnTrueDeath               보스 처치 → BossSlowMotion(15% + 카메라 확대) → 끝난 뒤 증강 창
```

- 노드별 방 목록(`MapRoomData`): `3. Data/MapData/1S/` (graduateMap = 1S 전투방, MidBoss_* , ShopMap, bosstest), `3. Data/MapData/2S/` (CombatMap, ShopMap_2S, BossMap, MidBoss_Ending = 방 없음 → 건너뜀)
- 상점 방 목록(ShopMap · ShopMap_2S)은 `allowRepeat` 켜짐 → 한 스테이지에 상점 칸이 둘이어도 같은 상점 방이 다시 나옴
- 스테이지 켜고 끄기: 일시정지 화면 '스테이지 설정' (`StageSettingsWindow`)

## 3. 방 프리팹 (`1.Prefab/Room/`)

| 폴더 | 내용 |
|---|---|
| `1S/GradRoom_01~07` | 1스테이지(금서고) 전투방. 조명 묶음 `VolumetricTest`(빛줄기 + `RoomLighting`) 포함 |
| `2S/Room2S_01~06`, `Room2S_Boss` | 2스테이지(폐철장) 전투방 · 보스방. `Editor/Stage2RoomBuilder` 로 만듦. Room2S_02 = 쌍둥이 웅덩이(2026-11-25 새 구조, 메뉴 "2스테이지 Room2S_02만 다시 만들기"). Room2S_04 붕괴 계단 / 05 기둥 숲 / 06 고철 산 (2026-11-25, 메뉴 "2스테이지 새 전투방 만들기 (04~06)", 상자·회복 없음, 뒤 타일맵 없음, 발판 끝 2S_tile_1/3). 맨 위 오브젝트에 2S 전용 `RoomLighting`(빛줄기 없음) |
| `Lobby/LobbyRoom` | **두 스테이지가 같이 쓰는 로비** (방 이름으로 스테이지 구분 불가 → StageFlow 로 구분). 금서고식 `RoomLighting` |
| `Shop/ShopRoom_1S`, `ShopRoom_2S` | 상점 방 |
| `MidBoss/midbossRoom/01_mid_boss` | 중간 보스 공용 방 (궁수 · 마법사 · 검사 중 코어 종류에 맞는 보스만 남김). `mid_boss_s01~03` 은 입장 코어가 있는 방 |
| `Tutorial/TutorialRoom` | 튜토리얼 방 (다시 만드는 옛 메뉴 실행 금지 — CLAUDE.md) |
| `Test/AugmentTestRoom` | 증강 시험 방 (F8) |

## 4. 적 · 보스

| 종류 | 위치 |
|---|---|
| 1스테이지 적 | `1.Prefab/Enemy/1S/1S_Atk · Dps · Ran01 · Ran02 · Spd` |
| 2스테이지 적 | `1.Prefab/Enemy/2S/Enemy_Atk · Dps · Ranged01 · Ranged02 · Spd` (+ `bullet`, `missile` 투사체) |
| 중간 보스 | `1.Prefab/Enemy/MidBoss/MidBoss_Archer · Mage · Swordsman` (1S · 2S 공용) |
| 1S 보스 | `1.Prefab/Enemy/1S/Boss1_IntroCutscene` |
| 2S 보스 | `1.Prefab/Enemy/2S/2boss` |
| 생성기 | `1.Prefab/Enemy/Spawner/1s/1sSp_*`, `Spawner/2s/2sSp_*` |
| 적 히트박스 | `1.Prefab/Hitbox/1S/`, `Hitbox/2S/2S_Enemy/`, `Hitbox/2S/2S_Boss/` (모두 투명 · 판정만) |
| 플레이어 무기 히트박스 | `1.Prefab/Hitbox/Player/` |
| 적 애니메이션 | `4. Art/Character/Enemy/Animations/Enemy.controller` = **틀**. 실제 적은 각자 override 컨트롤러를 써야 함 (예: `Stage2/Atk/2S_Attack.overrideController`) — 틀에 직접 연결하면 공격 판정 이벤트가 없어 공격을 못 함 |

- 2스테이지 적 크기: 프리팹 맨 위 크기(1.32 / 1.44) + `Enemy.spawnScale = 1.2` (히트박스 · 투사체 · 이펙트 · 공격 시작 거리에 곱함)
- 직선 사격형(직선 투사체 + `aimAtPlayer` 끔, 지금은 Enemy_Ranged02): 가로 선 위에 있을 때만 쏨 (`lineAttackMargin`)
- 수치(2026-11-24 밸런스): 1S 적 공격력 −25%, 2S 적 = 같은 역할 1S × 체력 1.3 · 공격력 1.2, 보스 · 중간 보스 체력 2배 · (2026-11-25) 공격력: 중간 보스 10.8→16, 1S 보스 15→22, 2S 보스 22→28

## 5. 증강 · 아이템

- 자세한 내용은 CLAUDE.md '증강 시스템'. 코드 `2.script/Augment/`, 데이터 `3. Data/ItemData/OBJ/<증강 이름>/`
- 증강 고르기 창: `1.Prefab/UI/Canvas/상점/AugmentOfferCanvas` + `AugmentOfferWindow` (두트윈 등장 · 선택 확대), 감독 `AugmentOfferDirector`(씬의 AugmentOffer 오브젝트)
- 증강 데이터의 `effects` 칸 = 기본 스탯(`StatBoostEffect`)만 남김. 회복은 과부하 키워드 증강만. 마이너스 스탯은 `limitText`(제한 줄)에 적음
- 효과 스크립트: `2.script/Item/Effect/`

## 6. 소리 (`5. Sound/`)

- BGM 실제 사용 파일: `5. Sound/BGM/Suno/*_Loop.ogg` (원본 mp3 는 같은 폴더). 시작 2초 페이드인 · 끝 3초 페이드아웃
  - 인트로 `Suno_Intro_Loop`, 튜토리얼 `Suno_Tutorial_Loop`, 1S `Suno_Lobby/Battle/Boss/MidBoss_Loop`, 2S `Suno_2S_Lobby/Battle/Boss_Loop`
- 곡 고르기: `RoomBGM` (방 종류 + StageFlow 묶음 "2S" 이면 2S 곡). 씬이 열린 첫 방은 처음부터, 같은 곡이면 이어서 반복
- mp3 → ogg 변환 시 **앨범 표지(영상 트랙)를 반드시 빼야 함** (`-map 0:a:0 -vn`). 빼지 않으면 유니티가 무음으로 읽음

## 7. 조명 · 하늘 · 카메라

- 방 조명: `2.script/Lighting/RoomLighting` (환경광 · 하늘 밝기 · 안개 · 스프라이트를 빛 받는 재질로). 값은 방 프리팹마다 따로 들어 있음
- 스프라이트 조명 셰이더: `6. Shader/SpriteLit3D.shader` (밝기 = max(환경광 + 빛, 최소 밝기))
- 하늘: 씬 RenderSettings 의 `Etc/Ultra Skybox Fog/Materials/nomer.mat` (환경광은 단색 모드라 하늘과 무관). 2S 하늘색 = `StageSkyboxColor`
- 카메라 2대: 3D 원근 카메라(배경, ProCamera2D) + 자식 2D 직교 카메라(캐릭터 · 타일). `CameraFollow` 가 2D 카메라 크기를 맞춤
- 연기 · 안개 이펙트: `1.Prefab/Effect/VFX/FX_SoftSmoke_Test`(증강 가스 분사가 색을 입혀 씀), `FX_FogSphere_Left`(3D 구 안개 — 무늬가 왼쪽으로 흐름, 다른 오브젝트용. 셰이더 `6. Shader/FogSphere.shader`, 재질 `4. Art/Materials/VFX/Fog/M_FogSphere.mat` · 주황 겹 재질 `M_FogSphere_Orange.mat`. 자식 Quill = 참격 중간 보스 코어 스파인(ring_slash)의 깃펜(e01, 부서져도 남는 부품)을 잘라 낸 스프라이트 `4. Art/Textures/Object/MidBossCore/ring_slash_e01_quill.png`. 보스방 코어용으로 참격처럼 세 겹 쌓는 중)
- 코어 부서짐 연출 재료: `4. Art/Textures/VFX/CoreBreak/`(번쩍임 · 5×5 비트맵 글자판 · 속성별 파편판), 상징 그림 `4. Art/Textures/Object/MidBossCore/ring_*_e01_quill.png`
- 보스 처치 확대 · 슬로우: `Effect/BossSlowMotion` (그리는 순간에만 확대 → ProCamera2D 와 안 부딪힘)

## 8. 데이터 (`3. Data/`)

`ItemData`(아이템 · 증강), `MapData`(StagePlan · 노드별 방 목록), `SkillData`, `WeaponData`(Sword ×1.3 · Hammer ×2.5 · Glove ×1.1), `Physics`

## 9. 자주 헷갈린 지점

- 로비 방은 두 스테이지 공용 → 스테이지별로 다르게 할 것은 StageFlow(묶음 이름)로 판단
- 2S 방은 1S 방(GradRoom_01)을 복사해 만든 것 → 1S 전용 요소(빛줄기 · 책장 · 금서고 코어)가 섞이지 않았는지 확인
- 유니티가 열어 둔 씬 파일을 밖에서 고치면 → "저장하지 말고 다시 불러오기"
- 적 크기를 바꾸면 `spawnScale` 도 같이 (판정 · 공격 거리)
- `StatBoostEffect` 수치는 증강 강화(Lv2 ×1.5, Lv3 ×2)에 따라 함께 커짐

## 10. 스크립트 목록 (한 줄 설명)

**2.script/**
- `EffectPrewarmer` — 이펙트를 미리 한 번 만들어 첫 사용 때 끊김을 줄임 (스스로 생김)

**2.script/Augment/**
- `AugmentBuffTray` — 체력바 오른쪽 끝에 맞춰, 지금 걸려 있는 '몇 초짜리' 증강 효과를 아이템 아이콘으로 보여 준다.
- `AugmentFx` — 증강 연출 부품 모음 — 기획서 '연출 기획' 표의 공용 부품을 코드 한 줄로 부르게 한 곳.
- `AugmentOfferDirector` — 증강을 언제 줄지 정하고 고르기 창(AugmentOfferWindow)을 연다.
- `AugmentOfferWindow` — 증강 고르기 창 — 3개 중 1개.
- `AugmentRules` — 증강 키워드 8종 (증강 기획서 '키워드 8개와 단계 능력')
- `AugmentRuntime.Advanced` — 증강 고유능력 — 희귀 22 · 영웅 11 · 전설 6.
- `AugmentRuntime` — 증강 능력을 실제로 돌리는 곳 — 키워드 8종 단계 효과 + 고유능력 59종 전부.
- `AugmentSandbox` — 연습용 증강 칸 — 튜토리얼 증강 체험에서 얻은 증강을 본 게임으로 가져가지 않게 한다.
- `AugmentTestRoom` — 증강 시험 방 (AugmentTestRoom 프리팹 맨 위 오브젝트에 붙음).
- `CombatEvents` — 전투 알림 모음 — 증강 능력이 전투 사건을 듣고, 피해 계산에 끼어드는 곳.
- `EnemyStatus` — 적에게 붙는 증강 상태 — 과열 · 균열 스택, 처형 표시, 머리 위 상태 띠.

**2.script/BG/**
- `InfiniteBackground` — 가로: 카메라 X에 고정한 채 텍스처만 흘러 무한 스크롤 원근을 낸다(여백 없음).

**2.script/Camera/**
- `AspectRatioEnforcer` — 화면 비율을 목표 비율(기본 16:9)로 고정한다.
- `CoreBreakFx` — (Effect) 보스방 코어 부서짐 연출 — 상징(깃펜)을 키우며 사라지게 · 화면 흔들림 · 끝나면 지움
- `CameraFollow` — ProCamera2D 에셋이 스스로 못 하는 부분만 보조하는 얇은 헬퍼.
- `CutsceneSequencer` — 플레이어 입력 없이 초 단위로 자동 진행되는 컷신용 시퀀서.

**2.script/Combat/**
- `Hitbox` — 근접 판정용 히트박스. 타격 방식(hitMode)에 따라 두 가지로 동작한다:
- `ProjectileBase` — 투사체 공통 베이스 클래스
- `ProjectileHoming` — 유도 미사일
- `ProjectileStraight` — 직선 투사체

**2.script/Creature/**
- `FogHpColorizer` — HP 비율 구간마다 지정한 색으로 씬 전체 Fog(RenderSettings.fogColor)를 서서히 바꾼다.
- `Health` — 맞기 · 무적 · 사망 처리 (Stats 의 체력을 깎음)
- `HealthDangerEffect` — HP가 낮아질수록 화면에 붉은 비네트 + 색 보정을 씌워 긴박한 분위기를 연출한다.
- `LightHpColorizer` — HP 비율 구간마다 지정한 색으로 이 오브젝트의 Light 색을 서서히 바꾼다.
- `SkyboxHpColorizer` — HP 비율 구간마다 지정한 색으로 스카이박스 머티리얼의 Tint Colour를 서서히 바꾼다.
- `Stats` — 체력 · 공격력 · 방어력 · 이동속도 · 치명타 · 퍼즈 시간. 피해 공식 CalculateEffectiveDamage (공격 − 방어, 최소 1)

**2.script/Creature/Boss/**
- `ArcherMidBoss` — 관통(궁수) 중간보스.
- `Boss1Controller` — 1스테이지 보스
- `BossController` — 모든 보스의 공통 기반 (처치 → 슬로우 · 증강 · 재화)
- `BossCore` — 보스방 코어 1개. 자식 HitBody(Enemy 레이어 + Stats + Health)가 실제로 맞는 몸체이고,
- `BossCoreShield` — 보스방에 1개. 방 안의 보스(BossController)를 찾아
- `MageMidBoss` — 타격(마법사) 중간보스 — 테스트판 (애니메이션·이펙트 없음, 색 박스 + 머리 위 글자).
- `MidBossController` — 중간보스 공용 컨트롤러.
- `MidBossTestBase` — 애니메이션·이펙트가 아직 없는 중간보스를 시험하기 위한 공통 부모.
- `SwordsmanMidBoss` — 참격(검사) 중간보스 — 테스트판 (애니메이션·이펙트 없음, 색 박스 + 머리 위 글자).

**2.script/Creature/Enemy/**
- `AnimEventRelay` — 자식 오브젝트 애니메이션 이벤트를 부모로 전달
- `Enemy` — 일반 적 AI — 추적 · 공격 판정(AttackRange = attackRange × spawnScale) · 히트박스/투사체 생성(애니메이션 이벤트 SpawnHitbox) · 직선 사격형 선 판정

**2.script/Effect/**
- `AutoPlayEffect` — 이펙트 프리팹에 붙이는 공용 스크립트.
- `BossSlowMotion` — 보스급(중간 보스 · 보스) 처치 연출 — 게임 속도를 15%로 떨어뜨렸다가 1.2초(실제 시간)에 걸쳐 원래대로.
- `CurrencyShardFX` — 재화 획득 연출 — "+N" 숫자 대신 노란 작은 파편이 튀어 올랐다가 바닥에 떨어져 잠깐 구르고 사라진다.
- `DeathDebris` — 적 사망 이펙트 프리팹 안에 "잔해 조각"(머리, 무기 등)으로 넣는 오브젝트에 붙이는 스크립트.
- `EffectTimeUtil` — 이펙트 오브젝트 자동 파괴 시간 계산 공용 유틸.
- `FloatingText` — 데미지 숫자 / 재화 획득 등, 월드 공간에 잠깐 떴다가 위로 떠오르며 사라지는 텍스트.
- `FloatingTextSpawner` — FloatingText를 어디서든 한 줄로 띄울 수 있게 해주는 헬퍼.
- `FxMirror` — 파티클 이펙트 좌우 반전 보조.
- `HammerChargeFX` — 망치 차지 연출 — 차지가 무장되는 순간부터 점과 선이 캐릭터를 향해 빨려 들어온다.
- `HitGlitchEffect` — 플레이어가 피격될 때마다 화면 가장자리에 붉은 글리치를 띄우는 전체 화면 효과.
- `HitGlitchScreenFeature` — 화면 글리치 효과를 "실제 게임 화면"에 입히는 렌더러 기능 (URP Renderer Feature).
- `HitGlitchSettings` — 피격 글리치 효과 설정 (프로젝트 전체 공용 1개).
- `HoloCloneFX` — (설명 없음)
- `HoloScreenSlash` — (설명 없음)
- `ItemPickupEffect` — 아이템 획득 효과. Item.Pickup() 에서 ItemPickupEffect.Spawn(위치, 아이템데이터) 한 줄로 부른다.
- `KeepUnflipped` — 부모가 좌우 반전(localScale.x < 0)돼도 이 오브젝트는 반전되지 않게 되돌린다.
- `LightShaftFX` — 빛줄기 + 먼지 연출 (기본 레이어에서 2D 카메라가 그림).
- `LowHpGlitchEffect` — 플레이어 HP 가 기준(기본 20%) 이하일 때 화면 가장자리에 채도 낮은 회붉은 노이즈 + 심한 왜곡을 씌우는 효과.
- `LowHpGlitchSettings` — 저체력 화면 효과(가장자리 회붉은 노이즈 + 심한 왜곡) 설정 (프로젝트 전체 공용 1개).
- `OrbPulse` — 원거리 적 에너지 구체(EnemyOrbProjectile)의 살아 있는 느낌.
- `PauseAfterimage` — 퍼즈(Q키) 중 주인공이 움직이면 그 자리에 "멈춘 잔상"을 남긴다.
- `PauseCameraZoom` — 퍼즈(Q키)가 발동되어 있는 동안 카메라를 넓게 잡는다 (줌 아웃). 퍼즈가 끝나면 원래 크기로 돌아온다.
- `PauseGlitchEffect` — 퍼즈(Q키)가 발동되어 있는 동안 화면을 푸르게 물들이고, 가장자리에 약한 노이즈 + 움직이는 번개를 씌우는 효과.
- `PauseGlitchSettings` — 퍼즈 발동 효과(푸른 화면 + 테두리 노이즈 + 번개) 설정 (프로젝트 전체 공용 1개).
- `PlayOnP` — (테스트) P키로 재생
- `RandomSpawner` — (설명 없음)
- `ScreenDistortFeature` — 이펙트용 "부분 화면 왜곡" 렌더러 기능 (URP Renderer Feature). Renderer3D_LightingTest 에 등록.
- `ScreenDistortSource` — "이 오브젝트는 화면 왜곡용"이라고 표시하는 컴포넌트.
- `ScreenSlashEffect` — 화면 가르기(참격) 풀스크린 연출 컨트롤러.
- `ScreenSlashMultiLine` — 화면 가르기를 여러 줄 동시에 긋는다 (ScreenSlashEffect 옆에 붙임).
- `ShieldImpactFX` — (설명 없음)
- `ShieldPulse` — (설명 없음)
- `SpawnEffectTrigger` — 몬스터 소환 이펙트 프리팹들에 붙이는 스크립트.
- `SpineMaterialOverride` — 스파인(SkeletonAnimation/SkeletonRenderer) 머티리얼 바꿔치기.
- `SpriteAnimEffect` — 소환되면 애니메이션이 재생되고, 끝나면 정해진 시간후에 사라지는 스프라이트 이펙트용.
- `SpriteFrameSequence` — 스프라이트 여러 장을 프레임마다 정한 시간대로 한 번(또는 반복) 재생한다.
- `WarningZone` — (설명 없음)

**2.script/Item/**
- `Box` — 상자
- `BoxSpawner` — 방 정리 시 상자 생성
- `HealObject` — (설명 없음)
- `HealOrb` — [2026-11-25] 회복 구. 일반 적 처치 시 25% 확률로 떨어지고, 먹으면 최대 체력 15% 회복 (× CombatEvents.HealOrbMultiplier). 프리팹 `Etc/Resources/HealOrb.prefab` 에서 확률 · 회복량 조절. Health.Die 에서 부름
- `Item` — 바닥 아이템 (줍기 · 확보)
- `ItemData` — 아이템 · 증강 데이터 (itemName · abilityName · abilityText · keywords · effects · limitText)
- `SkillPickup` — 스킬 획득용 월드 오브젝트.
- `SpawnUtil` — 스폰 관련 공용 유틸. Box / BoxSpawner 등이 공통으로 사용.

**2.script/Item/Effect/**
- `EffectText` — 아이템 효과를 아이템 정보 팝업에 보여줄 "글자"로 바꿔주는 공용 도우미.
- `FadeEffect` — 시간이 지나면서 점차 효과가 줄어드는 효과
- `HpInverseAtkEffect` — 잃은 체력에 비례해 공격력 증가 (세 시간의 말미)
- `HpInverseAtkRunner` — HpInverseAtkEffect 가 플레이어에 붙이는 부품.
- `HpRegenEffect` — 최대체력의 N% 이하로 떨어지면 interval 초마다 HP 회복
- `ItemEffect` — 모든 아이템 효과의 기본 틀
- `RandomStatBoostEffect` — 후보 스탯 중 하나를 무작위로 골라, 지정한 크기만큼 영구 적용한다.
- `RepeatingStatEffect` — "일정 시간 효과 적용 후 원복, repeat면 cooldown 후 반복" 하는 스탯 이펙트 공통 베이스.
- `RestHealEffect` — 일정 시간 움직이지 않으면 주기적으로 체력 회복 (영웅의 쉼터)
- `RestHealRunner` — RestHealEffect 가 플레이어에 붙이는 부품.
- `StackEffect` — 스택 쌓일수록 강해지는 효과
- `StatBoostEffect` — 영구 스탯 변화
- `StatModifier` — 스탯 변화 구조체
- `TimedEffect` — N초 동안 스탯 변화 후 원복

**2.script/Lighting/**
- `DarknessScreenFeature` — 화면을 어둡게 하고, 빛줄기(LightBeam)가 닿는 곳만 원래 색으로 되살리는 렌더러 기능 (URP Renderer Feature).
- `DarknessSystem` — 화면 어둠(어둠 + 빛줄기) 상태를 한곳에서 관리하는 정적 클래스.
- `LightBeam3D` — 입체 원뿔 빛줄기. 3D 배경용. (2D 화면용 납작한 빛줄기는 LightBeam)
- `RoomDarkness` — 이 컴포넌트가 붙은 방에 들어가면 화면이 어두워지고, LightBeam(빛줄기)이 닿는 곳만 밝게 보인다.
- `RoomLighting` — 이 컴포넌트가 붙은 방에 들어가면 씬의 "환경광·하늘·안개"만 어둡게 만든다. 빛 연출은 방 안에 둔 빛으로 따로 한다.
- `SpotLightDust` — 스포트라이트(또는 점광원)에 붙이면, 실행할 때 그 빛 안에 "빛받는 먼지" 파티클을 자동으로 만든다.
- `StageSkyboxColor` — 2스테이지(폐철장)에서만 하늘(스카이박스) 색을 바꾼다.

**2.script/Map/**
- `TilemapOutlineMask` — 타일 테두리 회로 빛용 "칸 지도"
- `TrapTile` — 함정 타일맵에 붙이는 스크립트

**2.script/Map/Lobby/**
- `TrainingDummy` — 로비 허수아비. 맞아도 죽지 않는 과녁 — 무기 손맛을 미리 확인하는 용도.
- `WeaponStand` — 로비 무기 진열대. 무기를 미리 들어 보고 마음에 드는 걸로 출발할 수 있게 한다.

**2.script/Map/Node/**
- `MapButton` — 마인드맵 버튼 하나
- `MapManager` — 노드 캔버스(마인드맵) 흐름 관리.
- `MapPositionMarker` — 노드 캔버스에서 "내가 지금 있는 노드" 위에 떠 있는 금색 마름모 핀.
- `MapRoomData` — 버튼 하나에 연결되는 맵 데이터
- `MapRoomInfoUI` — 노드 캔버스에서 고른 노드의 방 이름·설명을 보여 주는 글자 묶음.
- `Maptier` — 노드 등급 표시
- `StagePlan` — 튜토리얼부터 마지막 스테이지까지의 순서표 (3. Data/MapData/StagePlan.asset).

**2.script/Map/Room/**
- `EnemyCleanupTrigger` — 플레이어가 닿으면 씬의 모든 Enemy 레이어 오브젝트 삭제
- `MidBossGate` — 일반 방에 놓이는 중간 보스 입장 코어.
- `MidBossRoomRoster` — 중간 보스 공용 방(01_mid_boss)에서, 들어온 코어 종류에 맞는 보스만 남기는 목록.
- `MonsterSpawner` — 몬스터 스포너
- `PlayerSpawner` — 방의 플레이어 시작 위치
- `Portal` — 방의 문 — 열림/닫힘 애니메이션, 들어가면 다음 노드
- `Room` — 방 한 칸 — 생성기 관리 · 방 정리 판정 · 보스방 카메라 고정
- `RoomCleanupTrigger` — 플레이어가 닿으면 미활성 스포너 제거 후
- `RoomManager` — 방 불러오기 · 플레이어 배치 · OnRoomLoaded 알림 · 중간 보스 방 다녀오기

**2.script/Map/Tutorial/**
- `TutorialController` — 튜토리얼 방 (기획서: 한 방 · 화면 글자 · 해 봐야 다음 구간이 열림 · 건너뛰기 없음).
- `TutorialZone` — 튜토리얼용 영역 (트리거).

**2.script/Player/**
- `PlayerController` — 플레이어 이동 · 대시 · 공격 · 이펙트
- `PlayerFootstepSfx` — 애니메이션 이벤트로 발소리를 재생한다. 등록한 소리 중 하나를 랜덤으로 고른다.

**2.script/Shop/**
- `AugmentUpgradeWindow` — 증강 강화 화면 — 능력치 강화(StatUpgradeWindow)를 대신한다.
- `ItemShopWindow` — 아이템 상점 화면 (ItemShopCanvas 맨 위 오브젝트에 붙음).
- `ShopNpcTrigger` — 상점 방의 NPC. 가까이 가면 머리 위에 "[F] 상점" 안내, F를 누르면 연결된 상점 화면을 연다.
- `ShopWindow` — 상점 화면 공통 (아이템 상점 · 스킬 상점 · 능력치 강화).
- `SkillShopWindow` — 스킬 상점 화면 (SkillShopCanvas 맨 위 오브젝트에 붙음).
- `StatUpgradeWindow` — 능력치 강화 화면 (StatUpgradeCanvas 맨 위 오브젝트에 붙음).

**2.script/System/**
- `AudioManager` — BGM / SFX 통합 관리
- `CharacterManager` — 캐릭터 A/B 관리 · 사망 → 게임오버 (캐릭터 교체 기능은 쓰지 않을 예정)
- `CharacterSkills` — 캐릭터 한 명이 보유하는 스킬 2칸(슬롯 0/1). [2026-11-20] 3칸 → 2칸 (키 A·S)
- `ExhibitionMode` — 전시 모드 — 관람객이 만지는 PC에서 사고를 막는다. (씬에 놓을 필요 없음, 게임이 켜지면 스스로 생김)
- `GamePause` — 창(ESC 메뉴, 인벤토리, 노드맵, 게임오버)이 게임 시간을 멈추는 것을 한곳에서 관리한다.
- `IdleResetWatcher` — 게임 중에 일정 시간(기본 3분) 동안 아무 입력이 없으면 판을 초기화하고 인트로(첫 화면)로 돌아간다.
- `KeyBindings` — 조작 키 저장소. 키를 읽는 곳은 전부 KeyBindings.Get(동작) 을 쓴다.
- `PauseManager` — 퍼즈 시스템 관리
- `PersistentSingleton` — "이미 인스턴스가 있으면 나를 파괴, 아니면 내가 유일한 인스턴스로 등록 + 씬 전환에도 유지"
- `ProximityInteractable` — "플레이어가 근처에 있을 때 F키를 누르면 상호작용" 패턴의 공용 베이스.
- `ReleaseLogSwitch` — (System) 정식 빌드에서 Debug.Log · 경고 출력을 끔 (웹 빌드 속도). 에디터 · 개발 빌드는 그대로. 씬 배치 불필요
- `RoomBGM` — 방 종류에 따라 배경음을 바꾼다 (로비 / 배틀 / 보스방).
- `RunManager` — 이번 판(런) 동안만 유지되는 데이터를 한 곳에서 관리한다.
- `SceneBGM` — 씬에 하나 배치해서 "이 씬에서 틀 BGM"을 지정하는 컴포넌트.
- `ScreenshotFreeze` — 스크린샷용 화면 정지 — 창(일시정지 캔버스) 없이 게임 시간만 멈춘다. (씬에 놓을 필요 없음, 게임이 켜지면 스스로 생김)
- `TestMaster` — 테스트 단축키 (Ctrl + 숫자)

**2.script/UI/**
- `ButtonHighlightDeco` — 버튼이 하이라이트 상태(마우스를 올리거나 키보드로 선택)가 되면
- `ButtonTextColorSync` — 버튼 옆에 붙이면, Button 컴포넌트에 이미 설정된 ColorBlock(Normal/Highlighted/Pressed/
- `CharacterPanelUI` — 우측(캐릭터 이미지) + 중앙(스킬 3칸 / 스탯 5줄) UI.
- `CombatUIGate` — 전투 UI(배틀 캔버스 등)를 잠시 꺼야 하는 상황(인벤토리, 대화 등)이 여러 곳에서 동시에
- `CombatUIVisibility` — 인벤토리/대화 등으로 CombatUIGate가 잠기면 전투 UI(예: battle Canvas)를 끄고,
- `ControlsGuidePopup` — 프롤로그(로비 첫 대화)가 끝나면 화면 가운데에 조작법 창을 띄우고, 아무 키나 누르면 닫는다.
- `CurrencyUI` — battle Canvas 한쪽에 붙어서 현재 보유 재화를 표시하는 간단한 UI.
- `CutsceneDialogueBox` — 시간 기반 컷신(CutsceneSequencer)에서 쓰는 대사창.
- `CutsceneOverlay` — 컷신용 화면 덮개 — 위아래 검은 띠, 섬광, 암전, 대사창.
- `EnemyCountUI` — 남은 적 수 표시
- `GameMenuController` — ESC 게임 메뉴
- `GeeksPerkWindow` — GEEKS 2026 특전 창. 로비 프롤로그가 끝나면 무작위 사이버웨어 5개를 주고 알림 (설정 창 '이벤트' 스위치로 끔 · 한 판에 한 번). 창 프리팹 `1.Prefab/UI/Canvas/특전/Resources/GeeksPerkCanvas`
- `GameOverAugmentCell` — 게임오버 · 엔딩 화면의 '획득한 증강' 칸 하나.
- `GameOverUI` — 게임오버 화면. CharacterManager.GameOver()가 Show()를 호출해서 활성화하고,
- `HpUI` — 플레이어 · 보스 체력 바
- `InteractPrompt` — 상호작용 안내 — 상자·NPC 등에 가까이 가면 그 오브젝트 위에 "[F] 열기" 상자를 띄운다.
- `IntroIllustrationCycler` — 인트로 화면 캐릭터 일러스트 교차.
- `IntroLayout` — 인트로 화면 배치를 한곳에서 조절한다 (화면 1920×1080 기준 px).
- `Inventory` — 인벤토리 데이터. 패시브·일반 아이템을 관리한다.
- `InventoryKeywordColumn` — 인벤토리 오른쪽 위에 세로로 늘어선 키워드 아이콘 8개.
- `InventoryUI` — 인벤토리 창 컨트롤러.
- `ItemInfoPopup` — 아이템 정보 팝업 (공용 싱글턴).
- `ItemSlotUI` — 인벤토리 슬롯 하나. 아이콘 표시 + 클릭/호버 이벤트를 InventoryUI로 전달한다.
- `KeyLabel` — 키 글자 자동 맞춤. 키 이름을 보여 주는 글자(TMP)에 붙이고 "어느 동작인지"만 고르면,
- `KeyboardMenuNav` — 메뉴 버튼을 키보드 위/아래로 고르고 결정하는 공용 컴포넌트 (인트로 화면, ESC 일시정지 메뉴).
- `KeywordIconHover` — 키워드 아이콘 칸 — 마우스를 올리면 키워드 설명 팝업
- `LoadingScreen` — 씬을 비동기로 불러오면서 진행바를 보여주는 로딩 화면.
- `MainMenuController` — 인트로 메뉴 (새 게임 → SampleScene, 설정 열기)
- `MenuBackdrop` — 일시정지 메뉴(ESC)가 열려 있는 동안의 "배경" 처리.
- `PauseGaugeUI` — 퍼즈 게이지 UI
- `RoomMinimap` — 화면 우측 상단 미니맵 — 지금 있는 방 하나만 보여준다.
- `ScreenNotice` — 화면 하단 중앙에 잠깐 떴다가 아래로 내려가며 사라지는 안내 문구.
- `SkillBarUI` — HP 바 인근에 배치되는 스킬 3칸 UI.
- `StatusPanelUI` — 인벤토리 왼쪽 능력치 판 (시안 C4).
- `TransitionCanvas` — 방 전환 시 페이드 인/아웃 연출
- `UIButtonJuice` — 버튼(또는 아무 UI 요소)에 붙이면 마우스 오버 시 살짝 확대되고, 클릭 시 펀치 축소되는
- `UIButtonSound` — (선택) 특정 버튼만 클릭 소리 종류를 직접 고르거나, 전용 소리를 쓰거나, 소리를 끄고 싶을 때 그 버튼 오브젝트에 붙인다.
- `UIClickSound` — 모든 UI 버튼 클릭 효과음을 한 곳에서 처리하는 자동 생성 오브젝트.
- `UIEmberParticles` — 화면 밑에서 서서히 올라오는 잔불·불씨 파티클 (UI 전용).
- `UIFadeSlideIn` — 오브젝트가 켜질 때마다(OnEnable) 투명도 0 → 1 로 페이드인하면서 오른쪽에서 왼쪽으로 들어오는 연출.
- `UILoader` — 게임 시작 시 필요한 UI 캔버스 프리팹들을 한 번씩 생성해두는 스크립트.
- `UIPanelAnimator` — 패널이 SetActive(true)로 켜질 때마다(=OnEnable) 페이드인 + 살짝 스케일업(예: 0.9 → 1)으로
- `UIScrollingPattern` — 작은 무늬 그림 한 칸을 화면 전체에 바둑판처럼 반복하고, 천천히 흐르게 한다 (인트로 배경용).
- `UISoundSettings` — UI 버튼 클릭 효과음 설정 (프로젝트 전체 공용 1개).
- `UIUtil` — UI 공용 유틸. 여러 UI 스크립트에서 반복되는 "아이콘 스프라이트 세팅 + null이면 숨김" 패턴과
- `WeaponShowcaseUI` — 인벤토리 능력치 판의 '현재 무기' 칸 (시안 C4).
- `WeaponStripUI` — 체력 바 오른쪽 끝에 붙는 작은 무기 정보 칸 (시안 B안).
- `WeaponUI` — 무기 UI
- `YoungbokChatter` — 영복 잡담 통신창 — 화면 왼쪽 가운데에 영복 얼굴과 짧은 대사를 잠깐 띄운다 (디자인: 시안 E "정비 통신창").

**2.script/UI/Npc/**
- `DialogueBeat` — 대화 연출 한 덩어리. 이 오브젝트가 켜지는 순간(OnEnable) 아래 연출을 DOTween 시퀀스로 재생한다.
- `LobbyIntroDialogue` — 로비(영복의 은신처)에 들어오면 프롤로그 대화를 자동으로 시작한다.
- `Npc` — NPC 대화
- `SpinePortraitTint` — 대화·상점 초상 칸의 스파인 표시 부품이 원래 그림(Image)의 색을 그대로 따라가게 한다.
- `npcTrigger` — NPC 대화 시작 트리거

**2.script/UI/Settings/**
- `GameSettings` — 게임 설정 값을 한곳에서 저장·불러오기·적용한다. (정적 클래스 — 씬에 놓을 필요 없음)
- `HudSettingVisibility` — 설정 창의 켬/끔에 따라 HUD 묶음을 보이거나 숨긴다.
- `KeyBindWindow` — 설정 창 안의 '조작 키' 창. (SettingsCanvas 프리팹의 KeyPanel 에 붙어 있음)
- `SettingSlider` — 설정 창의 0~100% 슬라이더 한 개.
- `SettingStepper` — 설정 창의 "◀ 값 ▶" 고르기 한 개 (해상도 고르기에 사용).
- `SettingToggle` — 설정 창의 켬/끔 스위치 한 개.
- `SettingsWindow` — 설정 창.
- `StageSettingsWindow` — 일시정지 화면 '스테이지 설정' 창.

**2.script/Weapon/**
- `Weapon` — 무기 장착 · 스탯 적용
- `WeaponData` — 무기 데이터 (배율 · 판정 · 효과음)

**2.script/skill/**
- `HoloCloneSkill` — (설명 없음)
- `PlayerShield` — (설명 없음)
- `ShieldSkill` — (설명 없음)
- `Skill` — 스킬 프리팹의 "두뇌" (A방식).
- `SkillSfx` — 스킬 효과음 재생 도우미.
- `SwordFlurrySkill` — (설명 없음)

**Editor/**
- `BossCoreSetup` — 보스방 프리팹에 코어 3개(발판 위 2 · 바닥 1)와 BossCoreShield를 배치한다.
- `BossRoomExitSetup` — 보스방에 나가는 문(포탈)을 붙인다. 유니티가 켜질 때 문이 없으면 자동으로 한 번 실행.
- `DialogueBeatTestSceneBuilder` — DialogueBeat(대화 연동 연출) 테스트 씬을 자동으로 만들어 주는 에디터 도구.
- `DialoguePortraitSpineSetup` — 대화창·상점 캔버스의 캐릭터 정지 그림 칸에 스파인 대기 동작을 겹쳐 놓는다. (한 번 실행해 프리팹에 저장)
- `GeeksPerkCanvasBuilder` — (Editor) GEEKS 2026 특전 창 프리팹을 만든다. 유니티 켜질 때 한 번 자동, 메뉴 "EventHorizon/GEEKS 2026 특전 창 만들기"
- `CoreBreakFxBuilder` — 보스 코어 부서짐 연출 프리팹 3개(FX_CoreBreak_Slash/Pierce/Strike) · 재질(4. Art/Materials/VFX/CoreBreak) 생성 + 보스방 BossCore.breakFx 연결. 없으면 한 번 자동 실행
- `DoorStageFix` — 방마다 제각각이던 문을 스테이지에 맞게 통일한다. 유니티가 켜질 때 한 번 자동 실행.
- `EffectPreviewCapture` — (설명 없음)
- `Plan1116Setup` — 기획서 반영용 1회성 도구.
- `RemoveWeaponStands` — 로비 무기 진열대 제거 — 에디터 전용 (한 번 실행용).
- `RoomBGReplacer` — 방(Room) 프리팹 안의 "BG" 오브젝트 밑에 있는 배경 오브젝트들을
- `RoomCameraRangeEditor` — 보스방·중간보스 방의 카메라 고정 범위를 씬 뷰에서 보고 바로 고치는 도구.
- `ShopCanvasBuilder` — 상점 화면 프리팹 생성기 (에디터 전용).
- `ShopRoomBuilder` — 1스테이지(금서고) 상점 방 프리팹 생성기 (에디터 전용).
- `ShopRoomFunctionSetup` — 상점 방(ShopRoom_1S)에 상점 기능을 연결하는 에디터 도구.
- `SkillFxLibraryBuilder` — (설명 없음)
- `SlashTestSceneBuilder` — 참격 이펙트(SwordSlashFX · ScreenSlashEffect) 테스트 씬을 자동으로 만드는 에디터 도구.
- `SpineTestSceneBuilder` — (설명 없음)
- `Stage2RoomBuilder` — 2스테이지(금서고가 아닌 돌 갱도 · 로봇 스테이지) 방 생성기 (에디터 전용).
- `TutorialBackgroundSetup` — 튜토리얼 방에 회로 배경 판을 놓는다 (셰이더 EventHorizon/TutorialCircuitBG).
- `TutorialBackgroundVariants` — 튜토리얼 회로 배경 시안 4종 (셰이더는 같고 재질 값만 다름).
- `TutorialPropsRebuild` — 튜토리얼 방 소품·문 다시 배치 (새 타일 배치 기준).
- `TutorialRoomBuilder` — 튜토리얼 방 기초 배치.
- `TutorialStepsSetup` — 튜토리얼 구간을 7단계로 정리하는 메뉴 (막이벽·허수아비 등 방 배치는 건드리지 않음)
- `TutorialTileDataSetup` — 튜토리얼 타일맵을 "데이터 벽"(푸른색 + 데이터 비 + 격자 맥박)으로 바꾸는 메뉴
- `TutorialZoneEditor` — 튜토리얼 영역(TutorialZone)을 씬 창에서 보고 고치기
- `WeaponSwapSetup` — 무기 교체(Tab) 목록 채우기 — 에디터 전용.
- `WebGLDocsBuilder` — 웹(WebGL) 빌드를 깃허브 페이지용 docs 폴더(Team_Hope/docs)에 한 번에 만드는 메뉴.
- `WebGLTextureOptimizer` — 웹(WebGL) 빌드용 텍스처 정리 도구.

**Editor/BookshelfPlacer/**
- `JiwonBookshelfPlacer` — (설명 없음)

**Editor/EventHorizonTools/**
- `BossCutsceneSceneCreator` — 1스테이지(금서고) 보스 처치 연출(boss_disposaleffect) 확인용 씬을 자동으로 만들어주는 도구.
- `CutsceneStepDrawer` — CutsceneStep의 타입(줌/포커스/대사 등)에 따라 관련 필드만 보여주는 인스펙터 커스텀 드로어.
- `FontSwapGoshin` — BoldGFCRedSpiritSDF 글꼴을 고신고딕 Bold(GoshinGothic-Bold) 로 바꾼다. 유니티가 켜질 때 한 번 자동 실행.
- `KoreanFontAssetMaker` — Etc/Font 폴더의 한글 글꼴 파일(.otf/.ttf)로 TextMeshPro 글꼴 에셋(SDF)을 만들어 준다.
- `StaticFontToDynamic` — 한글 2,488자를 4096 글자판에 미리 구워 둔 "고정(Static)" 글꼴 에셋을 "동적(Dynamic)" 으로 바꾼다.
- `TileAtlasMaker` — 1스테이지(금서고) 타일 그림을 스프라이트 아틀라스로 묶어 "타일 사이 틈(옆 타일이 1프레임씩 비치는 선)" 을 없앤다.
- `UIGradientSpriteCreator` — 왼쪽은 투명, 오른쪽으로 갈수록 진해지는 그라데이션 이미지를 만들어 프로젝트에 저장한다.
