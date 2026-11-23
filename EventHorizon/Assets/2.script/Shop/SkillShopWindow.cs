using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 스킬 상점 화면 (SkillShopCanvas 맨 위 오브젝트에 붙음).
///
///  - 처음 열 때, 지금 캐릭터가 갖고 있지 않은 일반 스킬을 최대 4개 진열한다 (스킬이 4개보다 적으면 남는 카드는 숨김).
///  - 구매: 재화 차감 → 빈 스킬 칸(Q→W 순서, CharacterSkills.AcquireSkill)에 넣는다.
///    칸이 가득 차 있으면 사지 않고 "스킬 칸이 가득 찼습니다" 안내 (교체 규칙은 기획이 정해지면 추가).
///  - 새로고침: 비용을 내고 진열 순서를 다시 섞는다.
///  - [2026-11-16] 가진 스킬 팔기: 누르면 판매 모드 — 카드에 지금 가진 일반 스킬(무기 스킬 칸 제외)이 나오고,
///    팔면 그 칸이 비고 재화(등급별 가격 × sellRate)를 받는다.
///
/// [2026-11-16 수정] 스킬 등급 추가 — 등급 확률(tierOdds)로 진열 순서를 뽑고, 등급별 가격(tierPrices)·카드 색·등급 글자를 쓴다.
/// </summary>
public class SkillShopWindow : ShopWindow
{
    [Header("진열 후보 (상점 생성기가 스킬 줍기 프리팹이 쓰는 스킬을 자동으로 채움)")]
    public List<Skill> skillPool = new List<Skill>();

    [Header("등급별 가격 (일반 / 레어 / 유니크 / 레전더리) — 임시값")]
    public int[] tierPrices = { 60, 90, 130, 180 };

    [Header("등급별 진열 확률 % (일반 / 레어 / 유니크 / 레전더리) — 임시값")]
    public float[] tierOdds = { 50, 35, 15, 0 };

    [Header("판매가 = 등급별 가격 × 이 비율 — 임시값")]
    [Range(0f, 1f)] public float sellRate = 0.4f;

    [Header("새로고침 — 임시값")]
    public int refreshCost = 20;
    public int refreshCostStep = 10;

    const int SlotCount = 4;

    class Card
    {
        public Transform root;
        public Button select, buy;
        public Image gradeBar, icon;
        public GameObject iconPlaceholder, selectFrame;
        public Transform gradeRing, badgeBorder;
        public TMP_Text badge, itemName, desc, price;
    }

    readonly Card[] cards = new Card[SlotCount];
    readonly Skill[] offers = new Skill[SlotCount];
    readonly bool[] sold = new bool[SlotCount];
    int selected = -1;
    int refreshCount;
    bool stocked;

    TMP_Text dName, dGrade, dFlavor, dEffects, dStats, odds;
    Button refreshBtn, sellBtn;

    // 판매 모드: 카드 0·1 = 스킬 칸 1·2 (0번 무기 스킬 칸은 팔지 않음)
    bool selling;

    protected override void Build()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var root = Find($"Window/Cards/Card_{i}");
            if (root == null) continue;
            var c = new Card { root = root };
            c.select = root.GetComponent<Button>();
            c.buy = root.Find("BuyButton")?.GetComponent<Button>();
            c.gradeBar = root.Find("GradeBar")?.GetComponent<Image>();
            c.icon = root.Find("IconSlot/Icon")?.GetComponent<Image>();
            c.iconPlaceholder = root.Find("IconSlot/IconPlaceholder")?.gameObject;
            c.gradeRing = root.Find("IconSlot/GradeRing");
            c.badgeBorder = root.Find("GradeBadge/Border");
            c.badge = root.Find("GradeBadge/Label")?.GetComponent<TMP_Text>();
            c.itemName = root.Find("ItemName")?.GetComponent<TMP_Text>();
            c.desc = root.Find("Description")?.GetComponent<TMP_Text>();
            c.price = root.Find("PriceRow/Price")?.GetComponent<TMP_Text>();
            c.selectFrame = root.Find("SelectFrame")?.gameObject;
            int idx = i;
            if (c.select != null) c.select.onClick.AddListener(() => Select(idx));
            if (c.buy != null) c.buy.onClick.AddListener(() => Buy(idx));
            cards[i] = c;
        }

        dName = FindText("Window/DetailStrip/Col_Selected/DetailName");
        dGrade = FindText("Window/DetailStrip/Col_Selected/DetailGrade");
        dFlavor = FindText("Window/DetailStrip/Col_Selected/DetailFlavor");
        dEffects = FindText("Window/DetailStrip/Col_Effects/DetailEffects");
        dStats = FindText("Window/DetailStrip/Col_Stats/DetailStats");
        odds = FindText("Window/BottomRow/OddsText");

        refreshBtn = Find("Window/BottomRow/RefreshButton")?.GetComponent<Button>();
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => { if (!selling) Refresh(); });
        sellBtn = Find("Window/BottomRow/SellButton")?.GetComponent<Button>();
        if (sellBtn != null) sellBtn.onClick.AddListener(() => { selling = !selling; selected = 0; RefreshAll(); });
    }

    protected override void OnOpened()
    {
        if (!stocked) { Restock(); stocked = true; }
        selling = false;   // 열 때는 항상 구매 화면부터
        RefreshAll();
    }

    protected override void OnCurrencyChanged(int amount) => RefreshAll();

    CharacterSkills Skills()
    {
        var p = Player();
        if (p.go == null) return null;
        var cs = p.go.GetComponent<CharacterSkills>();
        return cs != null ? cs : p.go.GetComponentInParent<CharacterSkills>();
    }

    bool Owned(Skill s)
    {
        var cs = Skills();
        if (cs == null || s == null) return false;
        for (int i = 0; i < cs.skillSlots.Length; i++) if (cs.skillSlots[i] == s) return true;
        return false;
    }

    void Restock()
    {
        var pool = new List<Skill>();
        foreach (var s in skillPool)
            if (s != null && s.skillType == Skill.SkillType.Normal && !Owned(s) && !pool.Contains(s)) pool.Add(s);
        // 칸마다 등급 확률로 등급을 뽑고 그 등급에서 하나 (그 등급이 비었으면 남은 등급 중에서)
        for (int i = 0; i < SlotCount; i++) { offers[i] = PickByTier(pool); sold[i] = false; if (offers[i] != null) pool.Remove(offers[i]); }
        selected = offers[0] != null ? 0 : -1;
    }

    Skill PickByTier(List<Skill> pool)
    {
        if (pool.Count == 0) return null;
        float total = 0;
        for (int t = 0; t < 4; t++) if (pool.Exists(s => (int)s.tier == t)) total += Odd(t);
        if (total <= 0) return pool[Random.Range(0, pool.Count)];
        float r = Random.value * total;
        for (int t = 0; t < 4; t++)
        {
            var group = pool.FindAll(s => (int)s.tier == t);
            if (group.Count == 0) continue;
            r -= Odd(t);
            if (r <= 0) return group[Random.Range(0, group.Count)];
        }
        return pool[Random.Range(0, pool.Count)];
    }

    float Odd(int t) => tierOdds != null && t < tierOdds.Length ? Mathf.Max(0, tierOdds[t]) : 0;
    int PriceOf(Skill s) => s == null ? 0 : (tierPrices != null && (int)s.tier < tierPrices.Length ? tierPrices[(int)s.tier] : 0);
    int SellPriceOf(Skill s) => Mathf.RoundToInt(PriceOf(s) * sellRate);

    // [2026-11-20] 판매 모드에서 보이는 카드 수 = 일반 스킬 칸 수 (지금 1칸)
    const int SellCards = CharacterSkills.SlotCount - 1;

    // 판매 모드에서 카드 i 가 가리키는 스킬 (카드 0 → 칸 1)
    Skill OwnedAt(int i)
    {
        var cs = Skills();
        int slot = i + 1;
        if (cs == null || i >= SellCards || slot >= cs.skillSlots.Length) return null;
        return cs.skillSlots[slot];
    }

    void Sell(int i)
    {
        var s = OwnedAt(i);
        var cs = Skills();
        if (s == null || cs == null) return;
        cs.SetSkill(i + 1, null);   // 그 칸을 비운다
        RunManager.Instance?.AddCurrency(SellPriceOf(s));
        RefreshAll();
    }

    // [2026-11-21] 키보드: ← → 고르기 · Space/Enter 구매(판매 화면이면 판매) · R 새로고침 · Tab 판매 화면 전환 · Esc 닫기
    protected override void Update()
    {
        base.Update();
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelect(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSelect(+1);
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && selected >= 0) Buy(selected);
        if (Input.GetKeyDown(KeyCode.R) && !selling) Refresh();
        if (Input.GetKeyDown(KeyCode.Tab)) { selling = !selling; selected = 0; RefreshAll(); }
    }

    void MoveSelect(int dir)
    {
        int n = selling ? SellCards : SlotCount;
        if (n <= 0) return;
        int s = selected < 0 ? 0 : selected;
        for (int k = 0; k < n; k++)
        {
            s = (s + dir + n) % n;
            bool has = selling ? OwnedAt(s) != null : offers[s] != null;
            if (has) { selected = s; RefreshAll(); return; }
        }
    }

    void Select(int i)
    {
        if (selling) { if (OwnedAt(i) != null) { selected = i; RefreshAll(); } return; }
        if (offers[i] == null) return;
        selected = i;
        RefreshAll();
    }

    void Buy(int i)
    {
        if (selling) { Sell(i); return; }
        var s = offers[i];
        if (s == null || sold[i]) return;
        var cs = Skills();
        if (cs == null) return;
        if (!HasFreeSlot(cs)) { ScreenNotice.Show("스킬 칸이 가득 찼습니다"); return; }
        int price = PriceOf(s);
        if (!Spend(price)) { NotEnoughMoney(); return; }
        if (!cs.AcquireSkill(s))
        {
            RunManager.Instance?.AddCurrency(price);   // 혹시 실패하면 돌려준다
            ScreenNotice.Show("스킬 칸이 가득 찼습니다");
            return;
        }
        sold[i] = true;
        RefreshAll();
        OnBought();   // [2026-11-20] 인물 대사
    }

    static bool HasFreeSlot(CharacterSkills cs)
    {
        for (int slot = 1; slot < CharacterSkills.SlotCount && slot < cs.skillSlots.Length; slot++)
            if (cs.skillSlots[slot] == null) return true;
        return false;
    }

    void Refresh()
    {
        int cost = refreshCost + refreshCount * refreshCostStep;
        if (!Spend(cost)) { NotEnoughMoney(); return; }
        refreshCount++;
        Restock();
        RefreshAll();
    }

    void RefreshAll()
    {
        if (selling) { RefreshSell(); return; }
        int money = Money;
        var cs = Skills();
        bool free = cs != null && HasFreeSlot(cs);
        if (sellBtn != null) Label(sellBtn, "<size=80%><color=#E3C27E>[Tab]</color></size> 가진 스킬 팔기");   // [2026-11-23] 단축키 표시
        if (odds != null)
            odds.text = $"일반 {tierOdds[0]:0}% · 레어 {tierOdds[1]:0}% · 유니크 {tierOdds[2]:0}%" + (tierOdds[3] > 0 ? $" · 레전더리 {tierOdds[3]:0}%" : "");

        for (int i = 0; i < SlotCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var s = offers[i];
            c.root.gameObject.SetActive(s != null || i == 0);   // 진열할 스킬이 없으면 첫 카드에만 안내
            if (c.selectFrame != null) c.selectFrame.SetActive(i == selected);
            if (s == null)
            {
                SetText(c.itemName, "진열할 스킬 없음");
                SetText(c.desc, "");
                SetText(c.badge, "-");
                SetText(c.price, "-");
                SetButton(c.buy, false, "-");
                continue;
            }
            Color tc = ShopColors.Tier(s.tier);
            if (c.gradeBar != null) c.gradeBar.color = tc;
            SetBorderColor(c.gradeRing, tc);
            SetBorderColor(c.badgeBorder, tc);
            if (c.badge != null) { c.badge.text = ShopColors.TierName(s.tier); c.badge.color = tc; }
            SetText(c.itemName, s.skillName);
            SetText(c.desc, s.description);
            if (c.icon != null) { c.icon.sprite = s.icon; c.icon.color = s.icon != null ? Color.white : new Color(1, 1, 1, 0); }
            if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(s.icon == null);
            int price = PriceOf(s);
            SetText(c.price, sold[i] ? "-" : price.ToString("#,0"));
            if (sold[i]) SetButton(c.buy, false, "구매 완료");
            else if (!free) SetButton(c.buy, false, "칸 가득 참");
            else SetButton(c.buy, money >= price, money >= price ? "구매" : "재화 부족");
        }

        var sel = selected >= 0 ? offers[selected] : null;
        SetText(dName, sel != null ? sel.skillName : "-");
        if (dGrade != null) { dGrade.text = sel != null ? ShopColors.TierName(sel.tier) + " 스킬" : ""; if (sel != null) dGrade.color = ShopColors.Tier(sel.tier); }
        SetText(dFlavor, sel != null ? sel.description : "");
        SetText(dEffects, sel != null ? $"재사용 대기 {sel.cooldown:0.#}초" : "");
        SetText(dStats, SlotText(cs));

        if (refreshBtn != null)
        {
            int cost = refreshCost + refreshCount * refreshCostStep;
            var t = refreshBtn.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = $"<size=80%><color=#E3C27E>[R]</color></size> 새로고침 · {cost}";
            refreshBtn.interactable = money >= cost;
        }
    }

    void RefreshSell()
    {
        var cs = Skills();
        if (selected < 0 || selected >= SellCards || OwnedAt(selected) == null) selected = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var s = OwnedAt(i);
            c.root.gameObject.SetActive(i < SellCards);
            if (c.selectFrame != null) c.selectFrame.SetActive(s != null && i == selected);
            if (i >= SellCards) continue;
            string key = KeyBindings.KeyName(KeyAction.Skill1 + i + 1);
            if (s == null)
            {
                SetText(c.itemName, $"[{key}] 빈 칸");
                SetText(c.desc, "");
                SetText(c.badge, "-");
                SetText(c.price, "-");
                if (c.icon != null) c.icon.color = new Color(1, 1, 1, 0);
                if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(true);
                SetButton(c.buy, false, "-");
                continue;
            }
            Color tc = ShopColors.Tier(s.tier);
            if (c.gradeBar != null) c.gradeBar.color = tc;
            SetBorderColor(c.gradeRing, tc);
            SetBorderColor(c.badgeBorder, tc);
            if (c.badge != null) { c.badge.text = ShopColors.TierName(s.tier); c.badge.color = tc; }
            SetText(c.itemName, $"[{key}] {s.skillName}");
            SetText(c.desc, s.description);
            if (c.icon != null) { c.icon.sprite = s.icon; c.icon.color = s.icon != null ? Color.white : new Color(1, 1, 1, 0); }
            if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(s.icon == null);
            SetText(c.price, "+" + SellPriceOf(s).ToString("#,0"));
            SetButton(c.buy, true, "판매");
        }

        var sel = OwnedAt(selected);
        SetText(dName, sel != null ? sel.skillName : "-");
        if (dGrade != null) { dGrade.text = sel != null ? ShopColors.TierName(sel.tier) + " 스킬" : ""; if (sel != null) dGrade.color = ShopColors.Tier(sel.tier); }
        SetText(dFlavor, sel != null ? sel.description : "");
        SetText(dEffects, sel != null ? $"재사용 대기 {sel.cooldown:0.#}초" : "");
        SetText(dStats, sel != null ? $"팔면 재화 +{SellPriceOf(sel):#,0}\n그 칸은 비게 됩니다" : SlotText(cs));

        if (sellBtn != null) Label(sellBtn, "<size=80%><color=#E3C27E>[Tab]</color></size> 구매로 돌아가기");
        if (refreshBtn != null) { Label(refreshBtn, "새로고침 (구매 화면에서)"); refreshBtn.interactable = false; }
        if (odds != null) odds.text = $"판매가는 구매가의 {sellRate * 100:0}% · 무기 스킬 칸은 팔 수 없습니다";
    }

    static void Label(Button b, string text)
    {
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = text;
    }

    // 일반 스킬 칸 상태 (키 이름은 설정의 조작 키를 따른다)
    static string SlotText(CharacterSkills cs)
    {
        if (cs == null) return "";
        var lines = new List<string>();
        for (int slot = 1; slot < CharacterSkills.SlotCount && slot < cs.skillSlots.Length; slot++)
        {
            var s = cs.skillSlots[slot];
            string key = KeyBindings.KeyName(KeyAction.Skill1 + slot);
            lines.Add($"[{key}] " + (s != null ? s.skillName : "<color=#8FCB8F>비어 있음</color>"));
        }
        return string.Join("\n", lines);
    }
}
