using System.Collections.Generic;
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

    // 예) "공격력 +6", "이동속도 +1" — 스탯 하나당 한 항목
    protected override void AppendDescription(List<string> lines)
    {
        EffectText.AddStatLines(modifier, lines);
    }
}
