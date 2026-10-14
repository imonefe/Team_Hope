using System.Collections;
using UnityEngine;

/// <summary>
/// 시간이 지나면서 점차 효과가 줄어드는 효과
/// 고정값은 서서히 감소 / 퍼센트는 즉시 적용 후 duration 끝날 때 제거
/// repeat: false → 1회 / true → cooldown 후 반복
/// </summary>
[CreateAssetMenu(fileName = "New FadeEffect", menuName = "Game/Effect/Fade")]
public class FadeEffect : RepeatingStatEffect
{
    protected override string DurationSuffix => " 점점 감소";

    protected override IEnumerator RunOnce(Stats stats)
    {
        // 퍼센트 부분만 먼저 즉시 적용 (스냅샷 저장)
        StatModifier pctOnly = new StatModifier
        {
            atkPercent = modifier.atkPercent,
            defPercent = modifier.defPercent,
            spdPercent = modifier.spdPercent,
            focusPercent = modifier.focusPercent,
            hpPercent = modifier.hpPercent
        };
        StatSnapshot pctSnap = pctOnly.Apply(stats);

        // 고정값은 서서히 감소
        float lastAtk = modifier.atk;
        float lastDef = modifier.def;
        float lastSpd = modifier.spd;
        float lastFocus = modifier.focus;

        // 고정값 즉시 적용
        stats.Atk += lastAtk;
        stats.Def += lastDef;
        stats.spd += lastSpd;
        stats.itemSpdBonus += lastSpd;
        stats.Focus += lastFocus;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = 1f - (elapsed / duration);

            float newAtk = modifier.atk * ratio;
            float newDef = modifier.def * ratio;
            float newSpd = modifier.spd * ratio;
            float newFocus = modifier.focus * ratio;

            stats.Atk += newAtk - lastAtk;
            stats.Def += newDef - lastDef;
            stats.spd += newSpd - lastSpd;
            stats.itemSpdBonus += newSpd - lastSpd;
            stats.Focus += newFocus - lastFocus;

            lastAtk = newAtk;
            lastDef = newDef;
            lastSpd = newSpd;
            lastFocus = newFocus;

            yield return null;
        }

        // 고정값 잔여 완전 제거
        stats.Atk -= lastAtk;
        stats.Def -= lastDef;
        stats.spd -= lastSpd;
        stats.itemSpdBonus -= lastSpd;
        stats.Focus -= lastFocus;

        // 퍼센트 스냅샷으로 정확히 원복
        StatModifier.Remove(stats, pctSnap);
    }
}
