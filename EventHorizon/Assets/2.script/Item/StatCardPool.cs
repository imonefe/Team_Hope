using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-26] 스탯 카드 후보 목록과 뽑는 규칙. Resources/StatCardPool 에 하나만 둔다 (씬 수정 없이 불러옴).
///  - 3장을 뽑는다. 장마다 tradeoffChance 확률로 대가 카드 목록에서 (한 번에 최대 maxTradeoff 장)
///  - 등급 확률은 일반 · 희귀 · 영웅 (보스 보상이 아니라 방 보상이라 전설 없음)
///  - 한 번에 같은 카드는 안 나오고, 판 안에서는 같은 카드가 다시 나올 수 있다 (스탯은 겹쳐 쌓임)
/// </summary>
[CreateAssetMenu(fileName = "StatCardPool", menuName = "Game/StatCardPool")]
public class StatCardPool : ScriptableObject
{
    public const string ResourcePath = "StatCardPool";

    public List<StatCardData> cards = new List<StatCardData>();
    [Header("등급 확률 % (일반 / 희귀 / 영웅)")]
    public float[] odds = { 60, 30, 10 };
    [Tooltip("한 장마다 대가 카드가 나올 확률 (0.2 = 20%)")]
    [Range(0f, 1f)] public float tradeoffChance = 0.2f;
    [Tooltip("한 번에 나오는 대가 카드 최대 장 수")]
    public int maxTradeoff = 1;

    static StatCardPool cached;
    public static StatCardPool Load()
    {
        if (cached == null) cached = Resources.Load<StatCardPool>(ResourcePath);
        return cached;
    }

    public List<StatCardData> Pick(int count)
    {
        var got = new List<StatCardData>();
        int trade = 0;
        for (int i = 0; i < count; i++)
        {
            bool wantTrade = trade < maxTradeoff && Random.value < tradeoffChance;
            var c = PickOne(got, wantTrade) ?? PickOne(got, !wantTrade);
            if (c == null) break;
            if (c.IsTradeoff) trade++;
            got.Add(c);
        }
        return got;
    }

    StatCardData PickOne(List<StatCardData> used, bool tradeoff)
    {
        var byTier = new List<StatCardData>[3];
        for (int t = 0; t < 3; t++) byTier[t] = new List<StatCardData>();
        foreach (var c in cards)
        {
            if (c == null || used.Contains(c) || c.IsTradeoff != tradeoff) continue;
            byTier[Mathf.Clamp((int)c.tier, 0, 2)].Add(c);
        }
        float total = 0f;
        for (int t = 0; t < 3; t++) if (byTier[t].Count > 0) total += Odd(t);
        if (total <= 0f)
        {
            for (int t = 0; t < 3; t++) if (byTier[t].Count > 0) return byTier[t][Random.Range(0, byTier[t].Count)];
            return null;
        }
        float r = Random.value * total;
        for (int t = 0; t < 3; t++)
        {
            if (byTier[t].Count == 0) continue;
            r -= Odd(t);
            if (r <= 0f) return byTier[t][Random.Range(0, byTier[t].Count)];
        }
        for (int t = 2; t >= 0; t--) if (byTier[t].Count > 0) return byTier[t][Random.Range(0, byTier[t].Count)];
        return null;
    }

    float Odd(int t) => odds != null && t < odds.Length ? Mathf.Max(0f, odds[t]) : 0f;
}
