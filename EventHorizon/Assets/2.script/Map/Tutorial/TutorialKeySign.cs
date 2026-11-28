using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-28] 튜토리얼 배경에 떠 있는 키보드 키 표시 (예: [C] 점프 · [Z][Z] 더블 대시).
///  - 그 구간(step)이 시작되는 순간(영복 통신 대사와 함께) 서서히 나타나고, 구간이 끝나면 사라진다.
///  - 키 글자는 지금 설정된 키를 따라간다 ({Jump} → C, 조정형이면 Space). ← → ↓ + 같은 글자는 그대로.
///  - 그 키를 누르면 키 칸이 잠깐 밝게 번쩍인다 (눌렀다는 확인).
///  - 칸 · 글자 · 위치는 프리팹(TutorialRoom/KeySigns)에서 직접 고치면 된다. 처음 배치는 메뉴 EventHorizon/튜토리얼 키 표시 만들기
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TutorialKeySign : MonoBehaviour
{
    public TutorialController controller;
    [Tooltip("이 구간 번호(0부터)가 진행 중일 때 보임")]
    public int step;
    [Tooltip("칸마다 표시할 키: {Jump} {Dash} {Attack} 같은 동작 이름 (지금 키로 바뀜) 또는 ← → ↓ + 같은 글자")]
    public string[] keys;
    [Tooltip("keys 와 같은 순서의 키 글자 칸")]
    public TMP_Text[] keyTexts;
    [Tooltip("keys 와 같은 순서의 키 바탕 (누르면 번쩍). + 같은 글자 칸은 비워 둠")]
    public Image[] keyBacks;
    [Tooltip("나타나고 사라지는 시간 (초)")]
    public float fadeTime = 0.45f;
    [Tooltip("떠 있는 동안 위아래로 흔들리는 폭")]
    public float bob = 0.06f;
    public Color pressColor = new Color(0.89f, 0.76f, 0.49f, 1f);

    CanvasGroup group;
    Vector3 basePos;
    float[] flash;
    Color[] backBase;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        basePos = transform.localPosition;
        int n = keys != null ? keys.Length : 0;
        flash = new float[n];
        backBase = new Color[n];
        for (int i = 0; i < n; i++)
            if (keyBacks != null && i < keyBacks.Length && keyBacks[i] != null) backBase[i] = keyBacks[i].color;
        if (controller == null) controller = GetComponentInParent<TutorialController>();
    }

    void LateUpdate()
    {
        bool on = controller != null && controller.ActiveStep == step;
        float target = on ? 1f : 0f;
        if (group.alpha != target) group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        if (group.alpha <= 0f) return;

        transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.unscaledTime * 2f) * bob;
        if (keys == null) return;
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i] ?? "";
            KeyAction act;
            bool isAction = TryAction(k, out act);
            if (keyTexts != null && i < keyTexts.Length && keyTexts[i] != null)
            {
                string s = isAction ? KeyBindings.KeyName(act) : k;
                if (keyTexts[i].text != s) keyTexts[i].text = s;
            }
            if (keyBacks == null || i >= keyBacks.Length || keyBacks[i] == null) continue;
            bool pressed = isAction ? KeyBindings.Down(act) : PressedArrow(k);
            if (pressed && on) flash[i] = 1f;
            flash[i] = Mathf.Max(0f, flash[i] - Time.unscaledDeltaTime / 0.25f);
            keyBacks[i].color = Color.Lerp(backBase[i], pressColor, flash[i]);
        }
    }

    static bool TryAction(string k, out KeyAction act)
    {
        act = KeyAction.Jump;
        if (k.Length < 3 || k[0] != '{' || k[k.Length - 1] != '}') return false;
        return System.Enum.TryParse(k.Substring(1, k.Length - 2), out act);
    }

    static bool PressedArrow(string k)
    {
        switch (k)
        {
            case "←": return Input.GetKeyDown(KeyCode.LeftArrow);
            case "→": return Input.GetKeyDown(KeyCode.RightArrow);
            case "↓": return Input.GetKeyDown(KeyCode.DownArrow);
            case "↑": return Input.GetKeyDown(KeyCode.UpArrow);
            default: return false;
        }
    }
}
