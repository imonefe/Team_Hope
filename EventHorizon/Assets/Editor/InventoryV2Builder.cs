using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-26] 인벤토리 시안 v2 적용 — 1-B(평소) · 2-A(교체 고르기) · 3-B(교체 확인).
///  기존 오브젝트는 지우지 않고 제자리에서 다시 배치한다 (InventoryTab = 띠, StatusPanel = 상태 판).
///  새로 만드는 것: RouteBar · KeywordPanel · SwapInfo · SwapConfirm(다시 만듦), ItemSlot 프리팹의 Name · Stars 글자.
///  판 모양: 단색 바탕 + 모서리를 넘는 1px 선 + 머리띠 (새 그림 없음, 글꼴은 상태 판 글자 복제)
///  메뉴 EventHorizon/인벤토리 시안 v2 적용 (프로젝트 폴더에 inv_v2.trigger 가 있으면 자동 1회). 다시 실행해도 됨.
/// </summary>
[InitializeOnLoad]
public static class InventoryV2Builder
{
    const string TriggerFile = "inv_v2.trigger";
    const string SwapPrefabPath = "Assets/1.Prefab/UI/Canvas/교체/Resources/ItemSwapCanvas.prefab";
    const string SlotPrefabPath = "Assets/1.Prefab/UI/Parts/ItemSlot.prefab";

    static readonly Color Bg = Hex("191919"), HeadBg = Hex("0E0E0E"), Tile = Hex("161616");
    static readonly Color LineC = new Color(0.91f, 0.9f, 0.88f, 0.55f);
    static readonly Color Gold = Hex("E3C27E"), Muted = Hex("8E8A84"), TextC = Hex("E8E6E1"), Purple = Hex("B08BE0");

    static TMP_Text font;   // 복제용 글자 (한글 글꼴)
    static bool warned;

    static InventoryV2Builder() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (!File.Exists(TriggerFile)) { EditorApplication.update -= Poll; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (FindInScene("InventoryTab") == null)
        {
            if (!warned) { Debug.LogWarning("[인벤토리 v2] SampleScene 을 열면 진행됩니다"); warned = true; }
            return;
        }
        EditorApplication.update -= Poll;
        File.Delete(TriggerFile);
        Build();
    }

    [MenuItem("EventHorizon/인벤토리 시안 v2 적용")]
    public static void Build()
    {
        var tab = FindInScene("InventoryTab") as RectTransform;
        if (tab == null) { Debug.LogError("[인벤토리 v2] 열린 씬에 InventoryTab 이 없음"); return; }
        var root = tab.parent as RectTransform;
        var status = root.Find("StatusPanel") as RectTransform;
        var content = tab.Find("Viewport/Content") as RectTransform;
        var viewport = tab.Find("Viewport") as RectTransform;
        var grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
        var w = tab.GetComponent<ItemSwapWindow>();
        var statusUI = status != null ? status.GetComponent<StatusPanelUI>() : null;
        if (status == null || grid == null || w == null || statusUI == null)
        { Debug.LogError("[인벤토리 v2] StatusPanel / InventoryTab/Viewport/Content(Grid) / ItemSwapWindow / StatusPanelUI 중 없는 것이 있음 — 중단"); return; }
        clips.Clear();
        font = statusUI.maxHp != null && statusUI.maxHp.value != null ? statusUI.maxHp.value : status.GetComponentInChildren<TMP_Text>(true);
        if (font == null) { Debug.LogError("[인벤토리 v2] 복제할 TMP 글자를 못 찾음"); return; }
        var oldColumn = tab.GetComponentInChildren<InventoryKeywordColumn>(true);
        var shardSprite = status.Find("ShardIcon")?.GetComponent<Image>()?.sprite;

        // 예전에 만든 것 정리 (다시 실행 대비)
        foreach (var n in new[] { "RouteBar", "KeywordPanel", "SwapInfo", "SwapConfirm" }) { var o = root.Find(n); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); }
        foreach (var n in new[] { "Frame", "Head", "HeadText", "Back" }) { var o = tab.Find(n); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); var p = status.Find(n); if (p != null) Undo.DestroyObjectImmediate(p.gameObject); }

        // 예전 것 끄기 (지우지 않음)
        Off(root.Find("inventory"));
        if (oldColumn != null) Off(oldColumn.transform);
        Off(tab.Find("noneitem"));

        // ════════ 1-B 띠: InventoryTab ════════
        Undo.RecordObject(tab, "인벤토리 v2");
        Center(tab, new Vector2(0f, 275f), new Vector2(1520f, 330f));
        PanelLook(tab, Color.white, LineC, 26f, 52f);
        var headText = Txt("HeadText", tab, 24f, TextC, TextAlignmentOptions.Center, false);
        Place(headText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -lastHeadCenter), new Vector2(600f, 40f));
        headText.text = "inventory  <size=66%><color=#8E8A84>0 / 8</color></size>";
        if (viewport != null)
        {
            Undo.RecordObject(viewport, "인벤토리 v2");
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var rm = viewport.GetComponent<RectMask2D>();
            if (rm != null) { Undo.RecordObject(rm, "인벤토리 v2"); rm.padding = Vector4.zero; }   // 예전 가림 여백(위 123 · 아래 80)이 칸을 잘랐음
        }
        Undo.RecordObject(content, "인벤토리 v2");
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0f, 330f);
        Undo.RecordObject(grid, "인벤토리 v2");
        grid.cellSize = new Vector2(160f, 160f);
        grid.spacing = new Vector2(18f, 18f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(40, 40, 80, 0);
        var notice = tab.Find("FullNotice");
        if (notice != null)
        {
            var nt = notice.GetComponent<TMP_Text>();
            Undo.RecordObject(notice, "인벤토리 v2");
            Place((RectTransform)notice, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(440f, 44f));
            if (nt != null) { Undo.RecordObject(nt, "인벤토리 v2"); nt.fontSize = 22f; nt.fontStyle = FontStyles.Bold; nt.color = Gold; }
            notice.SetAsLastSibling();
        }

        // 칸 프리팹: 이름 · 별
        SlotLabels();

        // ════════ 1-B 상태 판: StatusPanel ════════
        Undo.RecordObject(status, "인벤토리 v2");
        Center(status, new Vector2(-330f, -210f), new Vector2(860f, 540f));
        PanelLook(status, Color.white, LineC, 26f, 52f);
        var stTitle = status.Find("status") as RectTransform;
        if (stTitle != null)
        {
            Undo.RecordObject(stTitle, "인벤토리 v2");
            Place(stTitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -lastHeadCenter), new Vector2(300f, 40f));
            var t = stTitle.GetComponent<TMP_Text>();
            if (t != null) { Undo.RecordObject(t, "인벤토리 v2"); t.fontSize = 24f; t.alignment = TextAlignmentOptions.Center; }
            stTitle.SetAsLastSibling();
        }
        foreach (Transform c in status) if (c.name == "Line") Off(c);
        Off(status.Find("WeaponLabel")); Off(status.Find("CurrencyLabel")); Off(status.Find("SkillLabel"));
        TL(status.Find("WeaponFrame"), 40f, -84f, 280f, 150f);
        TL(status.Find("NameRow"), 40f, -246f, 280f, 30f);
        for (int i = 0; i < 3; i++)
        {
            var s = status.Find("Skill_" + (i + 1)) as RectTransform;
            if (s == null) continue;
            Undo.RecordObject(s, "인벤토리 v2");
            Place(s, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(80f + i * 88f, -330f), new Vector2(80f, 80f));
        }
        TL(status.Find("CurrencyIcon"), 40f, -410f, 28f, 28f);
        TL(status.Find("CurrencyAmount"), 78f, -407f, 220f, 34f);
        TL(status.Find("ShardIcon"), 40f, -450f, 28f, 28f);
        TL(status.Find("ShardAmount"), 78f, -447f, 220f, 34f);
        for (int i = 0; i < 7; i++)
        {
            var s = status.Find("Stat_" + (i + 1)) as RectTransform;
            if (s == null) continue;
            TL(s, 360f + (i % 2) * 242f, -84f - (i / 2) * 88f, 218f, 76f);
            var img = s.GetComponent<Image>() ?? Undo.AddComponent<Image>(s.gameObject);
            Undo.RecordObject(img, "인벤토리 v2");
            img.sprite = null; img.color = Tile; img.raycastTarget = false;
            Off(s.Find("Line"));
            var label = s.Find("Label") as RectTransform;
            if (label != null)
            {
                TL(label, 14f, -9f, 196f, 22f);
                var t = label.GetComponent<TMP_Text>();
                Undo.RecordObject(t, "인벤토리 v2");
                t.fontSize = 14f; t.color = Muted; t.alignment = TextAlignmentOptions.TopLeft; t.enableWordWrapping = false;
            }
            var row = s.Find("ValueRow") as RectTransform;
            if (row == null) row = New("ValueRow", s);
            { var h = row.GetComponent<HorizontalLayoutGroup>(); if (h != null) Undo.DestroyObjectImmediate(h); }   // [수정] 자동 배치 대신 고정 위치
            TL(row, 14f, -34f, 196f, 34f);
            foreach (var n in new[] { "Value", "Bonus" })
            {
                var v = s.Find(n) ?? row.Find(n);
                if (v == null) continue;
                if (v.parent != row) v.SetParent(row, false);
                v.localScale = Vector3.one;   // [수정] 꺼진 캔버스(배율 0) 기준으로 옮겨져 배율이 0이 됐던 것
                v.localRotation = Quaternion.identity;
                var t = v.GetComponent<TMP_Text>();
                Undo.RecordObject(t, "인벤토리 v2");
                t.fontSize = n == "Value" ? 24f : 15f;
                t.alignment = TextAlignmentOptions.BottomLeft;
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Overflow;
                if (n == "Value") TL(v, 0f, 0f, 110f, 34f);
                else TL(v, 112f, -4f, 84f, 28f);
            }
            var bonus = row.Find("Bonus"); if (bonus != null) bonus.SetAsLastSibling();
        }
        Undo.RecordObject(statusUI, "인벤토리 v2");
        statusUI.upColor = Gold;
        statusUI.downColor = Hex("D9604C");

        // ════════ 1-B 키워드 판 ════════
        var kwPanel = New("KeywordPanel", root);
        Center(kwPanel, new Vector2(455f, -210f), new Vector2(610f, 540f));
        kwPanel.gameObject.AddComponent<Image>();
        PanelLook(kwPanel, Color.white, LineC, 26f, 52f);
        var kwHead = Txt("HeadText", kwPanel, 24f, TextC, TextAlignmentOptions.Center, false);
        Place(kwHead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -lastHeadCenter), new Vector2(300f, 40f));
        kwHead.text = "keyword";
        var kwGrid = kwPanel.gameObject.AddComponent<InventoryKeywordGrid>();
        kwGrid.cells = new RectTransform[8];
        kwGrid.slotContent = content;
        for (int k = 0; k < 8; k++)
        {
            var cell = New("Keyword_" + AugmentRules.KeywordName((AugmentKeyword)k), kwPanel);
            TL(cell, 34f + (k % 4) * 139f, -84f - (k / 4) * 204f, 125f, 186f);
            var bg = cell.gameObject.AddComponent<Image>();
            bg.color = Tile; bg.raycastTarget = true;
            cell.gameObject.AddComponent<CanvasGroup>();
            Sprite sp = oldColumn != null && oldColumn.icons != null && k < oldColumn.icons.Length ? oldColumn.icons[k] : null;
            var icon = New("Icon", cell);
            Place(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(72f, 72f));
            var ii = icon.gameObject.AddComponent<Image>();
            ii.sprite = sp; ii.preserveAspect = true; ii.raycastTarget = false; ii.enabled = sp != null;
            var lab = Txt("Label", cell, 16f, TextC, TextAlignmentOptions.Center, true);
            Place(lab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(120f, 24f));
            lab.text = AugmentRules.KeywordName((AugmentKeyword)k);
            ColorUtility.TryParseHtmlString("#" + AugmentRules.KeywordHex((AugmentKeyword)k), out Color kc);
            lab.color = kc;
            var pips = New("Pips", cell);
            Place(pips, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(100f, 20f));
            float[] vis = { 8f, 8f, 17f, 17f, 17f };   // 화면에서 차지하는 폭 (마름모 = 12 × 1.42)
            float total = 0f; foreach (var v in vis) total += v; total += 4 * 5f;
            float x = -total * 0.5f;
            for (int p = 0; p < 5; p++)
            {
                var pip = New("P" + p, pips);
                bool dia = p >= 2;
                float s = dia ? 12f : 8f;
                Place(pip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x + vis[p] * 0.5f, 0f), new Vector2(s, s));
                if (dia) pip.localEulerAngles = new Vector3(0f, 0f, 45f);
                var pi = pip.gameObject.AddComponent<Image>();
                pi.color = Hex("2E2B28"); pi.raycastTarget = false;
                x += vis[p] + 5f;
            }
            var hov = cell.gameObject.AddComponent<KeywordIconHover>();
            hov.keyword = (AugmentKeyword)k; hov.icon = sp;
            kwGrid.cells[k] = cell;
        }

        // ════════ 1-B 진행 줄 ════════
        var bar = New("RouteBar", root);
        Center(bar, new Vector2(0f, 490f), new Vector2(1520f, 40f));
        var bh = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        bh.spacing = 10f; bh.childAlignment = TextAnchor.MiddleCenter;
        bh.childControlWidth = bh.childControlHeight = true;
        bh.childForceExpandWidth = bh.childForceExpandHeight = false;
        var node = New("Node", bar);
        var nle = node.gameObject.AddComponent<LayoutElement>(); nle.preferredWidth = nle.preferredHeight = 16f;
        foreach (var n in new[] { "Edge", "Fill" })
        {
            var d = New(n, node);
            Place(d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f));
            d.localEulerAngles = new Vector3(0f, 0f, 45f);
            var di = d.gameObject.AddComponent<Image>(); di.raycastTarget = false;
        }
        node.gameObject.SetActive(false);
        var line = New("Line", bar);
        var lle = line.gameObject.AddComponent<LayoutElement>(); lle.preferredWidth = lle.minWidth = 70f; lle.preferredHeight = lle.minHeight = 1f;
        var lim = line.gameObject.AddComponent<Image>(); lim.raycastTarget = false;
        line.gameObject.SetActive(false);
        var route = bar.gameObject.AddComponent<InventoryRouteBar>();
        route.nodeTemplate = node; route.lineTemplate = line;

        // ════════ 2-A 오른쪽 new / out 판 ════════
        var info = New("SwapInfo", root);
        info.anchorMin = Vector2.zero; info.anchorMax = Vector2.one; info.offsetMin = info.offsetMax = Vector2.zero;
        var np = New("NewPanel", info);
        Center(np, new Vector2(290f, 260f), new Vector2(380f, 400f));
        np.gameObject.AddComponent<Image>();
        PanelLook(np, Color.white, LineC, 18f, 44f);
        HeadLabel(np, "new", TextC, 20f, 44f);
        w.infoNewIcon = Icon("Icon", np, 28f, -70f, 96f);
        w.infoNewName = TxtTL("Name", np, 140f, -82f, 212f, 30f, 20f, TextC, true);
        w.infoNewMeta = TxtTL("Meta", np, 140f, -116f, 212f, 22f, 13f, Muted, false);
        w.infoNewDesc = TxtTL("Desc", np, 28f, -182f, 324f, 120f, 15f, Hex("CFCBC4"), false, true);
        w.infoNewKw = TxtTL("Keywords", np, 28f, -310f, 324f, 76f, 15f, TextC, false);
        var op = New("OutPanel", info);
        Center(op, new Vector2(290f, -131f), new Vector2(380f, 330f));
        op.gameObject.AddComponent<Image>();
        op.gameObject.AddComponent<CanvasGroup>().alpha = 0.85f;
        PanelLook(op, Color.white, new Color(0.91f, 0.9f, 0.88f, 0.35f), 18f, 44f);
        HeadLabel(op, "out", Muted, 20f, 44f);
        w.infoOutIcon = Icon("Icon", op, 28f, -66f, 80f);
        w.infoOutName = TxtTL("Name", op, 124f, -74f, 228f, 28f, 18f, TextC, true);
        w.infoOutMeta = TxtTL("Meta", op, 124f, -106f, 228f, 22f, 13f, Muted, false);
        w.infoOutDesc = TxtTL("Desc", op, 28f, -162f, 324f, 100f, 14f, Hex("A9A49D"), false, true);
        w.infoOutKw = TxtTL("Keywords", op, 28f, -274f, 250f, 44f, 15f, TextC, false);
        var sIcon = Icon("ShardIcon", op, 290f, -280f, 22f);
        UIUtilSet(sIcon, shardSprite);
        w.infoOutShard = TxtTL("Shard", op, 318f, -276f, 50f, 30f, 18f, TextC, false);
        var hint = Txt("KeyHint", info, 14f, Muted, TextAlignmentOptions.Center, false);
        Center(hint.rectTransform, new Vector2(290f, -326f), new Vector2(380f, 24f));
        hint.text = "방향키 고르기     F 결정     Esc 포기";
        info.gameObject.SetActive(false);

        // ════════ 3-B 확인 창 ════════
        var src = PrefabUtility.LoadPrefabContents(SwapPrefabPath);
        RectTransform confirm;
        try
        {
            var win = src.transform.Find("Panel/Window");
            if (win == null || win.Find("Buttons") == null) { Debug.LogError("[인벤토리 v2] 교체 창 프리팹 구조가 다름 (Panel/Window/Buttons) — 중단"); return; }
            confirm = New("SwapConfirm", root);
            confirm.anchorMin = Vector2.zero; confirm.anchorMax = Vector2.one; confirm.offsetMin = confirm.offsetMax = Vector2.zero;
            var cimg = confirm.gameObject.AddComponent<Image>();
            cimg.color = Hex("070707");   // [수정] 완전히 가림 (뒤 교체 화면이 비쳤음) cimg.raycastTarget = true;
            var title = Txt("Title", confirm, 34f, Gold, TextAlignmentOptions.Center, true);
            Center(title.rectTransform, new Vector2(0f, 398f), new Vector2(800f, 50f));
            title.text = "교체하시겠습니까?";

            // 버림 (왼쪽, 흐리게)
            var ocBack = New("OldCardBack", confirm);   // 흐린 카드 뒤 불투명 바닥
            Center(ocBack, new Vector2(-290f, 50f), new Vector2(400f, 560f));
            var ocbi = ocBack.gameObject.AddComponent<Image>(); ocbi.color = Hex("070707"); ocbi.raycastTarget = false;
            var oc = New("OldCard", confirm);
            Center(oc, new Vector2(-290f, 50f), new Vector2(400f, 560f));
            oc.gameObject.AddComponent<Image>();
            oc.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f;
            PanelLook(oc, Color.white, new Color(0.91f, 0.9f, 0.88f, 0.4f), 22f, 46f);
            HeadLabel(oc, "버 림", Muted, 18f, 46f);
            CardBody(oc, out w.confirmOldIcon, out w.confirmOldName, out w.confirmOldMeta, out w.confirmOldDesc, out w.confirmOldKw, Hex("A9A49D"));
            w.confirmOldIcon.color = new Color(0.62f, 0.62f, 0.62f, 1f);   // 버릴 것 — 어둡게
            // 획득 (오른쪽, 보라 테)
            var nc = New("NewCard", confirm);
            Center(nc, new Vector2(290f, 50f), new Vector2(400f, 560f));
            nc.gameObject.AddComponent<Image>();
            PanelLook(nc, new Color(0.86f, 0.78f, 1f, 1f), Purple, 22f, 46f);
            HeadLabel(nc, "획 득", Purple, 18f, 46f);
            CardBody(nc, out w.confirmNewIcon, out w.confirmNewName, out w.confirmNewMeta, out w.confirmNewDesc, out w.confirmNewKw, Hex("CFCBC4"));
            // 화살표 · 파편
            var arrow = Txt("Arrow", confirm, 48f, TextC, TextAlignmentOptions.Center, false);
            Center(arrow.rectTransform, new Vector2(0f, 80f), new Vector2(120f, 60f));
            arrow.text = "→";
            var ci = Icon("ShardIcon", confirm, 0f, 0f, 26f);
            Center(ci.rectTransform, new Vector2(-22f, 30f), new Vector2(26f, 26f));
            UIUtilSet(ci, shardSprite);
            w.confirmInfo = Txt("Shard", confirm, 20f, TextC, TextAlignmentOptions.Left, false);
            Center(w.confirmInfo.rectTransform, new Vector2(24f, 30f), new Vector2(60f, 30f));
            w.confirmInfo.text = "+0";
            // 버튼
            var buttons = Clone(win.Find("Buttons"), confirm, "Buttons");
            Center(buttons, new Vector2(0f, -320f), new Vector2(464f, 60f));
            var hl = buttons.GetComponent<HorizontalLayoutGroup>() ?? buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 24f; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = buttons.GetComponent<ContentSizeFitter>(); if (fit != null) Object.DestroyImmediate(fit);
            foreach (Transform b in buttons)
            {
                ItemSwapBuilder.StripCard(b);
                Pref(b.gameObject, 220f, 60f);
                var t = b.Find("Label")?.GetComponent<TMP_Text>();
                if (t != null) { t.fontSize = 24f; if (b.name == "CancelButton") t.text = "취소"; if (b.name == "SwapButton") t.text = "교체"; }
            }
            w.confirmYes = buttons.Find("SwapButton")?.GetComponent<Button>();
            w.confirmNo = buttons.Find("CancelButton")?.GetComponent<Button>();
            var ch = Txt("KeyHint", confirm, 14f, Muted, TextAlignmentOptions.Center, false);
            Center(ch.rectTransform, new Vector2(0f, -378f), new Vector2(600f, 24f));
            ch.text = "← → 선택     F 결정     Esc 취소";
            w.confirmHint = ch;
            confirm.gameObject.SetActive(false);
        }
        finally { PrefabUtility.UnloadPrefabContents(src); }

        // 판 겹침 방지 이웃 연결 (같은 화면에 함께 뜨는 판끼리)
        var group = new[] { tab, status, kwPanel, np, op };
        foreach (var cl in clips)
        {
            var ns = new List<RectTransform>();
            bool inGroup = System.Array.IndexOf(group, cl.box) >= 0;
            if (inGroup) { foreach (var g in group) if (g != cl.box) ns.Add(g); }
            else foreach (var o in clips) if (o != cl && System.Array.IndexOf(group, o.box) < 0) ns.Add(o.box);   // 확인 카드끼리
            cl.neighbors = ns.ToArray();
        }

        // ════════ 연결 ════════
        var dim = root.Find("SwapDim");
        if (dim == null)
        {
            var d = New("SwapDim", root);
            d.anchorMin = Vector2.zero; d.anchorMax = Vector2.one; d.offsetMin = d.offsetMax = Vector2.zero;
            var di = d.gameObject.AddComponent<Image>(); di.color = new Color(0.03f, 0.03f, 0.03f, 0.92f);
            d.gameObject.SetActive(false);
            dim = d;
        }
        dim.SetAsFirstSibling();
        confirm.SetAsLastSibling();

        Undo.RecordObject(w, "인벤토리 v2");
        w.useInventory = true;
        w.dim = dim.gameObject;
        w.panel = notice != null ? notice.gameObject : null;
        w.hideWhileSwapping = new[] { status.gameObject, kwPanel.gameObject, bar.gameObject, headText.gameObject };
        w.centerDuringSwap = new RectTransform[0];
        w.centerShiftX = 0f;
        w.showWhileSwapping = new[] { info.gameObject };
        w.swapHint = hint;
        w.swapRect = tab;
        w.swapPos = new Vector2(-200f, 0f);
        w.swapSize = new Vector2(480f, 920f);
        w.swapGrid = grid;
        w.swapCell = new Vector2(176f, 176f);
        w.swapSpacing = new Vector2(24f, 24f);
        w.swapColumns = 2;
        w.swapPadTop = 100;
        w.confirmRoot = confirm.gameObject;
        w.confirmOld = null; w.confirmNew = null;

        var invUI = Object.FindObjectOfType<InventoryUI>(true);
        if (invUI != null)
        {
            Undo.RecordObject(invUI, "인벤토리 v2");
            invUI.countText = headText;
            invUI.emptyLabel = null;
            invUI.emptyLabelName = "";
        }
        else Debug.LogWarning("[인벤토리 v2] InventoryUI 를 못 찾아 머리글 숫자 연결 못 함");
        var inv = Object.FindObjectOfType<Inventory>(true);
        if (inv != null) { Undo.RecordObject(inv, "인벤토리 v2"); inv.swapWindow = w; }

        var scene = tab.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log("[인벤토리 v2] 1-B · 2-A · 3-B 적용 완료" + (saved ? " — 씬 저장함" : " — 씬 저장 실패, 직접 저장해 주세요"));
    }

    // ── 판 모양: 기존 창 그림(inventori.png — 상점 · 게임오버 창과 같은 그림)을 9분할로 늘려 씀 ──
    //  [19:40 수정] 처음엔 단색 + 선을 직접 그렸으나, 이미 있는 그림을 쓰도록 바꿈.
    //  그림 바깥 투명 여백(선 바깥)이 있어서, 판 크기(선 안쪽 상자) 밖으로 여백만큼 늘린 자식 'Back' 에 그림을 깐다.
    const string WindowSpriteGuid = "ac566161f1a3d3549836248e606ea9c6";
    const float SpriteScale = 1.73f;   // pixelsPerUnitMultiplier — 머리띠가 시안 높이에 가깝게
    const float RawL = 130f, RawR = 116f, RawT = 113f, RawB = 117f;   // 그림 가장자리 → 선까지 (원본 px)
    const float RawBorder = 140f, RawH = 1934f, RawHeadBottom = 206f;
    static float lastHeadCenter = 27f;   // 마지막 판의 머리띠 가운데 (상자 위에서 아래로 px)
    static Sprite windowSprite;
    static readonly List<PanelFrameClip> clips = new List<PanelFrameClip>();

    static void PanelLook(RectTransform panel, Color tint, Color unused, float unused2, float unused3)
    {
        if (windowSprite == null)
            windowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(WindowSpriteGuid));
        var img = panel.GetComponent<Image>();
        if (img != null) { Undo.RecordObject(img, "인벤토리 v2"); if (img.sprite == null) { img.sprite = windowSprite; img.color = Color.white; } img.enabled = false; }   // 판 자체 그림은 끔 (늘어나 찌그러짐) — 지우지 않음
        var back = New("Back", panel);
        back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one;
        float m = SpriteScale;
        back.offsetMin = new Vector2(-RawL / m, -RawB / m);
        back.offsetMax = new Vector2(RawR / m, RawT / m);
        // [19:50] 겹침 방지: Back = 잘라내는 틀(RectMask2D + PanelFrameClip), 그림은 자식 Art
        back.gameObject.AddComponent<RectMask2D>();
        var clip = back.gameObject.AddComponent<PanelFrameClip>();
        clip.box = panel;
        clips.Add(clip);
        var art = New("Art", back);
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = art.offsetMax = Vector2.zero;
        var bi = art.gameObject.AddComponent<Image>();
        bi.sprite = windowSprite;
        bi.type = Image.Type.Sliced;
        bi.fillCenter = true;
        bi.pixelsPerUnitMultiplier = m;
        bi.color = tint;
        bi.raycastTarget = true;
        back.SetAsFirstSibling();
        // 머리띠 가운데: 그림 위 테두리(140px)는 그대로, 그 아래는 판 높이에 따라 늘어남
        float h = panel.rect.height > 1f ? panel.rect.height : panel.sizeDelta.y;
        float full = h + (RawT + RawB) / m;
        float s = Mathf.Max(0f, full - 2f * RawBorder / m) / (RawH - 2f * RawBorder);
        float headBottom = RawBorder / m + (RawHeadBottom - RawBorder) * s - RawT / m;   // 선에서 머리띠 아래까지
        lastHeadCenter = Mathf.Max(12f, headBottom * 0.5f);
        if (windowSprite == null) Debug.LogWarning("[인벤토리 v2] 창 그림(inventori.png)을 못 찾음");
    }

    static void HeadLabel(RectTransform panel, string text, Color c, float size, float headH)
    {
        var t = Txt("HeadText", panel, size, c, TextAlignmentOptions.Center, false);
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -lastHeadCenter), new Vector2(300f, 40f));
        t.text = text;
    }

    // 확인 카드 안: 아이콘 150 · 이름 · 등급 · 능력 · 키워드 변화 (가운데 정렬)
    static void CardBody(RectTransform card, out Image icon, out TMP_Text name, out TMP_Text meta, out TMP_Text desc, out TMP_Text kw, Color descColor)
    {
        var ic = New("Icon", card);
        Place(ic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(150f, 150f));
        icon = ic.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        name = Txt("Name", card, 22f, TextC, TextAlignmentOptions.Center, true);
        Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(332f, 32f));
        meta = Txt("Meta", card, 13f, Muted, TextAlignmentOptions.Center, false);
        Place(meta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(332f, 22f));
        desc = Txt("Desc", card, 15f, descColor, TextAlignmentOptions.Top, false);
        Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(332f, 160f));
        desc.enableWordWrapping = true;   // [수정] 한 줄로 잘리던 것
        AutoSize(desc, 11f, 15f);
        kw = Txt("Keywords", card, 16f, TextC, TextAlignmentOptions.Center, false);
        Place(kw.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -480f), new Vector2(332f, 60f));
    }

    static Image Icon(string name, RectTransform parent, float x, float y, float size)
    {
        var r = New(name, parent);
        TL(r, x, y, size, size);
        var i = r.gameObject.AddComponent<Image>(); i.preserveAspect = true; i.raycastTarget = false;
        return i;
    }

    static void UIUtilSet(Image i, Sprite s) { i.sprite = s; i.enabled = s != null; }

    static TMP_Text TxtTL(string name, RectTransform parent, float x, float y, float w, float h, float size, Color c, bool bold, bool auto = false)
    {
        var t = Txt(name, parent, size, c, TextAlignmentOptions.TopLeft, bold);
        TL(t.rectTransform, x, y, w, h);
        t.enableWordWrapping = true;
        if (auto) AutoSize(t, 11f, size);
        return t;
    }

    static void AutoSize(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    // 상태 판 글자를 복제해 새 글자 (한글 글꼴 · 재질 그대로)
    static TMP_Text Txt(string name, Transform parent, float size, Color c, TextAlignmentOptions align, bool bold)
    {
        var go = Object.Instantiate(font.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "인벤토리 v2");
        foreach (Transform ch in go.transform) Object.DestroyImmediate(ch.gameObject);
        foreach (var comp in go.GetComponents<Component>())
            if (!(comp is RectTransform) && !(comp is TMP_Text) && !(comp is CanvasRenderer)) Object.DestroyImmediate(comp);
        var t = go.GetComponent<TMP_Text>();
        t.text = "";
        t.fontSize = size; t.color = c; t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.enableAutoSizing = false;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.richText = true;
        var rt = (RectTransform)go.transform;
        rt.localScale = Vector3.one; rt.localEulerAngles = Vector3.zero;
        return t;
    }

    // ItemSlot 프리팹에 칸 아래 이름 · 오른쪽 위 별 (인벤토리 칸에서만 보임 — ItemSlotUI)
    static void SlotLabels()
    {
        var pr = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            var ui = pr.GetComponent<ItemSlotUI>();
            if (ui == null) { Debug.LogWarning("[인벤토리 v2] ItemSlot 프리팹에 ItemSlotUI 가 없음"); return; }
            foreach (var n in new[] { "Name", "Stars" }) { var o = pr.transform.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
            var name = Txt("Name", pr.transform, 14f, TextC, TextAlignmentOptions.Center, false);
            Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(178f, 22f));
            name.overflowMode = TextOverflowModes.Ellipsis;
            var stars = Txt("Stars", pr.transform, 12f, Gold, TextAlignmentOptions.TopRight, false);
            Place(stars.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -5f), new Vector2(60f, 18f));
            ui.nameText = name; ui.starsText = stars;
            name.gameObject.SetActive(false); stars.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(pr, SlotPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(pr); }
    }

    // ── 배치 도우미 ──
    static void Center(RectTransform r, Vector2 pos, Vector2 size)
    {
        Place(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
        r.sizeDelta = size; r.anchoredPosition = pos;
    }

    static void TL(Transform t, float x, float y, float w, float h)
    {
        var r = t as RectTransform;
        if (r == null) return;
        Undo.RecordObject(r, "인벤토리 v2");
        Place(r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
        if (r.localScale == Vector3.zero) r.localScale = Vector3.one;
    }

    static RectTransform New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "인벤토리 v2");
        return (RectTransform)go.transform;
    }

    static RectTransform Clone(Transform src, Transform parent, string name)
    {
        var go = Object.Instantiate(src.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "인벤토리 v2");
        return (RectTransform)go.transform;
    }

    static void Off(Transform t)
    {
        if (t == null || !t.gameObject.activeSelf) return;
        Undo.RecordObject(t.gameObject, "인벤토리 v2");
        t.gameObject.SetActive(false);
    }

    static void Pref(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = w; le.preferredHeight = le.minHeight = h;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }

    static Transform FindInScene(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded) return null;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
