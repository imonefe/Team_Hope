using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>[2026-11-23 추가] 키워드 아이콘 칸 — 마우스를 올리면 키워드 설명 팝업</summary>
public class KeywordIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public AugmentKeyword keyword;
    public Sprite icon;

    public void OnPointerEnter(PointerEventData eventData) => ItemInfoPopup.Instance?.ShowKeyword(keyword, icon);
    public void OnPointerExit(PointerEventData eventData) => ItemInfoPopup.Instance?.Hide();
    void OnDisable() => ItemInfoPopup.Instance?.Hide();
}
