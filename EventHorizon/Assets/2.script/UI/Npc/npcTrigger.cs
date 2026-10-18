using UnityEngine;

public class npcTrigger : ProximityInteractable
{
    [Tooltip("대화 캔버스에 붙은 NPC 스크립트")]
    public NPC npcScript;

    [Tooltip("[2026-10-18 추가] 말을 걸 때마다 이 순서대로 대사 세트(conditionKey)를 고른다. 끝까지 가면 처음으로. 비워 두면 예전처럼 첫 번째 대사 세트")]
    public string[] conditionCycle;

    PlayerController nearbyPlayer;
    int cycleIndex;

    // 외부에서 호출 가능
    public void TriggerStory(PlayerController player)
    {
        if (npcScript == null) return;

        // [2026-10-18 추가] 순환 목록이 있으면 다음 대사 세트로
        string key = null;
        if (conditionCycle != null && conditionCycle.Length > 0)
        {
            key = conditionCycle[cycleIndex % conditionCycle.Length];
            cycleIndex = (cycleIndex + 1) % conditionCycle.Length;
        }
        npcScript.SetCondition(key, player);
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동. (참고: 기존엔 Player 태그여도 PlayerController 컴포넌트가
    // 없으면 playerNearby 자체가 안 켜졌는데, 지금은 playerNearby는 켜지되 nearbyPlayer가 null로
    // 남아서 CanInteract()가 막아준다 — 동작 결과는 동일함)
    // [2026-10-17 추가] 가까이 가면 머리 위에 "[F] 대화"
    protected override string PromptLabel => "대화";

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
