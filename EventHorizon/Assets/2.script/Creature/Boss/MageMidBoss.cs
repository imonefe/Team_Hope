using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-19 신규] 타격(마법사) 중간보스 — 테스트판 (애니메이션·이펙트 없음, 색 박스 + 머리 위 글자).
///
/// 상시 — 속성 방어막: 참격 → 관통 → 타격 순서로 일정 시간마다 바뀐다. 지금 방어막과 같은 속성 무기로 때리면 피해 0.
/// 패턴 1 유도 구체: 머리 위에 구체를 만든 뒤 플레이어에게 유도로 던짐.
/// 패턴 2 레이저:   안전 구간 하나만 남기고 나머지 전체를 경고 → 하늘에서 레이저가 떨어져 태움 (궁수 화살비와 같은 규칙).
/// 패턴 3 퍼즈 기믹 (체력 50% 이하에서 1회): 차징 + 바닥에 시간 오브젝트 생성.
///         오브젝트를 부수면 플레이어 퍼즈 게이지가 차고, 차징 중 퍼즈를 쓰면 파훼 + 스턴.
///         못 막으면 시간 정지(노란빛) — 플레이어가 멈춘 동안 구체 5개를 만들고, 풀리는 순간 던짐.
/// </summary>
public class MageMidBoss : MidBossTestBase
{
    [Header("[ 속성 방어막 ]")]
    public NowAdvantage[] shieldOrder = { NowAdvantage.Slash, NowAdvantage.Pierce, NowAdvantage.Blunt };
    public float shieldDuration = 6f;
    [Tooltip("방어막 그림 (보스를 감싸는 원)")]
    public SpriteRenderer shieldRenderer;
    public Color slashShieldColor = new Color(0.3f, 0.9f, 0.85f, 0.35f);
    public Color pierceShieldColor = new Color(0.45f, 0.95f, 0.35f, 0.35f);
    public Color bluntShieldColor = new Color(1f, 0.55f, 0.2f, 0.35f);

    [Header("[ 패턴 1 — 유도 구체 ]")]
    public Vector2 orbSpawnOffset = new Vector2(0f, 4f);
    public float orbGrowTime = 0.8f;
    public float orbSize = 0.9f;
    public float orbSpeed = 6.5f;
    [Tooltip("초당 꺾이는 최대 각도")]
    public float orbTurnRate = 120f;
    public float orbLife = 5f;
    public float orbDamage = 1f;
    public Color orbColor = new Color(0.75f, 0.45f, 1f, 1f);

    [Header("[ 패턴 2 — 레이저 ]")]
    public float laserSafeWidth = 3f;
    public float laserWarnTime = 1.6f;
    public float laserBurnTime = 0.8f;
    public float laserBeamWidth = 1.2f;
    [Tooltip("레이저 기둥 간격 (그림만, 판정은 위험 구간 전체)")]
    public float laserBeamGap = 1.6f;
    public float laserDamage = 1.3f;
    public Color laserColor = new Color(1f, 0.6f, 0.15f, 0.9f);

    [Header("[ 패턴 3 — 퍼즈 기믹 ]")]
    [Range(0f, 1f)] public float pauseGimmickHpRatio = 0.5f;
    public float gimmickChargeTime = 9f;
    [Tooltip("시간 오브젝트 (FX_MageTimeAnchor — 적 레이어, 체력 있음)")]
    public GameObject anchorPrefab;
    public int anchorCount = 3;
    [Tooltip("파훼 후 스턴 시간 (플레이어 퍼즈가 끝난 뒤부터)")]
    public float stunTime = 4f;
    public float timeStopTime = 2.2f;
    public int timeStopOrbs = 5;
    public float timeStopOrbDamage = 0.7f;
    [Tooltip("시간 정지 화면 색")]
    public Color timeStopTint = new Color(1f, 0.82f, 0.2f, 0.22f);

    const int P_ORB = 0, P_LASER = 1, P_GIMMICK = 2;

    int shieldIndex;
    float shieldTimer;
    bool shieldOn;
    float shieldFlash;
    bool gimmickUsed;
    // 시간 정지로 묶어 둔 플레이어 (죽거나 사라질 때 풀어 주기 위해 기억)
    PlayerController frozenPc;
    Rigidbody2D frozenRb;
    RigidbodyConstraints2D frozenOldCons;

    protected override void Awake()
    {
        base.Awake();
        if (shieldRenderer != null) shieldRenderer.enabled = false;
        health.invulnerable = () => shieldOn && PlayerAdvantage() == CurrentShield;
    }

    NowAdvantage CurrentShield => shieldOrder != null && shieldOrder.Length > 0 ? shieldOrder[shieldIndex % shieldOrder.Length] : NowAdvantage.None;

    NowAdvantage PlayerAdvantage()
    {
        if (player == null) return NowAdvantage.None;
        Stats s = player.GetComponent<Stats>();
        return s != null ? s.advantage : NowAdvantage.None;
    }

    static string AdvName(NowAdvantage a) =>
        a == NowAdvantage.Slash ? "참격" : a == NowAdvantage.Pierce ? "관통" : a == NowAdvantage.Blunt ? "타격" : "없음";

    Color ShieldColor(NowAdvantage a) =>
        a == NowAdvantage.Slash ? slashShieldColor : a == NowAdvantage.Pierce ? pierceShieldColor : bluntShieldColor;

    // ───────────────────────── 방어막 순환 ─────────────────────────
    protected override void Update()
    {
        base.Update();
        bool fighting = state != BossState.Inactive && !IsDefeated;
        if (fighting && !shieldOn) { shieldOn = true; shieldTimer = shieldDuration; }
        if (!fighting) shieldOn = false;

        if (shieldOn && !IsWorldPaused)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f) { shieldIndex++; shieldTimer = shieldDuration; }
        }
        if (shieldFlash > 0f) shieldFlash -= Time.deltaTime;

        if (shieldRenderer != null)
        {
            shieldRenderer.enabled = shieldOn;
            Color c = ShieldColor(CurrentShield);
            if (shieldFlash > 0f) c.a = Mathf.Min(1f, c.a + 0.5f);   // 막았을 때 번쩍
            shieldRenderer.color = c;
        }
    }

    public override void OnHit(int dir)
    {
        if (health.IsInvulnerable) shieldFlash = 0.15f;
        base.OnHit(dir);
    }

    protected override void OnTrueDeath()
    {
        ReleasePlayer();
        shieldOn = false;
        if (shieldRenderer != null) shieldRenderer.enabled = false;
        base.OnTrueDeath();
    }

    // 방어막 속성을 글자 뒤에 붙임
    void Say(string text, Color c) => SetLabel($"{text}\n<size=70%>[{AdvName(CurrentShield)} 방어막]</size>", c);

    protected override void OnCooldownStart()
    {
        base.OnCooldownStart();
        Say("", Color.white);
    }

    // ───────────────────────── 패턴 선택 ─────────────────────────
    protected override int GetPatternCount() => 2;   // 일반 패턴은 구체·레이저 (퍼즈 기믹은 체력 조건으로만)

    protected override int SelectPattern()
    {
        if (!gimmickUsed && stats != null && stats.maxHp > 0 && stats.currentHp <= stats.maxHp * pauseGimmickHpRatio)
        {
            gimmickUsed = true;
            return P_GIMMICK;
        }
        return base.SelectPattern();
    }

    protected override IEnumerator ExecutePattern(int index)
    {
        if (player == null) yield break;
        state = BossState.Attacking;
        rigid.velocity = Vector2.zero;
        switch (index)
        {
            case P_ORB: yield return OrbPattern(); break;
            case P_LASER: yield return LaserPattern(); break;
            default: yield return PauseGimmickPattern(); break;
        }
        ResetBody();
        Say("", Color.white);
    }

    // ───────────────────────── 패턴 1 — 유도 구체 ─────────────────────────
    IEnumerator OrbPattern()
    {
        FacePlayer();
        Say("유도 구체", orbColor);
        GameObject orb = SpawnGrowingOrb(transform.position + (Vector3)orbSpawnOffset);
        yield return GrowOrb(orb, orbGrowTime);
        Vector2 dir = (Vector2)(PlayerCenter() - orb.transform.position);
        StartCoroutine(MoveProjectile(orb, dir, orbSpeed, orbTurnRate, orbLife, orbSize * 0.5f, orbDamage, "Mage_Orb", false));
        yield return PausableWait(0.5f);
    }

    GameObject SpawnGrowingOrb(Vector3 pos)
    {
        GameObject orb = Shape(circlePrefab, pos, Vector2.one * 0.05f, 0f, orbColor, 4110, -1f, 0f);
        return orb;
    }

    IEnumerator GrowOrb(GameObject orb, float time)
    {
        float t = 0f;
        while (orb != null && t < time)
        {
            if (!IsWorldPaused) t += Time.deltaTime;
            float s = Mathf.Lerp(0.05f, orbSize, t / Mathf.Max(0.01f, time));
            orb.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ───────────────────────── 패턴 2 — 레이저 ─────────────────────────
    IEnumerator LaserPattern()
    {
        FacePlayer();
        Say("레이저", laserColor);
        GetRoomBounds(out float left, out float right, out float floor, out float ceil);
        float half = laserSafeWidth * 0.5f;
        float sx = Random.Range(left + half + 0.5f, right - half - 0.5f);
        float safeL = sx - half, safeR = sx + half;
        float h = ceil - floor, cy = (floor + ceil) * 0.5f;
        Vector3 cL = new Vector3((left + safeL) * 0.5f, cy, 0f), cR = new Vector3((safeR + right) * 0.5f, cy, 0f);
        Vector2 sL = new Vector2(safeL - left, h), sR = new Vector2(right - safeR, h);
        if (sL.x > 0.2f) WarnBox(cL, sL, 0f, laserWarnTime);
        if (sR.x > 0.2f) WarnBox(cR, sR, 0f, laserWarnTime);
        yield return PausableWait(laserWarnTime);

        // 하늘에서 떨어지는 레이저 기둥 (그림) — 판정은 위험 구간 전체
        Say("레이저!", Color.white);
        var xs = new List<float>();
        for (float x = left + laserBeamGap * 0.5f; x < right; x += laserBeamGap)
            if (x < safeL - laserBeamWidth * 0.3f || x > safeR + laserBeamWidth * 0.3f) xs.Add(x);
        foreach (float x in xs)
            Shape(shapePrefab, new Vector3(x, cy, 0f), new Vector2(laserBeamWidth, h), 0f, laserColor, 4120, laserBurnTime, 0.25f);

        bool hit = false; float t = 0f;
        while (t < laserBurnTime)
        {
            if (!IsWorldPaused)
            {
                t += Time.deltaTime;
                if (!hit && ((sL.x > 0.2f && PlayerInBox(cL, sL)) || (sR.x > 0.2f && PlayerInBox(cR, sR))))
                    hit = Hit(laserDamage, 0, 4f, 4f, PlayerCenter(), "Mage_Laser");
            }
            yield return null;
        }
        yield return PausableWait(0.4f);
    }

    // ───────────────────────── 패턴 3 — 퍼즈 기믹 ─────────────────────────
    IEnumerator PauseGimmickPattern()
    {
        FacePlayer();
        SetBodyColor(new Color(1f, 0.85f, 0.3f, 1f));
        GetRoomBounds(out float left, out float right, out float floor, out float ceil);

        // 바닥에 시간 오브젝트 — 부술 때마다 퍼즈 게이지
        var anchors = new List<GameObject>();
        for (int i = 0; i < anchorCount && anchorPrefab != null; i++)
        {
            float x = Mathf.Lerp(left + 2f, right - 2f, anchorCount == 1 ? 0.5f : i / (float)(anchorCount - 1)) + Random.Range(-1f, 1f);
            GameObject a = Instantiate(anchorPrefab, new Vector3(x, floor, 0f), Quaternion.identity);
            anchors.Add(a); live.Add(a);
        }
        float gaugePer = 100f / Mathf.Max(1, anchorCount) + 0.5f;
        PauseManager pm = PauseManager.Instance;

        bool countered = false;
        float t = 0f;
        while (t < gimmickChargeTime)
        {
            if (pm != null && pm.IsPaused) { countered = true; break; }   // 플레이어 퍼즈 → 파훼
            t += Time.deltaTime;
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i] != null) continue;
                anchors.RemoveAt(i);
                if (pm != null) pm.AddGauge(gaugePer);                       // 부서진 오브젝트 → 게이지
            }
            float left01 = Mathf.Max(0f, gimmickChargeTime - t);
            Say($"시간 정지 차징 {left01:0.0}\n<size=70%>오브젝트를 부숴 퍼즈를 써라</size>", new Color(1f, 0.85f, 0.3f, 1f));
            yield return null;
        }
        foreach (var a in anchors) if (a != null) { live.Remove(a); Destroy(a); }

        if (countered)
        {
            // 파훼 → 퍼즈가 끝난 뒤 스턴
            Say("파훼! 스턴", Color.white);
            SetBodyColor(new Color(0.45f, 0.45f, 0.45f, 1f));
            while (pm != null && pm.IsPaused) yield return null;
            yield return PausableWait(stunTime);
            yield break;
        }

        yield return TimeStop();
    }

    // 막지 못함 → 시간 정지 (노란빛) — 플레이어가 멈춘 동안 구체 5개를 만들고, 풀리는 순간 던진다
    IEnumerator TimeStop()
    {
        Say("시간 정지", new Color(1f, 0.85f, 0.3f, 1f));
        GetRoomBounds(out float left, out float right, out float floor, out float ceil);
        Vector3 roomC = new Vector3((left + right) * 0.5f, (floor + ceil) * 0.5f, 0f);
        GameObject tint = Shape(shapePrefab, roomC, new Vector2(right - left + 40f, ceil - floor + 30f), 0f, timeStopTint, 4900, -1f, 0f);

        FreezePlayer();

        var orbs = new List<GameObject>();
        float gap = timeStopTime / Mathf.Max(1, timeStopOrbs + 1);
        for (int i = 0; i < timeStopOrbs; i++)
        {
            float ang = 90f + (i - (timeStopOrbs - 1) * 0.5f) * 28f;
            Vector3 p = transform.position + new Vector3(0f, 2f, 0f) + Quaternion.Euler(0f, 0f, ang) * Vector3.right * 2.6f;
            GameObject orb = SpawnGrowingOrb(p);
            StartCoroutine(GrowOrb(orb, gap * 0.9f));
            orbs.Add(orb);
            yield return new WaitForSeconds(gap);   // 시간 정지 중엔 퍼즈와 상관없이 진행
        }
        yield return new WaitForSeconds(gap);

        ReleasePlayer();
        if (tint != null) StartCoroutine(FadeTint(tint, 0.3f));

        Say("", Color.white);
        foreach (var orb in orbs)
        {
            if (orb == null) continue;
            Vector2 dir = (Vector2)(PlayerCenter() - orb.transform.position);
            StartCoroutine(MoveProjectile(orb, dir, orbSpeed + 1.5f, orbTurnRate * 0.6f, orbLife, orbSize * 0.5f, timeStopOrbDamage, "Mage_TimeStopOrb", false));
            yield return PausableWait(0.12f);
        }
        yield return PausableWait(0.8f);
    }

    void FreezePlayer()
    {
        if (player == null) return;
        frozenPc = player.GetComponent<PlayerController>();
        frozenRb = player.GetComponent<Rigidbody2D>();
        if (frozenPc != null) frozenPc.LockControl(this);
        if (frozenRb != null)
        {
            frozenOldCons = frozenRb.constraints;
            frozenRb.velocity = Vector2.zero;
            frozenRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void ReleasePlayer()
    {
        if (frozenRb != null) frozenRb.constraints = frozenOldCons;
        if (frozenPc != null) frozenPc.ResumeControl(this);
        frozenRb = null; frozenPc = null;
    }

    IEnumerator FadeTint(GameObject g, float time)
    {
        SpriteRenderer sr = g.GetComponentInChildren<SpriteRenderer>();
        Color c0 = sr != null ? sr.color : Color.white;
        float t = 0f;
        while (g != null && t < time)
        {
            t += Time.deltaTime;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - t / time));
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    // 죽거나 사라질 때 플레이어 정지가 남지 않게
    protected override void OnDestroy()
    {
        base.OnDestroy();
        ReleasePlayer();
    }
}
