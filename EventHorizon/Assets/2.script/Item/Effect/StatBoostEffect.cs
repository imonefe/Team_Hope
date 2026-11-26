using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영구 스탯 변화
/// 버프: 양수값 / 디버프: 음수값
/// </summary>
[CreateAssetMenu(fileName = "New StatBoostEffect", menuName = "Game/Effect/StatBoost")]
public class StatBoostEffect : ItemEffect, IAugmentLevelEffect
{
    public StatModifier modifier;
    [Tooltip("[2026-11-26] 사이버웨어 스탯 끔(AugmentRules.AugmentStatBoost = false)이어도 이 효과는 켜 둠 — 능력의 일부인 단점 (예: 세 시간의 말미 제로포인트 -60%)")]
    public bool keepWhenAugmentStatsOff;

    // [2026-11-26] 사이버웨어에서 스탯 증가를 뺌 (스탯은 스탯 방 카드로) — AugmentRules.AugmentStatBoost 하나로 켜고 끔
    //  단, 제로포인트 지속시간은 사이버웨어에서만 조절하므로 끈 상태에서도 그 값만은 남긴다
    public StatModifier Effective => AugmentRules.AugmentStatBoost || keepWhenAugmentStatsOff ? modifier
        : new StatModifier { pauseDuration = modifier.pauseDuration, pauseDurationPercent = modifier.pauseDurationPercent };

    /// <summary>실제로 적용되는 값이 하나라도 있는지</summary>
    public bool Active
    {
        get
        {
            if (AugmentRules.AugmentStatBoost || keepWhenAugmentStatsOff) return true;
            return !Mathf.Approximately(modifier.pauseDuration, 0f) || !Mathf.Approximately(modifier.pauseDurationPercent, 0f);
        }
    }

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        if (!Active) return;
        Effective.Apply(stats);
    }

    // [2026-11-21] 증강 강화: 주 수치가 Lv2 ×1.5, Lv3 ×2 가 되도록 늘어난 배율만큼 한 번 더 적용
    public void OnAugmentLevelChanged(Stats stats, MonoBehaviour runner, int oldLevel, int newLevel)
    {
        if (!Active) return;
        float extra = AugmentRules.Multiplier(newLevel) - AugmentRules.Multiplier(oldLevel);
        if (stats == null || extra <= 0f) return;
        Scaled(Effective, extra).Apply(stats);
    }

    // [2026-11-21] 증강 판매: 단계 배율만큼 들어간 수치를 뺀다 (퍼센트 항목은 지금 능력치 기준이라 근삿값)
    public override void Revert(Stats stats, MonoBehaviour runner, int level)
    {
        if (stats == null || !Active) return;
        var m = Scaled(Effective, AugmentRules.Multiplier(Mathf.Max(1, level)));
        var snap = new StatSnapshot
        {
            atk = m.atk + stats.Atk * m.atkPercent,
            def = m.def + stats.Def * m.defPercent,
            spd = m.spd + stats.spd * m.spdPercent,
            pauseDuration = m.pauseDuration + stats.PauseDuration * m.pauseDurationPercent,
            critChance = m.critChance,
            critMultiplier = m.critMultiplier,
            hp = m.hp + Mathf.RoundToInt(stats.maxHp * m.hpPercent)
        };
        StatModifier.Remove(stats, snap);
    }

    static StatModifier Scaled(StatModifier m, float k)
    {
        m.hp = Mathf.RoundToInt(m.hp * k);
        m.atk *= k; m.def *= k; m.spd *= k; m.pauseDuration *= k;
        m.critChance *= k; m.critMultiplier *= k;
        m.atkPercent *= k; m.defPercent *= k; m.spdPercent *= k; m.pauseDurationPercent *= k; m.hpPercent *= k;
        return m;
    }

    // 예) "공격력 +6", "이동속도 +1" — 스탯 하나당 한 항목
    protected override void AppendDescription(List<string> lines)
    {
        if (!Active) return;
        EffectText.AddStatLines(Effective, lines);
    }
}
