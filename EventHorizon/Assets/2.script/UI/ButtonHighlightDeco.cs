using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 버튼이 하이라이트 상태(마우스를 올리거나 키보드로 선택)가 되면
/// '데코' 오브젝트를 그 버튼 위치로 옮겨서 투명도 / 크기 / 위치 애니메이션으로 나타내고,
/// 상태가 풀리면 다시 숨긴다.
///
/// 사용법: 버튼마다 이 컴포넌트를 붙이고, deco 칸에 데코 오브젝트를 넣는다.
///  - 데코 하나를 여러 버튼이 함께 써도 된다 (마우스를 올린 버튼으로 데코가 따라 이동한다).
///  - 인스펙터에서 보이는 모습 = '완성된 모습'. 게임이 시작되면 아래 '시작 상태'에서 출발한다.
///  - 데코의 가로 위치는 인스펙터에 놓아둔 그대로, 세로 위치와 높이는 버튼에 맞춰진다.
/// </summary>
[DisallowMultipleComponent]
public class ButtonHighlightDeco : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("연결")]
    [Tooltip("나타났다 사라질 데코 오브젝트 (여러 버튼이 같은 데코를 넣어도 된다)")]
    public RectTransform deco;

    [Header("크기 / 위치")]
    [Tooltip("켜면 데코의 Height 값을 하이라이트된 버튼의 높이와 똑같이 맞춘다. 데코의 원래 모양을 그대로 쓰려면 꺼둔다")]
    public bool matchButtonHeight = false;
    [Tooltip("데코가 다른 버튼으로 옮겨갈 때 걸리는 시간(초). 0이면 순간이동")]
    public float slideDuration = 0.12f;

    [Header("나타날 때")]
    public float showDuration = 0.25f;
    public Ease showEase = Ease.OutCubic;

    [Header("사라질 때")]
    public float hideDuration = 0.15f;
    public Ease hideEase = Ease.InQuad;

    [Header("시작 상태 (여기서 완성된 모습으로 변한다)")]
    [Range(0f, 1f)] public float startAlpha = 0f;
    public Vector2 startScale = new Vector2(0.3f, 1f);
    public Vector2 startOffset = new Vector2(40f, 0f);

    [Header("글자 이동 (선택)")]
    [Tooltip("하이라이트되면 움직일 글자 오브젝트. 비워두면 글자는 움직이지 않는다")]
    public RectTransform text;
    [Tooltip("하이라이트됐을 때 글자가 이동할 거리. 음수 X = 왼쪽")]
    public Vector2 textOffset = new Vector2(-15f, 0f);
    public float textDuration = 0.2f;
    public Ease textEase = Ease.OutCubic;

    [Header("옵션")]
    [Tooltip("켜면 클릭/키보드로 선택된 동안에도 데코를 유지한다")]
    public bool showWhenSelected = true;

    // 같은 데코를 쓰는 버튼들이 함께 공유하는 상태.
    // (버튼마다 따로 애니메이션하면 서로 덮어쓰므로 데코 하나당 하나만 둔다)
    class Shared
    {
        public RectTransform deco;
        public CanvasGroup group;
        public float baseX;                 // 인스펙터에 놓아둔 가로 위치
        public Vector3 baseScale;           // 인스펙터에 설정해 둔 스케일 = 애니메이션이 끝났을 때의 최종 스케일
        public float progress;              // 0 = 숨김, 1 = 완성
        public float worldY;                // 데코가 지금 서 있는 세로 위치 (월드)
        public bool shown;
        public ButtonHighlightDeco owner;   // 지금 데코를 쓰고 있는 버튼
        public Tween tween;
        public Tween moveTween;
        public int users;
    }

    static readonly Dictionary<RectTransform, Shared> sharedMap = new Dictionary<RectTransform, Shared>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sharedMap.Clear();
    }

    Shared shared;
    RectTransform buttonRect;
    Selectable selectable;
    bool pointerInside;
    bool selected;

    // 글자 이동은 버튼마다 따로 움직인다 (데코와 달리 공유하지 않음)
    Vector2 textBasePos;
    float textProgress;         // 0 = 원래 위치, 1 = 이동 완료
    bool textMoved;
    Tween textTween;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        buttonRect = transform as RectTransform;

        if (text != null) textBasePos = text.anchoredPosition;

        if (deco == null)
        {
            // 데코가 없어도 글자 이동만 쓸 수 있다
            if (text == null)
            {
                Debug.LogWarning("[ButtonHighlightDeco] deco 칸이 비어 있습니다: " + name, this);
                enabled = false;
            }
            return;
        }

        if (!sharedMap.TryGetValue(deco, out shared))
        {
            shared = new Shared();
            shared.deco = deco;
            shared.group = deco.GetComponent<CanvasGroup>();
            if (shared.group == null) shared.group = deco.gameObject.AddComponent<CanvasGroup>();
            shared.group.interactable = false;
            shared.group.blocksRaycasts = false;   // 데코가 마우스 판정을 가로채지 않게
            shared.baseX = deco.anchoredPosition.x;
            shared.baseScale = deco.localScale;
            sharedMap[deco] = shared;
        }
        shared.users++;
    }

    void OnEnable()
    {
        pointerInside = false;
        selected = false;
        ResetText();
        if (shared != null && (shared.owner == null || shared.owner == this)) ResetShared(shared);
    }

    void OnDisable()
    {
        pointerInside = false;
        selected = false;
        ResetText();
        if (shared != null && (shared.owner == null || shared.owner == this)) ResetShared(shared);
    }

    void OnDestroy()
    {
        if (textTween != null) textTween.Kill();
        if (shared == null) return;
        if (shared.owner == this) shared.owner = null;
        shared.users--;
        if (shared.users <= 0)
        {
            if (shared.tween != null) shared.tween.Kill();
            if (shared.moveTween != null) shared.moveTween.Kill();
            if (deco != null) sharedMap.Remove(deco);
        }
    }

    // 버튼의 크기가 바뀔 때(레이아웃 그룹이 정렬을 끝냈을 때 등) 유니티가 자동으로 불러 준다
    void OnRectTransformDimensionsChange()
    {
        if (shared == null || shared.owner != this || !shared.shown) return;
        MatchHeight();
        shared.worldY = ButtonCenterWorldY();
        ApplyShared(shared);
    }

    // ───────── 이벤트 ─────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        Refresh();
    }

    // ───────── 상태 판단 ─────────

    void Refresh()
    {
        bool highlighted = pointerInside || (showWhenSelected && selected);
        bool canUse = selectable == null || selectable.IsInteractable();
        bool on = highlighted && canUse;

        SetTextMoved(on);

        if (shared == null) return;
        if (on) Show();
        else Hide();
    }

    // ───────── 글자 이동 ─────────

    void SetTextMoved(bool on)
    {
        if (text == null || textMoved == on) return;
        textMoved = on;

        if (textTween != null) textTween.Kill();

        float target = on ? 1f : 0f;
        float duration = textDuration * Mathf.Abs(target - textProgress);
        if (duration <= 0f)
        {
            textProgress = target;
            ApplyText();
            return;
        }

        textTween = DOTween.To(() => textProgress, v => { textProgress = v; ApplyText(); }, target, duration)
            .SetEase(textEase)
            .SetUpdate(true);   // 일시정지에서도 재생
    }

    void ApplyText()
    {
        if (text == null) return;
        text.anchoredPosition = textBasePos + textOffset * textProgress;
    }

    void ResetText()
    {
        if (textTween != null) textTween.Kill();
        textProgress = 0f;
        textMoved = false;
        ApplyText();
    }

    // 이 버튼이 데코를 가져와서 보여 준다
    void Show()
    {
        shared.owner = this;        // 이후 애니메이션은 이 버튼의 설정값을 사용
        MatchHeight();
        MoveToButton();

        if (shared.shown)           // 이미 보이는 중이면 위치만 옮기면 됨
        {
            ApplyShared(shared);
            return;
        }

        shared.shown = true;
        StartProgress(1f, showDuration, showEase);
    }

    // 이 버튼이 데코의 주인일 때만 숨긴다 (다른 버튼이 이미 가져갔다면 건드리지 않음)
    void Hide()
    {
        if (shared.owner != this || !shared.shown) return;

        shared.shown = false;
        StartProgress(0f, hideDuration, hideEase);
    }

    // ───────── 위치 / 크기 맞추기 ─────────

    float ButtonCenterWorldY()
    {
        return buttonRect.TransformPoint(buttonRect.rect.center).y;
    }

    // 데코의 세로 길이를 버튼과 똑같이 맞춘다 (가로 방향 설정은 건드리지 않음)
    void MatchHeight()
    {
        if (!matchButtonHeight) return;

        RectTransform d = shared.deco;

        // 세로 앵커와 기준점을 가운데로 모아야 '높이'를 그대로 지정할 수 있다
        Vector2 min = d.anchorMin;
        Vector2 max = d.anchorMax;
        Vector2 pivot = d.pivot;
        min.y = 0.5f;
        max.y = 0.5f;
        pivot.y = 0.5f;
        d.anchorMin = min;
        d.anchorMax = max;
        d.pivot = pivot;

        // 화면에서 보이는 버튼 높이를, 데코가 속한 부모의 단위로 환산
        float worldHeight = buttonRect.rect.height * buttonRect.lossyScale.y;
        float parentScale = d.parent != null ? d.parent.lossyScale.y : 1f;
        float totalScale = parentScale * Mathf.Abs(shared.baseScale.y);

        Vector2 size = d.sizeDelta;
        size.y = totalScale > 0f ? worldHeight / totalScale : buttonRect.rect.height;
        d.sizeDelta = size;
    }

    // 데코의 세로 위치를 이 버튼 정중앙으로. 이미 보이는 중이면 미끄러지듯 이동한다
    void MoveToButton()
    {
        float y = ButtonCenterWorldY();

        if (shared.moveTween != null)
        {
            shared.moveTween.Kill();
            shared.moveTween = null;
        }
        if (Mathf.Approximately(shared.worldY, y)) return;

        bool visible = shared.progress > 0.01f;
        if (!visible || slideDuration <= 0f)
        {
            shared.worldY = y;
            return;
        }

        Shared s = shared;
        s.moveTween = DOTween.To(() => s.worldY, v => { s.worldY = v; ApplyShared(s); }, y, slideDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    // ───────── 애니메이션 ─────────

    void StartProgress(float target, float fullDuration, Ease ease)
    {
        Shared s = shared;
        if (s.tween != null) s.tween.Kill();

        // 중간에 방향이 바뀌어도 남은 거리만큼만 시간을 쓴다 (뚝 끊기지 않음)
        float duration = fullDuration * Mathf.Abs(target - s.progress);
        if (duration <= 0f)
        {
            s.progress = target;
            ApplyShared(s);
            return;
        }

        s.tween = DOTween.To(() => s.progress, v => { s.progress = v; ApplyShared(s); }, target, duration)
            .SetEase(ease)
            .SetUpdate(true);   // 일시정지(Time.timeScale = 0)에서도 재생
    }

    // progress(0~1)에 맞춰 투명도, 크기, 위치를 한 번에 계산한다.
    // 애니메이션의 종류를 바꾸고 싶으면 이 함수만 고치면 된다.
    static void ApplyShared(Shared s)
    {
        if (s.deco == null || s.group == null) return;

        ButtonHighlightDeco o = s.owner;
        if (o == null || s.progress <= 0f)
        {
            s.group.alpha = 0f;     // 숨김
            return;
        }

        float t = s.progress;

        s.group.alpha = Mathf.Clamp01(Mathf.LerpUnclamped(o.startAlpha, 1f, t));

        // 인스펙터에 설정한 스케일(baseScale)이 '완성된 모습'. 그 값에 배율만 곱해서 변화시키므로
        // 애니메이션이 끝나면 정확히 원래 스케일로 돌아온다.
        Vector2 sc = Vector2.LerpUnclamped(o.startScale, Vector2.one, t);
        s.deco.localScale = new Vector3(s.baseScale.x * sc.x, s.baseScale.y * sc.y, s.baseScale.z);

        Vector2 move = Vector2.LerpUnclamped(o.startOffset, Vector2.zero, t);

        // 가로: 인스펙터에 놓아둔 위치 + 이동값
        Vector2 p = s.deco.anchoredPosition;
        p.x = s.baseX + move.x;
        s.deco.anchoredPosition = p;

        // 세로: 하이라이트된 버튼 정중앙(월드 좌표) + 이동값
        Vector3 wp = s.deco.position;
        wp.y = s.worldY;
        s.deco.position = wp;
        if (move.y != 0f)
        {
            p = s.deco.anchoredPosition;
            p.y += move.y;
            s.deco.anchoredPosition = p;
        }
    }

    static void ResetShared(Shared s)
    {
        if (s.tween != null) s.tween.Kill();
        if (s.moveTween != null) s.moveTween.Kill();
        s.progress = 0f;
        s.shown = false;
        s.owner = null;
        if (s.deco != null) s.deco.localScale = s.baseScale;   // 숨겨진 뒤에도 원래 스케일 유지
        ApplyShared(s);
    }
}
