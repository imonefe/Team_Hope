using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// ESC 키로 여는 게임 메뉴(설정/일시정지). "PauseGaugeUI"(집중 게이지), "PauseThrowUI"(투척 아이템 원형
/// 메뉴)와는 이름만 비슷할 뿐 완전히 다른 기능이니 헷갈리지 말 것.
///
/// 이 스크립트가 붙어있는 오브젝트(GameMenuCanvas) 자체는 평소 비활성화 상태로 씬에 있다가,
/// 별도의 감시자(UILoader)가 ESC를 감지해서 SetActive(true)로 켜준다.
/// (2026-09-29: 이 주석은 원래 GameMenuWatcher를 가리켰는데, 그 스크립트는 죽은 코드로
/// 이미 삭제됐고 실제로 이 역할을 하는 건 UILoader라서 이름을 바로잡음.)
/// 켜지는 순간 OnEnable()에서 자동으로 설정 패널을 띄우고 게임을 정지시키며,
/// 닫힐 때(Resume)는 스스로를 다시 비활성화해서 평소 상태로 돌아간다 —
/// 그래야 다음 ESC 입력을 UILoader가 다시 감지할 수 있다.
///
/// 동작:
/// - 켜짐(OnEnable): 설정 패널 표시 + Time.timeScale = 0으로 전체 정지 + 전투 UI 가림
/// - ESC 또는 계속하기: 메뉴 닫고 재개 (오브젝트 스스로 비활성화)
/// - 처음부터 다시시작: 현재 씬을 처음부터 다시 로드
/// - 제작자: 크레딧 패널로 전환 (ESC 또는 뒤로 버튼으로 다시 메인 메뉴로)
///
/// CombatUIGate를 같이 사용해서, 메뉴가 열려있는 동안 battle Canvas 등 전투 UI를 자동으로 숨긴다
/// (인벤토리/대화창과 동일한 방식 — Inventory.cs, Npc.cs 참고).
///
/// [버튼 연결 방식 — 2026-09-30 변경]
/// 버튼은 코드(AddListener)가 아니라 인스펙터의 OnClick()으로 연결한다.
/// 버튼 추가 방법: 아래 "버튼에서 호출하는 함수" 영역에 public void 함수를 하나 만들고,
/// 버튼 인스펙터 OnClick() → + → GameMenuCanvas 오브젝트를 끌어다 놓기 → GameMenuController → 함수 선택.
/// 호버 연출(커짐/이동)도 각 버튼에 붙은 UIButtonJuice 컴포넌트에서 인스펙터로 조절한다.
///
/// [2026-10-06 추가]
/// - 열려 있는 동안 뒤의 게임 화면을 흐리게 하고, 메뉴보다 아래에 그려지는 다른 UI(전투 UI 등)를 숨긴다 (MenuBackdrop).
/// - 닫을 때(ESC/계속하기)는 게임을 즉시 다시 움직이게 하고, 메뉴 전체는 짧게 페이드아웃한 뒤 꺼진다.
/// </summary>
public class GameMenuController : MonoBehaviour
{
    [Header("패널")]
    public GameObject pausePanel;
    public GameObject creditsPanel;

    [Header("뒤 화면 블러 / 다른 UI 숨기기")]
    [Tooltip("블러 넓이 배율 (클수록 더 넓게 흐려짐)")]
    public float blurRadius = 1.5f;
    [Tooltip("블러 반복 횟수 (많을수록 부드럽고 넓게, 대신 조금 무거워짐)")]
    [Range(0, 8)] public int blurIterations = 4;
    [Tooltip("뒤 화면을 어둡게 하는 정도 (0 = 그대로)")]
    [Range(0f, 1f)] public float blurDim = 0.25f;
    [Tooltip("열릴 때 블러가 차오르는 시간(초)")]
    public float blurFadeIn = 0.25f;
    [Tooltip("닫힐 때 블러가 걷히는 시간(초)")]
    public float blurFadeOut = 0.12f;
    [Tooltip("메뉴가 열려 있는 동안 전투 UI 등 메뉴 아래의 다른 캔버스를 숨김")]
    public bool hideOtherUI = true;

    [Header("닫기")]
    [Tooltip("ESC/계속하기로 닫을 때 메뉴 전체가 사라지는 시간(초). 게임은 누르는 즉시 다시 움직인다")]
    public float closeDuration = 0.12f;

    CanvasGroup rootGroup;
    bool closing;
    bool gameplayRestored = true;

    void Awake()
    {
        rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();

        if (pausePanel != null) pausePanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        // 패널 등장 애니메이션 (켜질 때마다 자동 재생 — OnEnable 기반)
        if (pausePanel != null && pausePanel.GetComponent<UIPanelAnimator>() == null)
            pausePanel.AddComponent<UIPanelAnimator>();
        if (creditsPanel != null && creditsPanel.GetComponent<UIPanelAnimator>() == null)
            creditsPanel.AddComponent<UIPanelAnimator>();
    }

    // UILoader 등이 gameObject.SetActive(true)로 켤 때마다 자동 실행됨
    void OnEnable()
    {
        OpenPause();
    }

    // gameObject.SetActive(false)로 꺼질 때(Resume 등)마다 자동으로 정지/UI 잠금을 원복
    void OnDisable()
    {
        if (rootGroup != null)
        {
            rootGroup.DOKill();
            rootGroup.alpha = 1f;          // 다음에 열 때를 위해 원래대로
            rootGroup.blocksRaycasts = true;
        }
        closing = false;
        RestoreGameplay();                 // 이미 Resume 에서 복구했다면 아무것도 안 함
    }

    // 정지/UI 잠금/블러를 원래대로 (한 번만 실행되도록 보호)
    void RestoreGameplay()
    {
        if (gameplayRestored) return;
        gameplayRestored = true;
        GamePause.Pop(GamePause.Menu);   // [2026-10-10] 다른 창이 아직 열려 있으면 시간은 계속 멈춰 있음
        CombatUIGate.Unblock();
        if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
        MenuBackdrop.Close();
    }

    void Update()
    {
        if (closing) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (creditsPanel != null && creditsPanel.activeSelf)
            BackFromCredits();
        else
            Resume();
    }

    // ─────────────────────────────────────────────
    // 버튼에서 호출하는 함수 (인스펙터 OnClick()에 연결)
    // ─────────────────────────────────────────────

    public void OpenPause()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        GamePause.Push(GamePause.Menu);
        if (gameplayRestored)              // 이미 열린 상태에서 다시 불려도 잠금이 겹치지 않게
        {
            gameplayRestored = false;
            CombatUIGate.Block();
            MenuBackdrop.Open(GetComponent<Canvas>(), blurRadius, blurIterations, blurDim, blurFadeIn, blurFadeOut, hideOtherUI);
        }
        // [2026-10-10] ?. 는 파괴된 플레이어(사망 후)를 걸러내지 못해 예외가 났다 → 명시적으로 확인
        if (PlayerController.Instance != null) PlayerController.Instance.LockControl(this);

        closing = false;
        if (rootGroup != null)
        {
            rootGroup.DOKill();
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
        }
    }

    public void Resume()
    {
        if (closing) return;
        closing = true;

        // 게임은 누르는 즉시 다시 움직인다 (메뉴가 사라지는 걸 기다리지 않음)
        RestoreGameplay();

        // 메뉴 전체를 짧게 페이드아웃한 뒤 꺼짐 (평소 상태 = 비활성)
        if (rootGroup == null || closeDuration <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }
        rootGroup.blocksRaycasts = false;
        rootGroup.DOKill();
        rootGroup.DOFade(0f, closeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => { if (this != null) gameObject.SetActive(false); });
    }

    public void RestartGame()
    {
        // 씬이 넘어가기 전에 명시적으로 원복 — Time.timeScale과 CombatUIGate는 전역 상태라
        // 씬을 다시 불러와도 저절로 리셋되지 않는다.
        // 예전에는 씬만 다시 불러와서 획득 아이템·재화·통계·맵 진행도가 이전 판 그대로 남았다.
        // 게임오버 "다시하기"와 같은 재시작 함수를 써서 판 데이터까지 함께 초기화한다.
        RunManager.RestartRun();
    }

    public void ShowCredits()
    {
        UIUtil.HidePanel(pausePanel, () =>
        {
            if (creditsPanel != null) creditsPanel.SetActive(true);
        });
    }

    public void BackFromCredits()
    {
        UIUtil.HidePanel(creditsPanel, () =>
        {
            if (pausePanel != null) pausePanel.SetActive(true);
        });
    }

    /// <summary>게임종료 버튼 — 에디터에서는 플레이 모드 종료, 빌드에서는 프로그램 종료.</summary>
    public void QuitGame()
    {
        GamePause.Clear();
        CombatUIGate.Unblock();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
