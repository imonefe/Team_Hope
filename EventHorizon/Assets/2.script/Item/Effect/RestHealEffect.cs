using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-09] 일정 시간 움직이지 않으면 주기적으로 체력 회복 (영웅의 쉼터)
/// 실제 판정은 플레이어에 붙는 RestHealRunner 가 함.
/// </summary>
[CreateAssetMenu(fileName = "New RestHealEffect", menuName = "Game/Effect/RestHeal")]
public class RestHealEffect : ItemEffect
{
    [Tooltip("이 시간 동안 이동·공격·대시·피격이 없어야 회복 시작 (초)")]
    public float restDelay = 2f;
    [Tooltip("회복 주기 (초)")]
    public float interval = 1f;
    [Tooltip("한 번에 회복하는 양 = 최대 체력 × 이 값 (0.02 = 2%)")]
    public float healRatio = 0.02f;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        if (stats == null) return;
        if (stats.GetComponent<RestHealRunner>() != null) return;   // 중복 획득은 무시
        stats.gameObject.AddComponent<RestHealRunner>().Init(stats, restDelay, interval, healRatio);
    }

    // 예) "2초간 움직이지 않으면 1초마다 최대 체력의 2% 회복"
    protected override void AppendDescription(List<string> lines)
    {
        lines.Add(EffectText.Num(restDelay) + "초간 움직이지 않으면 " + EffectText.Num(interval)
                  + "초마다 최대 체력의 " + EffectText.Num(healRatio * 100f) + "% 회복");
    }
}
