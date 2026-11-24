using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-24] 보스방에 나가는 문(포탈)을 붙인다. 유니티가 켜질 때 문이 없으면 자동으로 한 번 실행.
///
/// 왜: 1스테이지 보스방(Room_BossTest)에 문이 없어서, 보스가 '마지막 칸'일 때만(게임 클리어) 진행이 됐다.
///     2스테이지 칸들을 켠 뒤로는 1스테이지 보스가 마지막이 아니라서, 보스를 잡아도 다음(2스테이지 로비)으로 갈 길이 없었다.
///     2스테이지 보스방(Room2S_Boss)도 같은 방을 복사해 만들어 같은 문제가 있었다.
/// 하는 일: 각 보스방 프리팹에 door 프리팹을 넣고, 방 스크립트(Room)의 portal 칸에 연결한다.
///   문은 보스 처치 전엔 닫혀 있고, 보스가 쓰러지면 열린다 (다른 전투방과 같은 방식). 문을 지나면 다음 칸으로 진행.
/// 메뉴: EventHorizon → 보스방 문 달기
/// </summary>
[InitializeOnLoad]
public static class BossRoomExitSetup
{
    static readonly string[] Rooms = { "Assets/_Test/Room/Room_BossTest.prefab", "Assets/1.Prefab/Room/2S/Room2S_Boss.prefab" };
    const string DoorPrefab = "Assets/1.Prefab/item/EtcOBJ/door.prefab";

    static BossRoomExitSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            foreach (var p in Rooms) if (NeedsDoor(p)) { Run(); return; }
        };
    }

    static bool NeedsDoor(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var room = go != null ? go.GetComponent<Room>() : null;
        if (room == null) return false;
        return new SerializedObject(room).FindProperty("portal").objectReferenceValue == null;
    }

    [MenuItem("EventHorizon/보스방 문 달기")]
    static void Run()
    {
        var doorPf = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
        if (doorPf == null) { Debug.LogError("[보스방 문] door 프리팹 없음"); return; }
        foreach (var path in Rooms)
        {
            if (!NeedsDoor(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var room = root.GetComponent<Room>();
                var so = new SerializedObject(room);
                Transform spawn = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "PlayerSpawner") spawn = t;
                Tilemap ground = null;
                foreach (var tm in root.GetComponentsInChildren<Tilemap>(true)) if (tm.name == "Tilemap_Grund") ground = tm;
                if (spawn == null || ground == null) { Debug.LogError("[보스방 문] " + path + ": 시작 위치나 땅 타일맵이 없어 건너뜀"); continue; }

                // 시작 위치가 서는 바닥 줄을 찾고, 그 바닥을 따라 오른쪽 벽(또는 낭떠러지) 직전까지 걸어가 두 칸 앞에 문을 놓는다
                var cell = ground.WorldToCell(spawn.position);
                int y = cell.y;
                while (y > cell.y - 40 && !ground.HasTile(new Vector3Int(cell.x, y - 1, 0))) y--;
                int x = cell.x;
                for (int steps = 0; steps < 120; steps++)
                {
                    if (ground.HasTile(new Vector3Int(x + 1, y, 0)) || !ground.HasTile(new Vector3Int(x + 1, y - 1, 0))) break;
                    x++;
                }
                x = Mathf.Max(cell.x + 4, x - 2);
                Vector3 floor = ground.CellToWorld(new Vector3Int(x, y, 0)) + new Vector3(0.5f, 0f, 0f);

                var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPf, root.transform);
                door.name = "door";
                door.transform.position = new Vector3(floor.x, floor.y, 0f);
                float minY = float.MaxValue;
                foreach (var r in door.GetComponentsInChildren<SpriteRenderer>(true)) minY = Mathf.Min(minY, r.bounds.min.y);
                if (minY < float.MaxValue) door.transform.position += Vector3.up * (floor.y - minY);

                var portal = door.GetComponentInChildren<Portal>(true);
                so.FindProperty("portal").objectReferenceValue = portal;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[보스방 문] " + path + " 에 문을 달았습니다" + (portal == null ? " (Portal 컴포넌트를 못 찾음!)" : ""));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
