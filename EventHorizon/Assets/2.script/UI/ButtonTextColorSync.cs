using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 옆에 붙이면, Button 컴포넌트에 이미 설정된 ColorBlock(Normal/Highlighted/Pressed/
/// Selected/Disabled)을 그대로 읽어서 지정한 텍스트(Text 또는 TextMeshProUGUI, 둘 다
/// Graphic이라 상관없음)에도 같은 색을 입혀준다. 배경색과 텍스트색을 따로 설정할 필요가 없고,
/// 나중에 버튼 색을 바꾸면 텍스트 색도 자동으로 같이 따라간다.
///
/// 사용법: Button이 붙어있는 오브젝트에 이 컴포넌트를 추가하고, targetText에 라벨을 드래그.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonTextColorSync : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Tooltip("버튼 색이 바뀔 때 같이 따라갈 텍스트 (Text 또는 TextMeshProUGUI)")]
    public Graphic targetText;

    Button button;
    bool pointerInside;
    bool pointerDown;
    bool selected;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        pointerInside = false;
        pointerDown = false;
        selected = false;
        Refresh(true);
    }

    void OnDisable()
    {
        pointerInside = false;
        pointerDown = false;
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; Refresh(false); }
    public void OnPointerExit(PointerEventData eventData) { pointerInside = false; Refresh(false); }
    public void OnPointerDown(PointerEventData eventData) { pointerDown = true; Refresh(false); }
    public void OnPointerUp(PointerEventData eventData) { pointerDown = false; Refresh(false); }
    public void OnSelect(BaseEventData eventData) { selected = true; Refresh(false); }
    public void OnDeselect(BaseEventData eventData) { selected = false; Refresh(false); }

    /// <summary>버튼의 interactable 값을 런타임에 코드로 바꾼 경우, 이걸 호출해서 색을 갱신할 수 있다.</summary>
    public void Refresh()
    {
        Refresh(false);
    }

    void Refresh(bool instant)
    {
        if (targetText == null || button == null) return;

        ColorBlock colors = button.colors;
        Color target;

        if (!button.interactable) target = colors.disabledColor;
        else if (pointerDown) target = colors.pressedColor;
        else if (selected) target = colors.selectedColor;
        else if (pointerInside) target = colors.highlightedColor;
        else target = colors.normalColor;

        targetText.CrossFadeColor(target, instant ? 0f : colors.fadeDuration, true, true);
    }
}
