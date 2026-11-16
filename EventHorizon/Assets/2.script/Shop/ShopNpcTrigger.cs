using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 상점 방의 NPC. 가까이 가면 머리 위에 "[F] 상점" 안내, F를 누르면 연결된 상점 화면을 연다.
///
/// 필요한 것: 이 오브젝트(또는 자식)에 Is Trigger 켠 Collider2D (플레이어가 들어오는 범위),
///           window 칸에 상점 화면(ItemShopWindow / SkillShopWindow / StatUpgradeWindow) 연결.
/// </summary>
public class ShopNpcTrigger : ProximityInteractable
{
    [Tooltip("F를 눌렀을 때 열 상점 화면 (같은 방 안의 상점 캔버스)")]
    public ShopWindow window;

    [Tooltip("머리 위 안내 문구 ([F] 뒤에 붙음)")]
    public string promptLabel = "상점";

    protected override string PromptLabel => promptLabel;

    protected override bool CanInteract() => window != null && !window.IsOpen && !ShopWindow.BlocksEscape;

    protected override void Interact()
    {
        window.Open();
    }
}
