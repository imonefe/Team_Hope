using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-26] 사이버웨어 교체 창 프리팹 만들기 (8칸이 가득 찼을 때 · ItemSwapWindow).
///  그림은 전부 기존 것을 복제: 어둡게 깔기 · 제목 · 안내 글자 · 카드 테두리 = 증강 고르기 창(AugmentOfferCanvas),
///  창 틀 = 상점 창(ShopCanvas_Base/Window), 칸 = ItemSlot 프리팹. 정렬은 Layout Group.
///  씬은 건드리지 않는다 — 결과는 Resources/ItemSwapCanvas.prefab, 게임 중 필요할 때 불러옴.
///  메뉴 EventHorizon/사이버웨어 교체 창 만들기 (프로젝트 폴더에 item_swap.trigger 가 있으면 자동 1회)
/// </summary>
[InitializeOnLoad]
public static class ItemSwapBuilder
{
    const string TriggerFile = "item_swap.trigger";
    const string OfferPath = "Assets/1.Prefab/UI/Canvas/상점/AugmentOfferCanvas.prefab";
    const string ShopPath = "Assets/1.Prefab/UI/Canvas/상점/ShopCanvas_Base.prefab";
    const string SlotPath = "Assets/1.Prefab/UI/Parts/ItemSlot.prefab";
    const string OutDir = "Assets/1.Prefab/UI/Canvas/교체/Resources";
    const string OutPath = OutDir + "/ItemSwapCanvas.prefab";
    const int SortOrder = 550;   // 상점 300 · 증강 고르기 320 · 특전 520 위, 게임오버 600 · 설명 팝업 700 아래
    const float SlotSize = 96f;

    static ItemSwapBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(TriggerFile)) return;
            File.Delete(TriggerFile);
            Build();
        };
    }

    [MenuItem("EventHorizon/사이버웨어 교체 창 만들기")]
    public static void Build()
    {
        var offer = PrefabUtility.LoadPrefabContents(OfferPath);
        var shop = PrefabUtility.LoadPrefabContents(ShopPath);
        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
        try
        {
            var title = offer.transform.Find("Title");
            var subtitle = offer.transform.Find("Subtitle");
            var arrow = offer.transform.Find("Arrow");
            var hint = offer.transform.Find("KeyHint");
            var dim = offer.transform.Find("Dim");
            var card = offer.transform.Find("Cards/Card_0");
            var frameSrc = card != null ? card.Find("SelectFrame") : null;
            var winSrc = shop.transform.Find("Window");
            if (title == null || subtitle == null || hint == null || dim == null || card == null || frameSrc == null || winSrc == null || slotPrefab == null)
            { Debug.LogError("[교체 창] 견본(증강 고르기 창 Title·Subtitle·KeyHint·Dim·Card_0 / 상점 Window / ItemSlot)을 못 찾음"); return; }

            // 바탕 = 증강 고르기 캔버스 복제 (Canvas · Scaler · Raycaster 설정 그대로) → 필요 없는 것 걷어냄
            var holder = new GameObject("holder");
            SceneManager.MoveGameObjectToScene(holder, offer.scene);
            var root = Object.Instantiate(offer, holder.transform);
            root.transform.SetParent(null, false);
            root.name = "ItemSwapCanvas";
            foreach (var mb in root.GetComponents<MonoBehaviour>())
                if (!(mb is CanvasScaler) && !(mb is GraphicRaycaster)) Object.DestroyImmediate(mb);
            var canvas = root.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = SortOrder;
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            // 켜고 끄는 본체
            var panel = NewRect("Panel", root.transform);
            Stretch(panel, 0f);
            var dimGo = Clone(dim, panel, "Dim");
            Stretch(dimGo, 0f);
            dimGo.GetComponent<Image>().raycastTarget = true;   // 뒤의 창 클릭 막기

            // 창 틀 (상점 창 그림) — 크기는 안의 내용에 맞춤
            var win = Clone(winSrc, panel, "Window");
            for (int i = win.childCount - 1; i >= 0; i--) Object.DestroyImmediate(win.GetChild(i).gameObject);
            foreach (var mb in win.GetComponents<MonoBehaviour>()) if (!(mb is Image)) Object.DestroyImmediate(mb);
            win.anchorMin = win.anchorMax = win.pivot = new Vector2(0.5f, 0.5f);
            win.anchoredPosition = new Vector2(0f, 20f);
            var vlg = win.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(64, 64, 48, 52);
            vlg.spacing = 22f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            var fit = win.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleT = CloneText(title, win, "Title", "사이버웨어 칸이 가득 찼습니다");
            Pref(titleT.gameObject, -1f, 72f);

            // 새 사이버웨어 줄
            var newRow = Row(win, "NewRow", 18f);
            var newLabel = CloneText(subtitle, newRow, "Label", "새 사이버웨어");
            newLabel.color = new Color(0.71f, 0.68f, 0.64f, 1f);
            Pref(newLabel.gameObject, 180f, SlotSize);
            newLabel.alignment = TextAlignmentOptions.MidlineRight;
            var newSlot = Slot(slotPrefab, newRow, "NewSlot", null);
            var newName = CloneText(subtitle, newRow, "Name", "이름");
            newName.color = new Color(0.89f, 0.76f, 0.49f, 1f);
            newName.fontSize = 26f;
            newName.alignment = TextAlignmentOptions.MidlineLeft;
            Pref(newName.gameObject, 300f, SlotSize);

            if (arrow != null) Pref(CloneText(arrow, win, "Arrow", "▼").gameObject, -1f, 36f);

            // 가진 8칸
            var slotsRow = Row(win, "Slots", 14f);
            var slots = new ItemSlotUI[Inventory.MaxItems];
            var frames = new GameObject[Inventory.MaxItems];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Slot(slotPrefab, slotsRow, "Slot_" + (i + 1), frameSrc, f => frames[i] = f);

            var info = CloneText(subtitle, win, "Info", "버릴 사이버웨어를 고르세요");
            Pref(info.gameObject, -1f, 40f);

            // 버튼 (증강 카드 테두리 그림)
            var btnRow = Row(win, "Buttons", 30f);
            var swapBtn = Button(card, subtitle, btnRow, "SwapButton", "교체", true);
            var cancelBtn = Button(card, subtitle, btnRow, "CancelButton", "포기", false);

            // 아래 조작 안내 (증강 고르기 창과 같은 자리)
            var hintT = CloneText(hint, panel, "KeyHint", "← → 고르기     Enter 교체     Esc 포기");

            var w = root.AddComponent<ItemSwapWindow>();
            w.panel = panel.gameObject;
            w.title = titleT;
            w.info = info;
            w.newSlot = newSlot;
            w.newName = newName;
            w.slots = slots;
            w.frames = frames;
            w.swapButton = swapBtn;
            w.cancelButton = cancelBtn;
            w.swapGroup = swapBtn.GetComponent<CanvasGroup>();
            panel.gameObject.SetActive(false);

            if (!AssetDatabase.IsValidFolder(OutDir)) Directory.CreateDirectory(OutDir);
            AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root, OutPath, out bool ok);
            Debug.Log(ok ? "[교체 창] 만듦 — " + OutPath : "[교체 창] 저장 실패");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(offer);
            PrefabUtility.UnloadPrefabContents(shop);
        }
    }

    // ── 도우미 ──
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Clone(Transform src, Transform parent, string name)
    {
        var go = Object.Instantiate(src.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        return (RectTransform)go.transform;
    }

    static TMP_Text CloneText(Transform src, Transform parent, string name, string text)
    {
        var rt = Clone(src, parent, name);
        for (int i = rt.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rt.GetChild(i).gameObject);
        var t = rt.GetComponent<TMP_Text>();
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void Pref(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (w >= 0f) { le.preferredWidth = w; le.minWidth = w; }
        if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
    }

    static RectTransform Row(Transform parent, string name, float spacing)
    {
        var rt = NewRect(name, parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return rt;
    }

    static ItemSlotUI Slot(GameObject prefab, Transform parent, string name, Transform frameSrc, System.Action<GameObject> frameOut = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        Pref(go, SlotSize, SlotSize);
        if (frameSrc != null)
        {
            // 고른 칸 표시 = 증강 카드의 금색 선택 테두리. 칸 뒤에 깔려 바깥으로 6 만큼 보임
            var f = Clone(frameSrc, go.transform, "SelectFrame");
            Stretch(f, -6f);
            f.SetAsFirstSibling();
            var img = f.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            var le = f.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            f.gameObject.SetActive(false);
            frameOut?.Invoke(f.gameObject);
        }
        return go.GetComponent<ItemSlotUI>();
    }

    /// <summary>[17:41 수정] 증강 카드를 버튼 바탕으로 복제할 때 카드 안 내용물(이름 · 그림 · 배지 · 설명)을 지운다 — 바탕 · 테두리만 남김</summary>
    public static void StripCard(Transform card)
    {
        var body = card.Find("Body");
        if (body != null) for (int i = body.childCount - 1; i >= 0; i--) Object.DestroyImmediate(body.GetChild(i).gameObject);
        for (int i = card.childCount - 1; i >= 0; i--)
        {
            var c = card.GetChild(i);
            if (c.name != "Body" && c.name != "Border" && c.name != "SelectFrame" && c.name != "Label") Object.DestroyImmediate(c.gameObject);
        }
    }

    static Button Button(Transform card, Transform textSrc, Transform parent, string name, string label, bool primary)
    {
        var rt = Clone(card, parent, name);
        foreach (var mb in rt.GetComponents<MonoBehaviour>())
            if (!(mb is Image) && !(mb is Button)) Object.DestroyImmediate(mb);
        StripCard(rt);
        var b = rt.GetComponent<Button>() ?? rt.gameObject.AddComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent();
        Pref(rt.gameObject, 240f, 64f);
        var sel = rt.Find("SelectFrame");
        if (sel != null) sel.gameObject.SetActive(primary);   // 교체 = 금색 테두리, 포기 = 파란 테두리만
        var t = CloneText(textSrc, rt, "Label", label);
        Stretch((RectTransform)t.transform, 0f);
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 28f;
        t.color = primary ? new Color(0.89f, 0.76f, 0.49f, 1f) : new Color(0.71f, 0.68f, 0.64f, 1f);
        if (primary) rt.gameObject.AddComponent<CanvasGroup>();
        return b;
    }
}
