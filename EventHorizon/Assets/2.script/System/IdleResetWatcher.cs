using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// [2026-10-21 추가] 게임 중에 일정 시간(기본 3분) 동안 아무 입력이 없으면 판을 초기화하고 인트로(첫 화면)로 돌아간다.
/// (전시·시연용 — 다음 사람이 처음부터 시작할 수 있게)
///
/// - 씬에 따로 배치할 필요 없음: 게임이 시작되면 스스로 만들어지고 씬이 바뀌어도 유지된다.
/// - 입력으로 치는 것: 아무 키·마우스 버튼(누르고 있는 것 포함), 마우스 이동, 휠.
/// - 시간은 실제 시간 기준이라 ESC 메뉴·노드맵처럼 게임이 멈춰 있어도 흐른다.
/// - 인트로 씬에서는 세지 않는다.
/// - 돌아가기 warnSeconds초 전부터 화면 가운데 아래에 남은 시간을 띄우고, 아무 입력이나 하면 취소된다.
/// - 초기화 내용은 게임오버 "다시하기"와 같다 (RunManager.ReturnToIntro).
/// </summary>
public class IdleResetWatcher : MonoBehaviour
{
    [Tooltip("이 시간(초) 동안 입력이 없으면 인트로로 돌아감")]
    public float idleSeconds = 180f;
    [Tooltip("돌아가기 몇 초 전부터 경고를 띄울지 (0 = 경고 없음)")]
    public float warnSeconds = 10f;
    [Tooltip("돌아갈 인트로 씬 이름 (빌드 설정에 들어 있어야 함)")]
    public string introSceneName = "intro";
    [Tooltip("경고 문구. {0} 자리에 남은 초가 들어감")]
    public string warnFormat = "입력이 없어 {0}초 뒤 처음 화면으로 돌아갑니다";

    static IdleResetWatcher instance;

    float idle;
    Vector3 lastMouse;
    bool resetting;
    Canvas warnCanvas;
    TextMeshProUGUI warnText;
    int shownSecond = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[IdleResetWatcher]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<IdleResetWatcher>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        lastMouse = Input.mousePosition;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        idle = 0f;
        resetting = false;
        HideWarning();
    }

    void Update()
    {
        if (resetting) return;

        bool input = HasInput();
        if (SceneManager.GetActiveScene().name == introSceneName)
        {
            idle = 0f;
            HideWarning();
            return;
        }

        if (input)
        {
            idle = 0f;
            HideWarning();
            return;
        }

        idle += Time.unscaledDeltaTime;
        float left = idleSeconds - idle;

        if (left <= 0f)
        {
            resetting = true;
            HideWarning();
            RunManager.ReturnToIntro(introSceneName);
            return;
        }

        if (warnSeconds > 0f && left <= warnSeconds) ShowWarning(Mathf.CeilToInt(left));
    }

    bool HasInput()
    {
        Vector3 mouse = Input.mousePosition;
        bool moved = (mouse - lastMouse).sqrMagnitude > 4f;   // 2픽셀보다 크게 움직였을 때만
        lastMouse = mouse;
        return Input.anyKey || moved || Input.mouseScrollDelta != Vector2.zero;
    }

    // ── 경고 문구 (코드로 만든 화면 맨 위 캔버스) ─────────────
    void ShowWarning(int seconds)
    {
        if (warnCanvas == null) BuildWarning();
        if (!warnCanvas.gameObject.activeSelf) warnCanvas.gameObject.SetActive(true);
        if (seconds != shownSecond)
        {
            shownSecond = seconds;
            warnText.text = string.Format(warnFormat, seconds);
        }
    }

    void HideWarning()
    {
        shownSecond = -1;
        if (warnCanvas != null && warnCanvas.gameObject.activeSelf) warnCanvas.gameObject.SetActive(false);
    }

    void BuildWarning()
    {
        var cgo = new GameObject("IdleWarningCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        warnCanvas = cgo.AddComponent<Canvas>();
        warnCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        warnCanvas.sortingOrder = 1000;   // 메뉴(500)·게임오버(600)보다 위
        var scaler = cgo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 반투명 띠
        var bgGo = new GameObject("Back", typeof(RectTransform));
        var bg = (RectTransform)bgGo.transform;
        bg.SetParent(cgo.transform, false);
        bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0f);
        bg.pivot = new Vector2(0.5f, 0f);
        bg.anchoredPosition = new Vector2(0f, 120f);
        bg.sizeDelta = new Vector2(900f, 64f);
        var img = bgGo.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = false;

        var tgo = new GameObject("Text", typeof(RectTransform));
        var tr = (RectTransform)tgo.transform;
        tr.SetParent(bg, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        warnText = tgo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = FindKoreanFont();
        if (font != null) warnText.font = font;
        warnText.fontSize = 30f;
        warnText.alignment = TextAlignmentOptions.Center;
        warnText.color = new Color(1f, 0.95f, 0.85f, 1f);
        warnText.raycastTarget = false;
        warnText.enableWordWrapping = false;

        cgo.SetActive(false);
    }

    // 게임에서 이미 쓰고 있는(불러와 둔) 한글 글꼴을 찾아 쓴다 (돋움 우선)
    static TMP_FontAsset FindKoreanFont()
    {
        TMP_FontAsset fallback = null;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (f.name.Contains("Dotum")) return f;
            if (fallback == null && (f.name.Contains("Ko") || f.name.Contains("Noto"))) fallback = f;
        }
        return fallback;
    }
}
