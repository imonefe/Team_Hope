using UnityEngine;
using DG.Tweening;

/// <summary>
/// 패널이 SetActive(true)로 켜질 때마다(=OnEnable) 페이드인 + 살짝 스케일업(예: 0.9 → 1)으로
/// 등장하는 연출을, SetActive(false)로 꺼질 때는 반대로 페이드아웃 + 스케일다운 연출을 재생해주는 공용 컴포넌트.
///
/// 붙이는 방법: 인벤토리/아이템 정보 팝업/일시정지·설정/게임오버·크레딧/맵 화면 등,
/// SetActive(true/false)로 열고 닫는 "패널의 루트 오브젝트"에 그대로 붙이면 끝 —
/// 여는 쪽 스크립트(GameMenuController, ItemInfoPopup 등)는 전혀 몰라도 된다.
///
/// 중요: SetActive(false)는 호출 즉시 렌더링을 멈춰버려서, OnDisable 안에서 트윈을 걸어도
/// 화면에는 전혀 보이지 않는다. 그래서 "꺼질 때" 애니메이션이 실제로 보이려면, 닫는 쪽 코드가
/// panel.SetActive(false) 대신 반드시 이 컴포넌트의 Hide()를 호출해야 한다
/// (또는 UIUtil.HidePanel(panel, onComplete) 헬퍼 사용). Hide()가 페이드아웃/스케일다운을
/// 다 재생한 뒤에 실제로 SetActive(false)를 호출해준다.
///
/// CanvasGroup이 없으면 자동으로 추가해서 페이드에 사용한다.
/// Time.timeScale이 0인 상태(일시정지 메뉴 등)에서도 애니메이션이 재생되도록
/// DOTween 트윈에 SetUpdate(true)를 사용한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIPanelAnimator : MonoBehaviour
{
    [Header("등장 애니메이션")]
    public float duration = 0.25f;
    public Ease ease = Ease.OutBack;
    [Range(0.5f, 1f)] public float startScale = 0.9f;
    public bool useFade = true;

    [Header("퇴장 애니메이션")]
    [Tooltip("0 이하면 등장 애니메이션과 같은 duration을 사용")]
    public float hideDuration = -1f;
    public Ease hideEase = Ease.InQuad;

    RectTransform rt;
    CanvasGroup cg;
    bool isHiding;

    /// <summary>지금 퇴장 애니메이션이 재생 중인지 (중복 Hide() 호출 방지용으로 외부에서 참고 가능)</summary>
    public bool IsHiding => isHiding;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (useFade)
        {
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        isHiding = false;
        if (cg != null)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        PlayShowAnimation();
    }

    void OnDisable()
    {
        if (rt != null) rt.DOKill();
        if (cg != null) cg.DOKill();
    }

    void PlayShowAnimation()
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        rt.DOKill();
        rt.localScale = Vector3.one * startScale;
        rt.DOScale(1f, duration).SetEase(ease).SetUpdate(true);

        if (useFade)
        {
            if (cg == null) cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOKill();
                cg.alpha = 0f;
                cg.DOFade(1f, duration).SetUpdate(true);
            }
        }
    }

    /// <summary>
    /// 이미 활성화된 상태에서 다시 등장 연출을 재생하고 싶을 때 호출한다 (예: 퇴장 애니메이션이
    /// 채 끝나기 전에 다시 보여줘야 하는 마우스 추적형 툴팁). 비활성 상태라면 SetActive(true)로
    /// 켜서 OnEnable이 알아서 등장 연출을 재생하게 한다.
    /// </summary>
    public void Show()
    {
        isHiding = false;
        if (cg == null) cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true); // OnEnable에서 PlayShowAnimation() 호출됨
            return;
        }

        PlayShowAnimation();
    }

    /// <summary>
    /// 페이드아웃 + 스케일다운 애니메이션을 재생한 뒤 실제로 SetActive(false)한다.
    /// onComplete는 오브젝트가 실제로 비활성화된 직후에 호출된다(타임스케일 복구, 잠금 해제 등에 사용).
    /// 이미 닫혀있으면 즉시 onComplete만 호출하고, 이미 닫히는 중이면 아무 것도 하지 않는다
    /// (먼저 걸린 Hide() 호출의 onComplete만 실행됨).
    /// </summary>
    public void Hide(System.Action onComplete = null)
    {
        if (this == null || !gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }
        if (isHiding) return;
        isHiding = true;

        if (rt == null) rt = GetComponent<RectTransform>();
        if (useFade && cg == null) cg = GetComponent<CanvasGroup>();

        if (cg != null)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        float d = hideDuration > 0f ? hideDuration : duration;
        bool finished = false;

        void Finish()
        {
            if (finished) return;
            finished = true;
            isHiding = false;
            if (this == null) return;
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        rt.DOKill();
        Tween scaleTween = rt.DOScale(startScale, d).SetEase(hideEase).SetUpdate(true);

        if (useFade && cg != null)
        {
            cg.DOKill();
            cg.DOFade(0f, d).SetEase(hideEase).SetUpdate(true).OnComplete(Finish);
        }
        else
        {
            scaleTween.OnComplete(Finish);
        }
    }
}
