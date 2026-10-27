using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-10-26 추가] 화면 하단 중앙에 잠깐 떴다가 아래로 내려가며 사라지는 안내 문구.
/// (예: 적이 남아 있는데 문에 F → "남아있는 적이 있다")
///
/// 사용법: 어디서든 ScreenNotice.Show("문구"); 한 줄이면 된다.
///  - 씬에 미리 배치할 필요 없음. 처음 호출될 때 화면 전체 캔버스를 스스로 만들고, 씬이 바뀌어도 유지된다.
///  - 표시 중에 다시 호출하면 문구를 바꾸고 처음부터 다시 재생한다.
///  - 글꼴은 상호작용 안내(Resources/InteractPrompt 프리팹)의 문구 글꼴을 그대로 가져다 쓴다 (없으면 TMP 기본 글꼴).
///  - 모양·시간 값은 아래 상수에서 조절.
/// </summary>
public class ScreenNotice : MonoBehaviour
{
    // ── 모양 ──────────────────────────────────────────────
    const float FontSize = 34f;
    const float BottomY = 170f;           // 화면 아래에서 문구 중심까지 (기준 해상도 1920×1080 픽셀)
    const float LineWidth = 70f;          // 양옆 금색 선 길이
    const float LineThickness = 2f;
    const float LineGap = 22f;            // 문구와 선 사이 간격
    static readonly Color TextColor = new Color(0.94f, 0.90f, 0.81f, 1f);   // 상호작용 안내와 같은 따뜻한 흰색
    static readonly Color LineColor = new Color(0.85f, 0.68f, 0.35f, 0.9f); // 금색 강조

    // ── 시간 ──────────────────────────────────────────────
    const float AppearTime = 0.15f;       // 나타나는 시간
    const float AppearRise = 10f;         // 나타날 때 아래에서 살짝 올라오는 거리
    const float HoldTime = 1.2f;          // 그대로 머무는 시간
    const float FadeTime = 0.6f;          // 내려가며 사라지는 시간
    const float FadeDrop = 40f;           // 사라지면서 내려가는 거리

    static ScreenNotice instance;

    RectTransform group;
    CanvasGroup canvasGroup;
    TextMeshProUGUI label;
    RectTransform leftLine, rightLine;
    Coroutine playing;

    /// <summary>화면 하단 중앙에 문구를 띄운다.</summary>
    public static void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (instance == null) Create();
        instance.Play(message);
    }

    static void Create()
    {
        var go = new GameObject("ScreenNotice");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenNotice>();

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // 게임 화면 UI 위, 화면 전환 페이드보다는 아래
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 문구 + 양옆 선을 묶는 그룹 (하단 중앙 기준)
        var groupGo = new GameObject("Group", typeof(RectTransform), typeof(CanvasGroup));
        groupGo.transform.SetParent(go.transform, false);
        instance.group = (RectTransform)groupGo.transform;
        instance.group.anchorMin = instance.group.anchorMax = new Vector2(0.5f, 0f);
        instance.group.pivot = new Vector2(0.5f, 0.5f);
        instance.group.sizeDelta = new Vector2(1200f, 60f);
        instance.canvasGroup = groupGo.GetComponent<CanvasGroup>();
        instance.canvasGroup.blocksRaycasts = false;
        instance.canvasGroup.interactable = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(groupGo.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(1000f, 60f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.fontSize = FontSize;
        label.color = TextColor;
        label.raycastTarget = false;
        label.characterSpacing = 4f;

        var promptTemplate = Resources.Load<InteractPrompt>("InteractPrompt");
        if (promptTemplate != null && promptTemplate.labelText != null && promptTemplate.labelText.font != null)
            label.font = promptTemplate.labelText.font;

        // 밝은 배경 위에서도 읽히도록 어두운 테두리
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color32(10, 9, 12, 220);
        instance.label = label;

        instance.leftLine = MakeLine(groupGo.transform, "LineL");
        instance.rightLine = MakeLine(groupGo.transform, "LineR");

        groupGo.SetActive(false);
    }

    static RectTransform MakeLine(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = LineColor;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(LineWidth, LineThickness);
        return rt;
    }

    void Play(string message)
    {
        label.text = message;
        label.ForceMeshUpdate();
        float half = label.preferredWidth * 0.5f;
        leftLine.anchoredPosition = new Vector2(-(half + LineGap + LineWidth * 0.5f), 0f);
        rightLine.anchoredPosition = new Vector2(half + LineGap + LineWidth * 0.5f, 0f);

        if (playing != null) StopCoroutine(playing);
        group.gameObject.SetActive(true);
        playing = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 메뉴 등으로 시간이 멈춰도 자연스럽게 끝나도록 실제 시간 기준
        float t = 0f;
        while (t < AppearTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / AppearTime);
            float e = 1f - (1f - k) * (1f - k);
            canvasGroup.alpha = e;
            group.anchoredPosition = new Vector2(0f, BottomY - AppearRise * (1f - e));
            yield return null;
        }
        canvasGroup.alpha = 1f;
        group.anchoredPosition = new Vector2(0f, BottomY);

        t = 0f;
        while (t < HoldTime) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < FadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeTime);
            canvasGroup.alpha = 1f - k;
            group.anchoredPosition = new Vector2(0f, BottomY - FadeDrop * k * k);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        group.gameObject.SetActive(false);
        playing = null;
    }
}
