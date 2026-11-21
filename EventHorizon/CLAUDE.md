# Event Horizon — 이 프로젝트에서 일하는 Claude 에게

Unity 2022.3 (URP 2D) 로 만드는 3인 팀 인디 게임. 이 파일은 팀원 누구의 Claude 든 같은 규칙으로 일하도록 적어 둔 것이다.
**답변 · 계획 · 작업 보고 · 코드 주석은 모두 한국어**로 쓴다.

## 일하는 방식

- 코드를 쓰거나 고치기 전에 무엇을 어떻게 바꿀지(의사코드 · 분석)를 먼저 보여 준다.
- 부탁받지 않은 스크립트는 고치지 않는다. 고쳐야 할 이유가 생기면 고친 뒤 무엇을 왜 고쳤는지 보고한다.
- 에디터로 만들 수 있는 것(프리팹 · 씬 오브젝트 · UI)은 코드로 실행 중에 만들지 말고 프리팹/씬에 넣는다.
- 바꾼 줄에는 `// [YYYY-MM-DD]` 날짜 주석을 단다 (기존 코드의 관례).
- **백업은 프로젝트 안에 두지 않는다.** 바탕화면 `백업` 폴더 등 프로젝트 밖에 둔다.
- 유니티 밖에서 파일을 옮길 때는 반드시 `.meta` 파일을 같이 옮긴다 (GUID 가 유지되어야 연결이 안 끊김).
- 수정이 끝나면 "유니티 창을 한 번 클릭해 다시 불러오기"를 안내한다.

## 폴더 규칙 (2026-11-21 정리)

```
Assets/
  0.Scenes/            씬
  1.Prefab/            프리팹만 (이미지 · 재질을 여기 두지 않음)
  2.script/            스크립트 (기능별 폴더: Augment · Combat · Creature · Effect · Item · Map · Player · Shop · System · UI · skill · Weapon)
  3. Data/             ScriptableObject 데이터 (ItemData · SkillData 등)
  4. Art/
    Materials/<분류>/<세부>/   ← 모든 재질(.mat)
    Textures/<분류>/<세부>/    ← 모든 이미지(.png .jpg .psd .tga)
    Character · Environment · Illustration · Model · Object · VFX · Atlas
                         ← 애니메이션 · 컨트롤러 · 메쉬 · 스파인 · 타일 에셋 등 (재질 · 이미지 제외)
  5. Sound/            소리
  6. Shader/           셰이더 · 셰이더 그래프만 (재질은 4. Art/Materials/Shader)
  Etc/                 외부 패키지 (TextMesh Pro · Spine · ProCamera · Resources) — 손대지 않음
```

- `<분류>` = `Character · Environment · Illustration · Model · Object · UI · VFX · Shader · Misc`, `<세부>` = 그 아래 한 단계 (예: `VFX/Skill`, `UI/HUD`). 두 단계보다 깊게 만들지 않는다.
- 새 재질 · 이미지는 처음부터 위 위치에 만든다. 에디터 도구 스크립트가 저장 경로를 적어 둘 때도 이 규칙을 따른다.
- **예외 — 옮기지 않는 것**
  - 스파인 폴더 (`*.atlas.txt` 가 있는 폴더): 아틀라스가 같은 폴더의 이미지를 이름으로 찾는다.
  - 스프라이트 아틀라스가 폴더째로 묶는 캐릭터 스프라이트 폴더:
    `4. Art/Character/Enemy/Stage2/{Atk, Dps, Spd, ranged01, ranged02, Boss2/Knight}`, `4. Art/Character/Player/Sprites`
    (옮기면 `4. Art/Atlas/*.spriteatlasv2` 묶음에서 빠진다)
  - `Etc/Resources` (코드가 `Resources.Load` 로 이름을 찾음), `Gizmos`, `Editor`
- 2026-11-21 에 옮긴 648개 파일의 원래 위치 목록: 바탕화면 `백업/bak_1121_full/folder_moves.csv`

## 증강 시스템 (어디를 보면 되는지)

- 기획서: Claude Docs "Event Horizon 증강 기획서" (전환표 · 연출 기획 · 문제 해결 기록)
- 코드: `2.script/Augment/`
  - `AugmentRuntime.cs` 바탕 + 키워드 + 일반 20종, `AugmentRuntime.Advanced.cs` 희귀 · 영웅 · 전설
  - `CombatEvents.cs` 전투 알림 (기존 스크립트가 한 줄씩 부름, 오류에 안전하게 감싸 둠)
  - `AugmentFx.cs` 연출 부품 (글자 안내 대신 이펙트 · 셰이더 · 화면 표식). 떠오르는 글자는 피해 숫자와 회복량만
  - `EnemyStatus.cs` 적 머리 위 상태 띠 (과열 · 균열 · 처형)
  - `AugmentTestRoom.cs` 시험 방 (개발 중 F8). 1 다음 증강 · Shift+1 강화 · Shift+2 이전 · 2 몬스터 · 3 체력 · 4 방 정리
- 증강 데이터: `3. Data/ItemData/**` — `abilityName` 은 코드가 증강을 구분하는 이름(화면에 안 보임), 화면에는 `itemName` · `abilityText` · `levelValues`
- 용어: 강탈 자원 = '탈취 데이터', 92퍼센트 조각 = '떠올림', 재화 = '증강 파편'
- 연출 위치는 씬의 `AugmentOffer` 오브젝트 인스펙터 '연출 위치 조정'에서 조절

## 하지 말 것

- 예전 메뉴 "EventHorizon/튜토리얼 방 만들기", "튜토리얼 방 소품·문 다시 배치" 를 실행하지 않는다 (지금 튜토리얼 방을 덮어씀).
- 팀원이 작업 중인 씬 · 프리팹을 통째로 다시 만들지 않는다. 필요한 부분만 고친다.
