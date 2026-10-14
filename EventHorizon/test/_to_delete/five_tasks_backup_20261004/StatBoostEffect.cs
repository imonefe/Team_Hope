using UnityEngine;

/// <summary>
/// 영구 스탯 변화
/// 버프: 양수값 / 디버프: 음수값
/// </summary>
[CreateAssetMenu(fileName = "New StatBoostEffect", menuName = "Game/Effect/StatBoost")]
public class StatBoostEffect : ItemEffect
{
    public StatModifier modifier;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        modifier.Apply(stats);
    }
}
