using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-16 추가] 1스테이지(금서고) 상점 방 프리팹 생성기 (에디터 전용).
///
/// 메뉴: EventHorizon → 상점 방 프리팹 만들기 (1스테이지(금서고))
///   1.Prefab/Room/Shop/ShopRoom_1S.prefab 을 만든다.
///
/// 만드는 방법
///   - 1스테이지(금서고) 전투 방(GradRoom_01)을 복사해서 시작한다 → 배경(3D 기둥·조명·볼륨), 문(포탈), 방 스크립트 설정을 그대로 물려받는다.
///   - 몬스터 스포너·스폰 트리거·상자 생성기·함정을 지운다. 배경 책장은 원래 자리에 그대로 둔다.
///   - 땅/발판 타일맵을 비우고 1스테이지(금서고) 타일로 작은 상점 방을 새로 칠한다.
///     (어떤 타일이 윗면·모서리·벽인지는 기존 1스테이지(금서고) 방 5개에서 쓰인 방식을 그대로 따름)
///   - 오른쪽 단상 위에 상점 인물 자리(NPC), 왼쪽에 시작 위치, 오른쪽 끝에 문.
///   - [2026-11-16 수정] 조명·볼륨·3D 오브젝트(배경 책장·돌 배경 등)를 전부 지운다 → 타일맵만 남은 회색 상자 방.
///     상점 인물 자리는 흰색 상자(ShopNpc)로 표시한다. 그림·대화 연결은 나중에 이 상자를 바꿔 끼우면 된다.
///
/// 만든 뒤에는 일반 프리팹이므로 에디터에서 직접 고치면 된다. 이 생성기를 다시 돌리면 덮어쓴다.
/// 상점 열기(구매 화면 띄우기) 연결은 하지 않는다 — 상점 인물(ShopNpc)에 대화/상점 연결은 따로.
/// </summary>
public static class ShopRoomBuilder
{
    const string SourceRoom = "Assets/1.Prefab/Room/1S/GradRoom_01.prefab";
    const string NpcPrefab = "Assets/1.Prefab/Room/Lobby/NPC/NPC_Idle.prefab";
    const string TileFolder = "Assets/4. Art/Environment/Stage1/Tiles/";
    const string OutFolderParent = "Assets/1.Prefab/Room";
    const string OutFolder = OutFolderParent + "/Shop";
    const string OutPath = OutFolder + "/ShopRoom_1S.prefab";
    const string WhiteBoxPath = OutFolder + "/WhiteBox.png";
    const string LabelFontPath = "Assets/Etc/Font/HiKR-ExtraBold SDF.asset";
    static readonly Vector2 NpcBoxSize = new Vector2(1.5f, 2.5f);   // 흰 상자 크기 (칸 단위)
    static readonly string[] KeepEmpty = { "PlayerSpawner", "endpont" };   // 비어 있어도 지우지 않는 오브젝트

    // ── 방 크기 (칸 단위). 기존 방과 같은 자리(배경 기둥 근처)에 놓이도록 시작 칸을 맞춤 ──
    const int BaseX = 70, BaseY = -36;     // 방 안쪽 왼쪽 아래 칸 (바닥 바로 위)
    const int Width = 48, Height = 16;     // 방 안쪽 빈 공간
    const int Pad = 12;                    // 바깥 벽 두께 (카메라에 바깥이 안 보이게 넉넉히)

    // 단상 (상점 인물이 서는 곳): 바닥에서 한 칸 높음
    const int DaisFrom = 30, DaisTo = 41;
    // 한쪽으로만 통과되는 발판 (아래에서 뛰어 올라갈 수 있음)
    static readonly (int x, int y, int len)[] Platforms = { (7, 5, 6), (17, 8, 6) };

    const int SpawnX = 3;     // 시작 위치
    const int NpcX = 36;      // 상점 인물 (단상 가운데)
    const int DoorX = 45;     // 문
    // 배경 책장은 원래 방 자리 그대로 둔다 (방 뒤 배경을 채움). 옮기고 싶은 책장만 여기 이름·칸을 적는다
    static readonly (string name, int x)[] Shelves = { };

    // 전투용 오브젝트만 지운다 (몬스터 스포너·스폰 트리거·상자 생성기·언덕 장애물)
    static readonly string[] RemovePrefixes = { "2sSp_", "1sSp_", "SpawnTrigger", "box_sponer", "hillobj" };

    [MenuItem("EventHorizon/상점 방 프리팹 만들기 (1스테이지(금서고))")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OutPath) != null &&
            !EditorUtility.DisplayDialog("상점 방 프리팹", "이미 있는 ShopRoom_1S 프리팹을 덮어씁니다. 손으로 고친 내용은 사라집니다.", "덮어쓰기", "취소"))
            return;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoom);
        var npc = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefab);
        if (src == null || npc == null) { Debug.LogError("[상점 방] 원본 방 또는 NPC 프리팹을 찾지 못함"); return; }

        var tiles = new Dictionary<string, TileBase>();
        foreach (var n in new[] { "tile_12", "test02 1_0", "test02 1_1", "test02 1_2", "test02 1_13", "test02 1_16", "test02 1_20",
                                  "test02 1_22", "test02 1_15", "test02 1_18", "test02 1_3", "test02 1_8", "test02 1_23", "test02 1_24" })
        {
            var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileFolder + n + ".asset");
            if (t == null) { Debug.LogError("[상점 방] 타일 없음: " + n); return; }
            tiles[n] = t;
        }

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            room.name = "ShopRoom_1S";

            var ground = Find(room.transform, "Tilemap_Grund").GetComponent<Tilemap>();
            var platform = Find(room.transform, "Tilemap_Platform").GetComponent<Tilemap>();
            var trap = Find(room.transform, "Tilemap_trap")?.GetComponent<Tilemap>();

            // 1) 옮길 오브젝트가 원래 바닥에서 얼마나 떠 있었는지 기억 (지우기 전에)
            var door = Find(room.transform, "door");
            var spawner = Find(room.transform, "PlayerSpawner");
            var keep = new List<(Transform t, int x)>();
            if (door != null) keep.Add((door, DoorX));
            if (spawner != null) keep.Add((spawner, SpawnX));
            foreach (var s in Shelves) { var t = Find(room.transform, s.name); if (t != null) keep.Add((t, s.x)); }
            var lift = new Dictionary<Transform, float>();
            foreach (var k in keep) lift[k.t] = k.t.position.y - FloorTopBelow(ground, k.t.position);

            // 2) 전투용 오브젝트 삭제
            var remove = new List<GameObject>();
            foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
                foreach (var p in RemovePrefixes)
                    if (t != room.transform && t.name.StartsWith(p)) { remove.Add(t.gameObject); break; }
            foreach (var g in remove) if (g != null) Object.DestroyImmediate(g);

            // 2-1) 조명·볼륨·3D 오브젝트 삭제 (타일맵·문·시작 위치·방 스크립트만 남김)
            int removed3D = Strip3DAndLights(room);

            // 방 스크립트의 스포너·상자 목록 비우기 (스포너가 없으면 들어오자마자 문이 열림)
            var roomComp = room.GetComponent<Room>();
            if (roomComp != null)
            {
                var so = new SerializedObject(roomComp);
                so.FindProperty("spawners").arraySize = 0;
                so.FindProperty("boxSpawners").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3) 타일 새로 칠하기
            ground.ClearAllTiles();
            platform.ClearAllTiles();
            if (trap != null) trap.ClearAllTiles();
            PaintGround(ground, tiles);
            foreach (var p in Platforms) PaintPlatform(platform, tiles, BaseX + p.x, BaseY + p.y, p.len);
            ground.CompressBounds();
            platform.CompressBounds();

            // 4) 문·시작 위치·책장을 새 바닥 위로
            foreach (var k in keep)
            {
                int cx = BaseX + k.x;
                float floor = ground.CellToWorld(new Vector3Int(cx, FloorCellTop(cx), 0)).y;
                float worldX = ground.CellToWorld(new Vector3Int(cx, 0, 0)).x + 0.5f;
                k.t.position = new Vector3(worldX, floor + lift[k.t], k.t.position.z);
            }

            // 5) 상점 인물 자리 → 흰색 상자 (위치 확인용)
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(OutFolderParent, "Shop");
            var box = new GameObject("ShopNpc");
            box.transform.SetParent(room.transform, false);
            var sr = box.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureWhiteBox();
            sr.color = Color.white;
            sr.sortingOrder = 10;
            box.transform.localScale = new Vector3(NpcBoxSize.x, NpcBoxSize.y, 1f);
            {
                int cx = BaseX + NpcX;
                float floor = ground.CellToWorld(new Vector3Int(cx, FloorCellTop(cx), 0)).y;
                float worldX = ground.CellToWorld(new Vector3Int(cx, 0, 0)).x + 0.5f;
                box.transform.position = new Vector3(worldX, floor + NpcBoxSize.y * 0.5f, 0f);   // 상자 아래 끝 = 바닥 윗면
            }
            var label = new GameObject("Label");
            label.transform.SetParent(room.transform, false);
            label.transform.position = box.transform.position + Vector3.up * (NpcBoxSize.y * 0.5f + 0.6f);
            label.transform.SetParent(box.transform, true);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath);
            tmp.text = "상점 NPC";
            tmp.fontSize = 4;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            tmp.sortingOrder = 11;

            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(OutFolderParent, "Shop");
            PrefabUtility.SaveAsPrefabAsset(room, OutPath);
            Debug.Log("[상점 방] " + OutPath + " 를 만들었습니다. (삭제: 전투 오브젝트 " + remove.Count + "개, 조명·3D 오브젝트 " + removed3D + "개)");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(OutPath));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // 조명·볼륨·3D 그림(메시)이 붙은 오브젝트를 지우고, 그 뒤에 남은 빈 껍데기도 정리한다
    static int Strip3DAndLights(GameObject room)
    {
        int count = 0;
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == room.transform) continue;
            if (Is3DOrLight(t.gameObject)) { Object.DestroyImmediate(t.gameObject); count++; }
        }
        // 빈 오브젝트(Transform만 있고 자식도 없음) 정리 — 시작 위치처럼 쓰는 빈 오브젝트는 남김
        bool any = true;
        while (any)
        {
            any = false;
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == room.transform || t.childCount > 0) continue;
                if (System.Array.IndexOf(KeepEmpty, t.name) >= 0) continue;
                if (t.GetComponents<Component>().Length > 1) continue;
                Object.DestroyImmediate(t.gameObject); count++; any = true;
            }
        }
        return count;
    }

    static bool Is3DOrLight(GameObject g)
    {
        if (g.GetComponent<MeshRenderer>() || g.GetComponent<SkinnedMeshRenderer>() || g.GetComponent<Light>()) return true;
        foreach (var c in g.GetComponents<Component>())
        {
            if (c == null) continue;
            string n = c.GetType().Name;
            if (n.Contains("Volume") || n.Contains("Light") || n.Contains("Volumetric")) return true;
        }
        return false;
    }

    // 흰색 4x4 그림을 만들어 1칸(=1 유닛) 크기 스프라이트로 저장
    static Sprite EnsureWhiteBox()
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
        if (sp != null) return sp;
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        System.IO.File.WriteAllBytes(WhiteBoxPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(WhiteBoxPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(WhiteBoxPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 4;
        imp.filterMode = FilterMode.Point;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
    }

    // ── 땅 모양 ──
    static bool IsAir(int x, int y)
    {
        int lx = x - BaseX, ly = y - BaseY;
        if (lx < 0 || lx >= Width || ly < 0 || ly >= Height) return false;
        if (ly == 0 && lx >= DaisFrom && lx <= DaisTo) return false;   // 단상
        return true;
    }

    // 그 칸 기둥에서 바닥 윗면 칸(빈 공간의 첫 칸) y
    static int FloorCellTop(int x)
    {
        int y = BaseY;
        while (!IsAir(x, y) && y < BaseY + Height) y++;
        return y;
    }

    static void PaintGround(Tilemap tm, Dictionary<string, TileBase> t)
    {
        for (int x = BaseX - Pad; x < BaseX + Width + Pad; x++)
            for (int y = BaseY - Pad; y < BaseY + Height + Pad; y++)
            {
                if (IsAir(x, y)) continue;
                bool u = IsAir(x, y + 1), d = IsAir(x, y - 1), l = IsAir(x - 1, y), r = IsAir(x + 1, y);
                string n;
                if (u && l) n = "test02 1_13";                    // 윗면 왼쪽 끝
                else if (u && r) n = "test02 1_16";               // 윗면 오른쪽 끝
                else if (u) n = "test02 1_" + (Mathf.Abs(x * 7 + y * 3) % 3);   // 윗면 (3종 섞기)
                else if (d) n = "tile_12";                        // 천장 (기존 방도 채움 타일 사용)
                else if (r) n = "test02 1_20";                    // 왼쪽 벽 (오른쪽이 비어 있음)
                else if (l) n = "test02 1_22";                    // 오른쪽 벽
                else if (IsAir(x + 1, y + 1)) n = "test02 1_18";  // 왼쪽 벽과 바닥이 만나는 안쪽 모서리
                else if (IsAir(x - 1, y + 1)) n = "test02 1_15";  // 오른쪽 벽과 바닥이 만나는 안쪽 모서리
                else n = "tile_12";                               // 속 채움
                tm.SetTile(new Vector3Int(x, y, 0), t[n]);
            }
    }

    static void PaintPlatform(Tilemap tm, Dictionary<string, TileBase> t, int x0, int y, int len)
    {
        for (int i = 0; i < len; i++)
        {
            string n = i == 0 ? "test02 1_3" : i == len - 1 ? "test02 1_8" : "test02 1_" + (i % 3);
            tm.SetTile(new Vector3Int(x0 + i, y, 0), t[n]);
        }
        tm.SetTile(new Vector3Int(x0, y - 1, 0), t["test02 1_23"]);             // 왼쪽 아래 받침
        tm.SetTile(new Vector3Int(x0 + len - 1, y - 1, 0), t["test02 1_24"]);   // 오른쪽 아래 받침
    }

    // 원래 방에서 이 위치 바로 아래 땅 윗면의 월드 y
    static float FloorTopBelow(Tilemap tm, Vector3 world)
    {
        var c = tm.WorldToCell(world);
        for (int y = c.y; y > c.y - 40; y--)
            if (tm.HasTile(new Vector3Int(c.x, y, 0)))
                return tm.CellToWorld(new Vector3Int(c.x, y + 1, 0)).y;
        return world.y;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
