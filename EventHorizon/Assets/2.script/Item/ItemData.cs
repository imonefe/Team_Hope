using UnityEngine;

public enum ItemType { Consumable, Equipment, Weapon }
public enum ItemTier { Common, Rare, Epic, Legendary }

[CreateAssetMenu(fileName = "New ItemData", menuName = "Game/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string   itemName;
    public string   description;
    public Sprite   icon;
    public ItemType itemType;
    public ItemTier tier;

    [Header("효과 목록")]
    public ItemEffect[] effects;

    // [2026-11-21 추가] 증강 키워드 1~2개 (증강 기획서). 비워 둬도 됨
    [Header("증강 키워드 (2026-11-21)")]
    public AugmentKeyword[] keywords;

    // [2026-11-21 추가] 증강 고유능력 (증강 기획서 전환표). 화면에는 이게 주 설명, description 은 설정 한 줄
    [Header("증강 고유능력 (2026-11-21)")]
    [Tooltip("고유능력 이름 (예: 연쇄 발화)")]
    public string abilityName;
    [Tooltip("고유능력 효과 설명")]
    [TextArea(2, 4)] public string abilityText;
    [Tooltip("강화 단계별 값 Lv1 / Lv2 / Lv3 (예: 10% / 30% / 50%)")]
    public string[] levelValues = new string[3];

    // [2026-11-23 추가] 증강 서술 규칙 (조건 · 문장 · 제한 · 역할)
    //  abilityText 안의 [과열] 은 키워드 색, {10%/30%/50%} 는 지금 단계 값만 밝게 보인다.
    [Header("증강 서술 (2026-11-23)")]
    [Tooltip("언제 켜지는지 — 짧은 칩 글자 (예: 맞힐 때, 처치, 대시, 제로포인트 중)")]
    public string triggerText;
    [Tooltip("얼마나 자주 — 재사용 대기 · 횟수 · 대상 제외 (예: 2초에 한 번 · 보스 제외). 없으면 비움")]
    public string limitText;
    [Tooltip("주고받는 것 — '+데이터'(만든다) '-데이터'(쓴다) '?과열'(있어야 켜짐) 같은 짧은 태그. 함께 작동 문장에 씀")]
    public string[] roles;
}
