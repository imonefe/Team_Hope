using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-16 추가] 보스방 프리팹에 코어 3개(발판 위 2 · 바닥 1)와 BossCoreShield를 배치한다.
/// 다시 실행하면 기존 "BossCores"를 지우고 새로 만든다 (위치를 손으로 옮겼다면 다시 실행하지 말 것).
/// </summary>
public static class BossCoreSetup
{
    const string RoomPath = "Assets/_Test/Room/Room_BossTest.prefab";
    const string WhiteBoxPath = "Assets/4. Art/Textures/Environment/Room/WhiteBox.png";

    [MenuItem("EventHorizon/보스방 코어 배치")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            Transform old = root.transform.Find("BossCores");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Sprite box = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
            Vector3[] spots = FindSpots(root);

            GameObject group = new GameObject("BossCores");
            group.transform.SetParent(root.transform, false);

            var kinds = new[] { BossCoreKind.Archer, BossCoreKind.Mage, BossCoreKind.Swordsman };
            var colors = new[] { new Color(0.55f, 0.9f, 0.55f), new Color(0.7f, 0.55f, 1f), new Color(1f, 0.6f, 0.5f) };
            var names = new[] { "Core_Archer", "Core_Mage", "Core_Swordsman" };
            var cores = new BossCore[3];

            for (int i = 0; i < 3; i++)
            {
                GameObject go = new GameObject(names[i]);
                go.transform.SetParent(group.transform, false);
                go.transform.position = spots[i];

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = box;
                sr.sortingOrder = 5;
                sr.color = colors[i];   // 편집 화면에서도 종류 구분
                go.transform.localScale = new Vector3(1.2f, 1.6f, 1f);

                GameObject hb = new GameObject("HitBody");
                hb.layer = LayerMask.NameToLayer("Enemy");
                hb.transform.SetParent(go.transform, false);
                BoxCollider2D col = hb.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = Vector2.one;
                Rigidbody2D rb = hb.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                Stats st = hb.AddComponent<Stats>();
                st.maxHp = 7;
                Health h = hb.AddComponent<Health>();

                BossCore core = go.AddComponent<BossCore>();
                core.kind = kinds[i];
                core.hitBody = h;
                core.body = sr;
                core.activeColor = colors[i];
                cores[i] = core;
            }

            BossCoreShield shield = root.GetComponent<BossCoreShield>();
            if (shield == null) shield = root.AddComponent<BossCoreShield>();
            shield.cores = cores;

            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log($"[BossCoreSetup] 코어 3개 배치: {spots[0]} / {spots[1]} / {spots[2]}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }


    [MenuItem("EventHorizon/보스방 캡처 (확인용)")]
    public static void Capture()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        foreach (var l in inst.GetComponentsInChildren<Light>(true)) l.enabled = false;
        Bounds bb = new Bounds(Vector3.zero, Vector3.zero);
        foreach (Tilemap t in inst.GetComponentsInChildren<Tilemap>(true))
        { var r = t.GetComponent<TilemapRenderer>(); if (r != null) { if (bb.size == Vector3.zero) bb = r.bounds; else bb.Encapsulate(r.bounds); } }
        var camGo = new GameObject("CapCam");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
        Camera cam = camGo.AddComponent<Camera>();
        cam.scene = scene; cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bb.extents.y, bb.extents.x * 9f / 16f) + 1f;
        cam.transform.position = new Vector3(bb.center.x, bb.center.y, -20);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../bak_1116_core/capture_boss.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        Debug.Log("[BossCoreSetup] 캡처 저장: bak_1116_core/capture_boss.png");
    }

    /// <summary>
    /// 바닥 타일맵에서 "위가 비어 있는 타일"(서 있을 수 있는 면)을 모아
    /// 가장 낮은 면 = 바닥(가운데), 그보다 2칸 이상 높은 면 중 왼쪽·오른쪽 끝 쪽 = 발판 2곳.
    /// 순서: [0] 왼쪽 발판, [1] 오른쪽 발판, [2] 바닥
    /// </summary>
    static Vector3[] FindSpots(GameObject root)
    {
        Tilemap tm = null;
        foreach (Tilemap t in root.GetComponentsInChildren<Tilemap>(true))
            if (t.name.Contains("Grund") || t.name.Contains("Ground")) { tm = t; break; }

        var result = new[] { new Vector3(-6, 2, 0), new Vector3(6, 2, 0), new Vector3(0, -2, 0) };
        if (tm == null) { Debug.LogWarning("[BossCoreSetup] 바닥 타일맵을 못 찾아 기본 위치 사용"); return result; }

        tm.CompressBounds();
        BoundsInt b = tm.cellBounds;
        var surfaces = new List<Vector3Int>();
        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax - 1; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (tm.HasTile(c) && !tm.HasTile(c + Vector3Int.up) && !tm.HasTile(c + new Vector3Int(0, 2, 0)))
                    surfaces.Add(c);
            }
        if (surfaces.Count == 0) return result;

        // 바닥: 가운데 기둥 근처에서 가장 낮은 면
        int midX = (b.xMin + b.xMax) / 2;
        Vector3Int floor = surfaces[0];
        int best = int.MaxValue;
        foreach (var s in surfaces)
        {
            int score = Mathf.Abs(s.x - midX) * 2 + (s.y - b.yMin) * 10;
            if (score < best) { best = score; floor = s; }
        }

        // 발판: 바닥보다 2칸 이상 위, 벽 근처(양 끝 3칸) 제외, 왼쪽 절반·오른쪽 절반 각각에서 가장 낮은 면
        Vector3Int? left = null, right = null;
        foreach (var s in surfaces)
        {
            if (s.y < floor.y + 2) continue;
            if (s.x < b.xMin + 3 || s.x > b.xMax - 4) continue;
            if (s.x < midX) { if (left == null || s.y < left.Value.y || (s.y == left.Value.y && s.x > left.Value.x)) left = s; }
            else { if (right == null || s.y < right.Value.y || (s.y == right.Value.y && s.x < right.Value.x)) right = s; }
        }

        Vector3 Up(Vector3Int c) => tm.GetCellCenterWorld(c) + new Vector3(0, tm.cellSize.y * 0.5f + 0.8f, 0);
        result[2] = Up(floor);
        // 발판이 없는 방(현재 Room_BossTest)은 바닥에서 3칸 위 공중 (점프로 닿는 높이)
        result[0] = left != null ? Up(left.Value) : Up(floor) + new Vector3(-6, 3, 0);
        result[1] = right != null ? Up(right.Value) : Up(floor) + new Vector3(6, 3, 0);
        return result;
    }
}
