using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-26] 스탯 카드 한 장 — 스탯 방 보상. 사이버웨어처럼 고르지만 특수 능력 없이 스탯만 바뀌고, 인벤토리 칸을 쓰지 않는다.
///  값 칸은 사이버웨어와 같은 StatModifier (고정값 · 퍼센트, 음수 = 단점).
///  단점(음수)이 하나라도 있으면 '대가 카드' — 카드에 표시되고 StatCardPool 이 따로 뽑는다.
///  만들기: Create > Game/StatCard, 또는 메뉴 EventHorizon/스탯 카드 기본 세트 만들기
/// </summary>
[CreateAssetMenu(fileName = "New StatCard", menuName = "Game/StatCard")]
public class StatCardData : ScriptableObject
{
    public string cardName;
    [Tooltip("일반 · 희귀 · 영웅 (전설은 쓰지 않음)")]
    public ItemTier tier = ItemTier.Common;
    [Tooltip("비우면 카드에 그림 없음")]
    public Sprite icon;
    public StatModifier modifier;

    public bool IsTradeoff
    {
        get
        {
            var m = modifier;
            return m.hp < 0 || m.atk < 0 || m.def < 0 || m.spd < 0 || m.pauseDuration < 0 || m.critChance < 0 || m.critMultiplier < 0
                || m.atkPercent < 0 || m.defPercent < 0 || m.spdPercent < 0 || m.pauseDurationPercent < 0 || m.hpPercent < 0;
        }
    }

    const string Good = "E3C27E", Bad = "D9604C";

    /// <summary>카드에 쓸 줄들 — 장점은 금색, 단점은 붉은색 (장점 먼저)</summary>
    public string BodyText()
    {
        var lines = new List<string>();
        EffectText.AddStatLines(modifier, lines);
        var good = new List<string>();
        var bad = new List<string>();
        foreach (var l in lines)
        {
            bool neg = l.Contains(EffectText.DebuffColor);   // 값 부분은 EffectText 가 초록 / 빨강으로 칠함
            (neg ? bad : good).Add($"<color=#{(neg ? Bad : Good)}>{l}</color>");
        }
        good.AddRange(bad);
        return string.Join("\n", good);
    }

    public static string TradeoffMark => $"<color=#{Bad}>대가</color>";
}
