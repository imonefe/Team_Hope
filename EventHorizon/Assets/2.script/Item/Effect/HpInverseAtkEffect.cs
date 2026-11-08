using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-09] 잃은 체력에 비례해 공격력 증가 (세 시간의 말미)
/// 체력이 가득이면 +0%, 바닥에 가까울수록 최대 maxAtkPercent 까지.
/// 효과 데이터(ScriptableObject)는 모든 판이 공유하므로, 실제 계산은 플레이어에 붙는 HpInverseAtkRunner 가 함.
/// </summary>
[CreateAssetMenu(fileName = "New HpInverseAtkEffect", menuName = "Game/Effect/HpInverseAtk")]
public class HpInverseAtkEffect : ItemEffect
{
    [Tooltip("체력이 바닥에 가까울 때의 공격력 증가율 (0.8 = +80%)")]
    public float maxAtkPercent = 0.8f;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        if (stats == null) return;
        if (stats.GetComponent<HpInverseAtkRunner>() != null) return;   // 중복 획득은 무시
        stats.gameObject.AddComponent<HpInverseAtkRunner>().Init(stats, maxAtkPercent);
    }

    // 예) "잃은 체력에 비례해 공격력 증가 (최대 +80%)"
    protected override void AppendDescription(List<string> lines)
    {
        lines.Add("잃은 체력에 비례해 공격력 증가 (최대 +" + EffectText.Num(maxAtkPercent * 100f) + "%)");
    }
}
