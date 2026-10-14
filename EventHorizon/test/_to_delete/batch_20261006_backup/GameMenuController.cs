using UnityEngine;
using UnityEngine.SceneManagement;

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
/// </summary>
public class GameMenuController : MonoBehaviour
{
    [Header("패널")]
    public GameObject pausePanel;
    public GameObject creditsPanel;

    void Awake()
    {
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
        Time.timeScale = 1f;
        CombatUIGate.Unblock();
        PlayerController.Instance?.ResumeControl();
    }

    void Update()
    {
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
        Time.timeScale = 0f;
        CombatUIGate.Block();
        PlayerController.Instance?.LockControl();
    }

    public void Resume()
    {
        int pending = 0;
        void Finish()
        {
            pending--;
            if (pending <= 0)
                gameObject.SetActive(false); // 평소 상태(비활성)로 되돌림 — OnDisable에서 timeScale/CombatUIGate 자동 복구
        }

        if (pausePanel != null && pausePanel.activeSelf) { pending++; UIUtil.HidePanel(pausePanel, Finish); }
        if (creditsPanel != null && creditsPanel.activeSelf) { pending++; UIUtil.HidePanel(creditsPanel, Finish); }

        if (pending == 0)
            gameObject.SetActive(false);
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
        Time.timeScale = 1f;
        CombatUIGate.Unblock();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
