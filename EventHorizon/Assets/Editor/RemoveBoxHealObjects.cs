using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-25 추가] 사이버웨어로 바뀌면서 필요 없어진 보상 상자 생성기(box_sponer)와 회복 오브젝트(hillobj_1)를
/// 1스테이지 방 · 로비 · 중간 보스 방에서 지운다. (2스테이지 방은 이미 없음)
///  - Unity 창을 누르면 한 번 자동 실행 (AutoKey), 메뉴로 다시 실행 가능
///  - 방의 Room 컴포넌트 boxSpawners 목록도 비움
/// </summary>
public static class RemoveBoxHealObjects
{
    const string AutoKey = "EH_RemoveBoxHeal_v1";

    static readonly string[] Rooms =
    {
        "Assets/1.Prefab/Room/1S/GradRoom_01.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_02.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_03.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_04.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_05.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_06.prefab",
        "Assets/1.Prefab/Room/1S/GradRoom_07.prefab",
        "Assets/1.Prefab/Room/Lobby/LobbyRoom.prefab",
        "Assets/1.Prefab/Room/MidBoss/mid_boss_s01.prefab",
        "Assets/1.Prefab/Room/MidBoss/mid_boss_s02.prefab",
        "Assets/1.Prefab/Room/MidBoss/mid_boss_s03.prefab",
    };

    static readonly string[] Sources =
    {
        "Assets/1.Prefab/item/EtcOBJ/box_sponer.prefab",
        "Assets/1.Prefab/item/EtcOBJ/hillobj_1.prefab",
    };

    [InitializeOnLoadMethod]
    static void Auto()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Run();
            EditorPrefs.SetBool(AutoKey, true);
        };
    }

    [MenuItem("EventHorizon/방에서 보상 상자 · 회복 오브젝트 지우기")]
    static void Run()
    {
        int total = 0;
        foreach (string path in Rooms)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var kill = new HashSet<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (go == root) continue;
                bool isTarget = go.GetComponent<BoxSpawner>() != null || go.GetComponent<HealObject>() != null;
                if (!isTarget && PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                    isTarget = src != null && Sources.Contains(AssetDatabase.GetAssetPath(src));
                }
                if (!isTarget) continue;
                // 프리팹 안의 자식이면 그 프리팹 통째로 (자식만 지울 수 없음)
                GameObject inst = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                kill.Add(inst != null && inst != root ? inst : go);
            }
            // 바깥 것만 남김 (안쪽이 이미 바깥에 포함되면 중복 삭제 방지)
            var outer = kill.Where(k => !kill.Any(o => o != k && k.transform.IsChildOf(o.transform))).ToList();
            foreach (var room in root.GetComponentsInChildren<Room>(true))
            {
                var so = new SerializedObject(room);
                var p = so.FindProperty("boxSpawners");
                if (p != null && p.isArray) { p.arraySize = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            foreach (var k in outer) Object.DestroyImmediate(k);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            total += outer.Count;
            Debug.Log($"[RemoveBoxHealObjects] {System.IO.Path.GetFileNameWithoutExtension(path)}: {outer.Count}개 지움");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RemoveBoxHealObjects] 끝 — 모두 {total}개");
    }
}
