using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TC = TutorialController.Condition;

/// <summary>
/// [2026-11-18] 튜토리얼 구간을 7단계로 정리하는 메뉴 (막이벽·허수아비 등 방 배치는 건드리지 않음)
/// 1 점프 · 더블 점프  2 대시 · 더블 대시  3 발판 내려가기  4 공격 3회  5 무기 교체  6 스킬 · 퍼즈  7 상자 열기 + 인벤토리
/// - 점프/더블 점프, 대시/더블 대시는 통신창 하나로 같이 설명 (통신창 수 줄이기)
/// - 대시 구간을 통과하면 위층 막이벽 두 개가 같이 사라짐
/// - 바닥에 놓인 스킬 구슬(SkillPickup)은 지움 — 스킬은 6단계 시작 때 바로 넣어 줌
/// - 2단계부터는 시작 영역(Zones/Start_..)을 지나야 그 구간 안내가 뜸 — 영역은 없을 때만 기본 위치에 만들고, 옮겨 둔 건 그대로 둠
/// - 튜토리얼 방 프리팹이 열려 있으면 열린 화면에 바로 적용 (Ctrl+Z 가능, 자동 저장), 아니면 파일을 열어 고친 뒤 저장
/// </summary>
public static class TutorialStepsSetup
{
    const string RoomPath = "Assets/1.Prefab/Room/Tutorial/TutorialRoom.prefab";
    const string GrantSkillPath = "Assets/1.Prefab/Skill/skill03.prefab";
    const float LowerY = -10f;   // 이보다 아래에 있으면 아래층
    const float UpperFloor = 0.86f, LowerFloor = -22.14f;   // 시작 영역 기본 높이 기준
    const float StartZoneW = 2f, StartZoneH = 8f;

    [MenuItem("EventHorizon/튜토리얼 구간 정리")]
    public static void Run()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool inStage = stage != null && stage.assetPath == RoomPath;
        GameObject root = inStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            TutorialController ctrl = root.GetComponentInChildren<TutorialController>(true);
            if (ctrl == null) { Debug.LogError("[TutorialSteps] TutorialController 없음"); return; }
            if (inStage) Undo.RecordObject(ctrl, "튜토리얼 구간 정리");

            List<TutorialController.Step> old = ctrl.steps ?? new List<TutorialController.Step>();
            TutorialController.Step Find(params TC[] cs)
                => old.FirstOrDefault(x => cs.Contains(x.condition)) ?? new TutorialController.Step();

            // 막이벽 (x 좌표 순) — 위층: 대시 · 더블 대시 / 아래층: 공격 · 스킬
            Transform gates = ctrl.transform.Find("Gates");
            List<Transform> all = gates == null ? new List<Transform>() : gates.Cast<Transform>().OrderBy(t => t.position.x).ToList();
            List<GameObject> upper = all.Where(t => t.position.y >= LowerY).Select(t => t.gameObject).ToList();
            List<GameObject> lower = all.Where(t => t.position.y < LowerY).Select(t => t.gameObject).ToList();
            GameObject At(List<GameObject> l, int i) => i < l.Count ? l[i] : null;

            TutorialController.Step jump = Find(TC.Jump, TC.DoubleJump);
            TutorialController.Step dash = Find(TC.Dash, TC.DoubleDash);
            TutorialController.Step drop = Find(TC.Reach);
            TutorialController.Step attack = Find(TC.DummyHits, TC.DummyDamage);
            TutorialController.Step swap = Find(TC.WeaponSwap);
            TutorialController.Step skill = Find(TC.SkillAndPause, TC.AnySkill, TC.WeaponSkill);
            TutorialController.Step box = Find(TC.BoxAndInventory);
            Transform lowerCp = attack.checkpoint ?? skill.checkpoint ?? box.checkpoint;

            foreach (var st in new[] { jump, dash, drop, attack, swap, skill, box })
            {
                st.gate = null;
                st.moreGates = new List<GameObject>();
                st.grantSkill = null;
                st.fillPauseGauge = false;
                if (st != drop) st.reachZone = null;
                if (st != attack) st.dummy = null;
                if (st != box) st.box = null;
            }

            // 1 점프 · 더블 점프
            jump.title = "점프 · 더블 점프";
            jump.guide = "들려? 좋아, 연결됐네. ← → 로 걷고 {Jump} 로 뛰어. 벽이 높으면 공중에서 {Jump} 한 번 더.";
            jump.condition = TC.DoubleJump;

            // 2 대시 · 더블 대시 (위층 막이벽 둘 다 사라짐)
            dash.title = "대시 · 더블 대시";
            dash.guide = "{Dash} 누르면 확 튀어 나가. 튀어 나가는 동안엔 적 공격도 안 맞아. 구덩이가 넓으면 {Dash} 두 번 이어서!";
            dash.condition = TC.DoubleDash;
            dash.gate = At(upper, 0);
            if (At(upper, 1) != null) dash.moreGates.Add(At(upper, 1));

            // 3 발판 내려가기 (문구 그대로)

            // 4 공격 3회
            attack.title = "공격";
            attack.guide = "{Attack} 로 저 허수아비 세 번만 두들겨 봐. 아프지 않으니까 마음껏.";
            attack.condition = TC.DummyHits;
            attack.hitsNeeded = 3;
            attack.gate = At(lower, 0);
            if (attack.dummy == null) attack.dummy = ctrl.GetComponentsInChildren<TrainingDummy>(true)
                .OrderBy(d => d.transform.position.x).Select(d => d.GetComponent<Stats>()).FirstOrDefault();

            // 5 무기 교체
            swap.title = "무기 교체";
            swap.guide = "무기가 하나뿐인 줄 알았지? {WeaponSwap} 로 바꿔 봐. 검, 건틀릿, 망치.";
            swap.condition = TC.WeaponSwap;
            if (swap.checkpoint == null) swap.checkpoint = lowerCp;

            // 6 스킬 · 퍼즈 (스킬 1회 → 퍼즈 안내로 바뀜 → 퍼즈 1회 · 스킬은 구간 시작 때, 퍼즈 게이지는 스킬 성공 때 넣어 줌) — 아래층 두 번째 막이벽
            skill.title = "스킬 · 퍼즈";
            skill.guide = "스킬 하나 넣어 뒀어. {Skill2} 로 써 봐.";
            skill.guide2 = "좋아. …어라, 전투용 안드로이드도 아닌데 이런 기능까지 달려 있네? 게이지 채워 놨으니까 {Pause} 눌러 봐. 세상이 멈출 거야.";   // 스킬 성공 뒤
            skill.condition = TC.SkillAndPause;
            skill.grantSkill = AssetDatabase.LoadAssetAtPath<Skill>(GrantSkillPath);
            skill.fillPauseGauge = true;
            skill.gate = At(lower, 1);
            if (skill.checkpoint == null) skill.checkpoint = lowerCp;

            // 7 아이템 획득 · 인벤토리 확인
            if (box.box == null) box.box = ctrl.GetComponentsInChildren<Box>(true).FirstOrDefault();
            if (box.checkpoint == null) box.checkpoint = lowerCp;

            ctrl.steps = new List<TutorialController.Step> { jump, dash, drop, attack, swap, skill, box };

            // ── [2026-11-18] 시작 영역: 지나가야 그 구간 안내 통신창이 뜸
            // 이미 있으면 (직접 옮겨 둔 위치 그대로) 다시 연결만 하고, 없을 때만 기본 위치에 만든다
            Transform zones = ctrl.transform.Find("Zones");
            if (zones == null)
            {
                zones = new GameObject("Zones").transform;
                zones.SetParent(ctrl.transform, false);
                if (inStage) Undo.RegisterCreatedObjectUndo(zones.gameObject, "튜토리얼 시작 영역");
            }
            int made = 0;
            TutorialZone StartZone(string name, float x, float floor)
            {
                Transform ex = zones.Find(name);
                if (ex != null && ex.GetComponent<TutorialZone>() != null) return ex.GetComponent<TutorialZone>();
                var go = new GameObject(name);
                go.transform.SetParent(zones, false);
                go.transform.position = new Vector3(x, floor + StartZoneH * 0.5f, 0f);
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(StartZoneW, StartZoneH);
                var z = go.AddComponent<TutorialZone>();
                z.kind = TutorialZone.Kind.Reach;
                z.controller = ctrl;
                if (inStage) Undo.RegisterCreatedObjectUndo(go, "튜토리얼 시작 영역");
                made++;
                return z;
            }
            float X(Object o, float fallback) => o is Component c ? c.transform.position.x : o is GameObject g ? g.transform.position.x : fallback;
            var dummies = ctrl.GetComponentsInChildren<TrainingDummy>(true).OrderBy(d => d.transform.position.x).ToList();
            float dummy2X = dummies.Count > 1 ? dummies[1].transform.position.x : 101f;

            jump.startZone = null;                                                                   // 1 시작하자마자
            dash.startZone = StartZone("Start_02_Dash", 28.5f, UpperFloor);                          // 2 높은 벽 넘은 뒤
            drop.startZone = StartZone("Start_03_Drop", X(dash.gate, 60.5f) + 2.5f, UpperFloor);     // 3 대시 막이벽 지난 뒤
            attack.startZone = StartZone("Start_04_Attack", X(attack.dummy, 84f) - 5f, LowerFloor);  // 4 첫 허수아비 앞
            swap.startZone = StartZone("Start_05_Swap", X(attack.gate, 91f) + 2.5f, LowerFloor);     // 5 공격 막이벽 지난 뒤
            skill.startZone = StartZone("Start_06_Skill", dummy2X - 3f, LowerFloor);                 // 6 두 번째 허수아비 앞
            box.startZone = StartZone("Start_08_Box", X(skill.gate, 110f) + 2.5f, LowerFloor);       // 7 스킬·퍼즈 막이벽 지난 뒤
            Transform unusedPause = zones.Find("Start_07_Pause");
            if (unusedPause != null) Debug.LogWarning("[TutorialSteps] 'Start_07_Pause' 영역은 이제 쓰지 않음 (스킬·퍼즈 합침) — 필요 없으면 직접 지워 주세요");

            // 바닥에 놓인 스킬 구슬 삭제
            int removed = 0;
            foreach (SkillPickup sp in root.GetComponentsInChildren<SkillPickup>(true))
            {
                GameObject go = PrefabUtility.GetOutermostPrefabInstanceRoot(sp.gameObject) ?? sp.gameObject;
                if (go == null) continue;
                if (inStage) Undo.DestroyObjectImmediate(go); else Object.DestroyImmediate(go);
                removed++;
            }

            for (int i = 2; i < lower.Count; i++) Debug.LogWarning($"[TutorialSteps] '{lower[i].name}' 는 어느 구간에도 연결되지 않음");
            for (int i = 2; i < upper.Count; i++) Debug.LogWarning($"[TutorialSteps] '{upper[i].name}' 는 어느 구간에도 연결되지 않음");

            if (inStage)
            {
                EditorUtility.SetDirty(ctrl);
                EditorSceneManager.MarkSceneDirty(stage.scene);
            }
            else PrefabUtility.SaveAsPrefabAsset(root, RoomPath);

            Debug.Log($"[TutorialSteps] 7구간 정리 완료 — 대시 막이벽 {upper.Count}개 · 공격 {(attack.gate ? attack.gate.name : "없음")} · 스킬·퍼즈 {(skill.gate ? skill.gate.name : "없음")} · 스킬 구슬 {removed}개 삭제 · 시작 영역 {made}개 새로 만듦");
        }
        finally { if (!inStage) PrefabUtility.UnloadPrefabContents(root); }
    }
}
