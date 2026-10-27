using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string nextSceneName;

    [Header("UI Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton; // [2026-10-18 추가] 설정 창 열기
    [SerializeField] private Button creditButton;   // [2026-10-18 추가]
    [SerializeField] private Button quitButton;

    // [2026-10-18 추가] 크레딧 창 — 크레딧 버튼으로 열고, 창 아무 곳 클릭 또는 ESC로 닫는다
    [Header("크레딧")]
    [SerializeField] private GameObject creditPanel;
    [Tooltip("누르면 크레딧 창이 닫히는 버튼 (창 배경 전체에 붙여 두면 아무 곳이나 눌러 닫기)")]
    [SerializeField] private Button creditCloseButton;

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (creditButton != null)
            creditButton.onClick.AddListener(OpenCredit);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (creditCloseButton != null)
            creditCloseButton.onClick.AddListener(CloseCredit);

        if (creditPanel != null)
            creditPanel.SetActive(false);

        // 버튼 호버/클릭 연출 (DOTween 기반 공용 컴포넌트)
        UIButtonJuice.Attach(startButton);
        UIButtonJuice.Attach(quitButton);
        if (creditButton != null) UIButtonJuice.Attach(creditButton);
        if (settingsButton != null) UIButtonJuice.Attach(settingsButton);

        // [2026-10-28 추가] 키보드 ↑/↓ 로 버튼 고르기, 엔터·스페이스로 누르기 (크레딧·설정 창이 떠 있을 땐 멈춤)
        KeyboardMenuNav.Attach(gameObject,
            () => startButton != null ? startButton.transform.parent : null,
            () => (creditPanel != null && creditPanel.activeSelf) || SettingsWindow.IsOpen);
    }

    private void Update()
    {
        if (SettingsWindow.BlocksEscape) return;   // [2026-10-18] 설정 창의 ESC 와 겹치지 않게

        // 크레딧 창이 열려 있으면 ESC로 닫기
        if (creditPanel != null && creditPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseCredit();
    }

    /// <summary>[2026-10-18 추가] 설정 창 열기</summary>
    public void OpenSettings()
    {
        SettingsWindow.Open();
    }

    public void OpenCredit()
    {
        if (creditPanel == null) return;
        creditPanel.SetActive(true);
        // 뒤의 메뉴 버튼이 키보드 선택 상태로 남아 있지 않게
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void CloseCredit()
    {
        if (creditPanel == null) return;
        creditPanel.SetActive(false);
    }

    public void StartGame()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("Next scene name is not set.");
            return;
        }

        // 로딩 화면(LoadingScreen)이 씬에 있으면 진행바와 함께 비동기로 전환,
        // 없으면(로딩 화면 프리팹이 아직 안 붙어있는 경우 등) 기존처럼 즉시 전환
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Game Quit");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}