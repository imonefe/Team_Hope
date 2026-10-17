using UnityEngine;

/// <summary>
/// 투사체 공통 베이스 클래스
/// ProjectileHoming(유도 미사일), ProjectileStraight(직선 투사체)가 공통으로 상속
/// 데미지 계산, 충돌 판정, 넣백 적용, 퍼즈 중 정지 로직을 여기서 공유함
/// </summary>
public abstract class ProjectileBase : MonoBehaviour
{
    [Header("데미지")]
    public int damage = 10;
    public Stats attackerStats;
    public string ownerLayer = "Enemy";

    [Header("넣백")]
    public float knockbackX = 5f;
    public float knockbackY = 3f;

    [Header("이펙트")]
    public GameObject hitEffectPrefab;

    protected Rigidbody2D rigid;
    protected bool hit = false;

    /// <summary>플레이어가 쏜 투사체인지 (Q 퍼즈 중에도 멈추지 않음)</summary>
    protected bool IsPlayerOwned => ownerLayer == "Player";

    protected virtual void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 퍼즈 중이면 속도를 0으로 만들고 true 반환 (호출부에서 이번 프레임 이동 처리를 건너뛰면 됨)
    /// </summary>
    protected bool PauseCheck()
    {
        // [2026-10-10 수정] 플레이어 투사체는 퍼즈 중에도 정상 비행 (근접 공격과 같은 규칙)
        if (IsPlayerOwned) return false;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            rigid.velocity = Vector2.zero;
            return true;
        }
        return false;
    }

    // 공통 공식은 Stats.CalculateEffectiveDamage() 참고 (Hitbox.cs 와 동일 공식 공유).
    // 상성 배율 적용 + 로그 출력도 Hitbox.cs와 완전히 같은 코드였어서
    // Stats.ApplyAdvantageMultiplierWithLog()로 공유한다.
    protected int CalcDamage(int rawDamage, Health targetHealth)
    {
        Stats targetStats = targetHealth.GetComponent<Stats>();
        int baseDamage = Stats.CalculateEffectiveDamage(rawDamage, attackerStats, targetStats, requireBothStats: true);

        int finalDamage = Stats.ApplyAdvantageMultiplierWithLog(baseDamage, attackerStats, targetStats, "Projectile", targetHealth.name);
        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// 충돌 판정에 쓸 "현재 진행 방향의 x부호" — 서브클래스가 자신의 이동 방향 값을 제공
    /// </summary>
    protected abstract float FacingX { get; }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (hit) return;
        // 히트박스(공격 판정)에는 맞지 않음
        if (Hitbox.IsHitboxCollider(other)) return;

        // [2026-10-10 추가] 벽·바닥(ground)에 닿으면 소멸 (한쪽 통과 발판 platform은 통과)
        if (other.gameObject.layer == LayerMask.NameToLayer("ground"))
        {
            hit = true;
            if (hitEffectPrefab != null)
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
            return;
        }
        if (!string.IsNullOrEmpty(ownerLayer) &&
            other.gameObject.layer == LayerMask.NameToLayer(ownerLayer)) return;

        Health health = Health.Resolve(other);

        // 같은 편 제외 — 적 투사체는 다른 적·보스에게 피해를 주지 않는다
        if (health != null && ownerLayer == "Enemy" && Hitbox.IsEnemySide(health.gameObject)) return;

        // [2026-10-10 추가] 대시·피격 무적 중인 대상은 그대로 통과 (피해 없는데 투사체만 사라지던 문제)
        if (health != null && !health.CanBeHit()) return;

        if (health != null)
        {
            hit = true;
            int dir = FacingX >= 0 ? 1 : -1;
            int finalDamage = CalcDamage(damage, health);
            health.TakeDamage(finalDamage, dir, knockbackX, knockbackY);

            if (hitEffectPrefab != null)
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
