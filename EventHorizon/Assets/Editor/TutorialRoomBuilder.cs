using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 튜토리얼 방 기초 배치.
/// 상점 방(ShopRoom_2S)의 2스테이지 타일·포탈·방 설정을 그대로 가져와 타일만 새로 칠한다.
/// 구간 11개(기획서 1~12, 패링 제외)를 왼쪽 → 오른쪽으로 놓고, 구간 끝마다 하얀 막이(통과 문)를 세운다.
/// 다시 실행하면 TutorialRoom.prefab을 새로 덮어쓰므로, 손으로 고친 뒤에는 다시 실행하지 말 것.
///
/// [2026-11-18 2차] 방을 짧고 간단하게 다시 짬 (가로 188칸·11구간 → 76칸·7구간).
///   - 빼거나 합친 구간: 이동·점프 + 더블 점프 → 하나(높은 벽), 더블 대시 뺌, 발판 내려가기 뺌, 무기 스킬 + 스킬 → 하나
///   - 공격 → 무기 교체, 스킬 → 퍼즈는 같은 자리에서 이어서 (사이 막이 없음)
///   - 그동안 프리팹에 손으로 넣었던 것도 여기서 함께 만든다:
///     ??? 말투 안내, 끝나면 로비(LobbyRoom)로 이동, 예전 위쪽 글자 판·출구 문 끄기,
///     만든 뒤 "튜토리얼 배경 배치"(TutorialBackgroundSetup)를 이어서 실행해 시뮬레이션 방 배경을 깖
/// 칸 단위 숫자(높이·구덩이 폭)는 플레이해 보고 조정이 필요하다 (점프력 19 · 중력 4 · 대시 12×0.2초 기준 추정).
/// </summary>
public static class TutorialRoomBuilder
{
    const string SourcePath = "Assets/1.Prefab/Room/Shop/ShopRoom_2S.prefab";
    const string OutFolder = "Assets/1.Prefab/Room/Tutorial";
    const string OutPath = OutFolder + "/TutorialRoom.prefab";
    const string WhiteBoxPath = "Assets/4. Art/Textures/Environment/Room/WhiteBox.png";
    const string DummyPath = "Assets/1.Prefab/Room/Lobby/TrainingDummy.prefab";
    const string BoxPath = "Assets/1.Prefab/item/EtcOBJ/Box_Normal.prefab";
    const string Pickup1Path = "Assets/1.Prefab/Skill/Pickup/SkillPickup_Skill03.prefab";
    const string Pickup2Path = "Assets/1.Prefab/Skill/Pickup/SkillPickup_Skill01.prefab";
    const string TitleFontPath = "Assets/Etc/Font/HiKR-ExtraBold SDF.asset";
    const string BodyFontPath = "Assets/Etc/Font/KoPubWorld Dotum_Pro Medium SDF.asset";
    const string LobbyPath = "Assets/1.Prefab/Room/Lobby/LobbyRoom.prefab";   // [2026-11-18] 튜토리얼 끝나면 가는 방

    const int Width = 76;      // 방 가로 칸 수 [2026-11-18] 188 → 76
    const int Ceiling = 16;    // 천장 높이 (칸)
    const int FloorDepth = 6;  // 바닥 두께

    static Tilemap grund, plat;
    static Vector3Int origin;
    static TileBase fillTile, topTile, platTile;
    static Sprite whiteBox;
    static int groundLayer;

    [MenuItem("EventHorizon/튜토리얼 방 만들기")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/1.Prefab/Room", "Tutorial");
        whiteBox = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
        groundLayer = LayerMask.NameToLayer("ground");

        GameObject root = PrefabUtility.LoadPrefabContents(SourcePath);
        try
        {
            root.name = "TutorialRoom";
            // 상점 전용 오브젝트 제거
            foreach (Transform c in root.transform.Cast<Transform>().ToList())
                if (c.name.StartsWith("ShopNpc") || c.name.EndsWith("ShopCanvas") || c.name == "StatUpgradeCanvas")
                    Object.DestroyImmediate(c.gameObject);
            foreach (var w in root.GetComponentsInChildren<ShopWindow>(true)) Object.DestroyImmediate(w);

            foreach (Tilemap t in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (t.name.Contains("Grund")) grund = t;
                else if (t.name.Contains("Platform")) plat = t;
            }
            PickTiles();
            foreach (Tilemap t in root.GetComponentsInChildren<Tilemap>(true)) t.ClearAllTiles();
            origin = grund.WorldToCell(root.transform.position);

            PaintLayout();
            grund.CompressBounds(); plat.CompressBounds();

            Transform objs = new GameObject("Tutorial").transform;
            objs.SetParent(root.transform, false);
            TutorialController ctrl = objs.gameObject.AddComponent<TutorialController>();
            BuildSteps(root, objs, ctrl);
            BuildGuideCanvas(objs, ctrl);

            // [2026-11-18] 안내는 ??? 통신창으로 → 예전 위쪽 글자 판은 꺼 둠 · 끝나면 로비로
            Transform guideCanvas = objs.Find("TutorialGuideCanvas");
            if (guideCanvas != null) guideCanvas.gameObject.SetActive(false);
            ctrl.doneGuide = "합격. 시뮬레이션은 여기까지야. …이제 진짜로 깨울게.";
            ctrl.lobbyRoom = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath);

            // 시작 위치 · 출구
            Transform spawner = root.transform.Find("PlayerSpawner");
            if (spawner != null) spawner.position = P(3, 1);
            Transform end = root.transform.Find("endpont");
            if (end != null)
            {
                Transform door = end.childCount > 0 ? end.GetChild(0) : end;
                end.position += P(Width - 4, 0) + new Vector3(0, 1.5f, 0) - door.position;
                end.gameObject.SetActive(false);   // [2026-11-18] 문으로 나가지 않고 로비로 자동 이동
            }

            PrefabUtility.SaveAsPrefabAsset(root, OutPath);
            Debug.Log($"[TutorialRoomBuilder] 저장: {OutPath} (채움 {fillTile?.name} / 윗면 {topTile?.name} / 발판 {platTile?.name})");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        TutorialBackgroundSetup.Place();   // [2026-11-18] 배경 판도 이어서 깔기
    }

    [MenuItem("EventHorizon/튜토리얼 방 캡처 (확인용)")]
    public static void Capture()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OutPath), scene);
        foreach (var l in inst.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var c in inst.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
        Tilemap g = inst.GetComponentsInChildren<Tilemap>(true).First(t => t.name.Contains("Grund"));
        g.CompressBounds();
        Bounds bb = g.GetComponent<TilemapRenderer>().bounds;
        var camGo = new GameObject("CapCam");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
        Camera cam = camGo.AddComponent<Camera>();
        cam.scene = scene; cam.orthographic = true;
        int w = 4000, h = Mathf.RoundToInt(4000f * bb.size.y / bb.size.x);
        cam.orthographicSize = bb.extents.y;
        cam.transform.position = new Vector3(bb.center.x, bb.center.y, -20);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.aspect = (float)w / h; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../bak_1116_tutorial/capture_tutorial.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        Debug.Log("[TutorialRoomBuilder] 캡처 저장: bak_1116_tutorial/capture_tutorial.png");
    }

    // ── 타일 고르기: 상점 방에서 가장 많이 쓴 타일 ──
    static void PickTiles()
    {
        var fill = new Dictionary<TileBase, int>(); var top = new Dictionary<TileBase, int>(); var pl = new Dictionary<TileBase, int>();
        foreach (var c in grund.cellBounds.allPositionsWithin)
        {
            TileBase t = grund.GetTile(c); if (t == null) continue;
            fill[t] = fill.TryGetValue(t, out int n) ? n + 1 : 1;
            if (grund.GetTile(c + Vector3Int.up) == null) top[t] = top.TryGetValue(t, out int m) ? m + 1 : 1;
        }
        foreach (var c in plat.cellBounds.allPositionsWithin)
        {
            TileBase t = plat.GetTile(c); if (t == null) continue;
            pl[t] = pl.TryGetValue(t, out int n) ? n + 1 : 1;
        }
        fillTile = fill.OrderByDescending(k => k.Value).First().Key;
        topTile = top.Count > 0 ? top.OrderByDescending(k => k.Value).First().Key : fillTile;
        platTile = pl.Count > 0 ? pl.OrderByDescending(k => k.Value).First().Key : topTile;
    }

    static void Block(int x0, int x1, int y0, int y1)   // 양 끝 포함
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                grund.SetTile(origin + new Vector3Int(x, y, 0), y == y1 ? topTile : fillTile);
    }
    static void Hole(int x0, int x1, int y0, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                grund.SetTile(origin + new Vector3Int(x, y, 0), null);
    }
    static void Platform(int x0, int x1, int y)
    {
        for (int x = x0; x <= x1; x++) plat.SetTile(origin + new Vector3Int(x, y, 0), platTile);
    }

    /// <summary>칸 (x, y)의 아랫면 가운데 = 높이 y 바닥 위에 서는 점</summary>
    static Vector3 P(float x, float y)
    {
        Vector3 cell = grund.CellToWorld(origin + new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0));
        return cell + new Vector3((x - Mathf.Floor(x)) + grund.cellSize.x * 0.5f, y - Mathf.Floor(y), 0);
    }

    static void PaintLayout()
    {
        Block(-4, Width + 3, -FloorDepth, -1);              // 바닥
        Block(-4, Width + 3, Ceiling, Ceiling + 3);         // 천장
        Block(-4, -1, 0, Ceiling - 1);                      // 왼쪽 벽
        Block(Width, Width + 3, 0, Ceiling - 1);            // 오른쪽 벽

        // [2026-11-18 2차] 짧은 배치 (76칸)
        Block(9, 15, 0, 6);             // 1 이동·점프 + 더블점프: 높은 벽 (7칸)
        Hole(22, 26, -FloorDepth, -1);  // 2 대시: 구덩이 5칸
    }

    // ── 구간 ──
    struct Def
    {
        public string title, guide; public TutorialController.Condition cond;
        public int gateX, gateFrom; public Vector3 checkpoint;
    }

    static void BuildSteps(GameObject root, Transform parent, TutorialController ctrl)
    {
        // [2026-11-18] 안내 문구는 ???(영복) 말투 · 2차: 7구간으로 줄임
        var defs = new List<Def>
        {
            // gateX < 0 = 막이 없음 (다음 구간을 같은 자리에서 이어서)
            D("이동 · 점프", "들려? 좋아, 연결됐네. 몸부터 풀자. ← → 로 걷고, {Jump} 를 공중에서 한 번 더 눌러 저 벽을 넘어 봐.", TutorialController.Condition.DoubleJump, 18, 0, P(3, 0)),
            D("대시", "{Dash} 누르면 확 튀어 나가. 저 구덩이, 그걸로 건너.", TutorialController.Condition.Dash, 31, 0, P(19, 0)),
            D("공격", "{Attack} 로 저 허수아비 좀 두들겨 봐. 아파하지 않으니까 마음껏.", TutorialController.Condition.DummyDamage, -1, 0, P(33, 0)),
            D("무기 교체", "무기가 하나뿐인 줄 알았지? {WeaponSwap} 로 바꿔 봐. 검, 건틀릿, 망치.", TutorialController.Condition.WeaponSwap, 44, 0, P(33, 0)),
            D("스킬", "저 구슬 주워. 그리고 {Skill1} · {Skill2} · {Skill3} 중에 아무거나 써 봐.", TutorialController.Condition.AnySkill, -1, 0, P(46, 0)),
            D("퍼즈", "게이지는 내가 꽉 채워 놨어. {Pause} 누르면 세상이 멈춰. 직접 느껴 봐.", TutorialController.Condition.Pause, 60, 0, P(46, 0)),
            D("상호작용 · 인벤토리", "상자 보이지? {Interact} 로 열고, {Inventory} 로 뭐 들었는지 확인해.", TutorialController.Condition.BoxAndInventory, -1, 0, P(62, 0)),
        };

        GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
        Transform gates = Child(parent, "Gates"), points = Child(parent, "Checkpoints"), props = Child(parent, "Props"), zones = Child(parent, "Zones");

        ctrl.steps = new List<TutorialController.Step>();
        for (int i = 0; i < defs.Count; i++)
        {
            Def d = defs[i];
            var s = new TutorialController.Step { title = d.title, guide = d.guide, condition = d.cond };

            // 막이: 바닥부터 천장까지 한 칸 폭 (gateX < 0 이면 막이 없음)
            if (d.gateX >= 0)
            {
                GameObject g = new GameObject($"Gate_{i + 1:00}");
                g.transform.SetParent(gates, false);
                g.layer = groundLayer;
                g.transform.position = P(d.gateX, d.gateFrom) + new Vector3(0, (Ceiling - d.gateFrom) * 0.5f, 0);
                g.transform.localScale = new Vector3(1f, Ceiling - d.gateFrom, 1f);
                var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = whiteBox; sr.color = new Color(0.6f, 0.85f, 1f, 0.55f); sr.sortingOrder = 4;
                g.AddComponent<BoxCollider2D>().size = Vector2.one;
                s.gate = g;
            }

            Transform cp = new GameObject($"Checkpoint_{i + 1:00}").transform;
            cp.SetParent(points, false);
            cp.position = d.checkpoint + Vector3.up;
            s.checkpoint = cp;
            ctrl.steps.Add(s);
        }

        // 구덩이 바닥 = 되돌리기 영역
        Zone(zones, "Respawn_Dash", TutorialZone.Kind.Respawn, ctrl, 22, 26, -12, -2);
        Zone(zones, "Respawn_Bottom", TutorialZone.Kind.Respawn, ctrl, -4, Width + 3, -20, -14);

        // 허수아비: 공격·무기 교체 한 개, 스킬·퍼즈 한 개
        ctrl.steps[2].dummy = Spawn(dummyPrefab, props, "Dummy_Attack", P(39, 0))?.GetComponent<Stats>();
        ctrl.steps[2].damageNeeded = 50;
        Spawn(dummyPrefab, props, "Dummy_SkillPause", P(55, 0));
        ctrl.steps[5].fillPauseGauge = true;

        Spawn(AssetDatabase.LoadAssetAtPath<GameObject>(Pickup1Path), props, "SkillPickup_1", P(49, 1));
        GameObject box = Spawn(AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath), props, "Box", P(66, 0));
        if (box != null) ctrl.steps[6].box = box.GetComponent<Box>();
    }

    static Def D(string t, string g, TutorialController.Condition c, int gateX, int from, Vector3 cp)
        => new Def { title = t, guide = g, cond = c, gateX = gateX, gateFrom = from, checkpoint = cp };

    static Transform Child(Transform p, string n) { var t = new GameObject(n).transform; t.SetParent(p, false); return t; }

    static TutorialZone Zone(Transform parent, string name, TutorialZone.Kind kind, TutorialController ctrl, int x0, int x1, int y0, int y1)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Vector3 a = P(x0, y0) - new Vector3(0.5f, 0, 0), b = P(x1, y1 + 1) + new Vector3(0.5f, 0, 0);
        go.transform.position = (a + b) * 0.5f;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        var z = go.AddComponent<TutorialZone>();
        z.kind = kind; z.controller = ctrl;
        return z;
    }

    static GameObject Spawn(GameObject prefab, Transform parent, string name, Vector3 pos)
    {
        if (prefab == null) { Debug.LogWarning($"[TutorialRoomBuilder] 프리팹 없음: {name}"); return null; }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        return go;
    }

    // ── 화면 글자: 화면 위쪽 가운데 안내 상자 (정렬 기능 사용) ──
    static void BuildGuideCanvas(Transform parent, TutorialController ctrl)
    {
        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        GameObject cgo = new GameObject("TutorialGuideCanvas", typeof(RectTransform));
        cgo.transform.SetParent(parent, false);
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("GuidePanel", typeof(RectTransform));
        panel.transform.SetParent(cgo.transform, false);
        RectTransform prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0, -40);
        prt.sizeDelta = new Vector2(1100, 0);
        var img = panel.AddComponent<Image>(); img.color = new Color(0.08f, 0.07f, 0.06f, 0.78f); img.raycastTarget = false;
        var v = panel.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(36, 36, 18, 22); v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var fit = panel.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ctrl.titleText = Text(panel.transform, "Title", titleFont, 30, new Color(0.91f, 0.77f, 0.48f), "1 / 11  이동 · 점프");
        ctrl.guideText = Text(panel.transform, "Guide", bodyFont, 34, new Color(0.96f, 0.94f, 0.9f), "← → 로 이동하고 Space 로 턱을 넘으세요");
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color color, string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.text = text;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        return t;
    }
}
