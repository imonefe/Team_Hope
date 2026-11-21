using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 증강 키워드 8종 (증강 기획서 '키워드 8개와 단계 능력')
/// </summary>
public enum AugmentKeyword
{
    [InspectorName("과열")] Overheat,
    [InspectorName("균열")] Fracture,
    [InspectorName("잔상")] Afterimage,
    [InspectorName("연격")] Combo,
    [InspectorName("처형")] Execute,
    [InspectorName("강탈")] Plunder,
    [InspectorName("정지")] Stasis,
    [InspectorName("과부하")] Overload,
}

/// <summary>
/// [2026-11-21 추가] 증강 고유능력 강화 규칙 (증강 기획서 '개요' 표의 값).
///  - 증강은 얻을 때 Lv1, 상점(강화 NPC)에서 증강 파편으로 Lv2 → Lv3
///  - 고유능력 주 수치: Lv1 ×1, Lv2 ×1.5, Lv3 ×2
///  - 강화 비용(파편): 일반 2/4 · 희귀 3/6 · 영웅 5/10 · 전설 8/16
/// </summary>
public static class AugmentRules
{
    public const int MaxLevel = 3;

    /// <summary>단계별 주 수치 배율</summary>
    public static float Multiplier(int level)
    {
        if (level >= 3) return 2f;
        if (level == 2) return 1.5f;
        return level <= 0 ? 0f : 1f;
    }

    /// <summary>[2026-11-21] 증강 판매가 (증강 파편): 등급 기본값 + 강화에 쓴 파편의 절반</summary>
    public static int SellShards(ItemTier tier, int level)
    {
        int b = tier == ItemTier.Legendary ? 6 : tier == ItemTier.Epic ? 4 : tier == ItemTier.Rare ? 2 : 1;
        int spent = 0;
        for (int l = 1; l < level; l++) spent += Mathf.Max(0, UpgradeCost(tier, l));
        return b + spent / 2;
    }

    /// <summary>지금 단계(level)에서 다음 단계로 올리는 비용. 최대 단계면 -1</summary>
    public static int UpgradeCost(ItemTier tier, int level)
    {
        if (level >= MaxLevel) return -1;
        int baseCost;
        switch (tier)
        {
            case ItemTier.Legendary: baseCost = 8; break;
            case ItemTier.Epic: baseCost = 5; break;
            case ItemTier.Rare: baseCost = 3; break;
            default: baseCost = 2; break;
        }
        return level <= 1 ? baseCost : baseCost * 2;
    }

    /// <summary>증강 등급 이름 (기획서 용어: 일반 · 희귀 · 영웅 · 전설). 스킬 상점은 ShopColors.TierName 그대로</summary>
    public static string TierName(ItemTier t)
    {
        switch (t)
        {
            case ItemTier.Legendary: return "전설";
            case ItemTier.Epic: return "영웅";
            case ItemTier.Rare: return "희귀";
            default: return "일반";
        }
    }

    public static string KeywordName(AugmentKeyword k)
    {
        switch (k)
        {
            case AugmentKeyword.Overheat: return "과열";
            case AugmentKeyword.Fracture: return "균열";
            case AugmentKeyword.Afterimage: return "잔상";
            case AugmentKeyword.Combo: return "연격";
            case AugmentKeyword.Execute: return "처형";
            case AugmentKeyword.Plunder: return "강탈";
            case AugmentKeyword.Stasis: return "정지";
            default: return "과부하";
        }
    }

    /// <summary>키워드 색 (기획서 시안과 같은 값)</summary>
    public static string KeywordHex(AugmentKeyword k)
    {
        switch (k)
        {
            case AugmentKeyword.Overheat: return "E8542A";
            case AugmentKeyword.Fracture: return "9B7BFF";
            case AugmentKeyword.Afterimage: return "4FC3E8";
            case AugmentKeyword.Combo: return "F2C230";
            case AugmentKeyword.Execute: return "D23A4A";
            case AugmentKeyword.Plunder: return "3FBF8A";
            case AugmentKeyword.Stasis: return "6E8BFF";
            default: return "FF7AB8";
        }
    }

    public const string AbilityHex = "F0A040";   // 고유능력 이름 색 (시안)
    public const string MutedHex = "9A9288";

    /// <summary>강화 단계 별 (Lv1 ★☆☆ · Lv2 ★★☆ · Lv3 ★★★)</summary>
    public static string Stars(int level)
    {
        level = Mathf.Clamp(level, 0, MaxLevel);
        return new string('★', level) + new string('☆', MaxLevel - level);
    }

    /// <summary>단계 값 하나 (Lv1~3). 없으면 빈 글자</summary>
    public static string LevelValue(ItemData d, int level)
    {
        if (d == null || d.levelValues == null || level < 1 || level > d.levelValues.Length) return "";
        return d.levelValues[level - 1] ?? "";
    }

    /// <summary>"10% → 30% → 50%" — 지금 단계(level)를 금색 굵게. level 0 = 강조 없음</summary>
    public static string LevelLine(ItemData d, int level)
    {
        if (d == null || d.levelValues == null || d.levelValues.Length == 0) return "";
        var parts = new string[d.levelValues.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string v = d.levelValues[i] ?? "";
            parts[i] = (i + 1 == level) ? $"<color=#E3C27E><b>{v}</b></color>" : v;
        }
        return string.Join(" → ", parts);
    }

    /// <summary>[2026-11-21] 등급 글자 오른쪽에 강화 단계와 별 ("희귀  Lv1 ★☆☆")</summary>
    public static string TierWithLevel(ItemTier t, int level)
        => level > 0 ? $"{TierName(t)}  <color=#E3C27E>Lv{level} {Stars(level)}</color>" : TierName(t);

    /// <summary>증강 설명 한 덩어리 (팝업 · 인벤토리 · 상점 공용): 효과 + 강화 값 + 키워드.
    /// [2026-11-21] 화면에는 아이템 이름만 쓰고 고유능력 이름은 보이지 않음 (abilityName 은 코드가 증강을 구분하는 데만 씀)</summary>
    public static string Describe(ItemData d, int level)
    {
        if (d == null) return "";
        if (string.IsNullOrEmpty(d.abilityText)) return d.description;
        var sb = new System.Text.StringBuilder();
        sb.Append(d.abilityText);
        string lv = LevelLine(d, level);
        if (!string.IsNullOrEmpty(lv)) sb.Append($"\n<color=#{MutedHex}>강화  {lv}</color>");
        string kw = KeywordsRich(d);
        if (!string.IsNullOrEmpty(kw)) sb.Append($"\n키워드  {kw}");
        return sb.ToString();
    }

    /// <summary>키워드 단계 (1개 · 3개 · 5개). 0이면 아직 없음</summary>
    public static int KeywordTier(int count) => count >= 5 ? 3 : count >= 3 ? 2 : count >= 1 ? 1 : 0;

    /// <summary>보유 증강들의 키워드 개수 (키워드 순서대로 8칸)</summary>
    public static int[] CountKeywords(IEnumerable<ItemData> items)
    {
        var n = new int[8];
        if (items == null) return n;
        foreach (var d in items)
            if (d != null && d.keywords != null)
                foreach (var k in d.keywords) n[(int)k]++;
        return n;
    }

    /// <summary>[키워드, 단계(0=1개,1=3개,2=5개)] 짧은 설명 (증강 기획서 '키워드 8개와 단계 능력')</summary>
    public static readonly string[,] KeywordTierText =
    {
        { "모든 공격이 10% 확률로 과열 1스택", "과열이 절반으로 줄기까지 +2초", "과열 5스택 이상인 적 처치 시 폭발" },
        { "치명타가 균열 1스택을 붙인다", "균열 최대 4스택", "균열 최대 스택인 적은 받는 피해 +20%" },
        { "대시하면 출발 자리에 잔상 (1초 · 공격력 40%)", "잔상 유지 +1초", "잔상이 적을 맞히면 잔상 1개 추가" },
        { "연격 유지 시간 +0.5초", "끊길 때 절반만 잃음", "연격 10스택에서 공격 속도 상한 +30%" },
        { "처형 대상에게 피해 +5%", "처형 기준 +5%p", "처형 대상 처치 시 퍼즈 게이지 +10" },
        { "처치하면 탈취 데이터 1개", "탈취 데이터 최대 5개", "탈취 데이터를 쓸 때 50% 확률로 남음" },
        { "퍼즈 게이지 충전 +10%", "퍼즈가 풀릴 때 화면 안 일반 적 0.5초 경직", "퍼즈 지속 +1초, 퍼즈 중 치명타 +20%p" },
        { "체력 50% 이하일 때 받는 피해 -5%", "과부하로 잃은 체력 20%를 방 정리 때 회복", "체력 25% 이하에서 공격력 +25%" },
    };

    /// <summary>"<color=#..>과열</color> · <color=#..>균열</color>" 형태 (키워드 없으면 빈 글자)</summary>
    public static string KeywordsRich(ItemData d)
    {
        if (d == null || d.keywords == null || d.keywords.Length == 0) return "";
        var parts = new string[d.keywords.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = $"<color=#{KeywordHex(d.keywords[i])}>{KeywordName(d.keywords[i])}</color>";
        return string.Join(" · ", parts);
    }
}

/// <summary>
/// [2026-11-21 추가] 증강 단계가 바뀔 때 알림을 받는 효과.
/// 고유능력 효과 에셋(ItemEffect)이 이걸 구현하면, 강화 창에서 단계를 올릴 때 불린다.
/// </summary>
public interface IAugmentLevelEffect
{
    void OnAugmentLevelChanged(Stats stats, MonoBehaviour runner, int oldLevel, int newLevel);
}
