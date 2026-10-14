using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리 슬롯 하나. 아이콘 표시 + 클릭/호버 이벤트를 InventoryUI로 전달한다.
/// 슬롯 프리팹(ItemSlot)에 이 스크립트를 붙이고, iconImage에 자식 아이콘 Image를 연결한다.
///
/// (퍼즈 투척 아이템 지원 기능은 제거됨 — 퍼즈 아이템 시스템 자체가 게임에서 빠지면서 함께 제거)
/// </summary>
public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("아이콘을 표시할 자식 Image (슬롯 배경 말고 아이콘용)")]
    public Image iconImage;

    ItemData itemData;

    InventoryUI owner;

    void Awake()
    {
        // 슬롯 호버/클릭 연출
        UIButtonJuice.Attach(this);
    }

    /// <summary>일반 아이템으로 세팅</summary>
    public void SetItem(ItemData data, InventoryUI ownerUI)
    {
        itemData = data;
        owner = ownerUI;
        ApplyIcon(data != null ? data.icon : null);
    }

    void ApplyIcon(Sprite sprite)
    {
        UIUtil.SetIcon(iconImage, sprite);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        if (itemData != null) owner.ShowItemDetail(itemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 인벤토리 전용 hoverPopup 대신, 공용 ItemInfoPopup 싱글턴을 사용 —
        // 이래야 월드 근접 팝업이랑 같은 UI를 재사용할 수 있음.
        if (itemData != null) ItemInfoPopup.Instance?.Show(itemData, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemInfoPopup.Instance?.Hide();
    }
}
