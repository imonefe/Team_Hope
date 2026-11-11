using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [2026-11-09] 전시 모드 — 관람객이 만지는 PC에서 사고를 막는다. (씬에 놓을 필요 없음, 게임이 켜지면 스스로 생김)
///
/// 1) 인트로로 올 때마다 설정 초기화
///    관람객이 소리를 꺼 두거나 해상도·흔들림·표시 옵션을 바꿔도, 인트로 화면이 열릴 때 기본값으로 돌아간다.
///    (이미 기본값이면 아무것도 안 함 — 화면이 깜빡이지 않게)
///
/// 2) 종료 막기
///    - "게임 종료" 버튼을 전부 숨긴다 (인트로 메뉴, 게임 중 메뉴).
///    - Alt+F4·창 닫기 등 모든 종료 요청을 거절한다.
///    - 운영자 종료: Ctrl + Shift + Q 를 2초 동안 누르고 있으면 종료.
///
/// 전시가 끝나면 아래 Enabled 를 false 로 바꾸면 전부 꺼진다.
/// </summary>
public class ExhibitionMode : MonoBehaviour
{
    public const bool Enabled = true;            // 전시 모드 켜기/끄기
    const string IntroSceneName = "intro";
    const float QuitHoldTime = 2f;               // 운영자 종료 키를 누르고 있어야 하는 시간
    const float QuitButtonScanInterval = 1f;     // 나중에 생기는 메뉴의 종료 버튼도 숨기려고 주기적으로 다시 찾음

    static ExhibitionMode instance;
    static bool allowQuit;
    float quitHold;
    float scanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        allowQuit = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        return;   // [2026-11-09] 웹 빌드에서는 끔 — 브라우저는 종료를 막을 수 없고, 해상도·전체 화면 강제 변경이 오류를 낼 수 있음
#endif
        if (!Enabled || instance != null) return;
        var go = new GameObject("[ExhibitionMode]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ExhibitionMode>();
        Application.wantsToQuit += WantsToQuit;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // 처음 켜진 씬
    }

    void OnDestroy()
    {
        if (instance != this) return;
        Application.wantsToQuit -= WantsToQuit;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    static bool WantsToQuit()
    {
        if (!allowQuit) Debug.Log("[ExhibitionMode] 종료 요청을 막았습니다 (운영자 종료: Ctrl+Shift+Q 2초)");
        return allowQuit;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && scene.name == IntroSceneName) ResetSettingsIfChanged();
        HideQuitButtons();
    }

    // ── 1) 설정 초기화 ─────────────────────────────
    static void ResetSettingsIfChanged()
    {
        // [2026-11-11] 바꾼 조작 키도 처음 값으로
        if (!KeyBindings.IsDefault()) { KeyBindings.ResetToDefaults(); Debug.Log("[ExhibitionMode] 인트로 진입 — 조작 키를 기본값으로 되돌림"); }

        Resolution native = Screen.currentResolution;
        bool screenDefault = GameSettings.Fullscreen
                          && GameSettings.ResolutionWidth == native.width
                          && GameSettings.ResolutionHeight == native.height;
        bool same = Mathf.Approximately(GameSettings.MasterVolume, GameSettings.DefaultMasterVolume)
                 && Mathf.Approximately(GameSettings.BgmVolume, GameSettings.DefaultBgmVolume)
                 && Mathf.Approximately(GameSettings.SfxVolume, GameSettings.DefaultSfxVolume)
                 && Mathf.Approximately(GameSettings.ShakeScale, GameSettings.DefaultShakeScale)
                 && GameSettings.ShowWeaponInfo && GameSettings.ShowKeyHints && GameSettings.ShowDamageNumbers
#if UNITY_EDITOR
                 ;   // 에디터 게임 화면은 해상도를 못 바꾸므로 화면 값은 비교하지 않음
#else
                 && screenDefault;
#endif
        if (same) return;
        GameSettings.ResetToDefaults();
        Debug.Log("[ExhibitionMode] 인트로 진입 — 설정을 기본값으로 되돌림");
    }

    // ── 2) 종료 막기 ───────────────────────────────
    static readonly FieldInfo MainMenuQuitField =
        typeof(MainMenuController).GetField("quitButton", BindingFlags.NonPublic | BindingFlags.Instance);

    static void HideQuitButtons()
    {
        // 인스펙터 OnClick 에 QuitGame 이 연결된 버튼 (게임 중 메뉴 등)
        foreach (var b in FindObjectsOfType<Button>(true))
        {
            if (b == null || !b.gameObject.activeSelf) continue;
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == "QuitGame") { b.gameObject.SetActive(false); break; }
        }
        // 인트로 메뉴의 종료 버튼 (코드에서 연결하는 방식이라 따로 찾음)
        if (MainMenuQuitField != null)
            foreach (var m in FindObjectsOfType<MainMenuController>(true))
                if (MainMenuQuitField.GetValue(m) is Button qb && qb != null && qb.gameObject.activeSelf)
                    qb.gameObject.SetActive(false);
    }

    void Update()
    {
        // 나중에 만들어지는 메뉴(일시정지 창 등)의 종료 버튼도 숨김
        scanTimer += Time.unscaledDeltaTime;
        if (scanTimer >= QuitButtonScanInterval) { scanTimer = 0f; HideQuitButtons(); }

        // 운영자 종료: Ctrl + Shift + Q 를 2초
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl && shift && Input.GetKey(KeyCode.Q))
        {
            quitHold += Time.unscaledDeltaTime;
            if (quitHold >= QuitHoldTime)
            {
                quitHold = 0f;
                allowQuit = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
        else quitHold = 0f;
    }
}
