using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 증강 능력을 실제로 돌리는 곳 — 키워드 8종 단계 효과 + 고유능력 59종 전부.
///   이 파일: 바탕(보유 읽기 · 피해 끼어들기 · 사건 연결 · 머리 위 표시) + 키워드 + 일반 20종
///   AugmentRuntime.Advanced.cs: 희귀 22 · 영웅 11 · 전설 6
///
/// 보유 증강은 인벤토리(Inventory.passives), 단계는 RunManager.GetAugmentLevel 에서 읽는다.
/// 전투 사건은 CombatEvents 로 듣고, 피해 계산은 CombatEvents 의 끼어들기 칸을 채운다.
/// 씬에 따로 둘 필요 없음 — 처음 필요할 때 스스로 생긴다 (이펙트 연결은 AugmentOfferDirector 칸에서 가져옴).
///
/// 연출은 기획서 '연출 기획' 부품(AugmentFx)으로만 한다. 떠오르는 글자는 피해 숫자와 회복량만.
/// 이름: 강탈 자원 = '탈취 데이터'(코드 이름 fragments), 92퍼센트의 조각 = '떠올림'(코드 이름 recall).
/// 수치는 증강 기획서 전환표·키워드 표 그대로. 기획서와 다르게 단순화한 곳은 [단순화] 로 적어 둠.
/// </summary>
public partial class AugmentRuntime : MonoBehaviour
{
    public static AugmentRuntime Instance { get; private set; }

    // ── 공용 값 (EnemyStatus 가 읽음) ──
    public static readonly Color OverheatColor = new Color(0.91f, 0.33f, 0.16f, 1f);
    /// <summary>과열 폭발 계열이 쓰는 기준 (상한 없음 · 5스택 이상)</summary>
    public const int OverheatBurst = 5;
    public static float OverheatDuration => Instance != null && Instance.Kw(AugmentKeyword.Overheat) >= 3 ? 6f : 4f;
    public static int FractureMax => Instance != null && Instance.Kw(AugmentKeyword.Fracture) >= 3 ? 4 : 3;
    public static float FractureDuration => 6f;
    public static float PlayerAtk => Instance != null && Instance.PStats != null ? Instance.PStats.Atk : 10f;
    public static TMP_FontAsset Font => Instance != null ? Instance.font : null;
    public static Sprite KeywordIcon(AugmentKeyword k) => Instance != null && Instance.icons != null && (int)k < Instance.icons.Length ? Instance.icons[(int)k] : null;

    static Color KC(AugmentKeyword k) => AugmentFx.KwColor(k);
    static Color CPlunder => KC(AugmentKeyword.Plunder);
    static Color CCombo => KC(AugmentKeyword.Combo);
    static Color CFracture => KC(AugmentKeyword.Fracture);
    static Color CAfter => KC(AugmentKeyword.Afterimage);
    static Color CExec => KC(AugmentKeyword.Execute);
    static Color CStasis => KC(AugmentKeyword.Stasis);
    static Color COverload => KC(AugmentKeyword.Overload);
    static readonly Color HealGreen = new Color(0.45f, 1f, 0.55f, 1f);

    // ── 연결 (AugmentOfferDirector 에서) ──
    Sprite[] icons;
    TMP_FontAsset font;
    bool fxBound;

    // ── 보유 상태 ──
    readonly HashSet<string> owned = new HashSet<string>();      // 고유능력 이름
    readonly Dictionary<string, ItemData> byAbility = new Dictionary<string, ItemData>();
    readonly HashSet<ItemData> seen = new HashSet<ItemData>();
    int[] kw = new int[8];

    // ── 진행 상태 ──
    int combo; float comboTimer; int comboSinceForced;
    bool forcedCrit;
    int fragments;                         // 탈취 데이터
    readonly List<int> fragKinds = new List<int>();   // 데이터 종류 (색 표시용)
    readonly List<float> fragTimes = new List<float>();   // [2026-11-21] 얻은 시각 — 이번 공격 중에 얻은 데이터는 이번 공격에 못 씀
    float nextAttackBonus;                 // 다음 공격 피해 추가 (시력교정 안약 · 원거리 관측 · 불명의 데이터)
    float lastDashTime = -99f;
    float pierceUntil; float pierceRatio; readonly HashSet<Health> pierced = new HashSet<Health>();
    float persuadeReady;
    int lostThisRoom;                      // 과부하 3: 이번 방에서 잃은 체력
    bool emergencyUsed;                    // 급조된 장기: 방마다 한 번
    readonly List<Vector3> pauseMarks = new List<Vector3>();   // 멈춘 칼자국
    readonly List<GameObject> pauseMarkFx = new List<GameObject>();
    int afterimagesAlive;

    PlayerController pc;
    Stats pstats;
    Health phealth;
    SpriteRenderer psprite;

    Stats PStats { get { RefreshPlayer(); return pstats; } }
    bool Paused => PauseManager.Instance != null && PauseManager.Instance.IsPaused;

    // ══════════════ 시작 ══════════════

    public static AugmentRuntime Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AugmentRuntime");
        return go.AddComponent<AugmentRuntime>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { seen.Clear(); boundInv = null; ResetRunState(); };
    }

    void OnEnable()
    {
        CombatEvents.PlayerHit += OnHit;
        CombatEvents.EnemyKilled += OnKill;
        CombatEvents.DashStarted += OnDashStart;
        CombatEvents.DashEnded += OnDashEnd;
        CombatEvents.PauseStarted += OnPauseStart;
        CombatEvents.PauseEnded += OnPauseEnd;
        CombatEvents.RoomCleared += OnRoomCleared;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
        CombatEvents.SwingStarted += OnSwing;
        CombatEvents.PlayerLanded += OnLanded;
        CombatEvents.EnemyAttackStarted += OnEnemyAttack;
        EnemyStatus.OverheatChanged += OnOverheatChanged;
        RoomManager.OnRoomLoaded += OnRoomLoaded;
        CombatEvents.ModifyOutgoing = Outgoing;
        CombatEvents.ModifyIncoming = Incoming;
        CombatEvents.ModifyEnemyIncoming = EnemyIncoming;
        CombatEvents.CritBonus = CritBonus;
        CombatEvents.ConsumeForcedCrit = TakeForcedCrit;
        CombatEvents.FreeCastAvailable = FreeCastAvailable;
        CombatEvents.OnSkillCast = OnSkillCast;
        CombatEvents.DashSpeedHook = DashSpeed;
    }

    void OnDisable()
    {
        CombatEvents.PlayerHit -= OnHit;
        CombatEvents.EnemyKilled -= OnKill;
        CombatEvents.DashStarted -= OnDashStart;
        CombatEvents.DashEnded -= OnDashEnd;
        CombatEvents.PauseStarted -= OnPauseStart;
        CombatEvents.PauseEnded -= OnPauseEnd;
        CombatEvents.RoomCleared -= OnRoomCleared;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
        CombatEvents.SwingStarted -= OnSwing;
        CombatEvents.PlayerLanded -= OnLanded;
        CombatEvents.EnemyAttackStarted -= OnEnemyAttack;
        EnemyStatus.OverheatChanged -= OnOverheatChanged;
        RoomManager.OnRoomLoaded -= OnRoomLoaded;
    }

    void ResetRunState()
    {
        combo = 0; comboTimer = 0; comboSinceForced = 0; forcedCrit = false; fragments = 0; fragKinds.Clear(); fragTimes.Clear(); nextAttackBonus = 0;
        pierceUntil = 0; lostThisRoom = 0; emergencyUsed = false; pauseMarks.Clear(); afterimagesAlive = 0;
        pc = null; psprite = null;
        ResetAdvancedState();
        DestroyHead();
    }

    // 인벤토리 · 연결 대상 찾기 (씬이 바뀌면 다시)
    Inventory boundInv;
    void Bind()
    {
        if (Inventory.Instance != null && boundInv != Inventory.Instance)
        {
            boundInv = Inventory.Instance;
            boundInv.OnInventoryChanged += Refresh;
            Refresh();
        }
        if (AugmentOfferDirector.Instance != null && AugmentOfferDirector.Instance.fxOffsets != null) AugmentFx.Offsets = AugmentOfferDirector.Instance.fxOffsets;
        if (!fxBound || icons == null || font == null)
        {
            var dir = AugmentOfferDirector.Instance;
            if (dir != null)
            {
                if (dir.window != null) icons = dir.window.keywordIcons;
                font = dir.statusFont;
                AugmentFx.ExplosionPrefab = dir.explosionFx;
                AugmentFx.SmokePrefab = dir.smokeFx;
                AugmentFx.MarkPrefab = dir.markFx;
                AugmentFx.ZonePrefab = dir.zoneFx;
                AugmentFx.ShatterPrefab = dir.shatterFx;
                AugmentFx.HealPrefab = dir.healFx;
                AugmentFx.StrikePrefab = dir.strikeFx;
                AugmentFx.SlashPrefab = dir.slashFx;
                AugmentFx.StreakPrefab = dir.streakFx;
                fxBound = true;
            }
        }
    }

    void RefreshPlayer()
    {
        var cur = PlayerController.Instance;
        if (cur != null && cur != pc)
        {
            pc = cur;
            pstats = cur.GetComponent<Stats>();
            phealth = cur.GetComponent<Health>();
            psprite = AugmentFx.PlayerSprite(cur);
            DestroyHead();
        }
    }

    Vector3 PPos => pc != null ? pc.transform.position : Vector3.zero;
    // [2026-11-21] 연출 위치 = 몸 판정(콜라이더) 기준 + 인스펙터 오프셋 (예전: 위치 + 고정 높이 → 대체로 너무 위에 떴음)
    Bounds PBody => pc != null ? AugmentFx.BodyBounds(pc) : new Bounds();
    Vector3 PCenter => PBody.center + (Vector3)AugmentFx.Offsets.playerCenter;
    Vector3 PFeet { get { var b = PBody; return new Vector3(b.center.x, b.min.y, PPos.z) + (Vector3)AugmentFx.Offsets.playerFeet; } }
    Vector3 PHead { get { var b = PBody; return new Vector3(b.center.x, b.max.y, PPos.z) + (Vector3)AugmentFx.Offsets.playerHead; } }
    static Vector3 EC(Component h) => h != null ? AugmentFx.BodyBounds(h).center + (Vector3)AugmentFx.Offsets.enemyCenter : Vector3.zero;
    static Vector3 EFeet(Component h) { if (h == null) return Vector3.zero; var b = AugmentFx.BodyBounds(h); return new Vector3(b.center.x, b.min.y, h.transform.position.z); }
    Vector3 DashFx(Vector3 pos) => pos + (PFeet - PPos) + (Vector3)AugmentFx.Offsets.dash;
    int PFacing => pc != null ? (pc.transform.localScale.x >= 0 ? 1 : -1) : 1;

    // ══════════════ 보유 증강 다시 읽기 ══════════════

    /// <summary>인벤토리가 바뀌거나 강화했을 때. 새로 얻은 증강 · 키워드 단계 변화를 연출로 알린다</summary>
    public void Refresh()
    {
        var before = (int[])kw.Clone();
        owned.Clear(); byAbility.Clear(); kw = new int[8];
        var list = Inventory.Instance != null ? Inventory.Instance.passives : null;
        var gained = new List<ItemData>();
        if (list != null)
            foreach (var d in list)
            {
                if (d == null) continue;
                if (!string.IsNullOrEmpty(d.abilityName)) { owned.Add(d.abilityName); byAbility[d.abilityName] = d; }
                if (d.keywords != null) foreach (var k in d.keywords) kw[(int)k]++;
                if (seen.Add(d)) gained.Add(d);
            }

        // 키워드로 정해지는 값
        CombatEvents.PauseGaugeMultiplier = Kw(AugmentKeyword.Stasis) >= 1 ? 1.1f : 1f;
        CombatEvents.PauseDurationBonus = Kw(AugmentKeyword.Stasis) >= 5 ? 1f : 0f;
        CombatEvents.PauseCritBonus = Kw(AugmentKeyword.Stasis) >= 5 ? 0.2f : 0f;
        CombatEvents.DashExtraInvuln = 0f;   // [2026-11-21] 잔상 1개 효과는 '대시하면 잔상'으로 바뀜
        RefreshAdvancedModifiers();
        while (fragments > FragmentMax) RemoveFragmentAt(fragments - 1);

        // 보이게 (글자 없음): 얻은 증강 = 발밑 파동 + 몸 번쩍, 키워드 3·5개 = 오른쪽 아래 아이콘 맥동 + 키워드 색 파동
        RefreshPlayer();
        foreach (var d in gained) ShowGain(d);
        for (int k = 0; k < 8; k++)
        {
            int tb = AugmentRules.KeywordTier(before[k]), ta = AugmentRules.KeywordTier(kw[k]);
            if (ta > tb && ta >= 2)
            {
                var c = KC((AugmentKeyword)k);
                AugmentFx.HudPop(KeywordIcon((AugmentKeyword)k), c, ta - 1);
                if (pc != null) AugmentFx.Ring(PFeet, c, 3.2f, 0.6f, 0.12f, 2, 0.45f);
            }
        }
        UpdateHead();
    }

    void ShowGain(ItemData d)
    {
        if (pc == null || d == null) return;
        Color c = d.keywords != null && d.keywords.Length > 0 ? KC(d.keywords[0]) : ShopColors.Hex(AugmentRules.AbilityHex);
        AugmentFx.Ring(PFeet, c, 2.2f, 0.5f, 0.1f, 1, 0.45f);
        AugmentFx.BodyFlash(pc, c, 0.7f, 0.35f);
        AugmentFx.Sparks(PCenter, c, 8, 3f, 0.45f, -2f, 0.14f);
    }

    public int Kw(AugmentKeyword k) => kw[(int)k];

    /// <summary>그 고유능력의 단계 (없으면 0)</summary>
    int L(string ability)
    {
        if (!owned.Contains(ability)) return 0;
        var d = byAbility[ability];
        return RunManager.Instance != null ? Mathf.Clamp(RunManager.Instance.GetAugmentLevel(d), 1, 3) : 1;
    }

    bool Has(string ability) => owned.Contains(ability);

    static float ByLevel(int lv, float a, float b, float c) => lv >= 3 ? c : lv == 2 ? b : a;

    // ══════════════ 공용 도우미 ══════════════

    public static EnemyStatus StatusOf(Health h)
    {
        if (h == null) return null;
        var s = h.GetComponent<EnemyStatus>();
        if (s == null) s = h.gameObject.AddComponent<EnemyStatus>();
        return s;
    }

    float ExecuteThreshold => 0.30f + (Kw(AugmentKeyword.Execute) >= 3 ? 0.05f : 0f) + ScaleThresholdBonus();

    bool IsExecuteTarget(Health h)
    {
        if (h == null || h.CompareTag("Player")) return false;
        var st = h.GetComponent<Stats>();
        if (st == null || st.maxHp <= 0) return false;
        return st.currentHp > 0 && st.currentHp <= st.maxHp * ExecuteThreshold;
    }

    /// <summary>상태 띠에 처형 아이콘을 띄울지 (처형 키워드를 가졌고 처형 대상일 때)</summary>
    public bool ShowsExecuteMark(Health h) => Kw(AugmentKeyword.Execute) > 0 && IsExecuteTarget(h);

    static readonly Collider2D[] buf = new Collider2D[48];
    static List<Health> EnemiesIn(Vector3 pos, float radius)
    {
        var list = new List<Health>();
        int n = Physics2D.OverlapCircleNonAlloc(pos, radius, buf, LayerMask.GetMask("Enemy", "BossBody"));
        for (int i = 0; i < n; i++)
        {
            var h = Health.Resolve(buf[i]);
            if (h != null && !h.IsDead && !h.CompareTag("Player") && !list.Contains(h)) list.Add(h);
        }
        return list;
    }

    static List<Health> EnemiesInBox(Vector3 center, Vector2 size)
    {
        var list = new List<Health>();
        int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, buf, LayerMask.GetMask("Enemy", "BossBody"));
        for (int i = 0; i < n; i++)
        {
            var h = Health.Resolve(buf[i]);
            if (h != null && !h.IsDead && !h.CompareTag("Player") && !list.Contains(h)) list.Add(h);
        }
        return list;
    }

    static Health Nearest(Vector3 pos, float radius, Health except = null)
    {
        Health best = null; float bd = radius * radius;
        foreach (var e in EnemiesIn(pos, radius))
        {
            if (e == except) continue;
            float d = (e.transform.position - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    static bool IsBoss(Health h) => h != null && h.GetComponent<BossController>() != null;

    /// <summary>증강 능력의 피해 (방어·치명타·상성 규칙 그대로, 능력 연쇄는 막음)</summary>
    void AbilityHit(Health h, float atkRatio, Vector3 pos, float kbX = 2f, float kbY = 1f)
    {
        if (h == null || PStats == null || atkRatio <= 0f) return;
        int dir = pos.x <= h.transform.position.x ? 1 : -1;
        bool prev = CombatEvents.AbilityDamageInProgress;
        CombatEvents.AbilityDamageInProgress = true;
        try { Hitbox.DealDirectHit(h, Mathf.Max(1, Mathf.RoundToInt(pstats.Atk * atkRatio)), pstats, dir, kbX, kbY, h.transform.position, null, null, "Augment", true); }
        finally { CombatEvents.AbilityDamageInProgress = prev; }
    }

    static void Stagger(Health h)
    {
        var e = h != null ? h.GetComponent<Enemy>() : null;
        if (e != null) e.OnHit(0);   // [단순화] 경직 = 피격 경직 한 번 (경직 시간은 적마다 정해진 값)
    }

    void StunEnemy(Health h, float seconds, Color c)
    {
        var e = h != null ? h.GetComponent<Enemy>() : null;
        if (e == null) return;   // 보스는 기절하지 않음
        e.Stun(seconds);
        AugmentFx.StunStars(h.transform, seconds, c);
    }

    void HealPlayer(int amount, Color c)
    {
        if (pstats == null || amount <= 0) return;
        pstats.Heal(amount);
        FloatingTextSpawner.SpawnText(PHead, $"+{amount}", ShopColors.Up);   // 회복량
        AugmentFx.HealFx(pc != null ? pc.transform : null, c);
    }

    // ══════════════ 머리 위 표시 (탈취 데이터 결정 · 연격 반원 게이지 · 떠올림 조각) ══════════════

    readonly List<SpriteRenderer> crystals = new List<SpriteRenderer>();
    LineRenderer comboArc, comboArcBack, backupArc;
    readonly List<SpriteRenderer> recallShards = new List<SpriteRenderer>();
    Transform headRoot;

    void DestroyHead()
    {
        if (headRoot != null) Destroy(headRoot.gameObject);
        headRoot = null; crystals.Clear(); recallShards.Clear(); comboArc = comboArcBack = backupArc = null;
    }

    void EnsureHead()
    {
        if (headRoot != null || pc == null) return;
        headRoot = new GameObject("AugHead").transform;
        headRoot.SetParent(pc.transform, false);
        comboArcBack = MakeArc("ComboBack", new Color(0f, 0f, 0f, 0.35f), 0.09f);
        comboArc = MakeArc("Combo", CCombo, 0.07f);
        backupArc = MakeArc("Backup", new Color(CCombo.r, CCombo.g, CCombo.b, 0.3f), 0.07f);
    }

    LineRenderer MakeArc(string n, Color c, float w)
    {
        var lr = new GameObject(n).AddComponent<LineRenderer>();
        lr.transform.SetParent(headRoot, false);
        lr.useWorldSpace = true; lr.positionCount = 0; lr.startWidth = lr.endWidth = w;
        lr.startColor = lr.endColor = c; lr.numCapVertices = 2; lr.sortingOrder = AugmentFx.SortFront + 2;
        if (AugmentFx.SpriteMat != null) lr.sharedMaterial = AugmentFx.SpriteMat;
        return lr;
    }

    void UpdateHead() { }   // 매 프레임 LateUpdate 에서 그림

    void LateUpdate()
    {
        if (pc == null) return;
        bool showFrag = FragmentMax > 0 && UsesData();
        bool showCombo = Kw(AugmentKeyword.Combo) > 0;
        bool showRecall = recall > 0;
        if (!showFrag && !showCombo && !showRecall) { if (headRoot != null) headRoot.gameObject.SetActive(false); return; }
        EnsureHead();
        headRoot.gameObject.SetActive(true);
        Vector3 head = PHead;

        // 탈취 데이터: 머리 주위를 도는 초록 결정 (빈 칸은 어두운 테두리만)
        int cmax = showFrag ? FragmentMax : 0;
        while (crystals.Count < cmax)
        {
            var r = new GameObject("Data").AddComponent<SpriteRenderer>();
            r.transform.SetParent(headRoot, false);
            r.sprite = AugmentFx.Diamond; r.sortingOrder = AugmentFx.SortFront + 3;
            if (AugmentFx.SpriteMat != null) r.sharedMaterial = AugmentFx.SpriteMat;
            crystals.Add(r);
        }
        for (int i = 0; i < crystals.Count; i++)
        {
            var r = crystals[i];
            bool on = i < cmax;
            r.gameObject.SetActive(on);
            if (!on) continue;
            float a = Time.time * 1.6f + i * Mathf.PI * 2f / cmax;
            r.transform.position = head + new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.18f, Mathf.Sin(a) < 0 ? -0.6f : 0.3f);
            bool lit = i < fragments;
            int kind = lit && i < fragKinds.Count ? fragKinds[i] : 0;
            Color c = kind == 2 ? new Color(0.3f, 0.95f, 0.95f) : kind == 3 ? new Color(1f, 0.85f, 0.4f) : CPlunder;
            bool fullGlow = fragments >= FragmentMax && SocketReady();
            float pulse = fullGlow ? 1f + 0.25f * Mathf.Sin(Time.time * 9f) : 1f;
            r.color = lit ? c : new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.55f);
            r.transform.localScale = Vector3.one * (lit ? 0.22f : 0.14f) * pulse;
        }

        // 연격: 발밑 반원 게이지 (10에서 금색으로 빛남) + 스택 백업의 반투명 복사본
        Vector3 feet = PFeet;
        if (showCombo)
        {
            DrawArc(comboArcBack, feet, 1f);
            float fill = combo / 10f;
            DrawArc(comboArc, feet, fill);
            Color cc = combo >= 10 ? Color.Lerp(CCombo, Color.white, 0.4f + 0.3f * Mathf.Sin(Time.time * 12f)) : CCombo;
            comboArc.startColor = comboArc.endColor = cc;
            comboArc.startWidth = comboArc.endWidth = combo >= 10 ? 0.1f : 0.07f;
            DrawArc(backupArc, feet, savedCombo > 0 ? savedCombo / 10f : 0f);
        }
        else { comboArc.positionCount = 0; comboArcBack.positionCount = 0; backupArc.positionCount = 0; }

        // 떠올림: 등 뒤 작은 빛 조각 (최대 12, 6개부터 빛남)
        while (recallShards.Count < recall)
        {
            var r = new GameObject("Recall").AddComponent<SpriteRenderer>();
            r.transform.SetParent(headRoot, false);
            r.sprite = AugmentFx.Dot; r.sortingOrder = AugmentFx.SortFront - 5;
            if (AugmentFx.SpriteMat != null) r.sharedMaterial = AugmentFx.SpriteMat;
            recallShards.Add(r);
        }
        for (int i = 0; i < recallShards.Count; i++)
        {
            var r = recallShards[i];
            bool on = i < recall;
            r.gameObject.SetActive(on);
            if (!on) continue;
            float ang = Mathf.Lerp(110f, 250f, recall > 1 ? i / (float)(recall - 1) : 0.5f) * Mathf.Deg2Rad;
            Vector3 back = new Vector3(-PFacing * 0.35f, 0.3f, 0.4f);
            r.transform.position = PCenter + back + new Vector3(Mathf.Cos(ang) * 0.75f * PFacing, Mathf.Sin(ang) * 0.75f, 0f) * -1f;
            bool glow = recall >= 6;
            float p = glow ? 0.8f + 0.2f * Mathf.Sin(Time.time * 4f + i) : 0.55f;
            r.color = new Color(0.85f, 0.9f, 1f, p);
            r.transform.localScale = Vector3.one * (glow ? 0.28f : 0.2f);
        }
    }

    static void DrawArc(LineRenderer lr, Vector3 center, float fill)
    {
        if (lr == null) return;
        fill = Mathf.Clamp01(fill);
        int n = Mathf.Max(0, Mathf.RoundToInt(24 * fill) + (fill > 0 ? 1 : 0));
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Lerp(Mathf.PI, 0f, n > 1 ? i / (float)(n - 1) * fill : 0f);
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * 0.75f, -Mathf.Sin(a) * 0.22f, -0.4f));
        }
    }

    bool UsesData() => Kw(AugmentKeyword.Plunder) > 0 || Has("404") || Has("특이점 조항") || Has("수집가의 불꽃");

    // ══════════════ 매 프레임 ══════════════

    void Update()
    {
        Bind();
        RefreshPlayer();
        bool paused = Paused;

        // 연격: 시간 안에 못 맞히면 끊김 (연격 3개면 절반만 잃음)
        if (combo > 0 && !paused)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) BreakCombo();
        }
        // 연격 카운터 · 연격 5개 · 조준 고정
        float spd = 1f + (L("연격 카운터") > 0 ? combo * ByLevel(L("연격 카운터"), 0.02f, 0.03f, 0.04f) : 0f);
        if (Kw(AugmentKeyword.Combo) >= 5 && combo >= 10) spd += 0.30f;
        CombatEvents.AttackSpeedMultiplier = spd;
        // [2026-11-21 수정] 조준 고정: 연격 5 · 10 에서 평타 판정 · 무기 궤적 · 투사체가 단계적으로 커짐 (눈에 보이게)
        int lvAim = L("조준 고정");
        float aim = 1f;
        if (lvAim > 0 && combo >= 10) aim += ByLevel(lvAim, 0.30f, 0.45f, 0.60f);
        else if (lvAim > 0 && combo >= 5) aim += ByLevel(lvAim, 0.15f, 0.22f, 0.30f);
        if (lvAim > 0 && aim > aimShown + 0.01f && pc != null) { AugmentFx.Ring(PCenter, CCombo, 1.8f, 0.3f, 0.1f, 2); AugmentFx.BodyFlash(pc, CCombo, 0.6f, 0.25f); }
        aimShown = aim;
        CombatEvents.PlayerProjectileScale = aim;
        CombatEvents.SwingScale = 1f;   // [2026-11-21] 무기 궤적은 키우지 않음 (검 1타 · 망치 이펙트가 커지면 안 보이는 문제) → 판정만 AimScale 로
        AimScale = aim;

        // 기억 갈고리: 대시 경로의 적을 꿰뚫음 (초록 관통 섬광)
        if (Time.time < pierceUntil && pc != null)
        {
            foreach (var h in EnemiesIn(PCenter, 1.1f))
                if (pierced.Add(h))
                {
                    AbilityHit(h, pierceRatio, PPos, 1f, 0.5f); Stagger(h);
                    AugmentFx.Sparks(EC(h), CTeal, 6, 4f, 0.25f, 0f, 0.12f);
                }
        }

        // 통신_도발: 탈취 데이터가 가득이면 주변 적이 끌려옴 (적과 헤네시를 잇는 초록 신호선)
        int lvTaunt = L("통신_도발");
        if (lvTaunt > 0 && fragments >= FragmentMax && FragmentMax > 0 && pc != null && !paused)
        {
            float r = ByLevel(lvTaunt, 4f, 5f, 6f);
            tauntPulse -= Time.deltaTime;
            bool pulseNow = tauntPulse <= 0f;
            if (pulseNow) { tauntPulse = 0.9f; AugmentFx.Ripple(PFeet, CPlunder, r, 0.9f, 0.03f, 2, 0.4f); }   // 파문: 밖으로 갈수록 옅고 굵게
            foreach (var h in EnemiesIn(PPos, r))
            {
                if (IsBoss(h)) continue;
                Vector3 to = PPos - h.transform.position;
                if (Mathf.Abs(to.x) < 0.8f) continue;
                h.transform.position += new Vector3(Mathf.Sign(to.x) * 1.2f * Time.deltaTime, 0f, 0f);
            }
        }

        UpdateAdvanced(paused);
    }

    float tauntPulse, aimShown = 1f;
    float AimScale = 1f;

    void BreakCombo()
    {
        int before = combo;
        // 스택 백업: 끊기기 직전 스택 저장
        if (TryBackupCombo(before)) { combo = 0; comboTimer = ComboWindow; return; }
        combo = Kw(AugmentKeyword.Combo) >= 3 ? combo / 2 : 0;
        comboTimer = ComboWindow;
    }

    float ComboWindow => (GauntletRhythm() ? 4f : 2f) + (Kw(AugmentKeyword.Combo) >= 1 ? 0.5f : 0f);
    int FragmentMax => (Kw(AugmentKeyword.Plunder) >= 3 ? 5 : 3) + (Has("보급 우선권") ? 1 : 0);

    void AddCombo(int n, Vector3 from)
    {
        if (n <= 0) return;
        int before = combo;
        combo = Mathf.Min(10, combo + n);
        comboTimer = ComboWindow;
        if (combo >= 10 && before < 10) OnComboFull();
    }

    void AddFragment(int n, Vector3 from, int kind = 0)
    {
        for (int i = 0; i < n; i++)
        {
            if (fragments >= FragmentMax) break;
            fragments++;
            fragKinds.Add(kind);
            fragTimes.Add(Time.time);
            Color c = kind == 2 ? new Color(0.3f, 0.95f, 0.95f) : kind == 3 ? new Color(1f, 0.85f, 0.4f) : CPlunder;
            if (pc != null) AugmentFx.Fly(from, pc.transform, c, 1, 0.45f, null, 0.24f);   // 처치 자리에서 헤네시에게 빨려 듦 (곡선)
        }
    }

    void RemoveFragmentAt(int i)
    {
        if (fragments <= 0) return;
        fragments--;
        if (i >= 0 && i < fragKinds.Count) fragKinds.RemoveAt(i);
        if (i >= 0 && i < fragTimes.Count) fragTimes.RemoveAt(i);
    }

    /// <summary>이 시각 전에 얻은 데이터 수 (이번 공격 중에 얻은 건 빼고 셈)</summary>
    int FragmentsBefore(float t)
    {
        int n = 0;
        for (int i = 0; i < fragments && i < fragTimes.Count; i++) if (fragTimes[i] < t) n++;
        return n;
    }

    /// <summary>탈취 데이터 하나를 쓴다 (강탈 5개: 50% 확률로 남음). before 보다 먼저 얻은 것만. 쓴 데이터 종류를 돌려줌, 없으면 -1</summary>
    int UseFragmentKind(float before = float.MaxValue)
    {
        int idx = -1;
        for (int i = Mathf.Min(fragments, fragTimes.Count) - 1; i >= 0; i--) if (fragTimes[i] < before) { idx = i; break; }
        if (idx < 0) return -1;
        int kind = idx < fragKinds.Count ? fragKinds[idx] : 0;
        bool keep = Kw(AugmentKeyword.Plunder) >= 5 && Random.value < 0.5f;
        if (!keep) RemoveFragmentAt(idx);
        if (pc != null) AugmentFx.Sparks(PHead, CPlunder, 5, 2f, 0.3f, 2f, 0.12f);
        OnFragmentUsed(kind);
        return kind;
    }

    bool UseFragment(float before = float.MaxValue) => UseFragmentKind(before) >= 0;

    /// <summary>이 맞힘을 낸 공격이 시작된 시각 (평타면 판정이 생긴 때, 아니면 0.3초 전)</summary>
    static float AttackStart(CombatEvents.HitInfo hi) => hi.swing != null ? hi.swing.startTime : Time.time - 0.3f;

    // ══════════════ 피해 계산 끼어들기 ══════════════

    int Outgoing(Health target, int dmg, bool crit)
    {
        float mult = 1f;
        var st = target.GetComponent<EnemyStatus>();
        if (st != null && st.fracture > 0)
        {
            mult += 0.05f * st.fracture;
            if (Kw(AugmentKeyword.Fracture) >= 5 && st.IsMaxFracture) mult += 0.20f;
        }
        bool exec = IsExecuteTarget(target);
        if (exec)
        {
            if (Kw(AugmentKeyword.Execute) >= 1) mult += 0.05f;
            int lv = L("처형 프로토콜");
            if (lv > 0) mult += ByLevel(lv, 0.20f, 0.30f, 0.40f);
        }
        if (!CombatEvents.AbilityDamageInProgress) StatusOf(target).lastHitWasExecute = exec;

        float hpRatio = PStats != null && pstats.maxHp > 0 ? (float)pstats.currentHp / pstats.maxHp : 1f;
        int lvFuse = L("과부하 퓨즈");
        if (lvFuse > 0 && hpRatio <= 0.5f) mult += ByLevel(lvFuse, 0.15f, 0.22f, 0.30f);
        if (Kw(AugmentKeyword.Overload) >= 5 && hpRatio <= 0.25f) mult += 0.25f;

        if (nextAttackBonus > 0f && !CombatEvents.AbilityDamageInProgress) { mult += nextAttackBonus; nextAttackBonus = 0f; }
        mult += AdvancedOutgoing(target, crit, exec);
        return Mathf.RoundToInt(dmg * mult);
    }

    int Incoming(int dmg)
    {
        RefreshPlayer();
        if (pstats == null) return dmg;
        float hpRatio = pstats.maxHp > 0 ? (float)pstats.currentHp / pstats.maxHp : 1f;
        if (Kw(AugmentKeyword.Overload) >= 1 && hpRatio <= 0.5f) dmg = Mathf.RoundToInt(dmg * 0.95f);
        return AdvancedIncoming(dmg);
    }

    float CritBonus(Health target)
    {
        float b = 0f;
        int lv = L("흐린 조준");
        if (lv > 0 && IsExecuteTarget(target)) b += ByLevel(lv, 0.10f, 0.15f, 0.20f);
        int lvG = L("전투 시야");
        if (lvG > 0) b += combo * ByLevel(lvG, 0.01f, 0.015f, 0.02f);
        return b;
    }

    bool TakeForcedCrit()
    {
        if (!forcedCrit || CombatEvents.AbilityDamageInProgress) return false;
        forcedCrit = false;
        if (critSpark != null) { Destroy(critSpark); critSpark = null; }
        AugmentFx.Sparks(PCenter + Vector3.right * PFacing * 0.8f, CCombo, 10, 5f, 0.3f, 0f, 0.15f);   // 확정 치명타 순간 터짐
        return true;
    }

    GameObject critSpark;

    // ══════════════ 사건 ══════════════

    void OnHit(CombatEvents.HitInfo hi)
    {
        var st = StatusOf(hi.target);
        if (st == null) return;
        bool paused = Paused;

        if (!hi.fromAbility)
        {
            // 연격
            AddCombo(1 + ComboExtraOnHit(hi), hi.position);
            int lvBrain = L("예측 연산");
            if (lvBrain > 0)
            {
                comboSinceForced++;
                int need = (int)ByLevel(lvBrain, 5, 4, 3);
                if (comboSinceForced >= need && !forcedCrit)
                {
                    comboSinceForced = 0; forcedCrit = true;
                    // 무기 끝 노란 별빛 (치명타가 나갈 때까지 반짝임)
                    if (pc != null && critSpark == null)
                    {
                        critSpark = AugmentFx.Orb(PCenter, CCombo, 0.22f);
                        var f = critSpark.AddComponent<FxFollow>(); f.target = pc.transform; f.offset = (PCenter - PPos) + new Vector3(0.75f * PFacing, 0f, -0.5f);
                    }
                }
            }
            if (critSpark != null) { var f = critSpark.GetComponent<FxFollow>(); if (f != null) f.offset = (PCenter - PPos) + new Vector3(0.75f * PFacing, 0f, -0.5f); }

            // 과열
            if (Kw(AugmentKeyword.Overheat) >= 1 && Random.value < 0.10f) st.AddOverheat(1);
            int lvCoil = L("예열 코일");
            if (lvCoil > 0 && Random.value < ByLevel(lvCoil, 0.35f, 0.52f, 0.70f)) st.AddOverheat(1);   // [2026-11-21] 25 → 35%

            // 균열
            if (Kw(AugmentKeyword.Fracture) >= 1 && hi.isCrit) st.AddFracture(1);
            int lvProbe = L("균열 탐침");
            if (lvProbe > 0 && Time.time - st.lastProbe >= ByLevel(lvProbe, 0.5f, 0.35f, 0.25f)) { st.lastProbe = Time.time; st.AddFracture(1); }
            int lvSleep = L("잠재우기");
            if (lvSleep > 0 && ++st.hitCounter % 3 == 0)
            {
                st.AddFracture(2); Stagger(hi.target);
                StunEnemy(hi.target, ByLevel(lvSleep, 0.4f, 0.6f, 0.8f), CFracture);
                AugmentFx.Ring(EC(hi.target), CFracture, 1.1f, 0.3f, 0.07f);
            }

            // 설득: 탈취 데이터를 실은 공격 → 크게 날리고, 날아간 적이 다른 적과 부딪치면 둘 다 피해
            int lvPersuade = L("설득");
            if (lvPersuade > 0 && Time.time >= persuadeReady && FragmentsBefore(AttackStart(hi)) > 0 && !IsBoss(hi.target) && UseFragment(AttackStart(hi)))   // 이번 공격으로 얻은 데이터는 이번 공격에 안 씀
            {
                persuadeReady = Time.time + 0.35f;
                StartCoroutine(Persuade(hi.target, ByLevel(lvPersuade, 1.0f, 1.5f, 2.0f)));
            }

            // 멈춘 칼자국: 퍼즈 중 맞힌 자리 기록 (최대 6) — 그 자리에 하얀 구체가 멈춰 남음
            if (paused && L("멈춘 칼자국") > 0 && pauseMarks.Count < 6)
            {
                pauseMarks.Add(hi.position);
                pauseMarkFx.Add(AugmentFx.SlashMark(hi.position, new Color(0.35f, 0.6f, 1f), 0.35f, Random.Range(-35f, 35f) + (PFacing < 0 ? 180f : 0f)));   // 작은 푸른 참격이 멈춰 남음
            }

            AdvancedOnHit(hi, st, paused);
        }
        else AdvancedOnAbilityHit(hi, st);
    }

    IEnumerator Persuade(Health target, float ratio)
    {
        if (target == null) yield break;
        var rb = target.GetComponent<Rigidbody2D>();
        float dir = pc != null ? Mathf.Sign(target.transform.position.x - PPos.x) : 1f;
        if (rb != null) rb.velocity = new Vector2(dir * 16f, 5f);
        AugmentFx.BodyFlash(target, CPlunder, 0.8f, 0.6f);
        float end = Time.time + 0.6f;
        Vector3 last = target.transform.position;
        while (Time.time < end && target != null && !target.IsDead)
        {
            // 날아가는 궤적: 초록 빛줄
            Vector3 now = target.transform.position;
            AugmentFx.Line(last + (EC(target) - now), EC(target), CPlunder, 0.25f, 0.12f);
            last = now;
            foreach (var other in EnemiesIn(target.transform.position, 1.1f))
            {
                if (other == target) continue;
                AbilityHit(other, ratio, target.transform.position, 4f, 2f);
                AbilityHit(target, ratio, other.transform.position, 1f, 1f);
                AugmentFx.Burst((EC(target) + EC(other)) * 0.5f, CPlunder, 0.6f);
                yield break;
            }
            yield return null;
        }
    }

    void OnKill(Health h, Vector3 pos)
    {
        if (h == null || h.CompareTag("Player")) return;
        var st = h.GetComponent<EnemyStatus>();
        bool overheated = st != null && st.overheat > 0;
        bool maxHeat = st != null && st.IsHot;   // 과열 5스택 이상
        bool executed = st != null && st.lastHitWasExecute;

        // 과열 5개: 최대 스택인 적이 쓰러지면 폭발
        if ((Kw(AugmentKeyword.Overheat) >= 5 || Has("붕괴 탄두")) && maxHeat) Explode(EFeet(h), 2f, 1.0f);   // [2026-11-21] 혈청 주입기도 이 폭발을 일으킴

        // 남은 불씨: 과열된 적 처치 → 가장 가까운 적에게 불씨가 튐 (주황 빛줄)
        int lvEmber = L("남은 불씨");
        if (lvEmber > 0 && overheated)
        {
            var best = Nearest(pos, 6f, h);
            if (best != null)
            {
                var target = best;
                // 불씨가 곡선으로 날아가 맞은 적에 과열
                AugmentFx.Fly(EC(h), best.transform, OverheatColor, 1, 0.4f, () =>
                {
                    if (target != null && !target.IsDead) { StatusOf(target).AddOverheat((int)ByLevel(lvEmber, 2, 3, 4)); AugmentFx.Sparks(EC(target), OverheatColor, 8, 3f, 0.35f); }
                }, 0.24f, false);
            }
        }

        // 처형
        if (executed)
        {
            if (Kw(AugmentKeyword.Execute) >= 5) PauseManager.Instance?.AddGauge(10f);
            int lvNext = L("다음 표적");
            if (lvNext > 0 && pstats != null)
            {
                nextAttackBonus = Mathf.Max(nextAttackBonus, ByLevel(lvNext, 0.30f, 0.45f, 0.60f));
                StartCoroutine(SpeedBuff(2f, 1.2f, CExec));
                BuffIcon("다음 표적", 2f);
            }
        }

        // 탈취 데이터
        // [2026-11-21] 강탈 키워드가 있으면 처치마다 1개 (예전 20% 확률로는 거의 안 쌓였음), 기억 갈고리는 1개 더
        int got = 0;
        if (Kw(AugmentKeyword.Plunder) >= 1) got++;
        if (L("기억 갈고리") > 0) got++;
        if (got > 0) AddFragment(got, EC(h), 0);

        AdvancedOnKill(h, pos, st, overheated, maxHeat, executed);
    }

    /// <summary>이동 속도 버프 — 그동안 헤네시 몸이 색으로 맥동</summary>
    IEnumerator SpeedBuff(float seconds, float mult, Color c)
    {
        var s = pstats;
        if (s == null) yield break;
        object key = new object();
        s.AddSpeedModifier(key, mult);
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            if (pc != null) AugmentFx.BodyFlash(pc, c, 0.45f, 0.2f);
            yield return new WaitForSeconds(0.25f);
        }
        if (s != null) s.RemoveSpeedModifier(key);
    }

    /// <summary>과열 폭발 (반경 · 배율). 붕괴 탄두 · 수집가의 불꽃 반영</summary>
    void Explode(Vector3 pos, float radius, float ratio)
    {
        radius = ExplosionRadius(radius);
        bool serum = Has("붕괴 탄두");
        Vector3 mid = pos + Vector3.up * 0.6f;   // pos = 발밑
        AugmentFx.Burst(mid, OverheatColor, radius * 0.6f, 2f);
        AugmentFx.Ring(pos + Vector3.up * 0.05f, OverheatColor, radius, 0.4f, 0.12f, 1, 0.45f);
        if (serum) { AugmentFx.Ring(mid, CFracture, radius * 0.8f, 0.45f, 0.06f, 2); AugmentFx.Sparks(mid, CFracture, 10, 4f, 0.4f); }
        foreach (var e in EnemiesIn(mid, radius))
        {
            var es = StatusOf(e);
            if (Time.time < es.explodeImmuneUntil) continue;
            es.explodeImmuneUntil = Time.time + 1.5f;   // 폭발 연쇄 방지
            AbilityHit(e, ratio, pos, 4f, 2f);
            if (serum) es.SetFractureFull();
        }
        OnOverheatExplosion(pos);
    }

    void OnDashStart(Vector3 pos, float dir)
    {
        float extra = Kw(AugmentKeyword.Afterimage) >= 3 ? 1f : 0f;
        int lvCloak = L("잔상 발생기");
        float ratio = lvCloak > 0 ? ByLevel(lvCloak, 0.6f, 0.9f, 1.2f) : 0.6f;
        // [2026-11-21] 잔상 키워드가 있으면 대시 출발 자리에 기본 잔상 (1초 · 공격력 40%) — 잔상 증강이 스텔스 우의 없이도 돌아감
        //  스텔스 우의가 있으면 그 잔상이 강해짐 (1.5초 · 60~120%, 붉은색 + 나타날 때 붉은 폭발)
        if (lvCloak > 0) SpawnAfterimage(pos, 1.5f + extra, ratio, CloakRed, true);
        else if (Kw(AugmentKeyword.Afterimage) >= 1) SpawnAfterimage(pos, 1f + extra, 0.4f, CAfter, false);
        lastDashTime = Time.time;

        // 가스 분사: 대시하는 자리에 작은 붉은 폭발
        if (L("가스 분사") > 0) AugmentFx.Burst(DashFx(pos), GasColor * 1.8f, 0.35f, 1.2f);

        // 기억 갈고리: 데이터 하나를 써서 대시 경로를 꿰뚫음 — 청록 잔상이 빠르게 지워지며 이어붙는 대시 잔상
        int lvHook = L("기억 갈고리");
        if (lvHook > 0 && UseFragment())
        {
            pierceUntil = Time.time + 0.3f;
            pierceRatio = ByLevel(lvHook, 0.10f, 0.30f, 0.50f);
            pierced.Clear();
            AugmentFx.GhostTrail(psprite, CTeal, 0.3f, 0.025f, 0.2f, 0.7f);
        }

        AdvancedOnDashStart(pos, dir);
    }

    static readonly Color CTeal = new Color(0.2f, 0.95f, 0.85f, 1f);
    static readonly Color CloakRed = new Color(1f, 0.22f, 0.2f, 1f);

    void OnDashEnd(Vector3 pos)
    {
        int lvGas = L("가스 분사");
        if (lvGas > 0) StartCoroutine(GasCloud(pos, ByLevel(lvGas, 2f, 3f, 4f)));
        AdvancedOnDashEnd(pos);
    }

    // 가스: 어두운 붉은색, 가로로 넓게 (판정도 가로 4.2m × 세로 1.8m)
    static readonly Color GasColor = new Color(0.42f, 0.05f, 0.04f, 1f);
    const float GasWidth = 4.2f, GasHeight = 1.8f;

    IEnumerator GasCloud(Vector3 pos, float seconds)
    {
        Vector3 at = pos + (PFeet - PPos);
        var fx = AugmentFx.Smoke(at + Vector3.up * 0.3f, GasColor, 0.8f, seconds + 1f);
        if (fx != null)
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var sh = ps.shape;
                sh.scale = new Vector3(sh.scale.x * 2.8f, sh.scale.y * 0.8f, sh.scale.z);
                var em = ps.emission;
                em.rateOverTimeMultiplier *= 1.3f;   // [2026-11-21] 입자 양 130%
                var main = ps.main;
                var c = main.startColor.color; main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, c.a * 0.45f));   // 더 옅게
            }
        // [2026-11-21] 위험 표시 상자 없앰
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            foreach (var e in EnemiesInBox(at + Vector3.up * 0.6f, new Vector2(GasWidth, GasHeight))) StatusOf(e).AddOverheat(1);
            yield return new WaitForSeconds(0.5f);
        }
        if (fx != null) foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ── 잔상 (PauseAfterimage 셰이더로 헤네시 그림 복사) ──
    void SpawnAfterimage(Vector3 pos, float life, float ratio, Color? color = null, bool burst = false)
    {
        if (afterimagesAlive >= 6) return;
        StartCoroutine(Afterimage(pos, life, ratio * AfterimageDamageMul, true, color ?? CAfter, burst));
    }

    /// <summary>추진 부츠: 잔상 피해 +20% (Lv2 +30%, Lv3 +40%)</summary>
    float AfterimageDamageMul => 1f + (L("이단 분사") > 0 ? ByLevel(L("이단 분사"), 0.20f, 0.30f, 0.40f) : 0f);

    IEnumerator Afterimage(Vector3 pos, float life, float ratio, bool canSplit, Color color, bool burst)
    {
        afterimagesAlive++;
        var ghost = AugmentFx.Ghost(psprite, color, life, 0.75f, false, psprite != null ? (Vector3?)(psprite.transform.position + (pos - PPos)) : null);
        Vector3 center = pos + (PCenter - PPos);
        if (burst) AugmentFx.Burst(center, color, 0.55f, 1.2f);   // 스텔스 우의: 잔상이 나타날 때 붉은 폭발 (홀로그램 생성 연출)
        var hitSet = new HashSet<Health>();
        float end = Time.time + life;
        while (Time.time < end)
        {
            foreach (var e in EnemiesIn(center, 0.9f))
            {
                if (!hitSet.Add(e)) continue;
                AbilityHit(e, ratio, pos, 2f, 1f);
                AugmentFx.Sparks(EC(e), color, 6, 3.5f, 0.25f, 0f, 0.13f);
                OnAfterimageHit(e, ghost != null ? ghost.transform : null);
                // 잔상 5개: 잔상이 적을 맞히면 잔상 하나 더 (동시에 최대 3)
                if (canSplit && Kw(AugmentKeyword.Afterimage) >= 5 && afterimagesAlive < 3)
                    StartCoroutine(Afterimage(EFeet(e) + (PPos - PFeet) + new Vector3(Random.Range(-1f, 1f), 0f, 0f), 1.5f, ratio, false, color, burst));
            }
            yield return new WaitForSeconds(0.1f);
        }
        OnAfterimageEnd(center);
        afterimagesAlive--;
    }

    // ── 퍼즈 ──
    void OnPauseStart()
    {
        pauseMarks.Clear();
        foreach (var m in pauseMarkFx) if (m != null) Destroy(m);
        pauseMarkFx.Clear();
        AdvancedOnPauseStart();
    }

    void OnPauseEnd()
    {
        // 멈춘 칼자국: 풀리는 순간 한꺼번에 터짐
        int lvSheath = L("멈춘 칼자국");
        if (lvSheath > 0 && pauseMarks.Count > 0)
        {
            float ratio = ByLevel(lvSheath, 0.5f, 0.75f, 1.0f);
            foreach (var p in pauseMarks)
            {
                AugmentFx.Burst(p, new Color(0.45f, 0.7f, 1f), 0.5f);
                foreach (var e in EnemiesIn(p, 1.0f)) AbilityHit(e, ratio, p, 2f, 1f);
            }
            pauseMarks.Clear();
            foreach (var m in pauseMarkFx) if (m != null) Destroy(m);
            pauseMarkFx.Clear();
        }

        // 정지 3개: 화면 안 일반 적 경직 (파란 파동)
        if (Kw(AugmentKeyword.Stasis) >= 3 && Camera.main != null)
        {
            if (pc != null) AugmentFx.Ring(PCenter, CStasis, 9f, 0.5f, 0.1f);
            foreach (var e in FindObjectsOfType<Enemy>())
            {
                Vector3 vp = Camera.main.WorldToViewportPoint(e.transform.position);
                if (vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1) { e.OnHit(0); e.TintFlash(CStasis, 0.6f, 0.3f); }
            }
        }
        AdvancedOnPauseEnd();
    }

    // ── 방 ──
    void OnRoomLoaded(Room room, bool isStart)
    {
        lostThisRoom = 0;
        emergencyUsed = false;
        combo = 0;
        AdvancedOnRoomLoaded(room);
    }

    void OnRoomCleared(Room room)
    {
        RefreshPlayer();
        if (pstats == null) return;
        int heal = 0;
        int lvCan = L("비상식량");
        if (lvCan > 0)
        {
            bool low = pstats.currentHp <= pstats.maxHp * 0.5f;
            heal += Mathf.RoundToInt(pstats.maxHp * (low ? ByLevel(lvCan, 0.15f, 0.22f, 0.30f) : 0.05f));
        }
        if (Kw(AugmentKeyword.Overload) >= 3) heal += Mathf.RoundToInt(lostThisRoom * 0.2f);
        if (heal > 0) HealPlayer(heal, HealGreen);
        lostThisRoom = 0;
        AdvancedOnRoomCleared(room);
    }

    void OnPlayerDamaged(int dmg)
    {
        lostThisRoom += dmg;
        RefreshPlayer();
        int lvOrgan = L("비상 이식");
        if (lvOrgan > 0 && !emergencyUsed && pstats != null && pstats.currentHp > 0 && pstats.currentHp < pstats.maxHp * 0.3f)
        {
            emergencyUsed = true;
            StartCoroutine(Regen(5f, ByLevel(lvOrgan, 0.02f, 0.03f, 0.04f)));
            BuffIcon("비상 이식", 5f);
            AugmentFx.ScreenPulse(0.25f, 0.2f);
            if (pc != null) AugmentFx.Ring(PCenter, COverload, 2f, 0.4f, 0.1f, 2);
        }
        AdvancedOnPlayerDamaged(dmg);
    }

    IEnumerator Regen(float seconds, float perSecond)
    {
        for (int i = 0; i < Mathf.RoundToInt(seconds); i++)
        {
            yield return new WaitForSeconds(1f);
            if (pstats == null || pstats.currentHp <= 0) yield break;
            if (pc != null) AugmentFx.BodyFlash(pc, COverload, 0.5f, 0.3f);
            HealPlayer(Mathf.Max(1, Mathf.RoundToInt(pstats.maxHp * perSecond)), COverload);
        }
    }
}
