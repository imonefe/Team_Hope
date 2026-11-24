using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-24] 2스테이지(금서고가 아닌 돌 갱도 · 로봇 스테이지) 방 생성기 (에디터 전용).
///
/// 메뉴: EventHorizon → 2스테이지 방 만들기 (전투 3 · 보스 · 상점)
/// 처음 한 번은 유니티가 켜질 때 자동으로 만든다 (결과물이 없을 때만).
///
/// 왜 필요한가
///   2스테이지 전투방 목록(CombatMap)과 보스방 목록(BossMap)이 가리키던 방 프리팹이 10/17 커밋에서 지워져 비어 있었고,
///   2스테이지 상점은 금서고(1스테이지) 상점 방을 그대로 쓰고 있었다.
///
/// 만드는 것 (1.Prefab/Room/2S/)
///   Room2S_01 폐갱도 입구  — 가로로 긴 방. 계단·기둥을 넘으며 로봇 5종을 처음 만남 (적 7, 3번에 나눠 등장)
///   Room2S_02 수직 갱도    — 위로 올라가는 방. 발판을 지그재그로 오르며 위에서 쏘는 원거리 로봇을 상대 (적 9)
///   Room2S_03 파쇄장 광장  — 가운데 단상과 좌우 높은 발판이 있는 넓은 방. 돌진형이 몰려오는 물량전 (적 11)
///   Room2S_Boss 붕괴 광장  — 좌우·가운데 발판이 있는 보스 방. 로봇 보스(2boss)
///   ShopRoom_2S            — 금서고 상점 방과 같은 구조·기능, 바닥만 2스테이지 타일
///   그리고 CombatMap · BossMap · ShopMap_2S(새로 만듦) · StagePlan(2s_04 상점) 연결.
///
/// 규칙
///   - 타일은 2스테이지 타일(Environment/Stage2)만 쓴다. 금서고 책장·기둥(3D) 배경은 지운다.
///   - 적은 2스테이지 로봇 스포너(Spawner/2s/2sSp_*)만 쓴다.
///   - 방 뼈대(방 스크립트·문(포탈)·시작 위치·조명·스폰 트리거 설정)는 기존 방을 복사해 물려받는다.
///   - 만든 뒤엔 일반 프리팹이므로 에디터에서 직접 고치면 된다. 메뉴로 다시 돌리면 덮어쓴다.
/// </summary>
[InitializeOnLoad]
public static class Stage2RoomBuilder
{
    const string Out = "Assets/1.Prefab/Room/2S/";
    const string CombatSrc = "Assets/1.Prefab/Room/1S/GradRoom_01.prefab";
    const string BossSrc = "Assets/_Test/Room/Room_BossTest.prefab";
    const string ShopSrc = "Assets/1.Prefab/Room/Shop/ShopRoom_1S.prefab";
    const string ShopOut = "Assets/1.Prefab/Room/Shop/ShopRoom_2S.prefab";
    const string TileA = "Assets/4. Art/Environment/Stage2/2Stage Tile/2Stage Tile_";   // 깔끔한 9칸 세트 (0~13)
    const string TileB = "Assets/4. Art/Environment/Stage2/till/2S_tile_";             // 돌무더기 장식 세트 (1~19)
    const string SpawnerDir = "Assets/1.Prefab/Enemy/Spawner/2s/";
    const string BoxPrefab = "Assets/1.Prefab/item/EtcOBJ/box_sponer.prefab";
    const string HealPrefab = "Assets/1.Prefab/item/EtcOBJ/hillobj_1.prefab";
    const string BossPrefab = "Assets/1.Prefab/Enemy/2S/2boss.prefab";
    const string CombatMap = "Assets/3. Data/MapData/2S/CombatMap.asset";
    const string BossMap = "Assets/3. Data/MapData/2S/BossMap.asset";
    const string ShopMap1 = "Assets/3. Data/MapData/1S/ShopMap.asset";
    const string ShopMap2 = "Assets/3. Data/MapData/2S/ShopMap_2S.asset";
    const string StagePlanPath = "Assets/3. Data/MapData/StagePlan.asset";
    const int Pad = 12;   // 바깥 벽 두께 (카메라에 바깥이 안 보이게)
    const string AutoKey = "EH_Stage2RoomBuilder_v2";

    // ───────────── 방 설계 (칸 단위, 왼쪽 아래 = 0,0 = 바닥 바로 위 첫 빈칸) ─────────────
    class Layout
    {
        public string name; public int w, h;
        public List<RectInt> solids = new List<RectInt>();          // 안쪽에 놓는 땅 덩어리
        public List<Vector3Int> plats = new List<Vector3Int>();     // 한 방향 발판 (x, y, 길이)
        public List<(char k, int x, int y)> marks = new List<(char, int, int)>();
        public List<RectInt> waves = new List<RectInt>();           // 스폰 트리거 영역 (밟으면 그 영역 적 등장)
    }
    // 표식: P 시작  D 문  B 보상 상자  H 회복  K 보스  C 보스 카메라 중심
    //       a 공격형  d 방어형  s 돌진형  r 유도탄(원거리1)  R 직사(원거리2)
    static readonly Dictionary<char, string> EnemyOf = new Dictionary<char, string>
    { { 'a', "2sSp_Atk" }, { 'd', "2sSp_Dfs" }, { 's', "2sSp_spd" }, { 'r', "2sSp_1Ran" }, { 'R', "2sSp_2Ran" } };

    // [2026-11-24 v2] 1스테이지 전투방(빈 공간 약 96 x 23~25칸)과 같은 크기 안에서 방식이 다른 방 5개.
    //   모든 전투방: 가로 96 · 세로 24 (1스테이지와 같은 볼륨, 넘지 않음)
    const int RW = 96, RH = 24;
    static void Add(Layout L, params (int, int, int, int)[] r) { foreach (var q in r) L.solids.Add(new RectInt(q.Item1, q.Item2, q.Item3, q.Item4)); }
    static void Plat(Layout L, params (int, int, int)[] p) { foreach (var q in p) L.plats.Add(new Vector3Int(q.Item1, q.Item2, q.Item3)); }
    static void Waves(Layout L, params (int, int, int, int)[] r) { foreach (var q in r) L.waves.Add(new RectInt(q.Item1, q.Item2, q.Item3, q.Item4)); }

    // 1. 폐갱도 통로 — 천장이 낮아졌다 높아지는 갱도. 낮은 구간은 근접전, 트인 구간은 발판 위 원거리
    static Layout Room01()
    {
        var L = new Layout { name = "Room2S_01", w = RW, h = RH };
        Add(L, (0, 14, 30, 10), (56, 12, 20, 12),                       // 낮은 천장 두 구간
               (16, 0, 6, 2), (22, 0, 8, 3), (40, 0, 4, 7), (62, 0, 8, 2));
        Plat(L, (32, 4, 5), (45, 4, 5), (46, 9, 8), (80, 5, 6), (86, 10, 6));
        L.marks.AddRange(new[] { ('P', 3, 0), ('a', 19, 2), ('s', 26, 3), ('r', 41, 7), ('H', 50, 10), ('a', 53, 0), ('s', 58, 0),
                                 ('d', 66, 2), ('s', 74, 0), ('B', 78, 0), ('R', 88, 11), ('D', 93, 0) });
        Waves(L, (8, 0, 24, 14), (32, 0, 30, 24), (62, 0, 34, 24));
        return L;
    }

    // 2. 이층 갱도 — 가운데 바닥판이 방을 위·아래 두 길로 나눔. 아래는 좁고 빠른 길, 위는 넓지만 원거리 로봇이 지킴. 구멍으로 오르내림
    static Layout Room02()
    {
        var L = new Layout { name = "Room2S_02", w = RW, h = RH };
        Add(L, (10, 8, 20, 2), (35, 8, 25, 2), (65, 8, 21, 2));        // 가운데 바닥판 (구멍: 30~34, 60~64)
        Plat(L, (4, 4, 5), (30, 4, 5), (30, 9, 5), (60, 4, 5), (60, 9, 5), (44, 15, 8), (72, 15, 6));   // 구멍마다 발판 2단 (아래 → 위)
        L.marks.AddRange(new[] { ('P', 2, 0), ('s', 20, 0), ('R', 25, 10), ('a', 40, 10), ('a', 45, 0), ('B', 48, 0), ('r', 50, 10),
                                 ('s', 75, 10), ('H', 80, 10), ('d', 70, 0), ('d', 90, 0), ('D', 93, 0) });
        Waves(L, (8, 0, 28, 24), (36, 0, 28, 24), (64, 0, 32, 24));
        return L;
    }

    // 3. 파쇄장 광장 — 가운데 단상 · 좌우 높은 턱 · 3층 발판. 돌진형이 몰려오는 물량전
    static Layout Room03()
    {
        var L = new Layout { name = "Room2S_03", w = RW, h = RH };
        Add(L, (40, 0, 16, 3), (0, 9, 14, 1), (82, 9, 14, 1));
        Plat(L, (18, 5, 8), (70, 5, 8), (42, 10, 12), (30, 15, 6), (60, 15, 6));
        L.marks.AddRange(new[] { ('P', 3, 0), ('H', 5, 10), ('s', 20, 0), ('s', 26, 0), ('d', 34, 0), ('a', 44, 3), ('R', 47, 11),
                                 ('r', 31, 16), ('B', 51, 3), ('r', 62, 16), ('d', 62, 0), ('s', 72, 0), ('s', 78, 0), ('a', 88, 10), ('D', 93, 0) });
        Waves(L, (10, 0, 26, 24), (34, 0, 28, 24), (62, 0, 34, 24));
        return L;
    }

    // 4. 붕괴 계단 — 큰 계단을 올라 꼭대기(천장이 내려앉아 좁음)를 지나 골짜기로 내려갔다가, 높은 출구 턱으로 다시 오름
    static Layout Room04()
    {
        var L = new Layout { name = "Room2S_04", w = RW, h = RH };
        Add(L, (14, 0, 10, 3), (24, 0, 10, 6), (34, 0, 10, 9), (44, 0, 10, 12),   // 오르막
               (54, 0, 10, 6), (64, 0, 8, 3),                                    // 내리막 → 골짜기(72~79)
               (80, 0, 16, 10),                                                  // 출구 턱
               (40, 19, 20, 5));                                                 // 꼭대기 위 내려앉은 천장
        Plat(L, (73, 4, 5), (75, 8, 5));
        L.marks.AddRange(new[] { ('P', 3, 0), ('s', 10, 0), ('a', 18, 3), ('R', 30, 6), ('d', 38, 9), ('a', 48, 12), ('B', 51, 12),
                                 ('r', 58, 6), ('s', 68, 3), ('s', 76, 0), ('H', 77, 9), ('R', 85, 10), ('a', 89, 10), ('D', 93, 10) });
        Waves(L, (8, 0, 26, 24), (34, 0, 30, 24), (64, 0, 32, 24));
        return L;
    }

    // 5. 기둥 숲 — 높이가 다른 기둥 사이를 발판으로 건너는 방. 기둥 위 원거리 · 바닥 돌진형으로 위아래를 동시에 압박
    static Layout Room05()
    {
        var L = new Layout { name = "Room2S_05", w = RW, h = RH };
        Add(L, (14, 0, 3, 5), (24, 0, 3, 9), (34, 0, 3, 6), (46, 0, 3, 11), (58, 0, 3, 7), (68, 0, 3, 10), (80, 0, 3, 6));
        Plat(L, (17, 7, 6), (27, 11, 6), (37, 9, 8), (49, 13, 8), (61, 10, 6), (71, 12, 8),
             (51, 4, 4), (63, 5, 4), (75, 4, 4));   // 바닥에서 높은 기둥으로 오르는 낮은 발판
        L.marks.AddRange(new[] { ('P', 3, 0), ('s', 20, 0), ('r', 25, 9), ('a', 30, 0), ('H', 38, 10), ('d', 41, 0), ('R', 47, 11),
                                 ('B', 50, 14), ('s', 53, 0), ('r', 59, 7), ('s', 64, 0), ('R', 69, 10), ('a', 76, 0), ('d', 86, 0), ('D', 93, 0) });
        Waves(L, (8, 0, 28, 24), (36, 0, 28, 24), (64, 0, 32, 24));
        return L;
    }

    static Layout BossRoom()
    {
        var L = new Layout { name = "Room2S_Boss", w = 26, h = 12 };
        L.solids.Add(new RectInt(3, 4, 5, 1));
        L.solids.Add(new RectInt(18, 4, 5, 1));
        L.solids.Add(new RectInt(10, 7, 6, 1));
        L.marks.AddRange(new[] { ('P', 2, 0), ('K', 20, 0), ('C', 13, 5) });
        return L;
    }

    // ───────────── 메뉴 · 자동 실행 ─────────────
    static Stage2RoomBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AutoKey, false)) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Out + "Room2S_05.prefab") != null) { EditorPrefs.SetBool(AutoKey, true); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorPrefs.SetBool(AutoKey, true);
            BuildCombatRooms();   // v2: 전투방만 다시 (보스방 · 상점은 이미 있고, 보스방엔 문이 달려 있으므로 건드리지 않음)
        };
    }

    [MenuItem("EventHorizon/2스테이지 전투방 다시 만들기 (5개)")]
    public static void Menu()
    {
        if (!EditorUtility.DisplayDialog("2스테이지 방", "2스테이지 전투방 5개를 다시 만듭니다. 손으로 고친 내용은 사라집니다.", "만들기", "취소")) return;
        BuildCombatRooms();
    }

    public static void BuildCombatRooms()
    {
        if (!LoadTiles()) return;
        var made = new List<GameObject>();
        foreach (var L in new[] { Room01(), Room02(), Room03(), Room04(), Room05() }) { var g = BuildCombat(L); if (g != null) made.Add(g); }
        SetRooms(CombatMap, made.ToArray());
        AssetDatabase.SaveAssets();
        DoorStageFix.Run();   // [2026-11-24] 금서고 방을 복사해 만든 문을 철문으로
        Debug.Log("[2스테이지 방] 전투방 " + made.Count + "개를 만들고 CombatMap 에 연결했습니다 (모두 96 x 24칸).");
    }

    public static void BuildAll()
    {
        if (!LoadTiles()) return;
        var made = new List<GameObject>();
        foreach (var L in new[] { Room01(), Room02(), Room03(), Room04(), Room05() }) { var g = BuildCombat(L); if (g != null) made.Add(g); }
        var boss = BuildBoss(BossRoom());
        var shop = BuildShop();

        SetRooms(CombatMap, made.ToArray());
        if (boss != null) SetRooms(BossMap, new[] { boss });
        if (shop != null)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopMap2) == null) AssetDatabase.CopyAsset(ShopMap1, ShopMap2);
            SetRooms(ShopMap2, new[] { shop });
            var plan = AssetDatabase.LoadAssetAtPath<ScriptableObject>(StagePlanPath);
            var map2 = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopMap2);
            if (plan != null && map2 != null)
            {
                var so = new SerializedObject(plan);
                var arr = so.FindProperty("entries");
                for (int i = 0; i < arr.arraySize; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("id").stringValue == "2s_04") e.FindPropertyRelative("roomData").objectReferenceValue = map2;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(plan);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[2스테이지 방] 전투 " + made.Count + "개 · 보스 " + (boss != null ? 1 : 0) + " · 상점 " + (shop != null ? 1 : 0) + " 만들고 맵 데이터에 연결했습니다.");
    }

    static void SetRooms(string mapPath, GameObject[] rooms)
    {
        var map = AssetDatabase.LoadAssetAtPath<ScriptableObject>(mapPath);
        if (map == null) { Debug.LogError("[2스테이지 방] 맵 데이터 없음: " + mapPath); return; }
        var so = new SerializedObject(map);
        var p = so.FindProperty("roomPrefabs");
        p.arraySize = rooms.Length;
        for (int i = 0; i < rooms.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    // ───────────── 전투방 ─────────────
    static GameObject BuildCombat(Layout L)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(CombatSrc);
        if (src == null) { Debug.LogError("[2스테이지 방] 원본 방 없음: " + CombatSrc); return null; }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            room.name = L.name;

            var triggerTemplate = Find(room.transform, "SpawnTrigger_01");
            var door = Find(room.transform, "door");
            var spawn = Find(room.transform, "PlayerSpawner");
            if (triggerTemplate != null) triggerTemplate.SetParent(null, true);   // 지우기 전에 견본으로 빼둠
            StripLibrary(room, door, spawn);

            var ground = Find(room.transform, "Tilemap_Grund").GetComponent<Tilemap>();
            var platform = Find(room.transform, "Tilemap_Platform")?.GetComponent<Tilemap>();
            var trap = Find(room.transform, "Tilemap_trap")?.GetComponent<Tilemap>();
            ground.ClearAllTiles(); if (platform != null) platform.ClearAllTiles(); if (trap != null) trap.ClearAllTiles();
            var back = MakeBack(ground);

            var solid = SolidMask(L);
            PaintSolid(ground, solid, L.w, L.h);
            foreach (var p in L.plats) PaintPlatform(platform != null ? platform : ground, p.x, p.y, p.z);
            PaintBack(back, solid, L.w, L.h);
            ground.CompressBounds(); back.CompressBounds(); if (platform != null) platform.CompressBounds();

            // 스폰 트리거 (견본 복제) — 영역마다 하나
            var triggers = new List<BoxCollider2D>();
            for (int i = 0; i < L.waves.Count; i++)
            {
                var r = L.waves[i];
                GameObject tg = triggerTemplate != null ? Object.Instantiate(triggerTemplate.gameObject) : new GameObject();
                SceneManager_Move(tg, scene);
                tg.name = "SpawnTrigger_" + (i + 1).ToString("00");
                tg.transform.SetParent(room.transform, false);
                Vector3 a = CellWorld(ground, r.x, r.y), b = CellWorld(ground, r.x + r.width, r.y + r.height);
                tg.transform.position = (a + b) * 0.5f;
                tg.transform.localScale = Vector3.one;
                var col = tg.GetComponent<BoxCollider2D>();
                if (col == null) col = tg.AddComponent<BoxCollider2D>();
                col.isTrigger = true; col.offset = Vector2.zero; col.size = new Vector2(b.x - a.x, b.y - a.y);
                triggers.Add(col);
            }
            if (triggerTemplate != null) Object.DestroyImmediate(triggerTemplate.gameObject);

            int enemies = 0;
            foreach (var m in L.marks)
            {
                Vector3 floor = CellWorld(ground, m.x, m.y) + new Vector3(0.5f, 0f, 0f);
                if (EnemyOf.TryGetValue(m.k, out var sp))
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerDir + sp + ".prefab");
                    if (pf == null) { Debug.LogError("[2스테이지 방] 스포너 없음: " + sp); continue; }
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
                    g.transform.SetParent(room.transform, false);
                    g.transform.position = floor + Vector3.up * 1f;
                    var ms = g.GetComponent<MonsterSpawner>();
                    int wi = WaveOf(L, m.x, m.y);
                    if (ms != null && wi >= 0) ms.triggerCollider = triggers[wi];
                    enemies++;
                }
                else if (m.k == 'P' && spawn != null) spawn.position = floor + Vector3.up * 1f;
                else if (m.k == 'D' && door != null) PlaceOnFloor(door.gameObject, floor);
                else if (m.k == 'B') Spawn(BoxPrefab, room, scene, floor);
                else if (m.k == 'H') Spawn(HealPrefab, room, scene, floor);
            }

            var path = Out + L.name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(room, path);
            Debug.Log("[2스테이지 방] " + path + " (적 " + enemies + ", 웨이브 " + L.waves.Count + ")");
            return saved;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // ───────────── 보스방 ─────────────
    static GameObject BuildBoss(Layout L)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(BossSrc);
        var bossPf = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab);
        if (src == null || bossPf == null) { Debug.LogError("[2스테이지 방] 보스방 원본 또는 2boss 없음"); return null; }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            room.name = L.name;
            foreach (var c in room.GetComponents<MonoBehaviour>())
                if (c != null && c.GetType().Name == "BossCoreShield") Object.DestroyImmediate(c);   // 금서고 중간보스 코어 방패
            var cores = Find(room.transform, "BossCores"); if (cores != null) Object.DestroyImmediate(cores.gameObject);
            var spawn = Find(room.transform, "PlayerSpawner");
            var roomComp = room.GetComponent<Room>();
            Transform camCenter = null;
            if (roomComp != null) camCenter = new SerializedObject(roomComp).FindProperty("roomCameraCenter").objectReferenceValue as Transform;
            var spn = Find(room.transform, "spn");
            StripLibrary(room, null, spawn, camCenter, spn);
            // 금서고 보스 등장 연출(보스 포함) 제거
            foreach (var t in room.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null || t == room.transform) continue;
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                {
                    var p = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject));
                    if (p.Contains("IntroCutscene") || p.Contains("/1S/")) Object.DestroyImmediate(t.gameObject);
                }
            }

            var ground = Find(room.transform, "Tilemap_Grund").GetComponent<Tilemap>();
            ground.ClearAllTiles();
            var back = MakeBack(ground);
            var solid = SolidMask(L);
            PaintSolid(ground, solid, L.w, L.h);
            PaintBack(back, solid, L.w, L.h);
            ground.CompressBounds(); back.CompressBounds();

            foreach (var m in L.marks)
            {
                Vector3 floor = CellWorld(ground, m.x, m.y) + new Vector3(0.5f, 0f, 0f);
                if (m.k == 'P' && spawn != null) spawn.position = floor + Vector3.up * 1f;
                else if (m.k == 'K')
                {
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(bossPf, scene);
                    b.transform.SetParent(room.transform, false);
                    PlaceOnFloor(b, floor);
                }
                else if (m.k == 'C')
                {
                    if (camCenter != null) camCenter.position = new Vector3(floor.x, floor.y, camCenter.position.z);
                    if (spn != null) spn.position = new Vector3(floor.x, floor.y, spn.position.z);
                }
            }
            var path = Out + L.name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(room, path);
            Debug.Log("[2스테이지 방] " + path + " (보스: 2boss)");
            return saved;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // ───────────── 상점 (금서고 상점과 같은 모양, 타일만 2스테이지) ─────────────
    static GameObject BuildShop()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(ShopSrc);
        if (src == null) { Debug.LogError("[2스테이지 방] 상점 원본 없음"); return null; }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            room.name = "ShopRoom_2S";
            var ground = Find(room.transform, "Tilemap_Grund")?.GetComponent<Tilemap>();
            if (ground == null) return null;
            foreach (var tm in room.GetComponentsInChildren<Tilemap>(true))
            {
                bool isBack = tm.GetComponent<TilemapCollider2D>() == null && tm.name.ToLower().Contains("bg");
                var cells = new HashSet<Vector2Int>();
                foreach (var p in tm.cellBounds.allPositionsWithin) if (tm.HasTile(p)) cells.Add(new Vector2Int(p.x, p.y));
                if (cells.Count == 0) continue;
                tm.ClearAllTiles();
                foreach (var c in cells)
                {
                    TileBase t;
                    if (isBack) t = BackTile(c.x, c.y);
                    else if (tm.name.Contains("Platform")) t = PlatformTile(c, cells);
                    else t = SolidTile(c.x, c.y, (x, y) => cells.Contains(new Vector2Int(x, y)));
                    tm.SetTile(new Vector3Int(c.x, c.y, 0), t);
                }
                if (isBack) tm.color = BackColor;
                tm.CompressBounds();
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(room, ShopOut);
            Debug.Log("[2스테이지 방] " + ShopOut);
            return saved;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // ───────────── 금서고 배경 · 전투 오브젝트 정리 ─────────────
    static void StripLibrary(GameObject room, Transform door, Transform spawn, params Transform[] keepMore)
    {
        var keep = new HashSet<Transform> { door, spawn };
        foreach (var k in keepMore) if (k != null) keep.Add(k);
        var bg = Find(room.transform, "BG");
        if (bg != null)
        {
            // 저체력 화면 효과(볼륨)는 배경 안에 들어 있어도 살려 둠
            foreach (var v in bg.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("LowHealthVolume")).ToArray())
                v.SetParent(room.transform, true);
            Object.DestroyImmediate(bg.gameObject);
        }
        foreach (var t in room.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t == null || t == room.transform || keep.Contains(t)) continue;
            if (IsUnder(t, keep)) continue;
            // 다른 프리팹 안쪽 조각은 지울 수 없으므로 건드리지 않음 (통째로 지울 땐 바깥 뿌리를 지움)
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
            string n = t.name;
            bool kill = n.StartsWith("SpawnTrigger") || n.StartsWith("1sSp_") || n.StartsWith("2sSp_") || n.StartsWith("box_sponer") || n.StartsWith("hillobj")
                        || n.StartsWith("Bookshelf") || n.StartsWith("DistantPillar") || n.StartsWith("Bridge") || n.StartsWith("wwer");
            // 조명(VolumetricTest)·저체력 볼륨은 남기고, 그 밖의 3D 메시(금서고 기둥·책장)는 지움
            if (!kill && t.GetComponent<MeshRenderer>() != null && t.GetComponentInParent<Light>() == null && !InParentNamed(t, "VolumetricTest")) kill = true;
            if (kill && t != null) Object.DestroyImmediate(t.gameObject);
        }
    }

    static bool IsUnder(Transform t, HashSet<Transform> set) { for (var p = t.parent; p != null; p = p.parent) if (set.Contains(p)) return true; return false; }

    static bool InParentNamed(Transform t, string n) { for (var p = t; p != null; p = p.parent) if (p.name == n) return true; return false; }

    // ───────────── 타일 ─────────────
    static readonly Dictionary<int, TileBase> A = new Dictionary<int, TileBase>();
    static readonly Dictionary<int, TileBase> B = new Dictionary<int, TileBase>();
    static readonly Color BackColor = new Color(0.38f, 0.36f, 0.36f, 1f);

    static bool LoadTiles()
    {
        A.Clear(); B.Clear();
        for (int i = 0; i <= 13; i++) { var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileA + i + ".asset"); if (t == null) { Debug.LogError("[2스테이지 방] 타일 없음: " + TileA + i); return false; } A[i] = t; }
        foreach (int i in new[] { 2, 4, 7, 10, 12, 14, 15, 16, 17, 18, 19 }) { var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileB + i + ".asset"); if (t != null) B[i] = t; }
        return true;
    }

    static int Hash(int x, int y) => Mathf.Abs(x * 73856093 ^ y * 19349663) % 100;

    // 칸 모양에 맞는 타일 (위가 비면 윗면, 옆이 비면 벽, 대각선만 비면 안쪽 모서리)
    static TileBase SolidTile(int x, int y, System.Func<int, int, bool> isSolid)
    {
        bool u = !isSolid(x, y + 1), d = !isSolid(x, y - 1), l = !isSolid(x - 1, y), r = !isSolid(x + 1, y);
        if (u && d && l && r) return A[12];
        if (u && l) return A[0];
        if (u && r) return A[1];
        if (d && l) return A[2];
        if (d && r) return A[3];
        if (u)
        {
            int h = Hash(x, y);
            if (h < 18 && B.ContainsKey(15 + h % 3)) return B[15 + h % 3];   // 돌무더기 윗면 가끔
            if (h < 26 && B.ContainsKey(19)) return B[19];
            if (h < 60 && B.ContainsKey(2)) return B[2];
            return A[5];
        }
        if (d) return A[6];
        if (l) return Hash(x, y) < 20 && B.ContainsKey(10) ? B[10] : A[4];
        if (r) return Hash(x, y) < 20 && B.ContainsKey(14) ? B[14] : A[7];
        if (!isSolid(x - 1, y + 1)) return A[11];
        if (!isSolid(x + 1, y + 1)) return A[10];
        if (!isSolid(x - 1, y - 1)) return A[9];
        if (!isSolid(x + 1, y - 1)) return A[8];
        return A[13];
    }

    static TileBase BackTile(int x, int y)
    {
        int h = Hash(x, y);
        if (h < 6 && B.ContainsKey(12)) return B[12];
        if (h < 10 && B.ContainsKey(18)) return B[18];
        if (h < 40 && B.ContainsKey(4)) return B[4];
        return A[13];
    }

    static TileBase PlatformTile(Vector2Int c, HashSet<Vector2Int> cells)
    {
        bool l = cells.Contains(c + Vector2Int.left), r = cells.Contains(c + Vector2Int.right);
        if (!l && !r) return A[12];
        if (!l) return A[0];
        if (!r) return A[1];
        return A[5];
    }

    static bool[,] SolidMask(Layout L)
    {
        var m = new bool[L.w, L.h];
        foreach (var s in L.solids)
            for (int x = s.xMin; x < s.xMax; x++) for (int y = s.yMin; y < s.yMax; y++)
                if (x >= 0 && x < L.w && y >= 0 && y < L.h) m[x, y] = true;
        return m;
    }

    static bool Solid(bool[,] m, int w, int h, int x, int y) => x < 0 || y < 0 || x >= w || y >= h || m[x, y];

    static void PaintSolid(Tilemap tm, bool[,] m, int w, int h)
    {
        for (int x = -Pad; x < w + Pad; x++)
            for (int y = -Pad; y < h + Pad; y++)
                if (Solid(m, w, h, x, y)) tm.SetTile(new Vector3Int(x, y, 0), SolidTile(x, y, (a, b) => Solid(m, w, h, a, b)));
    }

    static void PaintPlatform(Tilemap tm, int x0, int y, int len)
    {
        for (int i = 0; i < len; i++)
            tm.SetTile(new Vector3Int(x0 + i, y, 0), len == 1 ? A[12] : i == 0 ? A[0] : i == len - 1 ? A[1] : A[5]);
    }

    static void PaintBack(Tilemap tm, bool[,] m, int w, int h)
    {
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (!m[x, y]) tm.SetTile(new Vector3Int(x, y, 0), BackTile(x, y));
    }

    // 뒷벽 타일맵: 땅 타일맵과 같은 정렬 층, 더 뒤 · 어둡게 · 충돌 없음
    static Tilemap MakeBack(Tilemap ground)
    {
        var old = ground.transform.parent.Find("Tilemap_Back");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Tilemap_Back");
        go.transform.SetParent(ground.transform.parent, false);
        go.transform.SetSiblingIndex(0);
        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        var gr = ground.GetComponent<TilemapRenderer>();
        if (gr != null) { tr.sortingLayerID = gr.sortingLayerID; tr.sortingOrder = gr.sortingOrder - 20; tr.sharedMaterial = gr.sharedMaterial; }
        tm.color = BackColor;
        return tm;
    }

    // ───────────── 배치 도우미 ─────────────
    static Vector3 CellWorld(Tilemap tm, int x, int y) => tm.CellToWorld(new Vector3Int(x, y, 0));

    static int WaveOf(Layout L, int x, int y)
    {
        for (int i = 0; i < L.waves.Count; i++) if (L.waves[i].Contains(new Vector2Int(x, y))) return i;
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < L.waves.Count; i++) { float d = Vector2.Distance(L.waves[i].center, new Vector2(x, y)); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    static void Spawn(string path, GameObject room, UnityEngine.SceneManagement.Scene scene, Vector3 floor)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf == null) { Debug.LogError("[2스테이지 방] 프리팹 없음: " + path); return; }
        var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
        g.transform.SetParent(room.transform, false);
        PlaceOnFloor(g, floor);
    }

    // 그림 아래 끝이 바닥 윗면에 닿게 놓기
    static void PlaceOnFloor(GameObject g, Vector3 floor)
    {
        g.transform.position = new Vector3(floor.x, floor.y, g.transform.position.z);
        var rs = g.GetComponentsInChildren<Renderer>(true).Where(r => r is SpriteRenderer || r is MeshRenderer).ToArray();
        if (rs.Length == 0) { g.transform.position += Vector3.up * 0.5f; return; }
        float minY = rs.Min(r => r.bounds.min.y);
        g.transform.position += Vector3.up * (floor.y - minY);
    }

    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene scene)
    {
        if (g.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, scene);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
