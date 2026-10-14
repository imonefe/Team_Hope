using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 오브젝트가 켜질 때마다(OnEnable) 투명도 0 → 1 로 페이드인하면서 오른쪽에서 왼쪽으로 들어오는 연출.
/// 게임메뉴 캔버스처럼 SetActive(true)로 켜지는 UI에 붙이면, 켤 때마다 자동으로 다시 재생된다.
///
/// - 투명도는 CanvasGroup으로 조절한다 (없으면 자동으로 붙임). 이미지 원래 색/알파는 건드리지 않는다.
/// - 메뉴가 열리면 Time.timeScale = 0 이 되므로 SetUpdate(true)로 시간 정지 중에도 재생되게 했다.
/// - 꺼질 때(OnDisable) 원래 위치/투명도로 되돌려 놓으므로 다음에 켜질 때도 같은 연출이 나온다.
///
/// 들어오는 방식(mode)
///   Slide : 오른쪽으로 slideDistance 만큼 떨어진 곳에서 원래 자리로 미끄러져 들어옴 (기본)
///   Wipe  : 제자리에서 오른쪽 끝부터 왼쪽으로 그려지듯 드러남 (Image가 있어야 함)
///   Both  : 위 두 가지를 동시에
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIFadeSlideIn : MonoBehaviour
{
    public enum RevealMode { Slide, Wipe, Both }

    [Header("타이밍")]
    [Tooltip("페이드인/이동에 걸리는 시간(초)")]
    public float duration = 0.6f;
    [Tooltip("켜진 뒤 연출 시작까지 기다리는 시간(초)")]
    public float delay = 0f;

    [Header("들어오는 방식")]
    public RevealMode mode = RevealMode.Slide;
    [Tooltip("Slide일 때 시작 위치가 원래 자리보다 오른쪽으로 떨어진 거리(px). 클수록 멀리서 들어옴. 음수면 왼쪽→오른쪽")]
    public float slideDistance = 150f;
    [Tooltip("이동(Slide)/드러남(Wipe) 곡선. OutCubic = 빠르게 들어와서 끝에서 부드럽게 멈춤")]
    public Ease moveEase = Ease.OutCubic;

    [Header("투명도")]
    [Tooltip("투명도 0 → 1 곡선")]
    public Ease fadeEase = Ease.OutQuad;

    RectTransform rt;
    CanvasGroup canvasGroup;
    Image image;
    Vector2 basePos;
    bool baseCaptured;
    Image.Type baseImageType;
    Image.FillMethod baseFillMethod;
    int baseFillOrigin;
    Sequence seq;

    void Awake()
    {
        rt = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        image = GetComponent<Image>();
        CaptureBase();
    }

    void CaptureBase()
    {
        if (baseCaptured) return;
        basePos = rt.anchoredPosition;
        if (image != null)
        {
            baseImageType = image.type;
            baseFillMethod = image.fillMethod;
            baseFillOrigin = image.fillOrigin;
        }
        baseCaptured = true;
    }

    void OnEnable()
    {
        Play();
    }

    void OnDisable()
    {
        ResetToBase();
    }

    /// <summary>연출을 처음부터 다시 재생한다. (코드에서 직접 불러도 됨)</summary>
    public void Play()
    {
        if (rt == null) return;
        CaptureBase();
        if (seq != null) seq.Kill();

        bool useSlide = mode == RevealMode.Slide || mode == RevealMode.Both;
        bool useWipe = (mode == RevealMode.Wipe || mode == RevealMode.Both) && image != null;

        // 시작 상태: 완전 투명 + (오른쪽으로 밀려남 / 오른쪽 끝만 남기고 가려짐)
        canvasGroup.alpha = 0f;
        rt.anchoredPosition = useSlide ? basePos + new Vector2(slideDistance, 0f) : basePos;
        if (useWipe)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Right; // 오른쪽에서부터 채워짐 = 우→좌로 드러남
            image.fillAmount = 0f;
        }

        Sequence s = DOTween.Sequence().SetUpdate(true); // 일시정지(timeScale 0) 중에도 재생
        if (delay > 0f) s.AppendInterval(delay);
        s.Append(canvasGroup.DOFade(1f, duration).SetEase(fadeEase));
        if (useSlide) s.Join(rt.DOAnchorPos(basePos, duration).SetEase(moveEase));
        if (useWipe) s.Join(image.DOFillAmount(1f, duration).SetEase(moveEase));
        s.OnKill(() => { if (seq == s) seq = null; });
        seq = s;
    }

    void ResetToBase()
    {
        if (seq != null) seq.Kill();
        if (rt == null || !baseCaptured) return;
        rt.anchoredPosition = basePos;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (image != null)
        {
            image.fillAmount = 1f;
            image.type = baseImageType;
            image.fillMethod = baseFillMethod;
            image.fillOrigin = baseFillOrigin;
        }
    }
}
