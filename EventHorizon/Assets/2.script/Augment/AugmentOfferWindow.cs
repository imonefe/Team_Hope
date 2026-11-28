using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-21 추가 · 같은 날 시안 ④ 구조로 다시 만듦] 증강 고르기 창 — 3개 중 1개.
/// AugmentOfferCanvas 프리팹(증강 전용 화면) 맨 위 오브젝트에 붙는다. 평소엔 꺼져 있고 AugmentOfferDirector 가 Offer() 로 연다.
///
/// 카드 한 장 = 등급 띠 · 아이콘 · 이름 · 키워드 배지 · 고유능력 이름과 효과 · 강화 값 · Lv ★ · 고르면 바뀌는 키워드 수
/// 아래: 보유 증강 파편(왼쪽) · 조작 안내(가운데) · 보유 키워드와 3·5 단계 칸(오른쪽)
///
///  - 후보: 이번 판에 아직 안 얻었고 바닥·상자·상점에 확보돼 있지 않은 증강 중, 등급 확률로 3개
///  - 확률: 전투방 입장 = 일반 60 · 희귀 30 · 영웅 9 · 전설 1 / 중간보스·보스 = 희귀 40 · 영웅 45 · 전설 15
///  - 고르면 바닥 아이템을 줍는 것과 같은 처리 (효과 적용 + 인벤토리 + 획득 기록 + 획득 연출)
///  - 반드시 하나를 고른다 (Esc로 닫히지 않음)
///  - 조작: ← → 고르기 · Space/Enter 결정 · 1 2 3 바로 결정 · 마우스 (카드에 올리면 선택, 누르면 결정)
/// </summary>
public class AugmentOfferWindow : ShopWindow
{
    public const int CardCount = 3;

    [Header("증강 후보 (아이템 데이터 59종)")]
    public List<ItemData> augmentPool = new List<ItemData>();

    [Header("등급 확률 % (일반 / 희귀 / 영웅 / 전설)")]
    public float[] roomOdds = { 60, 30, 9, 1 };
    public float[] bossOdds = { 0, 40, 45, 15 };
    [Header("[2026-11-25] 첫 상점에 도착하기 전 확률 (전설을 크게 낮춤)")]
    public float[] earlyRoomOdds = { 62, 30, 7.9f, 0.1f };
    public float[] earlyBossOdds = { 0, 45, 53, 2 };
    [Header("[2026-11-25] GEEKS 2026 특전 확률 (전설 없음)")]
    public float[] geeksOdds = { 45, 40, 15, 0 };

    // [2026-11-25] 이번 진행에서 상점 방에 들어간 적이 있는지 (씬을 다시 열면 = 새 진행이면 처음으로)
    static bool reachedShop;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitShopFlag()
    {
        reachedShop = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => reachedShop = false;
        RoomManager.OnRoomLoaded += (room, isStart) => { if (room != null && room.name.Contains("Shop")) reachedShop = true; };
    }
    float[] RoomOddsNow => reachedShop ? roomOdds : earlyRoomOdds;
    float[] BossOddsNow => reachedShop ? bossOdds : earlyBossOdds;
    [Tooltip("[2026-11-25] 가진 키워드와 겹치는 후보의 가중치 (겹치는 키워드 하나당 +이 값, 기본 1). 1.5 → 키워드 하나 겹치면 2.5배 잘 나옴")]
    public float keywordWeight = 1.5f;

    [Header("키워드 아이콘 (과열 · 균열 · 잔상 · 연격 · 처형 · 강탈 · 정지 · 과부하 순서)")]
    public Sprite[] keywordIcons = new Sprite[8];

    [Header("글자")]
    public string title = "모듈 선택";
    public string roomSubtitle = "전투방 진입 보상 · 하나를 고르세요";
    public string bossSubtitle = "보스 처치 보상 · 하나를 고르세요";
    [Tooltip("[2026-11-28] 아직 모듈이 하나도 없을 때(첫 선택) 부제 자리에 나오는 안내")]
    public string firstSubtitle = "같은 키워드를 3개 이상 모으면, 새로운 능력이 발생합니다.";
    [Tooltip("선택한 카드 크기 배율")]
    public float selectedScale = 1.05f;

    class Card
    {
        public RectTransform root;
        public GameObject selectFrame;
        public Image border, header, icon;
        public TMP_Text headerText, name, abilityName, abilityText, levelValues, footerLeft, footerRight;
        public GameObject[] chips = new GameObject[2];
        public Image[] chipIcons = new Image[2];
        public TMP_Text[] chipLabels = new TMP_Text[2];
        public Vector2 basePos;          // [2026-11-24] 등장 연출이 돌아올 원래 자리
        public Tween scaleTween;         // [2026-11-24] 선택 확대 · 축소
        public int shownSel = -1;        // 마지막으로 적용한 선택 상태 (-1 = 아직 없음, 0 = 아님, 1 = 선택)
    }

    class OwnedRow
    {
        public GameObject go;
        public Image icon;
        public TMP_Text name;
        public Image[] pips;
    }

    /// <summary>[2026-11-26] 사이버웨어가 아닌 카드 (다음 방 고르기 · 스탯 카드) — 같은 창 · 같은 연출로 띄운다</summary>
    public class CardView
    {
        public string header;        // 카드 윗띠 글자 (등급 등)
        public Color color = Color.white;   // 윗띠 · 테두리 색
        public Sprite icon;
        public string name;
        public string body;          // 가운데 설명 (리치 텍스트)
        public string footerLeft;    // 아래 왼쪽 (예: 대가)
        public string footerRight;   // 아래 오른쪽
    }

    readonly Card[] cards = new Card[CardCount];
    readonly Vector2[] origPos = new Vector2[CardCount];   // [2026-11-26] 에디터에 놓인 3장 자리 (장 수에 따라 다시 배치)
    CardView[] customCards;                  // null 이면 사이버웨어 카드
    System.Action<int> customChoose;
    string customTitle, customSubtitle;
    bool Custom => customCards != null;
    bool Has(int i) => i >= 0 && i < CardCount && (Custom ? customCards[i] != null : offers[i] != null);
    readonly ItemData[] offers = new ItemData[CardCount];
    readonly OwnedRow[] rows = new OwnedRow[8];
    int selected;
    bool bossReward;
    TMP_Text titleText, subtitleText, shardCount;
    RectTransform arrow, ownedPanel;

    /// <summary>창을 닫은 뒤 (골랐든 후보가 없었든) — 감독이 다음 요청을 이어서 처리</summary>
    public event System.Action OnFinished;

    protected override bool UsesShopFrame => false;

    // ───────── [2026-11-28] Esc = 포기 확인 창 ("모듈 선택을 포기하고 재화(+N)로 변경하시겠습니까?") ─────────
    //  창 모양은 프리팹 AugmentOfferCanvas / GiveUpConfirm (에디터 메뉴 EventHorizon/모듈 포기 확인 창 만들기 로 만듦, 이후 프리팹에서 직접 수정)
    [Header("[2026-11-28] 포기 확인")]
    [Tooltip("포기하면 받는 재화. 0 = 후보 중 가장 높은 등급 모듈을 팔 때 값")]
    public int giveUpShards = 0;
    [Tooltip("{0} 자리에 받는 재화 수")]
    public string giveUpMessage = "모듈 선택을 포기하고 재화(+{0})로 변경하시겠습니까?";
    GameObject giveUpPanel;
    TMP_Text giveUpText;
    bool giveUpOpen;
    int giveUpFrame;

    protected override void Build()
    {
        titleText = FindText("Title");
        subtitleText = FindText("Subtitle");
        arrow = Find("Arrow") as RectTransform;
        shardCount = FindText("ShardBox/Count");
        ownedPanel = Find("Owned") as RectTransform;
        giveUpPanel = Find("GiveUpConfirm")?.gameObject;
        giveUpText = FindText("GiveUpConfirm/Box/Message");
        if (giveUpPanel != null)
        {
            giveUpPanel.SetActive(false);
            var yes = Find("GiveUpConfirm/Box/Yes")?.GetComponent<Button>();
            var no = Find("GiveUpConfirm/Box/No")?.GetComponent<Button>();
            if (yes != null) yes.onClick.AddListener(() => { UIClickSound.PlayClick(); GiveUp(); });
            if (no != null) no.onClick.AddListener(() => { UIClickSound.PlayClick(); HideGiveUp(); });
        }

        for (int i = 0; i < CardCount; i++)
        {
            var t = Find($"Cards/Card_{i}");
            if (t == null) continue;
            var c = new Card { root = (RectTransform)t };
            c.basePos = c.root.anchoredPosition;
            origPos[i] = c.basePos;
            c.selectFrame = t.Find("SelectFrame")?.gameObject;
            c.border = t.Find("Border")?.GetComponent<Image>();
            var body = t.Find("Body");
            c.header = body.Find("Header")?.GetComponent<Image>();
            c.headerText = Txt(body, "Header/HeaderText");
            c.icon = body.Find("Icon")?.GetComponent<Image>();
            c.name = Txt(body, "Name");
            c.abilityName = Txt(body, "AbilityName");
            c.abilityText = Txt(body, "AbilityText");
            c.levelValues = Txt(body, "LevelValues");
            c.footerLeft = Txt(body, "FooterLeft");
            c.footerRight = Txt(body, "FooterRight");
            for (int k = 0; k < 2; k++)
            {
                var ch = body.Find($"Chip_{k}");
                c.chips[k] = ch?.gameObject;
                c.chipIcons[k] = ch?.Find("Icon")?.GetComponent<Image>();
                c.chipLabels[k] = ch != null ? Txt(ch, "Label") : null;
            }
            int idx = i;
            var btn = t.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => Choose(idx));
            AddHover(t.gameObject, idx);
            cards[i] = c;
        }

        for (int r = 0; r < rows.Length; r++)
        {
            var t = Find($"Owned/Row_{r}");
            if (t == null) continue;
            var row = new OwnedRow { go = t.gameObject };
            row.icon = t.Find("Icon")?.GetComponent<Image>();
            row.name = Txt(t, "Name");
            row.pips = new Image[5];
            for (int p = 0; p < 5; p++) row.pips[p] = t.Find($"Pip_{p}")?.GetComponent<Image>();
            rows[r] = row;
        }
    }

    static TMP_Text Txt(Transform root, string path) => root.Find(path)?.GetComponent<TMP_Text>();

    // [2026-11-25] 등장 연출이 끝나 카드가 제자리로 올 때 가만히 있던 커서 아래 카드가 '올라감'으로 선택되던 문제
    //  (첫 엔터 = 연출 건너뛰기 → 선택이 오른쪽 카드로 넘어감) → 마우스를 실제로 움직이기 전에는 올라감을 무시
    bool hoverArmed;
    Vector3 hoverMouseRef;
    void DisarmHover() { hoverArmed = false; hoverMouseRef = Input.mousePosition; }

    void UpdateHoverArm()
    {
        if (hoverArmed || (Input.mousePosition - hoverMouseRef).sqrMagnitude < 16f) return;
        hoverArmed = true;
        for (int i = 0; i < CardCount; i++)
            if (Has(i) && cards[i] != null && UIUtil.MouseOver(cards[i].root) && selected != i) { selected = i; RefreshAll(); break; }
    }

    // 마우스를 올리면 그 카드를 선택 (누르면 결정)
    void AddHover(GameObject go, int idx)
    {
        var trig = go.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => { if (hoverArmed && Has(idx) && selected != idx) { selected = idx; RefreshAll(); } });   // [2026-11-25] 마우스를 실제로 움직인 뒤에만
        trig.triggers.Add(e);
    }

    /// <summary>후보를 뽑아 창을 연다. 후보가 하나도 없으면 열지 않고 false</summary>
    public bool Offer(bool boss)
    {
        customCards = null; customChoose = null;
        bossReward = boss;
        var used = new HashSet<ItemData>();
        int n = 0;
        for (int i = 0; i < CardCount; i++)
        {
            offers[i] = Pick(used, boss ? BossOddsNow : RoomOddsNow);
            if (offers[i] != null) { used.Add(offers[i]); n++; RunManager.Instance?.HoldItem(offers[i], this); }
        }
        if (n == 0) return false;
        selected = 0;
        while (selected < CardCount - 1 && offers[selected] == null) selected++;
        DisarmHover();
        Open();
        return true;
    }

    /// <summary>
    /// [2026-11-26] 사이버웨어가 아닌 카드 1~3장을 같은 창에 띄운다 (다음 방 고르기 · 스탯 카드).
    /// 고르면 창을 닫고 onChoose(번호). 이 창은 건너뛰기가 없어서 반드시 하나를 고른다.
    /// </summary>
    public bool OfferCustom(string titleText_, string subtitleText_, IList<CardView> views, System.Action<int> onChoose)
    {
        if (IsOpen || views == null || views.Count == 0) return false;
        ReleaseAll();
        customCards = new CardView[CardCount];
        int n = 0;
        for (int i = 0; i < CardCount && i < views.Count; i++) { customCards[i] = views[i]; if (views[i] != null) n++; }
        if (n == 0) { customCards = null; return false; }
        customChoose = onChoose;
        customTitle = titleText_;
        customSubtitle = subtitleText_;
        selected = 0;
        while (selected < CardCount - 1 && !Has(selected)) selected++;
        DisarmHover();
        Open();
        return true;
    }

    // 카드 장 수에 맞춰 가운데 기준으로 다시 놓는다 (3장 = 에디터 자리 그대로, 2장 = 그 간격의 절반씩 양옆, 1장 = 가운데)
    void LayoutCards(CardView[] views)
    {
        int n = 0;
        for (int i = 0; i < CardCount; i++) if (views == null || views[i] != null) n++;
        float step = CardCount > 1 ? (origPos[CardCount - 1].x - origPos[0].x) / (CardCount - 1) : 0f;
        float mid = (origPos[0].x + origPos[CardCount - 1].x) * 0.5f;
        int k = 0;
        for (int i = 0; i < CardCount; i++)
        {
            if (cards[i] == null) continue;
            Vector2 p = origPos[i];
            if (views != null && views[i] != null) { p.x = mid + (k - (n - 1) * 0.5f) * step; k++; }
            cards[i].basePos = p;
            cards[i].root.anchoredPosition = p;
        }
    }

    // [2026-11-21 변경 · 11-24 두트윈으로 교체] 열릴 때: 배경 · 제목 · 보유 목록은 바로 보이고,
    // 증강 카드 3장만 왼쪽부터 차례로 위에서 아래로 내려오며 페이드인 (게임이 멈춘 상태라 실제 시간 기준)
    [Header("[2026-11-24] 열릴 때 연출 — 카드 순차 등장 (두트윈)")]
    [Tooltip("카드 한 장이 나타나는 시간(초)")]
    public float cardFadeTime = 0.9f;    // [2026-11-24] 0.35 → 0.9 (더 천천히 나타나게)
    [Tooltip("다음 카드가 나타나기 시작하기까지 간격(초)")]
    public float cardStagger = 0.7f;     // [2026-11-24] 0.12 → 0.7 (카드 사이 간격)
    [Tooltip("카드가 얼마나 위에서 내려오는지 (UI 단위)")]
    public float cardDropDistance = 40f;
    [Header("[2026-11-24] 선택 카드 확대 연출 (두트윈)")]
    [Tooltip("선택한 카드가 커지는 시간(초)")]
    public float selectScaleTime = 0.18f;
    [Tooltip("선택이 풀린 카드가 원래 크기로 줄어드는 시간(초)")]
    public float deselectScaleTime = 0.12f;
    readonly CanvasGroup[] cardGroups = new CanvasGroup[CardCount];
    Sequence introSeq;
    bool introPlaying;

    protected override void OnOpened()
    {
        KillTweens();
        ShowEscHint(!Custom);   // [2026-11-28] 문 고르기 · 스탯 카드는 Esc 로 포기할 수 없으므로 [Esc] 포기 안내를 숨김
        LayoutCards(customCards);   // [2026-11-26] 장 수에 맞춰 자리 (처음 열 때 Build 가 끝난 뒤라 여기서)
        for (int i = 0; i < CardCount; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            c.shownSel = -1;   // 처음 그릴 때는 크기를 바로 맞춤 (연출 없이)
            if (cardGroups[i] == null)
            {
                cardGroups[i] = c.root.GetComponent<CanvasGroup>();   // 에디터에서 카드에 붙여 두면 그걸 씀
                if (cardGroups[i] == null) cardGroups[i] = c.root.gameObject.AddComponent<CanvasGroup>();
            }
        }
        RefreshAll();
        PlayIntro();
    }

    void PlayIntro()
    {
        introPlaying = true;
        introSeq = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < CardCount; i++)
        {
            var c = cards[i];
            var g = cardGroups[i];
            if (c == null || g == null) continue;
            g.alpha = 0f;
            g.blocksRaycasts = true;   // [18:21] 나타나는 중에도 누르면 바로 선택 (예전: 다 나타나기 전엔 막음)
            c.root.anchoredPosition = c.basePos + new Vector2(0f, cardDropDistance);
            float at = cardStagger * i;
            // [2026-11-24 변경] 내려오는 동시에 투명 0 → 1, 제자리에 닿는 순간 100%가 되도록 같은 시간 · 같은 곡선
            introSeq.Insert(at, g.DOFade(1f, cardFadeTime).SetEase(Ease.OutCubic));
            introSeq.Insert(at, c.root.DOAnchorPos(c.basePos, cardFadeTime).SetEase(Ease.OutCubic));
        }
        introSeq.OnComplete(FinishIntro);
    }

    // [2026-11-24] 등장 연출을 끝까지 건너뜀 (남은 카드를 제자리 · 완전히 보이게)
    void SkipIntro()
    {
        if (!introPlaying) return;
        if (introSeq != null) introSeq.Complete();   // OnComplete → FinishIntro
        if (introPlaying) FinishIntro();
    }

    void FinishIntro()
    {
        introPlaying = false;
        introSeq = null;
        DisarmHover();
        for (int i = 0; i < CardCount; i++)
        {
            if (cardGroups[i] != null) { cardGroups[i].alpha = 1f; cardGroups[i].blocksRaycasts = true; }
            if (cards[i] != null && cards[i].root != null) cards[i].root.anchoredPosition = cards[i].basePos;
        }
    }

    void KillTweens()
    {
        if (introSeq != null) { introSeq.Kill(); introSeq = null; }
        introPlaying = false;
        foreach (var c in cards)
            if (c != null && c.scaleTween != null) { c.scaleTween.Kill(); c.scaleTween = null; }
    }

    // [2026-11-24] 선택한 카드는 살짝 튀며 커지고, 선택이 풀린 카드는 부드럽게 원래 크기로
    void ApplySelectScale(Card c, bool sel)
    {
        int state = sel ? 1 : 0;
        if (c.shownSel == state) return;
        bool instant = c.shownSel < 0;
        c.shownSel = state;
        if (c.scaleTween != null) { c.scaleTween.Kill(); c.scaleTween = null; }
        Vector3 target = Vector3.one * (sel ? selectedScale : 1f);
        if (instant) { c.root.localScale = target; return; }
        c.scaleTween = c.root.DOScale(target, sel ? selectScaleTime : deselectScaleTime)
            .SetEase(sel ? Ease.OutBack : Ease.OutQuad)
            .SetUpdate(true);
    }

    // Esc 로 닫히지 않게 base.Update 를 부르지 않는다
    protected override void Update()
    {
        if (!IsOpen) return;
        if (ItemSwapWindow.BlocksInput) return;   // [2026-11-26] 교체 창이 위에 떠 있음
        // [22:00] 사이버웨어 고르기 창: Esc = 이번 보상 건너뛰기 (교체 창 Esc 로 돌아온 뒤 나갈 길).
        //  문 고르기 · 스탯 카드(Custom)는 반드시 골라야 하므로 제외
        // [2026-11-28] Esc = 바로 건너뛰지 않고 포기 확인 창 (예 → 재화를 받고 닫힘 / 아니요 · Esc → 다시 고르기)
        if (giveUpOpen)
        {
            if (Time.frameCount <= giveUpFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.N)) { UIClickSound.PlayClick(); HideGiveUp(); }
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                     || Input.GetKeyDown(KeyCode.Y) || KeyBindings.Down(KeyAction.Interact))
            { UIClickSound.PlayClick(); GiveUp(); }
            return;
        }
        if (!Custom && Input.GetKeyDown(KeyCode.Escape)) { UIClickSound.PlayClick(); AskGiveUp(); return; }
        UpdateHoverArm();
        UpdateShardHover();
        // [2026-11-24 변경] 카드가 나오는 중에도 ← → 로 고르기는 바로 됨.
        // [18:21] 카드가 나오는 중에도 결정 키 · 숫자 키 · 클릭은 바로 선택 (예전: 연출 건너뛰기만 했음)
        int before = selected;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) selected = Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) selected = Step(+1);
        if (selected != before) { RefreshAll(); UIClickSound.PlayClick(); }   // [2026-11-25] 키보드로 고르기 소리
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Choose(selected); return; }
        for (int i = 0; i < CardCount; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Choose(i); return; }
    }

    int Step(int dir)
    {
        int s = selected;
        for (int k = 0; k < CardCount; k++)
        {
            s = (s + dir + CardCount) % CardCount;
            if (Has(s)) return s;
        }
        return selected;
    }

    // [22:00] 아무것도 고르지 않고 닫기 (보상 없음). 같은 Esc 로 ESC 메뉴가 열리지 않는 건 ShopWindow.BlocksEscape 가 막음
    void Skip()
    {
        if (!IsOpen || Custom) return;
        ReleaseAll();
        KillTweens();
        Close();
        HideShardHover();
        CountOffer();
        OnFinished?.Invoke();
    }

    // [2026-11-28] 아래 조작 안내 판(KeyHints)의 [Esc] 포기 칸을 켜고 끔 (판 폭도 맞춤)
    RectTransform hintPanel; float hintFullWidth = -1f;
    void ShowEscHint(bool on)
    {
        if (hintPanel == null)   // 안내 판이 창 바로 아래가 아닐 수 있어 하위 전체에서 찾음
            foreach (var r in GetComponentsInChildren<RectTransform>(true))
                if (r.name == "KeyHints") { hintPanel = r; break; }
        if (hintPanel == null) return;
        var key = hintPanel.Find("Key_Esc") as RectTransform;
        var label = hintPanel.Find("Label_포기") as RectTransform;
        if (key == null || label == null) return;
        if (hintFullWidth < 0f) hintFullWidth = hintPanel.sizeDelta.x;
        key.gameObject.SetActive(on); label.gameObject.SetActive(on);
        float w = on ? hintFullWidth : key.anchoredPosition.x - 26f + 16f;   // 앞 묶음 끝 + 여백 (빌더의 GroupGap 26 · PadX 16)
        hintPanel.sizeDelta = new Vector2(w, hintPanel.sizeDelta.y);
    }

    int GiveUpAmount()
    {
        if (giveUpShards > 0) return giveUpShards;
        int best = 1;
        foreach (var d in offers) if (d != null) best = Mathf.Max(best, AugmentRules.SellShards(d.tier, 1));
        return best;
    }

    void AskGiveUp()
    {
        if (giveUpPanel == null) { Skip(); return; }   // 프리팹에 확인 창이 없으면 예전처럼 바로 건너뜀
        giveUpOpen = true; giveUpFrame = Time.frameCount;
        if (giveUpText != null) giveUpText.text = string.Format(giveUpMessage, GiveUpAmount());
        giveUpPanel.SetActive(true);
        giveUpPanel.transform.SetAsLastSibling();
    }

    void HideGiveUp()
    {
        giveUpOpen = false; giveUpFrame = Time.frameCount;
        if (giveUpPanel != null) giveUpPanel.SetActive(false);
    }

    void GiveUp()
    {
        if (!giveUpOpen) return;
        int n = GiveUpAmount();
        HideGiveUp();
        var rm = RunManager.Instance;
        if (rm != null && n > 0)
        {
            rm.AddShards(n);
            var p = Player();
            if (p.go != null) CurrencyShardFX.Spawn(p.go.transform.position, n * rm.shardFxPerShard, rm.shardColorA, rm.shardColorB);
            ScreenNotice.Show($"모듈 선택 포기 → 모듈 조각 +{n}");
        }
        Skip();
    }

    /// <summary>[2026-11-28] 모듈 고르기 한 번 끝남 (첫 안내를 다음부터 안 띄움). 튜토리얼 체험은 세지 않음</summary>
    static void CountOffer()
    {
        if (!AugmentSandbox.Active && RunManager.Instance != null) RunManager.Instance.ModuleOffersDone++;
    }

    void Choose(int i)
    {
        if (!IsOpen || !Has(i)) return;
        if (ItemSwapWindow.BlocksInput) return;
        if (introPlaying) SkipIntro();   // [18:21] 연출 중에 골라도 카드 자리 · 투명도를 바로 맞춤
        if (Custom)
        {
            GameSfx.AugmentSelect();
            var cb = customChoose;
            customCards = null; customChoose = null;
            KillTweens();
            Close();
            HideShardHover();
            cb?.Invoke(i);
            OnFinished?.Invoke();
            return;
        }
        var d = offers[i];
        GameSfx.AugmentSelect();   // [2026-11-25] 사이버웨어 선택 소리
        var p = Player();
        string gainLine = KeywordGainLine(d);   // 얻기 전 기준으로 미리 (얻은 뒤엔 키워드 수가 바뀜)
        // [2026-11-26] 8칸 제한 — 가득 차 있으면 교체 창.
        // [21:58] 교체 창에서 Esc = 이 창으로 돌아와 다시 고르기 (예전: 포기 = 보상 없음)
        ItemObtain.Request(d, p.go, r =>
        {
            if (r == ItemObtain.Result.Declined && IsOpen) { RefreshAll(); return; }   // 창은 그대로 — 다른 카드를 고를 수 있음
            ReleaseAll();
            KillTweens();
            Close();
            CountOffer();
            if (r != ItemObtain.Result.Declined)
            {
                if (p.go != null) ItemPickupEffect.SpawnAugment(p.go.transform.position, d, gainLine);   // [2026-11-25] 일반 아이템보다 크게
                YoungbokChatter.OnItemPicked();
            }
            HideShardHover();
            OnFinished?.Invoke();
        }, cancelReturns: true);
    }

    // [2026-11-25] 왼쪽 아래 증강 파편 칸에 마우스를 올리면 무엇인지 설명
    bool shardHover;
    void UpdateShardHover()
    {
        var box = shardCount != null ? shardCount.transform.parent as RectTransform : null;
        bool over = UIUtil.MouseOver(box);
        if (over == shardHover) return;
        shardHover = over;
        if (!over) { ItemInfoPopup.Instance?.Hide(); return; }
        var iconImg = box.Find("Icon")?.GetComponent<Image>() ?? box.GetComponentInChildren<Image>();
        ItemInfoPopup.Instance?.ShowInfo(iconImg != null ? iconImg.sprite : null, "모듈 조각",
            "전투에서 얻는 재화입니다.\n상점의 강화 담당에게 가져가면 가진 모듈을 강화할 수 있습니다 (★ 단계가 올라가고 효과가 강해짐).\n모듈을 팔면 일부를 돌려받습니다.",
            $"재화 · 보유 {(RunManager.Instance != null ? RunManager.Instance.Shards : 0)}개", ShopColors.Hex("E3C27E"));
    }

    void HideShardHover()
    {
        if (!shardHover) return;
        shardHover = false;
        ItemInfoPopup.Instance?.Hide();
    }

    /// <summary>[2026-11-25] GEEKS 2026 특전: 고르기 창 없이 무작위 사이버웨어 count 개를 바로 준다 (geeksOdds 등급 확률 — 전설 없음 · 가진 키워드 가중치 그대로). 준 목록을 돌려줌</summary>
    public List<ItemData> GrantRandom(int count)
    {
        var got = new List<ItemData>();
        var used = new HashSet<ItemData>();
        var p = Player();
        for (int i = 0; i < count; i++)
        {
            var d = Pick(used, geeksOdds);
            if (d == null) break;
            used.Add(d);
            // [2026-11-26] 8칸 제한 — 칸이 남으면 바로, 넘치면 교체 창을 차례로 (그건 특전 창 목록에는 안 나옴)
            if (Inventory.Instance != null && Inventory.Instance.IsFull && !ItemObtain.Owns(d)) { ItemObtain.Request(d, p.go, null); continue; }
            ItemObtain.Request(d, p.go, null);
            got.Add(d);
        }
        return got;
    }

    void ReleaseAll()
    {
        for (int i = 0; i < CardCount; i++)
        {
            if (offers[i] != null) RunManager.Instance?.ReleaseItem(offers[i], this);
            offers[i] = null;
        }
    }

    void OnDestroy() { KillTweens(); ReleaseAll(); }

    ItemData Pick(HashSet<ItemData> used, float[] odds)
    {
        var byTier = new List<ItemData>[4];
        for (int t = 0; t < 4; t++) byTier[t] = new List<ItemData>();
        foreach (var it in augmentPool)
        {
            if (it == null || used.Contains(it)) continue;
            if (RunManager.Instance != null && !RunManager.Instance.IsNewItem(it)) continue;
            byTier[(int)it.tier].Add(it);
        }
        float total = 0;
        for (int t = 0; t < 4; t++) if (byTier[t].Count > 0) total += Odd(odds, t);
        if (total <= 0)
        {
            for (int t = 0; t < 4; t++) if (byTier[t].Count > 0) return PickWeighted(byTier[t]);
            return null;
        }
        float r = Random.value * total;
        for (int t = 0; t < 4; t++)
        {
            if (byTier[t].Count == 0) continue;
            r -= Odd(odds, t);
            if (r <= 0) return PickWeighted(byTier[t]);
        }
        for (int t = 3; t >= 0; t--) if (byTier[t].Count > 0 && Odd(odds, t) > 0) return PickWeighted(byTier[t]);
        return null;
    }

    // [2026-11-25] 방금 얻은 사이버웨어의 키워드와 지금 개수 ("과열 2 · 잔상 1")
    static string KeywordGainLine(ItemData d)
    {
        if (d == null || d.keywords == null || d.keywords.Length == 0 || Inventory.Instance == null) return null;
        int[] n = AugmentRules.CountKeywords(Inventory.Instance.passives);
        var parts = new List<string>();
        foreach (var k in d.keywords) parts.Add($"{AugmentRules.KeywordName(k)} {n[(int)k]}");
        return string.Join(" · ", parts);
    }

    // [2026-11-25] 가진 키워드와 겹치는 후보일수록 잘 나옴 — 겹치는 키워드 하나당 가중치 + keywordWeight
    float OfferWeight(ItemData it, int[] owned)
    {
        float w = 1f;
        if (owned == null || it.keywords == null) return w;
        foreach (var k in it.keywords)
            if ((int)k < owned.Length && owned[(int)k] > 0) w += keywordWeight;
        return w;
    }

    ItemData PickWeighted(List<ItemData> list)
    {
        if (list.Count == 0) return null;
        int[] owned = Inventory.Instance != null ? AugmentRules.CountKeywords(Inventory.Instance.passives) : null;
        float total = 0f;
        foreach (var it in list) total += OfferWeight(it, owned);
        float r = Random.value * total;
        foreach (var it in list) { r -= OfferWeight(it, owned); if (r <= 0f) return it; }
        return list[list.Count - 1];
    }

    static float Odd(float[] odds, int t) => odds != null && t < odds.Length ? Mathf.Max(0, odds[t]) : 0;

    Sprite KeywordIcon(AugmentKeyword k) => keywordIcons != null && (int)k < keywordIcons.Length ? keywordIcons[(int)k] : null;

    static Color Hex(string h) => ShopColors.Hex(h);

    // ── 화면 ──
    void RefreshAll()
    {
        if (Custom) { RefreshCustom(); return; }
        if (ownedPanel != null && !ownedPanel.gameObject.activeSelf) ownedPanel.gameObject.SetActive(true);   // 일반 카드 모드에서 꺼 둔 것 (아래 RefreshOwned 가 다시 판단)
        SetText(titleText, title);
        // [2026-11-28] 이번 판 첫 고르기(GEEKS 특전으로 모듈을 이미 가져도) · 튜토리얼 체험이면 규칙 안내
        bool firstPick = AugmentSandbox.Active || RunManager.Instance == null || RunManager.Instance.ModuleOffersDone == 0;
        SetText(subtitleText, firstPick && !string.IsNullOrEmpty(firstSubtitle) ? $"<color=#E3C27E>{firstSubtitle}</color>" : bossReward ? bossSubtitle : roomSubtitle);
        SetText(shardCount, $"× {(RunManager.Instance != null ? RunManager.Instance.Shards : 0)}");

        var owned = Inventory.Instance != null ? Inventory.Instance.passives : null;
        var counts = AugmentRules.CountKeywords(owned);

        for (int i = 0; i < CardCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var d = offers[i];
            c.root.gameObject.SetActive(d != null);
            if (d == null) continue;
            bool sel = i == selected;
            ApplySelectScale(c, sel);
            if (c.selectFrame != null) c.selectFrame.SetActive(sel);

            Color tc = ShopColors.Tier(d.tier);
            if (c.border != null) c.border.color = tc;
            if (c.header != null) c.header.color = tc;
            SetText(c.headerText, $"{Spaced(AugmentRules.TierName(d.tier))}   Lv1 {AugmentRules.Stars(1)}");   // [2026-11-21] 등급 오른쪽에 단계·별
            if (c.icon != null) { c.icon.sprite = d.icon; c.icon.enabled = d.icon != null; }
            SetText(c.name, d.itemName);   // [2026-11-25] 이름 앞 번호 삭제 (숫자키 1 2 3 선택은 그대로)

            // 키워드 배지 (1개면 가운데, 2개면 양옆)
            int kc = d.keywords != null ? Mathf.Min(2, d.keywords.Length) : 0;
            for (int k = 0; k < 2; k++)
            {
                if (c.chips[k] == null) continue;
                bool on = k < kc;
                c.chips[k].SetActive(on);
                if (!on) continue;
                var kw = d.keywords[k];
                if (c.chipIcons[k] != null) c.chipIcons[k].sprite = KeywordIcon(kw);
                if (c.chipLabels[k] != null) { c.chipLabels[k].text = AugmentRules.KeywordName(kw); c.chipLabels[k].color = Hex(AugmentRules.KeywordHex(kw)); }
                var rt = (RectTransform)c.chips[k].transform;
                rt.anchoredPosition = new Vector2(kc == 1 ? 0f : (k == 0 ? -66f : 66f), rt.anchoredPosition.y);
            }

            bool hasAbility = !string.IsNullOrEmpty(d.abilityText);
            SetText(c.abilityName, "");   // [2026-11-21] 고유능력 이름은 보이지 않음 (아이템 이름만)
            if (c.abilityName != null) c.abilityName.gameObject.SetActive(false);
            // [2026-11-23] 증강 서술 규칙: [키워드] 색 · {단계 값} 중 1단계 강조 · 제한 줄. 단계 값이 문장 안에 있으면 '강화' 줄은 비움
            // [2026-11-28] 한 줄 요약 + 세부(지금 단계 값만) + 키워드 뜻 + 고르면 열리는 단계 → AugmentRules.OfferCardBody
            SetText(c.abilityText, hasAbility || !string.IsNullOrEmpty(d.summary) ? AugmentRules.OfferCardBody(d, counts) : d.description);
            string lv = AugmentRules.HasInlineLevels(d) || !string.IsNullOrEmpty(d.summary) ? "" : AugmentRules.LevelLine(d, 1);
            SetText(c.levelValues, string.IsNullOrEmpty(lv) ? "" : $"강화  {lv}");
            SetText(c.footerLeft, "");
            SetText(c.footerRight, KeywordDelta(d, counts));
        }

        // 선택 표시 화살표
        if (arrow != null && selected >= 0 && selected < CardCount && cards[selected] != null)
            arrow.anchoredPosition = new Vector2(cards[selected].basePos.x, arrow.anchoredPosition.y);

        RefreshOwned(counts);
    }

    // "과열 2 → 3" (키워드 색). 단계가 오르면 ▲ 표시
    static string KeywordDelta(ItemData d, int[] counts)
    {
        if (d.keywords == null || d.keywords.Length == 0) return "";
        var parts = new List<string>();
        foreach (var k in d.keywords)
        {
            int b = counts[(int)k], a = b + 1;
            string up = AugmentRules.KeywordTier(a) > AugmentRules.KeywordTier(b) ? "▲" : "";
            parts.Add($"<color=#{AugmentRules.KeywordHex(k)}>{AugmentRules.KeywordName(k)} {b}→{a}{up}</color>");   // 키워드 2개여도 한 줄에 들어가게 짧게
        }
        return string.Join("  ", parts);
    }

    // 오른쪽 아래 '보유 키워드' — 가진 키워드만, 개수만큼 칸을 채움 (3번째·5번째 칸 = 단계 지점, 마름모)
    // 선택한 카드를 고르면 늘어나는 칸은 흐린 키워드 색으로 미리 보여 준다
    void RefreshOwned(int[] counts)
    {
        var sel = selected >= 0 && selected < CardCount ? offers[selected] : null;
        var gain = new int[8];
        if (sel != null && sel.keywords != null) foreach (var k in sel.keywords) gain[(int)k]++;

        // [2026-11-25] 아직 키워드가 하나도 없으면(첫 증강) '보유 키워드' 칸을 띄우지 않음 — 설명 없이 나와 헷갈렸음
        bool anyOwned = false;
        for (int k = 0; k < 8; k++) if (counts[k] > 0) { anyOwned = true; break; }
        if (!anyOwned)
        {
            for (int r = 0; r < rows.Length; r++) if (rows[r] != null) rows[r].go.SetActive(false);
            if (ownedPanel != null) ownedPanel.gameObject.SetActive(false);
            return;
        }

        int shown = 0;
        for (int k = 0; k < 8 && shown < rows.Length; k++)
        {
            if (counts[k] + gain[k] == 0) continue;
            var row = rows[shown];
            if (row == null) { shown++; continue; }
            row.go.SetActive(true);
            var kw = (AugmentKeyword)k;
            Color kc = Hex(AugmentRules.KeywordHex(kw));
            if (row.icon != null) row.icon.sprite = KeywordIcon(kw);
            if (row.name != null) { row.name.text = $"{AugmentRules.KeywordName(kw)} {counts[k]}"; row.name.color = kc; }
            for (int p = 0; p < 5; p++)
            {
                if (row.pips[p] == null) continue;
                if (p < counts[k]) row.pips[p].color = kc;
                else if (p < counts[k] + gain[k]) row.pips[p].color = new Color(kc.r, kc.g, kc.b, 0.45f);
                else row.pips[p].color = ShopColors.Hex("3A3530");
            }
            var rt = (RectTransform)row.go.transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -40f - 26f * shown);
            shown++;
        }
        for (int r = shown; r < rows.Length; r++) if (rows[r] != null) rows[r].go.SetActive(false);
        if (ownedPanel != null)
        {
            ownedPanel.gameObject.SetActive(shown > 0);
            ownedPanel.sizeDelta = new Vector2(ownedPanel.sizeDelta.x, 48f + 26f * shown);
        }
    }

    static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    // [2026-11-26] 일반 카드 모드 (다음 방 · 스탯 카드): 보유 키워드 칸은 숨김
    void RefreshCustom()
    {
        SetText(titleText, customTitle);
        SetText(subtitleText, customSubtitle);
        SetText(shardCount, $"× {(RunManager.Instance != null ? RunManager.Instance.Shards : 0)}");
        if (ownedPanel != null) ownedPanel.gameObject.SetActive(false);
        for (int i = 0; i < CardCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var v = customCards[i];
            c.root.gameObject.SetActive(v != null);
            if (v == null) continue;
            bool sel = i == selected;
            ApplySelectScale(c, sel);
            if (c.selectFrame != null) c.selectFrame.SetActive(sel);
            if (c.border != null) c.border.color = v.color;
            if (c.header != null) c.header.color = v.color;
            SetText(c.headerText, v.header);
            if (c.icon != null) { c.icon.sprite = v.icon; c.icon.enabled = v.icon != null; }
            SetText(c.name, v.name);
            for (int k = 0; k < 2; k++) if (c.chips[k] != null) c.chips[k].SetActive(false);
            SetText(c.abilityName, "");
            if (c.abilityName != null) c.abilityName.gameObject.SetActive(false);
            SetText(c.abilityText, v.body);
            SetText(c.levelValues, "");
            SetText(c.footerLeft, v.footerLeft ?? "");
            SetText(c.footerRight, v.footerRight ?? "");
        }
        if (arrow != null && selected >= 0 && selected < CardCount && cards[selected] != null)
            arrow.anchoredPosition = new Vector2(cards[selected].basePos.x, arrow.anchoredPosition.y);
    }

    /// <summary>[2026-11-26] 증강 파편 칸의 그림 (다음 방 카드 '사이버웨어 방' 아이콘으로 씀)</summary>
    public Sprite ShardIcon
    {
        get
        {
            var box = shardCount != null ? shardCount.transform.parent : (Find("ShardBox"));
            var img = box != null ? box.Find("Icon")?.GetComponent<Image>() : null;
            return img != null ? img.sprite : null;
        }
    }
}
