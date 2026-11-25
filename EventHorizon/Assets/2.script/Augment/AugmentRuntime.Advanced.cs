using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가 · 수정] 증강 고유능력 — 희귀 22 · 영웅 11 · 전설 6.
/// 바탕(보유 읽기 · 피해 끼어들기 · 사건 연결)은 AugmentRuntime.cs. 연출은 AugmentFx 부품만 쓴다 (글자 안내 없음).
/// 단순화한 곳은 [단순화].
///
/// '내 공격을 따라 하는 잔상'(고장난 비서 · 현상 재생 · 92퍼센트)은 실제 평타 판정을 복사해서 그 자리에 다시 낸다
///  (판정 · 피해 · 상성 · 치명타 규칙이 평타와 똑같음, 피해만 배율).
/// </summary>
public partial class AugmentRuntime
{
    // ── 상태 ──
    int recall;                                  // 떠올림 (92퍼센트, 최대 12)
    int savedCombo; float backupReadyAt;         // 스택 백업
    bool bladeCharged; float bladePulse;         // 박자 칼날
    bool meleeBoost, rangedSlash;                // 빼앗은 기술
    bool dashFirstHitArmed; float dashFirstHitUntil;   // 접속 단자
    float reflectUntil; readonly HashSet<Health> reflected = new HashSet<Health>();   // 파편 반사
    float slowAdded;                             // 느린 정리 (퍼즈당 최대 1.5초)
    float queuedGauge;                           // 특이점 조항: 퍼즈 끝나고 넣을 게이지
    int restDeferred; float restStill; Vector3 restLastPos; FxCircle restRing;   // 잠깐의 휴식
    float barrierTick; FxCircle barrierRing;     // 치유 결계
    float replayTimer;                           // 현상 재생
    float glintTimer, fireTimer;                 // 전투 시야 · 히어로 착지 연출
    int rhythmCount;                             // 강철 리듬
    float secretaryReady;                        // 고장난 비서
    Mimic secretary;
    bool observeArmed;                           // 원거리 관측
    float predictUntil; readonly HashSet<Enemy> predicted = new HashSet<Enemy>();
    readonly HashSet<Enemy> countered = new HashSet<Enemy>();
    readonly List<GameObject> stasisOrbs = new List<GameObject>();   // 정지 잔상
    readonly List<CapturedShot> captured = new List<CapturedShot>(); // 역류 탄도
    readonly Dictionary<ProjectileBase, FxCircle> shotRings = new Dictionary<ProjectileBase, FxCircle>();
    readonly HashSet<EnemyStatus> horizonHits = new HashSet<EnemyStatus>();   // 사건의 지평선
    readonly Dictionary<Health, GameObject> wedgeMarks = new Dictionary<Health, GameObject>();
    readonly Dictionary<Health, FxEdgeArrow> execArrows = new Dictionary<Health, FxEdgeArrow>();
    GameObject weakMark; EnemyStatus weakTarget;
    float markScan, unknownReady;
    float lensUntil; readonly HashSet<Health> lensHit = new HashSet<Health>();   // 궤적 예측
    bool collectorBoost;                         // 수집가의 불꽃: 데이터를 쓰면 다음 과열 폭발 반경 증가

    // 퍼즈 기록 (92퍼센트) · 따라 하는 잔상들
    struct Pose { public Vector3 pos; public Sprite sprite; public Vector3 scale; public bool flip; }
    readonly List<Pose> pausePoses = new List<Pose>();
    float poseNext;
    readonly List<Mimic> echoes = new List<Mimic>();

    // 현상 재생: 마지막 공격 (판정 원본 + 휘두르는 동안의 그림)
    GameObject replayTemplate; Vector3 replayOffset; int replayFacing; float replayAt;
    readonly List<KeyValuePair<float, Sprite>> replayFrames = new List<KeyValuePair<float, Sprite>>();
    Pose replayPose; float recordUntil, recordStart;

    class CapturedShot { public GameObject go; public Transform shooter; }

    /// <summary>제자리에 서서 내 행동을 따라 하는 잔상 (고장난 비서 · 92퍼센트)</summary>
    class Mimic
    {
        public FxGhost ghost;
        public Vector3 pos;       // 잔상 그림 위치 (헤네시 그림과 같은 기준)
        public float until, ratio;
        public bool wrongWay;     // 고장난 비서: 가끔 반대쪽
        public Color color;
    }

    void ResetAdvancedState()
    {
        recall = 0; savedCombo = 0; backupReadyAt = 0; bladeCharged = false; collectorBoost = false;
        meleeBoost = rangedSlash = false; dashFirstHitArmed = false; socketUsed = false;
        reflectUntil = 0; slowAdded = 0; queuedGauge = 0; restDeferred = 0; restStill = 0; replayTimer = 0; rhythmCount = 0;
        observeArmed = false; predictUntil = 0; predicted.Clear(); countered.Clear();
        foreach (var o in stasisOrbs) if (o != null) Destroy(o);
        stasisOrbs.Clear(); captured.Clear(); horizonHits.Clear();
        foreach (var kv in wedgeMarks) if (kv.Value != null) Destroy(kv.Value);
        wedgeMarks.Clear();
        foreach (var kv in execArrows) if (kv.Value != null) Destroy(kv.Value.gameObject);
        execArrows.Clear();
        foreach (var kv in shotRings) if (kv.Value != null) Destroy(kv.Value.gameObject);
        shotRings.Clear();
        if (weakMark != null) Destroy(weakMark);
        weakTarget = null;
        if (restRing != null) Destroy(restRing.gameObject);
        if (barrierRing != null) Destroy(barrierRing.gameObject);
        foreach (var m in echoes) if (m.ghost != null) Destroy(m.ghost.gameObject);
        echoes.Clear();
        if (secretary != null && secretary.ghost != null) Destroy(secretary.ghost.gameObject);
        secretary = null;
        if (replayTemplate != null) Destroy(replayTemplate);
        pausePoses.Clear();
    }

    void RefreshAdvancedModifiers()
    {
        int lvSupply = L("보급 우선권");
        CombatEvents.ShardDropMultiplier = lvSupply > 0 ? ByLevel(lvSupply, 2f, 2.5f, 3f) : 1f;
    }

    /// <summary>체력바 오른쪽 아이콘 줄에 이 증강 아이콘 + 남은 초 (시간이 정해진 강화 · 재사용 대기)</summary>
    void BuffIcon(string ability, float seconds, bool cooldown = false)
    {
        if (!byAbility.TryGetValue(ability, out var d) || d == null) return;
        AugmentBuffTray.Show(ability, d.icon, seconds, cooldown);
    }

    float ScaleThresholdBonus()
    {
        int lv = L("처형인의 저울");
        if (lv <= 0 || pstats == null || pstats.maxHp <= 0) return 0f;
        float hp = Mathf.Clamp01((float)pstats.currentHp / pstats.maxHp);
        return (1f - hp) * ByLevel(lv, 0.15f, 0.20f, 0.25f);
    }

    /// <summary>확장 소켓: 데이터가 충분히 모였는지 (Lv2 1개 덜, Lv3 2개 덜) — 모으기 한 번에 한 번
    /// [2026-11-23] B안: 데이터를 쓰지 않음. 방출이 끝나 다시 모으기 시작하면 다시 쓸 수 있음</summary>
    bool SocketReady()
    {
        int lv = L("확장 소켓");
        if (lv <= 0 || socketUsed) return false;
        int need = Mathf.Max(1, FragmentMax - (lv - 1));
        return releasing || fragments >= need;
    }
    bool socketUsed;

    /// <summary>[2026-11-23] 방출이 끝나 데이터가 0개가 됨 → 확장 소켓 다시 준비</summary>
    void OnReleaseEnd() { socketUsed = false; }

    bool GauntletRhythm() => Has("강철 리듬") && pc != null && pc.weapontype == Weapontype.Glove;

    public int OverheatGainFor(EnemyStatus st, int n) => Has("붕괴 탄두") && st != null && st.IsMaxFracture ? n * 2 : n;

    float LastLineThreshold => ByLevel(L("마지막 한 줄"), 0.15f, 0.18f, 0.20f);

    public bool InstantKillReady(Health h)
    {
        if (!Has("마지막 한 줄") || h == null || IsBoss(h)) return false;
        var st = h.GetComponent<Stats>();
        return st != null && st.maxHp > 0 && st.currentHp > 0 && st.currentHp <= st.maxHp * LastLineThreshold;
    }

    float ExplosionRadius(float r)
    {
        int lvSerum = L("붕괴 탄두");
        if (lvSerum > 0) r += ByLevel(lvSerum, 0f, 0.5f, 1f);
        int lvCol = L("수집가의 불꽃");
        if (lvCol > 0 && collectorBoost) r *= ByLevel(lvCol, 2f, 2.5f, 3f);
        return r;
    }

    void OnOverheatExplosion(Vector3 pos) { collectorBoost = false; }

    // ══════════════ 피해 끼어들기 ══════════════

    float AdvancedOutgoing(Health target, bool crit, bool exec)
    {
        float m = 0f;
        if (recall > 0) m += recall * 0.02f;                                         // 92퍼센트: 떠올림당 공격력 +2%
        if (Has("마지막 한 줄") && IsBoss(target))
        {
            var st = target.GetComponent<Stats>();
            if (st != null && st.maxHp > 0 && st.currentHp <= st.maxHp * LastLineThreshold) m += 0.15f;   // [2026-11-21] 보스는 대신 피해 +15%
        }
        return m;
    }

    int AdvancedIncoming(int dmg)
    {
        // 반응 코팅: 받는 피해 감소 + 때린 적에게 균열 2 [단순화: 때린 적 = 가장 가까운 적]
        int lvCoat = L("반응 코팅");
        if (lvCoat > 0 && dmg > 0)
        {
            dmg = Mathf.RoundToInt(dmg * (1f - ByLevel(lvCoat, 0.15f, 0.20f, 0.25f)));
            if (pc != null)
            {
                AugmentFx.BodyFlash(pc, CFracture, 0.8f, 0.25f);
                var atk = Nearest(PPos, 3.5f);
                if (atk != null) { StatusOf(atk).AddFracture(2); AugmentFx.Sparks(EC(atk), CFracture, 6, 3f, 0.3f); }
            }
        }
        return dmg;
    }

    int EnemyIncoming(Health h, int dmg)
    {
        // 사건의 지평선: 퍼즈 중 입힌 피해는 저장 (체력은 줄지 않음 → 1 밑으로 안 내려감)
        if (Paused && Has("사건의 지평선") && dmg > 0)
        {
            var st = StatusOf(h);
            st.horizonStored += dmg;
            horizonHits.Add(st);
            return 0;
        }
        return dmg;
    }

    // ══════════════ 스킬 ══════════════

    bool FreeCastAvailable() => SocketReady();

    float OnSkillCast(int slot, bool free)
    {
        RefreshPlayer();
        float scale = 1f;
        // 확장 소켓: 데이터가 다 모이면 → 데이터를 모두 써서 이번 스킬은 재사용 대기 없음
        if (free && SocketReady())
        {
            socketUsed = true;   // [2026-11-23] 데이터는 쓰지 않음
            if (pc != null) { AugmentFx.BodyFlash(pc, CPlunder, 0.8f, 0.3f); AugmentFx.Ring(PCenter, CPlunder, 1.6f, 0.3f, 0.08f, 2); }
        }
        // 혈류 대체 회로: 최대 체력 5% 소모 → 스킬 피해 증가 (체력 10% 이하면 소모·보너스 없음)
        int lvBlood = L("혈류 대체 회로");
        if (lvBlood > 0 && pstats != null && pstats.maxHp > 0 && pstats.currentHp > pstats.maxHp * 0.1f)
        {
            int cost = Mathf.Min(Mathf.RoundToInt(pstats.maxHp * 0.05f), pstats.currentHp - 1);
            if (cost > 0)
            {
                pstats.TakeDamage(cost);
                lostThisRoom += cost;
                scale += ByLevel(lvBlood, 0.35f, 0.52f, 0.70f);
                AugmentFx.BodyFlash(pc, COverload, 0.85f, 0.35f);
                AugmentFx.Sparks(PCenter, COverload, 10, 2.5f, 0.6f, 6f, 0.16f);   // 분홍 조각이 떨어져 나감
                QueueNano(cost);
            }
        }
        // 백자 열람 권한: 데이터 1개를 써서 이번 스킬 피해 증가
        int lvRead = L("백자 열람 권한");
        if (lvRead > 0 && fragments > 0 && UseFragment())
        {
            scale += ByLevel(lvRead, 1.0f, 1.5f, 2.0f);
            if (pc != null) { AugmentFx.Ring(PCenter, Color.white, 2.5f, 0.4f, 0.1f); AugmentFx.Ring(PCenter, CFracture, 2.3f, 0.45f, 0.07f, 2); }
        }
        return scale;
    }

    // 나노 봉합: 잃은 체력의 일부가 3초 뒤 돌아옴
    float nanoReady;
    void QueueNano(int lost)
    {
        int lv = L("나노 봉합");
        if (lv <= 0 || lost <= 0) return;
        if (Time.time < nanoReady) return;   // [2026-11-26] 재사용 대기 4초 (예전엔 맞을 때마다 → 상시 피해 감소처럼 됨)
        nanoReady = Time.time + 4f;
        StartCoroutine(Nano(Mathf.Max(1, Mathf.RoundToInt(lost * ByLevel(lv, 0.15f, 0.20f, 0.25f)))));   // [2026-11-26] 20/30/40% → 15/20/25%
        BuffIcon("나노 봉합", 3f);
    }

    IEnumerator Nano(int amount)
    {
        // 분홍 조각이 헤네시 주위를 3초 돌다가 몸으로 흡수
        var dots = new List<GameObject>();
        for (int i = 0; i < 3; i++) dots.Add(AugmentFx.Orb(PCenter, COverload, 0.18f));
        float t = 0f;
        while (t < 3f)
        {
            for (int i = 0; i < dots.Count; i++)
                if (dots[i] != null)
                {
                    float a = t * 4f + i * Mathf.PI * 2f / 3f;
                    dots[i].transform.position = PCenter + new Vector3(Mathf.Cos(a) * 0.9f, Mathf.Sin(a) * 0.5f, -0.5f);
                }
            t += Time.deltaTime;
            yield return null;
        }
        foreach (var d in dots) if (d != null) Destroy(d);
        HealPlayer(amount, COverload);
    }

    // ══════════════ 대시 ══════════════

    float DashSpeed(Vector3 pos, float dir)
    {
        // 원거리 관측: 처형 대상을 향한 대시는 속도 증가 + 붉은 대시 잔상
        if (!Has("원거리 관측")) return 1f;
        foreach (var e in EnemiesIn(pos, 14f))
        {
            float dx = e.transform.position.x - pos.x;
            if (Mathf.Sign(dx) == Mathf.Sign(dir) && Mathf.Abs(e.transform.position.y - pos.y) < 3f && IsExecuteTarget(e))
            {
                observeArmed = true;
                AugmentFx.GhostTrail(psprite, CExec, 0.25f, 0.02f, 0.18f, 0.75f);
                return 1.8f;
            }
        }
        return 1f;
    }

    void AdvancedOnDashStart(Vector3 pos, float dir)
    {
        // 정지 잔상: 퍼즈 중 대시하면 에너지 구체가 멈춰 남음
        if (Paused && Has("정지 잔상") && stasisOrbs.Count < 8)
            stasisOrbs.Add(AugmentFx.Orb(pos + (PCenter - PPos), CStasis, 0.45f));

        // 궤적 예측: 퍼즈 중 대시로 지나친 적에게 피해 (대시마다 새로)
        if (Paused && Has("궤적 예측")) { lensUntil = Time.time + 0.32f; lensHit.Clear(); AugmentFx.GhostTrail(psprite, CStasis, 0.25f, 0.025f, 0.2f, 0.7f); }

        // 파편 반사: 대시로 통과한 적에게 균열 + 파편 (보라 막)
        if (Has("파편 반사"))
        {
            reflectUntil = Time.time + 0.32f;
            reflected.Clear();
            AugmentFx.BodyFlash(pc, CFracture, 0.6f, 0.3f);
        }

        // 고장난 비서: 대시를 시작한 자리에 홀로그램 잔상이 4초 서서 내 공격을 따라 함 (6초에 한 번)
        if (SecretaryWillSpawn())
        {
            secretaryReady = Time.time + 6f;
            if (secretary != null && secretary.ghost != null) secretary.ghost.Release();
            // [2026-11-22] 다른 잔상(푸른빛)과 구분되는 하늘색 홀로그램
            secretary = MakeMimic(psprite.transform.position, 4f, ByLevel(L("고장난 비서"), 0.30f, 0.45f, 0.60f), CSecretary, true);
            BuffIcon("고장난 비서", 4f);
        }
    }

    void AdvancedOnDashEnd(Vector3 pos)
    {
        if (observeArmed)
        {
            observeArmed = false;
            nextAttackBonus = Mathf.Max(nextAttackBonus, ByLevel(L("원거리 관측"), 0.40f, 0.60f, 0.80f));
            if (pc != null) AugmentFx.BodyFlash(pc, CExec, 0.7f, 0.3f);
        }
        if (Has("접속 단자")) { dashFirstHitArmed = true; dashFirstHitUntil = Time.time + 1.5f; }
    }

    static readonly Color CSecretary = new Color(0.72f, 0.74f, 0.77f, 1f);   // [2026-11-23] 잔상 계열 무채색 (기본 잔상보다 조금 어두운 회색)
    /// <summary>[2026-11-22] 이번 대시에 고장난 비서 홀로그램이 생기는지 (잔상 그림 겹침 방지에도 씀)</summary>
    bool SecretaryWillSpawn() => Has("고장난 비서") && Time.time >= secretaryReady && psprite != null;

    Mimic MakeMimic(Vector3 spritePos, float life, float ratio, Color c, bool wrongWay)
    {
        var g = AugmentFx.Ghost(psprite, c, life, 0.6f, false, spritePos);
        if (g == null) return null;
        g.hold = true;
        return new Mimic { ghost = g, pos = spritePos, until = Time.time + life, ratio = ratio, wrongWay = wrongWay, color = c };
    }

    // ══════════════ 평타 ══════════════

    void OnSwing(SwingTag tag)
    {
        RefreshPlayer();
        var tr = tag.transform;
        int f = tag.facing;

        // 조준 고정: 평타 판정을 키우고, 커지기 전 · 후 범위를 알려 줌 → 무기 궤적 이펙트가 판정이 늘어난 만큼만 같이 늘어남
        // [2026-11-24] 예전의 노란 ㄷ자 테두리(판정 상자를 그대로 그린 것)는 없앰
        if (AimScale > 1.01f)
        {
            var box = tag.GetComponent<BoxCollider2D>();
            Rect before = box != null ? WorldRect(box) : default;
            tr.localScale = Vector3.Scale(tr.localScale, new Vector3(AimScale, AimScale, 1f));
            if (box != null) CombatEvents.SetSwingFit(before, WorldRect(box), f);
        }

        // 과충전 망치: 최대 차지 강타 공격력 2배 (체력 50% 이하면 분홍)
        if (Has("과충전 망치") && tag.weapon == Weapontype.Hammer && tag.charged && tag.chargePower >= 0.95f)
        {
            var hb = tag.GetComponent<Hitbox>();
            if (hb != null) hb.damage *= 2;
            AugmentFx.Ring(PFeet, HpRatioP() <= 0.5f ? COverload : new Color(1f, 0.85f, 0.5f), 2.5f, 0.35f, 0.12f, 2, 0.4f);
        }
        // 빼앗은 기술: 근접 적의 기술 = 다음 공격 범위 2배 / 원거리 적의 기술 = 다음 공격에 정면 참격
        if (meleeBoost)
        {
            meleeBoost = false;
            tr.localScale = new Vector3(tr.localScale.x * 2f, tr.localScale.y * 2f, tr.localScale.z);
            AugmentFx.Ring(tr.position, CPlunder, 2f, 0.3f, 0.08f);
        }
        if (rangedSlash)
        {
            rangedSlash = false;
            StartCoroutine(PierceSlash(PCenter, f, 6f, ByLevel(L("빼앗은 기술"), 0.5f, 0.75f, 1.0f), new Color(0.3f, 0.95f, 0.95f), 1.4f));
        }
        // 박자 칼날: 연격 10에서 충전된 다음 공격 = 관통 빛줄기 (거리 4m)
        if (bladeCharged && Has("박자 칼날"))
        {
            bladeCharged = false;
            StartCoroutine(PierceSlash(PCenter, f, 4f, ByLevel(L("박자 칼날"), 0.75f, 1.12f, 1.5f), new Color(1f, 0.85f, 0.25f), 1.2f));
            combo = 5; comboTimer = ComboWindow;
        }

        // 따라 하는 잔상들 (고장난 비서 · 92퍼센트): 실제 판정을 복사해 잔상 자리에서 다시 냄
        Vector3 offset = tr.position - (psprite != null ? psprite.transform.position : PPos);
        if (secretary != null && secretary.ghost != null && Time.time < secretary.until) MimicSwing(secretary, tag, offset);
        foreach (var m in echoes) if (m.ghost != null && Time.time < m.until) MimicSwing(m, tag, offset);

        // 현상 재생: 이 공격의 판정 원본과 휘두르는 그림을 기록
        if (Has("현상 재생"))
        {
            if (replayTemplate != null) Destroy(replayTemplate);
            replayTemplate = CopyHitbox(tag.gameObject, tr.position, 1f, f, false);
            replayOffset = offset; replayFacing = f; replayAt = Time.time;
            replayFrames.Clear(); recordStart = Time.time; recordUntil = Time.time + 0.45f;
            if (psprite != null) replayPose = new Pose { pos = psprite.transform.position, sprite = psprite.sprite, scale = psprite.transform.lossyScale, flip = psprite.flipX };
        }

        // 역류 탄도: 퍼즈 중 멈춘 적 투사체를 치면 내 것이 됨
        if (Paused && Has("역류 탄도") && captured.Count < 8)
        {
            var col = tag.GetComponent<Collider2D>();
            Bounds b = col != null ? col.bounds : new Bounds(PCenter + new Vector3(f, 0f, 0f), new Vector3(2f, 1.5f, 1f));
            b.Expand(0.6f);
            foreach (var p in FindObjectsOfType<ProjectileBase>())
            {
                if (captured.Count >= 8) break;
                if (p == null || p.ownerLayer != "Enemy" || !p.enabled) continue;
                Vector3 pp = p.transform.position;
                if (!b.Contains(new Vector3(pp.x, pp.y, b.center.z))) continue;
                Capture(p);
            }
        }

        Keyword5OnSwing(tag);   // [2026-11-25] 연격 5 · 가열 블레이드: 3타 마무리 특수 평타
    }

    /// <summary>평타 판정을 복사 (피해 배율 · 방향). active=false 면 꺼 둔 원본으로 보관</summary>
    GameObject CopyHitbox(GameObject src, Vector3 pos, float ratio, int facing, bool active = true)
    {
        if (src == null) return null;
        var go = Instantiate(src, pos, src.transform.rotation);
        go.SetActive(false);
        var tag = go.GetComponent<SwingTag>();
        if (tag != null) DestroyImmediate(tag);   // 복사본은 '평타'로 치지 않음 (증강이 또 반응하지 않게)
        var s = go.transform.localScale; s.x = Mathf.Abs(s.x) * (facing < 0 ? -1 : 1); go.transform.localScale = s;
        var hb = go.GetComponent<Hitbox>();
        if (hb != null) { hb.damage = Mathf.Max(1, Mathf.RoundToInt(hb.damage * ratio)); hb.direction = facing; }
        if (active) go.SetActive(true);
        return go;
    }

    void SpawnFromTemplate(GameObject template, Vector3 pos, float ratio, int facing)
    {
        if (template == null) return;
        var go = Instantiate(template, pos, template.transform.rotation);
        var s = go.transform.localScale; s.x = Mathf.Abs(s.x) * (facing < 0 ? -1 : 1); go.transform.localScale = s;
        var hb = go.GetComponent<Hitbox>();
        if (hb != null) { hb.damage = Mathf.Max(1, Mathf.RoundToInt(hb.damage * ratio)); hb.direction = facing; }
        go.SetActive(true);
    }

    void MimicSwing(Mimic m, SwingTag tag, Vector3 offset)
    {
        int f = tag.facing;
        if (m.wrongWay && Random.value < 0.2f) f = -f;   // 가끔 엉뚱한 방향
        Vector3 off = new Vector3(Mathf.Abs(offset.x) * f, offset.y, offset.z);
        CopyHitbox(tag.gameObject, m.pos + off, m.ratio, f);
        // 잔상 그림도 지금 헤네시 동작으로 바뀌며 번쩍
        var r = m.ghost.Renderer;
        if (r != null && psprite != null)
        {
            r.sprite = psprite.sprite;
            var sc = psprite.transform.lossyScale;
            if (f != tag.facing) sc.x = -sc.x;
            r.transform.localScale = sc;
        }
        m.ghost.SetColor(Color.Lerp(m.color, Color.white, 0.5f));
        StartCoroutine(RestoreColor(m));
    }

    IEnumerator RestoreColor(Mimic m)
    {
        yield return new WaitForSeconds(0.12f);
        if (m.ghost != null) m.ghost.SetColor(m.color);
    }

    /// <summary>상자 판정의 지금 월드 범위 (물리 갱신을 기다리지 않고 크기 · 위치로 바로 계산)</summary>
    static Rect WorldRect(BoxCollider2D box)
    {
        var t = box.transform;
        Vector2 h = box.size * 0.5f, o = box.offset;
        Vector3 a = t.TransformPoint(o - h), b = t.TransformPoint(o + h), c = t.TransformPoint(new Vector2(o.x - h.x, o.y + h.y)), d = t.TransformPoint(new Vector2(o.x + h.x, o.y - h.y));
        float x0 = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), x1 = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float y0 = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)), y1 = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    IEnumerator AimOutline(Collider2D col)
    {
        yield return new WaitForFixedUpdate();   // 크기가 반영된 뒤의 범위
        if (col == null) yield break;
        var b = col.bounds;
        Color c = new Color(CCombo.r, CCombo.g, CCombo.b, 0.8f);
        AugmentFx.Line(new Vector3(b.min.x, b.max.y, 0f), new Vector3(b.max.x, b.max.y, 0f), c, 0.15f, 0.05f);
        AugmentFx.Line(new Vector3(b.min.x, b.min.y, 0f), new Vector3(b.max.x, b.min.y, 0f), c, 0.15f, 0.05f);
        AugmentFx.Line(new Vector3(PFacing > 0 ? b.max.x : b.min.x, b.min.y, 0f), new Vector3(PFacing > 0 ? b.max.x : b.min.x, b.max.y, 0f), c, 0.15f, 0.07f);
    }

    float HpRatioP() => pstats != null && pstats.maxHp > 0 ? (float)pstats.currentHp / pstats.maxHp : 1f;

    void OnComboFull()
    {
        if (Has("박자 칼날") && !bladeCharged)
        {
            bladeCharged = true;
            if (pc != null) AugmentFx.Ring(PCenter, new Color(1f, 0.85f, 0.25f), 1.5f, 0.3f, 0.09f);
        }
    }

    /// <summary>관통 빛줄기: 정면으로 달리며 지나가는 적을 모두 벰 (궁수 횡·종 일격 이펙트를 색 바꿈)</summary>
    IEnumerator PierceSlash(Vector3 from, int dir, float length, float ratio, Color c, float height)
    {
        var hit = new HashSet<Health>();
        Vector3 to = from + new Vector3(dir * length, 0f, 0f);
        const float speed = 30f;
        AugmentFx.StreakFx(from, to, c, speed, 0.8f);
        float time = length / speed, t = 0f;
        while (t <= time)
        {
            Vector3 head = Vector3.Lerp(from, to, Mathf.Clamp01(t / time));
            foreach (var e in EnemiesInBox(head, new Vector2(1.4f, height)))
                if (hit.Add(e)) { AbilityHit(e, ratio, from, 3f, 1f); AugmentFx.Sparks(EC(e), c, 8, 4f, 0.3f, 0f, 0.14f); }
            t += Time.deltaTime;
            yield return null;
        }
    }

    void OnLanded(Vector3 pos) { }   // [2026-11-21] 히어로 착지의 착지 충격파는 뺌

    // ══════════════ 맞힘 ══════════════

    int ComboExtraOnHit(CombatEvents.HitInfo hi)
    {
        int extra = 0;
        // 스택 백업: 저장해 둔 연격을 다음 공격에 되살림 → 이때부터 재사용 대기
        if (savedCombo > 0)
        {
            extra += Mathf.Max(0, savedCombo - combo);
            savedCombo = 0;
            float cd = ByLevel(L("스택 백업"), 10f, 7f, 5f);
            backupReadyAt = Time.time + cd;
            BuffIcon("스택 백업", cd, true);
            if (pc != null) AugmentFx.Ring(PFeet, CCombo, 1.2f, 0.35f, 0.08f, 1, 0.35f);
        }
        // 접속 단자: 대시 직후 첫 공격은 연격 추가 (노란 섬광)
        if (dashFirstHitArmed && Time.time < dashFirstHitUntil)
        {
            dashFirstHitArmed = false;
            extra += (int)ByLevel(L("접속 단자"), 2, 3, 4);
            if (pc != null) { AugmentFx.Ring(PCenter, CCombo, 1.4f, 0.3f, 0.1f, 2); AugmentFx.Sparks(hi.position, CCombo, 10, 4f, 0.3f, 0f, 0.15f); }
        }
        return extra;
    }

    bool TryBackupCombo(int before)
    {
        if (!Has("스택 백업") || before < 3 || Time.time < backupReadyAt || savedCombo > 0) return false;
        savedCombo = before;
        return true;
    }

    void AdvancedOnHit(CombatEvents.HitInfo hi, EnemyStatus st, bool paused)
    {
        var h = hi.target;
        // 잠깐의 휴식: 퍼즈 중 입힌 피해를 모아 둠
        if (paused && Has("잠깐의 휴식")) restDeferred += Mathf.Max(0, hi.damage);
        // 히어로 착지: 연격 5 이상(Lv2 4, Lv3 3)이면 공격마다 과열 1 확정
        int lvHero = L("히어로 착지");
        if (lvHero > 0 && combo >= (int)ByLevel(lvHero, 5, 4, 3)) st.AddOverheat(1);

        // 검무: [2026-11-25] 검 3타에 불타는 검기가 날아가 과열을 부여 → AugmentRuntime.Keyword5.cs (근접 타격 자체의 과열은 뺌)

        // 쐐기 각인: 균열이 최대인 적을 치명타로 → 균열을 모두 쓰고 추가 피해
        int lvWedge = L("쐐기 각인");
        if (lvWedge > 0 && hi.isCrit && st.IsMaxFracture)
        {
            st.ClearFracture();
            AugmentFx.Shatter(EC(h), CFracture, 0.7f);
            AugmentFx.Sparks(EC(h), CFracture, 14, 5f, 0.4f);
            AbilityHit(h, ByLevel(lvWedge, 2f, 3f, 4f), PPos, 3f, 1.5f);
        }

        // 과충전 망치: 체력 50% 이하면 차지 강타에 맞은 적 기절
        int lvHammer = L("과충전 망치");
        if (lvHammer > 0 && hi.swing != null && hi.swing.weapon == Weapontype.Hammer && hi.swing.charged && HpRatioP() <= 0.5f)
        {
            StunEnemy(h, ByLevel(lvHammer, 0.8f, 1.0f, 1.2f), COverload);
            AugmentFx.Sparks(EC(h), COverload, 10, 6f, 0.2f, 0f, 0.1f);   // 분홍 전기
        }

        // 강철 리듬: 연격 10에서 5타마다 헤네시를 중심으로 둥근 충격파
        int lvRhythm = L("강철 리듬");
        if (lvRhythm > 0 && hi.swing != null && hi.swing.weapon == Weapontype.Glove && combo >= 10 && hi.swing.once.Add("리듬"))
        {
            if (++rhythmCount % 5 == 0)
            {
                AugmentFx.Burst(PCenter, CCombo, 0.9f);
                AugmentFx.Ripple(PCenter, CCombo, 2.6f, 0.4f, 0.08f, 2);
                CameraFollow.Shake(0.2f, 0.15f);
                foreach (var e in EnemiesIn(PCenter, 2.6f)) AbilityHit(e, ByLevel(lvRhythm, 0.7f, 1.05f, 1.4f), PPos, 4f, 1.5f);
            }
        }

        // 불명의 데이터: 공격이 맞을 때 탈취 데이터 1개를 써서 무작위 강화 (피해 빨강 · 넉백 흰색 · 경직 파랑 · 과열 주황)
        int lvUnknown = L("불명의 데이터");
        if (lvUnknown > 0 && FragmentsBefore(AttackStart(hi)) > 0 && Time.time >= unknownReady && !h.IsDead && UseFragment(AttackStart(hi)))
        {
            unknownReady = Time.time + 0.8f;
            int n = lvUnknown >= 3 ? 2 : 1;
            var pool = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                int pick = lvUnknown >= 2 && pool.Contains(0) && Random.value < 0.4f ? 0 : pool[Random.Range(0, pool.Count)];
                pool.Remove(pick);
                if (pick == 0) { AbilityHit(h, 0.5f, PPos, 1f, 0.5f); AugmentFx.Burst(EC(h), new Color(1f, 0.25f, 0.2f), 0.5f); }
                else if (pick == 1) { var rb = h.GetComponent<Rigidbody2D>(); if (rb != null && !IsBoss(h)) rb.velocity = new Vector2(PFacing * 14f, 4f); AugmentFx.Ripple(EC(h), Color.white, 1.6f, 0.3f, 0.06f); }
                else if (pick == 2) { StunEnemy(h, 1f, new Color(0.55f, 0.8f, 1f)); AugmentFx.Sparks(EC(h), new Color(0.6f, 0.85f, 1f), 10, 3f, 0.5f, 2f, 0.16f); }
                else { st.AddOverheat(3); AugmentFx.Sparks(EC(h), OverheatColor, 10, 4f, 0.4f); }
            }
        }

        // 백자 열람 권한: 스킬에 맞은 적의 방패·버프 2초 무효 + 균열 2 (같은 적 8초에 한 번)
        if (hi.fromSkill && Has("백자 열람 권한")) ReadShield(h, st);

        // 404: 처형 대상 일반 적을 일정 확률로 글리치와 함께 삭제
        int lv404 = L("404");
        if (lv404 > 0 && !h.IsDead && !IsBoss(h) && IsExecuteTarget(h) && Random.value < ByLevel(lv404, 0.15f, 0.22f, 0.30f))
        {
            Delete404(h);
            return;
        }
        // 마지막 한 줄: 기준 이하 일반 적은 즉시 처치 + 퍼즈 게이지 +5
        if (!h.IsDead && InstantKillReady(h)) LastLine(h);
    }

    void AdvancedOnAbilityHit(CombatEvents.HitInfo hi, EnemyStatus st)
    {
        if (hi.fromSkill && Has("백자 열람 권한")) ReadShield(hi.target, st);
    }

    void ReadShield(Health h, EnemyStatus st)
    {
        if (h == null || Time.time < st.shieldBreakReadyAt) return;
        st.shieldBreakReadyAt = Time.time + 8f;
        h.invulnSuppressedUntil = Time.time + 2f;
        st.AddFracture(2);
        AugmentFx.Shatter(EC(h), Color.white, 0.8f);
        AugmentFx.Ring(EC(h), CFracture, 1.4f, 0.35f, 0.08f, 2);
    }

    void Delete404(Health h)
    {
        var sr = AugmentFx.PlayerSprite(h);
        if (sr != null) AugmentFx.Ghost(sr, new Color(0.4f, 1f, 0.9f), 0.35f, 0.9f);
        AugmentFx.ScreenPulse(0.35f, 0.1f);
        var b = AugmentFx.BodyBounds(h);
        for (int i = 0; i < 3; i++) AugmentFx.Sparks(new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, (i + 0.5f) / 3f), 0f), i == 1 ? CPlunder : new Color(0.9f, 0.2f, 0.9f), 6, 3f, 0.35f, 0f, 0.13f);   // 픽셀이 흩어짐
        Vector3 c = EC(h);
        var st = h.GetComponent<Stats>();
        if (st != null) h.TakeDamage(st.currentHp + 1, PFacing, 0f, 0f);
        AddFragment(1, c, 0);
    }

    void LastLine(Health h)
    {
        var st = h.GetComponent<Stats>();
        if (st == null) return;
        Vector3 c = EC(h);
        AugmentFx.Streak(c + new Vector3(-1.4f, 0.5f, 0f), c + new Vector3(1.4f, -0.5f, 0f), CExec, 0.08f, 0.2f);   // 붉은 일자 베기
        var sr = AugmentFx.PlayerSprite(h);
        if (sr != null) AugmentFx.Ghost(sr, CExec, 0.5f, 0.9f);   // 붉게 녹아 사라짐
        h.TakeDamage(st.currentHp + 1, PFacing, 0f, 0f);
        PauseManager.Instance?.AddGauge(5f);
    }

    // ══════════════ 처치 ══════════════

    void AdvancedOnKill(Health h, Vector3 pos, EnemyStatus st, bool overheated, bool hot, bool executed)
    {
        bool paused = Paused;
        Vector3 c = EC(h);
        // 번제: 과열된 적 처치 → 그 자리에 {3/4/5}초 불길
        // [2026-11-24] 체력 회복 제거 (회복은 과부하 키워드 전용) — 대신 단계마다 불길이 오래 남음
        int lvAltar = L("번제");
        if (lvAltar > 0 && overheated)
            StartCoroutine(FireFloor(EFeet(h), ByLevel(lvAltar, 3f, 4f, 5f)));
        // 수집가의 불꽃: 과열 5스택 이상인 적이 쓰러지면 데이터 1개 (불꽃 속에서 결정)
        if (Has("수집가의 불꽃") && hot) CollectorData(c);
        // 빼앗은 기술: 처치한 적의 기술을 빼앗음 (근접 = 초록, 원거리 = 청록 결정이 날아옴)
        if (Has("빼앗은 기술") && !IsBoss(h) && pc != null)
        {
            var e = h.GetComponent<Enemy>();
            bool ranged = e != null && e.projectilePrefab != null;
            if (ranged) rangedSlash = true; else meleeBoost = true;
            if (releasing) { rangedSlash = true; meleeBoost = true; }   // [2026-11-23] 방출 중 처치하면 두 기술 모두
            AugmentFx.Fly(c, pc.transform, ranged ? new Color(0.3f, 0.95f, 0.95f) : CPlunder, 1, 0.4f, null, 0.26f);
        }
        // 특이점 조항: 퍼즈 중 처치 → 데이터 2개 + 퍼즈 게이지 (풀린 뒤 들어감)
        int lvPatent = L("특이점 조항");
        if (paused && lvPatent > 0)
        {
            AddFragment(2, c, 0);
            queuedGauge += ByLevel(lvPatent, 5f, 8f, 10f);
            if (pc != null) AugmentFx.Fly(c, pc.transform, CStasis, 3, 0.5f, null, 0.14f, false);
        }
        if (weakTarget != null && weakTarget.Health == h) { if (weakMark != null) Destroy(weakMark); weakTarget = null; }
    }

    void CollectorData(Vector3 pos)
    {
        AugmentFx.Sparks(pos, OverheatColor, 8, 3.5f, 0.4f, -1f);
        AddFragment(1, pos, 0);
    }

    IEnumerator FireFloor(Vector3 feet, float seconds)
    {
        Vector3 c = feet + Vector3.up * 0.4f;
        Vector2 size = new Vector2(2.6f, 1.2f);
        AugmentFx.FloorGlow(feet, size.x, OverheatColor, seconds);   // [2026-11-25] 위험 박스(적 공격 예고용) 대신 바닥 불빛
        float end = Time.time + seconds, tick = 0f;
        while (Time.time < end)
        {
            if (!Paused)
            {
                tick -= Time.deltaTime;
                if (Random.value < 0.35f) AugmentFx.Sparks(c + new Vector3(Random.Range(-1.2f, 1.2f), -0.4f, 0f), OverheatColor, 1, 1f, 0.6f, -3f, 0.14f);   // 작은 불꽃
                if (tick <= 0f)
                {
                    tick = 1f;
                    foreach (var e in EnemiesInBox(c, size)) StatusOf(e).AddOverheat(1);
                }
            }
            yield return null;
        }
    }

    // ── 과열 5스택: 연쇄 발화 ──
    void OnOverheatChanged(EnemyStatus st, int before, int after)
    {
        int lv = L("연쇄 발화");
        if (lv <= 0 || st == null || after < OverheatBurst || before >= OverheatBurst || st.chainArmed || Time.time < st.explodeImmuneUntil) return;
        StartCoroutine(ChainIgnite(st, ByLevel(lv, 1.2f, 1.8f, 2.4f)));
    }

    IEnumerator ChainIgnite(EnemyStatus st, float ratio)
    {
        st.chainArmed = true;
        Vector3 feet = EFeet(st);
        float r = ExplosionRadius(2f);
        AugmentFx.Disc(feet + Vector3.up * 0.05f, r, OverheatColor, 0.35f, 0.3f);   // 터지기 0.3초 전 바닥 주황 원
        yield return new WaitForSeconds(0.3f);
        while (Paused) yield return null;
        if (st == null) yield break;
        st.chainArmed = false;
        feet = EFeet(st);
        Vector3 c = EC(st);
        bool wasHot = st.IsHot;
        Explode(feet, 2f, ratio);
        foreach (var e in EnemiesIn(c, r))
        {
            if (e == st.Health || e.IsDead) continue;
            var target = e;
            AugmentFx.Fly(c, e.transform, OverheatColor, 1, 0.35f, () => { if (target != null && !target.IsDead) StatusOf(target).AddOverheat(2); }, 0.2f, false);   // 불티가 곡선으로 튐
        }
        if (Has("수집가의 불꽃") && wasHot) CollectorData(c);
    }

    // ══════════════ 탈취 데이터 사용 ══════════════

    void OnFragmentUsed(int kind)
    {
        // 수집가의 불꽃: 데이터를 쓰면 다음 과열 폭발 반경 증가
        if (Has("수집가의 불꽃")) collectorBoost = true;
    }

    // ══════════════ 잔상 ══════════════

    void OnAfterimageHit(Health e, Transform ghost)
    {
        // 잔향 공명: 잔상 피해도 과열·균열
        if (Has("잔향 공명")) { var st = StatusOf(e); st.AddOverheat(1); st.AddFracture(1); }
        // 접속 단자: 잔상이 맞히면 연격 +1 ([2026-11-22] 노란 신호선 제거)
        if (Has("접속 단자") && pc != null) AddCombo(1, e.transform.position);
    }

    void OnAfterimageEnd(Vector3 center)
    {
        // 잔향 공명: 잔상이 사라질 때 폭발 (하늘색 음파 링 여러 겹)
        int lv = L("잔향 공명");
        if (lv <= 0) return;
        AugmentFx.Burst(center, CloakRed, 0.7f, 1.4f);   // 붉은 폭발 (파문은 쓰지 않음)
        AugmentFx.Sparks(center, CloakRed, 10, 4f, 0.35f, 2f, 0.14f);
        foreach (var e in EnemiesIn(center, 1.5f)) AbilityHit(e, ByLevel(lv, 0.8f, 1.2f, 1.6f), center, 3f, 1.5f);
    }

    // ══════════════ 퍼즈 ══════════════

    void AdvancedOnPauseStart()
    {
        slowAdded = 0f; queuedGauge = 0f; restDeferred = 0; restStill = 0f; restLastPos = PPos;
        captured.Clear(); horizonHits.Clear();
        pausePoses.Clear(); poseNext = 0f;


    }

    void AdvancedOnPauseEnd()
    {
        // 특이점 조항: 모아 둔 게이지
        if (queuedGauge > 0f) { PauseManager.Instance?.AddGauge(queuedGauge); queuedGauge = 0f; }

        // 정지 잔상: 구체가 곡선을 그리며 가까운 적에게 돌진
        int lvOrb = L("정지 잔상");
        foreach (var o in stasisOrbs)
            if (o != null) StartCoroutine(OrbDash(o, ByLevel(Mathf.Max(1, lvOrb), 1f, 1.5f, 2f)));
        stasisOrbs.Clear();

        // 역류 탄도: 친 투사체가 쏜 적에게 2배 속도로 돌아감
        float ratioShot = ByLevel(L("역류 탄도"), 1.5f, 2.25f, 3f);
        foreach (var c in captured) StartCoroutine(ReturnShot(c, ratioShot));
        captured.Clear();
        foreach (var kv in shotRings) if (kv.Value != null) Destroy(kv.Value.gameObject);
        shotRings.Clear();


        // 사건의 지평선: 저장한 피해를 한 번에 (보스는 최대 체력 25%까지)
        int lvHor = L("사건의 지평선");
        if (lvHor > 0 && horizonHits.Count > 0)
        {
            float mult = ByLevel(lvHor, 1.5f, 2f, 2.5f);
            bool any = false;
            foreach (var st in horizonHits)
            {
                if (st == null || st.Health == null || st.Health.IsDead || st.horizonStored <= 0) continue;
                int dmg = Mathf.RoundToInt(st.horizonStored * mult);
                if (st.IsBoss && st.Stats != null) dmg = Mathf.Min(dmg, Mathf.RoundToInt(st.Stats.maxHp * 0.25f));
                st.horizonStored = 0;
                Vector3 p = st.Center;
                AugmentFx.Burst(p, Color.white, 1.1f);
                AugmentFx.Ring(p, new Color(0.75f, 0.7f, 1f), 2.2f, 0.35f, 0.14f, 2);
                st.Health.TakeDamage(Mathf.Max(1, dmg), PFacing, 3f, 2f);
                FloatingTextSpawner.SpawnDamage(p, dmg, true);   // 피해 숫자
                any = true;
            }
            if (any) { AugmentFx.ScreenPulse(0.55f, 0.22f); CameraFollow.Shake(0.4f, 0.3f); }
        }
        foreach (var st in horizonHits) if (st != null) st.horizonStored = 0;
        horizonHits.Clear();

        // [2026-11-21] 잠깐의 휴식: 퍼즈 중 입힌 피해의 10%가 풀린 뒤 체력으로 돌아옴 (예전: 받은 피해 절반)
        if (restDeferred > 0 && pstats != null)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(restDeferred * 0.10f));
            restDeferred = 0;
            HealPlayer(heal, HealGreen);
            AugmentFx.Ring(PFeet, HealGreen, 1.6f, 0.4f, 0.1f, 2, 0.35f);
        }
        if (restRing != null) restRing.Show(false);

        // 92퍼센트 (떠올림 6개부터): 퍼즈 중 남긴 잔상의 20%가 5초 동안 남아 내 공격을 따라 함
        int lv92 = L("92퍼센트");
        if (lv92 > 0 && recall >= 6 && pausePoses.Count > 0)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(pausePoses.Count * 0.2f), 1, 6);
            float ratio = ByLevel(lv92, 0.6f, 0.8f, 1.0f);
            for (int i = 0; i < n; i++)
            {
                var pose = pausePoses[Mathf.Min(pausePoses.Count - 1, Mathf.FloorToInt((i + 0.5f) * pausePoses.Count / n))];
                var m = MakeMimic(pose.pos, 5f, ratio, CStasis, false);
                if (m == null) continue;
                var r = m.ghost.Renderer;
                r.sprite = pose.sprite; r.transform.localScale = pose.scale; r.flipX = pose.flip;
                echoes.Add(m);
            }
            BuffIcon("92퍼센트", 5f);
        }
        pausePoses.Clear();
    }

    IEnumerator OrbDash(GameObject orb, float ratio)
    {
        if (orb == null) yield break;
        Vector3 from = orb.transform.position;
        var target = Nearest(from, 12f);
        if (target == null) { Destroy(orb); AugmentFx.Sparks(from, CStasis, 6, 2f, 0.3f); yield break; }
        float t = 0f, time = 0.35f;
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 last = from;
        while (t < time && orb != null && target != null)
        {
            Vector3 to = EC(target);
            float k = t / time, u = 1f - k;
            // 곡선: 위·옆으로 크게 휘는 2차 베지어
            Vector3 ctrl = (from + to) * 0.5f + new Vector3(0f, 2.2f, 0f) + Vector3.Cross((to - from).normalized, Vector3.forward) * side * 1.2f;
            Vector3 p = u * u * from + 2f * u * k * ctrl + k * k * to;
            AugmentFx.Line(last, p, CStasis, 0.25f, 0.14f);   // 돌진 궤적
            orb.transform.position = p;
            last = p;
            t += Time.deltaTime;
            yield return null;
        }
        if (orb != null) Destroy(orb);
        if (target != null && !target.IsDead)
        {
            AbilityHit(target, ratio, from, 3f, 1.5f);
            AugmentFx.Burst(EC(target), CStasis, 0.6f);
        }
    }

    void Capture(ProjectileBase p)
    {
        var go = p.gameObject;
        var shooter = p.attackerStats != null ? p.attackerStats.transform : null;
        p.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider2D>()) c.enabled = false;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.velocity = Vector2.zero; rb.isKinematic = true; }
        foreach (var r in go.GetComponentsInChildren<SpriteRenderer>()) r.color = new Color(0.45f, 0.65f, 1f, r.color.a);   // 친 투사체는 파랗게
        AugmentFx.Ring(go.transform.position, CStasis, 0.8f, 0.25f, 0.06f);
        captured.Add(new CapturedShot { go = go, shooter = shooter });
        if (shotRings.TryGetValue(p, out var ring) && ring != null) ring.color = CStasis;
    }

    IEnumerator ReturnShot(CapturedShot c, float ratio)
    {
        if (c.go == null) yield break;
        Vector3 pos = c.go.transform.position;
        Health target = c.shooter != null ? c.shooter.GetComponent<Health>() : null;
        if (target == null || target.IsDead) target = Nearest(pos, 15f);
        float speed = 20f, life = 2.5f;
        while (life > 0f && c.go != null)
        {
            Vector3 to = target != null && !target.IsDead ? EC(target) : pos + Vector3.right * 10f;
            Vector3 d = to - c.go.transform.position;
            if (d.magnitude < 0.7f && target != null)
            {
                AbilityHit(target, ratio, c.go.transform.position, 3f, 1f);
                AugmentFx.Burst(c.go.transform.position, CStasis, 0.5f);
                break;
            }
            Vector3 step = d.normalized * speed * Time.deltaTime;
            c.go.transform.position += step;
            c.go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            AugmentFx.Line(c.go.transform.position - step * 3f, c.go.transform.position, CStasis, 0.12f, 0.08f);
            life -= Time.deltaTime;
            yield return null;
        }
        if (c.go != null) Destroy(c.go);
    }

    void OnEnemyAttack(Enemy e) { }   // [2026-11-21] 궤적 예측이 '퍼즈 중 대시로 지나친 적에게 피해'로 바뀌어 쓰지 않음

    IEnumerator WatchDodge(Enemy e)
    {
        float end = Time.time + 0.7f;
        while (Time.time < end && e != null && pc != null)
        {
            if (pc.IsDashing && Mathf.Abs(e.transform.position.x - PPos.x) < e.attackRange + 1.8f && !countered.Contains(e))
            {
                countered.Add(e);
                AugmentFx.BodyFlash(pc, CStasis, 0.9f, 0.25f);
                AugmentFx.Streak(PCenter, EC(e), CStasis, 0.1f, 0.22f);
                AbilityHit(e.GetComponent<Health>(), ByLevel(L("궤적 예측"), 1f, 1.5f, 2f), PPos, 3f, 1f);
                yield break;
            }
            yield return null;
        }
    }

    // ══════════════ 방 ══════════════

    void AdvancedOnRoomLoaded(Room room)
    {
        savedCombo = 0; bladeCharged = false;
        if (weakMark != null) Destroy(weakMark);
        weakTarget = null;
        if (Has("약점 스캔")) StartCoroutine(WeakScan());
    }

    /// <summary>시험 방: 방에 막 들어온 것처럼 (약점 스캔 등 다시)</summary>
    public void TestRoomEntered() { OnRoomLoaded(null, false); }

    /// <summary>시험 방: 방을 정리한 것처럼 (92퍼센트 떠올림 · 비상식량 등)</summary>
    public void TestRoomCleared() { OnRoomCleared(null); }

    IEnumerator WeakScan()
    {
        yield return new WaitForSeconds(0.8f);
        Health best = null; int bestHp = 0;
        foreach (var e in FindObjectsOfType<Enemy>())
        {
            var h = e.GetComponent<Health>(); var s = e.GetComponent<Stats>();
            if (h == null || s == null || h.IsDead) continue;
            if (s.currentHp > bestHp) { bestHp = s.currentHp; best = h; }
        }
        foreach (var b in FindObjectsOfType<BossController>())
        {
            var h = b.GetComponent<Health>(); var s = b.GetComponent<Stats>();
            if (h != null && s != null && !h.IsDead && s.currentHp > bestHp) { bestHp = s.currentHp; best = h; }
        }
        if (best == null) yield break;
        // 보라 스캔 선이 화면을 한 번 훑음
        var cam = Camera.main;
        if (cam != null)
        {
            float hh = cam.orthographicSize;
            float w = hh * cam.aspect;
            Vector3 c = cam.transform.position; c.z = 0f;
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(c.x - w, c.x + w, i / 7f);
                AugmentFx.Line(new Vector3(x, c.y - hh, 0f), new Vector3(x, c.y + hh, 0f), new Color(CFracture.r, CFracture.g, CFracture.b, 0.7f), 0.12f, 0.08f);
                yield return new WaitForSeconds(0.03f);
            }
        }
        if (best == null || best.IsDead) yield break;
        weakTarget = StatusOf(best);
        weakTarget.weakPoint = true;
        weakTarget.weakExtra = (int)ByLevel(L("약점 스캔"), 2, 3, 4);
        weakTarget.RefreshBadge();
        weakMark = AugmentFx.Mark(best.transform, CFracture, 1f, true, (EC(best) - best.transform.position).y);
    }

    void AdvancedOnRoomCleared(Room room)
    {
        // 92퍼센트: 방 정리마다 떠올림 +1 (최대 12)
        if (Has("92퍼센트") && recall < 12)
        {
            recall++;
            if (pc != null) { AugmentFx.Ring(PCenter, new Color(0.85f, 0.9f, 1f), 1.6f, 0.4f, 0.08f); if (recall == 6) AugmentFx.Ring(PCenter, CStasis, 3f, 0.6f, 0.12f, 2); }
        }
        // [2026-11-21] 잠깐의 휴식: 방 정리 때 퍼즈 게이지가 오르던 것은 뺌
    }

    void AdvancedOnPlayerDamaged(int dmg)
    {
        // 나노 봉합: 체력 50% 이하에서 잃은 체력의 일부가 3초 뒤 돌아옴
        if (Has("나노 봉합") && HpRatioP() <= 0.5f) QueueNano(dmg);
    }

    // ══════════════ 매 프레임 ══════════════

    void UpdateAdvanced(bool paused)
    {
        if (pc == null) return;

        // 박자 칼날: 충전 중 노란 맥동
        if (bladeCharged)
        {
            bladePulse -= Time.deltaTime;
            if (bladePulse <= 0f) { bladePulse = 0.35f; AugmentFx.BodyFlash(pc, new Color(1f, 0.85f, 0.25f), 0.5f, 0.3f); }
        }

        // 히어로 착지: 연격 조건을 넘으면 무기에 불꽃
        int lvHero = L("히어로 착지");
        if (lvHero > 0 && combo >= (int)ByLevel(lvHero, 5, 4, 3))
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f) { fireTimer = 0.12f; AugmentFx.Sparks(PCenter + new Vector3(PFacing * 0.7f, 0f, -0.5f), OverheatColor, 1, 1.2f, 0.35f, -3f, 0.12f); }
        }

        // 궤적 예측: 퍼즈 중 대시로 지나친 적에게 피해
        if (Time.time < lensUntil)
        {
            int lvLens = L("궤적 예측");
            foreach (var e in EnemiesIn(PCenter, 1.2f))
                if (lensHit.Add(e))
                {
                    AbilityHit(e, ByLevel(lvLens, 1f, 1.5f, 2f), PPos, 1f, 0.5f);
                    AugmentFx.Streak(EC(e) + new Vector3(-PFacing * 0.9f, 0.4f, 0f), EC(e) + new Vector3(PFacing * 0.9f, -0.3f, 0f), CStasis, 0.08f, 0.2f);
                    AugmentFx.Sparks(EC(e), CStasis, 8, 4f, 0.3f, 0f, 0.13f);
                }
        }

        // 파편 반사: 대시로 통과한 적 균열 + 튕긴 파편 (투사체 막기는 뺌)
        if (Time.time < reflectUntil)
        {
            int lvRef = L("파편 반사");
            foreach (var e in EnemiesIn(PCenter, 1.1f))
                if (reflected.Add(e))
                {
                    StatusOf(e).AddFracture(1);
                    AbilityHit(e, ByLevel(lvRef, 0.30f, 0.45f, 0.60f), PPos, 1f, 0.5f);
                    AugmentFx.Sparks(EC(e), CFracture, 8, 5f, 0.3f);
                }
        }

        // 치유 결계: 체력 50% 이하 = 분홍 가시 결계(주변 피해), 초과 = 초록 잎 결계(회복)
        int lvBarrier = L("치유 결계");
        if (lvBarrier > 0)
        {
            bool low = HpRatioP() <= 0.5f;
            // [2026-11-21] 영역 테두리는 그리지 않음 (맞는 적의 분홍 불티 · 회복 연출만)
            if (!paused)
            {
                barrierTick -= Time.deltaTime;
                if (barrierTick <= 0f)
                {
                    barrierTick = 1f;
                    if (low)
                    {
                        foreach (var e in EnemiesIn(PPos, 3f)) { AbilityHit(e, ByLevel(lvBarrier, 0.15f, 0.22f, 0.30f), PPos, 0.5f, 0.2f); AugmentFx.Sparks(EC(e), COverload, 4, 3f, 0.25f, 0f, 0.12f); }
                    }
                    else if (pstats != null && pstats.currentHp < pstats.maxHp)
                    {
                        HealPlayer(Mathf.Max(1, Mathf.RoundToInt(pstats.maxHp * 0.005f)), HealGreen);   // [2026-11-25] 1% → 0.5%
                    }
                }
            }
        }
        else if (barrierRing != null) { Destroy(barrierRing.gameObject); barrierRing = null; }

        // 현상 재생: 휘두르는 동안의 그림 기록 → 일정 시간마다 그 자리에서 잔상이 같은 동작 + 실제 판정(피해 50%)
        if (Time.time < recordUntil && psprite != null) replayFrames.Add(new KeyValuePair<float, Sprite>(Time.time - recordStart, psprite.sprite));
        int lvReplay = L("현상 재생");
        if (lvReplay > 0 && !paused)
        {
            replayTimer -= Time.deltaTime;
            if (replayTimer <= 0f)
            {
                replayTimer = ByLevel(lvReplay, 3f, 2.5f, 2f);
                if (replayTemplate != null && Time.time - replayAt < 3f) StartCoroutine(ReplaySwing());
            }
        }

        // 잠깐의 휴식: 퍼즈 중 움직이지 않으면 발밑 초록 원이 차오르고 1초마다 회복
        int lvRest = L("잠깐의 휴식");
        if (lvRest > 0 && paused)
        {
            if (restRing == null) restRing = AugmentFx.Circle(pc.transform, 0.2f, HealGreen, 0.07f);
            restRing.offset = PFeet - PPos;
            restRing.yScale = 0.35f;
            restRing.Show(true);
            bool still = (PPos - restLastPos).sqrMagnitude < 0.0004f;
            restLastPos = PPos;
            restStill = still ? restStill + Time.unscaledDeltaTime : 0f;
            restRing.radius = Mathf.Lerp(0.2f, 1.1f, Mathf.Clamp01(restStill));
            if (restStill >= 1f && pstats != null)
            {
                restStill = 0f;
                HealPlayer(Mathf.Max(1, Mathf.RoundToInt(pstats.maxHp * ByLevel(lvRest, 0.01f, 0.015f, 0.02f))), HealGreen);
                AugmentFx.Ring(PFeet, HealGreen, 1.3f, 0.3f, 0.08f, 1, 0.35f);
            }
        }

        // 92퍼센트: 퍼즈 중 동선 기록 (퍼즈 잔상 간격과 비슷하게)
        if (paused && recall >= 6 && Has("92퍼센트") && psprite != null && Time.unscaledTime >= poseNext && pausePoses.Count < 60)
        {
            poseNext = Time.unscaledTime + 0.2f;
            pausePoses.Add(new Pose { pos = psprite.transform.position, sprite = psprite.sprite, scale = psprite.transform.lossyScale, flip = psprite.flipX });
        }
        // 따라 하는 잔상 정리
        for (int i = echoes.Count - 1; i >= 0; i--)
            if (echoes[i].ghost == null || Time.time >= echoes[i].until) { if (echoes[i].ghost != null) echoes[i].ghost.Release(); echoes.RemoveAt(i); }
        if (secretary != null && (secretary.ghost == null || Time.time >= secretary.until)) { if (secretary.ghost != null) secretary.ghost.Release(); secretary = null; }

        // 역류 탄도: 퍼즈 중 칠 수 있는 투사체에 하늘색 테두리
        if (paused && Has("역류 탄도"))
        {
            markScan -= Time.unscaledDeltaTime;
            if (markScan <= 0f)
            {
                markScan = 0.25f;
                foreach (var p in FindObjectsOfType<ProjectileBase>())
                {
                    if (p == null || p.ownerLayer != "Enemy" || !p.enabled || shotRings.ContainsKey(p)) continue;
                    shotRings[p] = AugmentFx.Circle(p.transform, 0.45f, CAfter, 0.04f);
                }
            }
        }

        UpdateMarks();
    }

    IEnumerator ReplaySwing()
    {
        var frames = new List<KeyValuePair<float, Sprite>>(replayFrames);
        var pose = replayPose;
        if (psprite == null) yield break;
        var g = AugmentFx.Ghost(psprite, CAfter, 0.7f, 0.7f, false, pose.pos);
        if (g == null) yield break;
        var r = g.Renderer;
        r.transform.localScale = pose.scale; r.flipX = pose.flip;
        if (frames.Count == 0 && pose.sprite != null) r.sprite = pose.sprite;
        SpawnFromTemplate(replayTemplate, pose.pos + replayOffset, 0.5f, replayFacing);   // 실제 판정 (피해 50%)
        float t = 0f; int fi = 0;
        float total = frames.Count > 0 ? frames[frames.Count - 1].Key : 0f;
        while (t <= total && g != null)
        {
            while (fi < frames.Count - 1 && frames[fi + 1].Key <= t) fi++;
            if (frames.Count > 0) r.sprite = frames[fi].Value;   // 공격 동작 그대로 재생
            t += Time.deltaTime;
            yield return null;
        }
    }

    float markTimer;
    void UpdateMarks()
    {
        markTimer -= Time.deltaTime;
        if (markTimer > 0f) return;
        markTimer = 0.25f;
        bool wedge = Has("쐐기 각인"), observe = Has("원거리 관측");
        if (!wedge && !observe && wedgeMarks.Count == 0 && execArrows.Count == 0) return;
        var alive = new HashSet<Health>();
        foreach (var st in FindObjectsOfType<EnemyStatus>())
        {
            var h = st.Health;
            if (h == null || h.IsDead) continue;
            alive.Add(h);
            bool wantWedge = wedge && st.IsMaxFracture && st != weakTarget;
            wedgeMarks.TryGetValue(h, out var wm);
            if (wantWedge && wm == null) wedgeMarks[h] = AugmentFx.Mark(h.transform, CFracture, 0.8f, true, (EC(h) - h.transform.position).y);
            else if (!wantWedge && wm != null) { Destroy(wm); wedgeMarks.Remove(h); }
        }
        if (observe)
        {
            foreach (var e in FindObjectsOfType<Enemy>())
            {
                var h = e.GetComponent<Health>();
                if (h == null || h.IsDead) continue;
                alive.Add(h);
                bool want = IsExecuteTarget(h);
                execArrows.TryGetValue(h, out var arrow);
                if (want)
                {
                    if (arrow == null) execArrows[h] = AugmentFx.EdgeArrow(h.transform, CExec);
                    e.TintFlash(CExec, 0.45f, 0.22f);   // 화면 안이면 몸에 붉은 윤곽 (맥동)
                }
                else if (arrow != null) { Destroy(arrow.gameObject); execArrows.Remove(h); }
            }
        }
        foreach (var k in new List<Health>(wedgeMarks.Keys)) if (k == null || !alive.Contains(k)) { if (wedgeMarks[k] != null) Destroy(wedgeMarks[k]); wedgeMarks.Remove(k); }
        foreach (var k in new List<Health>(execArrows.Keys)) if (k == null || !alive.Contains(k) || !observe) { if (execArrows[k] != null) Destroy(execArrows[k].gameObject); execArrows.Remove(k); }
    }
}
