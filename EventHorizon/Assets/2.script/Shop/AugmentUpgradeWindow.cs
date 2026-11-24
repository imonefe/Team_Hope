using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-21 추가] 증강 강화 화면 — 능력치 강화(StatUpgradeWindow)를 대신한다.
/// StatUpgradeCanvas 맨 위 오브젝트에 붙는다 (화면 오브젝트는 능력치 강화 때 것을 그대로 씀).
///
///  - 보유한 증강을 5줄씩 보여 준다 (아이콘 · 등급 테두리 · 키워드 · 단계 점 3개)
///  - 강화: 증강 파편을 내고 Lv1 → Lv2 → Lv3. 비용은 AugmentRules (등급별)
///  - 강화하면 그 증강의 효과 중 IAugmentLevelEffect 를 구현한 것에 단계 변화를 알린다
///  - 키보드: ↑↓ 고르기 · ← → 쪽 넘기기 · Space/Enter 강화 · Esc 닫기 (마우스도 됨, 휠로 쪽 넘기기)
///  - 위쪽 숫자는 보유 재화 대신 보유 증강 파편
/// </summary>
public class AugmentUpgradeWindow : ShopWindow
{
    const int RowsPerPage = 5;

    [Header("증강 강화 (2026-11-21)")]
    [Tooltip("증강 파편이 모자랄 때 화면 알림")]
    public string notEnoughShardsNotice = "증강 파편이 부족합니다";
    [Tooltip("파편 표시 색 (위쪽 동전 그림·비용 동전 그림에 입힘)")]
    public Color shardTint = new Color(0.69f, 0.55f, 0.88f, 1f);

    class RowView
    {
        public GameObject go;
        public Button select, upgrade;
        public TMP_Text name, desc, level, current, next, cost, placeholder;
        public Image icon;
        public Image[] pips;
        public Transform gradeRing;
        public GameObject selectFrame, costRow;
        public Image costCoin;
    }

    RowView[] views;
    readonly List<ItemData> owned = new List<ItemData>();
    int selected;
    TMP_Text dName, dLevel, dEffects, dStats;
    bool subscribed;

    protected override bool ShowsMoneyInHeader => false;

    protected override void Build()
    {
        views = new RowView[RowsPerPage];
        for (int i = 0; i < RowsPerPage; i++)
        {
            var root = Find($"Window/Rows/Row_{i}");
            if (root == null) continue;
            var v = new RowView { go = root.gameObject };
            v.select = root.GetComponent<Button>();
            v.upgrade = root.Find("UpgradeButton")?.GetComponent<Button>();
            v.name = Txt(root, "NameCol/StatName");
            v.desc = Txt(root, "NameCol/StatDesc");
            v.level = Txt(root, "LevelCol/LevelText");
            v.current = Txt(root, "ValueRow/Current");
            v.next = Txt(root, "ValueRow/Next");
            v.cost = Txt(root, "CostRow/Cost");
            v.placeholder = Txt(root, "IconSlot/IconPlaceholder");
            v.icon = root.Find("IconSlot/Icon")?.GetComponent<Image>();
            v.gradeRing = root.Find("IconSlot/GradeRing");
            v.costRow = root.Find("CostRow")?.gameObject;
            v.costCoin = root.Find("CostRow/CoinIcon")?.GetComponent<Image>();
            if (v.costCoin != null) v.costCoin.color = Color.white;   // [2026-11-24] 아이콘을 증강 파편(coin02) 그림으로 바꿔 물들이기 안 함
            v.selectFrame = root.Find("SelectFrame")?.gameObject;
            var list = new List<Image>();
            var pipRoot = root.Find("LevelCol/Pips");
            if (pipRoot != null) foreach (Transform t in pipRoot) { var im = t.GetComponent<Image>(); if (im != null) list.Add(im); }
            v.pips = list.ToArray();
            int slot = i;
            if (v.select != null) v.select.onClick.AddListener(() => { int idx = PageStart + slot; if (idx < owned.Count) { selected = idx; RefreshAll(); } });
            if (v.upgrade != null) v.upgrade.onClick.AddListener(() => { int idx = PageStart + slot; if (idx < owned.Count) Upgrade(idx); });
            views[i] = v;
        }
        dName = FindText("Window/DetailStrip/Col_Selected/DetailName");
        dLevel = FindText("Window/DetailStrip/Col_Selected/DetailLevel");
        dEffects = FindText("Window/DetailStrip/Col_Effects/DetailEffects");
        dStats = FindText("Window/DetailStrip/Col_Stats/DetailStats");

        var headerCoin = transform.Find("Window/HeaderRow/CoinIcon")?.GetComponent<Image>();
        if (headerCoin != null) headerCoin.color = Color.white;

        if (RunManager.Instance != null && !subscribed)
        {
            RunManager.Instance.OnShardsChanged += OnShards;
            subscribed = true;
        }
    }

    void OnDestroy()
    {
        if (subscribed && RunManager.Instance != null) RunManager.Instance.OnShardsChanged -= OnShards;
    }

    static TMP_Text Txt(Transform root, string path) => root.Find(path)?.GetComponent<TMP_Text>();

    void OnShards(int n) { if (IsOpen) RefreshAll(); }

    protected override void OnOpened()
    {
        CollectOwned();
        if (selected >= owned.Count) selected = Mathf.Max(0, owned.Count - 1);
        RefreshAll();
    }

    protected override void OnCurrencyChanged(int amount) { if (views != null) RefreshAll(); }

    void CollectOwned()
    {
        owned.Clear();
        var inv = Inventory.Instance;
        if (inv == null) return;
        foreach (var d in inv.passives)
            if (d != null && !owned.Contains(d)) owned.Add(d);
    }

    int PageStart => (selected / RowsPerPage) * RowsPerPage;
    int PageCount => Mathf.Max(1, (owned.Count + RowsPerPage - 1) / RowsPerPage);

    static int Shards => RunManager.Instance != null ? RunManager.Instance.Shards : 0;
    static int Level(ItemData d) => RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(d)) : 1;

    // ── 키보드 ──
    protected override void Update()
    {
        base.Update();
        if (!IsOpen || owned.Count == 0) return;
        int before = selected;
        if (Input.GetKeyDown(KeyCode.UpArrow)) selected = Mathf.Max(0, selected - 1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) selected = Mathf.Min(owned.Count - 1, selected + 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) selected = Mathf.Max(0, selected - RowsPerPage);
        if (Input.GetKeyDown(KeyCode.RightArrow)) selected = Mathf.Min(owned.Count - 1, selected + RowsPerPage);
        // 마우스 휠로 쪽 넘기기 (아래로 굴리면 다음 쪽)
        float wheel = Input.mouseScrollDelta.y;
        if (wheel < 0f) selected = Mathf.Min(owned.Count - 1, selected + RowsPerPage);
        else if (wheel > 0f) selected = Mathf.Max(0, selected - RowsPerPage);
        if (selected != before) RefreshAll();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Upgrade(selected);
    }

    // ── 강화 ──
    void Upgrade(int idx)
    {
        if (idx < 0 || idx >= owned.Count || RunManager.Instance == null) return;
        var d = owned[idx];
        int lv = Level(d);
        int cost = AugmentRules.UpgradeCost(d.tier, lv);
        if (cost < 0) return;
        if (!RunManager.Instance.SpendShards(cost))
        {
            ScreenNotice.Show(notEnoughShardsNotice);
            Talk(poorLines);
            return;
        }
        RunManager.Instance.SetAugmentLevel(d, lv + 1);

        // 효과에 단계 변화 알림 (증강을 얻을 때처럼 지금 캐릭터에 적용)
        var p = Player();
        var runner = p.go != null ? p.go.GetComponent<MonoBehaviour>() : null;
        if (d.effects != null)
            foreach (var e in d.effects)
                if (e is IAugmentLevelEffect le) le.OnAugmentLevelChanged(p.stats, runner, lv, lv + 1);

        selected = idx;
        RefreshAll();
        OnBought();
    }

    // 단계 값 (기획서 강화 칸). 비어 있으면 배율로
    static string ValueAt(ItemData d, int lv)
    {
        string v = AugmentRules.LevelValue(d, lv);
        return string.IsNullOrEmpty(v) ? $"×{AugmentRules.Multiplier(lv):0.0#}" : v;
    }

    // ── 화면 ──
    void RefreshAll()
    {
        if (views == null) return;
        int shards = Shards;
        SetText(currencyText, shards.ToString("#,0"));
        string up = ColorUtility.ToHtmlStringRGB(ShopColors.Up);
        int start = PageStart;

        for (int i = 0; i < RowsPerPage; i++)
        {
            var v = views[i];
            if (v == null) continue;
            int idx = start + i;
            bool has = idx < owned.Count;
            v.go.SetActive(has);
            if (!has) continue;

            var d = owned[idx];
            int lv = Level(d);
            int cost = AugmentRules.UpgradeCost(d.tier, lv);
            bool full = cost < 0;

            SetText(v.name, d.itemName);
            string kw = AugmentRules.KeywordsRich(d);
            string ab = AugmentRules.TierWithLevel(d.tier, lv);   // [2026-11-21] 고유능력 이름 대신 등급 + 단계·별
            SetText(v.desc, string.IsNullOrEmpty(kw) ? ab : $"{ab} · {kw}");
            if (v.icon != null) { v.icon.sprite = d.icon; v.icon.enabled = d.icon != null; v.icon.preserveAspect = true; }
            if (v.placeholder != null) v.placeholder.gameObject.SetActive(d.icon == null);
            SetBorderColor(v.gradeRing, ShopColors.Tier(d.tier));

            SetText(v.level, "");
            for (int p = 0; p < v.pips.Length; p++)
            {
                v.pips[p].gameObject.SetActive(p < AugmentRules.MaxLevel);
                v.pips[p].color = p < lv ? ShopColors.Gold : ShopColors.Line;
            }
            SetText(v.current, ValueAt(d, lv));
            if (v.next != null) { v.next.text = full ? "최대" : ValueAt(d, lv + 1); v.next.color = full ? ShopColors.Gold : ShopColors.Up; }
            if (v.costRow != null) v.costRow.SetActive(!full);
            SetText(v.cost, full ? "" : cost.ToString());
            if (full) SetButton(v.upgrade, false, "최대 단계");
            else SetButton(v.upgrade, shards >= cost, shards >= cost ? "강화" : "파편 부족");
            if (v.selectFrame != null) v.selectFrame.SetActive(idx == selected);
        }

        if (owned.Count == 0)
        {
            SetText(dName, "보유한 증강이 없습니다");
            SetText(dLevel, "");
            SetText(dEffects, "전투방에 들어서거나 보스를 쓰러뜨리면 증강을 고를 수 있습니다");
            SetText(dStats, $"보유 증강 파편  {shards:#,0}");
            return;
        }

        var sd = owned[Mathf.Clamp(selected, 0, owned.Count - 1)];
        int slv = Level(sd);
        int scost = AugmentRules.UpgradeCost(sd.tier, slv);
        string page = PageCount > 1 ? $"  ({selected / RowsPerPage + 1}/{PageCount}쪽)" : "";
        SetText(dName, $"<color=#{ColorUtility.ToHtmlStringRGB(ShopColors.Tier(sd.tier))}>{sd.itemName}</color>{page}");
        SetText(dLevel, scost < 0 ? $"최대 단계 (Lv{slv})" : $"Lv{slv} / Lv{AugmentRules.MaxLevel} · 다음 비용 파편 {scost}");

        // [2026-11-21] 고유능력 이름 · 효과 · 단계 값 (예전: 설명문 + 능력치 줄)
        SetText(dEffects, AugmentRules.Describe(sd, slv));

        string abName = "강화 값";
        SetText(dStats, scost < 0
            ? $"{abName}  {ValueAt(sd, slv)}\n보유 증강 파편  {shards:#,0}"
            : $"{abName}  {ValueAt(sd, slv)} → <color=#{up}>{ValueAt(sd, slv + 1)}</color>\n남은 증강 파편  {shards:#,0} → {Mathf.Max(0, shards - scost):#,0}");
    }
}
