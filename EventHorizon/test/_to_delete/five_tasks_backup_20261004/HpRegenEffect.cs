using System.Collections;
using UnityEngine;

/// <summary>
/// 최대체력의 N% 이하로 떨어지면 interval 초마다 HP 회복
/// 체력이 threshold 이상으로 회복되면 작동 멈추고 대기
/// </summary>
[CreateAssetMenu(fileName = "New HpRegenEffect", menuName = "Game/Effect/HpRegen")]
public class HpRegenEffect : ItemEffect
{
    [Header("발동 조건")]
    [Range(0f, 1f)]
    public float threshold = 0.5f;   // 최대체력의 몇 % 이하일 때 발동 (0.5 = 50%)

    [Header("회복 설정")]
    public int healAmount = 10;    // 회복량
    public float interval = 3f;   // 회복 주기 (초)

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        runner.StartCoroutine(RegenRoutine(stats));
    }

    IEnumerator RegenRoutine(Stats stats)
    {
        while (true)
        {
            // 체력이 threshold 이하로 떨어질 때까지 대기
            yield return new WaitUntil(() =>
                (float)stats.currentHp / stats.maxHp <= threshold);

            // threshold 이하인 동안 interval 마다 회복
            while ((float)stats.currentHp / stats.maxHp <= threshold)
            {
                stats.Heal(healAmount);
                yield return new WaitForSeconds(interval);
            }

            // threshold 이상 회복되면 다시 대기 상태로
        }
    }
}