using UnityEngine;

/// <summary>
/// 퍼즈 시스템 관리
/// Q키 → 세상이 천천히 멈춤 / 플레이어만 정상 이동
/// 지속시간 = 플레이어 PauseDuration(초), 최대 maxPauseTime (이동거리 제한은 삭제됨 — MaxDist/RemainingDist 값만 남아 있음)
/// </summary>
public class PauseManager : PersistentSingleton<PauseManager>
{
    public static KeyCode PauseKey => KeyBindings.Get(KeyAction.Pause);   // [2026-11-11] 설정에서 바꿀 수 있음 (기본 D)

    [Header("제로포인트 설정")]
    [Tooltip("적·투사체 속도 배율 (0.1 = 10% 속도)")]
    public float slowTimeScale = 0.1f;

    // [2026-11-16 수정] 집중력 → 퍼즈 지속시간. 능력치 숫자가 곧 초 단위라 배율(timePerFocus) 삭제
    // [2026-11-17 수정] 상한 12초 → 7초. 퍼즈 아이템을 전부 모아도 한 번 사용은 7초를 넘지 않는다 (기본 5초 + 아이템 최대 2초)
    [Tooltip("아이템으로 제로포인트 지속시간이 쌓여도 넘지 않는 최대 시간 (초)")]
    public float maxPauseTime = 7f;
    [Tooltip("[2026-11-25] 제로포인트를 켠 뒤 이 시간(초, 실제 시간) 동안은 키를 다시 눌러도 꺼지지 않음 — 연타로 바로 꺼지는 실수 방지")]
    public float toggleLockSeconds = 1f;

    [Tooltip("Spd 1당 이동 가능 거리")]
    public float distPerSpd = 2.0f;

    [Header("제로포인트 중 보너스")]
    [Tooltip("제로포인트 중 플레이어 공격의 치명타 확률 (0.2 = 20%). 원래 확률이 더 높으면 원래 값 유지")]
    [Range(0f, 1f)] public float pauseCritChance = 0.2f;

    [Header("[2026-11-25] 소리 (볼륨 × 설정 창 효과음 크기)")]
    [Tooltip("제로포인트 발동 순간")]
    public AudioClip startClip;
    [Range(0f, 1f)] public float startVolume = 0.6f;
    [Tooltip("제로포인트 중 반복 재생 (시계 초침)")]
    public AudioClip loopClip;
    [Range(0f, 1f)] public float loopVolume = 0.7f;
    [Tooltip("제로포인트 풀릴 때")]
    public AudioClip endClip;
    [Range(0f, 1f)] public float endVolume = 0.3f;

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

    // [2026-11-25] 제로포인트 소리 — 반복용 · 한 번용 소리 출력기 (2D, 시간 배율 영향 없음)
    AudioSource loopSource, oneShotSource;

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f;
        return s;
    }

    void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (oneShotSource == null) oneShotSource = MakeSource(false);
        oneShotSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        SfxBus.Duck(0.5f);   // [2026-11-25] 1등급 — 나머지 효과음을 잠깐 낮춤
    }

    // 제로포인트 중에만 반복 소리 (어떤 경로로 풀려도 여기서 멈춤)
    void SyncLoopSound()
    {
        bool want = IsPaused && loopClip != null && Time.timeScale > 0f;   // 창(ESC 메뉴 등)이 시간을 멈추면 초침도 멈춤
        if (want)
        {
            if (loopSource == null) loopSource = MakeSource(true);
            loopSource.volume = loopVolume * GameSettings.SfxVolume;
            if (!loopSource.isPlaying) { loopSource.clip = loopClip; loopSource.Play(); }
        }
        else if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
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

    /// <summary>[2026-11-25] 연출 장면(컷신 · NPC 대화) 중인지 — 이 동안 제로포인트를 쓸 수 없고, 화면 위아래에 검은 띠가 깔림</summary>
    public static bool InCinematic => CutsceneSequencer.IsAnyPlaying || NPC.AnyTalking;

    void Update()
    {
        // [2026-11-25] 연출 장면: 검은 띠 페이드 · 제로포인트 정지 (창이 시간을 멈춘 동안에도 띠는 갱신)
        bool cinematic = InCinematic;
        CutsceneOverlay.SetCinematic(cinematic);
        if (cinematic)
        {
            if (IsPaused) DeactivatePause();   // 켜진 채로 연출이 시작되면 끔 (보스 등장 연출이 멈춰 버리지 않게)
            return;
        }

        // [2026-10-10 추가] 창(ESC 메뉴·인벤토리·노드맵·게임오버)이 시간을 멈춘 동안에는 Q 입력도 받지 않고
        // 퍼즈 남은 시간도 줄지 않는다 (예전엔 실제 시간 기준이라 메뉴를 띄워둔 동안 퍼즈가 다 소모됐음)
        if (Time.timeScale == 0f) { SyncLoopSound(); return; }   // [2026-11-25] 창이 떠 있는 동안 초침 소리도 멈춤

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
            {
                ActivatePause();
                // [2026-11-25] 끄는 방법 안내는 튜토리얼에서만 (본 게임에서는 매번 뜨지 않게)
                if (IsPaused && FindObjectOfType<TutorialController>() != null)
                    ScreenNotice.Show($"[{KeyBindings.KeyName(KeyAction.Pause)}] 다시 누르면 제로포인트 종료");
            }
            else if (IsPaused && elapsedRealTime >= toggleLockSeconds)   // [2026-11-25] 켠 직후 잠깐은 다시 눌러도 안 꺼짐
                DeactivatePause();
        }

        SyncLoopSound();   // [2026-11-25]
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
        PlayOneShot(startClip, startVolume);   // [2026-11-25]
        SyncLoopSound();

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
        if (was) PlayOneShot(endClip, endVolume);   // [2026-11-25]
        SyncLoopSound();

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
