using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 효과를 아이템 정보 팝업에 보여줄 "글자"로 바꿔주는 공용 도우미.
/// 스탯 이름, 증가/감소 색상을 여기 한 곳에서만 바꾸면 모든 아이템 설명에 반영된다.
/// </summary>
public static class EffectText
{
    // ── 스탯 표시 이름 ─────────────────────────────────────
    public const string Hp = "최대 체력";
    public const string Atk = "공격력";
    public const string Def = "방어력";
    public const string Spd = "이동속도";
    public const string Focus = "집중력";

    // ── 색상 (TMP 리치텍스트) ──────────────────────────────
    public const string BuffColor = "#7CE38B";   // + (증가)
    public const string DebuffColor = "#FF6B6B"; // - (감소)

    /// <summary>1.50 → "1.5", 3 → "3" 처럼 불필요한 소수점 제거</summary>
    public static string Num(float v)
    {
        return v.ToString("0.##");
    }

    /// <summary>"+3" / "-2" / "+10%" 를 색 입혀서 반환</summary>
    public static string Signed(float v, bool percent = false)
    {
        string body = (v > 0 ? "+" : "") + (percent ? Num(v * 100f) + "%" : Num(v));
        return "<color=" + (v >= 0 ? BuffColor : DebuffColor) + ">" + body + "</color>";
    }

    /// <summary>StatModifier 를 스탯 하나당 한 항목("공격력 +3")으로 쪼개서 lines 에 추가</summary>
    public static void AddStatLines(StatModifier m, List<string> lines)
    {
        AddOne(lines, Hp, m.hp, false);
        AddOne(lines, Hp, m.hpPercent, true);
        AddOne(lines, Atk, m.atk, false);
        AddOne(lines, Atk, m.atkPercent, true);
        AddOne(lines, Def, m.def, false);
        AddOne(lines, Def, m.defPercent, true);
        AddOne(lines, Spd, m.spd, false);
        AddOne(lines, Spd, m.spdPercent, true);
        AddOne(lines, Focus, m.focus, false);
        AddOne(lines, Focus, m.focusPercent, true);
    }

    /// <summary>StatModifier 전체를 한 줄로 ("공격력 +3, 방어력 +2"). 값이 하나도 없으면 빈 문자열</summary>
    public static string StatsInline(StatModifier m)
    {
        var tmp = new List<string>();
        AddStatLines(m, tmp);
        return string.Join(", ", tmp);
    }

    static void AddOne(List<string> lines, string label, float value, bool percent)
    {
        if (Mathf.Approximately(value, 0f)) return;
        lines.Add(label + " " + Signed(value, percent));
    }

    /// <summary>리치텍스트 태그(&lt;color&gt; 등)를 뺀 실제 보이는 글자 수</summary>
    public static int VisibleLength(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int count = 0;
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) count++;
        }
        return count;
    }

    /// <summary>아이템의 모든 효과 설명을 항목 단위로 모아서 반환</summary>
    public static List<string> Collect(ItemData item)
    {
        var lines = new List<string>();
        if (item == null || item.effects == null) return lines;
        foreach (ItemEffect e in item.effects)
        {
            if (e != null) e.Describe(lines);
        }
        return lines;
    }
}
