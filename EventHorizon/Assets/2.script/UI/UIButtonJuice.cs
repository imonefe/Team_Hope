using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 버튼(또는 아무 UI 요소)에 붙이면 마우스 오버 시 살짝 확대되고, 클릭 시 펀치 축소되는
/// 공용 인터랙션 연출을 추가해준다. UnityEngine.UI.Button이 없어도(커스텀 클릭 처리 UI든 뭐든)
/// 포인터 이벤트만 받을 수 있으면(레이캐스트 타겟 있는 Graphic) 동작한다.
///
/// 사용법 1 — 코드에서: 각 컨트롤러의 Awake()에서 버튼 하나당 한 줄씩만 호출하면 됨.
///   UIButtonJuice.Attach(startButton);
/// 사용법 2 — 인스펙터에서: 버튼 오브젝트에 이 컴포넌트를 직접 드래그해서 붙여도 동일하게 동작.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("마우스 오버")]
    public float hoverScale = 1.08f;
    public float hoverDuration = 0.15f;

    [Header("클릭 펀치")]
    public float pressScale = 0.92f;
    public float pressDuration = 0.08f;

    [Header("마우스 오버 이동 (선택)")]
    [Tooltip("마우스를 올렸을 때 버튼이 밀려날 거리. (0,0)이면 이동 없음 — 기본값이라 기존 버튼들은 영향 없음. 음수 X = 왼쪽")]
    public Vector2 hoverOffset = Vector2.zero;

    RectTransform rt;
    Vector3 baseScale;
    bool pointerInside;

    // 절대 위치를 저장하지 않고 "지금 얼마나 밀려있는지"만 기억한다.
    // (레이아웃 그룹이 잡아준 위치를 덮어쓰지 않기 위해 차이값만 더하고 뺀다)
    Vector2 appliedOffset;
    Tween offsetTween;

    void Awake()
    {
        rt = transform as RectTransform;
        baseScale = rt != null ? rt.localScale : Vector3.one;
    }

    void OnDisable()
    {
        pointerInside = false;
        if (offsetTween != null) offsetTween.Kill();
        if (rt == null) return;
        rt.DOKill();
        rt.localScale = baseScale;
        if (appliedOffset != Vector2.zero) rt.anchoredPosition -= appliedOffset;
        appliedOffset = Vector2.zero;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        AnimateTo(baseScale * hoverScale, hoverDuration);
        MoveOffsetTo(hoverOffset, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        AnimateTo(baseScale, hoverDuration);
        MoveOffsetTo(Vector2.zero, hoverDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(baseScale * pressScale, pressDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(pointerInside ? baseScale * hoverScale : baseScale, pressDuration);
        MoveOffsetTo(pointerInside ? hoverOffset : Vector2.zero, hoverDuration);
    }

    void AnimateTo(Vector3 targetScale, float duration)
    {
        if (rt == null) return;
        rt.DOKill();
        // SetUpdate(true) — 일시정지 메뉴(Time.timeScale = 0)에서도 버튼 반응이 죽지 않도록
        rt.DOScale(targetScale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    void MoveOffsetTo(Vector2 target, float duration)
    {
        if (rt == null) return;
        // 이동 기능을 안 쓰는 버튼(hoverOffset = 0)은 아무것도 하지 않음
        if (target == appliedOffset && offsetTween == null) return;

        if (offsetTween != null) offsetTween.Kill();
        // rt에 타겟을 걸지 않은 별도 트윈 — AnimateTo의 rt.DOKill()에 같이 죽지 않도록
        Tween t = null;
        t = DOTween.To(() => appliedOffset, SetOffset, target, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnKill(() => { if (offsetTween == t) offsetTween = null; });
        offsetTween = t;
    }

    void SetOffset(Vector2 value)
    {
        if (rt == null) return;
        rt.anchoredPosition += value - appliedOffset;
        appliedOffset = value;
    }

    /// <summary>대상에 UIButtonJuice가 없으면 붙여서 반환한다. 이미 있으면 그걸 그대로 반환.</summary>
    public static UIButtonJuice Attach(Component target)
    {
        if (target == null) return null;
        GameObject go = target.gameObject;
        UIButtonJuice existing = go.GetComponent<UIButtonJuice>();
        return existing != null ? existing : go.AddComponent<UIButtonJuice>();
    }
}
