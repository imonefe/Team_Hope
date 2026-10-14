using System.Collections;
using UnityEngine;

/// <summary>
/// 1스테이지 보스
/// BossController 상속 후 패턴만 구현
/// </summary>
public class Boss1Controller : BossController
{
    [Header("[ 1스테이지 보스 전용 ]")]
    public float comboDistance = 2f;   // 콤보 시작 거리
    public float comboDashForce = 3f;   // 콤보 타격마다 이동하는 힘
    public float slamDistance = 3f;   // 차징 시작 거리
    public float fallDelay = 1.5f; // 낙하 전 대기 시간
    [Tooltip("낙하기 시작 때 점프 자세를 보여주는 시간(초)")]
    public float fallWindupTime = 0.5f;
    public float jumpHeight = 10f;  // 낙하기 이동 높이

    // 콤보 연결 플래그 (애니메이션 이벤트로 제어)
    bool comboNextReady = false;

    [Header("[ 낙하기 이펙트 ]")]
    public GameObject jumpEffectPrefab;   // 도약 순간 이펙트
    public Vector2 jumpEffectOffset;      // 도약 이펙트 스폰 위치 보정값 (보스 기준 상대 오프셋)
    public GameObject landEffectPrefab;   // 착지 순간 이펙트
    public Vector2 landEffectOffset;      // 착지 이펙트 스폰 위치 보정값 (보스 기준 상대 오프셋)

    // ── 패턴 개수 ─────────────────────────────────────────
    protected override int GetPatternCount() => 3;

    // ── 공격 트리거 리셋 ──────────────────────────────────
    // 패턴이 피격/타임아웃 등으로 중간에 끊기면 이 보스가 쓰는 트리거들 중 일부가
    // 소비되지 않은 채 남을 수 있음 — 매 쿨다운 시작마다(BossController.OnCooldownStart) 호출되어 전부 비움
    protected override void ResetAttackTriggers()
    {
        animator.ResetTrigger("Combo1");
        animator.ResetTrigger("Combo2");
        animator.ResetTrigger("Combo3");
        animator.ResetTrigger("Fall");
        animator.ResetTrigger("Slam");
        animator.ResetTrigger("Charge");
        animator.ResetTrigger("isHit");
    }

    // ── 패턴 선택 ─────────────────────────────────────────
    protected override IEnumerator ExecutePattern(int index)
    {
        switch (index)
        {
            case 0: yield return Pattern_Combo(); break;
            case 1: yield return Pattern_Falling(); break;
            case 2: yield return Pattern_Slam(); break;
        }
    }

    // ── 패턴 1: 3단 콤보 ──────────────────────────────────
    IEnumerator Pattern_Combo()
    {
        state = BossState.Moving;
        yield return MoveToPlayer(comboDistance);

        // 방향 잠금
        lockedDir = player.position.x > transform.position.x ? 1 : -1;
        FlipTo(lockedDir);

        state = BossState.Attacking;

        // 1타 (방향 잠금 유지)
        comboNextReady = false;
        animator.SetTrigger("Combo1");
        yield return WaitComboNext();

        // 2타 (방향 잠금 유지)
        FlipTo(lockedDir);
        comboNextReady = false;
        animator.SetTrigger("Combo2");
        yield return WaitComboNext();

        // 3타 (플레이어 방향 재감지)
        lockedDir = player.position.x > transform.position.x ? 1 : -1;
        FlipTo(lockedDir);
        animator.SetTrigger("Combo3");
        yield return WaitAnimFinish("Combo3");  // 마지막은 끝까지 재생
    }

    // 콤보 연결 대기 (애니메이션 이벤트 OnComboNext가 켜주거나 타임아웃)
    IEnumerator WaitComboNext(float maxWait = 2f)
    {
        float timer = 0f;
        while (!comboNextReady && timer < maxWait)
        {
            if (!IsWorldPaused) timer += Time.deltaTime;
            yield return null;
        }
    }

    // 애니메이션 이벤트에서 호출 — 다음 콤보로 넘어가도 되는 프레임에 찍기
    public void OnComboNext()
    {
        comboNextReady = true;
    }

    // ── 패턴 2: 낙하기 ────────────────────────────────────
    IEnumerator Pattern_Falling()
    {
        state = BossState.Attacking;

        // 1. 점프 애니메이션 (보이는 상태)
        // [2026-10-10 수정] Fall 상태는 재생 속도가 거의 0이라 끝나지 않아서 매번 3초(최대 대기)를 기다렸다
        // → 정해진 시간만 점프 자세를 보여준다
        animator.SetTrigger("Fall");
        yield return PausableWait(fallWindupTime);

        // 2. 스프라이트 숨기고 플레이어 위치 위로 이동
        // 도약 이펙트 (보스가 서있던 자리)
        if (jumpEffectPrefab != null)
        {
            GameObject jumpFx = Instantiate(jumpEffectPrefab, transform.position + (Vector3)jumpEffectOffset, Quaternion.identity);
            Destroy(jumpFx, 2f); // 자식 파티클(AutoPlayEffect)은 스스로 사라지지만 루트 컨테이너는 안 사라져서 남는 문제 방지
        }

        spriteRenderer.enabled = false;
        rigid.velocity = Vector2.zero;
        rigid.simulated = false;

        yield return PausableWait(fallDelay);

        Vector3 targetPos = new Vector3(
            player.position.x,
            player.position.y + jumpHeight,
            transform.position.z);
        transform.position = targetPos;

        // 3. 나타나서 낙하
        spriteRenderer.enabled = true;
        rigid.simulated = true;
        rigid.velocity = new Vector2(0, -20f);

        // 4. 착지 감지 (레이캐스트 사용 — simulated 여부 무관)
        float landTimeout = 5f;
        float landTimer = 0f;
        while (!IsGroundedRaycast() && landTimer < landTimeout)
        {
            if (!IsWorldPaused) landTimer += Time.deltaTime;
            yield return null;
        }

        // 5. 착지 (중력으로 자연 착지 — velocity 강제 조작 안 함)
        // 착지 이펙트 (착지한 지점)
        if (landEffectPrefab != null)
        {
            GameObject landFx = Instantiate(landEffectPrefab, transform.position + (Vector3)landEffectOffset, Quaternion.identity);
            Destroy(landFx, 2f); // 자식 파티클(AutoPlayEffect)은 스스로 사라지지만 루트 컨테이너는 안 사라져서 남는 문제 방지
        }

        animator.SetTrigger("Slam");
        SpawnHitboxByKey("FallSlam");
        yield return WaitAnimFinish("Slam");
    }

    // [2026-10-10 수정] 보스는 platform(한쪽 통과 발판)과 충돌하지 않으므로 ground만 땅으로 본다.
    // 또 예전엔 발 아래 0.5칸에서 1.2칸을 쏴서 땅 위 0.5~1.7칸 공중에서 이미 착지로 판정했음 → 발바닥 바로 위에서 짧게
    bool IsGroundedRaycast()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        return Physics2D.Raycast(origin, Vector2.down, 0.3f,
               LayerMask.GetMask("ground"));
    }

    // ── 패턴 3: 차징 ──────────────────────────────────────
    IEnumerator Pattern_Slam()
    {
        state = BossState.Moving;
        yield return MoveToPlayer(slamDistance);

        state = BossState.Attacking;

        // 방향 잠금
        lockedDir = player.position.x > transform.position.x ? 1 : -1;
        FlipTo(lockedDir);

        // 차징 중 이동 정지
        rigid.velocity = new Vector2(0f, rigid.velocity.y);

        // 차징 애니메이션 (히트박스는 애니메이션 이벤트 SpawnHitbox로 소환)
        animator.SetTrigger("Charge");

        // 차징 애니메이션이 끝날 때까지 매 프레임 x이동 정지
        float chargeTimer = 0f;
        bool chargeEntered = false;
        while (!AnimStateDone("Charge", ref chargeEntered) && chargeTimer < 3f)
        {
            rigid.velocity = new Vector2(0f, rigid.velocity.y);
            if (!IsWorldPaused) chargeTimer += Time.deltaTime;
            yield return null;
        }
    }

    // 1스테이지 보스는 페이즈2(부활)가 없음 — 체력이 0이 되면 바로 진짜 사망 처리
    protected override bool HasPhase2 => false;

    // 진짜 사망
    protected override void OnTrueDeath()
    {
        base.OnTrueDeath();
        // 보스 클리어 처리 추가
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, comboDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, slamDistance);
    }
}