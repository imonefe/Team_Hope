using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
    public float Enemyeye = 10f;
    public float attackRange = 2f;

    [Header("추격 잠시 멈춤")]
    [Tooltip("플레이어와 좌우(x) 거리가 공격 범위보다 가까운데 공격은 안 닿을 때(바로 위 발판 등) 이 시간(초)만큼 제자리에서 기다림.\n" +
             "플레이어 밑에서 좌우로 빠르게 왔다 갔다 하는 문제 방지. 0 = 끔")]
    public float chaseHoldTime = 0.5f;
    float chaseHoldTimer = 0f;
    public float knockbackForce = 5f;
    public float knockbackDuration = 0.2f;
    public float hitFlashTime = 0.1f;
    public Material flashMaterial;

    [Header("피격 반응")]
    [Tooltip("꺼두면 맞아도 피격 애니메이션(isHit)이 재생되지 않음. 넉백·경직 자체는 이 값과 무관하게 그대로 적용됨")]
    public bool playHitAnimation = true;

    [Header("냉기 속성 (선택 — 나중에 파티클 연결)")]
    [Tooltip("냉기 속성 타격마다 튀는 파편. 동결(스턴) 시작 순간에도 같은 걸로 1회 더 스폰됨")]
    public GameObject iceShardPrefab;
    [Tooltip("동결 중 랜더러에 씌울 색")]
    public Color frostTintColor = new Color(0.4f, 0.7f, 1f, 1f);
    [Range(0f, 1f)] public float frostTintAmount = 0.6f;
    [Tooltip("동결 중 계속 붙어있는 얼음 비주얼 (얼음에 갇힌 느낌). 동결 시작 시 스프라이트 자식으로 붙었다가 풀리면 사라짐")]
    public GameObject frozenVisualPrefab;

    public GameObject hitboxPrefab;
    public Transform hitboxPoint;
    public GameObject projectilePrefab; // 원거리 투사체 (없으면 미사용)

    [Header("공격 스프라이트 이펙트 (선택)")]
    [Tooltip("공격 애니메이션 이벤트로 소환할 이펙트 프리펩")]
    public GameObject attackEffectPrefab;
    [Tooltip("이펙트 생성 위치 (비우면 hitboxPoint, 그것도 없으면 자기 위치)")]
    public Transform attackEffectPoint;
    [Tooltip("바라보는 방향에 따라 이펙트 좌우 반전")]
    public bool effectFlipByFacing = true;

    [Header("공격 이동 (돌진형 캐릭터)")]
    [Tooltip("켜면 공격 시작과 함께 바라보는 방향으로 일정 거리를 돌진한 뒤 공격을 이어감")]
    public bool attackDash = false;
    [Tooltip("돌진 속도")]
    public float attackDashSpeed = 6f;
    [Tooltip("돌진 거리 — attackDashSpeed로 이 거리를 이동하는 데 걸리는 시간만큼만 돌진하고 멈춤")]
    public float attackDashDistance = 1.5f;
    [Tooltip("공격 시작 후 이 시간(초)이 지나야 돌진 시작 (0 = 공격 시작과 동시에). 준비 동작이 긴 공격은 실제로 밀고 나가는 프레임 시간에 맞추면 됨")]
    public float attackDashDelay = 0f;
    [Tooltip("켜면 공격 판정(히트박스)이 적을 따라 같이 움직임 — 돌진 공격처럼 판정이 나온 뒤에도 적이 움직이는 경우용")]
    public bool hitboxFollowAttacker = false;

    [Header("공격 종류 (랜덤)")]
    [Tooltip("공격 애니메이션 종류 수 (1 = 단일, 2 이상 = 랜덤 선택)")]
    public int attackVariations = 1;
    // 현재 재생 중인 공격 종류 (애니메이터 attackType 파라미터와 동기화)
    int currentAttackType = 0;

    public bool isGrounded;

    [Header("스폰 대기")]
    [Tooltip("스폰 직후 이 시간(초) 동안 움직이지 않음 (0 = 즉시 활동)")]
    public float spawnIdleTime = 0f;
    float spawnIdleTimer = 0f;

    bool isAttacking = false;
    bool isHit = false;
    bool isKnockback = false;
    bool isChasing = false;
    bool isFrozen = false;       // 냉기 스턴 중
    int frostStacks = 0;         // 냉기 속성 누적 스택
    bool isDirectionLocked = false;  // 조준 중 방향 고정
    int lockedDir = 1;       // 고정된 방향  // 플레이어 추적 중 여부

    public int NextMove;

    [Header("조준 레이저")]
    public LineRenderer laserRenderer;   // LineRenderer 컴포넌트
    public float laserLength = 10f;
    public Color laserColor = new Color(1f, 0f, 0f, 0.8f);
    [Tooltip("공격(EndAttack) 이후에도 조준 자세가 잠깐 남아있는 애니메이션이 있는 경우, 그 시간(초) 동안은 방향 전환을 계속 막아둔다. 0이면 공격이 끝나자마자 바로 방향 전환 허용")]
    public float aimHoldAfterAttack = 0f;

    [Header("공격 쿨타임")]
    public float attackCooldown = 3.0f;
    [Tooltip("공격을 시작하고 이 시간(초)이 지나면 애니메이터에 '공격 끝' 신호를 보냄.\n" +
             "적은 그 뒤에도 애니메이터가 공격 모션에서 실제로 빠져나올 때까지 제자리에 서 있다 (모션 중 미끄러짐 방지)")]
    public float attackDuration = 0.8f;
    [Tooltip("애니메이션 이벤트가 빠지거나 상태가 꼬였을 때를 대비한 안전장치: '공격 끝' 신호 후 이 시간(초)이 지나면 무조건 공격 종료")]
    public float attackEndTimeout = 2f;

    [Header("원거리 조준 (투사체를 쓰는 적만)")]
    [Tooltip("켜면 투사체를 플레이어 쪽으로 기울여 쏜다 (끄면 예전처럼 수평으로만)")]
    public bool aimAtPlayer = true;
    [Tooltip("수평에서 위아래로 최대 몇 도까지 기울여 쏠 수 있는지")]
    [Range(0f, 89f)] public float maxAimAngle = 45f;
    float attackTimer = 0f;
    float attackDurTimer = 0f;
    bool attackAnimReleased = false;   // 애니메이터에 '공격 끝' 신호를 보냈는지 (그 뒤 모션이 실제로 끝나길 기다리는 중)
    Vector2 aimDir = Vector2.right;    // 조준 방향 (LockDirection 때 정해짐)
    bool hasAimDir = false;
    Coroutine releaseLockRoutine;
    float attackDashTimer = 0f;    // 돌진 시작 후 경과 시간
    float attackDashDuration = 0f; // attackDashDistance / attackDashSpeed (공격 시작 시 계산)
    int attackDashDir = 1;         // 돌진 방향 (공격 시작 시 고정)

    [Tooltip("기본 적 애니메이터(Enemy.controller)의 공격(Atk01) → 대기 전이 종료 시점. 공격 끝 신호가 이 시점보다 늦으면 애니메이터가 전이를 놓쳐 공격 모션을 한 바퀴 더 재생하므로, 그때는 직접 대기 상태로 넘긴다")]
    public float attackExitTime = 0.8125f;
    static readonly int HashIdleState = Animator.StringToHash("idle");

    // Q 퍼즈 중 정지 상태 (중력을 잠시 끄고, 풀릴 때 원래 속도·중력 복원)
    bool pauseFrozen = false;
    Vector2 pauseSavedVelocity;
    float pauseSavedGravity;

    Rigidbody2D rigid;
    Animator anim;
    SpriteRenderer sr;
    Material originalMaterial;
    MaterialPropertyBlock flashBlock;
    Stats stats;
    Transform player;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        stats = GetComponent<Stats>();
        anim = GetComponentInChildren<Animator>();
        // 루트에 스프라이트가 비어있는 더미 SpriteRenderer가 있는 프리팹이 있어
        // Animator가 붙은 자식의 SpriteRenderer를 우선적으로 찾음
        sr = anim != null ? anim.GetComponent<SpriteRenderer>() : null;
        if (sr == null)
        {
            foreach (var candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (candidate.sprite != null) { sr = candidate; break; }
            }
        }
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null) originalMaterial = sr.sharedMaterial;
        flashBlock = new MaterialPropertyBlock();

        if (CharacterManager.Instance != null)
        {
            UpdatePlayerTarget(CharacterManager.Instance.CurrentCharacter);
            CharacterManager.Instance.OnCharacterChanged += UpdatePlayerTarget;
        }
        else
        {
            // CharacterManager 초기화 순서상 아직 없으면 태그로 플레이어 직접 찾기 (BossController와 동일 처리)
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // 스폰 대기 시작
        spawnIdleTimer = spawnIdleTime;

        // 플레이어 미발견 시에만 랜덤 배회
        InvokeRepeating(nameof(Think), 0f, 3f);
    }

    void OnDestroy()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= UpdatePlayerTarget;
    }

    void UpdatePlayerTarget(GameObject obj)
    {
        if (obj != null) player = obj.transform;
    }

    void FixedUpdate()
    {
        // 퍼즈 중엔 완전 정지
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            // [2026-10-10 수정] 속도만 0으로 하면 그 물리 프레임에 중력이 다시 더해져 공중의 적이 천천히 떨어졌다
            // → 퍼즈 동안은 중력을 끄고, 풀릴 때 원래 속도·중력을 되돌린다
            if (!pauseFrozen)
            {
                pauseFrozen = true;
                pauseSavedVelocity = rigid.velocity;
                pauseSavedGravity = rigid.gravityScale;
                rigid.gravityScale = 0f;
            }
            rigid.velocity = Vector2.zero;
            anim.speed = 0f;
            return;
        }
        else
        {
            if (pauseFrozen)
            {
                pauseFrozen = false;
                rigid.gravityScale = pauseSavedGravity;
                rigid.velocity = pauseSavedVelocity;
            }
            anim.speed = 1f;
        }

        // 냉기 동결 중엔 완전 정지
        if (isFrozen)
        {
            anim.speed = 0f;
            rigid.velocity = Vector2.zero;
            return;
        }

        GroundCheck();
        attackTimer -= Time.deltaTime;

        // 스폰 직후 대기 (등장 연출 동안 행동 안 함)
        if (spawnIdleTimer > 0f)
        {
            spawnIdleTimer -= Time.fixedDeltaTime;
            anim.SetBool("isMove", false);
            rigid.velocity = new Vector2(0f, rigid.velocity.y);
            return;
        }

        if (isKnockback || isHit) { anim.SetBool("isMove", false); return; }

        // 공격 중 타이머로 자동 종료 (애니메이션 이벤트 백업)
        if (isAttacking)
        {
            attackDurTimer += Time.fixedDeltaTime;

            // 정해둔 공격 시간이 지나면 애니메이터에만 먼저 '공격 끝' 신호 (애니메이터가 공격 모션을 마무리하고 빠져나가게)
            if (!attackAnimReleased && attackDurTimer >= attackDuration)
            {
                attackAnimReleased = true;
                anim.SetBool("isAttack", false);
                anim.SetBool("isAttacking", false);
                ForceLeaveAttackIfMissed();
            }

            // 실제 공격 종료 = 애니메이터가 공격 모션에서 빠져나온 순간 (또는 안전장치 시간 초과)
            // [2026-10-09 수정] 예전엔 attackDuration(0.8초)이 지나면 바로 끝내서, 공격 애니메이션이
            // 그보다 긴 적(기본 적 1.92초, 판정 1.0초)은 공격 모션 중에 미끄러지듯 움직이고
            // 공격 판정이 이미 움직이거나 돌아선 상태에서 나왔다.
            if (attackAnimReleased && (!IsInAttackAnimation() || attackDurTimer >= attackDuration + attackEndTimeout))
                EndAttack();
            else
            {
                anim.SetBool("isMove", false);

                // 돌진형 캐릭터 — 정해둔 거리만큼만 이동하고 이후엔 제자리에서 공격 유지
                if (attackDash && attackDurTimer >= attackDashDelay && attackDashTimer < attackDashDuration)
                {
                    attackDashTimer += Time.fixedDeltaTime;
                    rigid.velocity = new Vector2(attackDashDir * attackDashSpeed, rigid.velocity.y);
                }
                else
                {
                    rigid.velocity = new Vector2(0, rigid.velocity.y);
                }
                return;
            }
        }

        // 공격은 끝났지만 조준 자세 유지 시간(aimHoldAfterAttack)이 아직 안 끝났으면
        // 방향뿐 아니라 위치도 그대로 고정해둔다.
        if (isDirectionLocked)
        {
            rigid.velocity = new Vector2(0, rigid.velocity.y);
            anim.SetBool("isMove", false);
            return;
        }
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // (연막 시야차단 기능은 퍼즈 투척 아이템 시스템과 함께 제거됨 — 항상 보임)
        bool canSeePlayer = true;

        // 공격 범위 안 + 앞에 있으면 공격 (시야가 막혀있으면 공격 불가)
        if (canSeePlayer && distance <= attackRange && IsPlayerInFront())
        {
            rigid.velocity = new Vector2(0, rigid.velocity.y);
            anim.SetBool("isMove", false);
            if (attackTimer <= 0f) Attack();
            return;
        }

        // 플레이어 감지 범위 안 + 시야가 막혀있지 않으면 추적
        if (canSeePlayer && distance <= Enemyeye)
        {
            isChasing = true;
            float dx = player.position.x - transform.position.x;

            // 좌우로는 이미 공격 범위 안인데 (위아래 차이 때문에) 공격이 안 닿는 경우
            // → 계속 쫓으면 플레이어 밑에서 좌우로 왔다 갔다 하므로 잠시 제자리에서 기다린다
            if (chaseHoldTime > 0f && Mathf.Abs(dx) < attackRange)
                chaseHoldTimer = chaseHoldTime;

            if (chaseHoldTimer > 0f)
            {
                chaseHoldTimer -= Time.fixedDeltaTime;
                rigid.velocity = new Vector2(0f, rigid.velocity.y);
                anim.SetBool("isMove", false);
                // 플레이어 쪽은 계속 바라봄 (바로 위에 있을 땐 방향이 깜빡이지 않게 약간 여유)
                if (Mathf.Abs(dx) > 0.15f) NextMove = dx > 0f ? 1 : -1;
                Flip();
                return;
            }

            NextMove = dx > 0f ? 1 : -1;
        }
        else
        {
            isChasing = false;
        }

        if (isGrounded)
        {
            Move();
            // 추적 중엔 엣지체크 스킵 (낙하해서라도 쫓아감)
            if (!isChasing) EdgeCheck();
        }

        Flip();

        if (Mathf.Abs(rigid.velocity.x) < 0.1f)
            anim.SetBool("isMove", false);
    }

    void Move()
    {
        anim.SetBool("isMove", true);
        rigid.velocity = new Vector2(NextMove * stats.spd * stats.GetSpeedMultiplier(), rigid.velocity.y);
    }

    // 플레이어 미발견 시 랜덤 배회
    void Think()
    {
        if (!isChasing)
            NextMove = Random.Range(-1, 2);
    }

    void GroundCheck()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.down * 0.5f;
        isGrounded = Physics2D.Raycast(origin, Vector2.down, 1f, LayerMask.GetMask("ground", "platform"));
    }

    void EdgeCheck()
    {
        Vector2 front = (Vector2)transform.position + new Vector2(NextMove * 0.9f, 0);
        bool hasGround = Physics2D.Raycast(front, Vector2.down, 1.5f, LayerMask.GetMask("ground", "platform"));
        if (!hasGround) NextMove *= -1;
    }

    void Flip()
    {
        if (isDirectionLocked) return; // 조준 중엔 방향 고정
        if (NextMove == 0) return;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (NextMove == -1 ? -1 : 1);
        transform.localScale = scale;
    }

    bool IsPlayerInFront()
    {
        if (player == null) return false;
        float dirToPlayer = player.position.x - transform.position.x;
        return Mathf.Sign(dirToPlayer) == Mathf.Sign(transform.localScale.x);
    }

    // 애니메이션 이벤트에서 호출 → 플레이어 방향으로 조준 후 방향 고정
    public void LockDirection()
    {
        if (player == null) return;
        // [2026-10-09 수정] 공격이 이미 끝난 뒤에 이벤트가 들어오면 방향 고정이 풀릴 기회가 없어
        // 적이 영영 멈춰버리므로 공격 중일 때만 받는다.
        if (!isAttacking) return;

        // 이전 공격에서 예약된 '조준 해제'가 이번 조준을 일찍 풀지 않도록 취소
        if (releaseLockRoutine != null) { StopCoroutine(releaseLockRoutine); releaseLockRoutine = null; }

        int dir = player.position.x > transform.position.x ? 1 : -1;
        lockedDir = dir;
        aimDir = ComputeAimDir(dir);
        hasAimDir = true;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * dir;
        transform.localScale = scale;

        isDirectionLocked = true;

        // 레이저 표시
        ShowLaser(true);
    }

    void ShowLaser(bool show)
    {
        if (laserRenderer == null) return;

        laserRenderer.enabled = show;

        if (!show) return;

        // 발사 위치 기준으로 레이저 방향 설정 (원거리 조준이면 기울어진 방향 그대로)
        Vector3 origin = hitboxPoint != null ? hitboxPoint.position : transform.position;
        Vector3 dir = hasAimDir ? (Vector3)aimDir : new Vector3(lockedDir, 0, 0);

        laserRenderer.SetPosition(0, origin);
        laserRenderer.SetPosition(1, origin + dir * laserLength);

        laserRenderer.startColor = laserColor;
        laserRenderer.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0f);
        laserRenderer.startWidth = 0.05f;
        laserRenderer.endWidth = 0f;
    }

    public void Attack()
    {
        if (isAttacking) return;
        isAttacking = true;
        attackTimer = attackCooldown;
        attackDurTimer = 0f;
        attackAnimReleased = false;
        hasAimDir = false;
        rigid.velocity = new Vector2(0, rigid.velocity.y);

        // 돌진형 캐릭터 — 방향 고정 + 목표 거리를 속도로 나눠 돌진 지속시간 계산
        attackDashTimer = 0f;
        if (attackDash)
        {
            attackDashDir = isDirectionLocked ? lockedDir : (int)Mathf.Sign(transform.localScale.x);
            if (attackDashDir == 0) attackDashDir = 1;
            attackDashDuration = attackDashSpeed > 0f ? attackDashDistance / attackDashSpeed : 0f;
        }

        // 공격 종류 랜덤 선택 (attackVariations 개 중 하나)
        currentAttackType = attackVariations > 1 ? Random.Range(0, attackVariations) : 0;
        anim.SetInteger("attackType", currentAttackType);

        anim.SetBool("isAttack", true);
        anim.SetBool("isAttacking", true);
    }

    // [2026-10-10 추가] 공격 끝 신호를 보낸 시점이 이미 공격 → 대기 전이 종료 시점(attackExitTime)을 지났으면,
    // 애니메이터는 다음 바퀴까지 전이를 하지 않아 공격 모션(과 그 안의 판정 이벤트)을 한 번 더 재생한다.
    // (2S_Spd: 0.82초에 신호, 전이 검사는 0.8125초 → 1.667초에 두 번째 타격 / 1S 원거리: 공격 자세로 1초 더 미끄러짐)
    // 그런 경우에만 직접 대기 상태로 넘긴다. 신호가 제때 갔으면 아무것도 하지 않는다.
    void ForceLeaveAttackIfMissed()
    {
        if (anim == null || anim.IsInTransition(0)) return;
        if (!IsInAttackAnimation()) return;
        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
        float t = st.loop ? (st.normalizedTime % 1f) : st.normalizedTime;
        if (t >= attackExitTime)
            anim.CrossFadeInFixedTime(HashIdleState, 0.1f, 0);
    }

    public void SpawnHitbox()
    {
        // [2026-10-10 추가] 공격 중이 아니면(맞아서 공격이 취소됐는데 피격 모션이 없어 공격 클립이 계속 재생되는 경우 등) 판정을 내지 않음
        if (!isAttacking) return;

        int dir = isDirectionLocked ? lockedDir : (int)Mathf.Sign(transform.localScale.x);

        // 히트박스 소환 (hitboxPrefab + hitboxPoint 둘 다 있을 때만)
        if (hitboxPrefab != null && hitboxPoint != null)
        {
            GameObject obj = Instantiate(hitboxPrefab, hitboxPoint.position, hitboxPoint.rotation);
            // 돌진 공격 등: 판정이 적을 따라 움직이도록 (월드 크기는 그대로 유지)
            if (hitboxFollowAttacker) obj.transform.SetParent(hitboxPoint, true);
            Hitbox hb = obj.GetComponent<Hitbox>();
            if (hb != null)
            {
                hb.direction = dir;
                hb.damage = Mathf.RoundToInt(stats.Atk);
                hb.attackerStats = stats;
                hb.ownerLayer = "Enemy";
            }
        }

        // 투사체 소환 (projectilePrefab 연결 시)
        if (projectilePrefab != null)
        {
            Vector3 spawnPos = hitboxPoint != null ? hitboxPoint.position : transform.position;
            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            ProjectileStraight ps = proj.GetComponent<ProjectileStraight>();
            if (ps != null)
            {
                ps.damage = Mathf.RoundToInt(stats.Atk);
                ps.attackerStats = stats;
                // 조준해 둔 방향이 있으면 그대로, 없으면 지금 플레이어 쪽으로 (수평에서 maxAimAngle 이내)
                ps.Init(hasAimDir ? aimDir : ComputeAimDir(dir));
            }
            ProjectileHoming ph = proj.GetComponent<ProjectileHoming>();
            // [2026-10-10 수정] 유도 미사일처럼 직선 투사체가 아닌 경우엔 경고하지 않음 (둘 다 없을 때만)
            if (ps == null && ph == null) Debug.LogWarning("[Enemy] 투사체 컴포넌트(ProjectileStraight/ProjectileHoming) 없음!");

            if (ph != null)
            {
                ph.damage = Mathf.RoundToInt(stats.Atk);
                ph.attackerStats = stats;
            }
        }
    }

    /// <summary>
    /// 공격 스프라이트 이펙트 소환. 공격 애니메이션 이벤트에서 호출.
    /// 바라보는 방향에 맞춰 좌우 반전 처리.
    /// </summary>
    public void SpawnAttackEffect()
    {
        if (attackEffectPrefab == null) return;
        if (!isAttacking) return; // 공격이 취소된 뒤 남은 클립 이벤트로 이펙트가 나오지 않게

        int dir = isDirectionLocked ? lockedDir : (int)Mathf.Sign(transform.localScale.x);
        if (dir == 0) dir = 1;

        Vector3 pos = attackEffectPoint != null ? attackEffectPoint.position
                    : (hitboxPoint != null ? hitboxPoint.position : transform.position);

        GameObject fx = Instantiate(attackEffectPrefab, pos, Quaternion.identity);

        // 왼쪽을 향하면 좌우 반전 (파티클/스프라이트 공통)
        if (effectFlipByFacing && dir < 0)
        {
            // 파티클: 렌더러 flip + 회전 방향 반전
            var renderers = fx.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
                r.flip = new Vector3(1f, r.flip.y, r.flip.z);

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var rol = ps.rotationOverLifetime;
                if (rol.enabled) rol.zMultiplier *= -1f;
                var rbs = ps.rotationBySpeed;
                if (rbs.enabled) rbs.zMultiplier *= -1f;
            }

            // 스프라이트: X 스케일 반전 (파티클 렌더러가 없을 때)
            if (renderers.Length == 0)
            {
                Vector3 s = fx.transform.localScale;
                s.x = Mathf.Abs(s.x) * -1f;
                fx.transform.localScale = s;
            }
        }
    }

    public void EndAttack()
    {
        isAttacking = false;
        ShowLaser(false);
        anim.SetBool("isAttack", false);
        anim.SetBool("isAttacking", false);

        // 조준 자세가 애니메이션상 잠깐 더 남아있는 캐릭터는, 그 동안은 홱 돌아보지 않도록
        // 방향 고정 해제만 별도로 살짝 늦춘다 (레이저/공격 상태 자체는 이미 정상 종료됨).
        attackAnimReleased = false;
        hasAimDir = false;

        if (isDirectionLocked)
        {
            if (releaseLockRoutine != null) StopCoroutine(releaseLockRoutine);
            if (aimHoldAfterAttack > 0f)
                releaseLockRoutine = StartCoroutine(ReleaseDirectionLockAfter(aimHoldAfterAttack));
            else
                isDirectionLocked = false;
        }
    }

    IEnumerator ReleaseDirectionLockAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        // 그 사이 새 공격이 시작돼 다시 조준 중이면 풀지 않는다
        if (!isAttacking) isDirectionLocked = false;
        releaseLockRoutine = null;
    }

    // 애니메이터가 지금 공격 모션을 재생 중이거나 그쪽으로 넘어가는 중인지.
    // 공격 클립 = 이름을 _ 로 나눈 마지막 부분이 atk / attack 으로 시작 (1S_Atk01, 1S_Attack_Atk, 2s_atk_atk, Ran01_Atk ...)
    // ※ 2s_atk_wark, 1S_Attack_idle 처럼 앞부분에만 atk/attack 이 있는 걷기·대기 클립은 공격이 아님
    bool IsInAttackAnimation()
    {
        if (anim == null) return false;
        if (HasAttackClip(anim.GetCurrentAnimatorClipInfo(0))) return true;
        if (anim.IsInTransition(0) && HasAttackClip(anim.GetNextAnimatorClipInfo(0))) return true;
        return false;
    }

    static bool HasAttackClip(AnimatorClipInfo[] clips)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip == null) continue;
            string n = clips[i].clip.name.ToLowerInvariant();
            int us = n.LastIndexOf('_');
            string last = us >= 0 ? n.Substring(us + 1) : n;
            if (last.StartsWith("atk") || last.StartsWith("attack")) return true;
        }
        return false;
    }

    // 투사체 조준 방향: 바라보는 쪽(facing) 기준으로 플레이어를 향하되, 수평에서 maxAimAngle 도까지만 기울임
    Vector2 ComputeAimDir(int facing)
    {
        if (facing == 0) facing = 1;
        if (!aimAtPlayer || player == null) return new Vector2(facing, 0f);

        Vector3 from = hitboxPoint != null ? hitboxPoint.position : transform.position;
        Vector2 to = (Vector2)(player.position - from);
        if (to.sqrMagnitude < 0.0001f) return new Vector2(facing, 0f);

        float angle = Mathf.Atan2(to.y, Mathf.Abs(to.x)) * Mathf.Rad2Deg;   // 바라보는 쪽 기준 위(+)/아래(-) 각도
        angle = Mathf.Clamp(angle, -maxAimAngle, maxAimAngle);
        float rad = angle * Mathf.Deg2Rad;
        Vector2 result = new Vector2(Mathf.Cos(rad) * facing, Mathf.Sin(rad));
        // 거의 수평이면 정확히 수평으로 (투사체가 회전 대신 좌우 반전으로 표현되게)
        if (Mathf.Abs(result.y) < 0.02f) result = new Vector2(facing, 0f);
        return result.normalized;
    }

    public void OnHit(int dir)
    {
        if (isHit || isKnockback) return;

        // 공격 애니메이션 도중에 맞으면 공격 상태를 여기서 확실히 정리해야 한다.
        // 안 그러면 isAttack/isAttacking bool과 isDirectionLocked가 true로 남은 채
        // 애니메이터가 피격 상태로 전환되고, 넉백이 끝난 뒤 그 남아있던 bool 때문에
        // 애니메이터가 공격 애니메이션 중간 상태로 어중간하게 재진입하면서
        // 애니메이션이 꼬여 보이는(끊기거나 순간적으로 다시 재생되는) 문제가 생긴다.
        bool wasAttacking = isAttacking;
        if (isAttacking)
            EndAttack();

        isHit = isKnockback = true;
        if (playHitAnimation) anim.SetBool("isHit", true);
        // [2026-10-10 추가] 피격 모션이 없는 적은 공격 클립이 계속 재생돼 공격 자세로 미끄러지므로 대기 상태로 끊는다
        else if (wasAttacking && anim != null) anim.CrossFadeInFixedTime(HashIdleState, 0.05f, 0);
        rigid.velocity = Vector2.zero;
        rigid.AddForce(new Vector2(dir * knockbackForce, 2f), ForceMode2D.Impulse);
        StartCoroutine(KnockbackRoutine());
        StartCoroutine(HitFlash());
    }

    IEnumerator KnockbackRoutine()
    {
        yield return new WaitForSeconds(knockbackDuration);
        isKnockback = isHit = false;
        if (playHitAnimation) anim.SetBool("isHit", false);
    }

    /// <summary>냉기 속성 히트마다 호출. requiredStacks 도달 시 stunDuration 만큼 동결.</summary>
    // 스프라이트(자식)가 있으면 그 위치, 없으면 루트 위치 — 루트엔 빈 더미 SpriteRenderer만 있는
    // 프리팹이 있어서(위 Awake 주석 참고) 얼음 파편이 몸통이 아니라 발밑/원점 근처에서 튀는 문제 방지.
    Vector3 IceShardSpawnPos()
    {
        return sr != null ? sr.transform.position : transform.position;
    }

    public void ApplyFrostStack(int requiredStacks, float stunDuration)
    {
        if (isFrozen) return;

        frostStacks++;
        if (iceShardPrefab != null)
            Instantiate(iceShardPrefab, IceShardSpawnPos(), Quaternion.identity);

        if (frostStacks >= requiredStacks)
        {
            frostStacks = 0;
            StartCoroutine(FreezeRoutine(stunDuration));
        }
    }

    IEnumerator FreezeRoutine(float duration)
    {
        isFrozen = true;

        if (iceShardPrefab != null)
            Instantiate(iceShardPrefab, IceShardSpawnPos(), Quaternion.identity);

        // 동결 중 계속 표시되는 얼음 비주얼 — 스프라이트를 따라다니도록 그 자식으로 붙임
        GameObject frozenVisual = null;
        if (frozenVisualPrefab != null)
        {
            Transform parent = sr != null ? sr.transform : transform;
            frozenVisual = Instantiate(frozenVisualPrefab, parent.position, Quaternion.identity, parent);
        }

        float t = 0f;
        while (t < duration)
        {
            if (rigid != null) rigid.velocity = Vector2.zero;

            // 히트플래시(흰색)가 매 프레임 덮어써도 다음 프레임에 다시 파란 틴트로 복구됨
            // 동결 틴트는 조명을 받게 함(_FlashLit 1) → 어두운 방에서 혼자 빛나지 않음
            ApplyFlash(frostTintColor, frostTintAmount, 1f);

            t += Time.deltaTime;
            yield return null;
        }

        isFrozen = false;

        if (frozenVisual != null)
            Destroy(frozenVisual);

        ClearFlash();
    }

    IEnumerator HitFlash()
    {
        // _FlashColor를 명시적으로 흰색으로 지정 — 안 그러면 직전 냉기 동결(파란 틴트)이 남긴
        // _FlashColor 값을 그대로 물려받아서, 얼렸다 푼 직후 피격 시 흰색이 아니라 파란색으로 반짝임
        // 피격 흰색은 조명과 상관없이 보이게 함(_FlashLit 0)
        ApplyFlash(Color.white, 1f, 0f);

        yield return new WaitForSeconds(hitFlashTime);

        ClearFlash();
    }

    // ── 피격 반짝임 / 동결 틴트 공통 처리 ──
    // 지금 머티리얼이 반짝임을 직접 지원하면(어두운 방에서 쓰는 SpriteLit3D) 머티리얼은 그대로 두고 값만 넣음 → 조명이 유지됨
    // 지원하지 않으면 기존처럼 flashMaterial 로 잠시 바꾸고, 끝나면 "바꾸기 직전" 머티리얼로 되돌림
    // (예전에는 시작할 때 기억한 머티리얼로 되돌려서, 어두운 방에서 바뀐 조명용 머티리얼이 벗겨졌음)
    Material materialBeforeFlash;

    bool CurrentMaterialSupportsFlash()
    {
        Material m = sr.sharedMaterial;
        return m != null && m != flashMaterial && m.HasProperty("_FlashLit");
    }

    void ApplyFlash(Color color, float amount, float lit)
    {
        if (sr == null || flashMaterial == null) return;
        if (!CurrentMaterialSupportsFlash() && sr.sharedMaterial != flashMaterial)
        {
            materialBeforeFlash = sr.sharedMaterial;
            sr.sharedMaterial = flashMaterial;
        }
        // SpriteRenderer가 내부적으로 관리하는 _MainTex 바인딩이 SetPropertyBlock으로
        // 덮어써지면서 사라지는 문제가 있어, 현재 스프라이트 텍스처를 직접 다시 넣어줌
        sr.GetPropertyBlock(flashBlock);
        if (sr.sprite != null) flashBlock.SetTexture("_MainTex", sr.sprite.texture);
        flashBlock.SetColor("_FlashColor", color);
        flashBlock.SetFloat("_FlashAmount", amount);
        flashBlock.SetFloat("_FlashLit", lit);
        sr.SetPropertyBlock(flashBlock);
    }

    void ClearFlash()
    {
        if (sr == null || flashMaterial == null) return;
        if (sr.sharedMaterial == flashMaterial)
            sr.sharedMaterial = materialBeforeFlash != null ? materialBeforeFlash : originalMaterial;
        materialBeforeFlash = null;
        sr.SetPropertyBlock(null);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, Enemyeye);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
