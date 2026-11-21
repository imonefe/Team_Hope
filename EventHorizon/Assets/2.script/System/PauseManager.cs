using UnityEngine;

/// <summary>
/// 퍼즈 시스템 관리
/// Q키 → 세상이 천천히 멈춤 / 플레이어만 정상 이동
/// 지속시간 = 플레이어 PauseDuration(초), 최대 maxPauseTime (이동거리 제한은 삭제됨 — MaxDist/RemainingDist 값만 남아 있음)
/// </summary>
public class PauseManager : PersistentSingleton<PauseManager>
{
    public static KeyCode PauseKey => KeyBindings.Get(KeyAction.Pause);   // [2026-11-11] 설정에서 바꿀 수 있음 (기본 D)

    [Header("퍼즈 설정")]
    [Tooltip("적·투사체 속도 배율 (0.1 = 10% 속도)")]
    public float slowTimeScale = 0.1f;

    // [2026-11-16 수정] 집중력 → 퍼즈 지속시간. 능력치 숫자가 곧 초 단위라 배율(timePerFocus) 삭제
    // [2026-11-17 수정] 상한 12초 → 7초. 퍼즈 아이템을 전부 모아도 한 번 사용은 7초를 넘지 않는다 (기본 5초 + 아이템 최대 2초)
    [Tooltip("아이템으로 퍼즈 지속시간이 쌓여도 넘지 않는 최대 시간 (초)")]
    public float maxPauseTime = 7f;

    [Tooltip("Spd 1당 이동 가능 거리")]
    public float distPerSpd = 2.0f;

    [Header("퍼즈 중 보너스")]
    [Tooltip("퍼즈 중 플레이어 공격의 치명타 확률 (0.2 = 20%). 원래 확률이 더 높으면 원래 값 유지")]
    [Range(0f, 1f)] public float pauseCritChance = 0.2f;

    [Header("게이지 설정")]
    [Tooltip("데미지를 줄 때마다 충전되는 양")]
    public float gaugePerHit = 20f;

    // ── 상태 ──────────────────────────────────────────────
    public bool IsPaused { get; private set; }
    public float GaugeValue { get; private set; }
    public float RemainingTime { get; private set; }
    public float RemainingDist { get; private set; }
    public Vector3 PauseStartPos { get; private set; }
    public float MaxDist { get; private set; }
    public float MaxTime { get; private set; }

    // ── 내부 변수 ─────────────────────────────────────────
    Stats playerStats;
    Transform playerTransform;
    Vector3 pauseStartPos;
    float maxTime;
    float maxDist;
    float elapsedRealTime;

    void Awake()
    {
        InitSingleton();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        RoomManager.OnRoomLoaded += OnRoomLoaded;   // [2026-10-20] 다음 방으로 넘어가면 퍼즈 강제 해제
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        RoomManager.OnRoomLoaded -= OnRoomLoaded;
    }

    // [2026-10-20 추가] 퍼즈를 켠 채로 포탈을 타서 새 방이 열리면 바로 끈다
    void OnRoomLoaded(Room room, bool isStartRoom)
    {
        if (IsPaused) DeactivatePause();
    }

    void Start()
    {
        SubscribeToCharacterManager();
    }

    // 씬이 재로드될 때마다(RestartRun 등으로 CharacterManager가 새로 생성될 때마다) 다시 구독해야 한다.
    // 안 그러면 최초 재시작 이후로 Q키 퍼즈 게이지 기능이 영구히 먹통이 된다.
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        SubscribeToCharacterManager();
    }

    void SubscribeToCharacterManager()
    {
        if (CharacterManager.Instance == null) return;
        RefreshPlayerRef();
        CharacterManager.Instance.OnCharacterChanged += _ => RefreshPlayerRef();
    }

    void RefreshPlayerRef()
    {
        var p = CharacterManager.Instance.CurrentCharacter;
        if (p == null) return;
        playerStats = p.GetComponent<Stats>();
        playerTransform = p.transform;
    }

    void Update()
    {
        // [2026-10-10 추가] 창(ESC 메뉴·인벤토리·노드맵·게임오버)이 시간을 멈춘 동안에는 Q 입력도 받지 않고
        // 퍼즈 남은 시간도 줄지 않는다 (예전엔 실제 시간 기준이라 메뉴를 띄워둔 동안 퍼즈가 다 소모됐음)
        if (Time.timeScale == 0f) return;

        // 테스트용 Shift+Q → 게이지 즉시 충전 (Q키 처리보다 먼저 해야 같은 프레임에 바로 발동된다)
        // [2026-11-11] 대시(Shift)+평타(Q)와 겹쳐서 실수로 충전되므로 에디터에서만, Ctrl+D 로 변경
#if UNITY_EDITOR
        if (Input.GetKeyDown(PauseKey) && Input.GetKey(KeyCode.LeftControl))
        {
            GaugeValue = 100f;
        }
#endif

        // Q키 입력
        // 대화 등으로 조작이 잠겨 있으면 새로 발동하지 않음 (해제는 허용)
        bool locked = PlayerController.Instance != null && PlayerController.Instance.IsControlLocked;

        if (KeyBindings.Down(KeyAction.Pause))
        {
            if (!IsPaused && GaugeValue >= 100f && !locked)
                ActivatePause();
            else if (IsPaused)
                DeactivatePause();
        }

        if (!IsPaused) return;

        // 실제 시간 기준으로 경과 체크
        elapsedRealTime += Time.unscaledDeltaTime;
        RemainingTime = Mathf.Max(0f, maxTime - elapsedRealTime);

        // 이동 거리 제한은 삭제됨 (퍼즈 중 자유롭게 이동 가능)

        // 시간 소진 시 해제
        if (RemainingTime <= 0f)
        {
            DeactivatePause();
        }
    }

    // ── 퍼즈 발동 ─────────────────────────────────────────
    void ActivatePause()
    {
        if (playerStats == null)
        {
            Debug.LogWarning("[Pause] 발동 실패: playerStats 가 null 입니다 (CharacterManager의 현재 캐릭터에 Stats 없음)");
            return;
        }

        IsPaused = true;
        GaugeValue = 0f;
        CombatEvents.RaisePauseStart();   // [2026-11-21] 증강

        maxTime = Mathf.Clamp(playerStats.PauseDuration + CombatEvents.PauseDurationBonus, 0f, maxPauseTime + CombatEvents.PauseDurationBonus);   // [2026-11-21] 증강 (정지)
        maxDist = playerStats.spd * distPerSpd;
        elapsedRealTime = 0f;
        RemainingTime = maxTime;
        RemainingDist = maxDist;
        pauseStartPos = playerTransform.position;
        PauseStartPos = pauseStartPos;
        MaxDist = maxDist;
        MaxTime = maxTime;

        Debug.Log($"[Pause] 발동 (지속시간 {maxTime:F1}초, 이동가능거리 {maxDist:F1})");
    }

    // ── 퍼즈 해제 ─────────────────────────────────────────
    public void DeactivatePause()
    {
        bool was = IsPaused;
        IsPaused = false;
        if (was) CombatEvents.RaisePauseEnd();   // [2026-11-21] 증강

        Debug.Log("[Pause] 해제");
    }

    /// <summary>[2026-10-10 추가] 새 판 시작 시 퍼즈 상태·게이지 초기화 (RunManager.RestartRun에서 호출)</summary>
    public void ResetForNewRun()
    {
        IsPaused = false;
        GaugeValue = 0f;
        elapsedRealTime = 0f;
        RemainingTime = 0f;
    }

    // ── 적·투사체는 각자 PauseManager.IsPaused 체크로 처리 ───

    /// <summary>[2026-11-21] 증강 '느린 정리': 퍼즈 중 남은 시간을 늘림</summary>
    public void ExtendPause(float seconds)
    {
        if (!IsPaused || seconds <= 0f) return;
        maxTime += seconds;
        MaxTime = maxTime;
    }

    // ── 게이지 충전 (Hitbox 에서 호출) ───────────────────
    public void AddGauge(float amount)
    {
        if (IsPaused) return;
        if (amount > 0f) amount *= CombatEvents.PauseGaugeMultiplier;   // [2026-11-21] 증강 (정지)
        GaugeValue = Mathf.Min(100f, GaugeValue + amount);
    }


}
