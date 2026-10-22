using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Com.LuisPedroFonseca.ProCamera2D;
using Spine.Unity;
using UnityEngine.Rendering;

/// <summary>
/// 플레이어 입력 없이 초 단위로 자동 진행되는 컷신용 시퀀서.
/// (F키/클릭으로 넘기는 Npc.cs의 대사 시스템과는 별개 — 보스 등장 같은 논-인터랙티브 연출용)
///
/// 인스펙터에 스텝을 순서대로 나열해두면 위에서부터 하나씩 실행된다.
/// 코드를 새로 짜지 않고, 스텝 추가/순서 변경/값 조정만으로 새 연출을 만들 수 있게 하는 게 목적.
///
/// [2026-10-15 추가] 보스 컷씬용 스텝 10종 (아래 enum 참고) + 프리팹 단독 동작 지원
///  - 카메라/흔들림 참조를 비워두면 씬의 ProCamera2D / ProCamera2DShake를 알아서 찾는다
///    (방 프리팹 안에 들어가는 컷씬은 씬 오브젝트를 드래그할 수 없기 때문)
///  - 대사창을 비워두면 화면 덮개(CutsceneOverlay)에 대사창을 직접 만들어 쓴다
///  - 재생 중에는 IsAnyPlaying이 true → Room.cs가 그동안 포탈을 열지 않는다
/// </summary>
// [2026-09-29: ScreenTransition 스텝은 ScreenFader.cs가 죽은 코드로 삭제되면서 함께 제거됨.
// 화면 암전 연출은 2026-10-15에 FadeScreen 스텝으로 다시 추가됨.]
public enum CutsceneStepType
{
    Wait,               // 그냥 대기
    ZoomTo,             // 카메라 줌 (절대 크기로)
    FocusOn,            // 카메라 포커스를 특정 대상으로 이동
    PlayAnimatorTrigger,// 애니메이터 트리거 실행
    SlowMotion,         // 슬로우모션 진입 → 유지 → 복귀
    Shake,              // 카메라 흔들림
    Dialogue,           // 대사 한 줄 출력
    // ── [2026-10-15 추가] 기존 값의 번호가 바뀌지 않도록 반드시 맨 뒤에만 추가할 것 ──
    LockPlayer,         // 플레이어 조작 잠금 + 전투 UI 숨김
    UnlockPlayer,       // 잠금 해제
    Letterbox,          // 위아래 검은 띠
    SpineAnimation,     // 스파인 애니메이션 재생
    SpawnEffect,        // 이펙트 프리팹 생성
    ZoomPunch,          // 순간 확대했다가 원래대로
    Flash,              // 화면 섬광
    ShakeRamp,          // 점점 세지는 흔들림 (뒤에서 계속 돌고 다음 스텝으로 바로 넘어감)
    KillPlayer,         // 플레이어 처리 연출 → 게임오버 화면
    FadeScreen,         // 화면 암전/밝아짐
    ProjectileShot,     // [2026-10-15] 마지막으로 생성한 이펙트 안의 투사체(화살)를 플레이어에게 날리고 카메라가 따라감
    NpcDialogue,        // [2026-10-18] NPC 대화창(F/클릭으로 넘기는 대사)을 열고, 대화가 끝날 때까지 기다림
    SwordSlashKill,     // [2026-10-22] 마지막으로 생성한 검사(SpriteFrameSequence)가 '베는 프레임'에 닿으면, 플레이어를 지나는 선으로 화면 가르기
}

[System.Serializable]
public class CutsceneStep
{
    public string label; // 리스트에서 알아보기 쉽게 다는 메모 — 실행 로직과는 무관
    public CutsceneStepType type = CutsceneStepType.Wait;
    [Tooltip("체크하면 이 스텝이 끝나길 기다리지 않고 바로 다음 스텝으로 넘어간다 (흔들림·슬로모션을 다른 연출과 겹칠 때)")]
    public bool runInBackground = false;

    // Wait
    public float waitSeconds = 1f;

    // ZoomTo (orthographic size 절대값)
    public float zoomSize = 3f;
    public float zoomDuration = 0.5f;

    // FocusOn
    public Transform focusTarget;
    public float focusDuration = 0.6f;
    [Tooltip("체크하면 대상 대신 지금 조작 중인 플레이어 캐릭터를 비춘다")]
    public bool focusOnPlayer = false;
    [Tooltip("체크하면 카메라가 이 대상 하나만 비춘다 (원래 따라가던 대상은 컷신이 끝나면 되돌림)")]
    public bool exclusiveFocus = true;
    [Tooltip("체크하면 대상 대신, 마지막으로 생성한 이펙트 안에서 아래 이름의 오브젝트를 비춘다 (예: Archer)")]
    public bool focusOnSpawned = false;
    [Tooltip("이펙트 안에서 찾을 오브젝트 이름 (비우면 이펙트 자체)")]
    public string spawnedChildName = "Archer";
    [Tooltip("비추는 점을 대상에서 이만큼 옮김 (월드 칸)")]
    public Vector2 focusOffset = Vector2.zero;

    // PlayAnimatorTrigger
    public Animator animator;
    public string triggerName;

    // SlowMotion
    public float slowMotionScale = 0.2f;
    public float slowMotionHoldSeconds = 1f;
    public float slowMotionRampSeconds = 0.25f;

    // Shake / ShakeRamp
    public float shakeDuration = 0.3f;
    public float shakeStrength = 0.3f;
    [Tooltip("ShakeRamp 시작 세기 (shakeStrength까지 점점 커진다)")]
    public float shakeRampFrom = 0.02f;

    // Dialogue
    public string speakerName;
    [TextArea(2, 4)] public string dialogueText;
    public float dialogueHoldSeconds = 2f;

    // Letterbox
    public bool letterboxShow = true;
    [Tooltip("띠 하나의 높이 (화면 높이 대비 비율)")]
    public float letterboxHeight = 0.11f;
    public float letterboxDuration = 0.5f;

    // SpineAnimation
    public SkeletonAnimation spineTarget;
    public string spineAnimationName = "idle";
    public bool spineLoop = true;
    [Tooltip("위 애니메이션이 끝나면 이어서 반복할 애니메이션 (비워두면 없음)")]
    public string spineNextAnimation;
    [Tooltip("체크하면 애니메이션 한 번이 끝날 때까지 다음 스텝으로 안 넘어간다 (반복 애니는 무시)")]
    public bool waitForSpine = false;

    // SpawnEffect
    public GameObject effectPrefab;
    [Tooltip("체크하면 플레이어 위치에 생성, 아니면 아래 기준점 위치에 생성")]
    public bool effectAtPlayer = true;
    public Transform effectAnchor;
    public Vector3 effectOffset;
    public float effectScale = 1f;
    [Tooltip("이 시간(초)이 지나면 생성한 이펙트를 지운다 (0 = 안 지움)")]
    public float effectLifetime = 6f;
    [Tooltip("생성한 이펙트의 재생 배속 (1 = 원래 속도, 1.25 = 25% 빠르게). 파티클·애니메이터·스파인에 같이 적용, 원본 프리팹은 그대로")]
    public float effectSpeed = 1f;
    [Tooltip("체크하면 이펙트 안 파티클이 이펙트 크기·좌우 반전을 같이 따르게 한다 (원본 이펙트는 파티클이 크기를 무시하게 되어 있음)")]
    public bool effectHierarchyScale = true;
    [Tooltip("이펙트 파티클의 그리기 순서 (0 = 원본 그대로). 플레이어(5000)보다 크면 플레이어 앞에 보임")]
    public int effectSortingOrder = 5100;
    [Tooltip("이펙트 안 스파인(궁수)의 그리기 순서 (0 = 원본 그대로)")]
    public int effectSpineSortingOrder = 4004;
    [Tooltip("생성 직후 끌 자식 오브젝트 이름들 (쉼표로 구분). 예: 원본 명중 연기를 끄고 플레이어 쪽에 따로 터뜨릴 때")]
    public string effectHideChildren = "";
    [Tooltip("체크하면 화면을 향한 파티클(화살 불꽃 등)의 그림 방향을 한 번 더 180도 돌린다 (그림이 반대로 보일 때)")]
    public bool effectFlipSprites = false;

    // ZoomPunch (화면 세로 절반 크기 기준 줄일 양)
    public float punchAmount = 1.5f;
    public float punchInSeconds = 0.08f;
    public float punchHoldSeconds = 0.15f;
    public float punchOutSeconds = 0.4f;

    // Flash
    public Color flashColor = Color.white;
    [Range(0f, 1f)] public float flashPeak = 0.85f;
    public float flashDuration = 0.35f;

    // KillPlayer
    [Tooltip("플레이어가 튕겨 나가는 세기")]
    public float killKnockback = 7f;
    [Tooltip("맞은 뒤 사라지기까지 걸리는 시간(초)")]
    public float killFadeSeconds = 1.2f;
    [Tooltip("사라진 뒤 게임오버 화면이 뜨기까지 대기(초)")]
    public float gameOverDelay = 0.6f;
    [Tooltip("튕겨 나갈 방향의 기준 (이 대상의 반대쪽으로 날아감). 비우면 마지막 화살이 날아간 방향으로 튕김")]
    public Transform killSource;

    // ProjectileShot
    [Tooltip("마지막으로 생성한 이펙트 안에서 날릴 오브젝트 이름 (궁수 이펙트의 화살 = shotparticle)")]
    public string projectileChildName = "shotparticle";
    [Tooltip("화살이 플레이어까지 날아가는 시간(초, 게임 시간 — 슬로모션이면 길어짐)")]
    public float projectileSeconds = 0.9f;
    [Tooltip("플레이어 위치에서 이만큼 옮긴 곳에 맞음 (가슴 = y 0.3)")]
    public Vector3 projectileTargetOffset = new Vector3(0f, 0.3f, 0f);
    [Tooltip("체크하면 카메라가 날아가는 화살을 따라가고, 맞으면 플레이어를 비춘다")]
    public bool projectileCameraFollow = true;
    [Tooltip("맞은 순간 플레이어 위치에 생성할 이펙트 (비워도 됨)")]
    public GameObject impactPrefab;
    public float impactScale = 1f;
    [Tooltip("체크하면 화살을 직접 움직이지 않고, 이펙트 안의 화살 파티클(궁수 이펙트의 spot)이 원래대로 날아가는 것을 카메라가 따라간다")]
    public bool projectileFollowParticles = false;
    [Tooltip("화살이 나는 동안의 시간 배율 (0.05 = 20배 느리게). 원래 화살은 매우 빨라서 느리게 해야 따라갈 수 있다")]
    public float projectileFlightTimeScale = 0.06f;
    [Tooltip("화살이 이 시간(실제 초) 안에 플레이어에 닿지 않으면 그냥 다음으로 넘어감")]
    public float projectileTimeout = 5f;
    [Tooltip("날아가는 화살 뒤에 그리는 빛 꼬리 두께 (월드 칸, 0 = 꼬리 없음)")]
    public float projectileTrailWidth = 0.22f;
    [Tooltip("빛 꼬리 길이 (초 — 클수록 길게 남음)")]
    public float projectileTrailTime = 0.18f;
    [Tooltip("화살 빛 색")]
    public Color projectileColor = new Color(1f, 0.9f, 0.55f, 1f);
    [Tooltip("화살 머리 빛 크기 (월드 칸)")]
    public float projectileHeadSize = 0.45f;

    // FadeScreen
    public Color fadeColor = Color.black;
    [Range(0f, 1f)] public float fadeTo = 1f;
    public float fadeDuration = 0.8f;

    // NpcDialogue [2026-10-18 추가]
    [Tooltip("대사를 띄울 NPC 대화창 (koke Canvas의 NPC)")]
    public NPC npc;
    [Tooltip("열 대사 세트 이름 (NPC 대사 세트의 Condition Key). 비우면 첫 번째 세트")]
    public string npcConditionKey;

    // SwordSlashKill [2026-10-22 추가]
    [Tooltip("검사 그림이 이 번호(0부터)의 프레임이 되는 순간 벤다")]
    public int slashFrame = 9;
    [Tooltip("이 시간(초, 실제 시간) 안에 베는 프레임이 안 오면 그냥 벤다")]
    public float slashTimeout = 6f;
    [Tooltip("화면이 갈라지는 선의 각도(도). 0 = 수평, 양수 = 오른쪽이 올라감")]
    public float slashAngle = 18f;
    [Tooltip("화면이 갈라지는 선이 지나갈 지점: 플레이어 그림 가운데에서 이만큼 옮긴 곳")]
    public Vector2 slashCenterOffset = new Vector2(0f, 0.1f);
    [Tooltip("화면 가르기를 시작한 뒤 다음 단계로 넘어가기까지(초, 실제 시간)")]
    public float cutDelay = 0.14f;
}

public class CutsceneSequencer : MonoBehaviour
{
    [Header("참조 (비워두면 씬에서 자동으로 찾음)")]
    public ProCamera2D proCam;
    public ProCamera2DShake proCamShake;
    public CutsceneDialogueBox dialogueBox;
    [Tooltip("대사창을 자동으로 만들 때 쓸 글꼴 (비워두면 유니티 기본 글꼴)")]
    public Font dialogueFont;
    [Tooltip("[2026-10-15] 대사창 이름표(말하는 사람) 글꼴. 비워두면 위 대사 글꼴을 굵게 그림")]
    public Font dialogueSpeakerFont;

    [Header("화면 가르기 (2026-10-22) — SwordSlashKill 단계가 사용")]
    public ScreenSlashEffect screenSlash;
    [Tooltip("점검용: 화면 가르기 동안 선의 중심점(플레이어 몸 중심)에 분홍 십자를 그리고, 시작할 때 콘솔에 좌표를 한 줄 출력. 위치 확인이 끝나면 끄기")]
    public bool slashDebugMarker = true;

    [Header("컷신 스텝 목록 (위에서부터 순서대로 재생)")]
    public List<CutsceneStep> steps = new List<CutsceneStep>();

    [Header("씬 시작하자마자 자동 재생할지")]
    public bool playOnStart = false;

    [Header("보스방 카메라")]
    [Tooltip("체크하면 재생 동안 보스방 '방 전체 고정' 카메라를 잠시 풀어서 줌/포커스가 먹히게 한다 (끝나면 다시 고정)")]
    public bool releaseRoomCamera = true;
    [Tooltip("체크하면 컷신이 끝날 때 카메라 대상/줌을 시작 전 상태로 되돌린다")]
    public bool restoreCameraOnEnd = true;

    /// <summary>지금 재생 중인 컷신이 하나라도 있는지 (Room.cs가 재생 중엔 포탈을 열지 않음)</summary>
    public static bool IsAnyPlaying => playingCount > 0;
    static int playingCount = 0;

    Transform currentFocusTarget;
    Coroutine playRoutine;
    float baseFixedDeltaTime;
    bool counted;          // playingCount에 이 컷신 몫을 더해뒀는지
    bool playerLocked;     // LockPlayer 스텝으로 잠갔는지
    bool roomCameraReleased;
    bool gameOverTriggered; // KillPlayer로 게임오버까지 갔으면 끝날 때 카메라/조작을 되돌리지 않는다
    List<CameraTarget> savedTargets; // exclusiveFocus로 치워둔 원래 카메라 대상
    float savedHalfHeight = -1f;
    readonly List<Coroutine> backgroundRoutines = new List<Coroutine>();
    readonly List<GameObject> spawnedEffects = new List<GameObject>();
    GameObject lastSpawnedEffect;
    float lastProjectileDir = 0f; // 마지막 화살이 날아간 방향 (+1 오른쪽, -1 왼쪽) — KillPlayer가 그 방향으로 튕김 // SpawnEffect로 마지막에 만든 이펙트 (FocusOn·ProjectileShot이 안의 오브젝트를 찾을 때 사용)

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>처음부터 재생. 이미 재생 중이면 멈추고 처음부터 다시 시작한다.</summary>
    public void Play()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        ResolveReferences();
        if (!counted) { counted = true; playingCount++; }
        playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>재생 중단. timeScale이 슬로우모션 도중에 눌린 채로 있었다면 정상 복구한다.</summary>
    public void Stop()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = null;
        Cleanup(false);
        RestoreTime();
    }

    // 비어 있는 참조를 씬에서 찾아 채운다 (방 프리팹 안에서는 씬 오브젝트를 드래그할 수 없으므로)
    void ResolveReferences()
    {
        if (proCam == null && ProCamera2D.Exists) proCam = ProCamera2D.Instance;
        if (proCamShake == null && ProCamera2DShake.Exists) proCamShake = ProCamera2DShake.Instance;
        if (dialogueBox == null)
        {
            bool needsBox = steps.Exists(s => s.type == CutsceneStepType.Dialogue);
            if (needsBox) dialogueBox = CutsceneOverlay.Get().GetDialogueBox(dialogueFont, dialogueSpeakerFont);
        }
    }

    // [2026-10-10 추가] 시간 복구 — 메뉴 등 창이 시간을 멈추고 있으면 0을 유지하고, 물리 간격은 항상 원래 값으로
    void RestoreTime()
    {
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime;
        Time.timeScale = GamePause.IsPaused ? 0f : 1f;
    }

    IEnumerator PlayRoutine()
    {
        if (releaseRoomCamera)
        {
            // 같은 프레임에 Room.Start()가 방 고정을 걸 수 있으므로 한 프레임 뒤에 푼다
            yield return null;
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.ReleaseRoom();
                roomCameraReleased = true;
            }
        }
        if (proCam != null) savedHalfHeight = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;

        foreach (var step in steps)
        {
            if (step.runInBackground) backgroundRoutines.Add(StartCoroutine(RunStep(step)));
            else yield return RunStep(step);
        }

        playRoutine = null;
        Cleanup(true);
    }

    // 컷신이 끝나거나 중단될 때 잠금/카메라/화면 덮개를 원래대로
    void Cleanup(bool finished)
    {
        if (gameOverTriggered) finished = false;

        foreach (var r in backgroundRoutines)
            if (r != null) StopCoroutine(r);
        backgroundRoutines.Clear();

        if (playerLocked && !gameOverTriggered)
        {
            playerLocked = false;
            if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
            CombatUIGate.Unblock();
        }

        // 화면 덮개(띠/암전)가 남아 있지 않도록 걷어준다
        if (!gameOverTriggered) CutsceneOverlay.HideAllIfExists(0.4f);

        if (restoreCameraOnEnd && proCam != null && !gameOverTriggered)
        {
            if (savedTargets != null)
            {
                proCam.RemoveAllCameraTargets();
                proCam.AddCameraTargets(savedTargets);
                savedTargets = null;
            }
            if (savedHalfHeight > 0f)
            {
                float cur = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
                if (finished) proCam.Zoom(savedHalfHeight - cur, 0.5f);
                savedHalfHeight = -1f;
            }
        }
        currentFocusTarget = null;

        if (roomCameraReleased)
        {
            roomCameraReleased = false;
            // 끝까지 재생됐을 때만 보스방 고정을 다시 건다 (게임오버로 끝나면 의미 없음)
            if (finished)
            {
                var room = GetComponentInParent<Room>();
                if (room != null) room.HoldBossCamera();
            }
        }

        if (counted) { counted = false; playingCount = Mathf.Max(0, playingCount - 1); }

        // 뒤에서 돌던 슬로모션이 중간에 멈췄을 수 있으므로 시간 배율을 항상 원래대로
        RestoreTime();
    }

    IEnumerator RunStep(CutsceneStep step)
    {
        switch (step.type)
        {
            case CutsceneStepType.Wait:
                yield return new WaitForSeconds(step.waitSeconds);
                break;

            case CutsceneStepType.ZoomTo:
                if (proCam != null)
                {
                    float currentHalfHeight = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
                    proCam.Zoom(step.zoomSize - currentHalfHeight, step.zoomDuration);
                }
                yield return new WaitForSeconds(step.zoomDuration);
                break;

            case CutsceneStepType.FocusOn:
                FocusStep(step);
                yield return new WaitForSeconds(step.focusDuration);
                break;

            case CutsceneStepType.PlayAnimatorTrigger:
                if (step.animator != null && !string.IsNullOrEmpty(step.triggerName))
                    step.animator.SetTrigger(step.triggerName);
                break;

            case CutsceneStepType.SlowMotion:
                yield return SlowMotionStep(step);
                break;

            case CutsceneStepType.Shake:
                if (proCamShake != null && GameSettings.ShakeScale > 0f)   // [2026-10-18] 설정 창의 '화면 흔들림' 배율
                {
                    float ss = step.shakeStrength * GameSettings.ShakeScale;
                    proCamShake.Shake(step.shakeDuration, new Vector2(ss, ss));
                }
                yield return new WaitForSeconds(step.shakeDuration);
                break;

            case CutsceneStepType.Dialogue:
                if (dialogueBox != null)
                {
                    dialogueBox.Show(step.speakerName, step.dialogueText);
                    yield return new WaitForSecondsRealtime(step.dialogueHoldSeconds);
                    dialogueBox.Hide();
                }
                else
                {
                    yield return new WaitForSeconds(step.dialogueHoldSeconds);
                }
                break;

            case CutsceneStepType.LockPlayer:
                if (!playerLocked)
                {
                    playerLocked = true;
                    if (PlayerController.Instance != null) PlayerController.Instance.LockControl(this);
                    CombatUIGate.Block();
                }
                break;

            case CutsceneStepType.UnlockPlayer:
                if (playerLocked)
                {
                    playerLocked = false;
                    if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
                    CombatUIGate.Unblock();
                }
                break;

            case CutsceneStepType.Letterbox:
                CutsceneOverlay.Get().Letterbox(step.letterboxShow, step.letterboxHeight, step.letterboxDuration);
                yield return new WaitForSecondsRealtime(step.letterboxDuration);
                break;

            case CutsceneStepType.SpineAnimation:
                yield return SpineStep(step);
                break;

            case CutsceneStepType.SpawnEffect:
                SpawnEffectStep(step);
                break;

            case CutsceneStepType.ZoomPunch:
                if (proCam != null)
                {
                    proCam.Zoom(-step.punchAmount, step.punchInSeconds, EaseType.EaseOut);
                    yield return new WaitForSeconds(step.punchInSeconds + step.punchHoldSeconds);
                    proCam.Zoom(step.punchAmount, step.punchOutSeconds, EaseType.EaseInOut);
                }
                break;

            case CutsceneStepType.Flash:
                CutsceneOverlay.Get().Flash(step.flashColor, step.flashPeak, step.flashDuration);
                break;

            case CutsceneStepType.ShakeRamp:
                backgroundRoutines.Add(StartCoroutine(ShakeRampRoutine(step)));
                break;

            case CutsceneStepType.KillPlayer:
                yield return KillPlayerStep(step);
                break;

            case CutsceneStepType.FadeScreen:
                CutsceneOverlay.Get().Fade(step.fadeColor, step.fadeTo, step.fadeDuration);
                yield return new WaitForSecondsRealtime(step.fadeDuration);
                break;

            case CutsceneStepType.ProjectileShot:
                yield return ProjectileStep(step);
                break;

            case CutsceneStepType.SwordSlashKill:
                yield return SwordSlashKillStep(step);
                break;

            case CutsceneStepType.NpcDialogue:
                yield return NpcDialogueStep(step);
                break;
        }
    }

    // ── [2026-10-18 추가] NPC 대화창으로 대사 진행 → 끝날 때까지 대기 ──────────
    // 대화창의 조작 잠금은 NPC 이름으로 따로 걸리므로, LockPlayer 스텝의 잠금과 겹쳐도 서로 풀지 않는다.
    IEnumerator NpcDialogueStep(CutsceneStep step)
    {
        if (step.npc == null) yield break;

        string key = string.IsNullOrEmpty(step.npcConditionKey) ? null : step.npcConditionKey;
        step.npc.SetCondition(key, PlayerController.Instance);

        yield return null; // 대화가 열린 다음 프레임부터 확인
        while (step.npc != null && step.npc.IsTalking) yield return null;
    }

    // ── 이펙트에 들어 있는 원래 화살 파티클을 카메라가 따라가기 ───────────
    // 궁수 이펙트(boss_disposaleffect)의 spot은 화살 모양 메쉬를 초속 200으로 쏘는 파티클이다.
    // 화살이 나오는 순간 시간을 아주 느리게 하고, 가장 먼저 나간 화살의 위치를 매 프레임 읽어
    // 카메라용 빈 오브젝트를 그 자리에 옮긴다. 화살이 플레이어를 지나면 명중 처리.
    IEnumerator FollowParticleProjectile(CutsceneStep step)
    {
        Transform emitter = FindInSpawned(step.projectileChildName);
        Transform player = CurrentPlayerTransform();
        var ps = emitter != null ? emitter.GetComponent<ParticleSystem>() : null;
        if (player == null) yield break;
        if (ps == null)
        {
            // 화살을 못 찾으면: 지금 비추던 곳(궁수)에서 플레이어로 카메라만 옮기고 명중 처리
            Debug.LogWarning("[CutsceneSequencer] 따라갈 화살 파티클을 찾지 못해 카메라만 플레이어로 옮깁니다.", this);
            SetFocus(player, Mathf.Max(0.05f, step.projectileSeconds), Vector2.zero, true);
            yield return new WaitForSeconds(Mathf.Max(0.05f, step.projectileSeconds));
            SpawnImpact(step, player);
            yield break;
        }

        var proxy = new GameObject("CutsceneArrowCamera").transform;
        proxy.position = emitter.position;
        spawnedEffects.Add(proxy.gameObject);
        Destroy(proxy.gameObject, 30f);
        SetFocus(proxy, 0.15f, Vector2.zero, true);

        float dir = Mathf.Sign(player.position.x - emitter.position.x);
        if (dir == 0f) dir = 1f;
        lastProjectileDir = dir;
        var buffer = new ParticleSystem.Particle[ps.main.maxParticles];
        bool flying = false;
        float savedH = proCam != null ? proCam.HorizontalFollowSmoothness : 0f;
        float savedV = proCam != null ? proCam.VerticalFollowSmoothness : 0f;
        float real = 0f;

        while (real < step.projectileTimeout)
        {
            int n = ps.GetParticles(buffer);
            if (n > 0)
            {
                if (!flying)
                {
                    flying = true;
                    SetTimeScale(step.projectileFlightTimeScale); // 화살이 나오는 순간부터 아주 느리게
                }
                // 가장 오래 날아간(= 가장 먼저 쏜) 화살
                int best = 0; float bestAge = -1f;
                for (int i = 0; i < n; i++)
                {
                    float age = buffer[i].startLifetime - buffer[i].remainingLifetime;
                    if (age > bestAge) { bestAge = age; best = i; }
                }
                Vector3 pos = buffer[best].position;
                var main = ps.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Local) pos = ps.transform.TransformPoint(pos);
                else if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && main.customSimulationSpace != null)
                    pos = main.customSimulationSpace.TransformPoint(pos);
                proxy.position = new Vector3(pos.x, pos.y, proxy.position.z);

                if ((pos.x - player.position.x) * dir >= 0f) break; // 플레이어에 닿음
            }
            else if (flying) break; // 화살이 다 사라짐

            if (proCam != null) { proCam.HorizontalFollowSmoothness = 0f; proCam.VerticalFollowSmoothness = 0f; } // 카메라가 화살에 딱 붙어 따라가게
            if (!GamePause.IsPaused) real += Time.unscaledDeltaTime;
            yield return null;
        }

        if (proCam != null) { proCam.HorizontalFollowSmoothness = savedH; proCam.VerticalFollowSmoothness = savedV; }
        SetTimeScale(1f);

        SpawnImpact(step, player);
        if (step.projectileCameraFollow) SetFocus(player, 0.1f, Vector2.zero, true);
    }

    void SpawnImpact(CutsceneStep step, Transform player)
    {
        if (step.impactPrefab == null || player == null) return;
        var fx = Instantiate(step.impactPrefab, player.position + step.projectileTargetOffset, Quaternion.identity);
        fx.transform.localScale = step.impactPrefab.transform.localScale * step.impactScale;
        spawnedEffects.Add(fx);
        Destroy(fx, 5f);
    }

    // [2026-10-15] 원래 화살 파티클은 "지나간 거리 1칸당 1개"만 뿌려서(원래는 0.27초에 100칸을 날아가 선처럼 보였음)
    // 천천히 날리면 거의 안 보인다 → 날리는 동안 촘촘히 뿌리고, 머리 빛과 빛 꼬리를 붙인다.
    void MakeProjectileVisible(Transform arrow, CutsceneStep step)
    {
        arrow.localScale = Vector3.one; // 크기 값을 월드 칸 그대로 쓰기 위해 (이펙트 크기 0.42가 곱해지지 않게)
        Material mat = null;
        foreach (var ps in arrow.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(step.projectileHeadSize * 0.5f, step.projectileHeadSize);
            main.startColor = step.projectileColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 90f;
            em.rateOverDistance = 30f;
            var shape = ps.shape;
            shape.enabled = false;   // 한 점(화살 머리)에서만 나오게
            if (!ps.isPlaying) ps.Play();
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null && mat == null) mat = r.sharedMaterial;
        }

        if (step.projectileTrailWidth > 0f)
        {
            var trail = arrow.gameObject.GetComponent<TrailRenderer>();
            if (trail == null) trail = arrow.gameObject.AddComponent<TrailRenderer>();
            trail.time = step.projectileTrailTime;
            trail.minVertexDistance = 0.03f;
            trail.widthMultiplier = step.projectileTrailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var c0 = step.projectileColor; var c1 = c0; c1.a = 0f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c0, 1f) },
                      new[] { new GradientAlphaKey(c0.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            if (mat != null) trail.sharedMaterial = mat;
            trail.sortingOrder = 5101;
            trail.alignment = LineAlignment.View;
            trail.Clear();
        }
    }

    // 마지막으로 생성한 이펙트 안에서 이름으로 오브젝트 찾기 (자식의 자식까지)
    Transform FindInSpawned(string childName)
    {
        if (lastSpawnedEffect == null) return null;
        if (string.IsNullOrEmpty(childName)) return lastSpawnedEffect.transform;
        foreach (var tr in lastSpawnedEffect.GetComponentsInChildren<Transform>(true))
            if (tr.name == childName) return tr;
        Debug.LogWarning($"[CutsceneSequencer] 생성한 이펙트 안에 '{childName}' 오브젝트가 없습니다.", lastSpawnedEffect);
        return null;
    }

    // ── 투사체(화살) 날리기 + 카메라 따라가기 ───────────────────
    IEnumerator ProjectileStep(CutsceneStep step)
    {
        if (step.projectileFollowParticles)
        {
            yield return FollowParticleProjectile(step);
            yield break;
        }

        Transform arrow = FindInSpawned(step.projectileChildName);
        Transform player = CurrentPlayerTransform();
        if (arrow == null || player == null) yield break;

        // 이펙트에 원래 붙어 있던 이동 애니메이션(순식간에 화면 밖으로 날아감)을 끄고 직접 움직인다
        foreach (var an in arrow.GetComponentsInChildren<Animator>(true)) an.enabled = false;
        arrow.SetParent(null, true);
        MakeProjectileVisible(arrow, step);
        spawnedEffects.Add(arrow.gameObject);
        Destroy(arrow.gameObject, step.projectileSeconds + 2f);

        if (step.projectileCameraFollow) SetFocus(arrow, 0.12f, Vector2.zero, true);

        Vector3 start = arrow.position;
        float t = 0f;
        float dur = Mathf.Max(0.01f, step.projectileSeconds);
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            u = u * u * (3f - 2f * u) * 0.3f + u * 0.7f; // 살짝 부드럽게 출발·도착
            Vector3 target = player.position + step.projectileTargetOffset;
            Vector3 p = Vector3.Lerp(start, target, u);
            p.z = start.z;
            arrow.position = p;
            yield return null;
        }

        Vector3 hit = player.position + step.projectileTargetOffset;
        if (step.impactPrefab != null)
        {
            var fx = Instantiate(step.impactPrefab, hit, Quaternion.identity);
            fx.transform.localScale = step.impactPrefab.transform.localScale * step.impactScale;
            spawnedEffects.Add(fx);
            Destroy(fx, 5f);
        }
        if (step.projectileCameraFollow) SetFocus(player, 0.1f, Vector2.zero, true);
    }

    // ── 포커스 ─────────────────────────────────────────────
    void FocusStep(CutsceneStep step)
    {
        Transform target = step.focusOnPlayer ? CurrentPlayerTransform()
                         : step.focusOnSpawned ? FindInSpawned(step.spawnedChildName)
                         : step.focusTarget;
        SetFocus(target, step.focusDuration, step.focusOffset, step.exclusiveFocus);
    }

    // 카메라가 target을 비추게 한다 (exclusive면 원래 대상들을 치워두고 끝날 때 되돌림)
    void SetFocus(Transform target, float duration, Vector2 offset, bool exclusive)
    {
        if (proCam == null || target == null) return;

        if (exclusive)
        {
            // 처음 한 번만 원래 대상들을 기억해두고 전부 치운다 (끝날 때 Cleanup에서 되돌림)
            if (savedTargets == null)
            {
                savedTargets = new List<CameraTarget>();
                foreach (var t in proCam.CameraTargets)
                    if (t != null && t.TargetTransform != null && t.TargetTransform != currentFocusTarget)
                        savedTargets.Add(new CameraTarget
                        {
                            TargetTransform = t.TargetTransform,
                            TargetInfluenceH = t.TargetInfluenceH,
                            TargetInfluenceV = t.TargetInfluenceV,
                            TargetOffset = t.TargetOffset,
                        });
                foreach (var t in savedTargets)
                    proCam.RemoveCameraTarget(t.TargetTransform, duration);
            }
        }

        var previous = currentFocusTarget;
        if (previous == target) proCam.RemoveCameraTarget(target); // 같은 대상을 오프셋만 바꿔 다시 비출 때
        proCam.AddCameraTarget(target, 1f, 1f, duration, offset);
        if (previous != null && previous != target)
            proCam.RemoveCameraTarget(previous, duration);
        currentFocusTarget = target;
    }

    // ── [2026-10-22 추가] 검사의 일격: 베는 프레임까지 기다림 → 플레이어 몸을 지나는 선으로 화면 가르기 ──────────
    IEnumerator SwordSlashKillStep(CutsceneStep step)
    {
        SpriteFrameSequence seq = lastSpawnedEffect != null ? lastSpawnedEffect.GetComponentInChildren<SpriteFrameSequence>() : null;
        float waited = 0f;
        while (seq != null && seq.CurrentFrame < step.slashFrame && !seq.Finished && waited < step.slashTimeout)
        {
            if (!GamePause.IsPaused) waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Transform player = CurrentPlayerTransform();
        if (player == null) yield break;
        // [2026-10-22] 몸 중심: 본체 충돌체 → 본체 스프라이트 → 가장 큰 스프라이트 순 (붙어 있는 이펙트 그림에 끌려가지 않게)
        Vector3 cut = BodyCenter(player) + (Vector3)step.slashCenterOffset;

        // 플레이어가 날아갈 방향 = 검사 반대쪽 (KillPlayer 단계가 이 방향으로 튕김)
        if (seq != null)
        {
            float d = Mathf.Sign(player.position.x - seq.transform.position.x);
            lastProjectileDir = d != 0f ? d : 1f;
        }

        // [2026-10-22] 여기서 카메라를 옮기지 않는다 — 앞 단계(검사 비춤)가 검사와 플레이어를 같이 잡고 있음
        if (screenSlash != null)
        {
            // 화면 좌표는 화면 효과가 그려지는 최종 카메라(게임 씬: Camera_2D) 기준으로 계산
            // (Camera.main 은 3D 배경 카메라라서 플레이어 위치가 엉뚱하게 계산됨)
            Camera finalCam = HitGlitchScreenFeature.FinalCamera;
            if (finalCam != null) screenSlash.targetCamera = finalCam;
            screenSlash.PlayAtWorld(step.slashAngle, cut);
            StartSlashTracking(player, cut - player.position);
            if (slashDebugMarker)
            {
                Camera c = finalCam != null ? finalCam : Camera.main;
                Vector3 v = c != null ? c.WorldToViewportPoint(cut) : Vector3.zero;
                Debug.Log($"[화면 가르기 점검] 카메라={(c != null ? c.name : "없음")} 화면={Screen.width}x{Screen.height} " +
                          $"카메라영역={(c != null ? c.pixelRect.ToString() : "-")} 플레이어={player.name} 몸중심={cut} 화면좌표=({v.x:0.000}, {v.y:0.000}) 각도={step.slashAngle}", this);
            }
        }
        else Debug.LogWarning("[CutsceneSequencer] Screen Slash 칸이 비어 있어 화면 가르기를 건너뜁니다.", this);

        for (float t = 0f; t < step.cutDelay; t += Time.unscaledDeltaTime) yield return null;
    }

    // ── [2026-10-22] 화면 가르기 선을 플레이어 몸에 고정 ──
    // 화면 가르기는 시작할 때 정한 '화면상의 점'에 선을 그으므로, 그 뒤 줌·흔들림으로 카메라가 움직이면 선이 몸을 비껴간다.
    // 재생되는 동안 최종 카메라가 그리기 직전마다 선의 중심(_SlashCenter)을 플레이어 몸 위치로 다시 맞춘다.
    static readonly int SlashCenterId = Shader.PropertyToID("_SlashCenter");
    Transform slashTrackTarget;
    Vector3 slashTrackOffset;
    bool slashTracking;

    void StartSlashTracking(Transform target, Vector3 offset)
    {
        slashTrackTarget = target;
        slashTrackOffset = offset;
        if (!slashTracking)
        {
            slashTracking = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }

    void StopSlashTracking()
    {
        if (slashDebugLine != null) Destroy(slashDebugLine.gameObject);
        slashDebugLine = null;
        if (!slashTracking) return;
        slashTracking = false;
        slashTrackTarget = null;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (screenSlash == null || slashTrackTarget == null || !screenSlash.IsPlaying || screenSlash.slashMaterial == null)
        {
            StopSlashTracking();
            return;
        }
        if (cam != HitGlitchScreenFeature.FinalCamera) return;
        Vector3 world = slashTrackTarget.position + slashTrackOffset;
        Vector3 vp = cam.WorldToViewportPoint(world);
        screenSlash.slashMaterial.SetVector(SlashCenterId, new Vector4(vp.x, vp.y, 0f, 0f));
        if (slashDebugMarker) UpdateSlashDebugMarker(world);
    }

    // ── 점검용 분홍 십자 (선의 중심점과 같은 월드 위치) ──
    LineRenderer slashDebugLine;

    void UpdateSlashDebugMarker(Vector3 world)
    {
        if (slashDebugLine == null)
        {
            var go = new GameObject("[화면 가르기 점검 십자]");
            slashDebugLine = go.AddComponent<LineRenderer>();
            slashDebugLine.useWorldSpace = false;
            slashDebugLine.positionCount = 5;
            const float r = 0.35f;
            slashDebugLine.SetPositions(new[] { new Vector3(-r, 0f), new Vector3(r, 0f), Vector3.zero, new Vector3(0f, r), new Vector3(0f, -r) });
            slashDebugLine.widthMultiplier = 0.05f;
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) slashDebugLine.material = new Material(sh);
            slashDebugLine.startColor = slashDebugLine.endColor = Color.magenta;
            slashDebugLine.sortingOrder = 32000;
            go.layer = slashTrackTarget != null ? slashTrackTarget.gameObject.layer : 0;   // 플레이어와 같은 카메라에 그려지게
        }
        slashDebugLine.transform.position = new Vector3(world.x, world.y, slashTrackTarget != null ? slashTrackTarget.position.z : world.z);
    }

    // 플레이어 몸 중심: 본체 충돌체(트리거 아님) → 본체 스프라이트 → 자식 중 가장 큰 스프라이트 → 위치
    static Vector3 BodyCenter(Transform player)
    {
        foreach (var col in player.GetComponents<Collider2D>())
            if (col.enabled && !col.isTrigger) return col.bounds.center;
        if (player.TryGetComponent(out SpriteRenderer own) && own.enabled && own.sprite != null) return own.bounds.center;
        SpriteRenderer body = MainSprite(player);
        return body != null ? body.bounds.center : player.position;
    }

    // 플레이어 안에서 가장 큰(= 몸) 그림
    static SpriteRenderer MainSprite(Transform root)
    {
        SpriteRenderer best = null; float bestArea = 0f;
        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sprite == null) continue;
            float a = r.bounds.size.x * r.bounds.size.y;
            if (a > bestArea) { bestArea = a; best = r; }
        }
        return best;
    }

    static Transform CurrentPlayerTransform()
    {
        if (CharacterManager.Instance != null && CharacterManager.Instance.CurrentCharacter != null)
            return CharacterManager.Instance.CurrentCharacter.transform;
        return PlayerController.Instance != null ? PlayerController.Instance.transform : null;
    }

    // ── 스파인 ─────────────────────────────────────────────
    IEnumerator SpineStep(CutsceneStep step)
    {
        var sa = step.spineTarget;
        if (sa == null || sa.AnimationState == null || string.IsNullOrEmpty(step.spineAnimationName)) yield break;

        var data = sa.AnimationState.Data.SkeletonData;
        if (data.FindAnimation(step.spineAnimationName) == null)
        {
            Debug.LogWarning($"[CutsceneSequencer] 스파인 애니메이션 '{step.spineAnimationName}'이(가) 없습니다.", sa);
            yield break;
        }

        var entry = sa.AnimationState.SetAnimation(0, step.spineAnimationName, step.spineLoop);
        if (!string.IsNullOrEmpty(step.spineNextAnimation) && data.FindAnimation(step.spineNextAnimation) != null)
            sa.AnimationState.AddAnimation(0, step.spineNextAnimation, true, 0f);

        if (step.waitForSpine && !step.spineLoop && entry != null)
        {
            float speed = Mathf.Max(0.01f, sa.timeScale);
            yield return new WaitForSeconds(entry.Animation.Duration / speed);
        }
    }

    // ── 이펙트 ─────────────────────────────────────────────
    void SpawnEffectStep(CutsceneStep step)
    {
        if (step.effectPrefab == null) return;
        Transform at = step.effectAtPlayer ? CurrentPlayerTransform() : step.effectAnchor;
        Vector3 pos = (at != null ? at.position : transform.position) + step.effectOffset;
        var fx = Instantiate(step.effectPrefab, pos, Quaternion.identity);
        ApplyEffectSpeed(fx, step.effectSpeed);
        Vector3 scale = step.effectPrefab.transform.localScale * step.effectScale;
        // [2026-10-15] 좌우 반전(크기 X가 음수)은 화살 메쉬를 뒤집어서 안 보이게 만든다(뒷면이 그려지지 않음).
        // 그래서 크기는 양수로 두고, 대신 세로축으로 180도 돌려서 같은 방향(왼쪽)을 보게 한다.
        bool turned = false;
        if (step.effectHierarchyScale && scale.x < 0f)
        {
            scale.x = -scale.x;
            fx.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            turned = true;
        }
        fx.transform.localScale = scale;
        spawnedEffects.Add(fx);
        lastSpawnedEffect = fx; // FocusOn(궁수 비추기)·ProjectileShot(화살 따라가기)이 이걸로 이펙트 안을 찾는다

        // [2026-10-15] 돌려서 왼쪽을 보게 하면, 화면 기준으로 방향이 정해지는 파티클(정렬이 View인 것 —
        // 화살 불꽃 spot2와 화살 메쉬 spot)은 같이 돌지 않아 그림이 거꾸로(꼬리가 앞) 보인다.
        // 이런 파티클은 시작 회전값에 180도를 더해서 날아가는 방향과 맞춘다.
        if (turned != step.effectFlipSprites)
        {
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var pr = ps.GetComponent<ParticleSystemRenderer>();
                if (pr == null) continue;
                if (pr.alignment != ParticleSystemRenderSpace.View && pr.alignment != ParticleSystemRenderSpace.Facing) continue;
                if (pr.renderMode == ParticleSystemRenderMode.Stretch || pr.renderMode == ParticleSystemRenderMode.None) continue;
                var main = ps.main;
                var rot = main.startRotation; // 3D 회전을 쓰면 이 값이 Z축 회전
                switch (rot.mode)
                {
                    case ParticleSystemCurveMode.Constant: rot.constant += Mathf.PI; break;
                    case ParticleSystemCurveMode.TwoConstants: rot.constantMin += Mathf.PI; rot.constantMax += Mathf.PI; break;
                    default: continue; // 곡선으로 된 회전은 건드리지 않음
                }
                if (main.startRotation3D) main.startRotationZ = rot; else main.startRotation = rot;
            }
        }

        // 원본 이펙트 프리팹은 건드리지 않고, 생성한 복사본에만 보정을 건다
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (step.effectHierarchyScale) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null && step.effectSortingOrder != 0) pr.sortingOrder = step.effectSortingOrder;
        }
        foreach (var mr in fx.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool isSpine = mr.GetComponent<SkeletonRenderer>() != null;
            int order = isSpine ? step.effectSpineSortingOrder : step.effectSortingOrder;
            if (order != 0) mr.sortingOrder = order;
        }
        if (!string.IsNullOrEmpty(step.effectHideChildren))
        {
            var names = new HashSet<string>();
            foreach (var n in step.effectHideChildren.Split(',')) if (n.Trim().Length > 0) names.Add(n.Trim());
            foreach (var tr in fx.GetComponentsInChildren<Transform>(true))
                if (names.Contains(tr.name)) tr.gameObject.SetActive(false);
        }
        if (step.effectLifetime > 0f) Destroy(fx, step.effectLifetime);
    }

    // [2026-10-21] 생성한 복사본만 빠르게/느리게 재생 (파티클 딜레이·스파인 동작·애니메이터가 같은 배율로 움직여 타이밍이 안 어긋남)
    static void ApplyEffectSpeed(GameObject fx, float speed)
    {
        if (speed <= 0f || Mathf.Approximately(speed, 1f)) return;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.simulationSpeed *= speed;
        }
        foreach (var an in fx.GetComponentsInChildren<Animator>(true)) an.speed *= speed;
        foreach (var sa in fx.GetComponentsInChildren<SkeletonAnimation>(true)) sa.timeScale *= speed;
    }

    // ── 점점 세지는 흔들림 (뒤에서 계속 실행) ─────────────────
    IEnumerator ShakeRampRoutine(CutsceneStep step)
    {
        if (proCamShake == null) yield break;
        const float interval = 0.08f;
        float t = 0f;
        while (t < step.shakeDuration)
        {
            float s = Mathf.Lerp(step.shakeRampFrom, step.shakeStrength, t / step.shakeDuration);
            s *= GameSettings.ShakeScale;   // [2026-10-18] 설정 창의 '화면 흔들림' 배율
            if (s > 0f) proCamShake.Shake(interval * 1.6f, new Vector2(s, s), vibrato: 12, smoothness: 0.04f);
            yield return new WaitForSeconds(interval);
            t += interval;
        }
    }

    // ── 플레이어 처리 → 게임오버 ─────────────────────────────
    IEnumerator KillPlayerStep(CutsceneStep step)
    {
        Transform player = CurrentPlayerTransform();
        if (player != null)
        {
            // 맞은 이펙트 (캐릭터에 설정된 피격 파티클 재사용)
            var health = player.GetComponent<Health>();
            if (health != null && health.hitParticlePrefab != null)
                Instantiate(health.hitParticlePrefab, player.position + Vector3.up * 0.5f, Quaternion.identity);

            // 튕겨 나가기 — 공격한 쪽의 반대 방향으로
            float dir = lastProjectileDir != 0f ? lastProjectileDir : 1f; // 기본: 화살이 날아온 방향으로
            if (step.killSource != null) dir = Mathf.Sign(player.position.x - step.killSource.position.x);
            if (dir == 0f) dir = 1f;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = new Vector2(dir * step.killKnockback, step.killKnockback * 0.6f);

            // 하얗게 번쩍 → 깜빡이며 사라짐
            var renderers = player.GetComponentsInChildren<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            float t = 0f;
            while (t < step.killFadeSeconds)
            {
                float u = t / step.killFadeSeconds;
                float flash = t < 0.12f ? 1f : (Mathf.Repeat(t * 12f, 1f) > 0.5f ? 0.6f : 0f) * (1f - u);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_FlashAmount"))
                    {
                        r.GetPropertyBlock(block);
                        block.SetFloat("_FlashAmount", flash);
                        block.SetColor("_FlashColor", Color.white);
                        r.SetPropertyBlock(block);
                    }
                    var c = r.color; c.a = 1f - u; r.color = c;
                }
                t += Time.deltaTime;
                yield return null;
            }
            foreach (var r in renderers)
                if (r != null) { var c = r.color; c.a = 0f; r.color = c; }
        }

        yield return new WaitForSeconds(step.gameOverDelay);

        // 게임오버 화면은 화면 덮개보다 위에 그려지므로 암전/띠를 걷어준다
        CutsceneOverlay.Get().Fade(Color.black, 0f, 0.4f);
        CutsceneOverlay.Get().Letterbox(false, 0.11f, 0.4f);
        if (dialogueBox != null) dialogueBox.Hide();

        gameOverTriggered = true;
        if (CharacterManager.Instance != null) CharacterManager.Instance.ForceGameOver();
    }

    IEnumerator SlowMotionStep(CutsceneStep step)
    {
        yield return RampTimeScale(1f, step.slowMotionScale, step.slowMotionRampSeconds);

        // 유지 구간 — 메뉴가 열려 있는 동안은 시간을 세지 않고, 닫히면 슬로모션 배율을 다시 적용
        float held = 0f;
        while (held < step.slowMotionHoldSeconds)
        {
            if (!GamePause.IsPaused)
            {
                held += Time.unscaledDeltaTime;
                SetTimeScale(step.slowMotionScale);
            }
            yield return null;
        }

        yield return RampTimeScale(step.slowMotionScale, 1f, step.slowMotionRampSeconds);
    }

    IEnumerator RampTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetTimeScale(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            // [2026-10-10] 메뉴 등 창이 시간을 멈춘 동안에는 진행도 덮어쓰기도 하지 않는다
            // (예전엔 다음 프레임에 timeScale을 다시 써서 메뉴 뒤에서 게임이 흘렀음)
            if (!GamePause.IsPaused)
            {
                t += Time.unscaledDeltaTime;
                SetTimeScale(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            }
            yield return null;
        }
        SetTimeScale(to);
    }

    void SetTimeScale(float scale)
    {
        if (GamePause.IsPaused) return;
        Time.timeScale = scale;
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime * scale;
    }

    // [2026-09-29 추가] 컷신 재생 도중(특히 SlowMotion 스텝 진행 중) 이 오브젝트가
    // SetActive(false)로 비활성화되면 PlayRoutine 코루틴이 그 자리에서 멈추고 OnDestroy도
    // 호출되지 않아 timeScale이 눌린 채로 영구히 남는 버그가 있었다. Stop()과 동일하게
    // 비활성화 시점에도 복구해준다.
    void OnDisable()
    {
        StopSlashTracking();
        if (playRoutine != null || counted) Cleanup(false);
        RestoreTime();
        playRoutine = null; // 멈춘 코루틴 참조 정리 — 이후 Play()가 새로 시작할 수 있도록
    }

    void OnDestroy()
    {
        StopSlashTracking();
        if (counted) Cleanup(false);
        RestoreTime();
    }
}
