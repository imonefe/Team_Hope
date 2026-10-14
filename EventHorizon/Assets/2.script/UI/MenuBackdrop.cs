using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일시정지 메뉴(ESC)가 열려 있는 동안의 "배경" 처리.
///  1) 뒤의 게임 화면을 흐리게 (블러 세기를 서서히 올리고 내림 → 실제 블러는 HitGlitchScreenFeature 가 MenuBlurScreen 셰이더로 그림)
///  2) 메뉴보다 아래에 그려지는 다른 캔버스(전투 UI, 보스 체력바, 월드 UI 등)를 "그리기만" 꺼서 숨김
///     → canvas.enabled 만 끄므로 그 캔버스의 스크립트는 그대로 동작한다. 메뉴가 닫히면 다시 켠다.
///     화면 전환·로딩 캔버스(이름에 Transition / Loading 이 들어간 것)는 건드리지 않는다.
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 스스로 생성된다.
/// GameMenuController 가 열 때 Open(), 닫을 때 Close() 를 부른다. 값은 GameMenuController 인스펙터에서 조절.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    static MenuBackdrop instance;

    // ── 렌더러 기능이 읽어 가는 값 ──
    /// <summary>블러 세기 (0~1)</summary>
    public static float BlurAmount { get; private set; }
    /// <summary>블러를 그려야 하는지</summary>
    public static bool BlurActive => BlurAmount > 0.001f;
    /// <summary>블러 반경 배율 (반복마다 샘플 간격에 곱함)</summary>
    public static float BlurRadius { get; private set; } = 1.5f;
    /// <summary>블러 반복 횟수 (많을수록 넓게 흐려짐)</summary>
    public static int BlurIterations { get; private set; } = 4;
    /// <summary>뒤 화면을 어둡게 하는 정도 (0~1)</summary>
    public static float BlurDim { get; private set; } = 0.25f;

    static bool open;
    static float fadeInTime = 0.25f;
    static float fadeOutTime = 0.12f;
    static readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        open = false;
        BlurAmount = 0f;
        hiddenCanvases.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("[MenuBackdrop]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MenuBackdrop>();
    }

    /// <summary>메뉴가 열릴 때: 블러 시작 + 다른 캔버스 숨김</summary>
    public static void Open(Canvas menuCanvas, float radius, int iterations, float dim, float fadeIn, float fadeOut, bool hideOtherUI)
    {
        open = true;
        BlurRadius = Mathf.Max(0f, radius);
        BlurIterations = Mathf.Clamp(iterations, 0, 8);
        BlurDim = Mathf.Clamp01(dim);
        fadeInTime = fadeIn;
        fadeOutTime = fadeOut;
        if (hideOtherUI) HideOtherCanvases(menuCanvas);
    }

    /// <summary>메뉴가 닫힐 때: 블러 서서히 끄기 + 숨겼던 캔버스 다시 켜기</summary>
    public static void Close()
    {
        open = false;
        RestoreCanvases();
    }

    static void HideOtherCanvases(Canvas menuCanvas)
    {
        RestoreCanvases();   // 혹시 남아 있던 것 먼저 정리
        int menuOrder = menuCanvas != null ? menuCanvas.sortingOrder : int.MaxValue;

        foreach (Canvas c in FindObjectsOfType<Canvas>())
        {
            if (c == null || !c.enabled || !c.isRootCanvas) continue;
            if (menuCanvas != null && (c == menuCanvas || c.transform.IsChildOf(menuCanvas.transform))) continue;

            string n = c.gameObject.name;
            if (n.Contains("Transition") || n.Contains("Loading")) continue;   // 화면 전환·로딩은 유지

            // 화면 위 UI 중 메뉴보다 위에 그려지는 것은 유지 (메뉴보다 높은 순서 = 메뉴 위에 떠야 하는 것)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= menuOrder) continue;

            c.enabled = false;
            hiddenCanvases.Add(c);
        }
    }

    static void RestoreCanvases()
    {
        for (int i = 0; i < hiddenCanvases.Count; i++)
        {
            if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = true;
        }
        hiddenCanvases.Clear();
    }

    void Update()
    {
        float target = open ? 1f : 0f;
        float time = open ? fadeInTime : fadeOutTime;
        float speed = time > 0f ? 1f / time : 1000f;
        BlurAmount = Mathf.MoveTowards(BlurAmount, target, speed * Time.unscaledDeltaTime);   // 일시정지 중에도 동작
    }
}
