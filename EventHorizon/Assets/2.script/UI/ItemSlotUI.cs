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

    /// <summary>[2026-11-26] 이 칸의 아이템 (빈 칸이면 null) — 인벤토리 키워드 필터가 읽음</summary>
    public ItemData Item => itemData;

    InventoryUI owner;

    // ── [2026-11-26 17:31] 교체 중 고른 칸 표시 (붉은 빛) ──
    static ItemData swapPick;
    static event System.Action SwapPickChanged;
    Image bg;
    Color bgBase;
    static readonly Color FocusTint = new Color(0.89f, 0.76f, 0.49f, 1f);   // [18:27] 키보드 · 마우스로 고른 칸 (확인 창 버튼과 같은 금색)
    bool bgSaved;

    /// <summary>교체 창이 고른 사이버웨어 (null = 없음) — 그 칸만 붉게</summary>
    public static void SetSwapPick(ItemData d)
    {
        swapPick = d;
        SwapPickChanged?.Invoke();
    }

    void OnEnable() { SwapPickChanged += ApplyPick; ApplyPick(); }
    void OnDisable() { SwapPickChanged -= ApplyPick; }

    void ApplyPick()
    {
        if (bg == null) bg = GetComponent<Image>();
        if (bg == null) return;
        if (!bgSaved) { bgBase = bg.color; bgSaved = true; }
        // [18:27] 교체 중 인벤토리에서 지금 고른 칸만 금색 (확인 창 · 새 사이버웨어 칸처럼 인벤토리 밖 칸은 제외)
        bool on = itemData != null && itemData == swapPick && owner != null && onClick == null;
        bg.color = on ? FocusTint : bgBase;
    }

    /// <summary>[2026-11-26] 눌렀을 때 대신 할 일 (교체 창의 칸 고르기). 비어 있으면 인벤토리 상세 보기</summary>
    public System.Action<ItemSlotUI> onClick;

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
        ApplyPick();
    }

    void ApplyIcon(Sprite sprite)
    {
        UIUtil.SetIcon(iconImage, sprite);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (onClick != null) { if (itemData != null) onClick(this); return; }
        if (ItemSwapWindow.IsOpen) { if (itemData != null) ItemSwapWindow.PickItem(itemData); return; }   // [17:31] 교체 중: 버릴 것 고르기
        if (owner == null) return;
        if (itemData != null) owner.ShowItemDetail(itemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ItemSwapWindow.IsOpen && owner != null && itemData != null) ItemSwapWindow.FocusItem(itemData);   // [18:27] 마우스를 올린 칸으로 지금 칸을 옮김
        // 인벤토리 전용 hoverPopup 대신, 공용 ItemInfoPopup 싱글턴을 사용 —
        // 이래야 월드 근접 팝업이랑 같은 UI를 재사용할 수 있음.
        if (itemData != null) ItemInfoPopup.Instance?.Show(itemData, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemInfoPopup.Instance?.Hide();
    }
}
