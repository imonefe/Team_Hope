using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-23 추가] 인벤토리 오른쪽 위에 세로로 늘어선 키워드 아이콘 8개.
///  - 보유 증강의 키워드 개수를 아이콘 오른쪽 아래 숫자로 보여 준다 (없으면 흐리게)
///  - 아이콘에 마우스를 올리면 증강 정보 팝업(ItemInfoPopup)으로 키워드 설명이 뜬다
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

    class Slot { public CanvasGroup group; public TMP_Text count; }
    readonly List<Slot> slots = new List<Slot>();
    Inventory hooked;

    void OnEnable()
    {
        Build();
        if (Inventory.Instance != null && hooked != Inventory.Instance) { hooked = Inventory.Instance; hooked.OnInventoryChanged += Refresh; }
        Refresh();
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
            slots.Add(new Slot { group = group, count = count });
        }
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
