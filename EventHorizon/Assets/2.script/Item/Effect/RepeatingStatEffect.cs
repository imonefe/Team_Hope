using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "일정 시간 효과 적용 후 원복, repeat면 cooldown 후 반복" 하는 스탯 이펙트 공통 베이스.
/// TimedEffect / FadeEffect 가 공통으로 상속.
/// repeat: false → 1회 / true → cooldown 후 반복
/// </summary>
public abstract class RepeatingStatEffect : ItemEffect
{
    public StatModifier modifier;

    [Header("시간 설정")]
    public float duration = 5f;
    public bool repeat = false;
    public float cooldown = 3f;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        runner.StartCoroutine(RepeatRoutine(stats));
    }

    IEnumerator RepeatRoutine(Stats stats)
    {
        do
        {
            yield return RunOnce(stats);

            if (repeat)
                yield return new WaitForSeconds(cooldown);

        } while (repeat);
    }

    /// <summary>1회분 적용 → 대기 → 원복 로직. 서브클래스가 구현.</summary>
    protected abstract IEnumerator RunOnce(Stats stats);

    /// <summary>"N초간" 뒤에 붙는 말. Timed는 "", Fade는 " 점점 감소" 등</summary>
    protected virtual string DurationSuffix => "";

    // 예) "공격력 +5, 방어력 +2 (5초간)" / 반복이면 "(5초간 · 3초마다 반복)"
    protected override void AppendDescription(List<string> lines)
    {
        string stats = EffectText.StatsInline(modifier);
        if (string.IsNullOrEmpty(stats)) return;

        string time = EffectText.Num(duration) + "초간" + DurationSuffix;
        if (repeat) time += " · " + EffectText.Num(cooldown) + "초마다 반복";
        lines.Add(stats + " (" + time + ")");
    }
}
