using UnityEngine;

public class npcTrigger : ProximityInteractable
{
    [Tooltip("대화 캔버스에 붙은 NPC 스크립트")]
    public NPC npcScript;

    PlayerController nearbyPlayer;

    // 외부에서 호출 가능
    public void TriggerStory(PlayerController player)
    {
        if (npcScript != null)
            npcScript.SetCondition(null, player);
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동. (참고: 기존엔 Player 태그여도 PlayerController 컴포넌트가
    // 없으면 playerNearby 자체가 안 켜졌는데, 지금은 playerNearby는 켜지되 nearbyPlayer가 null로
    // 남아서 CanInteract()가 막아준다 — 동작 결과는 동일함)
    protected override bool CanInteract() => nearbyPlayer != null && (npcScript == null || !npcScript.IsTalking);

    protected override void Interact()
    {
        TriggerStory(nearbyPlayer);
    }

    protected override void OnPlayerEnter(Collider2D other)
    {
        nearbyPlayer = other.GetComponent<PlayerController>();
    }

    protected override void OnPlayerExit(Collider2D other)
    {
        nearbyPlayer = null;
    }
}
