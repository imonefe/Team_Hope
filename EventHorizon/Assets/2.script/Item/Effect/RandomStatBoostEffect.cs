using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 후보 스탯 중 하나를 무작위로 골라, 지정한 크기만큼 영구 적용한다.
/// 획득 시 1번만 랜덤으로 결정되고, 그 뒤로는 고정된다.
/// 기존 StatModifier/StatBoostEffect와 같은 적용 방식을 그대로 재사용한다.
/// </summary>
[CreateAssetMenu(fileName = "New RandomStatBoostEffect", menuName = "Game/Effect/RandomStatBoost")]
public class RandomStatBoostEffect : ItemEffect
{
    [Header("후보 스탯 (체크된 것들 중 랜덤으로 1개)")]
    public bool includeHp = true;
    public bool includeAtk = true;
    public bool includeDef = true;
    public bool includeSpd = true;
    [FormerlySerializedAs("includeFocus")] public bool includePauseDuration = false;   // [2026-11-16 수정] 집중력 → 퍼즈 지속시간으로 이름 변경
    [Tooltip("[2026-11-16 추가] 뽑히면 치명타 확률 + (적용 크기 × 아래 비율)")]
    public bool includeCritChance = true;

    [Header("적용 크기 (고정값)")]
    public float amount = 4f;
    [Tooltip("치명타 확률로 뽑혔을 때 적용 크기 1당 확률 (0.02 = 2%). 적용 크기 3이면 +6%")]
    public float critChancePerAmount = 0.02f;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        List<int> candidates = new List<int>();
        if (includeHp) candidates.Add(0);
        if (includeAtk) candidates.Add(1);
        if (includeDef) candidates.Add(2);
        if (includeSpd) candidates.Add(3);
        if (includePauseDuration) candidates.Add(4);
        if (includeCritChance) candidates.Add(5);

        if (candidates.Count == 0) return;

        int picked = candidates[Random.Range(0, candidates.Count)];

        StatModifier modifier = new StatModifier();
        switch (picked)
        {
            case 0: modifier.hp = Mathf.RoundToInt(amount); break;
            case 1: modifier.atk = amount; break;
            case 2: modifier.def = amount; break;
            case 3: modifier.spd = amount; break;
            case 4: modifier.pauseDuration = amount; break;
            case 5: modifier.critChance = amount * critChancePerAmount; break;
        }

        modifier.Apply(stats);
    }

    // 예) "무작위 스탯 1개 +3" / "공격력·방어력 중 1개 +3"
    protected override void AppendDescription(List<string> lines)
    {
        List<string> names = new List<string>();
        if (includeHp) names.Add(EffectText.Hp);
        if (includeAtk) names.Add(EffectText.Atk);
        if (includeDef) names.Add(EffectText.Def);
        if (includeSpd) names.Add(EffectText.Spd);
        if (includePauseDuration) names.Add(EffectText.PauseDuration);
        if (includeCritChance) names.Add(EffectText.CritChance);
        if (names.Count == 0) return;

        string value = EffectText.Signed(names.Count == 1 && includeHp ? Mathf.RoundToInt(amount) : amount);
        // 치명타 확률은 단위가 달라(%) 한 줄로 묶어 쓰면 숫자가 헷갈림 → 확률 수치를 따로 표기
        if (includeCritChance)
        {
            int i = names.IndexOf(EffectText.CritChance);
            names[i] = EffectText.CritChance + "(" + EffectText.Num(amount * critChancePerAmount * 100f) + "%)";
        }
        if (names.Count == 6) lines.Add("무작위 스탯 1개 " + value);
        else if (names.Count == 1) lines.Add(names[0] + " " + value);
        else lines.Add(string.Join("·", names) + " 중 1개 " + value);
    }
}
