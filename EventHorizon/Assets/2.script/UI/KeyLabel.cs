using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-11 추가] 키 글자 자동 맞춤. 키 이름을 보여 주는 글자(TMP)에 붙이고 "어느 동작인지"만 고르면,
/// 설정에서 키를 바꿀 때마다 글자가 알아서 바뀐다. (우측 하단 키 안내·조작법 창·스킬 칸 등)
///
/// - format: 키 이름을 끼워 넣을 문장. {0} 자리에 키 이름이 들어간다. 키 이름만이면 "{0}".
/// - fitBox: 켜면 "Shift" 처럼 긴 이름이 상자 밖으로 넘치지 않게 글자 크기를 줄인다 (원래 크기가 최대).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class KeyLabel : MonoBehaviour
{
    public KeyAction action = KeyAction.Jump;
    [Tooltip("{0} 자리에 키 이름이 들어감. 키 이름만 보여 주려면 {0}")]
    public string format = "{0}";
    [Tooltip("긴 키 이름이 상자를 넘치지 않게 글자 크기를 자동으로 줄임")]
    public bool fitBox = true;

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        if (fitBox && text != null && !text.enableAutoSizing)
        {
            float size = text.fontSize;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Min(10f, size);
            text.enableAutoSizing = true;
        }
    }

    void OnEnable()
    {
        KeyBindings.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        KeyBindings.Changed -= Apply;
    }

    void Apply()
    {
        if (text == null) return;
        string keyName = KeyBindings.KeyName(action);
        text.text = string.IsNullOrEmpty(format) ? keyName : string.Format(format, keyName);
    }
}
