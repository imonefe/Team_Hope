using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;

/// <summary>
/// [2026-11-17 신규] 중간보스 공용 컨트롤러.
///
/// 흐름: 스폰 즉시 전투 시작 → 대기 애니만 재생하며 idleTime 초 기다림(플레이어 쪽을 계속 바라봄)
///      → 패턴 목록에서 직전과 다른 것 하나를 무작위로 고름
///      → (필요하면) 플레이어에게 접근: 순간이동 또는 직접 걸어서
///      → 공격 애니 재생 + "출력 목록"에 적힌 시간마다 이펙트·히트박스 생성
///      → 회복 시간 후 다시 대기
///
/// 애니메이션은 두 방식 중 하나를 고른다 (animMode).
///  - 애니메이터: isIdle / isMove 불값, isHit 트리거, 패턴별 트리거 (1스테이지 보스와 같은 방식)
///  - 스파인:     SkeletonAnimation 의 애니 이름으로 재생
///
/// 체력·피격·사망·퍼즈 정지·재화 드랍은 부모(BossController)가 그대로 처리한다.
/// 필요한 컴포넌트: Rigidbody2D, Stats, Health, Collider2D (1스테이지 보스와 같음)
/// </summary>
public class MidBossController : BossController
{
    public enum AnimMode { Animator, Spine }

    [System.Serializable]
    public class AttackSpawn
    {
        [Tooltip("공격 애니가 시작된 뒤 몇 초에 나갈지")]
        public float time = 0.3f;
        [Tooltip("이펙트 프리팹 (비워도 됨)")]
        public GameObject effectPrefab;
        [Tooltip("히트박스 프리팹 (Hitbox 컴포넌트가 붙은 것, 비워도 됨)")]
        public GameObject hitboxPrefab;
        [Tooltip("보스 기준 위치 보정. x 는 보스가 바라보는 쪽이 + (왼쪽을 보면 자동으로 뒤집힘)")]
        public Vector2 offset = new Vector2(1.5f, 0f);
        [Tooltip("켜면 생성된 이펙트·히트박스가 보스를 따라다님 (보스의 자식이 됨)")]
        public bool followBoss = false;
        [Tooltip("히트박스 데미지 = 보스 공격력 × 이 값")]
        public float damageMultiplier = 1f;
        [Tooltip("이펙트를 몇 초 뒤 지울지 (0 = 이펙트가 알아서 사라짐 — AutoPlayEffect 등)")]
        public float effectLifetime = 0f;
    }

    [System.Serializable]
    public class Pattern
    {
        [Tooltip("알아보기 쉬운 이름 (예: 돌진 베기)")]
        public string name = "패턴";

        [Header("애니메이션")]
        [Tooltip("애니메이터 방식: 공격 트리거 이름")]
        public string animTrigger = "Attack1";
        [Tooltip("애니메이터 방식: 공격 상태 이름 (끝날 때까지 기다리는 데 씀)")]
        public string animState = "Attack1";
        [Tooltip("스파인 방식: 공격 애니 이름")]
        public string spineAnim = "attack1";
        [Tooltip("공격 애니를 최대 몇 초까지 기다릴지 (애니가 안 끝나도 이 시간이 지나면 다음으로)")]
        public float maxAnimTime = 3f;

        [Header("플레이어에게 접근")]
        [Tooltip("켜면 공격 전에 플레이어에게 다가감")]
        public bool needsApproach = false;
        [Tooltip("켜면 순간이동, 끄면 직접 걸어서 이동")]
        public bool useTeleport = false;
        [Tooltip("플레이어와 이 거리(가로)까지 다가감")]
        public float approachDistance = 2f;
        [Tooltip("직접 이동일 때 걷는 속도")]
        public float approachSpeed = 5f;
        [Tooltip("직접 이동일 때 이 시간 안에 못 다가가면 그 자리에서 공격")]
        public float maxApproachTime = 4f;
        [Tooltip("순간이동: 사라질 때 이펙트 (원래 자리)")]
        public GameObject teleportOutFx;
        [Tooltip("순간이동: 나타날 때 이펙트 (도착 자리)")]
        public GameObject teleportInFx;
        [Tooltip("순간이동: 사라져 있는 시간 (이동 중 무적)")]
        public float teleportHideTime = 0.35f;
        [Tooltip("부유 캐릭터일 때만: 접근해서 멈출 높이 = 플레이어 높이 + 이 값 (0 = 같은 높이, 2 = 위에서 내려찍기 자세)")]
        public float flyHeightOffset = 0f;

        [Header("공격 출력 (이펙트·히트박스)")]
        public AttackSpawn[] spawns;

        [Header("공격 후")]
        [Tooltip("공격이 끝난 뒤 대기로 돌아가기 전 쉬는 시간")]
        public float recoveryTime = 0.4f;
    }

    // ───────────────────────── 인스펙터 ─────────────────────────
    [Header("[ 중간보스 — 대기 ]")]
    [Tooltip("대기 애니만 재생하는 시간 a (초). 이 시간이 지나면 공격")]
    public float idleTime = 2f;
    [Tooltip("대기 시간에 무작위로 더해지는 최대값 (0 = 항상 idleTime)")]
    public float idleRandomExtra = 0f;
    [Tooltip("대기하는 동안 플레이어 쪽으로 몸을 돌림")]
    public bool faceDuringIdle = true;

    [Header("[ 중간보스 — 피격 ]")]
    [Tooltip("끄면 맞아도 피격 애니를 재생하지 않음 (데미지는 그대로 들어감)")]
    public bool useHitReaction = true;

    [Header("[ 중간보스 — 애니메이션 방식 ]")]
    public AnimMode animMode = AnimMode.Animator;
    [Tooltip("스파인 방식일 때 보스 몸통의 SkeletonAnimation")]
    public SkeletonAnimation skeletonAnimation;
    [Tooltip("스파인: 대기 애니 이름")]
    public string spineIdle = "idle";
    [Tooltip("스파인: 이동 애니 이름 (직접 이동할 때)")]
    public string spineMove = "move";
    [Tooltip("스파인: 피격 애니 이름 (비우면 피격 애니 없음)")]
    public string spineHit = "hit";

    [Header("[ 중간보스 — 패턴 (3~4개 권장) ]")]
    public Pattern[] patterns;

    public enum HoverReference { Ground, Player }

    [Header("[ 중간보스 — 공중 부유 ]")]
    [Tooltip("켜면 중력을 끄고 공중에 떠 있음. 걷기 대신 날아서 이동")]
    public bool isFloating = false;
    [Tooltip("기준 높이에서 몇 칸 위에 떠 있을지")]
    public float hoverHeight = 2f;
    [Tooltip("높이 기준: Ground = 보스 아래 땅, Player = 플레이어 높이")]
    public HoverReference hoverReference = HoverReference.Ground;
    [Tooltip("밀리거나 이동 뒤 제 높이로 돌아가는 빠르기 (클수록 빨리)")]
    public float hoverSpeed = 3f;
    [Tooltip("둥실거림 폭 (칸). 0 = 둥실거림 없음")]
    public float bobAmount = 0.15f;
    [Tooltip("둥실거림 빠르기 (1초에 몇 번 오르내리는지)")]
    public float bobSpeed = 0.6f;
    [Tooltip("둥실거릴 그림 오브젝트 (스파인·스프라이트가 붙은 자식). 비우면 둥실거림 없음 — 판정은 흔들리지 않음")]
    public Transform bobTarget;

    [Header("[ 중간보스 — 순간이동 바닥 찾기 ]")]
    [Tooltip("순간이동 도착 지점 아래 바닥을 찾을 레이어")]
    public LayerMask groundMask;

    // ───────────────────────── 내부 ─────────────────────────
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    readonly List<Collider2D> hiddenColliders = new List<Collider2D>();
    bool hiddenByTeleport;
    float spineBaseTimeScale = 1f;
    Vector3 bobBaseLocal;
    float bobClock;

    bool IsSpine => animMode == AnimMode.Spine && skeletonAnimation != null;

    protected override bool HasPhase2 => false;   // 중간보스는 부활하지 않음

    // 컴포넌트를 처음 붙일 때 기본값 (기획서: 중간보스 재화 60~80, 보스 처치 수에는 안 셈)
    void Reset()
    {
        countAsBossKill = false;
        minCurrencyDrop = 60;
        maxCurrencyDrop = 80;
        groundMask = LayerMask.GetMask("ground", "platform");
    }

    protected override void Awake()
    {
        if (animator == null && animMode == AnimMode.Animator) animator = GetComponentInChildren<Animator>();
        if (skeletonAnimation == null && animMode == AnimMode.Spine) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("ground", "platform");
        if (skeletonAnimation != null) spineBaseTimeScale = skeletonAnimation.timeScale;
        base.Awake();
        if (isFloating && rigid != null) rigid.gravityScale = 0f;
        if (bobTarget != null) bobBaseLocal = bobTarget.localPosition;
    }

    void Start()
    {
        // 스폰되자마자 전투 시작 (플레이어 감지 범위 없음)
        Activate();
    }

    protected override void OnActivate()
    {
        // 부모의 대기 루프가 쓰는 시간에 a 를 넣는다 (매 대기마다 OnCooldownStart 에서 다시 정함)
        cooldownTime = idleTime;
    }

    protected override void Update()
    {
        base.Update();
        if (faceDuringIdle && state == BossState.Cooldown && player != null && !IsWorldPaused)
        {
            // 플레이어가 거의 바로 위·아래에 있으면 돌지 않음 (매 프레임 좌우로 떨리는 것 방지)
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.2f)
            {
                int dir = dx > 0f ? 1 : -1;
                if (dir != lockedDir) FlipTo(dir);
            }
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        // 퍼즈 중엔 0, 풀리면 제작자가 정해 둔 재생 속도로 되돌림 (1로 덮어쓰지 않음)
        if (IsSpine) skeletonAnimation.timeScale = IsWorldPaused ? 0f : spineBaseTimeScale;

        if (isFloating && !IsWorldPaused && !hiddenByTeleport && rigid != null && rigid.simulated)
        {
            rigid.gravityScale = 0f;
            if (state == BossState.Inactive || state == BossState.Cooldown)
            {
                // 목표 높이로 부드럽게 (가로 속도는 건드리지 않음)
                float vy = 0f;
                if (TryGetHoverTargetY(out float targetY))
                    vy = Mathf.Clamp((targetY - transform.position.y) * hoverSpeed, -8f, 8f);
                rigid.velocity = new Vector2(rigid.velocity.x * 0.8f, vy);
            }
            else if (state == BossState.Attacking)
            {
                rigid.velocity = Vector2.zero;   // 공격 중엔 공중 정지 (공격 위치가 흔들리지 않게)
            }
        }
    }

    // 부유 높이 목표. 바닥 기준인데 아래가 낭떠러지면 false (지금 높이 유지)
    bool TryGetHoverTargetY(out float y)
    {
        y = transform.position.y;
        if (hoverReference == HoverReference.Player)
        {
            if (player == null) return false;
            y = player.position.y + hoverHeight;
            return true;
        }
        RaycastHit2D below = Physics2D.Raycast(transform.position, Vector2.down, hoverHeight + 10f, groundMask);
        if (below.collider == null) return false;
        y = below.point.y + hoverHeight;
        return true;
    }

    // 둥실거림: 그림 자식만 위아래로 (퍼즈 중엔 멈춤)
    void LateUpdate()
    {
        if (!isFloating || bobTarget == null || bobAmount <= 0f) return;
        if (!IsWorldPaused) bobClock += Time.deltaTime;
        bobTarget.localPosition = bobBaseLocal + new Vector3(0f, Mathf.Sin(bobClock * bobSpeed * Mathf.PI * 2f) * bobAmount, 0f);
    }

    // ───────────────────────── 대기 ─────────────────────────
    protected override void OnSetInactive() { PlayIdle(); }

    protected override void OnCooldownStart()
    {
        base.OnCooldownStart();          // 트리거 정리 + (애니메이터면) 대기 불값
        cooldownTime = idleTime + (idleRandomExtra > 0f ? Random.Range(0f, idleRandomExtra) : 0f);
        PlayIdle();
    }

    protected override void ResetAttackTriggers()
    {
        if (animator == null || patterns == null) return;
        foreach (var p in patterns)
            if (!string.IsNullOrEmpty(p.animTrigger)) animator.ResetTrigger(p.animTrigger);
        animator.ResetTrigger("isHit");
    }

    // ───────────────────────── 패턴 ─────────────────────────
    protected override int GetPatternCount() => patterns != null ? patterns.Length : 0;

    protected override IEnumerator ExecutePattern(int index)
    {
        if (patterns == null || patterns.Length == 0) yield break;
        Pattern p = patterns[Mathf.Clamp(index, 0, patterns.Length - 1)];

        // 1) 접근
        if (p.needsApproach && player != null)
        {
            state = BossState.Moving;
            if (p.useTeleport) yield return TeleportNearPlayer(p);
            else if (isFloating) yield return FlyToPlayer(p);
            else yield return WalkToPlayer(p);
        }

        // 2) 방향 고정 후 공격
        if (player != null) FlipTo(player.position.x >= transform.position.x ? 1 : -1);
        state = BossState.Attacking;
        rigid.velocity = new Vector2(0f, rigid.velocity.y);

        Coroutine spawnCo = StartCoroutine(RunSpawns(p));
        yield return PlayAttackAndWait(p);
        yield return spawnCo;                 // 출력 시간이 애니보다 길면 끝까지 다 내보낸 뒤 진행

        // 3) 회복
        PlayIdle();
        if (p.recoveryTime > 0f) yield return PausableWait(p.recoveryTime);
    }

    // 출력 목록을 시간 순서대로 (퍼즈 중엔 시계가 멈춤)
    IEnumerator RunSpawns(Pattern p)
    {
        if (p.spawns == null || p.spawns.Length == 0) yield break;
        var list = new List<AttackSpawn>(p.spawns);
        list.Sort((a, b) => a.time.CompareTo(b.time));

        float t = 0f; int i = 0;
        while (i < list.Count)
        {
            while (i < list.Count && list[i].time <= t) { Spawn(list[i]); i++; }
            if (i >= list.Count) break;
            yield return null;
            if (!IsWorldPaused) t += Time.deltaTime;
        }
    }

    void Spawn(AttackSpawn s)
    {
        int dir = lockedDir >= 0 ? 1 : -1;
        Vector3 pos = transform.position + new Vector3(s.offset.x * dir, s.offset.y, 0f);

        // 먼저 바라보는 쪽으로 뒤집은 뒤 붙인다 — 붙인 다음 뒤집으면 왼쪽을 볼 때 한 번 더 뒤집혀 반대가 됨
        if (s.effectPrefab != null)
        {
            GameObject fx = Instantiate(s.effectPrefab, pos, s.effectPrefab.transform.rotation);
            FaceObject(fx.transform, dir);
            if (s.followBoss) fx.transform.SetParent(transform, true);
            if (s.effectLifetime > 0f) Destroy(fx, s.effectLifetime);
        }

        if (s.hitboxPrefab != null)
        {
            GameObject obj = Instantiate(s.hitboxPrefab, pos, s.hitboxPrefab.transform.rotation);
            FaceObject(obj.transform, dir);
            if (s.followBoss) obj.transform.SetParent(transform, true);

            Hitbox hb = obj.GetComponent<Hitbox>();
            if (hb != null)
            {
                hb.direction = dir;
                hb.damage = Mathf.RoundToInt(stats.Atk * s.damageMultiplier);
                hb.attackerStats = stats;
                hb.ownerLayer = "Enemy";
            }
        }
    }

    static void FaceObject(Transform t, int dir)
    {
        Vector3 sc = t.localScale;
        sc.x = Mathf.Abs(sc.x) * dir;
        t.localScale = sc;
    }

    // ───────────────────────── 접근: 직접 이동 ─────────────────────────
    IEnumerator WalkToPlayer(Pattern p)
    {
        PlayMove();
        float timer = 0f;
        while (player != null)
        {
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= p.approachDistance) break;
            if (timer >= p.maxApproachTime) break;

            int dir = dx >= 0 ? 1 : -1;
            if (dir != lockedDir) FlipTo(dir);
            if (!HasGroundAhead(dir)) break;   // 앞이 낭떠러지면 멈추고 그 자리에서 공격
            if (!IsWorldPaused)
            {
                rigid.velocity = new Vector2(dir * p.approachSpeed, rigid.velocity.y);
                timer += Time.deltaTime;
            }
            yield return null;
        }
        rigid.velocity = new Vector2(0f, rigid.velocity.y);
        PlayIdle();
    }

    // ───────────────────────── 접근: 날아서 이동 (부유 캐릭터) ─────────────────────────
    IEnumerator FlyToPlayer(Pattern p)
    {
        PlayMove();
        float timer = 0f;
        while (player != null && timer < p.maxApproachTime)
        {
            float side = Mathf.Sign(transform.position.x - player.position.x);
            if (side == 0f) side = -lockedDir;
            Vector2 target = new Vector2(player.position.x + side * p.approachDistance, player.position.y + p.flyHeightOffset);
            Vector2 to = target - (Vector2)transform.position;
            if (to.magnitude <= 0.2f) break;

            int face = player.position.x >= transform.position.x ? 1 : -1;
            if (face != lockedDir) FlipTo(face);
            if (!IsWorldPaused)
            {
                // 마지막 구간은 속도를 줄여서 목표를 지나치지 않게
                float spd = Mathf.Min(p.approachSpeed, to.magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
                rigid.velocity = to.normalized * spd;
                timer += Time.deltaTime;
            }
            yield return null;
        }
        rigid.velocity = Vector2.zero;
        PlayIdle();
    }

    // ───────────────────────── 접근: 순간이동 ─────────────────────────
    IEnumerator TeleportNearPlayer(Pattern p)
    {
        if (p.teleportOutFx != null) Instantiate(p.teleportOutFx, transform.position, p.teleportOutFx.transform.rotation);
        SetHidden(true);
        rigid.velocity = Vector2.zero;

        if (p.teleportHideTime > 0f) yield return PausableWait(p.teleportHideTime);

        if (player != null)
        {
            // 보스가 있던 쪽에서 approachDistance 만큼 떨어진 자리 (플레이어 등 뒤로 넘어가지 않음)
            // 그 자리가 벽 속이면 반대쪽, 둘 다 막혀 있으면 플레이어 바로 위치로
            float side = Mathf.Sign(transform.position.x - player.position.x);
            if (side == 0f) side = -lockedDir;
            float feet = FeetOffset();
            float tx = player.position.x + side * p.approachDistance;
            float ty = ArriveY(tx, feet, p);
            if (IsInsideWall(tx, ty))
            {
                float tx2 = player.position.x - side * p.approachDistance;
                float ty2 = ArriveY(tx2, feet, p);
                if (!IsInsideWall(tx2, ty2)) { tx = tx2; ty = ty2; }
                else { tx = player.position.x; ty = ArriveY(tx, feet, p); }
            }
            transform.position = new Vector3(tx, ty, transform.position.z);
        }

        SetHidden(false);
        if (p.teleportInFx != null) Instantiate(p.teleportInFx, transform.position, p.teleportInFx.transform.rotation);
    }

    // 보스 발밑에서 중심까지의 높이 (순간이동 직전, 아직 원래 자리에 있을 때 잼)
    float FeetOffset()
    {
        RaycastHit2D below = Physics2D.Raycast(transform.position, Vector2.down, 20f, groundMask);
        return below.collider != null ? transform.position.y - below.point.y : 0f;
    }

    // 도착 지점 아래 바닥 높이 + 발밑 높이
    // 광선은 플레이어 중심 높이에서 아래로 쏜다 — 위에서 쏘면 플레이어 머리 위 발판에 내려앉음
    float FindLandingY(float x, float feetOffset)
    {
        Vector2 from = new Vector2(x, player.position.y);
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, 30f, groundMask);
        if (hit.collider != null && hit.distance > 0f) return hit.point.y + feetOffset;
        return player.position.y;   // 바닥을 못 찾으면 플레이어 높이 (그 뒤 중력으로 떨어짐)
    }

    // 도착 높이: 부유 캐릭터는 공중(플레이어 높이 + 공격 높이), 아니면 바닥
    float ArriveY(float x, float feet, Pattern p)
    {
        if (isFloating) return player.position.y + p.flyHeightOffset;
        return FindLandingY(x, feet);
    }

    bool IsInsideWall(float x, float y)
    {
        return Physics2D.OverlapCircle(new Vector2(x, y), 0.3f, groundMask) != null;
    }

    // 걸어가는 방향 앞쪽 아래에 바닥이 있는지
    bool HasGroundAhead(int dir)
    {
        float feet = FeetOffset();
        Vector2 from = new Vector2(transform.position.x + dir * 0.8f, transform.position.y);
        return Physics2D.Raycast(from, Vector2.down, feet + 1.5f, groundMask).collider != null;
    }

    // 순간이동 중엔 안 보이고, 맞지도 않음
    void SetHidden(bool hide)
    {
        if (hide)
        {
            hiddenRenderers.Clear(); hiddenColliders.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            foreach (var c in GetComponentsInChildren<Collider2D>())
                if (c.enabled) { c.enabled = false; hiddenColliders.Add(c); }
            rigid.simulated = false;
            hiddenByTeleport = true;
        }
        else
        {
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var c in hiddenColliders) if (c != null) c.enabled = true;
            hiddenRenderers.Clear(); hiddenColliders.Clear();
            rigid.simulated = true;
            hiddenByTeleport = false;
        }
    }

    // 순간이동 도중 죽는 경우 대비 — 몸을 다시 보이게 한 뒤 부모 사망 처리
    protected override void OnTrueDeath()
    {
        if (hiddenByTeleport) SetHidden(false);
        base.OnTrueDeath();
    }

    // ───────────────────────── 피격 ─────────────────────────
    protected override void OnHitEffect(int dir)
    {
        if (!useHitReaction) return;
        if (state == BossState.Attacking || state == BossState.Moving) return;   // 공격·이동 중엔 생략 (애니 꼬임 방지)

        if (IsSpine)
        {
            if (string.IsNullOrEmpty(spineHit) || !HasSpineAnim(spineHit)) return;
            skeletonAnimation.AnimationState.SetAnimation(0, spineHit, false);
            if (HasSpineAnim(spineIdle)) skeletonAnimation.AnimationState.AddAnimation(0, spineIdle, true, 0f);
        }
        else if (animator != null)
        {
            animator.SetTrigger("isHit");
        }
    }

    // ───────────────────────── 애니 재생 (두 방식 공통 창구) ─────────────────────────
    void PlayIdle()
    {
        if (IsSpine) SetSpineLoop(spineIdle);
        else if (animator != null) { animator.SetBool("isIdle", true); animator.SetBool("isMove", false); }
    }

    void PlayMove()
    {
        if (IsSpine) SetSpineLoop(string.IsNullOrEmpty(spineMove) ? spineIdle : spineMove);
        else if (animator != null) { animator.SetBool("isMove", true); animator.SetBool("isIdle", false); }
    }

    IEnumerator PlayAttackAndWait(Pattern p)
    {
        if (IsSpine)
        {
            if (string.IsNullOrEmpty(p.spineAnim) || !HasSpineAnim(p.spineAnim)) yield break;
            TrackEntry e = skeletonAnimation.AnimationState.SetAnimation(0, p.spineAnim, false);
            if (HasSpineAnim(spineIdle)) skeletonAnimation.AnimationState.AddAnimation(0, spineIdle, true, 0f);

            float timer = 0f;
            while (e != null && !e.IsComplete && timer < p.maxAnimTime)
            {
                if (!IsWorldPaused) timer += Time.deltaTime;
                yield return null;
            }
        }
        else if (animator != null)
        {
            if (!string.IsNullOrEmpty(p.animTrigger)) animator.SetTrigger(p.animTrigger);
            if (!string.IsNullOrEmpty(p.animState)) yield return WaitAnimFinish(p.animState, p.maxAnimTime);
            else yield return PausableWait(p.maxAnimTime);
        }
    }

    void SetSpineLoop(string anim)
    {
        if (!IsSpine || string.IsNullOrEmpty(anim) || !HasSpineAnim(anim)) return;
        TrackEntry cur = skeletonAnimation.AnimationState.GetTrack(0);
        if (cur != null && cur.Animation != null && cur.Animation.Name == anim && cur.Loop) return;   // 이미 재생 중
        skeletonAnimation.AnimationState.SetAnimation(0, anim, true);
    }

    bool HasSpineAnim(string anim)
    {
        if (skeletonAnimation == null || skeletonAnimation.Skeleton == null) return false;
        bool ok = skeletonAnimation.Skeleton.Data.FindAnimation(anim) != null;
#if UNITY_EDITOR
        if (!ok) Debug.LogWarning($"[중간보스] 스파인에 '{anim}' 애니가 없음 — 이름 확인 필요 ({name})", this);
#endif
        return ok;
    }

    // ───────────────────────── 기즈모: 패턴 접근 거리 ─────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        if (patterns == null) return;
        Gizmos.color = new Color(1f, .6f, .1f, .8f);
        foreach (var p in patterns)
            if (p != null && p.needsApproach)
                Gizmos.DrawWireSphere(transform.position, p.approachDistance);
    }
}
