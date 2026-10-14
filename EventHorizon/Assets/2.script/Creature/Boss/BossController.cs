using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 보스의 공통 기반 클래스
/// 새 보스 추가 시 이 클래스를 상속받아 패턴만 구현
/// </summary>
public abstract class BossController : MonoBehaviour
{
    // ── 상태 열거형 ───────────────────────────────────────
    public enum BossState { Inactive, Cooldown, Moving, Attacking }

    // ── 공통 인스펙터 설정 ────────────────────────────────
    [Header("[ 기본 설정 ]")]
    public float cooldownTime = 3f;
    public float moveSpeed = 4f;

    [Header("[ 페이즈 2 설정 ]")]
    public float p2CooldownTime = 1.5f;
    public float p2MoveSpeed = 6f;
    public float p2DamageMultiplier = 1.5f;

    [Header("[ 참조 ]")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Transform hitboxPoint;

    [Header("[ 히트박스 프로필 ]")]
    [Tooltip("패턴별 히트박스를 자유롭게 등록. 애니메이션 이벤트에서 key로 호출")]
    public HitboxProfile[] hitboxProfiles;

    [Header("[ 사망 연출 ]")]
    [Tooltip("사망 시 생성할 이펙트 프리펩")]
    public GameObject deathEffectPrefab;
    [Tooltip("사망 시 활성화할 캔버스 (선택)")]
    public GameObject deathCanvas;
    [Tooltip("사망 연출 후 오브젝트 제거까지 대기 시간")]
    public float deathDelay = 1.5f;

    [Header("[ 재화 드랍 ]")]
    public int minCurrencyDrop = 100;
    public int maxCurrencyDrop = 130;

    // ── 공통 상태 변수 ────────────────────────────────────
    protected BossState state = BossState.Inactive;
    protected bool isPhase2 = false;

    /// <summary>이 보스가 사망 시 페이즈2로 부활하는지. false면 첫 사망이 바로 진짜 사망으로 처리됨 (보스마다 재정의)</summary>
    protected virtual bool HasPhase2 => true;

    // 실행 중인 BossLoop 코루틴 핸들 (정확한 정지용)
    Coroutine bossLoopCo;
    bool isReviving = false;  // 부활 연출 중 (이 동안 OnDie 무시)
    protected bool revived = false;
    protected bool isGrounded;
    protected int lockedDir = 1;

    // ── 컴포넌트 참조 ─────────────────────────────────────
    protected Rigidbody2D rigid;
    protected Stats stats;
    protected Health health;
    protected Transform player;

    // ── 초기화 ────────────────────────────────────────────
    protected virtual void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        stats = GetComponent<Stats>();
        health = GetComponent<Health>();

        if (CharacterManager.Instance != null)
        {
            UpdatePlayerTarget(CharacterManager.Instance.CurrentCharacter);
            CharacterManager.Instance.OnCharacterChanged += UpdatePlayerTarget;
        }
        else
        {
            // CharacterManager 없으면 태그로 플레이어 직접 찾기
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        SetInactive();
    }

    [Header("[ 자동 활성화 ]")]
    public float activateRange = 10f; // 이 범위 안에 플레이어 들어오면 자동 활성화

    protected virtual void Update()
    {
        // 비활성 상태에서 플레이어 범위 감지 → 자동 활성화
        if (state == BossState.Inactive && player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist <= activateRange)
                Activate();
        }
    }

    // 퍼즈 중엔 완전 정지(이동/애니메이션) — Enemy.cs의 FixedUpdate 정지 방식과 동일
    protected virtual void FixedUpdate()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            // [2026-10-10 수정] 중력을 잠시 꺼서 공중에서 천천히 떨어지지 않게 (풀릴 때 원래대로)
            if (rigid != null)
            {
                if (!pauseFrozen)
                {
                    pauseFrozen = true;
                    pauseSavedVelocity = rigid.velocity;
                    pauseSavedGravity = rigid.gravityScale;
                    rigid.gravityScale = 0f;
                }
                rigid.velocity = Vector2.zero;
            }
            if (animator != null) animator.speed = 0f;
        }
        else
        {
            if (pauseFrozen && rigid != null)
            {
                pauseFrozen = false;
                rigid.gravityScale = pauseSavedGravity;
                rigid.velocity = pauseSavedVelocity;
            }
            if (animator != null) animator.speed = 1f;
        }
    }

    bool pauseFrozen = false;
    Vector2 pauseSavedVelocity;
    float pauseSavedGravity;

    // 퍼즈 중인지 — 패턴 코루틴들의 타이머가 이 값을 보고 진행을 멈춘다
    protected static bool IsWorldPaused => PauseManager.Instance != null && PauseManager.Instance.IsPaused;

    // WaitForSeconds 대체 — 퍼즈 중엔 시간이 흐르지 않는다 (실제 대기시간이 퍼즈 시간만큼 늘어남)
    protected IEnumerator PausableWait(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (!IsWorldPaused) t += Time.deltaTime;
            yield return null;
        }
    }

    protected virtual void OnDestroy()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= UpdatePlayerTarget;
    }

    void UpdatePlayerTarget(GameObject obj)
    {
        if (obj != null) player = obj.transform;
    }

    // ── 비활성 상태 ───────────────────────────────────────
    void SetInactive()
    {
        state = BossState.Inactive;
        rigid.velocity = Vector2.zero;
        OnSetInactive();
    }

    // 비활성 시 애니메이션 등 재정의 가능
    protected virtual void OnSetInactive()
    {
        animator.SetBool("isIdle", true);
        animator.SetBool("isMove", false);
    }

    // ── 활성화 ────────────────────────────────────────────
    public void Activate()
    {
        // isReviving 동안은 state가 일시적으로 Inactive로 바뀌는데(부활 연출용),
        // 이때 Activate()가 다시 불리면(피격 or Update의 자동 활성화 감지) BossLoop()이 하나 더 시작되어
        // 기존 부활 코루틴이 끝나면서 시작하는 BossLoop()과 두 개가 동시에 같은 Animator를 건드리게 된다.
        // 이게 "가끔 애니메이션이 꼬인다"의 실제 원인 중 하나 — 여기서 완전히 막는다.
        if (state != BossState.Inactive || isReviving) return;
        OnActivate();
        if (bossLoopCo != null) StopCoroutine(bossLoopCo);
        bossLoopCo = StartCoroutine(BossLoop());
    }

    protected virtual void OnActivate() { }

    // ── 메인 루프 ─────────────────────────────────────────
    IEnumerator BossLoop()
    {
        while (true)
        {
            // 쿨다운 (대기)
            state = BossState.Cooldown;
            rigid.velocity = Vector2.zero;
            OnCooldownStart();

            float cd = isPhase2 ? p2CooldownTime : cooldownTime;
            yield return PausableWait(cd);

            // 패턴 선택 및 실행
            int pattern = SelectPattern();
            yield return StartCoroutine(ExecutePattern(pattern));
        }
    }

    // 쿨다운 시작 시 (애니메이션 등)
    protected virtual void OnCooldownStart()
    {
        // 직전 패턴이 피격/타임아웃 등으로 중간에 끊겼을 경우, Animator에 소비되지 않은 트리거가
        // 남아있다가 엉뚱한 타이밍에 튀어나오는 걸 방지 — 매 쿨다운(=새 패턴 시작 전)마다 확실히 비움
        ResetAttackTriggers();
        animator.SetBool("isIdle", true);
        animator.SetBool("isMove", false);
    }

    /// <summary> 공격/콤보용 트리거들을 전부 리셋. 보스마다 자기 트리거 이름으로 재정의 </summary>
    protected virtual void ResetAttackTriggers() { }

    // ── 하위 클래스에서 반드시 구현 ──────────────────────

    protected int lastPattern = -1; // 직전에 실행한 패턴

    /// <summary> 패턴 선택 (기본: 랜덤, 직전 패턴 반복 안 함) </summary>
    protected virtual int SelectPattern()
    {
        int count = GetPatternCount();
        if (count <= 1) return 0; // 패턴 1개면 반복 방지 불가

        int p;
        do
        {
            p = Random.Range(0, count);
        } while (p == lastPattern);

        lastPattern = p;
        return p;
    }

    /// <summary> 사용할 패턴 수 반환 </summary>
    protected abstract int GetPatternCount();

    /// <summary> 패턴 번호에 따른 실행 </summary>
    protected abstract IEnumerator ExecutePattern(int index);

    // ── 공통 유틸: 플레이어 방향으로 이동 ─────────────────
    protected IEnumerator MoveToPlayer(float stopDistance)
    {
        float spd = isPhase2 ? p2MoveSpeed : moveSpeed;

        animator.SetBool("isMove", true);
        animator.SetBool("isIdle", false);

        while (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist <= stopDistance) break;

            int dir = player.position.x > transform.position.x ? 1 : -1;
            FlipTo(dir);
            rigid.velocity = new Vector2(dir * spd, rigid.velocity.y);
            yield return null;
        }

        rigid.velocity = Vector2.zero;
        animator.SetBool("isMove", false);
        animator.SetBool("isIdle", true);
    }

    // ── 히트박스 소환 (key 기반) ──────────────────────────
    protected void SpawnHitboxByKey(string key)
    {
        if (hitboxProfiles == null) return;

        HitboxProfile profile = System.Array.Find(hitboxProfiles, p => p.key == key);
        if (profile == null)
        {
            Debug.LogWarning($"[Boss] 히트박스 프로필 '{key}' 없음");
            return;
        }
        if (profile.prefab == null) return;

        Transform point = profile.point != null ? profile.point : hitboxPoint;
        if (point == null) return;

        GameObject obj = Instantiate(profile.prefab, point.position, point.rotation);
        Hitbox hb = obj.GetComponent<Hitbox>();
        if (hb != null)
        {
            hb.direction = lockedDir;

            float baseDmg = stats.Atk * profile.damageMultiplier;
            if (profile.useP2Multiplier && isPhase2)
                baseDmg *= p2DamageMultiplier;

            hb.damage = Mathf.RoundToInt(baseDmg);
            hb.attackerStats = stats;
            hb.ownerLayer = "Enemy";
        }

        // 크기 오버라이드
        if (profile.sizeOverride != Vector2.zero)
        {
            BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
            if (box != null) box.size = profile.sizeOverride;
        }
    }

    // 애니메이션 이벤트에서 호출 (String 파라미터로 key 전달)
    public virtual void SpawnHitbox(string key)
    {
        SpawnHitboxByKey(key);
    }

    // ── 공통 유틸: 방향 전환 ──────────────────────────────
    protected void FlipTo(int dir)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dir;
        transform.localScale = scale;
        lockedDir = dir;
    }

    // ── 공통 유틸: 애니메이션 완료 감지 ───────────────────
    protected bool IsAnimFinished(string stateName)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName) && info.normalizedTime >= 1f;
    }

    // [2026-10-10 추가] 해당 상태가 "끝났는지" — 끝까지 재생했거나, 그 상태에 들어간 뒤 나가는 전이가 시작됐거나 이미 벗어났으면 끝.
    // 예전엔 끝까지 재생(정규화 시간 1)만 봤는데, 콤보 3타·착지·차징은 클립이 끝나기 전에 나가는 전이가 시작돼
    // 한 번도 1에 도달하지 못해서 매번 최대 대기시간(3초)을 꽉 채워 기다렸다.
    // entered: 트리거 직후 아직 상태에 들어가기 전에 "벗어남"으로 오판하지 않도록, 들어간 적이 있는지 기억하는 값.
    protected bool AnimStateDone(string stateName, ref bool entered)
    {
        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(0);
        bool inTransition = animator.IsInTransition(0);
        bool inState = cur.IsName(stateName);
        bool goingTo = inTransition && animator.GetNextAnimatorStateInfo(0).IsName(stateName);

        if (inState || goingTo) entered = true;
        if (!entered) return false;

        if (inState && !inTransition && cur.normalizedTime >= 1f) return true; // 끝까지 재생
        if (inState && inTransition && !goingTo) return true;                  // 나가는 전이 시작
        if (!inState && !goingTo) return true;                                 // 이미 벗어남
        return false;
    }

    // 타임아웃 포함 — 애니메이션이 안 끝나도 maxWait 초 후 강제 진행
    protected IEnumerator WaitAnimFinish(string stateName, float maxWait = 3f)
    {
        float timer = 0f;
        bool entered = false;
        while (!AnimStateDone(stateName, ref entered))
        {
            if (!IsWorldPaused)
            {
                timer += Time.deltaTime;
                if (timer >= maxWait) break; // 타임아웃
            }
            yield return null;
        }
    }

    // ── 피격 ──────────────────────────────────────────────
    public virtual void OnHit(int dir)
    {
        if (isReviving) return; // 부활 연출 중엔 피격 반응/재활성화 시도 모두 무시

        if (state == BossState.Inactive)
        {
            Activate();
            return;
        }
        OnHitEffect(dir);
    }

    protected virtual void OnHitEffect(int dir)
    {
        // Attacking(콤보/차징/낙하 등) 도중에 isHit 트리거가 끼어들면, 그 공격 코루틴이
        // WaitAnimFinish 등으로 기다리던 애니메이션 상태를 Animator가 이탈해버려서
        // 코루틴의 "논리적 진행 단계"와 실제 화면의 "애니메이션 상태"가 어긋나게 된다.
        // 이게 눈에 보이는 "애니메이션 꼬임"의 가장 흔한 원인이라 — 공격 중엔 피격 연출을 생략한다
        // (데미지 자체는 Health/Stats 쪽에서 그대로 처리되므로 전투에는 영향 없음).
        if (state == BossState.Attacking) return;
        animator.SetTrigger("isHit");
    }

    // ── 사망 / 페이즈2 부활 ───────────────────────────────
    public void OnDie()
    {
        if (isReviving) return;  // 부활 연출 중엔 사망 처리 무시

        if (HasPhase2 && !revived)
        {
            revived = true;
            isPhase2 = true;
            StartCoroutine(ReviveRoutine());
        }
        else
        {
            OnTrueDeath();
        }
    }

    protected virtual IEnumerator ReviveRoutine()
    {
        isReviving = true;
        if (bossLoopCo != null) StopCoroutine(bossLoopCo);
        state = BossState.Inactive;
        rigid.velocity = Vector2.zero;

        // 낙하기 등 패턴 중단 대비 — 물리/스프라이트 정상 복구
        rigid.simulated = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        animator.speed = 1f;

        animator.SetTrigger("Revive");

        // HP 전량 회복 — currentHp를 직접 대입하면 Stats.OnHpChanged가 안 불려서
        // (HP 바 UI 등 이벤트 구독자에게) 부활한 게 반영이 안 됨. Heal()을 통해서 제대로 알림.
        stats.Heal(stats.maxHp);

        yield return PausableWait(2f);

        isReviving = false;
        OnPhase2Start();
        bossLoopCo = StartCoroutine(BossLoop());
    }

    protected virtual void OnPhase2Start() { }

    /// <summary>보스가 진짜로 사망했을 때 발생 (부활 전 임시 사망 X). 보스 HP UI 등이 구독해서 정리 처리에 사용</summary>
    public event System.Action OnDied;

    protected virtual void OnTrueDeath()
    {
        StopAllCoroutines();
        rigid.velocity = Vector2.zero;
        state = BossState.Inactive;

        // 낙하 패턴 등 스프라이트/물리를 꺼둔 상태(Pattern_Falling 등) 도중에 죽는 경우를 대비해
        // ReviveRoutine()과 동일하게 복구 — 안 그러면(HasPhase2=false인 보스는 특히) 죽었는데
        // 안 보이거나 물리가 안 움직이는 상태로 사망 연출이 진행된다.
        rigid.simulated = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        animator.speed = 1f;

        OnDied?.Invoke();

        // 보스 처치 수 집계 + 재화 드랍(일반 적보다 많이)
        RunManager.Instance?.RegisterBossKilled();
        RunManager.Instance?.DropRandomCurrency(transform.position, minCurrencyDrop, maxCurrencyDrop);

        // 사망 이펙트
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        // 사망 캔버스 활성화
        if (deathCanvas != null)
            deathCanvas.SetActive(true);

        // 딜레이 후 오브젝트 제거 (제거되면 Room이 자동으로 포탈 오픈)
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return PausableWait(deathDelay);
        Destroy(gameObject);
    }

    // ── 착지 감지 ─────────────────────────────────────────
    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & LayerMask.GetMask("ground", "platform")) != 0)
            isGrounded = true;
    }

    protected virtual void OnCollisionExit2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & LayerMask.GetMask("ground", "platform")) != 0)
            isGrounded = false;
    }

    // ── 기즈모 ────────────────────────────────────────────
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
}

/// <summary>
/// 패턴별 히트박스 설정 묶음
/// 인스펙터에서 보스마다 자유롭게 등록
/// </summary>
[System.Serializable]
public class HitboxProfile
{
    [Tooltip("애니메이션 이벤트에서 호출할 이름 (예: Combo1, Slam)")]
    public string key;

    [Tooltip("히트박스 프리펩")]
    public GameObject prefab;

    [Tooltip("소환 위치 (비우면 보스의 기본 hitboxPoint 사용)")]
    public Transform point;

    [Tooltip("데미지 배율 (1 = ATK 그대로)")]
    public float damageMultiplier = 1f;

    [Tooltip("콜라이더 크기 오버라이드 (0,0이면 프리펩 기본값)")]
    public Vector2 sizeOverride = Vector2.zero;

    [Tooltip("페이즈2 데미지 배율 적용 여부")]
    public bool useP2Multiplier = true;
}