using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [2026-11-23 추가] 게임오버 · 엔딩 화면의 '획득한 증강' 칸 하나.
/// 마우스를 올리면 인벤토리와 같은 증강 정보 팝업(ItemInfoPopup)을 띄운다.
/// GameOverUI 가 칸을 복제할 때 붙여 준다.
/// </summary>
public class GameOverAugmentCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ItemData item;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null) ItemInfoPopup.Instance?.Show(item, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemInfoPopup.Instance?.Hide();
    }

    void OnDisable()
    {
        ItemInfoPopup.Instance?.Hide();
    }
}
