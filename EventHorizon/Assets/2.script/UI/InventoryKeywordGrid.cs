using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-26 v2 · 시안 1-B] 인벤토리 오른쪽 아래 키워드 판 (4칸 × 2줄).
///  칸마다 아이콘 · "과열 3" · 점 5개 (3~5번째는 마름모 = 단계 기준 3 · 4 · 5개). 보유 0 이면 흐리게.
///  - 마우스를 올리면 키워드 설명 팝업 (KeywordIconHover)
///  - 키워드 필터: 올리면 미리 보기, 누르면 고정 · 다시 누르면 해제 — 그 키워드가 없는 인벤토리 칸을 어둡게
///  칸 8개는 편집기에서 만들어 둔 것 (메뉴 EventHorizon/인벤토리 시안 v2 적용). 자식 이름: Icon · Label · Pips/P0~P4
/// </summary>
public class InventoryKeywordGrid : MonoBehaviour
{
    [Tooltip("키워드 순서(과열 · 균열 · 잔상 · 연격 · 처형 · 강탈 · 정지 · 과부하)대로 8칸")]
    public RectTransform[] cells = new RectTransform[8];
    [Tooltip("빈 점 색")]
    public Color pipOff = new Color(0.18f, 0.17f, 0.157f, 1f);   // #2E2B28
    [Range(0f, 1f)] public float emptyAlpha = 0.3f;
    [Header("키워드 필터")]
    public bool useFilter = true;
    [Range(0f, 1f)] public float filterDimAlpha = 0.12f;   // [2026-11-28] 0.3 → 0.12 (더 어둡게)
    [Tooltip("고정한 칸 바탕 색 진하기 (키워드 색)")]
    [Range(0f, 1f)] public float pinnedBackAlpha = 0.22f;
    [Tooltip("모듈 칸들이 들어가는 Content (InventoryUI.generalContent)")]
    public Transform slotContent;

    class Cell { public CanvasGroup group; public TMP_Text label; public Image[] pips; public Image back; public Color backBase; }
    readonly List<Cell> list = new List<Cell>();
    Inventory hooked;
    int hoverK = -1, pinK = -1;
    bool dimmed;

    void OnEnable()
    {
        Build();
        if (Inventory.Instance != null && hooked != Inventory.Instance) { hooked = Inventory.Instance; hooked.OnInventoryChanged += Refresh; }
        Refresh();
    }

    void OnDisable()
    {
        hoverK = -1; pinK = -1;
        UpdateBacks();
        ApplyFilter(-1);   // 창이 닫히거나 교체 화면으로 바뀌면 어둡게 한 칸을 되돌림
    }

    void OnDestroy() { if (hooked != null) hooked.OnInventoryChanged -= Refresh; }

    void Build()
    {
        if (list.Count > 0 || cells == null) return;
        for (int k = 0; k < cells.Length; k++)
        {
            var rt = cells[k];
            var c = new Cell();
            if (rt != null)
            {
                c.group = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
                c.label = rt.Find("Label")?.GetComponent<TMP_Text>();
                c.back = rt.GetComponent<Image>();
                if (c.back != null) c.backBase = c.back.color;
                var pips = rt.Find("Pips");
                c.pips = new Image[pips != null ? pips.childCount : 0];
                for (int i = 0; i < c.pips.Length; i++) c.pips[i] = pips.GetChild(i).GetComponent<Image>();
                var t = rt.GetComponent<KeywordGridTarget>() ?? rt.gameObject.AddComponent<KeywordGridTarget>();
                t.grid = this; t.keyword = k;
            }
            list.Add(c);
        }
    }

    void Refresh()
    {
        if (list.Count == 0) return;
        int[] n = AugmentRules.CountKeywords(Inventory.Instance != null ? Inventory.Instance.passives : null);
        for (int k = 0; k < list.Count && k < n.Length; k++)
        {
            var c = list[k];
            Color kc;
            if (!ColorUtility.TryParseHtmlString("#" + AugmentRules.KeywordHex((AugmentKeyword)k), out kc)) kc = Color.white;
            if (c.group != null) c.group.alpha = n[k] > 0 ? 1f : emptyAlpha;
            if (c.label != null)
            {
                c.label.text = AugmentRules.KeywordName((AugmentKeyword)k) + (n[k] > 0 ? " " + n[k] : "");
                c.label.color = kc;
            }
            if (c.pips != null)
                for (int i = 0; i < c.pips.Length; i++) if (c.pips[i] != null) c.pips[i].color = i < n[k] ? kc : pipOff;
        }
    }

    // ══════════ 키워드 필터 ══════════
    public void OnEnter(int k) { if (useFilter && !ItemSwapWindow.IsOpen) hoverK = k; }
    public void OnExit(int k) { if (hoverK == k) hoverK = -1; }
    public void OnClick(int k)
    {
        if (!useFilter || ItemSwapWindow.IsOpen) return;
        pinK = pinK == k ? -1 : k;
        UpdateBacks();
    }

    void UpdateBacks()
    {
        for (int k = 0; k < list.Count; k++)
        {
            var b = list[k].back;
            if (b == null) continue;
            if (k == pinK)
            {
                Color c;
                ColorUtility.TryParseHtmlString("#" + AugmentRules.KeywordHex((AugmentKeyword)k), out c);
                c.a = pinnedBackAlpha;
                b.color = Color.Lerp(list[k].backBase, c, pinnedBackAlpha * 3f);
            }
            else b.color = list[k].backBase;
        }
    }

    void LateUpdate()
    {
        int k = useFilter ? (hoverK >= 0 ? hoverK : pinK) : -1;
        if (k < 0 && !dimmed) return;
        ApplyFilter(k);
    }

    // 칸은 InventoryUI 가 목록이 바뀔 때마다 새로 만들므로 매 프레임 맞춤 (8칸이라 가벼움)
    void ApplyFilter(int k)
    {
        if (slotContent == null) { var ui = FindObjectOfType<InventoryUI>(true); if (ui != null) slotContent = ui.generalContent; }
        if (slotContent == null) return;
        for (int i = 0; i < slotContent.childCount; i++)
        {
            var slot = slotContent.GetChild(i);
            var ui = slot.GetComponent<ItemSlotUI>();
            if (ui == null) continue;
            float a = k < 0 || Has(ui.Item, k) ? 1f : filterDimAlpha;
            // [2026-11-28] 마우스를 올린 동안(고정 클릭 말고) 그 키워드를 가진 칸 프레임에 키워드 색
            ui.SetKeywordTint(hoverK >= 0 && k == hoverK && Has(ui.Item, k) ? AugmentFx.KwColor((AugmentKeyword)k) : (Color?)null);
            var g = slot.GetComponent<CanvasGroup>();
            if (g == null) { if (a >= 1f) continue; g = slot.gameObject.AddComponent<CanvasGroup>(); }
            g.alpha = a;
        }
        dimmed = k >= 0;
    }

    static bool Has(ItemData d, int k)
    {
        if (d == null || d.keywords == null) return false;
        foreach (var kw in d.keywords) if ((int)kw == k) return true;
        return false;
    }
}

/// <summary>[v2] 키워드 판 한 칸의 마우스 올림 · 클릭 → InventoryKeywordGrid 필터</summary>
public class KeywordGridTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public InventoryKeywordGrid grid;
    public int keyword;
    public void OnPointerEnter(PointerEventData e) { if (grid != null) grid.OnEnter(keyword); }
    public void OnPointerExit(PointerEventData e) { if (grid != null) grid.OnExit(keyword); }
    public void OnPointerClick(PointerEventData e) { if (grid != null) grid.OnClick(keyword); }
}
