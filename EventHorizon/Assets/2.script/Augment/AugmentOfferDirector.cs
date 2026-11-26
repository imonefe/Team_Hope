using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 증강을 언제 줄지 정하고 고르기 창(AugmentOfferWindow)을 연다.
/// 씬의 AugmentOffer 오브젝트(켜져 있음)에 붙고, 자식인 AugmentOfferCanvas(꺼져 있음)를 window 칸에 연결한다.
///
///  - 전투방에 처음 들어설 때: 몬스터 생성기가 있는 방 (시작방 · 보스방 · 중간보스 방 · 튜토리얼 방 제외).
///    같은 방에 다시 돌아와도(중간보스 다녀오기) 또 주지 않는다.
///  - 중간보스 · 보스 처치: BossController 가 RequestBossOffer() 를 부른다.
///  - 다른 창 · 컷신 · 대화가 끝날 때까지 기다렸다가 연다. 여러 번 겹치면 차례대로.
/// </summary>
public class AugmentOfferDirector : MonoBehaviour
{
    public static AugmentOfferDirector Instance { get; private set; }

    public AugmentOfferWindow window;

    [Header("증강 능력 표시 (AugmentRuntime 이 가져다 씀)")]
    [Tooltip("적·헤네시 머리 위 숫자 글꼴")]
    public TMPro.TMP_FontAsset statusFont;
    [Tooltip("[2026-11-25] 증강 터렛 (조준 터렛 · 정리 터렛)이 소환하는 스킬 프리팹 — Skill02 (점사형 자동 터렛)")]
    public Skill turretSkill;
    [Tooltip("폭발 이펙트 (과열 폭발 · 멈춘 칼자국 · 설득 충돌). 재질 색을 바꿔 씀")]
    public GameObject explosionFx;
    [Tooltip("연기 이펙트 (가스 구름). 입자 색을 바꿔 씀")]
    public GameObject smokeFx;
    [Header("증강 연출 부품 (기획서 '연출 기획')")]
    [Tooltip("대상 표식 — FX_BossSnipeMark")] public GameObject markFx;
    [Tooltip("바닥 범위 — FX_WarningZone")] public GameObject zoneFx;
    [Tooltip("깨짐 — FX_ShieldShatter")] public GameObject shatterFx;
    [Tooltip("회복 — healing")] public GameObject healFx;
    [Tooltip("주먹·착지 충격 — FX_Strike01")] public GameObject strikeFx;
    [Tooltip("작은 참격 자국 — sword_skill_1step1 (난무 참격)")] public GameObject slashFx;
    [Tooltip("빛줄기 — FX_Archer_Streak (궁수 횡·종 일격)")] public GameObject streakFx;

    [Header("[2026-11-21] 연출 위치 조정 — 몸 판정(콜라이더) 기준에 더하는 값. 플레이 중에 바꿔도 바로 반영")]
    public AugmentFxOffsets fxOffsets = new AugmentFxOffsets();
    [Tooltip("방에 들어선 뒤 창을 열기까지 (화면 전환이 끝나도록, 실제 시간 초)")]
    public float roomOfferDelay = 0.8f;
    [Tooltip("[2026-11-25] 보스 처치 슬로우모션이 끝난 뒤 창을 열기까지 (실제 시간 초)")]
    public float bossOfferDelay = 1.0f;
    [Tooltip("끄면 전투방 입장 때 주지 않음 (보스 보상만)")]
    public bool offerOnCombatRoom = true;

    [Header("증강 시험 방 (2026-11-21, 에디터·개발 빌드에서만)")]
    [Tooltip("이 키를 누르면 증강 시험 방으로 이동")]
    public KeyCode testRoomKey = KeyCode.F8;
    public GameObject augmentTestRoom;

    enum Req { Room, Boss, Stat }   // [2026-11-26] Stat = 스탯 방 (문 선택에서 고른 보상)
    readonly Queue<Req> pending = new Queue<Req>();
    readonly HashSet<GameObject> offeredRooms = new HashSet<GameObject>();
    Coroutine runner;

    void Awake()
    {
        Instance = this;
        if (window == null) window = GetComponentInChildren<AugmentOfferWindow>(true);   // 연결이 끊겨도 자식에서 찾음
    }

    void OnEnable()
    {
        RoomManager.OnRoomLoaded += HandleRoomLoaded;
        if (window != null) window.OnFinished += HandleFinished;
    }

    void OnDisable()
    {
        RoomManager.OnRoomLoaded -= HandleRoomLoaded;
        if (window != null) window.OnFinished -= HandleFinished;
        if (Instance == this) Instance = null;
    }

    /// <summary>[2026-11-21] 고르기 창이 열려 있거나 열릴 차례가 남아 있으면 true —
    /// 중간보스 방 복귀 연출(RoomManager) · 포탈(Portal)이 이게 풀릴 때까지 기다린다</summary>
    public static bool IsBusy => Instance != null && (Instance.runner != null || (Instance.window != null && Instance.window.IsOpen));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        if (augmentTestRoom != null && Input.GetKeyDown(testRoomKey) && !GamePause.IsPaused && RoomManager.Instance != null)
            RoomManager.Instance.LoadRoom(augmentTestRoom);
    }
#endif

    /// <summary>[2026-11-21] 튜토리얼 증강 체험 구간에서 (전투방 입장과 같은 확률)</summary>
    public static void RequestTutorialOffer()
    {
        if (Instance != null) Instance.Enqueue(Req.Room);
    }

    /// <summary>중간보스 · 보스를 쓰러뜨렸을 때 (BossController)</summary>
    public static void RequestBossOffer()
    {
        if (Instance != null) Instance.Enqueue(Req.Boss);
    }

    void HandleRoomLoaded(Room room, bool isStart)
    {
        var reward = RoomReward.Take();   // [2026-11-26] 문 선택에서 고른 보상 (어느 방이든 한 번 읽으면 사이버웨어로 돌아감)
        if (!offerOnCombatRoom || isStart || room == null) return;
        if (room.isBossRoom) return;
        if (room.GetComponent<MidBossRoomRoster>() != null) return;
        if (room.GetComponentInChildren<TutorialController>(true) != null) return;
        if (room.GetComponentInChildren<MonsterSpawner>(true) == null) return;
        if (!offeredRooms.Add(room.gameObject)) return;
        offeredRooms.RemoveWhere(r => r == null);
        Enqueue(reward == RoomRewardKind.Stat ? Req.Stat : Req.Room);
    }

    void Enqueue(Req boss)
    {
        pending.Enqueue(boss);
        if (runner == null) runner = StartCoroutine(Run());
    }

    bool waitingWindow;
    void HandleFinished() => waitingWindow = false;

    IEnumerator Run()
    {
        while (pending.Count > 0)
        {
            Req req = pending.Dequeue();
            bool boss = req == Req.Boss;
            if (boss)
            {
                // [2026-11-25] 슬로우모션이 다 끝난 다음부터 bossOfferDelay 초 뒤에 연다 (예전: 처치 순간부터 셌음 → 슬로우 끝나자마자 열림)
                yield return null;   // 같은 프레임에 슬로우가 시작될 수 있으니 한 프레임 기다림
                while (BossSlowMotion.IsPlaying) yield return null;
                yield return new WaitForSecondsRealtime(bossOfferDelay);
            }
            else yield return new WaitForSecondsRealtime(roomOfferDelay);
            while (!Free()) yield return null;
            if (window == null) break;
            waitingWindow = req == Req.Stat ? StatCardOffer.Open(window) : window.Offer(boss);
            while (waitingWindow && window != null && window.IsOpen) yield return null;
            waitingWindow = false;
        }
        runner = null;
    }

    static bool Free()
    {
        if (GamePause.IsPaused) return false;
        if (BossSlowMotion.IsPlaying) return false;   // [2026-11-24] 보스 처치 슬로우가 원래 속도로 돌아온 뒤에 연다
        if (CutsceneSequencer.IsAnyPlaying) return false;
        if (NPC.AnyTalking) return false;
        if (ShopWindow.BlocksEscape) return false;
        return true;
    }
}
