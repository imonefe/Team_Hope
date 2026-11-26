using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 아이템 상점 화면 (ItemShopCanvas 맨 위 오브젝트에 붙음).
///
///  - 처음 열 때 진열 아이템 4개를 뽑는다: 이번 판에 아직 안 얻은 아이템 중 등급 확률로.
///    (진열된 아이템은 상자가 같은 아이템을 또 뽑지 않게 RunManager에 '확보'해 둔다)
///  - 카드를 누르면 선택 → 아래 설명 띠에 정확한 효과·바뀌는 능력치 표시
///  - 구매: 재화가 충분하면 차감 → 아이템 효과 적용 + 인벤토리에 추가 (바닥 아이템을 줍는 것과 같은 처리)
///  - 새로고침: 비용을 내고 진열을 다시 뽑는다. 누를 때마다 비용이 refreshCostStep 만큼 오른다.
///  - [2026-11-16] 가진 아이템 팔기: 누르면 판매 모드 — 카드 4장에 인벤토리 아이템이 4개씩 나오고(새로고침 버튼 = 다음 쪽),
///    팔면 인벤토리에서 빠지고 재화를 받는다. 아이템 효과는 되돌리지 않는다 (판 아이템의 효과는 그대로 남음).
///    판매가 = 등급별 가격 × sellRate
///
/// 가격·확률·새로고침 비용은 전부 임시값 — 인스펙터에서 바꾸면 된다.
///
/// [2026-11-21] 증강 상점으로 전환 (증강 기획서 '상점 판매'): 일반 100 · 희귀 200 · 영웅 350, 전설은 무작위 진열 안 함.
///  전용 진열(exclusiveOffer, 백자 열람 권한): 진열할 때마다 exclusiveChance 확률로 마지막 칸에 나옴.
///  가진 증강 팔기: 증강 파편을 받고, 그 증강의 능력·능력치가 함께 사라진다 (AugmentRules.SellShards).
/// </summary>
public class ItemShopWindow : ShopWindow
{
    [Header("진열 후보 (상점 생성기가 1.Prefab/item 의 아이템을 자동으로 채움)")]
    public List<ItemData> itemPool = new List<ItemData>();

    [Header("등급별 가격 (일반 / 레어 / 유니크 / 레전더리) — 임시값")]
    public int[] tierPrices = { 100, 200, 350, 0 };

    [Header("등급별 진열 확률 % (일반 / 레어 / 유니크 / 레전더리) — 임시값")]
    public float[] tierOdds = { 55, 35, 10, 0 };

    [Header("판매가 = 등급별 가격 × 이 비율 — 임시값")]
    [Range(0f, 1f)] public float sellRate = 0.4f;

    [Header("새로고침 비용 — 임시값")]
    public int refreshCost = 50;
    public int refreshCostStep = 25;

    [Header("[2026-11-21] 가진 증강 팔기 (증강 파편을 받음)")]
    public bool allowSell = true;

    [Header("[2026-11-21] 이 상점 전용 증강 (백자 열람 권한)")]
    public ItemData exclusiveOffer;
    [Range(0f, 1f)] public float exclusiveChance = 0.3f;
    public int exclusivePrice = 600;

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
    readonly ItemData[] offers = new ItemData[SlotCount];
    readonly bool[] sold = new bool[SlotCount];
    int selected = -1;
    int refreshCount;
    bool stocked;

    // 판매 모드
    bool selling;
    int sellPage;
    readonly ItemData[] sellItems = new ItemData[SlotCount];
    readonly int[] sellIndex = new int[SlotCount];   // 인벤토리 목록에서의 위치

    TMP_Text dName, dGrade, dFlavor, dEffects, dStats, odds;
    Button refreshBtn, sellBtn;

    // ── 화면 연결 ──
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
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => { if (selling) NextPage(); else Refresh(); });
        sellBtn = Find("Window/BottomRow/SellButton")?.GetComponent<Button>();
        if (sellBtn != null) sellBtn.onClick.AddListener(ToggleSell);
        if (sellBtn != null && !allowSell) sellBtn.gameObject.SetActive(false);   // [2026-11-21]

        if (odds != null)
            odds.text = OddsLine();
    }

    protected override void OnOpened()
    {
        if (!stocked) { Restock(); stocked = true; }
        selling = false;   // 열 때는 항상 구매 화면부터
        RefreshAll();
    }

    protected override void OnCurrencyChanged(int amount) => RefreshAll();

    void OnDestroy() => ReleaseAll();

    // ── 진열 ──
    void Restock()
    {
        ReleaseAll();
        var used = new HashSet<ItemData>();
        for (int i = 0; i < SlotCount; i++)
        {
            offers[i] = Pick(used);
            sold[i] = false;
            if (offers[i] != null)
            {
                used.Add(offers[i]);
                RunManager.Instance?.HoldItem(offers[i], this);
            }
        }
        // [2026-11-21] 전용 증강: 확률로 마지막 칸에
        if (exclusiveOffer != null && (RunManager.Instance == null || RunManager.Instance.IsNewItem(exclusiveOffer)) && Random.value < exclusiveChance)
        {
            int last = SlotCount - 1;
            if (offers[last] != null) RunManager.Instance?.ReleaseItem(offers[last], this);
            offers[last] = exclusiveOffer;
            RunManager.Instance?.HoldItem(exclusiveOffer, this);
        }
        selected = -1;
        for (int i = 0; i < SlotCount; i++) if (offers[i] != null) { selected = i; break; }
    }

    void ReleaseAll()
    {
        for (int i = 0; i < SlotCount; i++)
            if (offers[i] != null) RunManager.Instance?.ReleaseItem(offers[i], this);
    }

    // 등급을 확률로 고른 뒤 그 등급의 '새' 아이템 중 하나. 그 등급에 없으면 다른 등급에서.
    ItemData Pick(HashSet<ItemData> used)
    {
        var byTier = new List<ItemData>[4];
        for (int t = 0; t < 4; t++) byTier[t] = new List<ItemData>();
        foreach (var it in itemPool)
        {
            if (it == null || used.Contains(it) || it == exclusiveOffer) continue;
            if (RunManager.Instance != null && !RunManager.Instance.IsNewItem(it)) continue;
            byTier[(int)it.tier].Add(it);
        }

        float total = 0;
        for (int t = 0; t < 4; t++) if (byTier[t].Count > 0) total += Odd(t);
        if (total <= 0) return null;
        float r = Random.value * total;
        for (int t = 0; t < 4; t++)
        {
            if (byTier[t].Count == 0) continue;
            r -= Odd(t);
            if (r <= 0) return byTier[t][Random.Range(0, byTier[t].Count)];
        }
        for (int t = 3; t >= 0; t--) if (byTier[t].Count > 0 && Odd(t) > 0) return byTier[t][Random.Range(0, byTier[t].Count)];   // [2026-11-21] 확률 0 등급(전설)은 진열 안 함
        return null;
    }

    // [2026-11-21] 확률 0 등급은 빼고 표시 (증강 등급 이름)
    string OddsLine()
    {
        var parts = new List<string>();
        for (int t = 0; t < 4; t++) if (Odd(t) > 0) parts.Add($"{AugmentRules.TierName((ItemTier)t)} {Odd(t):0}%");
        return string.Join(" · ", parts);
    }

    float Odd(int t) => tierOdds != null && t < tierOdds.Length ? Mathf.Max(0, tierOdds[t]) : 0;
    int PriceOf(ItemData d) => d == null ? 0 : d == exclusiveOffer ? exclusivePrice : (tierPrices != null && (int)d.tier < tierPrices.Length ? tierPrices[(int)d.tier] : 0);
    int CurrentRefreshCost => refreshCost + refreshCount * refreshCostStep;
    // [2026-11-21] 증강 판매가 = 증강 파편 (등급 기본값 + 강화에 쓴 파편의 절반)
    int SellPriceOf(ItemData d) => d == null ? 0 : AugmentRules.SellShards(d.tier, RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(d)) : 1);

    static List<ItemData> Owned => Inventory.Instance != null ? Inventory.Instance.passives : new List<ItemData>();
    int PageCount => Mathf.Max(1, Mathf.CeilToInt(Owned.Count / (float)SlotCount));

    // ── 판매 모드 ──
    void ToggleSell()
    {
        selling = !selling;
        sellPage = 0;
        selected = selling ? 0 : FirstOffer();
        RefreshAll();
    }

    void NextPage()
    {
        sellPage = (sellPage + 1) % PageCount;
        selected = 0;
        RefreshAll();
    }

    int FirstOffer()
    {
        for (int i = 0; i < SlotCount; i++) if (offers[i] != null) return i;
        return -1;
    }

    void FillSellPage()
    {
        var list = Owned;
        if (sellPage >= PageCount) sellPage = PageCount - 1;
        for (int i = 0; i < SlotCount; i++)
        {
            int idx = sellPage * SlotCount + i;
            sellItems[i] = idx < list.Count ? list[idx] : null;
            sellIndex[i] = idx;
        }
    }

    // [2026-11-21] 팔기: 증강 파편을 받고, 그 증강의 능력치 효과를 되돌린 뒤 인벤토리에서 뺀다
    //  (고유능력·키워드는 인벤토리를 읽어 돌아가므로 빠지는 순간 함께 꺼진다)
    void Sell(int i)
    {
        var d = sellItems[i];
        if (d == null || Inventory.Instance == null) return;
        int shards = SellPriceOf(d);
        int level = RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(d)) : 1;
        if (!Inventory.Instance.RemovePassiveAt(sellIndex[i], d)) return;
        var p = Player();
        if (d.effects != null && p.stats != null)
            foreach (var e in d.effects)
                if (e != null) e.Revert(p.stats, p.go != null ? p.go.GetComponent<MonoBehaviour>() : null, level);
        RunManager.Instance?.ForgetObtained(d);
        RunManager.Instance?.AddShards(shards);
        AugmentRuntime.Instance?.Refresh();
        RefreshAll();
    }

    // [2026-11-21] 키보드: ← → 고르기 · Space/Enter 구매(판매 화면이면 판매) · R 새로고침(판매 화면이면 다음 쪽) · Tab 판매 화면 전환 · Esc 닫기
    protected override void Update()
    {
        base.Update();
        if (!IsOpen) return;
        if (ItemSwapWindow.BlocksInput) return;   // [2026-11-26] 교체 창이 위에 떠 있음
        if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelect(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSelect(+1);
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && selected >= 0) Buy(selected);
        if (Input.GetKeyDown(KeyCode.R)) { if (selling) NextPage(); else Refresh(); }
        if (Input.GetKeyDown(KeyCode.Tab) && allowSell) ToggleSell();
    }

    void MoveSelect(int dir)
    {
        int s = selected < 0 ? 0 : selected;
        for (int k = 0; k < SlotCount; k++)
        {
            s = (s + dir + SlotCount) % SlotCount;
            bool has = selling ? sellItems[s] != null : offers[s] != null;
            if (has) { selected = s; RefreshAll(); return; }
        }
    }

    // ── 버튼 ──
    void Select(int i)
    {
        if (selling) { if (sellItems[i] != null) { selected = i; RefreshAll(); } return; }
        if (offers[i] == null) return;
        selected = i;
        RefreshAll();
    }

    void Buy(int i)
    {
        if (selling) { Sell(i); return; }
        var d = offers[i];
        if (d == null || sold[i]) return;
        var p = Player();
        if (p.stats == null) return;
        if (ItemSwapWindow.BlocksInput) return;
        int price = PriceOf(d);
        if (Money < price) { NotEnoughMoney(); return; }

        // [2026-11-26] 8칸 제한 — 가득 차 있으면 교체 창. 돈은 실제로 얻을 때만 냄 (포기 = 구매 취소)
        // 바닥 아이템을 줍는 것과 같은 처리 (Item.Pickup 참고)
        ItemObtain.Request(d, p.go, r =>
        {
            if (r == ItemObtain.Result.Declined || this == null) return;
            Spend(price);
            RunManager.Instance?.ReleaseItem(d, this);
            sold[i] = true;
            RefreshAll();
            OnBought();   // [2026-11-20] 인물 대사
        });
    }

    void Refresh()
    {
        if (!Spend(CurrentRefreshCost)) { NotEnoughMoney(); return; }
        refreshCount++;
        Restock();
        RefreshAll();
    }

    // ── 화면 갱신 ──
    void RefreshAll()
    {
        if (selling) { RefreshSell(); return; }
        int money = Money;
        if (sellBtn != null) SetSecondaryLabel(sellBtn, "<size=80%><color=#E3C27E>[Tab]</color></size> 가진 사이버웨어 팔기");
        if (odds != null)
            odds.text = OddsLine();
        for (int i = 0; i < SlotCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var d = offers[i];
            c.root.gameObject.SetActive(true);
            if (c.selectFrame != null) c.selectFrame.SetActive(i == selected);

            if (d == null)
            {
                SetText(c.itemName, "진열할 아이템 없음");
                SetText(c.desc, "");
                SetText(c.badge, "-");
                SetText(c.price, "-");
                if (c.icon != null) c.icon.color = new Color(1, 1, 1, 0);
                if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(true);
                SetButton(c.buy, false, "-");
                continue;
            }

            Color tc = ShopColors.Tier(d.tier);
            if (c.gradeBar != null) c.gradeBar.color = tc;
            SetBorderColor(c.gradeRing, tc);
            SetBorderColor(c.badgeBorder, tc);
            if (c.badge != null) { c.badge.text = AugmentRules.TierName(d.tier); c.badge.color = tc; }
            SetText(c.itemName, d.itemName);
            // [2026-11-21] 증강: 고유능력 이름 · 효과 · 키워드 (설명문 대신)
            string kw = AugmentRules.KeywordsRich(d);
            string ab = string.IsNullOrEmpty(d.abilityText) ? d.description : AugmentRules.Rich(d.abilityText, 1);   // [2026-11-23] [키워드] · {단계 값} 표시
            SetText(c.desc, string.IsNullOrEmpty(kw) ? ab : $"{kw}\n{ab}");
            if (c.icon != null)
            {
                c.icon.sprite = d.icon;
                c.icon.color = d.icon != null ? Color.white : new Color(1, 1, 1, 0);
            }
            if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(d.icon == null);
            int price = PriceOf(d);
            SetText(c.price, sold[i] ? "-" : price.ToString("#,0"));
            if (sold[i]) SetButton(c.buy, false, "구매 완료");
            else SetButton(c.buy, money >= price, money >= price ? "구매" : "재화 부족");
        }

        // 설명 띠
        var sel = selected >= 0 ? offers[selected] : null;
        if (sel == null)
        {
            SetText(dName, "-"); SetText(dGrade, ""); SetText(dFlavor, ""); SetText(dEffects, ""); SetText(dStats, "");
        }
        else
        {
            SetText(dName, sel.itemName);
            if (dGrade != null) { dGrade.text = AugmentRules.TierWithLevel(sel.tier, 1); dGrade.color = ShopColors.Tier(sel.tier); }
            SetText(dFlavor, sel.description);
            SetText(dEffects, AugmentRules.Describe(sel, 1));   // [2026-11-21] 고유능력 · 강화 값 · 키워드
            SetText(dStats, StatPreview(sel));
        }

        if (refreshBtn != null)
        {
            var t = refreshBtn.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = $"<size=80%><color=#E3C27E>[R]</color></size> 새로고침 · {CurrentRefreshCost}";   // [2026-11-23] 단축키 표시
            refreshBtn.interactable = money >= CurrentRefreshCost;
        }
    }

    void RefreshSell()
    {
        FillSellPage();
        if (selected < 0 || selected >= SlotCount || sellItems[selected] == null) selected = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            var d = sellItems[i];
            c.root.gameObject.SetActive(d != null || i == 0);
            if (c.selectFrame != null) c.selectFrame.SetActive(d != null && i == selected);
            if (d == null)
            {
                SetText(c.itemName, "팔 사이버웨어 없음");
                SetText(c.desc, "");
                SetText(c.badge, "-");
                SetText(c.price, "-");
                if (c.icon != null) c.icon.color = new Color(1, 1, 1, 0);
                if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(true);
                SetButton(c.buy, false, "-");
                continue;
            }
            Color tc = ShopColors.Tier(d.tier);
            if (c.gradeBar != null) c.gradeBar.color = tc;
            SetBorderColor(c.gradeRing, tc);
            SetBorderColor(c.badgeBorder, tc);
            if (c.badge != null) { c.badge.text = AugmentRules.TierName(d.tier); c.badge.color = tc; }
            SetText(c.itemName, d.itemName);
            SetText(c.desc, d.description);
            if (c.icon != null) { c.icon.sprite = d.icon; c.icon.color = d.icon != null ? Color.white : new Color(1, 1, 1, 0); }
            if (c.iconPlaceholder != null) c.iconPlaceholder.SetActive(d.icon == null);
            SetText(c.price, $"파편 +{SellPriceOf(d)}");
            SetButton(c.buy, true, "판매");
        }

        var sel = sellItems[selected];
        SetText(dName, sel != null ? sel.itemName : "-");
        if (dGrade != null) { dGrade.text = sel != null ? AugmentRules.TierWithLevel(sel.tier, RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(sel)) : 1) : ""; if (sel != null) dGrade.color = ShopColors.Tier(sel.tier); }
        SetText(dFlavor, sel != null ? sel.description : "");
        SetText(dEffects, sel != null ? string.Join("\n", EffectText.Collect(sel)) : "");
        SetText(dStats, sel != null ? $"팔면 사이버웨어 파편 +{SellPriceOf(sel)}\n<color=#{ColorUtility.ToHtmlStringRGB(ShopColors.Down)}>능력과 능력치가 함께 사라집니다</color>" : "");

        if (sellBtn != null) SetSecondaryLabel(sellBtn, "<size=80%><color=#E3C27E>[Tab]</color></size> 구매로 돌아가기");
        if (refreshBtn != null)
        {
            SetSecondaryLabel(refreshBtn, $"<size=80%><color=#E3C27E>[R]</color></size> 다음 쪽 ({sellPage + 1}/{PageCount})");
            refreshBtn.interactable = PageCount > 1;
        }
        if (odds != null) odds.text = $"가진 사이버웨어 {Owned.Count}개 · 판매가는 등급과 강화 단계에 따라 사이버웨어 파편";
    }

    static void SetSecondaryLabel(Button b, string label)
    {
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = label;
    }

    // 고정값으로 오르는 능력치만 "현재 → 후" 로 보여준다 (확률·지속 효과는 위 '정확한 효과' 참고)
    static string StatPreview(ItemData d)
    {
        var p = Player();
        if (p.stats == null || d.effects == null) return "";
        float hp = 0, atk = 0, def = 0, spd = 0, pause = 0, cc = 0, cm = 0;
        foreach (var e in d.effects)
        {
            if (e is StatBoostEffect sb && sb.Active)   // [2026-11-26] 꺼진 사이버웨어 스탯은 미리 보기에서도 뺌 (제로포인트만 남음)
            {
                var m = sb.Effective;
                hp += m.hp + p.stats.maxHp * m.hpPercent;
                atk += m.atk + p.stats.Atk * m.atkPercent;
                def += m.def + p.stats.Def * m.defPercent;
                spd += m.spd + p.stats.spd * m.spdPercent;
                pause += m.pauseDuration + p.stats.PauseDuration * m.pauseDurationPercent;
                cc += m.critChance;
                cm += m.critMultiplier;
            }
        }
        var lines = new List<string>();
        Line(lines, "최대 체력", p.stats.maxHp, hp, "0");
        Line(lines, "공격력", p.stats.Atk, atk, "0.#");
        Line(lines, "방어력", p.stats.Def, def, "0.#");
        Line(lines, "이동속도", p.stats.spd, spd, "0.#");
        Line(lines, "제로포인트 시간", p.stats.PauseDuration, pause, "0.#");
        if (!Mathf.Approximately(cc, 0)) lines.Add($"치명타 확률  {p.stats.critChance * 100:0}% → {Colored((p.stats.critChance + cc) * 100, cc, "0")}%");
        if (!Mathf.Approximately(cm, 0)) lines.Add($"치명타 배율  {p.stats.critMultiplier:0.#} → {Colored(p.stats.critMultiplier + cm, cm, "0.#")}");
        return lines.Count > 0 ? string.Join("\n", lines) : "바뀌는 능력치 없음";
    }

    static void Line(List<string> lines, string label, float cur, float delta, string fmt)
    {
        if (Mathf.Approximately(delta, 0)) return;
        lines.Add($"{label}  {cur.ToString(fmt)} → {Colored(cur + delta, delta, fmt)}");
    }

    static string Colored(float v, float delta, string fmt)
    {
        string col = ColorUtility.ToHtmlStringRGB(delta >= 0 ? ShopColors.Up : ShopColors.Down);
        return $"<color=#{col}>{v.ToString(fmt)}</color>";
    }
}
