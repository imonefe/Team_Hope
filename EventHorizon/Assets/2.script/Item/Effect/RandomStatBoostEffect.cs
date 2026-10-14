using System.Collections.Generic;
using UnityEngine;

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
    public bool includeFocus = true;

    [Header("적용 크기 (고정값)")]
    public float amount = 4f;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        List<int> candidates = new List<int>();
        if (includeHp) candidates.Add(0);
        if (includeAtk) candidates.Add(1);
        if (includeDef) candidates.Add(2);
        if (includeSpd) candidates.Add(3);
        if (includeFocus) candidates.Add(4);

        if (candidates.Count == 0) return;

        int picked = candidates[Random.Range(0, candidates.Count)];

        StatModifier modifier = new StatModifier();
        switch (picked)
        {
            case 0: modifier.hp = Mathf.RoundToInt(amount); break;
            case 1: modifier.atk = amount; break;
            case 2: modifier.def = amount; break;
            case 3: modifier.spd = amount; break;
            case 4: modifier.focus = amount; break;
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
        if (includeFocus) names.Add(EffectText.Focus);
        if (names.Count == 0) return;

        string value = EffectText.Signed(names.Count == 1 && includeHp ? Mathf.RoundToInt(amount) : amount);
        if (names.Count == 5) lines.Add("무작위 스탯 1개 " + value);
        else if (names.Count == 1) lines.Add(names[0] + " " + value);
        else lines.Add(string.Join("·", names) + " 중 1개 " + value);
    }
}
