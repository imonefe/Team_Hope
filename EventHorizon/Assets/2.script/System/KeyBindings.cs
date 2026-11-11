using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>설정에서 바꿀 수 있는 동작 목록. [2026-11-11] 순서를 바꾸면 씬·프리팹에 저장된 번호가 어긋나므로 맨 뒤에만 추가할 것.</summary>
public enum KeyAction
{
    Jump,       // 점프
    Attack,     // 공격
    Dash,       // 대시
    Skill1,     // 스킬 1 (A 슬롯)
    Skill2,     // 스킬 2 (B 슬롯)
    Skill3,     // 스킬 3 (C 슬롯)
    Pause,      // 퍼즈
    Interact,   // 상호작용 (문·상자·대화)
    Inventory,  // 인벤토리
}

/// <summary>
/// [2026-11-11 추가] 조작 키 저장소. 키를 읽는 곳은 전부 KeyBindings.Get(동작) 을 쓴다.
///
/// - 값은 PlayerPrefs 에 저장되어 게임을 껐다 켜도 유지된다 (GameSettings 와 같은 방식).
/// - Set(): 다른 동작이 이미 그 키를 쓰고 있으면 두 동작의 키를 서로 맞바꾼다 (키 하나에 동작 둘이 겹치지 않게).
/// - 바꿀 수 없는 키: 방향키(이동·두 번 누르기 대시), Esc(메뉴·취소), 마우스·패드 버튼.
/// - 화면 글자는 KeyLabel 이 Changed 를 듣고 알아서 바꾼다.
/// - 전시 모드에서 인트로로 돌아가면 ExhibitionMode 가 ResetToDefaults() 로 되돌린다.
/// </summary>
public static class KeyBindings
{
    static readonly Dictionary<KeyAction, KeyCode> Defaults = new Dictionary<KeyAction, KeyCode>
    {
        { KeyAction.Jump, KeyCode.Space },
        { KeyAction.Attack, KeyCode.R },   // [2026-11-11] E → R
        { KeyAction.Dash, KeyCode.LeftShift },
        { KeyAction.Skill1, KeyCode.Q },
        { KeyAction.Skill2, KeyCode.W },
        { KeyAction.Skill3, KeyCode.E },   // [2026-11-11] 공격과 맞바꿈 R → E
        { KeyAction.Pause, KeyCode.D },
        { KeyAction.Interact, KeyCode.F },
        { KeyAction.Inventory, KeyCode.I },
    };

    static readonly Dictionary<KeyAction, KeyCode> current = new Dictionary<KeyAction, KeyCode>();
    static bool loaded;

    /// <summary>키가 바뀔 때마다 (화면 글자 갱신용)</summary>
    public static event Action Changed;

    const string Prefix = "EH_Key_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;   // 플레이 모드를 다시 시작해도 예전 구독이 남지 않게
        loaded = false;
        current.Clear();
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        current.Clear();
        foreach (var pair in Defaults)
        {
            int saved = PlayerPrefs.GetInt(Prefix + pair.Key, (int)pair.Value);
            KeyCode key = Enum.IsDefined(typeof(KeyCode), saved) ? (KeyCode)saved : pair.Value;
            current[pair.Key] = IsAllowed(key) ? key : pair.Value;
        }
    }

    /// <summary>그 동작의 지금 키</summary>
    public static KeyCode Get(KeyAction action)
    {
        EnsureLoaded();
        return current.TryGetValue(action, out KeyCode k) ? k : Defaults[action];
    }

    /// <summary>그 동작의 키를 이번 프레임에 눌렀는지. 대시는 Shift 를 고르면 왼쪽·오른쪽 Shift 둘 다 인정.</summary>
    public static bool Down(KeyAction action)
    {
        KeyCode k = Get(action);
        if (Input.GetKeyDown(k)) return true;
        KeyCode twin = Twin(k);
        return twin != KeyCode.None && Input.GetKeyDown(twin);
    }

    /// <summary>그 동작의 키를 누르고 있는지 (망치 차지 등)</summary>
    public static bool Held(KeyAction action)
    {
        KeyCode k = Get(action);
        if (Input.GetKey(k)) return true;
        KeyCode twin = Twin(k);
        return twin != KeyCode.None && Input.GetKey(twin);
    }

    // 왼쪽·오른쪽이 같은 이름으로 보이는 키는 둘 다 같은 키로 친다
    static KeyCode Twin(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftShift: return KeyCode.RightShift;
            case KeyCode.RightShift: return KeyCode.LeftShift;
            case KeyCode.LeftControl: return KeyCode.RightControl;
            case KeyCode.RightControl: return KeyCode.LeftControl;
            case KeyCode.LeftAlt: return KeyCode.RightAlt;
            case KeyCode.RightAlt: return KeyCode.LeftAlt;
            default: return KeyCode.None;
        }
    }

    /// <summary>설정에서 고를 수 있는 키인지</summary>
    public static bool IsAllowed(KeyCode key)
    {
        if (key == KeyCode.None || key == KeyCode.Escape) return false;
        if (key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.UpArrow || key == KeyCode.DownArrow) return false;
        if (key >= KeyCode.Mouse0) return false;   // 마우스·패드 버튼
        return true;
    }

    /// <summary>
    /// 그 동작의 키를 바꾼다. 다른 동작이 쓰던 키면 서로 맞바꾸고, 맞바꾼 동작을 swapped 로 돌려준다 (없으면 null).
    /// </summary>
    public static bool Set(KeyAction action, KeyCode key, out KeyAction? swapped)
    {
        swapped = null;
        if (!IsAllowed(key)) return false;
        EnsureLoaded();

        KeyCode old = Get(action);
        if (old == key) return true;

        foreach (var pair in new List<KeyValuePair<KeyAction, KeyCode>>(current))
        {
            if (pair.Key == action) continue;
            if (pair.Value == key || Twin(pair.Value) == key)
            {
                current[pair.Key] = old;
                PlayerPrefs.SetInt(Prefix + pair.Key, (int)old);
                swapped = pair.Key;
                break;
            }
        }
        current[action] = key;
        PlayerPrefs.SetInt(Prefix + action, (int)key);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>모든 키를 처음 값으로</summary>
    public static void ResetToDefaults()
    {
        EnsureLoaded();
        foreach (var pair in Defaults)
        {
            current[pair.Key] = pair.Value;
            PlayerPrefs.DeleteKey(Prefix + pair.Key);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>지금 키가 전부 처음 값인지 (전시 모드 확인용)</summary>
    public static bool IsDefault()
    {
        foreach (var pair in Defaults) if (Get(pair.Key) != pair.Value) return false;
        return true;
    }

    /// <summary>동작 이름 (설정 창 표시용)</summary>
    public static string ActionName(KeyAction action)
    {
        switch (action)
        {
            case KeyAction.Jump: return "점프";
            case KeyAction.Attack: return "공격";
            case KeyAction.Dash: return "대시";
            case KeyAction.Skill1: return "스킬 1";
            case KeyAction.Skill2: return "스킬 2";
            case KeyAction.Skill3: return "스킬 3";
            case KeyAction.Pause: return "퍼즈";
            case KeyAction.Interact: return "상호작용";
            case KeyAction.Inventory: return "인벤토리";
            default: return action.ToString();
        }
    }

    /// <summary>그 동작의 지금 키 이름 (화면 표시용)</summary>
    public static string KeyName(KeyAction action) => KeyName(Get(action));

    /// <summary>키 이름을 짧게 (화면 표시용)</summary>
    public static string KeyName(KeyCode k)
    {
        if (k >= KeyCode.A && k <= KeyCode.Z) return k.ToString();
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "숫자판 " + (int)(k - KeyCode.Keypad0);
        switch (k)
        {
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.Return: case KeyCode.KeypadEnter: return "Enter";
            case KeyCode.Backspace: return "Back";
            case KeyCode.CapsLock: return "Caps";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Backslash: return "\\";
            case KeyCode.Delete: return "Del";
            case KeyCode.Insert: return "Ins";
            case KeyCode.PageUp: return "PgUp";
            case KeyCode.PageDown: return "PgDn";
            default: return k.ToString();
        }
    }
}
