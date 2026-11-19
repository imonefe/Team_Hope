using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// [2026-11-19 신규] 관통(궁수) 중간보스.
///
/// 대기: 횡공격 대기 스파인(archer_idle). 근처에 플레이어가 붙으면 활대로 밀쳐내기(팅겨내기 그림).
/// 패턴 1 — 스코프 사격: 위로 화살을 쏘면(2s_archer02) 화살이 흩어져 포탈로 들어가고,
///          포탈에서 나온 스코프 3개가 플레이어를 쫓는다. 플레이어에 겹치면 잠깐 멈췄다가 그 자리를 화살이 내리꽂는다.
/// 패턴 2 — 횡·종 일격: 횡이면 플레이어 높이의 맵 오른쪽 끝, 종이면 플레이어 아래 바닥으로 순간이동해
///          경고 박스(WarningZone)를 띄운 뒤 그 줄을 화살 빛줄기가 지나간다. (횡: 2s_archer / 종: 2s_archer02)
/// 패턴 3 — 도탄 화살: 가로로 쏜 화살이 벽에 맞고 튕겨 무작위 직선으로 궁수에게 돌아오고,
///          궁수가 활대로 쳐내(팅겨내기) 플레이어 쪽으로 다시 날린다. 모든 구간에서 닿으면 피해.
/// 대시는 소스가 오면 추가한다.
///
/// 체력·피격·사망·재화 드랍·퍼즈 정지는 부모(MidBossController → BossController)가 처리한다.
/// 피해 계산은 Hitbox.DealDirectHit (근접 판정과 같은 규칙: 방어·치명타·속성 배율).
/// </summary>
public class ArcherMidBoss : MidBossController
{
    [Header("[ 궁수 — 그림 (하나만 켜짐) ]")]
    public GameObject idleVisual;          // 횡공격 대기 스파인
    public GameObject skyIdleVisual;       // 종공격 대기 스파인
    public SpriteFrameSequence shotH;      // 가로 사격 (2s_archer)
    public SpriteFrameSequence shotV;      // 위로 사격 (2s_archer02)
    public GameObject parryVisual;         // 팅겨내기
    [Tooltip("[2026-11-19] 팅겨내기 그림 좌우 반전. 켜면 원본 그림(활대가 등 뒤)을 뒤집어 활대가 바라보는 쪽 앞으로 오게 함")]
    public bool parryFlipX = true;
    [Tooltip("가로 사격에서 활시위를 놓는 그림 번호 (0부터)")]
    public int shotHReleaseFrame = 10;
    [Tooltip("위로 사격에서 활시위를 놓는 그림 번호 (0부터)")]
    public int shotVReleaseFrame = 6;
    [Tooltip("가로 사격 화살이 나오는 위치 (발 기준, 오른쪽을 볼 때)")]
    public Vector2 bowOffsetH = new Vector2(1.6f, 2.4f);
    [Tooltip("위로 사격 화살이 나오는 위치 (발 기준, 오른쪽을 볼 때)")]
    public Vector2 bowOffsetV = new Vector2(0.6f, 3.6f);

    [Header("[ 궁수 — 공통 ]")]
    [Tooltip("벽·바닥 판정 (맵 끝을 찾을 때)")]
    public LayerMask wallMask;
    public GameObject teleportFx;          // 순간이동할 때 양쪽에 (선택)
    public float teleportHide = 0.22f;
    public GameObject impactFx;            // 화살이 꽂히는 곳 (Boss2_ArrowImpactFX 등)
    public GameObject sparkFx;             // 도탄 불꽃

    [Header("[ 패턴 1 — 스코프 사격 ]")]
    public GameObject scopePrefab;
    public GameObject portalPrefab;
    public GameObject arrowPrefab;         // 날아가는 화살 (오른쪽을 향한 그림)
    public int scopeCount = 3;
    [Tooltip("스코프가 쫓는 속도 (플레이어 이동보다 빠르게)")]
    public float scopeSpeed = 11f;
    [Tooltip("스코프가 하나씩 출발하는 간격")]
    public float scopeStagger = 0.35f;
    [Tooltip("이만큼 가까워지면 플레이어에 겹친 것으로 보고 멈춤")]
    public float scopeLockDistance = 0.35f;
    public float scopeMaxChase = 3.5f;
    [Tooltip("멈춘 뒤 화살이 꽂히기까지")]
    public float scopeLockDelay = 0.45f;
    public float scopeStrikeRadius = 1.3f;
    public float scopeArrowSpeed = 40f;
    public float scopeDamage = 1.0f;
    public Color scopeColor = new Color(0.4f, 0.9f, 1f, 1f);
    public Color scopeLockColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("[ 패턴 2 — 횡·종 일격 ]")]
    public GameObject warningPrefab;       // FX_WarningZone
    public GameObject streakPrefab;        // 빛줄기 화살 (오른쪽이 머리)
    [Tooltip("[2026-11-19] 공격 준비: 이 시간 동안 플레이어 좌표를 따라가며 겨눔 (횡=높이, 종=가로 위치). 경고 박스도 같이 따라감")]
    public float lineTrackTime = 2.0f;
    [Tooltip("겨누는 위치가 따라가는 속도 (초당 칸)")]
    public float lineTrackSpeed = 9f;
    [Tooltip("겨눔을 멈춘 뒤 쏘기까지 (피할 틈)")]
    public float lineLockTime = 0.3f;
    public float lineThickness = 2.2f;
    public float streakSpeed = 60f;
    public float lineDamage = 1.3f;
    [Tooltip("횡 공격 때 오른쪽 벽에서 떨어지는 거리")]
    public float edgeMargin = 1.4f;
    [Tooltip("[2026-11-19] 종 공격 때 바닥 아래로 내려가는 깊이 (월드 단위). 이만큼 타일맵 밑에 숨어서 위로 쏨")]
    public float underDepth = 1.6f;
    [Tooltip("[2026-11-19] 바닥 밑에 있을 때 타일 뒤로 숨길지 (끄면 타일보다 위에 그려져 땅속 모습이 보임)")]
    public bool hideUnderTiles = false;
    [Tooltip("숨길 때 쓰는 그리는 순서 — 바닥 타일(1004)보다 작아야 타일 뒤로 숨음")]
    public int underSortingOrder = 1000;

    [Header("[ 패턴 2-종 — 화살비 (2026-11-19) ]")]
    [Tooltip("맵 가운데로 순간이동해 하늘로 쏘면 화살비가 떨어진다. 이 폭만큼만 안전 구간")]
    public float rainSafeWidth = 3.5f;
    [Tooltip("하늘로 쏜 뒤 화살비가 떨어지기까지 (경고 박스가 떠 있는 시간)")]
    public float rainWarnTime = 1.4f;
    [Tooltip("화살비가 쏟아지는 시간")]
    public float rainDuration = 0.8f;
    public int rainArrowCount = 26;
    public float rainArrowSpeed = 32f;
    public float rainDamage = 1.3f;
    [Tooltip("순간이동할 높이 — 바닥에서 천장까지 중 몇 % 지점 (0.35 = 아래쪽 1/3 남짓)")]
    [Range(0f, 1f)] public float rainHoverRatio = 0.35f;
    [Tooltip("[2026-11-19] 체력이 이 비율 아래로 처음 내려가면 다음 공격은 무조건 화살비 (한 번)")]
    [Range(0f, 1f)] public float rainForceHpRatio = 0.5f;

    [Header("[ 패턴 3 — 도탄 화살 ]")]
    public float ricochetSpeed = 17f;
    public float returnSpeed = 19f;
    public float batSpeed = 22f;
    [Tooltip("돌아올 때 위아래로 꺾이는 최대 각도")]
    public float returnAngle = 35f;
    [Tooltip("화살 판정 두께 (반지름). 판정은 화살 그림 길이 전체(arrowHalfLength × 2)를 따라감")]
    public float ricochetHitRadius = 0.35f;
    [Tooltip("[2026-11-19] 화살 판정 길이의 절반 — 화살 그림(FX_Archer_Arrow) 길이에 맞춤")]
    public float arrowHalfLength = 1.2f;
    public float ricochetDamage = 0.9f;
    [Tooltip("돌아온 화살을 쳐내는 거리")]
    public float batDistance = 1.6f;
    public float batPoseTime = 0.35f;

    [Header("[ 대기 중 떠다니기 (2026-11-19) ]")]
    [Tooltip("공격하지 않는 동안(대기) 대기 모션 그대로 방 안을 둥실둥실 떠다님")]
    public bool wanderWhileIdle = true;
    [Tooltip("떠다니는 속도 (초당 칸)")]
    public float wanderSpeed = 2.5f;
    [Tooltip("바닥 위로 떠다닐 높이 범위 (최소, 최대)")]
    public Vector2 wanderHeight = new Vector2(0.3f, 3.5f);
    [Tooltip("양쪽 벽에서 떨어질 거리")]
    public float wanderMargin = 2f;
    [Tooltip("다음 목적지를 고르는 간격 (초) — 도착하면 바로 새로 고름")]
    public float wanderRetarget = 2.5f;
    [Tooltip("떠다니면서 위아래로 출렁이는 폭과 빠르기")]
    public float wanderBobAmount = 0.25f;
    public float wanderBobSpeed = 0.5f;

    [Header("[ 밀쳐내기 (가까이 붙으면) ]")]
    public float pushRange = 2.6f;
    [Tooltip("[2026-11-19] 밀쳐내기(튕겨내기) 재사용 대기 시간 (초). 이 시간 안에는 가까이 붙어 있어도 일반 패턴을 씀")]
    public float pushCooldown = 6f;
    public float pushDamage = 0.6f;
    public float pushKnockX = 14f;
    public float pushKnockY = 5f;
    public Vector2 pushBox = new Vector2(3.2f, 3.5f);

    // ── 내부 ──
    GameObject[] visuals;
    SkeletonAnimation skyIdleSpine;
    readonly List<GameObject> live = new List<GameObject>();   // 패턴이 만든 것 (죽으면 정리)
    Collider2D bodyCol;
    Health playerHealth;
    float pushTimer;
    bool rainForceUsed, forceRain;
    Vector2 wanderTarget;
    float wanderTimer, wanderClock;
    bool hasWander;
    Renderer[] allRenderers;
    int[] baseOrders;
    bool isUnder;
    SpriteRenderer parrySr;

    const int P_SCOPE = 0, P_LINE = 1, P_RICOCHET = 2, P_PUSH = 3;

    protected override void Awake()
    {
        if (idleVisual != null && skeletonAnimation == null) skeletonAnimation = idleVisual.GetComponent<SkeletonAnimation>();
        animMode = AnimMode.Spine;
        useHitReaction = false;
        base.Awake();
        if (wallMask.value == 0) wallMask = LayerMask.GetMask("ground");
        visuals = new[] { idleVisual, skyIdleVisual, shotH != null ? shotH.gameObject : null, shotV != null ? shotV.gameObject : null, parryVisual };
        if (skyIdleVisual != null) skyIdleSpine = skyIdleVisual.GetComponent<SkeletonAnimation>();
        bodyCol = GetComponent<Collider2D>();
        if (parryVisual != null) parrySr = parryVisual.GetComponent<SpriteRenderer>();
        allRenderers = GetComponentsInChildren<Renderer>(true);
        baseOrders = new int[allRenderers.Length];
        for (int i = 0; i < allRenderers.Length; i++) baseOrders[i] = allRenderers[i].sortingOrder;
        ShowVisual(idleVisual);
    }

    protected override void Update()
    {
        base.Update();
        // 퍼즈 중엔 프레임 그림·종 대기 스파인도 멈춤 (가로 대기 스파인은 부모가 멈춤)
        if (pushTimer > 0f && !IsWorldPaused) pushTimer -= Time.deltaTime;   // 밀쳐내기 재사용 대기 (퍼즈 중엔 멈춤)
        float s = IsWorldPaused ? 0f : 1f;
        if (shotH != null) shotH.speed = s;
        if (shotV != null) shotV.speed = s;
        if (skyIdleSpine != null) skyIdleSpine.timeScale = s;
    }

    // 팅겨내기: 바라볼 쪽으로 돌린 뒤 그림 좌우 반전 설정을 적용
    void ShowParry(int faceDir)
    {
        if (faceDir != 0) FlipTo(faceDir);
        if (parrySr != null) parrySr.flipX = parryFlipX;
        ShowVisual(parryVisual);
    }

    // 바닥 밑에 숨을 때는 타일 뒤로, 나오면 원래 순서로
    void SetUnderground(bool under)
    {
        isUnder = under;
        if (allRenderers == null) return;
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i] != null) allRenderers[i].sortingOrder = under && hideUnderTiles ? underSortingOrder : baseOrders[i];
        if (bodyCol != null) bodyCol.isTrigger = under;   // 땅속에서 바닥 충돌에 밀려 튀지 않게
    }

    // ───────────────────────── 대기 중 떠다니기 ─────────────────────────
    // 부모(MidBossController)의 부유 처리 뒤에 속도를 덮어써서, 대기(Cooldown) 동안 방 안의 무작위 지점으로 천천히 이동
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!wanderWhileIdle || IsWorldPaused || rigid == null || !rigid.simulated) return;
        if (state != BossState.Cooldown) { hasWander = false; return; }

        wanderTimer -= Time.fixedDeltaTime;
        wanderClock += Time.fixedDeltaTime;
        Vector2 pos = rigid.position;
        if (!hasWander || wanderTimer <= 0f || (wanderTarget - pos).magnitude < 0.3f) PickWanderTarget();

        Vector2 to = wanderTarget - pos;
        float spd = Mathf.Min(wanderSpeed, to.magnitude * 2f);          // 도착 무렵 천천히
        Vector2 v = to.sqrMagnitude > 0.0001f ? to.normalized * spd : Vector2.zero;
        float w = wanderBobSpeed * Mathf.PI * 2f;
        v.y += Mathf.Cos(wanderClock * w) * wanderBobAmount * w;        // 위아래 출렁임 (사인 곡선의 속도)
        rigid.velocity = v;
    }

    void PickWanderTarget()
    {
        Vector3 from = player != null ? player.position : transform.position;
        GetRoomBounds(from, out float left, out float right, out float floor, out float ceil);
        float x0 = left + wanderMargin, x1 = right - wanderMargin;
        float x = x0 < x1 ? Random.Range(x0, x1) : (left + right) * 0.5f;
        float maxH = Mathf.Max(wanderHeight.x, Mathf.Min(wanderHeight.y, ceil - floor - 4f));   // 천장에 머리가 닿지 않게
        float y = floor + Random.Range(wanderHeight.x, maxH);
        wanderTarget = new Vector2(x, y);
        wanderTimer = wanderRetarget;
        hasWander = true;
    }

    void ShowVisual(GameObject v)
    {
        if (visuals == null) return;
        foreach (var g in visuals) if (g != null) g.SetActive(g == v);
    }

    protected override void OnCooldownStart()
    {
        base.OnCooldownStart();
        ShowVisual(idleVisual);
        SetUnderground(false);
    }

    protected override void OnTrueDeath()
    {
        foreach (var g in live) if (g != null) Destroy(g);
        live.Clear();
        if (isUnder) { transform.position += Vector3.up * underDepth; rigid.position = transform.position; }   // 땅속에서 죽으면 바닥 위로
        SetUnderground(false);
        ShowVisual(idleVisual);
        base.OnTrueDeath();
    }

    // ───────────────────────── 패턴 선택 ─────────────────────────
    protected override int GetPatternCount() => 3;

    protected override int SelectPattern()
    {
        // [2026-11-19] 체력이 절반(rainForceHpRatio) 아래로 처음 내려가면 무조건 화살비
        if (!rainForceUsed && stats != null && stats.maxHp > 0 && stats.currentHp <= stats.maxHp * rainForceHpRatio)
        {
            rainForceUsed = true;
            forceRain = true;
            return P_LINE;
        }
        // 가까이 붙으면 밀쳐내기 — 재사용 대기 시간이 지났을 때만 (직전 패턴 기록은 그대로)
        if (pushTimer <= 0f && player != null && Vector2.Distance(player.position, transform.position + Vector3.up * 1.5f) <= pushRange)
            return P_PUSH;
        return base.SelectPattern();
    }

    protected override IEnumerator ExecutePattern(int index)
    {
        if (player == null) yield break;
        state = BossState.Attacking;
        rigid.velocity = Vector2.zero;
        switch (index)
        {
            case P_SCOPE: yield return ScopePattern(); break;
            case P_LINE: yield return LinePattern(); break;
            case P_RICOCHET: yield return RicochetPattern(); break;
            default: yield return PushPattern(); break;
        }
        ShowVisual(idleVisual);
    }

    void FacePlayer()
    {
        if (player == null) return;
        FlipTo(player.position.x >= transform.position.x ? 1 : -1);
    }

    Vector3 Bow(Vector2 off) => transform.position + new Vector3(off.x * lockedDir, off.y, 0f);

    // 프레임 그림을 재생하고 활시위를 놓는 그림까지 기다린다
    IEnumerator PlayShotUntilRelease(SpriteFrameSequence seq, int releaseFrame)
    {
        if (seq == null) yield break;
        ShowVisual(seq.gameObject);
        seq.Play();
        float guard = 0f;
        while (seq.CurrentFrame < releaseFrame && !seq.Finished && guard < 5f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitSeqEnd(SpriteFrameSequence seq)
    {
        if (seq == null) yield break;
        float guard = 0f;
        while (!seq.Finished && guard < 5f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
    }

    // ───────────────────────── 패턴 1 — 스코프 사격 ─────────────────────────
    IEnumerator ScopePattern()
    {
        FacePlayer();
        yield return PlayShotUntilRelease(shotV, shotVReleaseFrame);

        // 포탈 자리: 천장 아래, 방 안에서 흩어지게
        GetRoomBounds(player.position, out float left, out float right, out float floor, out float ceil);
        int n = Mathf.Max(1, scopeCount);
        var portals = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float x = Mathf.Lerp(left + 1.5f, right - 1.5f, t) + Random.Range(-1f, 1f);
            portals[i] = new Vector3(x, ceil - Random.Range(1.2f, 2.4f), 0f);
        }

        // 화살이 흩어지며 포탈로 들어감
        var portalObjs = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            portalObjs[i] = Make(portalPrefab, portals[i], Quaternion.identity);
            StartCoroutine(FlyVisual(arrowPrefab, Bow(bowOffsetV), portals[i], 30f, true));
        }
        yield return PausableWait(0.35f);
        ShowVisual(idleVisual);

        // 스코프 출발 (하나씩)
        int done = 0;
        for (int i = 0; i < n; i++)
        {
            StartCoroutine(OneScope(portals[i], () => done++));
            yield return PausableWait(scopeStagger);
        }
        float guard = 0f;
        while (done < n && guard < scopeMaxChase + scopeLockDelay + 3f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
        foreach (var p in portalObjs) if (p != null) { live.Remove(p); Destroy(p, 0.2f); }
        yield return PausableWait(0.3f);
    }

    IEnumerator OneScope(Vector3 portal, System.Action onDone)
    {
        GameObject scope = Make(scopePrefab, portal, Quaternion.identity);
        SpriteRenderer sr = scope != null ? scope.GetComponentInChildren<SpriteRenderer>() : null;
        if (sr != null) sr.color = scopeColor;

        // 플레이어를 쫓음
        float t = 0f;
        while (scope != null && player != null && t < scopeMaxChase)
        {
            if (!IsWorldPaused)
            {
                Vector3 target = PlayerCenter();
                scope.transform.position = Vector3.MoveTowards(scope.transform.position, target, scopeSpeed * Time.deltaTime);
                scope.transform.Rotate(0f, 0f, 90f * Time.deltaTime);
                t += Time.deltaTime;
                if (Vector2.Distance(scope.transform.position, target) <= scopeLockDistance) break;
            }
            yield return null;
        }
        if (scope == null) { onDone(); yield break; }

        // 고정 → 깜빡임
        Vector3 spot = scope.transform.position;
        float lt = 0f;
        while (lt < scopeLockDelay)
        {
            if (!IsWorldPaused)
            {
                lt += Time.deltaTime;
                if (sr != null) sr.color = (Mathf.FloorToInt(lt * 16f) % 2 == 0) ? scopeLockColor : scopeColor;
            }
            yield return null;
        }

        // 포탈에서 화살이 내리꽂힘
        yield return FlyVisual(arrowPrefab, portal, spot, scopeArrowSpeed, false);
        Make(impactFx, spot, Quaternion.identity, 2f);
        HitPlayerCircle(spot, scopeStrikeRadius, scopeDamage, 0, 4f, 4f);
        live.Remove(scope); Destroy(scope);
        onDone();
    }

    // ───────────────────────── 패턴 2 — 횡·종 일격 ─────────────────────────
    IEnumerator LinePattern()
    {
        bool horizontal = !forceRain && Random.value < 0.5f;
        forceRain = false;
        if (!horizontal) { yield return RainPattern(); yield break; }   // [2026-11-19] 종 공격 = 화살비
        Vector3 pp = player.position;
        GetRoomBounds(pp, out float left, out float right, out float floor, out float ceil);

        // 횡: 활 높이가 플레이어 몸 가운데에 오도록 (바닥 아래로는 내려가지 않음) / 종: 플레이어 아래 바닥
        float bandY = PlayerCenter().y;
        Vector3 dest;
        if (horizontal) dest = new Vector3(right - edgeMargin, Mathf.Max(floor + 0.05f, bandY - bowOffsetH.y), 0f);
        else dest = new Vector3(pp.x, floor - underDepth, 0f);   // [2026-11-19] 종: 타일맵 밑으로 내려가 위로 쏨
        if (!horizontal) SetUnderground(true);
        yield return TeleportTo(dest);

        if (horizontal) FlipTo(-1); else FacePlayer();
        ShowVisual(horizontal ? idleVisual : skyIdleVisual);
        if (!horizontal && skyIdleSpine != null && skyIdleSpine.AnimationState != null)
            skyIdleSpine.AnimationState.SetAnimation(0, "idle", true);

        // 경고 박스
        Vector3 center; Vector2 size;
        if (horizontal) { center = new Vector3((left + right) * 0.5f, bandY, 0f); size = new Vector2(right - left, lineThickness); }
        else { center = new Vector3(transform.position.x, (floor + ceil) * 0.5f, 0f); size = new Vector2(lineThickness, ceil - floor); }
        WarningZone wz = null;
        if (warningPrefab != null)
        {
            wz = WarningZone.Spawn(warningPrefab, center, size, lineTrackTime + lineLockTime);
            if (wz != null) live.Add(wz.gameObject);
        }

        // [2026-11-19] 공격 준비 — lineTrackTime 동안 플레이어 좌표를 쫓으며 겨눔 (횡: 높이 / 종: 가로 위치)
        float tt = 0f;
        while (tt < lineTrackTime && player != null)
        {
            if (!IsWorldPaused)
            {
                tt += Time.deltaTime;
                Vector3 p = transform.position;
                if (horizontal)
                {
                    float wantY = Mathf.Clamp(PlayerCenter().y, floor + lineThickness * 0.5f, ceil - lineThickness * 0.5f);
                    center.y = Mathf.MoveTowards(center.y, wantY, lineTrackSpeed * Time.deltaTime);
                    p.y = Mathf.Max(floor + 0.05f, center.y - bowOffsetH.y);
                }
                else
                {
                    float wantX = Mathf.Clamp(player.position.x, left + lineThickness * 0.5f, right - lineThickness * 0.5f);
                    center.x = Mathf.MoveTowards(center.x, wantX, lineTrackSpeed * Time.deltaTime);
                    p.x = center.x;
                }
                rigid.position = p; transform.position = p;
                if (wz != null) wz.transform.position = center;
            }
            yield return null;
        }
        yield return PausableWait(lineLockTime);   // 겨눔 고정 → 피할 틈

        // 사격
        var seq = horizontal ? shotH : shotV;
        yield return PlayShotUntilRelease(seq, horizontal ? shotHReleaseFrame : shotVReleaseFrame);
        // 횡: 활에서 왼쪽 벽까지 / 종: 바닥에서 천장까지
        Vector3 from, to;
        if (horizontal) { from = new Vector3(Bow(bowOffsetH).x, center.y, 0f); to = new Vector3(left, center.y, 0f); }
        else { from = new Vector3(center.x, transform.position.y + bowOffsetV.y * 0.6f, 0f); to = new Vector3(center.x, ceil, 0f); }   // 땅속 활에서 시작해 바닥을 뚫고 올라감
        yield return LineStreak(from, to, center, size);
        yield return WaitSeqEnd(seq);
        if (!horizontal)
        {
            // 바닥 위로 올라와 다음 행동
            yield return TeleportTo(new Vector3(transform.position.x, floor + 0.05f, 0f));
            SetUnderground(false);
        }
    }

    // ───────────────────────── 패턴 2-종 — 화살비 ─────────────────────────
    // 맵 가운데로 순간이동 → 하늘로 사격 → 안전 구간 하나만 남기고 경고 → 화살비 (안전 구간 밖이면 피해)
    IEnumerator RainPattern()
    {
        GetRoomBounds(player.position, out float left, out float right, out float floor, out float ceil);
        Vector3 dest = new Vector3((left + right) * 0.5f, floor + (ceil - floor) * rainHoverRatio, 0f);
        yield return TeleportTo(dest);
        FacePlayer();

        // 안전 구간 (벽에서 조금 떨어진 무작위 위치)
        float half = rainSafeWidth * 0.5f;
        float sx = Random.Range(left + half + 0.5f, right - half - 0.5f);
        float safeL = sx - half, safeR = sx + half;

        // 하늘로 사격
        yield return PlayShotUntilRelease(shotV, shotVReleaseFrame);
        for (int i = 0; i < 5; i++)
        {
            Vector3 from = Bow(bowOffsetV);
            Vector3 to = new Vector3(from.x + Random.Range(-2.5f, 2.5f), ceil + 3f, 0f);
            StartCoroutine(FlyVisual(arrowPrefab, from, to, 40f, false));
        }

        // 위험 구간 경고 (안전 구간 양옆)
        float h = ceil - floor, cy = (floor + ceil) * 0.5f;
        Vector3 cL = new Vector3((left + safeL) * 0.5f, cy, 0f), cR = new Vector3((safeR + right) * 0.5f, cy, 0f);
        Vector2 sL = new Vector2(safeL - left, h), sR = new Vector2(right - safeR, h);
        if (warningPrefab != null)
        {
            if (sL.x > 0.2f) { var a = WarningZone.Spawn(warningPrefab, cL, sL, rainWarnTime + rainDuration); if (a != null) live.Add(a.gameObject); }
            if (sR.x > 0.2f) { var b = WarningZone.Spawn(warningPrefab, cR, sR, rainWarnTime + rainDuration); if (b != null) live.Add(b.gameObject); }
        }
        yield return WaitSeqEnd(shotV);
        ShowVisual(idleVisual);
        yield return PausableWait(Mathf.Max(0f, rainWarnTime - 0.3f));

        // 화살비 — 위험 구간 안 무작위 x 에서 천장 위부터 바닥까지 (그림), 판정은 위험 구간 전체
        int n = Mathf.Max(1, rainArrowCount);
        float lenL = Mathf.Max(0f, safeL - left), lenR = Mathf.Max(0f, right - safeR);
        for (int i = 0; i < n; i++)
        {
            float r = Random.Range(0f, lenL + lenR);
            float x = r < lenL ? left + r : safeR + (r - lenL);
            StartCoroutine(RainArrow(x, ceil + 1.5f, floor, Random.Range(0f, rainDuration)));
        }
        bool hit = false; float t = 0f;
        while (t < rainDuration + 0.2f)
        {
            if (!IsWorldPaused)
            {
                t += Time.deltaTime;
                if (!hit && t > 0.12f)
                    hit = (sL.x > 0.2f && HitPlayerBox(cL, sL, rainDamage, 0, 4f, 4f)) || (sR.x > 0.2f && HitPlayerBox(cR, sR, rainDamage, 0, 4f, 4f));
            }
            yield return null;
        }
        yield return PausableWait(0.3f);
    }

    IEnumerator RainArrow(float x, float top, float floor, float delay)
    {
        yield return PausableWait(delay);
        yield return FlyVisual(arrowPrefab, new Vector3(x, top, 0f), new Vector3(x, floor, 0f), rainArrowSpeed, false);
        if (Random.value < 0.35f) Make(impactFx, new Vector3(x, floor, 0f), Quaternion.identity, 2f);
    }

    // 빛줄기가 줄을 따라 지나가며, 머리가 플레이어를 지나칠 때 그 줄 안에 있으면 피해
    IEnumerator LineStreak(Vector3 from, Vector3 to, Vector3 bandCenter, Vector2 bandSize)
    {
        Vector3 dir = (to - from).normalized;
        float len = Vector3.Distance(from, to);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject streak = Make(streakPrefab, from, Quaternion.Euler(0, 0, ang));
        bool hit = false;
        float d = 0f;
        while (d < len)
        {
            if (!IsWorldPaused)
            {
                d = Mathf.Min(len, d + streakSpeed * Time.deltaTime);
                Vector3 head = from + dir * d;
                if (streak != null) streak.transform.position = head;
                if (!hit && player != null)
                {
                    // 머리가 지나간 구간 = 시작점 ~ 머리
                    Vector3 mid = (from + head) * 0.5f;
                    Vector2 sweep = Mathf.Abs(dir.x) > 0.5f ? new Vector2(d, bandSize.y) : new Vector2(bandSize.x, d);
                    hit = HitPlayerBox(mid, sweep, lineDamage, dir.x < -0.5f ? -1 : 1, 6f, 3f);
                }
            }
            yield return null;
        }
        if (streak != null) { live.Remove(streak); Destroy(streak, 0.08f); }
        Make(impactFx, to, Quaternion.identity, 2f);
    }

    // ───────────────────────── 패턴 3 — 도탄 화살 ─────────────────────────
    IEnumerator RicochetPattern()
    {
        FacePlayer();
        yield return PlayShotUntilRelease(shotH, shotHReleaseFrame);

        Vector3 pos = Bow(bowOffsetH);
        GameObject arrow = Make(arrowPrefab, pos, Quaternion.identity);

        // ① 가로로 날아가 벽에 맞음
        Vector2 dir = new Vector2(lockedDir, 0f);
        yield return MoveArrowUntilWall(arrow, dir, ricochetSpeed, 4f);
        if (arrow == null) yield break;
        pos = arrow.transform.position;
        Make(sparkFx, pos, Quaternion.identity, 0.6f);
        ShowVisual(idleVisual);

        // ② 무작위 직선으로 한 번 더 튕김 (궁수 쪽으로, 위아래로 꺾여서)
        float a = Random.Range(-returnAngle, returnAngle) * Mathf.Deg2Rad;
        Vector2 back = new Vector2(-lockedDir * Mathf.Cos(a), Mathf.Sin(a));
        yield return MoveArrowUntilWall(arrow, back, returnSpeed, 0.45f + Random.Range(0f, 0.35f));
        if (arrow == null) yield break;
        Make(sparkFx, arrow.transform.position, Quaternion.identity, 0.6f);

        // ③ 궁수에게 곧장 돌아옴
        yield return MoveArrowTo(arrow, () => transform.position + Vector3.up * bowOffsetH.y, returnSpeed, batDistance);
        if (arrow == null) yield break;

        // ④ 활대로 쳐내 플레이어 쪽으로 — 화살이 날아온 쪽을 보고 휘두름
        ShowParry(arrow.transform.position.x >= transform.position.x ? 1 : -1);
        Make(sparkFx, arrow.transform.position, Quaternion.identity, 0.6f);
        Vector2 toPlayer = player != null ? ((Vector2)(PlayerCenter() - arrow.transform.position)).normalized : new Vector2(lockedDir, 0f);
        Coroutine fly = StartCoroutine(MoveArrowUntilWall(arrow, toPlayer, batSpeed, 4f));
        yield return PausableWait(batPoseTime);
        ShowVisual(idleVisual);
        yield return fly;
        if (arrow != null) { Make(sparkFx, arrow.transform.position, Quaternion.identity, 0.6f); live.Remove(arrow); Destroy(arrow); }
    }

    // 화살을 방향대로 움직이다 벽에 닿으면 멈춤 (maxTime 이 지나도 멈춤). 지나가며 닿으면 피해 (구간마다 한 번)
    IEnumerator MoveArrowUntilWall(GameObject arrow, Vector2 dir, float speed, float maxTime)
    {
        if (arrow == null) yield break;
        dir = dir.normalized;
        SetArrowAngle(arrow, dir);
        bool hit = false; float t = 0f;
        while (arrow != null && t < maxTime)
        {
            if (!IsWorldPaused)
            {
                float step = speed * Time.deltaTime;
                Vector2 p = arrow.transform.position;
                RaycastHit2D h = Physics2D.Raycast(p, dir, step + 0.1f, wallMask);
                if (h.collider != null) { arrow.transform.position = h.point - dir * 0.15f; break; }
                arrow.transform.position = p + dir * step;
                t += Time.deltaTime;
                if (!hit) hit = HitPlayerArrow(arrow.transform.position, dir, ricochetDamage);
            }
            yield return null;
        }
    }

    IEnumerator MoveArrowTo(GameObject arrow, System.Func<Vector3> target, float speed, float stopDist)
    {
        bool hit = false; float t = 0f;
        while (arrow != null && t < 4f)
        {
            if (!IsWorldPaused)
            {
                Vector3 tg = target();
                Vector2 dir = ((Vector2)(tg - arrow.transform.position));
                if (dir.magnitude <= stopDist) break;
                dir.Normalize();
                SetArrowAngle(arrow, dir);
                arrow.transform.position += (Vector3)(dir * speed * Time.deltaTime);
                t += Time.deltaTime;
                if (!hit) hit = HitPlayerArrow(arrow.transform.position, dir, ricochetDamage);
            }
            yield return null;
        }
    }

    static void SetArrowAngle(GameObject arrow, Vector2 dir)
    {
        if (arrow == null) return;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // ───────────────────────── 밀쳐내기 ─────────────────────────
    IEnumerator PushPattern()
    {
        pushTimer = pushCooldown;
        ShowParry(player != null && player.position.x < transform.position.x ? -1 : 1);
        yield return PausableWait(0.12f);
        Vector3 c = transform.position + new Vector3(pushBox.x * 0.5f * lockedDir, pushBox.y * 0.5f, 0f);
        HitPlayerBox(c, pushBox, pushDamage, lockedDir, pushKnockX, pushKnockY);
        Make(sparkFx, c, Quaternion.identity, 0.6f);
        yield return PausableWait(0.35f);
        ShowVisual(idleVisual);
        yield return PausableWait(0.25f);
    }

    // ───────────────────────── 공용 ─────────────────────────
    IEnumerator TeleportTo(Vector3 dest)
    {
        Make(teleportFx, transform.position + Vector3.up * 2f, Quaternion.identity, 1.5f);
        ShowVisual(null);
        yield return PausableWait(teleportHide);
        rigid.velocity = Vector2.zero;
        rigid.position = dest;
        transform.position = dest;
        Make(teleportFx, dest + Vector3.up * 2f, Quaternion.identity, 1.5f);
        ShowVisual(idleVisual);
    }

    // 날아가는 그림만 (피해 없음) — 화살이 포탈로 들어가는 연출 / 포탈에서 내리꽂는 연출
    IEnumerator FlyVisual(GameObject prefab, Vector3 from, Vector3 to, float speed, bool shrinkAtEnd)
    {
        GameObject g = Make(prefab, from, Quaternion.identity);
        if (g == null) yield break;
        Vector2 dir = (to - from).normalized;
        SetArrowAngle(g, dir);
        while (g != null && Vector3.Distance(g.transform.position, to) > 0.05f)
        {
            if (!IsWorldPaused) g.transform.position = Vector3.MoveTowards(g.transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    GameObject Make(GameObject prefab, Vector3 pos, Quaternion rot, float lifetime = 0f)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, pos, rot);
        if (lifetime > 0f) Destroy(g, lifetime);
        else live.Add(g);
        return g;
    }

    // 플레이어가 서 있는 줄을 기준으로 방의 왼쪽·오른쪽 벽, 바닥, 천장
    void GetRoomBounds(Vector3 from, out float left, out float right, out float floor, out float ceil)
    {
        Vector2 o = from + Vector3.up * 0.8f;
        RaycastHit2D h;
        h = Physics2D.Raycast(o, Vector2.left, 60f, wallMask); left = h.collider != null ? h.point.x : o.x - 12f;
        h = Physics2D.Raycast(o, Vector2.right, 60f, wallMask); right = h.collider != null ? h.point.x : o.x + 12f;
        h = Physics2D.Raycast(o, Vector2.down, 60f, wallMask); floor = h.collider != null ? h.point.y : from.y;
        h = Physics2D.Raycast(o, Vector2.up, 60f, wallMask); ceil = h.collider != null ? h.point.y : from.y + 12f;
    }

    Vector3 PlayerCenter()
    {
        if (player == null) return transform.position;
        Collider2D c = player.GetComponent<Collider2D>();
        return c != null ? c.bounds.center : player.position;
    }

    Health PlayerHealth()
    {
        if (player == null) return null;
        if (playerHealth == null || playerHealth.transform != player) playerHealth = player.GetComponent<Health>();
        return playerHealth;
    }

    bool HitPlayerCircle(Vector3 c, float r, float mul, int dir, float kbX, float kbY)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        Vector2 pc = col != null ? (Vector2)col.ClosestPoint(c) : (Vector2)player.position;
        if (Vector2.Distance(pc, c) > r) return false;
        if (dir == 0) dir = player.position.x >= c.x ? 1 : -1;
        return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir, kbX, kbY, pc, null, null, "ArcherMidBoss", false);
    }

    // 화살 그림 길이를 따라 판정 (가운데 점 하나만 보면 긴 화살의 머리·꼬리에 닿아도 안 맞던 문제)
    bool HitPlayerArrow(Vector3 center, Vector2 dir, float mul)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        if (col == null) return HitPlayerCircle(center, ricochetHitRadius, mul, dir.x < 0 ? -1 : 1, 5f, 2f);
        Vector2 n = dir.normalized;
        for (int i = 0; i <= 6; i++)
        {
            Vector2 p = (Vector2)center + n * Mathf.Lerp(-arrowHalfLength, arrowHalfLength, i / 6f);
            Vector2 cp = col.ClosestPoint(p);
            if (Vector2.Distance(cp, p) <= ricochetHitRadius)
                return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, n.x < 0 ? -1 : 1, 5f, 2f, cp, null, null, "ArcherMidBoss", false);
        }
        return false;
    }

    bool HitPlayerBox(Vector3 center, Vector2 size, float mul, int dir, float kbX, float kbY)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        Bounds pb = col != null ? col.bounds : new Bounds(player.position, Vector3.one);
        Bounds box = new Bounds(center, new Vector3(size.x, size.y, 100f));
        pb.center = new Vector3(pb.center.x, pb.center.y, 0f);
        if (!box.Intersects(pb)) return false;
        return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir, kbX, kbY, pb.center, null, null, "ArcherMidBoss", false);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, pushRange);
        Gizmos.color = Color.cyan;
        int d = transform.localScale.x >= 0 ? 1 : -1;
        Gizmos.DrawWireSphere(transform.position + new Vector3(bowOffsetH.x * d, bowOffsetH.y, 0), 0.2f);
        Gizmos.DrawWireSphere(transform.position + new Vector3(bowOffsetV.x * d, bowOffsetV.y, 0), 0.2f);
    }
}
