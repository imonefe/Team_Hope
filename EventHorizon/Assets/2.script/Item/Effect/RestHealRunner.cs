using System.Reflection;
using UnityEngine;

/// <summary>
/// [2026-11-09] RestHealEffect 가 플레이어에 붙이는 부품.
/// 이동·공격·대시·피격이 있으면 쉰 시간을 0으로 되돌리고, restDelay 이상 쉬면 interval 마다 회복.
/// 공격 중인지는 PlayerController 의 비공개 값(isAttacking)을 읽기만 함 (PlayerController 는 수정하지 않음).
/// </summary>
public class RestHealRunner : MonoBehaviour
{
    static readonly FieldInfo AttackingField =
        typeof(PlayerController).GetField("isAttacking", BindingFlags.NonPublic | BindingFlags.Instance);

    const float MoveEpsilonSqr = 0.0001f;   // 0.01 보다 많이 움직이면 이동으로 봄

    Stats stats;
    PlayerController pc;
    float restDelay, interval, healRatio;
    float rested, tick;
    Vector3 lastPos;
    int lastHp;

    public void Init(Stats s, float delay, float iv, float ratio)
    {
        stats = s; restDelay = delay; interval = Mathf.Max(0.05f, iv); healRatio = ratio;
        pc = GetComponent<PlayerController>();
        lastPos = transform.position;
        lastHp = stats.currentHp;
        stats.OnHpChanged += OnHpChanged;
    }

    void OnHpChanged(int cur, int max)
    {
        if (cur < lastHp) ResetRest();   // 맞으면 휴식 끊김
        lastHp = cur;
    }

    void ResetRest() { rested = 0f; tick = 0f; }

    bool IsBusy()
    {
        if (pc == null) return false;
        if (pc.IsDashing) return true;
        return AttackingField != null && (bool)AttackingField.GetValue(pc);
    }

    void Update()
    {
        if (stats == null) return;
        Vector3 p = transform.position;
        bool moved = (p - lastPos).sqrMagnitude > MoveEpsilonSqr;
        lastPos = p;

        if (moved || IsBusy()) { ResetRest(); return; }

        rested += Time.deltaTime;
        if (rested < restDelay || stats.currentHp <= 0 || stats.currentHp >= stats.maxHp) { tick = 0f; return; }

        tick += Time.deltaTime;
        if (tick >= interval)
        {
            tick -= interval;
            stats.Heal(Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * healRatio)));
        }
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnHpChanged -= OnHpChanged;
    }
}
