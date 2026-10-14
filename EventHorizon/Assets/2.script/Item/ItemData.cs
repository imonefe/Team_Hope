using UnityEngine;

public enum ItemType { Consumable, Equipment, Weapon }
public enum ItemTier { Common, Rare, Epic, Legendary }

// (원소 속성 시스템 — ElementType enum, element/power 필드 — 은 제거됨 — 속성 시스템 자체가 게임에서 빠지면서 함께 제거)

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
}
