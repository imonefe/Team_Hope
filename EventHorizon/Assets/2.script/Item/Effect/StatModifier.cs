using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    // [2026-11-16 수정] 집중력 → 퍼즈 지속시간으로 이름 변경 (기존 아이템 값은 그대로 이어받음)
    [FormerlySerializedAs("focus")] public float pauseDuration;
    [Header("치명타 (2026-11-16 추가)")]
    [Tooltip("치명타 확률 증감 (0.02 = +2%)")] public float critChance;
    [Tooltip("치명타 배율 증감 (0.1 = +0.1배)")] public float critMultiplier;

    [Header("퍼센트 (0.1 = 10%, 음수 = 디버프)")]
    public float atkPercent;
    public float defPercent;
    public float spdPercent;
    [FormerlySerializedAs("focusPercent")] public float pauseDurationPercent;
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
        snap.pauseDuration = pauseDuration + stats.PauseDuration * pauseDurationPercent;
        snap.critChance = critChance;
        snap.critMultiplier = critMultiplier;
        snap.hp = hp + Mathf.RoundToInt(stats.maxHp * hpPercent);

        stats.Atk += snap.atk;
        stats.Def += snap.def;
        stats.spd += snap.spd;
        stats.itemSpdBonus += snap.spd;
        stats.PauseDuration += snap.pauseDuration;
        stats.critChance += snap.critChance;
        stats.critMultiplier += snap.critMultiplier;

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
        stats.PauseDuration -= snap.pauseDuration;
        stats.critChance -= snap.critChance;
        stats.critMultiplier -= snap.critMultiplier;

        // [2026-10-10 수정] 최대 체력 보너스를 되돌릴 때 현재 체력까지 같이 깎아(IncreaseMaxHp(-값))
        // 사망 처리 없이 체력 0으로 살아남는 일이 있었다 → 최대치만 줄이고 현재 체력은 새 최대치에 맞춰 자르기만 한다
        if (snap.hp > 0)
            stats.DecreaseMaxHp(snap.hp);
        else if (snap.hp < 0)
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
    public float pauseDuration;
    public float critChance;
    public float critMultiplier;
    public int hp;
}