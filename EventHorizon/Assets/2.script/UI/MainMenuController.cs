using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string nextSceneName;

    [Header("UI Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // 버튼 호버/클릭 연출 (DOTween 기반 공용 컴포넌트)
        UIButtonJuice.Attach(startButton);
        UIButtonJuice.Attach(quitButton);
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