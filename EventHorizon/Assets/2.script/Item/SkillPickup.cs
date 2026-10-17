using UnityEngine;

/// <summary>
/// 스킬 획득용 월드 오브젝트.
/// skillPrefab은 CharacterSkills.skillSlots에 들어가는 것과 같은 Skill 프리팹을 그대로 참조하면 된다.
///
/// [2026-10-17 변경] 아이템(Item.cs)처럼 F키로 줍는 방식 추가
///  - pickupWithKey 켜짐(기본): 가까이 가면 머리 위에 스킬 정보 팝업 → F키로 획득
///  - pickupWithKey 꺼짐: 예전처럼 닿는 순간 바로 획득 (PlayerController.OnTriggerEnter2D에서 처리)
///  프리팹 구성은 아이템 프리팹과 같다: 본체(그림 + 바닥 충돌 + Rigidbody2D + 이 스크립트)
///  + 자식 PickupZone(줍기 범위, Is Trigger 켠 원형 콜라이더)
/// </summary>
public class SkillPickup : ProximityInteractable
{
    [Tooltip("획득 시 CharacterSkills.AcquireSkill()에 그대로 전달될 스킬 프리팹")]
    public Skill skillPrefab;

    [Tooltip("켜면 F키로 줍기 + 가까이 가면 스킬 정보 팝업. 끄면 닿는 순간 바로 획득")]
    public bool pickupWithKey = true;

    bool popupShown;

    protected override bool CanInteract() => pickupWithKey && skillPrefab != null;

    protected override void Interact()
    {
        if (playerObj == null) return;

        CharacterSkills skills = playerObj.GetComponent<CharacterSkills>();
        if (skills == null) skills = playerObj.GetComponentInParent<CharacterSkills>();
        if (skills == null) return;

        // 일반 스킬 칸이 가득 차 있으면 AcquireSkill 이 false — 그대로 바닥에 남겨 둔다
        if (skills.AcquireSkill(skillPrefab))
        {
            HidePopup();
            Destroy(gameObject);
        }
    }

    // 범위 진입 — 플레이어 머리 위에 스킬 정보 팝업
    protected override void OnPlayerEnter(Collider2D other)
    {
        if (!pickupWithKey || skillPrefab == null || ItemInfoPopup.Instance == null) return;
        ItemInfoPopup.Instance.ShowNearSkill(skillPrefab, playerObj.transform);
        popupShown = true;
    }

    protected override void OnPlayerExit(Collider2D other)
    {
        HidePopup();
    }

    void OnDestroy()
    {
        HidePopup();
    }

    void HidePopup()
    {
        if (!popupShown) return;
        popupShown = false;
        ItemInfoPopup.Instance?.Hide();
    }
}
