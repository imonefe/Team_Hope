using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// [2026-10-18 추가] 설정 창의 "◀ 값 ▶" 고르기 한 개 (해상도 고르기에 사용).
///
/// - 이 컴포넌트가 붙은 오브젝트의 왼쪽 절반을 누르면 이전 값, 오른쪽 절반을 누르면 다음 값 (Image 의 Raycast Target 필요).
/// - 마우스를 올린 상태에서 ←/→ 키로도 바뀐다.
/// - 목록 끝에서는 더 넘어가지 않고, 끝에 닿은 쪽 화살표는 흐려진다.
/// - 값이 바뀌면 OnChanged(번호) 가 불린다 (SettingsWindow 가 연결).
/// </summary>
public class SettingStepper : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text valueText;
    [Tooltip("왼쪽 화살표 글자")]
    public TMP_Text prevArrow;
    [Tooltip("오른쪽 화살표 글자")]
    public TMP_Text nextArrow;
    [Range(0f, 1f)] public float disabledArrowAlpha = 0.25f;

    public int Index { get; private set; }
    public Action<int> OnChanged;

    readonly List<string> options = new List<string>();
    bool hovered;

    public void SetOptions(List<string> labels, int index)
    {
        options.Clear();
        options.AddRange(labels);
        Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
        Refresh();
    }

    void Step(int dir)
    {
        int next = Mathf.Clamp(Index + dir, 0, Mathf.Max(0, options.Count - 1));
        if (next == Index) return;
        Index = next;
        UIClickSound.PlayClick();
        Refresh();
        OnChanged?.Invoke(Index);
    }

    void Refresh()
    {
        if (valueText != null) valueText.text = options.Count > 0 ? options[Index] : "-";
        SetAlpha(prevArrow, Index > 0);
        SetAlpha(nextArrow, Index < options.Count - 1);
    }

    void SetAlpha(TMP_Text t, bool enabled)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = enabled ? 1f : disabledArrowAlpha;
        t.color = c;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        RectTransform r = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out Vector2 lp)) return;
        Step(lp.x < r.rect.center.x ? -1 : 1);
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    void OnDisable() => hovered = false;

    void Update()
    {
        if (!hovered) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
    }
}
