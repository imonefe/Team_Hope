using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TC = TutorialController.Condition;

/// <summary>
/// [2026-11-18 추가] 튜토리얼 방 소품·문 다시 배치 (새 타일 배치 기준).
///
/// 타일은 건드리지 않고, Tutorial 아래의 Gates · Checkpoints · Zones · Props 만 새로 만든다.
/// - 막이(Gate): 원래 튜토리얼 푸른 반투명 막이벽(WhiteBox, 높이 16). 통과하면 노이즈와 함께 사라짐 (TutorialController.GlitchOut)
/// - 허수아비: 로비 세트 그대로 = TrainingDummy(윗몸) + hosuabibody(받침대), 로비와 같은 간격·크기
/// - 상자: 로비 상자 소환대(box_sponer)를 받침대로 깔고, 이미 놓아 둔 Box 를 그 위에 올림
///   (소환대는 Room.boxSpawners 에 등록하지 않으므로 상자를 따로 소환하지 않음 — 받침대 역할만)
/// - 구간 11개(지금 프리팹에 있는 것)의 막이·체크포인트·영역·허수아비·상자 연결을 다시 건다. 안내 문구는 그대로.
///
/// 좌표는 2026-11-18 손으로 고친 타일 배치에서 읽은 값 (위층 바닥 0.86 · 아래층 바닥 -22.14).
/// 타일 배치를 또 바꾸면 아래 숫자만 고쳐서 다시 실행하면 된다. 실행 전 프리팹은 bak_1118_tutorialprops 에 백업 권장.
/// </summary>
public static class TutorialPropsRebuild
{
    const string RoomPath = "Assets/1.Prefab/Room/Tutorial/TutorialRoom.prefab";
    const string DummyPath = "Assets/1.Prefab/Room/Lobby/TrainingDummy.prefab";
    const string StandSpritePath = "Assets/4. Art/Character/Enemy/Stage2/hosuabibody.png";
    const string WallSpritePath = "Assets/1.Prefab/Room/Shop/WhiteBox.png";   // 원래 막이벽 그림
    static readonly Color WallColor = new Color(0.6f, 0.85f, 1f, 0.55f);     // 원래 막이벽 색
    const string BoxStandPath = "Assets/1.Prefab/item/EtcOBJ/box_sponer.prefab";
    const string GrantSkillPath = "Assets/1.Prefab/Skill/skill03.prefab";   // 스킬 구간 시작 때 넣어 줄 스킬

    // ── 바닥 높이 (발이 닿는 면, 월드)
    const float Upper = 0.86f;    // 위층
    const float Ledge = 2.86f;    // 2칸 턱 위
    const float Lower = -22.14f;  // 아래층

    // ── 로비 세트 간격 (로비 프리팹에서 잰 값: 발판 높이 기준)
    const float DummyRootUp = 1.09f;      // 허수아비 윗몸 루트 = 발판 + 1.09
    const float StandUp = 2.61f;          // 받침대 그림 가운데 = 발판 + 2.61
    const float StandDx = 0.02f;
    const float StandScale = 2.127614f;
    const float GateHeight = 16f;         // 막이벽 높이 (원래 튜토리얼 값)
    const float BoxOnStand = 0.874f - 0.12f;   // 소환대 상자 자리(0.5,1.9 × 0.46) - 상자 발 위치 보정
    const float BoxStandDx = 0.23f;

    [MenuItem("EventHorizon/튜토리얼 방 소품·문 다시 배치")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            TutorialController ctrl = root.GetComponentInChildren<TutorialController>(true);
            if (ctrl == null) { Debug.LogError("[TutorialProps] TutorialController 없음"); return; }
            // [2026-11-18] 8구간으로 정리: 점프 · 더블 점프 · 대시 · 더블 대시 · 발판 내려가기 · 공격 3회 · 스킬 1회 · 상자·인벤토리
            // 조건 종류로 기존 구간을 찾아 안내 문구를 살리고, 무기 교체·퍼즈 구간은 뺀다 (몇 번 돌려도 같은 결과)
            {
                if (ctrl.steps == null) ctrl.steps = new List<TutorialController.Step>();
                TutorialController.Step Find(params TutorialController.Condition[] cs)
                    => ctrl.steps.FirstOrDefault(x => cs.Contains(x.condition)) ?? new TutorialController.Step();
                var order = new List<TutorialController.Step>
                {
                    Find(TC.Jump), Find(TC.DoubleJump), Find(TC.Dash), Find(TC.DoubleDash), Find(TC.Reach),
                    Find(TC.DummyHits, TC.DummyDamage), Find(TC.AnySkill, TC.WeaponSkill), Find(TC.BoxAndInventory),
                };
                ctrl.steps = order;
            }
            Transform tut = ctrl.transform;
            int groundLayer = LayerMask.NameToLayer("ground");

            // 이미 놓아 둔 상자는 살린다
            Box keepBox = tut.GetComponentsInChildren<Box>(true).FirstOrDefault();
            if (keepBox != null) keepBox.transform.SetParent(tut, true);

            foreach (string n in new[] { "Gates", "Checkpoints", "Zones", "Props" })
            {
                Transform old = tut.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Transform gates = Child(tut, "Gates"), points = Child(tut, "Checkpoints"), zones = Child(tut, "Zones"), props = Child(tut, "Props");

            Sprite wall = AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePath);
            Sprite stand = AssetDatabase.LoadAssetAtPath<Sprite>(StandSpritePath);
            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);

            // [2026-11-18 수정] 관문 = 원래 쓰던 푸른 반투명 막이벽 (문 그림 아님)
            GameObject Gate(string name, float x, float floor)
            {
                var g = new GameObject(name);
                g.transform.SetParent(gates, false);
                g.layer = groundLayer;
                g.transform.position = new Vector3(x, floor + GateHeight * 0.5f, 0.12912512f);
                g.transform.localScale = new Vector3(1f, GateHeight, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = wall;
                sr.color = WallColor;
                sr.sortingOrder = 4;
                g.AddComponent<BoxCollider2D>().size = Vector2.one;
                return g;
            }
            Transform Point(string name, float x, float floor)
            {
                var t = new GameObject(name).transform;
                t.SetParent(points, false);
                t.position = new Vector3(x, floor + 1f, 0f);
                return t;
            }
            TutorialZone Zone(string name, TutorialZone.Kind kind, float cx, float cy, float w, float h)
            {
                var go = new GameObject(name);
                go.transform.SetParent(zones, false);
                go.transform.position = new Vector3(cx, cy, 0f);
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(w, h);
                var z = go.AddComponent<TutorialZone>();
                z.kind = kind;
                z.controller = ctrl;
                return z;
            }
            Stats Dummy(string name, float x, float floor)
            {
                var set = new GameObject(name).transform;       // 로비처럼 받침대 + 윗몸 한 묶음
                set.SetParent(props, false);
                set.position = new Vector3(x, floor, 0f);
                var st = new GameObject("DummyStand");
                st.transform.SetParent(set, false);
                st.transform.position = new Vector3(x + StandDx, floor + StandUp, 1f);
                st.transform.localScale = Vector3.one * StandScale;
                st.AddComponent<SpriteRenderer>().sprite = stand;
                if (dummyPrefab == null) return null;
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, set);
                d.transform.position = new Vector3(x, floor + DummyRootUp, -3f); // 로비와 같은 z (받침대보다 앞)
                return d.GetComponent<Stats>();
            }
            // [2026-11-18] 스킬 구슬을 바닥에 놓던 기능 삭제

            // ── 위층
            Transform cp1 = Point("Checkpoint_01", 2.85f, Upper);
            Transform cp2 = Point("Checkpoint_02", 15f, Ledge);
            Transform cp3 = Point("Checkpoint_03", 30f, Upper);
            Transform cp4 = Point("Checkpoint_04", 38.5f, Upper);
            Transform cp5 = Point("Checkpoint_05", 55f, Upper);
            Transform cpL = Point("Checkpoint_Lower", 68f, Lower);
            GameObject gDash = Gate("Gate_03_Dash", 33.6f, Upper);          // 2칸 구덩이 앞
            GameObject gDouble = Gate("Gate_04_DoubleDash", 48.6f, Upper);  // 4칸 구덩이 앞
            Zone("Respawn_PitA", TutorialZone.Kind.Respawn, 35.35f, -10f, 2.2f, 6f);
            Zone("Respawn_PitB", TutorialZone.Kind.Respawn, 51.35f, -10f, 4.2f, 6f);
            Zone("Respawn_Bottom", TutorialZone.Kind.Respawn, 70f, -45f, 260f, 10f);   // 아래층보다 한참 아래 (만일 대비)
            TutorialZone reach = Zone("Reach_Drop", TutorialZone.Kind.Reach, 70.85f, -20f, 13f, 4f);   // 발판 아래 통로 바닥

            // ── 아래층: 허수아비(공격) → 막이벽 → 허수아비 2개(스킬 과녁) → 막이벽 → 상자
            Stats dummy1 = Dummy("DummySet_Attack", 84f, Lower);
            GameObject gAttack = Gate("Gate_06_Attack", 91f, Lower);
            Dummy("DummySet_Skill_1", 101f, Lower);
            Dummy("DummySet_Skill_2", 116f, Lower);
            GameObject gSkill = Gate("Gate_07_Skill", 122f, Lower);

            // 상자 + 받침대(상자 소환대)
            float boxX = keepBox != null ? keepBox.transform.position.x : 131.6f;
            var standPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxStandPath);
            if (standPrefab != null)
            {
                var bs = (GameObject)PrefabUtility.InstantiatePrefab(standPrefab, props);
                bs.name = "BoxStand";
                bs.transform.position = new Vector3(boxX - BoxStandDx, Lower, 0f);
            }
            if (keepBox != null)
            {
                keepBox.transform.SetParent(props, true);
                keepBox.transform.position = new Vector3(boxX, Lower + BoxOnStand, keepBox.transform.position.z);
            }

            // ── 구간 연결 (안내 문구·조건은 그대로)
            var s = ctrl.steps;
            Set(s[0], null, cp1);                 // 이동·점프
            Set(s[1], null, cp2);                 // 더블 점프 (높은 벽 자체가 막이)
            Set(s[2], gDash, cp3);                // 대시
            Set(s[3], gDouble, cp4);              // 더블 대시
            Set(s[4], null, cp5); s[4].reachZone = reach;   // 발판 내려가기
            Set(s[5], gAttack, cpL); s[5].dummy = dummy1;   // 공격 3회
            s[5].title = "\uACF5\uACA9";
            s[5].guide = "{Attack} \uB85C \uC800 \uD5C8\uC218\uC544\uBE44 \uC138 \uBC88\uB9CC \uB450\uB4E4\uACA8 \uBD10. \uC544\uD30C\uD558\uC9C0 \uC54A\uC73C\uB2C8\uAE4C \uB9C8\uC74C\uAECF.";
            s[5].condition = TutorialController.Condition.DummyHits;
            s[5].hitsNeeded = 3;
            Set(s[6], gSkill, cpL);                // 스킬 1회 (구간 시작 때 스킬을 바로 넣어 줌)
            s[6].title = "\uC2A4\uD0AC";
            s[6].guide = "\uB0B4\uAC00 \uC2A4\uD0AC \uD558\uB098 \uB123\uC5B4 \uB410\uC5B4. {Skill2} \uB20C\uB7EC\uC11C \uC368 \uBD10.";
            s[6].condition = TutorialController.Condition.AnySkill;
            s[6].grantSkill = AssetDatabase.LoadAssetAtPath<Skill>(GrantSkillPath);
            Set(s[7], null, cpL); s[7].box = keepBox;    // 아이템 획득 · 인벤토리 확인

            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log("[TutorialProps] 튜토리얼 방 소품·문 다시 배치 완료");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Set(TutorialController.Step st, GameObject gate, Transform cp)
    {
        st.gate = gate;
        st.checkpoint = cp;
        st.reachZone = null;
        st.dummy = null;
        st.box = null;
        st.fillPauseGauge = false;
        st.grantSkill = null;
    }

    static Transform Child(Transform p, string n)
    {
        var t = new GameObject(n).transform;
        t.SetParent(p, false);
        return t;
    }
}
