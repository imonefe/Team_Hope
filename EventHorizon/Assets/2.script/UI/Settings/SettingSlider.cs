using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// [2026-10-18 추가] 설정 창의 0~100% 슬라이더 한 개.
///
/// - 이 컴포넌트가 붙은 오브젝트(누르는 영역)를 누르거나 끌면 값이 바뀐다. 이 오브젝트의 Image 는 투명해도 되지만
///   Raycast Target 은 켜져 있어야 한다.
/// - bar(바 전체 영역) 기준으로 위치를 계산하고, fill 은 왼쪽부터 값만큼 차오르며, knob 은 값 위치로 움직인다.
/// - 마우스를 올린 상태에서 ←/→ 키로도 step 만큼 조절된다.
/// - 값이 바뀌면 OnChanged 가 불린다 (SettingsWindow 가 연결).
/// </summary>
public class SettingSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("바 전체 영역 (이 영역의 왼쪽 끝 = 0%, 오른쪽 끝 = 100%)")]
    public RectTransform bar;
    [Tooltip("차오르는 부분 (bar 안에 넣고, 기준점을 왼쪽 아래~왼쪽 위로 늘린 상태)")]
    public RectTransform fill;
    [Tooltip("손잡이 (bar 안에 넣기)")]
    public RectTransform knob;
    [Tooltip("값 글자 (예: 80%)")]
    public TMP_Text valueText;
    [Tooltip("한 칸 단위 (0.05 = 5%씩)")]
    [Range(0.01f, 0.5f)] public float step = 0.05f;

    public float Value { get; private set; }
    public Action<float> OnChanged;

    bool hovered;

    public void SetValue(float v, bool notify)
    {
        v = Mathf.Clamp01(Mathf.Round(v / step) * step);
        bool changed = !Mathf.Approximately(v, Value);
        Value = v;
        Refresh();
        if (notify && changed) OnChanged?.Invoke(Value);
    }

    void Refresh()
    {
        if (fill != null) fill.anchorMax = new Vector2(Value, fill.anchorMax.y);
        if (knob != null)
        {
            knob.anchorMin = new Vector2(Value, knob.anchorMin.y);
            knob.anchorMax = new Vector2(Value, knob.anchorMax.y);
        }
        if (valueText != null) valueText.text = Mathf.RoundToInt(Value * 100f) + "%";
    }

    void FromPointer(PointerEventData e)
    {
        RectTransform r = bar != null ? bar : (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out Vector2 lp)) return;
        float t = Mathf.InverseLerp(r.rect.xMin, r.rect.xMax, lp.x);
        SetValue(t, true);
    }

    public void OnPointerDown(PointerEventData e) => FromPointer(e);
    public void OnDrag(PointerEventData e) => FromPointer(e);
    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;

    void OnDisable() => hovered = false;

    void Update()
    {
        if (!hovered) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SetValue(Value - step, true);
        if (Input.GetKeyDown(KeyCode.RightArrow)) SetValue(Value + step, true);
    }
}
