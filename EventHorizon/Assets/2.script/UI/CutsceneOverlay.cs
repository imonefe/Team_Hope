using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// [2026-10-15 추가] 컷신용 화면 덮개 — 위아래 검은 띠, 섬광, 암전, 대사창.
/// CutsceneSequencer가 필요할 때 CutsceneOverlay.Get()으로 불러 쓰며, 처음 부를 때 UI를 코드로 직접 만든다.
/// (씬이나 프리팹에 미리 배치할 필요 없음 — 방 프리팹 안에 들어간 컷신도 그대로 동작하게 하려고)
///
/// 그리는 순서: 게임오버 화면(정렬 600)보다 아래인 550. 그래서 게임오버 화면은 항상 덮개 위에 뜬다.
/// 모든 연출은 실제 시간 기준(슬로모션·일시정지 영향 없음).
/// </summary>
public class CutsceneOverlay : MonoBehaviour
{
    const int SortingOrder = 550;

    static CutsceneOverlay instance;

    RectTransform topBar, bottomBar;
    Image flashImage, fadeImage;
    CutsceneDialogueBox dialogueBox;

    /// <summary>덮개를 가져온다. 없으면 새로 만든다.</summary>
    public static CutsceneOverlay Get()
    {
        if (instance == null)
        {
            var go = new GameObject("CutsceneOverlay");
            instance = go.AddComponent<CutsceneOverlay>();
            instance.Build();
        }
        return instance;
    }

    /// <summary>덮개가 이미 있을 때만 띠·섬광·암전을 걷는다 (없으면 아무것도 안 함)</summary>
    public static void HideAllIfExists(float duration)
    {
        if (instance == null) return;
        instance.Letterbox(false, 0.11f, duration);
        instance.Fade(Color.black, 0f, duration);
        if (instance.dialogueBox != null) instance.dialogueBox.Hide();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 순서 = 그리는 순서 (뒤에 만든 것이 위)
        fadeImage = MakeImage("Fade", Color.clear);
        Stretch(fadeImage.rectTransform);

        topBar = MakeImage("LetterboxTop", Color.black).rectTransform;
        bottomBar = MakeImage("LetterboxBottom", Color.black).rectTransform;
        SetBar(topBar, true, 0f);
        SetBar(bottomBar, false, 0f);

        flashImage = MakeImage("Flash", Color.clear);
        Stretch(flashImage.rectTransform);
    }

    Image MakeImage(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // 띠 하나를 화면 위/아래에 h(화면 높이 비율)만큼 걸친다
    static void SetBar(RectTransform rt, bool top, float h)
    {
        rt.anchorMin = new Vector2(0f, top ? 1f - h : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : h);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    float barHeight = 0f;

    /// <summary>위아래 검은 띠를 보이거나 숨긴다</summary>
    public void Letterbox(bool show, float heightRatio, float duration)
    {
        DOTween.Kill(this, complete: false);
        float target = show ? heightRatio : 0f;
        DOTween.To(() => barHeight, v =>
        {
            barHeight = v;
            SetBar(topBar, true, v);
            SetBar(bottomBar, false, v);
        }, target, Mathf.Max(0.01f, duration)).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
    }

    /// <summary>화면 전체를 색으로 번쩍였다가 사라지게 한다</summary>
    public void Flash(Color color, float peak, float duration)
    {
        DOTween.Kill(flashImage);
        var c = color; c.a = peak;
        flashImage.color = c;
        c.a = 0f;
        flashImage.DOColor(c, Mathf.Max(0.01f, duration)).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    /// <summary>화면을 색으로 덮거나(to=1) 걷는다(to=0)</summary>
    public void Fade(Color color, float to, float duration)
    {
        DOTween.Kill(fadeImage);
        var start = fadeImage.color;
        var from = new Color(color.r, color.g, color.b, start.a);
        fadeImage.color = from;
        var end = color; end.a = to;
        fadeImage.DOColor(end, Mathf.Max(0.01f, duration)).SetEase(Ease.InOutSine).SetUpdate(true);
    }

    /// <summary>화면 아래쪽 대사창 (없으면 만든다).
    /// speakerFont: [2026-10-15] 이름표 글꼴 — 비워두면 대사 글꼴을 굵게 그린다 (이미 굵은 글꼴을 주면 그대로 씀)</summary>
    public CutsceneDialogueBox GetDialogueBox(Font font, Font speakerFont = null)
    {
        if (dialogueBox != null) return dialogueBox;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var box = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        box.transform.SetParent(transform, false);
        // 띠보다 위, 섬광보다 아래
        box.transform.SetSiblingIndex(flashImage.transform.GetSiblingIndex());
        var rt = (RectTransform)box.transform;
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(1320f, 190f);
        rt.anchoredPosition = new Vector2(0f, 150f);
        var bg = box.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.07f, 0.78f);
        bg.raycastTarget = false;

        // 이름표 위 금색 선
        var line = MakeChildImage(box.transform, "Line", new Color(0.86f, 0.72f, 0.42f, 0.9f));
        var lrt = line.rectTransform;
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f); lrt.sizeDelta = new Vector2(0f, 3f); lrt.anchoredPosition = Vector2.zero;

        var speaker = speakerFont != null
            ? MakeText(box.transform, "Speaker", speakerFont, 34, new Color(0.93f, 0.8f, 0.5f), FontStyle.Normal)
            : MakeText(box.transform, "Speaker", font, 34, new Color(0.93f, 0.8f, 0.5f), FontStyle.Bold);
        var srt = speaker.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0f, 1f); srt.sizeDelta = new Vector2(-80f, 50f); srt.anchoredPosition = new Vector2(40f, -16f);

        var body = MakeText(box.transform, "Line", font, 38, new Color(0.96f, 0.95f, 0.92f), FontStyle.Normal);
        var brt = body.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(40f, 20f); brt.offsetMax = new Vector2(-40f, -70f);

        // CanvasGroup이 먼저 붙어 있어야 CutsceneDialogueBox.Awake()가 찾아서 투명하게 시작한다
        dialogueBox = box.AddComponent<CutsceneDialogueBox>();
        dialogueBox.speakerText = speaker;
        dialogueBox.lineText = body;
        return dialogueBox;
    }

    static Image MakeChildImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    static Text MakeText(Transform parent, string name, Font font, int size, Color color, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAnchor.UpperLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    void OnDestroy()
    {
        DOTween.Kill(this);
        if (flashImage != null) DOTween.Kill(flashImage);
        if (fadeImage != null) DOTween.Kill(fadeImage);
        if (instance == this) instance = null;
    }
}
