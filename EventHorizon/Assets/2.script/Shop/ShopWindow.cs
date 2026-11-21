using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 상점 화면 공통 (아이템 상점 · 스킬 상점 · 능력치 강화).
///
/// 상점 캔버스 프리팹(ItemShopCanvas / SkillShopCanvas / StatUpgradeCanvas)의 맨 위 오브젝트에 붙는다.
/// 상점 방 안에서 꺼진 상태로 있다가, NPC(ShopNpcTrigger)에서 F를 누르면 Open() 으로 켜진다.
///
/// 열려 있는 동안
///  - 게임 시간 멈춤 (GamePause), 전투 UI 숨김 (CombatUIGate), 플레이어 조작 잠금
///  - Esc 또는 화면의 "Esc 닫기"를 누르면 닫힘. 같은 Esc로 ESC 메뉴가 같이 열리지 않게 BlocksEscape 제공 (UILoader가 확인)
///  - 위쪽 "보유 재화" 숫자는 RunManager 재화가 바뀔 때마다 자동 갱신
///
/// 화면 안 오브젝트는 이름(경로)으로 찾는다 — 상점 캔버스 생성기(ShopCanvasBuilder)가 만든 이름 그대로.
/// 에디터에서 이름을 바꾸면 콘솔에 "찾지 못함" 경고가 뜬다.
/// </summary>
public abstract class ShopWindow : MonoBehaviour
{
    static int openCount;
    static int closedFrame = -1;

    /// <summary>상점 화면이 열려 있거나 방금(이번 프레임) Esc로 닫혔으면 true — ESC 메뉴가 같이 열리지 않게</summary>
    public static bool BlocksEscape => openCount > 0 || Time.frameCount == closedFrame;

    [Header("인물 대사 (열 때마다 이 중 하나)")]
    [TextArea] public string[] greetings;

    // [2026-11-20 추가] 스토리 대본의 상점 대사 (상점 아래 대사창에 나옴). 비운 칸은 건너뜀
    [Header("인물 (2026-11-20 추가)")]
    [Tooltip("대사창 이름표 (DialogueBox/NpcName). 비우면 프리팹 글자 그대로")]
    public string npcName;
    [Tooltip("처음 열 때 (첫 대면) — 여러 줄이면 줄바꿈으로 이어서 한 번에 보여 줌. 게임을 켠 뒤 한 번 (전시 모드 인트로로 돌아가면 다시)")]
    [TextArea] public string[] firstGreetings;
    [Tooltip("2스테이지부터 열 때 (첫 패배 후). 비우면 평소 대사")]
    [TextArea] public string[] afterDefeatGreetings;
    [Tooltip("사고 나서 (이 중 하나)")]
    [TextArea] public string[] buyLines;
    [Tooltip("재화가 모자랄 때 (이 중 하나)")]
    [TextArea] public string[] poorLines;

    static readonly System.Collections.Generic.HashSet<string> met = new System.Collections.Generic.HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => met.Clear();

    /// <summary>첫 대면 기록을 지운다 (전시 모드 인트로)</summary>
    public static void ResetFirstMeetings() => met.Clear();

    public bool IsOpen { get; private set; }

    protected TMP_Text currencyText;   // [2026-11-21] 증강 강화 창이 파편 수로 바꿔 쓰도록 protected
    TMP_Text dialogueText, npcNameText;
    bool built;

    // ── 열기 / 닫기 ──
    public void Open()
    {
        if (IsOpen) return;
        gameObject.SetActive(true);
        EnsureBuilt();
        IsOpen = true;
        openCount++;
        GamePause.Push(this);
        CombatUIGate.Block();
        if (PlayerController.Instance != null) PlayerController.Instance.LockControl(this);
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnCurrencyChanged += OnCurrency;
            OnCurrency(RunManager.Instance.Amount);
        }
        Greet();
        OnOpened();
    }

    public void Close() => CloseInternal(true);

    void CloseInternal(bool hide)
    {
        if (!IsOpen) return;
        IsOpen = false;
        openCount = Mathf.Max(0, openCount - 1);
        closedFrame = Time.frameCount;
        if (RunManager.Instance != null) RunManager.Instance.OnCurrencyChanged -= OnCurrency;
        GamePause.Pop(this);
        CombatUIGate.Unblock();
        if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
        if (hide) gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // 방이 사라지거나 다른 이유로 꺼져도 멈춤·잠금이 남지 않게
        if (IsOpen) CloseInternal(false);
    }

    // [2026-11-21] 증강 강화 창이 키보드 조작을 더하도록 protected virtual
    protected virtual void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void EnsureBuilt()
    {
        if (built) return;
        built = true;
        if (!UsesShopFrame) { Build(); return; }   // [2026-11-21] 증강 고르기 창: 상점 틀(재화·대사창·Esc) 없음
        currencyText = FindText("Window/HeaderRow/CurrencyAmount");
        dialogueText = FindText("DialogueBox/DialogueText");
        npcNameText = FindText("DialogueBox/NpcName");
        if (npcNameText != null && !string.IsNullOrEmpty(npcName)) npcNameText.text = npcName;
        var esc = Find("Window/BottomRow/EscHint");
        if (esc != null)
        {
            var b = esc.GetComponent<Button>();
            if (b == null)
            {
                var img = esc.GetComponent<Image>();
                if (img == null) img = esc.gameObject.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.01f);
                img.raycastTarget = true;
                b = esc.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
            }
            b.onClick.AddListener(Close);
        }
        Build();
    }

    // [2026-11-20] 열 때 대사: 첫 대면 → 2스테이지부터 → 평소
    void Greet()
    {
        if (dialogueText == null) return;
        string key = GetType().Name;
        if (Has(firstGreetings) && met.Add(key)) { dialogueText.text = string.Join("\n", firstGreetings); return; }
        met.Add(key);
        bool afterDefeat = MapManager.Instance != null && MapManager.Instance.CurrentStageIndex >= 1;
        if (afterDefeat && Has(afterDefeatGreetings)) { Talk(afterDefeatGreetings); return; }
        Talk(greetings);
    }

    static bool Has(string[] a) => a != null && System.Array.Exists(a, s => !string.IsNullOrEmpty(s));

    /// <summary>대사창에 이 중 하나를 띄운다 (비어 있으면 그대로 둠)</summary>
    protected void Talk(string[] pool)
    {
        if (dialogueText == null || !Has(pool)) return;
        string s;
        do s = pool[Random.Range(0, pool.Length)]; while (string.IsNullOrEmpty(s));
        dialogueText.text = s;
    }

    /// <summary>[2026-11-20] 사고 나서 부름</summary>
    protected void OnBought() => Talk(buyLines);

    /// <summary>[2026-11-20] 재화가 모자랄 때 부름 (화면 알림 + 인물 대사)</summary>
    protected void NotEnoughMoney()
    {
        ScreenNotice.Show("재화가 부족합니다");
        Talk(poorLines);
    }

    void OnCurrency(int amount)
    {
        if (currencyText != null && ShowsMoneyInHeader) currencyText.text = amount.ToString("#,0");
        OnCurrencyChanged(amount);
    }

    // ── 자식이 구현 ──
    /// <summary>처음 한 번: 화면 오브젝트 찾기, 버튼 연결</summary>
    protected abstract void Build();
    /// <summary>열 때마다: 목록 채우기, 화면 갱신</summary>
    protected abstract void OnOpened();
    /// <summary>재화가 바뀔 때 (살 수 있는지 버튼 상태 갱신 등)</summary>
    protected virtual void OnCurrencyChanged(int amount) { }
    /// <summary>[2026-11-21] 위쪽 숫자에 재화를 보여 줄지 (증강 강화 창은 false → 증강 파편을 직접 표시)</summary>
    protected virtual bool ShowsMoneyInHeader => true;
    /// <summary>[2026-11-21] 상점 공용 틀(위쪽 재화 · 아래 대사창 · Esc 닫기 글자)을 쓰는지. 증강 고르기 창은 false</summary>
    protected virtual bool UsesShopFrame => true;

    // ── 공용 도우미 ──
    protected static int Money => RunManager.Instance != null ? RunManager.Instance.Amount : 0;

    protected static bool Spend(int price)
    {
        if (RunManager.Instance == null || RunManager.Instance.Amount < price) return false;
        RunManager.Instance.AddCurrency(-price);
        return true;
    }

    protected Transform Find(string path)
    {
        var t = transform.Find(path);
        if (t == null) Debug.LogWarning($"[상점] {name}: '{path}' 를 찾지 못함 — 오브젝트 이름이 바뀌었는지 확인");
        return t;
    }

    protected TMP_Text FindText(string path)
    {
        var t = Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    protected static void SetText(TMP_Text t, string s) { if (t != null) t.text = s; }

    /// <summary>테두리(선 4개짜리 오브젝트) 색 바꾸기</summary>
    protected static void SetBorderColor(Transform border, Color c)
    {
        if (border == null) return;
        foreach (var img in border.GetComponentsInChildren<Image>(true)) img.color = c;
    }

    /// <summary>주 버튼(금색) 상태: 누를 수 있으면 금색, 아니면 회색</summary>
    protected static void SetButton(Button b, bool interactable, string label)
    {
        if (b == null) return;
        b.interactable = interactable;
        var img = b.targetGraphic as Image;
        if (img != null) img.color = interactable ? ShopColors.Gold : ShopColors.Disabled;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.text = label;
            t.color = interactable ? ShopColors.Dark : ShopColors.DisabledText;
        }
    }

    protected static PlayerContext Player()
    {
        var cm = CharacterManager.Instance;
        GameObject go = cm != null ? cm.CurrentCharacter : null;
        if (go == null && PlayerController.Instance != null) go = PlayerController.Instance.gameObject;
        return new PlayerContext(go);
    }

    protected struct PlayerContext
    {
        public readonly GameObject go;
        public readonly Stats stats;
        public PlayerContext(GameObject g)
        {
            go = g;
            stats = g != null ? g.GetComponent<Stats>() : null;
        }
    }
}

/// <summary>상점 화면 공용 색 (시안과 같은 값)</summary>
public static class ShopColors
{
    public static readonly Color Gold = Hex("E3C27E");
    public static readonly Color Dark = Hex("14110E");
    public static readonly Color Disabled = Hex("3A3A3A");
    public static readonly Color DisabledText = Hex("8E8E8E");
    public static readonly Color Line = Hex("3A3A3A");
    public static readonly Color Up = Hex("8FCB8F");
    public static readonly Color Down = Hex("E07A6A");

    public static Color Tier(ItemTier t)
    {
        switch (t)
        {
            case ItemTier.Legendary: return Hex("E39A4B");
            case ItemTier.Epic: return Hex("B08BE0");
            case ItemTier.Rare: return Hex("6FA8DC");
            default: return Hex("B5AEA3");
        }
    }

    public static string TierName(ItemTier t)
    {
        switch (t)
        {
            case ItemTier.Legendary: return "레전더리";
            case ItemTier.Epic: return "유니크";
            case ItemTier.Rare: return "레어";
            default: return "일반";
        }
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
