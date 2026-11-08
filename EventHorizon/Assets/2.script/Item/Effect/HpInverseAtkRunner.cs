using UnityEngine;

/// <summary>
/// [2026-11-09] HpInverseAtkEffect 가 플레이어에 붙이는 부품.
/// 체력이 바뀔 때마다 이전 보너스를 빼고 새 보너스를 더함 (다른 아이템으로 오른 공격력도 기준에 포함).
/// </summary>
public class HpInverseAtkRunner : MonoBehaviour
{
    Stats stats;
    float maxPercent;
    float bonus;   // 지금 이 부품이 더해 둔 공격력

    public void Init(Stats s, float percent)
    {
        stats = s;
        maxPercent = percent;
        stats.OnHpChanged += OnHpChanged;
        Refresh();
    }

    void OnHpChanged(int cur, int max) { Refresh(); }

    void Refresh()
    {
        if (stats == null) return;
        float baseAtk = stats.Atk - bonus;
        float lost = stats.maxHp > 0 ? 1f - Mathf.Clamp01((float)stats.currentHp / stats.maxHp) : 0f;
        float newBonus = baseAtk * maxPercent * lost;
        stats.Atk += newBonus - bonus;
        bonus = newBonus;
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnHpChanged -= OnHpChanged;
    }
}
