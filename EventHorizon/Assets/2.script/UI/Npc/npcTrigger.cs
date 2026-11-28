using UnityEngine;

public class npcTrigger : ProximityInteractable
{
    [Tooltip("대화 캔버스에 붙은 NPC 스크립트")]
    public NPC npcScript;

    [Tooltip("[2026-10-18 추가] 말을 걸 때마다 이 순서대로 대사 세트(conditionKey)를 고른다. 끝까지 가면 처음으로. 비워 두면 예전처럼 첫 번째 대사 세트")]
    public string[] conditionCycle;
    [Tooltip("[2026-11-28] 2스테이지(StagePlan 그룹 2S)일 때 위 목록 대신 쓰는 대사 세트 순서. 비우면 스테이지 상관없이 위 목록")]
    public string[] stage2ConditionCycle;

    PlayerController nearbyPlayer;
    int cycleIndex;

    // 외부에서 호출 가능
    public void TriggerStory(PlayerController player)
    {
        if (npcScript == null) return;

        // [2026-10-18 추가] 순환 목록이 있으면 다음 대사 세트로
        string key = null;
        string[] cycle = conditionCycle;
        if (stage2ConditionCycle != null && stage2ConditionCycle.Length > 0
            && StageFlow.Active && StageFlow.CurrentEntry != null && StageFlow.CurrentEntry.group == "2S")
            cycle = stage2ConditionCycle;
        if (cycle != null && cycle.Length > 0)
        {
            key = cycle[cycleIndex % cycle.Length];
            cycleIndex = (cycleIndex + 1) % cycle.Length;
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
