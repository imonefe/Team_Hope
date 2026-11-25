using System.Collections;
using UnityEngine;

/// <summary>
/// N초 동안 스탯 변화 후 원복
/// StatSnapshot 으로 정확한 원복 보장
/// repeat: false → 1회 / true → cooldown 후 반복
/// </summary>
[CreateAssetMenu(fileName = "New TimedEffect", menuName = "Game/Effect/Timed")]
public class TimedEffect : RepeatingStatEffect
{
    protected override IEnumerator RunOnce(Stats stats)
    {
        // 적용 후 스냅샷 저장
        StatSnapshot snap = modifier.Apply(stats);

        yield return new WaitForSeconds(duration);

        // 스냅샷으로 정확히 원복
        StatModifier.Remove(stats, snap);
    }
}
