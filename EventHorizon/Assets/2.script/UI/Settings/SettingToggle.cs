using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-10-18 추가] 설정 창의 켬/끔 스위치 한 개.
///
/// - 이 컴포넌트가 붙은 오브젝트를 누르면 켬 ↔ 끔이 바뀐다 (Image 의 Raycast Target 필요).
/// - 켜지면 손잡이(knob)가 오른쪽, 꺼지면 왼쪽으로 가고, 바탕(background) 색과 글자가 바뀐다.
/// - 값이 바뀌면 OnChanged 가 불린다 (SettingsWindow 가 연결).
/// </summary>
public class SettingToggle : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("스위치 바탕 (켬/끔에 따라 색이 바뀜)")]
    public Image background;
    [Tooltip("손잡이 (기준점 X 0 = 왼쪽, 1 = 오른쪽으로 움직임)")]
    public RectTransform knob;
    [Tooltip("켬/끔 글자")]
    public TMP_Text stateText;

    public Color onColor = new Color(0.93f, 0.8f, 0.52f, 1f);
    public Color offColor = new Color(0.2f, 0.22f, 0.29f, 1f);
    public string onLabel = "켬";
    public string offLabel = "끔";

    public bool IsOn { get; private set; }
    public Action<bool> OnChanged;

    public void SetValue(bool on, bool notify)
    {
        bool changed = on != IsOn;
        IsOn = on;
        Refresh();
        if (notify && changed) OnChanged?.Invoke(IsOn);
    }

    void Refresh()
    {
        if (background != null) background.color = IsOn ? onColor : offColor;
        if (knob != null)
        {
            float x = IsOn ? 1f : 0f;
            knob.anchorMin = new Vector2(x, knob.anchorMin.y);
            knob.anchorMax = new Vector2(x, knob.anchorMax.y);
            knob.pivot = new Vector2(x, knob.pivot.y);
        }
        if (stateText != null) stateText.text = IsOn ? onLabel : offLabel;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        UIClickSound.PlayClick();
        SetValue(!IsOn, true);
    }
}
