using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-26] 인벤토리 8칸 배치 + 가득 찼을 때 인벤토리에서 고르는 교체 (18:02 구성).
///  - 아이템 그리드: 2칸 × 4줄 세로 배치 (170px), 스크롤 끔
///  - ItemSlot 프리팹 아이콘: 고정 크기 → 칸의 80% 로 늘어남 (큰 칸에서 아이콘이 작던 문제)
///  - 교체 중: 상태 판을 끄고 인벤토리를 화면 가운데로 (닫으면 제자리)
///  - 칸을 고르면 확인 창 SwapConfirm: "교체하시겠습니까?" · 버릴 것 → 새 것 · 파편 +N · [교체] [취소]
///    그림 · 글꼴은 상태 판 그림과 교체 창 프리팹(Resources/ItemSwapCanvas)에서 복제
///  - InventoryTab 의 ItemSwapWindow(useInventory) 에 연결, Inventory.swapWindow 연결 → 씬 저장
///  메뉴 EventHorizon/인벤토리 8칸 배치 · 교체 줄 만들기 (프로젝트 폴더에 inv_swap.trigger 가 있으면 자동 1회)
///  다시 실행하면 확인 창 · 가림막만 새로 만듦 (그리드 값도 다시 넣음)
/// </summary>
[InitializeOnLoad]
public static class InventorySwapBuilder
{
    const string TriggerFile = "inv_swap.trigger";
    const string SwapPrefabPath = "Assets/1.Prefab/UI/Canvas/교체/Resources/ItemSwapCanvas.prefab";
    const float Cell = 170f, Gap = 26f;   // [17:46] 2칸 × 4줄 세로 배치
    const int Columns = 2;
    const string SlotPrefabPath = "Assets/1.Prefab/UI/Parts/ItemSlot.prefab";
    const float IconInset = 0.1f;   // 아이콘 = 칸의 80%

    static bool warned;
    static TMP_Text confirmHintRef;
    static InventorySwapBuilder() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (!File.Exists(TriggerFile)) { EditorApplication.update -= Poll; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (FindInScene("InventoryTab") == null)
        {
            if (!warned) { Debug.LogWarning("[인벤토리 교체] SampleScene 을 열면 진행됩니다"); warned = true; }
            return;
        }
        EditorApplication.update -= Poll;
        File.Delete(TriggerFile);
        Build();
    }

    [MenuItem("EventHorizon/인벤토리 8칸 배치 · 교체 줄 만들기")]
    public static void Build()
    {
        var tab = FindInScene("InventoryTab") as RectTransform;
        if (tab == null) { Debug.LogError("[인벤토리 교체] 열린 씬에 InventoryTab 이 없음"); return; }
        var content = tab.Find("Viewport/Content");
        var grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
        if (grid == null) { Debug.LogError("[인벤토리 교체] InventoryTab/Viewport/Content 의 GridLayoutGroup 을 못 찾음"); return; }
        var panelRoot = tab.parent;
        var status = panelRoot.Find("StatusPanel") as RectTransform;
        if (status == null) { Debug.LogError("[인벤토리 교체] StatusPanel 을 못 찾음"); return; }

        // ── 1) 8칸 배치 ──
        Undo.RecordObject(grid, "인벤토리 8칸");
        grid.cellSize = new Vector2(Cell, Cell);
        grid.spacing = new Vector2(Gap, Gap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        var scroll = tab.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            Undo.RecordObject(scroll, "인벤토리 8칸");
            scroll.vertical = false;
            scroll.verticalScrollbar = null;
            scroll.verticalNormalizedPosition = 1f;
        }
        var bar = tab.Find("Scrollbar Vertical");
        if (bar != null) { Undo.RecordObject(bar.gameObject, "인벤토리 8칸"); bar.gameObject.SetActive(false); }

        // ── 2) 칸 아이콘 크게 (프리팹) ──
        FixSlotIcon();

        // 예전 구성 정리
        { var o = tab.Find("SwapBar"); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); }
        foreach (var n in new[] { "SwapPanel", "SwapConfirm", "SwapDim" }) { var o = panelRoot.Find(n); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); }

        var src = PrefabUtility.LoadPrefabContents(SwapPrefabPath);
        try
        {
            var win = src.transform.Find("Panel/Window");
            if (win == null) { Debug.LogError("[인벤토리 교체] 교체 창 프리팹 구조가 다름 (Panel/Window)"); return; }

            // ── 3) 뒤 가림막 (교체 중 뒤의 증강 고르기 · 상점 창이 비치지 않게) ──
            var dim = Full("SwapDim", panelRoot, new Color(0.03f, 0.03f, 0.03f, 0.92f));
            dim.SetAsFirstSibling();
            dim.gameObject.SetActive(false);

            // ── 4) 확인 창 ──
            var confirm = Full("SwapConfirm", panelRoot, new Color(0f, 0f, 0f, 0.6f));
            confirm.SetAsLastSibling();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            box.gameObject.layer = panelRoot.gameObject.layer;
            box.SetParent(confirm, false);
            Undo.RegisterCreatedObjectUndo(box.gameObject, "교체 확인");
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            var sImg = status.GetComponent<Image>();
            var bImg = box.GetComponent<Image>();
            if (sImg != null) { bImg.sprite = sImg.sprite; bImg.type = sImg.type; bImg.color = sImg.color; bImg.pixelsPerUnitMultiplier = sImg.pixelsPerUnitMultiplier; bImg.material = sImg.material; }
            var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(70, 70, 56, 52);
            v.spacing = 22f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            // [18:10 수정] 크기는 고정 — 내용에 맞춤(ContentSizeFitter)을 쓰면 판 그림(상태 판)의 원본 크기가 더 커서 화면보다 커졌음
            //  내용 높이: 여백 108 + 제목 46 + 칸 · 이름 186 + 안내 30 + 버튼 58 + 키 안내 24 + 간격 88 = 540
            box.sizeDelta = new Vector2(680f, 560f);
            v.childAlignment = TextAnchor.MiddleCenter;

            var title = Clone(win.Find("Title"), box, "Title").GetComponent<TMP_Text>();
            title.text = "교체하시겠습니까?";
            title.fontSize = 32f;
            title.alignment = TextAlignmentOptions.Center;
            Pref(title.gameObject, 520f, 46f);

            // 버릴 것 → 새 것
            var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.gameObject.layer = box.gameObject.layer;
            row.SetParent(box, false);
            Undo.RegisterCreatedObjectUndo(row.gameObject, "교체 확인");
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 26f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            TMP_Text oldName, newName;
            var oldSlot = Column(win, row, "Old", out oldName);
            var arrow = Clone(win.Find("Arrow") ?? win.Find("Info"), row, "Arrow").GetComponent<TMP_Text>();
            arrow.text = "→";
            arrow.fontSize = 40f;
            arrow.alignment = TextAlignmentOptions.Center;
            Pref(arrow.gameObject, 60f, 120f);
            var newSlot = Column(win, row, "New", out newName);

            var info = Clone(win.Find("Info"), box, "Info").GetComponent<TMP_Text>();
            info.fontSize = 20f;
            info.alignment = TextAlignmentOptions.Center;
            Pref(info.gameObject, 520f, 30f);

            var buttons = Clone(win.Find("Buttons"), box, "Buttons");
            var bh = buttons.GetComponent<HorizontalLayoutGroup>();
            if (bh != null) bh.spacing = 24f;
            foreach (Transform b in buttons)
            {
                ItemSwapBuilder.StripCard(b);   // 카드 안 내용물 제거 (바탕 · 테두리 · 글자만)
                Pref(b.gameObject, 200f, 58f);
                var t = b.Find("Label")?.GetComponent<TMP_Text>();
                if (t != null) { t.fontSize = 24f; if (b.name == "CancelButton") t.text = "취소"; }
            }
            var hintSrc = src.transform.Find("Panel/KeyHint");
            if (hintSrc != null)
            {
                var hint = Clone(hintSrc, box, "KeyHint").GetComponent<TMP_Text>();
                hint.text = "← → 선택     F 결정     Esc 취소";
                confirmHintRef = hint;
                hint.fontSize = 15f;
                hint.alignment = TextAlignmentOptions.Center;
                Pref(hint.gameObject, 520f, 24f);
            }
            confirm.gameObject.SetActive(false);

            // ── 5) 연결 ──
            var w = tab.GetComponent<ItemSwapWindow>();
            if (w == null) w = Undo.AddComponent<ItemSwapWindow>(tab.gameObject);
            Undo.RecordObject(w, "교체");
            w.useInventory = true;
            w.title = null; w.info = null; w.newSlot = null; w.newName = null;
            w.slots = new ItemSlotUI[0];
            w.frames = new GameObject[0];
            w.swapButton = null; w.cancelButton = null; w.swapGroup = null;
            w.dim = dim.gameObject;
            // [18:16] 고르는 화면: 'inventory' 제목 대신 "사이버웨어 칸이 가득 찼습니다"
            var invTitle = panelRoot.Find("inventory") as RectTransform;
            { var o = tab.Find("FullNotice"); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); }
            var notice = Clone(win.Find("Title"), tab, "FullNotice").GetComponent<TMP_Text>();
            var nrt = (RectTransform)notice.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(Columns * Cell + (Columns - 1) * Gap + 200f, 50f);
            float gridCenterX = (grid.padding.left - grid.padding.right) * 0.5f;
            // [18:21 수정] 'inventory' 제목과 같은 높이 — 판 위쪽 기준 거리로 직접 계산 (월드 좌표로 맞추면 꺼진 창에서 값이 틀려 화면 맨 위로 튐)
            float fromTop = 88f;
            if (invTitle != null && tab.parent is RectTransform pr && pr.rect.height > 1f)
            {
                float tabTop = (tab.anchorMax.y - 0.5f) * pr.rect.height + tab.offsetMax.y;   // 부모 가운데 기준 판 위쪽
                float d = tabTop - invTitle.anchoredPosition.y;
                if (d > 20f && d < 300f) fromTop = d;
            }
            nrt.anchoredPosition = new Vector2(gridCenterX, -fromTop);
            notice.text = "사이버웨어 칸이 가득 찼습니다";
            notice.fontSize = 26f;
            notice.color = new Color(0.89f, 0.76f, 0.49f, 1f);
            notice.alignment = TextAlignmentOptions.Center;
            notice.enableWordWrapping = false;
            notice.raycastTarget = false;
            notice.gameObject.SetActive(false);
            w.panel = notice.gameObject;
            w.hideWhileSwapping = invTitle != null ? new[] { status.gameObject, invTitle.gameObject } : new[] { status.gameObject };
            w.centerDuringSwap = new[] { tab };
            w.centerShiftX = -tab.anchoredPosition.x;
            w.confirmRoot = confirm.gameObject;
            w.confirmOld = oldSlot; w.confirmNew = newSlot;
            w.confirmOldName = oldName; w.confirmNewName = newName;
            w.confirmInfo = info;
            w.confirmYes = buttons.Find("SwapButton")?.GetComponent<Button>();
            w.confirmNo = buttons.Find("CancelButton")?.GetComponent<Button>();
            w.confirmHint = confirmHintRef;

            var inv = Object.FindObjectOfType<Inventory>(true);
            if (inv != null) { Undo.RecordObject(inv, "교체"); inv.swapWindow = w; }
            else Debug.LogWarning("[인벤토리 교체] 씬에 Inventory 가 없어 연결 못 함");
        }
        finally { PrefabUtility.UnloadPrefabContents(src); }

        var scene = tab.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log("[인벤토리 교체] 2칸 × 4줄 · 아이콘 · 확인 창 완료" + (saved ? " — 씬 저장함" : " — 씬 저장 실패, 직접 저장해 주세요"));
    }

    // 칸 + 이름 한 세로 묶음
    static ItemSlotUI Column(Transform win, Transform parent, string name, out TMP_Text label)
    {
        var col = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        col.gameObject.layer = parent.gameObject.layer;
        col.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(col.gameObject, "교체 확인");
        var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        var slot = Clone(win.Find("NewRow/NewSlot"), col, "Slot");
        Pref(slot.gameObject, 120f, 120f);
        label = Clone(win.Find("NewRow/Name"), col, "Name").GetComponent<TMP_Text>();
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        Pref(label.gameObject, 200f, 56f);
        return slot.GetComponent<ItemSlotUI>();
    }

    // 화면 전체를 덮는 단색 판
    static RectTransform Full(string name, Transform parent, Color c)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rt.gameObject.layer = parent.gameObject.layer;
        rt.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(rt.gameObject, "교체");
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = rt.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = true;
        return rt;
    }

    // ItemSlot 프리팹: 아이콘을 칸 크기에 맞춰 늘어나게 (칸의 80%)
    static void FixSlotIcon()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            var ui = root.GetComponent<ItemSlotUI>();
            var icon = ui != null && ui.iconImage != null ? ui.iconImage.rectTransform : null;
            if (icon == null) { Debug.LogWarning("[인벤토리 교체] ItemSlot 프리팹의 아이콘(iconImage)을 못 찾음"); return; }
            icon.anchorMin = new Vector2(IconInset, IconInset);
            icon.anchorMax = new Vector2(1f - IconInset, 1f - IconInset);
            icon.offsetMin = icon.offsetMax = Vector2.zero;
            icon.anchoredPosition = Vector2.zero;
            ui.iconImage.preserveAspect = true;
            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static RectTransform Clone(Transform src, Transform parent, string name)
    {
        var go = Object.Instantiate(src.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "교체 줄");
        return (RectTransform)go.transform;
    }

    static GameObject NewSpacer(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "교체 판");
        return go;
    }

    static void Pref(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (w >= 0f) { le.preferredWidth = w; le.minWidth = w; }
        if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
    }

    static Transform FindInScene(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
