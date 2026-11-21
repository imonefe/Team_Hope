using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 상점 방(ShopRoom_2S)에 상점 기능을 연결하는 에디터 도구.
///
/// 메뉴: EventHorizon → 상점 방에 상점 기능 연결
///   1) 능력치 강화 화면 프리팹(StatUpgradeCanvas)이 없으면 만든다
///   2) 상점 화면 프리팹 3개에 화면 스크립트를 붙이고 진열 후보를 채운다
///      - ItemShopCanvas  ← ItemShopWindow  (1.Prefab/item 안의 아이템 프리팹들이 쓰는 아이템)
///      - SkillShopCanvas ← SkillShopWindow (1.Prefab/Skill/Pickup 스킬 줍기 프리팹들이 쓰는 스킬)
///      - StatUpgradeCanvas ← StatUpgradeWindow
///   3) 상점 방 안의 흰 상자 NPC 3개에 F 상호작용(ShopNpcTrigger)을 붙이고, 방 안에 상점 화면 3개를 꺼진 채로 넣어 연결
///      - 위쪽 두 상자 중 왼쪽 → 아이템 상점, 오른쪽 → 스킬 상점 / 가장 아래 상자 → 능력치 강화
///      (상자 위치로 판단하므로 이름이 바뀌어도 된다. 위치를 크게 바꿨다면 다시 실행)
///
/// 여러 번 실행해도 같은 결과 (이미 붙어 있으면 다시 붙이지 않고 값만 갱신). 방 지형·상자 위치는 건드리지 않는다.
/// </summary>
public static class ShopRoomFunctionSetup
{
    const string RoomPath = "Assets/1.Prefab/Room/Shop/ShopRoom_2S.prefab";
    const string CanvasFolder = "Assets/1.Prefab/UI/Canvas/상점";
    const string ItemCanvasPath = CanvasFolder + "/ItemShopCanvas.prefab";
    const string SkillCanvasPath = CanvasFolder + "/SkillShopCanvas.prefab";

    [MenuItem("EventHorizon/상점 방에 상점 기능 연결")]
    public static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasBuilder.StatUpgradePath) == null)
            if (!ShopCanvasBuilder.BuildStatUpgrade()) return;

        // 2) 화면 스크립트
        var items = CollectItems();
        var skills = CollectSkills();
        EditPrefab(ItemCanvasPath, go =>
        {
            var w = Get<ItemShopWindow>(go);
            w.itemPool = items;
            if (w.greetings == null || w.greetings.Length == 0) w.greetings = new[] { "[영복 대사 한 줄]" };
        });
        EditPrefab(SkillCanvasPath, go =>
        {
            var w = Get<SkillShopWindow>(go);
            w.skillPool = skills;
        });
        EditPrefab(ShopCanvasBuilder.StatUpgradePath, go =>
        {
            if (go.GetComponent<AugmentUpgradeWindow>() == null) go.AddComponent<AugmentUpgradeWindow>();   // [2026-11-21] 능력치 강화 → 증강 강화
        });

        // 3) 상점 방
        var root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            var boxes = root.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.gameObject.name.StartsWith("ShopNpc"))
                .Select(r => r.transform).ToList();
            if (boxes.Count < 3)
            {
                Debug.LogError($"[상점 기능 연결] 상점 방에서 NPC 상자(ShopNpc...)를 3개 찾지 못함 (찾은 수 {boxes.Count})");
                return;
            }
            var bottom = boxes.OrderBy(b => b.position.y).First();
            var tops = boxes.Where(b => b != bottom).OrderByDescending(b => b.position.y).Take(2).OrderBy(b => b.position.x).ToList();

            var itemWin = EnsureCanvas(root.transform, ItemCanvasPath, "ItemShopCanvas").GetComponent<ShopWindow>();
            var skillWin = EnsureCanvas(root.transform, SkillCanvasPath, "SkillShopCanvas").GetComponent<ShopWindow>();
            var statWin = EnsureCanvas(root.transform, ShopCanvasBuilder.StatUpgradePath, "StatUpgradeCanvas").GetComponent<ShopWindow>();

            SetupNpc(tops[0], itemWin, "아이템 상점", "ShopNpc_Item");
            SetupNpc(tops[1], skillWin, "스킬 상점", "ShopNpc_Skill");
            SetupNpc(bottom, statWin, "능력치 강화", "ShopNpc_StatUpgrade");

            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log($"[상점 기능 연결] 완료 — 아이템 후보 {items.Count}개, 스킬 후보 {skills.Count}개");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    static void SetupNpc(Transform box, ShopWindow win, string label, string newName)
    {
        box.name = newName;
        var col = Get<BoxCollider2D>(box.gameObject);
        col.isTrigger = true;
        col.size = new Vector2(2.4f, 1.2f);   // 상자(1.5 x 2.5) 기준 → 실제 범위 약 가로 3.6 x 세로 3칸
        var trg = Get<ShopNpcTrigger>(box.gameObject);
        trg.window = win;
        trg.promptLabel = label;
    }

    // 이미 있으면 그것을, 없으면 새로 붙여서 돌려준다 (유니티 오브젝트는 ?? 로 비교하면 안 됨)
    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject EnsureCanvas(Transform room, string prefabPath, string name)
    {
        var existing = room.Find(name);
        if (existing != null) { existing.gameObject.SetActive(false); return existing.gameObject; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
        go.name = name;
        go.SetActive(false);   // NPC에서 F를 누르면 켜진다
        return go;
    }

    static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        var go = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(go);
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(go);
        }
    }

    static List<ItemData> CollectItems()
    {
        var list = new List<ItemData>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/1.Prefab/item" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var it = go != null ? go.GetComponentInChildren<Item>(true) : null;
            if (it != null && it.data != null && !list.Contains(it.data)) list.Add(it.data);
        }
        return list.OrderBy(d => d.tier).ThenBy(d => d.itemName).ToList();
    }

    static List<Skill> CollectSkills()
    {
        var list = new List<Skill>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/1.Prefab/Skill/Pickup" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var sp = go != null ? go.GetComponentInChildren<SkillPickup>(true) : null;
            if (sp != null && sp.skillPrefab != null && !list.Contains(sp.skillPrefab)) list.Add(sp.skillPrefab);
        }
        return list;
    }
}
