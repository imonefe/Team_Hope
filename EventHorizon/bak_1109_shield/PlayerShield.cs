using UnityEngine;

// [Faust 2026-11-08] 플레이어 보호막: 체력 대신 보호막 수치가 먼저 깎이고, 0이 되면 깨져서 사라짐
// FX_Shield_Future 같은 쉴드 이펙트(플레이어 자식)에 붙음. ShieldSkill 이 생성할 때 자동으로 붙여 줌.
public class PlayerShield : MonoBehaviour
{
    public int maxShield = 50;
    public int current;
    public float duration = 0f;            // 0이면 깨질 때까지 유지
    [Header("파손 연출")]
    public float hitShake = 0.08f;         // 맞을 때 흔들림 크기
    public float hitShakeTime = 0.15f;
    public float maxDamageDissolve = 0.45f;// 수치가 바닥에 가까울수록 이만큼 구멍이 뚫려 보임
    public float breakTime = 0.12f;       // 깨질 때 쉴드가 사라지는 시간 (조각은 FX_ShieldShatter)

    Health health;
    ShieldPulse pulse;
    ShieldImpactFX impact;
    Vector3 baseLocalPos;
    float shakeUntil, endTime;
    bool broken;

    public float Ratio => maxShield > 0 ? (float)current / maxShield : 0f;

    public void Init(Health h, int amount, float dur)
    {
        health = h; maxShield = Mathf.Max(1, amount); current = maxShield; duration = dur;
        pulse = GetComponent<ShieldPulse>();
        impact = GetComponent<ShieldImpactFX>();
        baseLocalPos = transform.localPosition;
        endTime = duration > 0f ? Time.time + duration : float.MaxValue;
        if (health != null) health.damageAbsorber += Absorb;
        UpdateVisual();
    }

    /// 같은 스킬을 다시 쓰면 수치·시간만 다시 채움
    public void Refill(int amount, float dur)
    {
        if (broken) return;
        maxShield = Mathf.Max(maxShield, amount); current = maxShield;
        if (dur > 0f) endTime = Time.time + dur;
        UpdateVisual();
    }

    int Absorb(int damage)
    {
        if (broken || damage <= 0) return damage;
        int used = Mathf.Min(current, damage);
        current -= used;
        shakeUntil = Time.time + hitShakeTime;
        if (pulse != null) pulse.HitFlash();
        if (impact != null) impact.Ripple(health != null ? health.lastHitDir : 0);   // 맞은 쪽 프레넬 파문
        UpdateVisual();
        if (current <= 0) Break();
        return damage - used;   // 보호막을 넘친 피해만 체력으로
    }

    void UpdateVisual()
    {
        if (pulse != null) pulse.damageDissolve = (1f - Ratio) * maxDamageDissolve;
    }

    void Update()
    {
        if (broken) return;
        if (Time.time >= endTime) { Break(); return; }
        // 맞을 때 덜컹
        if (Time.time < shakeUntil) transform.localPosition = baseLocalPos + (Vector3)(Random.insideUnitCircle * hitShake);
        else transform.localPosition = baseLocalPos;
    }

    void Break()
    {
        if (broken) return;
        broken = true;
        if (health != null) health.damageAbsorber -= Absorb;
        transform.localPosition = baseLocalPos;
        if (pulse != null) pulse.BreakAway(breakTime);
        if (impact != null) impact.Shatter();   // 유리 조각 흩어짐
        Destroy(gameObject, breakTime + 0.05f);
    }

    void OnDestroy()
    {
        if (!broken && health != null) health.damageAbsorber -= Absorb;
    }
}
