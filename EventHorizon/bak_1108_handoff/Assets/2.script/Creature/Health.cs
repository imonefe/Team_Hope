using UnityEngine;
using UnityEngine.Serialization;

public class Health : MonoBehaviour
{
    Stats stats;

    /// <summary>Collider2D로부터 Health를 찾는다 — 콜라이더가 자식 오브젝트에 있는 경우까지 대비해 부모도 확인.</summary>
    public static Health Resolve(Collider2D col)
    {
        Health h = col.GetComponent<Health>();
        if (h == null) h = col.GetComponentInParent<Health>();
        return h;
    }

    [Header("피격 이펙트")]
    public GameObject hitParticlePrefab;
    public float randomOffset = 0.3f;

    [Header("사망 이펙트 (적 전용 — 죽은 자리 가리기용)")]
    [Tooltip("보스(BossController가 붙은 오브젝트)는 이 필드를 쓰지 않음 — 보스는 자체 deathEffectPrefab을 따로 가짐")]
    [FormerlySerializedAs("deathEffectPrefab")]
    public GameObject enemyDeathEffectPrefab;

    [Header("재화 드랍 (적 전용 — 보스는 BossController에서 별도 관리)")]
    public int minCurrencyDrop = 8;
    public int maxCurrencyDrop = 16;

    [Header("피격 무적 시간 (0 = 없음)")]
    public float invincibleTime = 0f;
    float invincibleTimer = 0f;

    // 같은 프레임에 두 번 이상 치명타가 들어와도 사망 처리(재화 드랍/킬 카운트/사망 이펙트 등)가
    // 중복 실행되지 않도록 막는 가드
    bool isDead = false;

    void Update()
    {
        if (invincibleTimer > 0f)
            invincibleTimer -= Time.deltaTime;
    }

    void Awake()
    {
        stats = GetComponent<Stats>();
    }

    public void Heal(int amount)
    {
        stats.Heal(amount);
    }

    /// <summary>
    /// [2026-10-10 추가] 지금 맞을 수 있는 상태인지 (이미 사망 / 플레이어 대시 중 / 플레이어 피격 무적 중이면 false).
    /// TakeDamage가 무시하는 조건과 같다. 공격 쪽(Hitbox 등)이 맞기 전에 확인해서, 피해가 안 들어가는데
    /// 데미지 숫자·화면 흔들림·피격음만 나오는 일을 막는다.
    /// </summary>
    public bool CanBeHit(bool ignoreInvincibility = false)
    {
        if (isDead) return false;
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsDashing) return false;
        if (!ignoreInvincibility && CompareTag("Player") && invincibleTimer > 0f) return false;
        return true;
    }

    /// <summary>[2026-10-10 추가] 체력이 다른 경로로 0이 된 경우(테스트 키 등) 정식 사망 처리를 실행</summary>
    public void ForceDie()
    {
        if (isDead) return;
        isDead = true;
        BossController boss = GetComponent<BossController>();
        if (boss != null) boss.OnDie();
        else Die();
    }

    public void TakeDamage(int damage, int dir, float knockbackX = 5f, float knockbackY = 3f, bool ignoreInvincibility = false)
    {
        // 이미 사망 처리가 시작된 경우 무시 (동프레임 중복 히트로 인한 이중 사망 처리 방지)
        if (isDead) return;

        // 대쉬 중 무적
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsDashing) return;

        // 피격 무적 중이면 무시 (플레이어 전용). 장판형 지속 피해(ignoreInvincibility=true)는 이 무적을
        // 소모하지도, 걸리지도 않는다 — 안 그러면 틱 간격이 무적시간과 겹칠 때 최초 1틱 이후로 계속 씹힘.
        bool isPlayer = gameObject.CompareTag("Player");
        if (isPlayer && !ignoreInvincibility && invincibleTimer > 0f) return;

        // 무적 시작 (플레이어 전용)
        if (isPlayer && !ignoreInvincibility && invincibleTime > 0f)
            invincibleTimer = invincibleTime;

        stats.TakeDamage(damage);

        // 플레이어 피격 시 퍼즈 게이지 3% 충전
        if (gameObject.CompareTag("Player"))
            PauseManager.Instance?.AddGauge(0.3f);

        // 피격 파티클 (피해받는 오브젝트 기준 랜덤 위치)
        if (hitParticlePrefab != null)
        {
            Vector3 offset = new Vector3(
                Random.Range(-randomOffset, randomOffset),
                Random.Range(-randomOffset, randomOffset),
                0f
            );
            Instantiate(hitParticlePrefab, transform.position + offset, Quaternion.identity);
        }

        // 적 넉백
        Enemy enemy = GetComponent<Enemy>();
        if (enemy != null)
            enemy.OnHit(dir);

        // 보스 연동
        BossController boss = GetComponent<BossController>();
        if (boss != null)
            boss.OnHit(dir);

        // 플레이어 넉백
        PlayerController player = GetComponent<PlayerController>();
        if (player != null)
            player.OnHit(dir, knockbackX, knockbackY);

        if (stats.currentHp <= 0)
        {
            isDead = true;

            BossController boss2 = GetComponent<BossController>();
            if (boss2 != null)
                boss2.OnDie();
            else
                Die();
        }
    }

    void Die()
    {
        if (CompareTag("Player"))
        {
            // 캐릭터 사망 알림 → 태그 배틀 전환 or 게임오버
            // (아이템 등 런 초기화는 여기서 하지 않는다 — 캐릭터 교체(태그 배틀)는 런이 계속되는 경우라
            //  여기서 초기화하면 안 되고, 진짜 게임오버일 때의 초기화는 RunManager.RestartRun()
            //  ["다시하기" 버튼]이 이미 전담해서 처리한다.)
            CharacterManager.Instance?.NotifyCharacterDead(gameObject);
        }
        else
        {
            // 적 사망 — 디스폰 이펙트 (갑자기 사라진 느낌 방지)
            if (enemyDeathEffectPrefab != null)
                Instantiate(enemyDeathEffectPrefab, transform.position, Quaternion.identity);

            // 일반 적(Enemy 컴포넌트가 붙은 대상)만 처치 수 집계 + 재화 드랍.
            // 보스는 Health.Die()를 거치지 않고 BossController.OnTrueDeath()에서 따로 처리한다.
            if (GetComponent<Enemy>() != null)
            {
                RunManager.Instance?.RegisterEnemyKilled();
                RunManager.Instance?.DropRandomCurrency(transform.position, minCurrencyDrop, maxCurrencyDrop);
            }
        }

        Destroy(gameObject);
    }
}