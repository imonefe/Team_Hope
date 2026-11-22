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

    /// <summary>증강 설명 한 덩어리 (인벤토리 상세 · 상점 · 강화 창 공용): 고유능력 줄 + 문장 + 제한 + 키워드.
    /// [2026-11-23] 증강 서술 규칙 — 고유능력 이름과 발동 조건을 보여 주고, 단계 값은 문장 안에서 지금 단계만 밝게</summary>
    public static string Describe(ItemData d, int level)
    {
        if (d == null) return "";
        if (string.IsNullOrEmpty(d.abilityText)) return d.description;
        var sb = new System.Text.StringBuilder();
        sb.Append(AbilityHeader(d)).Append('\n').Append(AbilityBody(d, level));
        string lim = LimitLine(d, level);
        if (lim.Length > 0) sb.Append('\n').Append(lim);
        string kw = KeywordsRich(d);
        if (!string.IsNullOrEmpty(kw)) sb.Append($"\n<size=85%><color=#{MutedHex}>키워드</color>  {kw}</size>");
        return sb.ToString();
    }

    // ══════════ [2026-11-23 추가] 증강 서술 규칙 ══════════
    //  문장 안 [과열] → 키워드 색,  {10%/30%/50%} → 지금 단계 값만 밝게 (나머지는 흐리게)

    public const string ValueDimHex = "8A857B";
    static readonly System.Text.RegularExpressions.Regex KwTag = new System.Text.RegularExpressions.Regex(@"\[([^\]]+)\]");
    static readonly System.Text.RegularExpressions.Regex LvTag = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");

    /// <summary>문장 속 낱말 → 키워드 (탈취 데이터 = 강탈, 처형 대상 = 처형). 모르는 낱말이면 -1</summary>
    public static int KeywordOfWord(string w)
    {
        if (string.IsNullOrEmpty(w)) return -1;
        if (w.Contains("탈취 데이터")) return (int)AugmentKeyword.Plunder;
        if (w.StartsWith("처형")) return (int)AugmentKeyword.Execute;
        for (int i = 0; i < 8; i++) if (w.StartsWith(KeywordName((AugmentKeyword)i))) return i;
        return -1;
    }

    /// <summary>[키워드] 와 {단계 값} 을 색 글자로 바꾼다. level 0 이면 단계 값을 모두 같은 밝기로</summary>
    public static string Rich(string text, int level)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string s = KwTag.Replace(text, m =>
        {
            int k = KeywordOfWord(m.Groups[1].Value);
            return k < 0 ? m.Groups[1].Value : $"<color=#{KeywordHex((AugmentKeyword)k)}>{m.Groups[1].Value}</color>";
        });
        s = LvTag.Replace(s, m =>
        {
            string[] parts = m.Groups[1].Value.Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = level <= 0 ? parts[i]
                         : (i + 1 == level ? $"<color=#FFFFFF><b>{parts[i]}</b></color>" : $"<color=#{ValueDimHex}>{parts[i]}</color>");
            return string.Join($"<color=#{ValueDimHex}> / </color>", parts);
        });
        return s;
    }

    /// <summary>고유능력 줄: 이름(주황) + 발동 조건 칩</summary>
    public static string AbilityHeader(ItemData d)
    {
        if (d == null || string.IsNullOrEmpty(d.abilityName)) return "";
        string trig = string.IsNullOrEmpty(d.triggerText) ? "" : $"  <size=78%><mark=#FFFFFF16><color=#E8E3D9> {d.triggerText} </color></mark></size>";
        return $"<color=#{AbilityHex}><b>{d.abilityName}</b></color>{trig}";
    }

    /// <summary>고유능력 문장 (단계 값 자리가 없는 옛 글이면 아래에 '강화 10% → 30% → 50%' 줄을 붙임)</summary>
    public static string AbilityBody(ItemData d, int level)
    {
        if (d == null) return "";
        if (string.IsNullOrEmpty(d.abilityText)) return d.description ?? "";
        string body = Rich(d.abilityText, level);
        if (d.abilityText.IndexOf('{') < 0)
        {
            string lv = LevelLine(d, level);
            if (!string.IsNullOrEmpty(lv)) body += $"\n<color=#{MutedHex}>강화  {lv}</color>";
        }
        return body;
    }

    /// <summary>제한 줄 (재사용 대기 · 횟수 · 대상 제외). 없으면 빈 글자</summary>
    public static string LimitLine(ItemData d, int level)
        => d == null || string.IsNullOrEmpty(d.limitText) ? "" : $"<size=85%><color=#99938A>{Rich(d.limitText, level)}</color></size>";

    /// <summary>문장 속 단계 값 자리가 있는지 (있으면 따로 '강화' 줄을 안 보여도 됨)</summary>
    public static bool HasInlineLevels(ItemData d) => d != null && !string.IsNullOrEmpty(d.abilityText) && d.abilityText.IndexOf('{') >= 0;

    /// <summary>키워드 진행 줄: "강탈 3  3/5 · 다음 탈취 데이터 최대 5개" (counts = 보유 키워드 개수 8칸)</summary>
    public static string KeywordProgress(ItemData d, int[] counts)
    {
        if (d == null || d.keywords == null || d.keywords.Length == 0 || counts == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var k in d.keywords)
        {
            int n = counts[(int)k];
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"<color=#{KeywordHex(k)}><b>{KeywordName(k)} {n}</b></color>  ");
            int next = n < 1 ? 1 : n < 3 ? 3 : n < 5 ? 5 : 0;
            if (next > 0) sb.Append($"<color=#{MutedHex}>{n}/{next} · 다음</color> {KeywordTierText[(int)k, next == 1 ? 0 : next == 3 ? 1 : 2]}");
            else sb.Append($"<color=#{MutedHex}>최고 단계 ·</color> {KeywordTierText[(int)k, 2]}");
        }
        return sb.ToString();
    }

    /// <summary>키워드가 무엇인지 한 줄 (키워드 설명 팝업 위)</summary>
    public static readonly string[] KeywordWhat =
    {
        "적에게 쌓는 불씨 스택", "적에게 새기는 금", "대시가 남기는 분신", "헤네시의 타격 리듬",
        "마무리 대상 표시", "탈취 데이터 (헤네시 머리 위 결정)", "제로포인트 (시간 정지)", "헤네시의 남은 체력",
    };

    /// <summary>키워드 기본 기능 (코드 값 그대로)</summary>
    public static readonly string[] KeywordBase =
    {
        "0.5초마다 스택당 공격력 2% 지속 피해. 4초 동안 새로 안 쌓이면 절반으로 줄어든다. 5스택 이상이면 '과열 폭발' 계열의 조건이 된다.",
        "스택당 받는 피해 +5%. 최대 3스택(키워드 3개: 4), 6초 유지. '균열 최대'가 여러 증강의 조건.",
        "대시하면 생기고, 닿은 적에게 피해를 준다. 잔상 피해·지속·개수를 늘리는 증강과 '잔상이 맞히면' 조건이 있다.",
        "맞힐 때마다 +1, 최대 10. 2초 안에 다시 맞히지 못하면 끊긴다. 스택 수가 조건·배율로 쓰인다.",
        "체력 30% 이하인 적이 '처형 대상'(키워드 3개: 35%). 대상에게 피해·치명타·즉시 처치 효과.",
        "처치하면 1개, 최대 3개(키워드 3개: 5). 가득 차면 '방출'이 시작돼 머리 위 결정이 빛나고, 방출 중에만 데이터를 쓰는 증강이 켜진다. 0개가 되면 다시 모은다(방출 중엔 2초마다 1개씩 줄어듦).",
        "게이지가 차면 Q로 5초 동안 시간을 멈춘다. '제로포인트 중' · '풀릴 때' 효과와 게이지 충전 효과.",
        "체력이 낮을수록(50% · 30% · 25%) 강해지거나 회복한다. 체력을 써서 힘을 얻는 증강도 있다.",
    };

    /// <summary>키워드 설명 팝업의 본문: 무엇인지 + 기본 기능</summary>
    public static string KeywordPopupBody(AugmentKeyword k)
        => $"<color=#{MutedHex}>{KeywordWhat[(int)k]}</color>\n{KeywordBase[(int)k]}";

    /// <summary>키워드 설명 팝업의 아래 칸: 1 · 3 · 5개 단계 (달성한 단계는 밝게) + 이 키워드를 가진 보유 증강</summary>
    public static string KeywordPopupSteps(AugmentKeyword k, int count, IEnumerable<ItemData> owned)
    {
        var sb = new System.Text.StringBuilder();
        int[] steps = { 1, 3, 5 };
        for (int i = 0; i < 3; i++)
        {
            bool on = count >= steps[i];
            if (i > 0) sb.Append('\n');
            sb.Append(on ? $"<color=#{KeywordHex(k)}>●</color> " : $"<color=#{MutedHex}>○</color> ");
            sb.Append(on ? $"<b>{steps[i]}개</b>  {KeywordTierText[(int)k, i]}" : $"<color=#{MutedHex}>{steps[i]}개  {KeywordTierText[(int)k, i]}</color>");
        }
        if (owned != null)
        {
            var names = new List<string>();
            foreach (var d in owned)
                if (d != null && d.keywords != null && System.Array.IndexOf(d.keywords, k) >= 0) names.Add(d.itemName);
            if (names.Count > 0) sb.Append($"\n<size=85%><color=#{MutedHex}>보유  </color>{string.Join(" · ", names)}</size>");
        }
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
        { "처형 대상에게 피해 +5%", "처형 기준 +5%p", "처형 대상 처치 시 제로포인트 게이지 +10" },
        { "처치하면 탈취 데이터 1개", "탈취 데이터 최대 5개", "탈취 데이터를 쓸 때 50% 확률로 남음" },
        { "제로포인트 게이지 충전 +10%", "제로포인트가 풀릴 때 화면 안 일반 적 0.5초 경직", "제로포인트 지속 +1초, 제로포인트 중 치명타 +20%p" },
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
