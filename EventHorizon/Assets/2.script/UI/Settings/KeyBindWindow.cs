using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-11-11 추가] 설정 창 안의 '조작 키' 창. (SettingsCanvas 프리팹의 KeyPanel 에 붙어 있음)
///
/// 사용법: 동작 줄의 키 버튼을 누름 → "키를 누르세요…" → 다음에 누른 키로 바뀜.
///   - Esc: 취소 (설정 창이 같이 닫히지 않게 BlocksEscape 제공)
///   - 방향키·Esc·마우스는 고를 수 없음
///   - 다른 동작이 쓰던 키면 서로 맞바꾸고 아래 안내 줄에 알려 줌
/// 줄·버튼·글자는 프리팹에 만들어 둔 오브젝트이고, 이 스크립트는 연결만 한다.
/// </summary>
public class KeyBindWindow : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        public KeyAction action;
        public Button button;
        public TMP_Text keyText;
    }

    [Tooltip("동작별 줄 (동작 · 키 버튼 · 버튼 안의 글자)")]
    public Row[] rows;
    public Button resetButton;
    public Button backButton;
    [Tooltip("아래 안내 줄 (맞바꿈·취소 등 알림)")]
    public TMP_Text notice;
    [Tooltip("이 창이 열릴 때 숨길 설정 창 본문 (Panel)")]
    public GameObject mainPanel;

    const string HintText = "버튼을 누른 뒤 바꿀 키를 누르세요 · Esc 취소";
    const string WaitText = "키를 누르세요…";

    /// <summary>지금 키 입력을 기다리는 중인지</summary>
    public static bool IsCapturing { get; private set; }
    static int captureEndFrame = -1;

    /// <summary>이번 프레임의 Esc 를 설정 창·메뉴가 처리하면 안 되는지 (기다리는 중이거나 방금 끝남)</summary>
    public static bool BlocksEscape => IsCapturing || Time.frameCount == captureEndFrame;

    static KeyCode[] keyboardKeys;
    int capturing = -1;
    int captureStartFrame = -1;

    void Awake()
    {
        if (keyboardKeys == null)
        {
            var list = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                if (k != KeyCode.None && k < KeyCode.Mouse0) list.Add(k);
            keyboardKeys = list.ToArray();
        }

        if (rows != null)
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i].button != null) rows[i].button.onClick.AddListener(() => Begin(index));
            }
        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
        if (backButton != null) backButton.onClick.AddListener(Hide);
    }

    public bool IsShown => gameObject.activeSelf;

    public void Show()
    {
        gameObject.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(false);
        Refresh();
        SetNotice(HintText);
        Deselect();
    }

    public void Hide()
    {
        Cancel();
        gameObject.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        Deselect();
    }

    void OnDisable()
    {
        if (capturing >= 0) { capturing = -1; IsCapturing = false; captureEndFrame = Time.frameCount; }
    }

    void Begin(int index)
    {
        if (rows == null || index < 0 || index >= rows.Length) return;
        capturing = index;
        IsCapturing = true;
        captureStartFrame = Time.frameCount;   // 버튼을 누른 그 프레임의 입력(Space·Enter 등)은 무시
        Refresh();
        if (rows[index].keyText != null) rows[index].keyText.text = WaitText;
        SetNotice(KeyBindings.ActionName(rows[index].action) + " — " + HintText);
        Deselect();
    }

    void Update()
    {
        if (capturing < 0 || Time.frameCount == captureStartFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            End();
            Refresh();
            SetNotice("취소했습니다");
            return;
        }
        if (!Input.anyKeyDown) return;

        foreach (KeyCode k in keyboardKeys)
        {
            if (!Input.GetKeyDown(k)) continue;
            if (!KeyBindings.IsAllowed(k))
            {
                SetNotice("방향키·Esc 는 바꿀 수 없습니다. 다른 키를 누르세요");
                return;
            }
            Apply(k);
            return;
        }
        // 마우스 클릭 등은 무시 (다른 줄 버튼을 누르면 그 줄로 넘어감)
    }

    void Apply(KeyCode key)
    {
        KeyAction action = rows[capturing].action;
        End();
        if (KeyBindings.Set(action, key, out KeyAction? swapped))
        {
            string msg = KeyBindings.ActionName(action) + " → " + KeyBindings.KeyName(key);
            if (swapped.HasValue)
                msg += "   (" + KeyBindings.ActionName(swapped.Value) + " 키와 맞바꿈 → " + KeyBindings.KeyName(swapped.Value) + ")";
            SetNotice(msg);
            UIClickSound.PlayClick();
        }
        Refresh();
    }

    void End()
    {
        capturing = -1;
        IsCapturing = false;
        captureEndFrame = Time.frameCount;
    }

    void Cancel()
    {
        if (capturing < 0) return;
        End();
        Refresh();
    }

    void ResetAll()
    {
        Cancel();
        KeyBindings.ResetToDefaults();
        Refresh();
        SetNotice("모든 키를 처음 값으로 되돌렸습니다");
    }

    void Refresh()
    {
        if (rows == null) return;
        foreach (var r in rows)
            if (r.keyText != null) r.keyText.text = KeyBindings.KeyName(r.action);
    }

    void SetNotice(string msg)
    {
        if (notice != null) notice.text = msg;
    }

    static void Deselect()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}
