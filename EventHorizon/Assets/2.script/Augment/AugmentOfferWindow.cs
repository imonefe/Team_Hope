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

    [Header("키워드 아이콘 (과열 · 균열 · 잔상 · 연격 · 처형 · 강탈 · 정지 · 과부하 순서)")]
    public Sprite[] keywordIcons = new Sprite[8];

    [Header("글자")]
    public string title = "증강 선택";
    public string roomSubtitle = "전투방 진입 보상 · 하나를 고르세요";
    public string bossSubtitle = "보스 처치 보상 · 하나를 고르세요";
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

    readonly Card[] cards = new Card[CardCount];
    readonly ItemData[] offers = new ItemData[CardCount];
    readonly OwnedRow[] rows = new OwnedRow[8];
    int selected;
    bool bossReward;
    TMP_Text titleText, subtitleText, shardCount;
    RectTransform arrow, ownedPanel;

    /// <summary>창을 닫은 뒤 (골랐든 후보가 없었든) — 감독이 다음 요청을 이어서 처리</summary>
    public event System.Action OnFinished;

    protected override bool UsesShopFrame => false;

    protected override void Build()
    {
        titleText = FindText("Title");
        subtitleText = FindText("Subtitle");
        arrow = Find("Arrow") as RectTransform;
        shardCount = FindText("ShardBox/Count");
        ownedPanel = Find("Owned") as RectTransform;

        for (int i = 0; i < CardCount; i++)
        {
            var t = Find($"Cards/Card_{i}");
            if (t == null) continue;
            var c = new Card { root = (RectTransform)t };
            c.basePos = c.root.anchoredPosition;
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

    // 마우스를 올리면 그 카드를 선택 (누르면 결정)
    void AddHover(GameObject go, int idx)
    {
        var trig = go.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => { if (offers[idx] != null && selected != idx) { selected = idx; RefreshAll(); } });
        trig.triggers.Add(e);
    }

    /// <summary>후보를 뽑아 창을 연다. 후보가 하나도 없으면 열지 않고 false</summary>
    public bool Offer(bool boss)
    {
        bossReward = boss;
        var used = new HashSet<ItemData>();
        int n = 0;
        for (int i = 0; i < CardCount; i++)
        {
            offers[i] = Pick(used, boss ? bossOdds : roomOdds);
            if (offers[i] != null) { used.Add(offers[i]); n++; RunManager.Instance?.HoldItem(offers[i], this); }
        }
        if (n == 0) return false;
        selected = 0;
        while (selected < CardCount - 1 && offers[selected] == null) selected++;
        Open();
        return true;
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
            g.blocksRaycasts = false;   // 다 나타나기 전에는 마우스로도 고르지 않음 (잘못 누름 방지)
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
        // [2026-11-24 변경] 카드가 나오는 중에도 ← → 로 고르기는 바로 됨.
        // 결정 키(Space · Enter · 1 2 3)는 아직 안 보인 카드를 고르는 실수를 막으려고 '남은 카드를 바로 다 보여 주기'로만 씀
        if (introPlaying)
        {
            int prev = selected;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) selected = Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) selected = Step(+1);
            for (int i = 0; i < CardCount; i++)
                if ((Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) && offers[i] != null) { selected = i; SkipIntro(); }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SkipIntro();
            if (selected != prev) RefreshAll();
            return;
        }
        int before = selected;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) selected = Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) selected = Step(+1);
        if (selected != before) RefreshAll();
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
            if (offers[s] != null) return s;
        }
        return selected;
    }

    void Choose(int i)
    {
        if (!IsOpen || i < 0 || i >= CardCount || offers[i] == null) return;
        var d = offers[i];
        var p = Player();
        if (p.stats != null && d.effects != null)
            foreach (var e in d.effects)
                if (e != null) e.Apply(p.stats, p.go.GetComponent<MonoBehaviour>());
        Inventory.Instance?.AddPassive(d);
        RunManager.Instance?.RegisterObtained(d);
        ReleaseAll();
        KillTweens();
        Close();
        if (p.go != null) ItemPickupEffect.Spawn(p.go.transform.position, d);
        YoungbokChatter.OnItemPicked();
        OnFinished?.Invoke();
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
            for (int t = 0; t < 4; t++) if (byTier[t].Count > 0) return byTier[t][Random.Range(0, byTier[t].Count)];
            return null;
        }
        float r = Random.value * total;
        for (int t = 0; t < 4; t++)
        {
            if (byTier[t].Count == 0) continue;
            r -= Odd(odds, t);
            if (r <= 0) return byTier[t][Random.Range(0, byTier[t].Count)];
        }
        for (int t = 3; t >= 0; t--) if (byTier[t].Count > 0 && Odd(odds, t) > 0) return byTier[t][Random.Range(0, byTier[t].Count)];
        return null;
    }

    static float Odd(float[] odds, int t) => odds != null && t < odds.Length ? Mathf.Max(0, odds[t]) : 0;

    Sprite KeywordIcon(AugmentKeyword k) => keywordIcons != null && (int)k < keywordIcons.Length ? keywordIcons[(int)k] : null;

    static Color Hex(string h) => ShopColors.Hex(h);

    // ── 화면 ──
    void RefreshAll()
    {
        SetText(titleText, title);
        SetText(subtitleText, bossReward ? bossSubtitle : roomSubtitle);
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
            SetText(c.name, $"{i + 1}. {d.itemName}");

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
            string lim = AugmentRules.LimitLine(d, 1);
            SetText(c.abilityText, hasAbility ? AugmentRules.Rich(d.abilityText, 1) + (lim.Length > 0 ? "\n" + lim : "") : d.description);
            string lv = AugmentRules.HasInlineLevels(d) ? "" : AugmentRules.LevelLine(d, 1);
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
}
