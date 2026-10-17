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
}
