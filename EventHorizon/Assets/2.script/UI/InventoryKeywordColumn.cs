using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-23 추가] 인벤토리 오른쪽 위에 세로로 늘어선 키워드 아이콘 8개.
///  - 보유 증강의 키워드 개수를 아이콘 오른쪽 아래 숫자로 보여 준다 (없으면 흐리게)
///  - 아이콘에 마우스를 올리면 증강 정보 팝업(ItemInfoPopup)으로 키워드 설명이 뜬다
///  - [2026-11-26] 키워드 필터: 아이콘에 마우스를 올리면 그 키워드가 없는 칸을 어둡게 (미리 보기), 클릭하면 고정 · 다시 클릭하면 해제
/// 견본 칸(KeywordIcon, 꺼 둠)은 씬에 있고, 키워드 수만큼 복제해 쓴다.
/// </summary>
public class InventoryKeywordColumn : MonoBehaviour
{
    [Tooltip("칸 견본 (꺼 둠). 자기 Image = 칸 테두리, 자식 Icon(Image) · Count(TMP)")]
    public RectTransform template;
    [Tooltip("키워드 아이콘 (과열 · 균열 · 잔상 · 연격 · 처형 · 강탈 · 정지 · 과부하 순서)")]
    public Sprite[] icons = new Sprite[8];
    [Tooltip("칸 사이 간격")]
    public float spacing = 10f;
    [Tooltip("보유하지 않은 키워드의 진하기")]
    [Range(0f, 1f)] public float emptyAlpha = 0.3f;

    [Header("키워드 필터 (2026-11-26)")]
    [Tooltip("아이콘에 마우스를 올리면 미리 보기, 클릭하면 고정 — 그 키워드가 없는 칸을 어둡게")]
    public bool useFilter = true;
    [Tooltip("필터에 안 맞는 칸(빈 칸 포함)의 진하기")]
    [Range(0f, 1f)] public float filterDimAlpha = 0.12f;   // [2026-11-28] 0.3 → 0.12 (더 어둡게)
    [Tooltip("고정된 키워드 아이콘 뒤 사각형 진하기 (키워드 색)")]
    [Range(0f, 1f)] public float pinnedBackAlpha = 0.25f;
    [Tooltip("모듈 칸들이 들어가는 Content. 비우면 InventoryUI 의 것을 찾아 씀")]
    public Transform slotContent;

    class Slot { public CanvasGroup group; public TMP_Text count; public Image back; }
    readonly List<Slot> slots = new List<Slot>();
    Inventory hooked;
    int hoverK = -1, pinK = -1, appliedK = -2;

    void OnEnable()
    {
        Build();
        if (Inventory.Instance != null && hooked != Inventory.Instance) { hooked = Inventory.Instance; hooked.OnInventoryChanged += Refresh; }
        Refresh();
    }

    // 창을 닫으면 필터 풀기 (다시 열면 전체가 보이게)
    void OnDisable()
    {
        hoverK = -1; pinK = -1; appliedK = -2;
        UpdatePinnedBack();
    }

    void OnDestroy()
    {
        if (hooked != null) hooked.OnInventoryChanged -= Refresh;
    }

    void Build()
    {
        if (slots.Count > 0 || template == null) return;
        template.gameObject.SetActive(false);
        float h = template.rect.height;
        for (int k = 0; k < 8; k++)
        {
            var rt = Instantiate(template, template.parent);
            rt.gameObject.SetActive(true);
            rt.name = "Keyword_" + AugmentRules.KeywordName((AugmentKeyword)k);
            rt.anchoredPosition = new Vector2(template.anchoredPosition.x, template.anchoredPosition.y - k * (h + spacing));
            var icon = rt.Find("Icon")?.GetComponent<Image>();
            Sprite sp = icons != null && k < icons.Length ? icons[k] : null;
            if (icon != null) { icon.sprite = sp; icon.enabled = sp != null; icon.raycastTarget = false; }
            var count = rt.Find("Count")?.GetComponent<TMP_Text>();
            if (count != null) count.raycastTarget = false;
            var group = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            var hover = rt.gameObject.AddComponent<KeywordIconHover>();
            hover.keyword = (AugmentKeyword)k;
            hover.icon = sp;
            // [2026-11-26] 필터: 견본 칸의 Image(투명)를 고정 표시용 배경으로 씀
            var filter = rt.gameObject.AddComponent<KeywordFilterTarget>();
            filter.column = this; filter.keyword = k;
            slots.Add(new Slot { group = group, count = count, back = rt.GetComponent<Image>() });
        }
    }

    // ══════════ [2026-11-26] 키워드 필터 ══════════
    public void OnIconEnter(int k) { if (useFilter && !ItemSwapWindow.IsOpen) hoverK = k; }   // [18:21] 교체 중에는 거르기 안 함
    public void OnIconExit(int k) { if (hoverK == k) hoverK = -1; }
    public void OnIconClick(int k)
    {
        if (!useFilter || ItemSwapWindow.IsOpen) return;
        pinK = pinK == k ? -1 : k;   // 같은 걸 다시 누르면 풀림, 다른 걸 누르면 바뀜
        UpdatePinnedBack();
    }

    void UpdatePinnedBack()
    {
        for (int k = 0; k < slots.Count; k++)
        {
            var b = slots[k].back;
            if (b == null) continue;
            Color c = Color.white;
            ColorUtility.TryParseHtmlString("#" + AugmentRules.KeywordHex((AugmentKeyword)k), out c);
            c.a = k == pinK ? pinnedBackAlpha : 0f;
            b.color = c;
        }
    }

    // 칸은 InventoryUI 가 목록이 바뀔 때마다 새로 만들므로 매 프레임 진하기를 맞춘다 (칸 수십 개라 가벼움)
    void LateUpdate()
    {
        int k = useFilter ? (hoverK >= 0 ? hoverK : pinK) : -1;
        if (slotContent == null)
        {
            var ui = FindObjectOfType<InventoryUI>();
            if (ui != null) slotContent = ui.generalContent;
            if (slotContent == null) return;
        }
        if (k < 0 && appliedK == -1) return;   // 필터 없음 + 이미 다 밝힘
        for (int i = 0; i < slotContent.childCount; i++)
        {
            var slot = slotContent.GetChild(i);
            var ui = slot.GetComponent<ItemSlotUI>();
            if (ui == null) continue;
            float a = k < 0 || HasKeyword(ui.Item, k) ? 1f : filterDimAlpha;
            // [2026-11-28] 마우스를 올린 동안(고정 클릭 말고) 그 키워드를 가진 칸 프레임에 키워드 색
            ui.SetKeywordTint(hoverK >= 0 && k == hoverK && HasKeyword(ui.Item, k) ? AugmentFx.KwColor((AugmentKeyword)k) : (Color?)null);
            var g = slot.GetComponent<CanvasGroup>();
            if (g == null) { if (a >= 1f) continue; g = slot.gameObject.AddComponent<CanvasGroup>(); }
            g.alpha = a;
        }
        appliedK = k < 0 ? -1 : k;
    }

    static bool HasKeyword(ItemData d, int k)
    {
        if (d == null || d.keywords == null) return false;
        foreach (var kw in d.keywords) if ((int)kw == k) return true;
        return false;
    }

    void Refresh()
    {
        if (slots.Count == 0) return;
        int[] n = AugmentRules.CountKeywords(Inventory.Instance != null ? Inventory.Instance.passives : null);
        for (int k = 0; k < slots.Count && k < n.Length; k++)
        {
            var s = slots[k];
            if (s.group != null) s.group.alpha = n[k] > 0 ? 1f : emptyAlpha;
            if (s.count != null)
            {
                s.count.text = n[k] > 0 ? n[k].ToString() : "";
                Color c;
                if (ColorUtility.TryParseHtmlString("#" + AugmentRules.KeywordHex((AugmentKeyword)k), out c)) s.count.color = c;
            }
        }
    }
}

/// <summary>[2026-11-26] 키워드 아이콘 한 칸의 마우스 올림 · 클릭을 InventoryKeywordColumn 필터로 전달 (실행 중 자동으로 붙음)</summary>
public class KeywordFilterTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public InventoryKeywordColumn column;
    public int keyword;
    public void OnPointerEnter(PointerEventData e) { if (column != null) column.OnIconEnter(keyword); }
    public void OnPointerExit(PointerEventData e) { if (column != null) column.OnIconExit(keyword); }
    public void OnPointerClick(PointerEventData e) { if (column != null) column.OnIconClick(keyword); }
}
