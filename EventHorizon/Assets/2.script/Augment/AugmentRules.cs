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
            case AugmentKeyword.Afterimage: return "D4D7DC";   // [2026-11-23] 푸른빛 → 무채색 흰·회색
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
        string head = AbilityHeader(d);
        if (head.Length > 0) sb.Append(head).Append('\n');
        sb.Append(AbilityBody(d, level));
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
        // [2026-11-23] %M균열% · %M데이터% (−1 · −2) → 지금 최대치 숫자 (설명 규칙 '스택 풀차징 = M')
        if (text.IndexOf('%') >= 0 && text.Contains("%M"))
        {
            int fm = AugmentRuntime.FractureMax, dm = AugmentRuntime.DataMax;
            text = text.Replace("%M균열%", fm.ToString())
                       .Replace("%M데이터-1%", Mathf.Max(1, dm - 1).ToString())
                       .Replace("%M데이터-2%", Mathf.Max(1, dm - 2).ToString())
                       .Replace("%M데이터%", dm.ToString());
        }
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

    /// <summary>발동 조건 칩 한 줄 (회색 상자). 없으면 빈 문자열.
    /// [2026-11-24] 고유능력 이름(예: 정지 잔상)은 모든 증강에서 표시하지 않음 — abilityName 은 코드 안 구분용으로만 씀.
    ///   상자 안 좌우 여백은 줄바꿈 없는 공백 2칸씩, 아래 문장과의 간격은 작은 빈 줄로 띄움 (TMP 3.0.7 은 mark 여백 지정이 안 됨)</summary>
    public static string AbilityHeader(ItemData d)
    {
        if (d == null || string.IsNullOrEmpty(d.triggerText)) return "";
        const string pad = "  ";   // [2026-11-24] 줄바꿈 없는 공백은 글꼴에 없어 폭이 0이었음 → 일반 공백
        return $"<size=80%><mark=#FFFFFF1C><color=#E8E3D9>{pad}{d.triggerText}{pad}</color></mark></size>\n<size=30%> </size>";
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
        // [2026-11-23] 덕팔 정리본 그대로 (과열 피해 n% = 코드 값 2%)
        "부여된 적에게 0.5초마다 공격력의 2% 피해를 입힌다.\n4초 동안 부여되지 않으면 절반씩 사라진다.",
        "균열 1개당 받는 피해 + 5% 증가.\n6초 동안 유지된다.",
        "대시 사용 시 분신이 생기며 적에게 직접 영향을 준다.",
        "적을 공격했을 경우 1개 획득.\n2초 동안 공격하지 않으면 연격 값은 사라진다.",
        "일정 체력 이하인 적을 처형 대상으로 지정한다.",
        "적 처치 시 + 1. 강탈을 모두 얻었을 경우 '방출' 상태로 전환된다.\n해당 경우에 탈취 데이터를 소모하는 증강이 발동한다.",
        "제로포인트 게이지를 채우거나, 사용됐을 경우에 발동한다.",
        "체력이 낮을 경우에 발동한다.",
    };

    /// <summary>키워드 설명 팝업의 본문: 무엇인지 + 기본 기능</summary>
    public static string KeywordPopupBody(AugmentKeyword k) => KeywordBase[(int)k];   // [2026-11-23] '무엇인지' 회색 줄은 빼고 정리본만

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
        // [2026-11-23] 증강 설명 규칙 표에 맞춰 다시 씀 (~했을 경우 · ~라면 · 부여 · 얻는다 · + n% · ~다.)
        { "적을 공격했을 경우, 10% 확률로 과열이 적에게 1개 부여된다.", "과열이 절반으로 줄기까지 + 2초.", "과열이 5개 이상인 적을 처치했을 경우, 폭발한다." },
        { "적을 치명타로 공격했을 경우, 균열이 적에게 1개 부여된다.", "균열 최대치가 4가 된다.", "균열이 최대인 적이 받는 피해 + 20%." },
        { "대시했을 경우, 출발 자리에 잔상이 1초 동안 남아 공격력의 40% 피해를 준다.", "잔상 유지 시간 + 1초.", "잔상이 적을 공격했을 경우, 잔상을 1개 더 얻는다." },
        { "연격 유지 시간 + 0.5초.", "연격이 끊겼을 경우, 절반만 잃는다.", "연격이 10이라면, 공격 속도 상한 + 30%." },
        { "처형 대상에게 주는 피해 + 5%.", "처형 기준 + 5%.", "처형 대상을 처치했을 경우, 제로포인트 게이지 + 10." },
        { "적을 처치했을 경우, 탈취 데이터를 1개 얻는다.", "탈취 데이터 최대치가 5가 된다.", "탈취 데이터를 소모했을 경우, 50% 확률로 잃지 않는다." },
        { "제로포인트 게이지 충전 + 10%.", "제로포인트가 풀렸을 경우, 화면 안의 일반 적이 0.5초 동안 경직된다.", "제로포인트 시간 + 1초. 제로포인트 중일 경우, 치명타 확률 + 20%." },
        { "체력이 50% 이하라면, 받는 피해 - 5%.", "과부하로 잃은 체력의 20%를 방을 정리했을 경우 다시 얻는다.", "체력이 25% 이하라면, 공격력 + 25%." },
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
