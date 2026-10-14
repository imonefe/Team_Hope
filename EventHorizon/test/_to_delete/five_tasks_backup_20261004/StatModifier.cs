using System;
using UnityEngine;

/// <summary>
/// 스탯 변화 구조체
/// Apply() 가 StatSnapshot 을 반환 → Remove 시 정확한 원복 보장
/// </summary>
[Serializable]
public struct StatModifier
{
    [Header("고정값 (음수 = 디버프)")]
    public int hp;
    public float atk;
    public float def;
    public float spd;
    public float focus;

    [Header("퍼센트 (0.1 = 10%, 음수 = 디버프)")]
    public float atkPercent;
    public float defPercent;
    public float spdPercent;
    public float focusPercent;
    public float hpPercent;

    /// <summary>
    /// 스탯 적용 후 스냅샷 반환
    /// 스냅샷을 저장해뒀다가 Remove 에 전달하면 정확히 원복됨
    /// </summary>
    public StatSnapshot Apply(Stats stats)
    {
        var snap = new StatSnapshot();

        // 고정값 + 퍼센트 합산해서 스냅샷에 저장 (나눗셈 없이 정확한 원복 가능)
        snap.atk = atk + stats.Atk * atkPercent;
        snap.def = def + stats.Def * defPercent;
        snap.spd = spd + stats.spd * spdPercent;
        snap.focus = focus + stats.Focus * focusPercent;
        snap.hp = hp + Mathf.RoundToInt(stats.maxHp * hpPercent);

        stats.Atk += snap.atk;
        stats.Def += snap.def;
        stats.spd += snap.spd;
        stats.itemSpdBonus += snap.spd;
        stats.Focus += snap.focus;

        // 최대체력 자체를 늘림 (Heal()은 기존 최대치까지만 채우고 최대치는 안 늘어나서 버그였음)
        if (snap.hp != 0)
            stats.IncreaseMaxHp(snap.hp);

        return snap;
    }

    /// <summary>
    /// Apply() 에서 받은 스냅샷으로 정확히 원복
    /// </summary>
    public static void Remove(Stats stats, StatSnapshot snap)
    {
        stats.Atk -= snap.atk;
        stats.Def -= snap.def;
        stats.spd -= snap.spd;
        stats.itemSpdBonus -= snap.spd;
        stats.Focus -= snap.focus;

        if (snap.hp != 0)
            stats.IncreaseMaxHp(-snap.hp);
    }
}

/// <summary>
/// Apply() 시 실제로 적용된 값 저장용
/// Remove() 에 전달해서 정확한 원복에 사용
/// </summary>
public struct StatSnapshot
{
    public float atk;
    public float def;
    public float spd;
    public float focus;
    public int hp;
}