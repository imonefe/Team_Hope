using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 능력치 강화 화면 (StatUpgradeCanvas 맨 위 오브젝트에 붙음).
///
///  - 강화할 능력치 5줄: 체력 · 공격력 · 방어력 · 이동속도 · 치명타 (rows 배열 순서 = 화면 줄 순서)
///  - 줄을 누르면 선택 → 아래 설명 띠에 단계별 효과 / 강화하면 달라지는 능력치 표시
///  - 강화: 재화 차감 → 두 캐릭터(CharacterManager의 A/B) 능력치를 모두 올린다 (캐릭터를 바꿔도 같게)
///  - 단계는 RunManager에 저장 → 이번 판 동안 유지, 새 판(다시하기)이면 0부터
///
/// 단계당 상승량·단계별 비용·최대 단계는 전부 임시값 — 인스펙터 rows 에서 바꾸면 된다.
/// (기획 미정: 강화가 판을 넘어 유지되는지, 이동속도 상한)
/// </summary>
public class StatUpgradeWindow : ShopWindow
{
    public enum StatKind { MaxHp, Attack, Defense, MoveSpeed, Critical }

    [System.Serializable]
    public class Row
    {
        public string label = "체력";
        [TextArea] public string description = "최대 체력이 늘어난다.";
        public StatKind kind;
        [Tooltip("한 단계당 오르는 양. 치명타는 확률(0.05 = 5%)")]
        public float perLevel = 20f;
        [Tooltip("치명타만 사용: 한 단계당 오르는 치명타 배율")]
        public float critMultiplierPerLevel = 0.1f;
        [Tooltip("단계별 비용. 배열 길이 = 최대 단계")]
        public int[] costs = { 30, 50, 80, 120, 170 };
    }

    public Row[] rows =
    {
        new Row { label = "체력", description = "최대 체력이 늘어난다.", kind = StatKind.MaxHp, perLevel = 20 },
        new Row { label = "공격력", description = "모든 공격의 피해가 늘어난다.", kind = StatKind.Attack, perLevel = 2 },
        new Row { label = "방어력", description = "받는 피해가 줄어든다.", kind = StatKind.Defense, perLevel = 1 },
        new Row { label = "이동속도", description = "이동 속도가 빨라진다.", kind = StatKind.MoveSpeed, perLevel = 0.5f },
        new Row { label = "치명타", description = "치명타 확률과 배율이 함께 오른다.", kind = StatKind.Critical, perLevel = 0.05f },
    };

    class RowView
    {
        public Transform root;
        public Button select, upgrade;
        public TMP_Text name, desc, level, current, next, cost;
        public Image[] pips;
        public GameObject selectFrame, costRow;
    }

    RowView[] views;
    int selected;
    TMP_Text dName, dLevel, dEffects, dStats;

    protected override void Build()
    {
        views = new RowView[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var root = Find($"Window/Rows/Row_{i}");
            if (root == null) continue;
            var v = new RowView { root = root };
            v.select = root.GetComponent<Button>();
            v.upgrade = root.Find("UpgradeButton")?.GetComponent<Button>();
            v.name = root.Find("NameCol/StatName")?.GetComponent<TMP_Text>();
            v.desc = root.Find("NameCol/StatDesc")?.GetComponent<TMP_Text>();
            v.level = root.Find("LevelCol/LevelText")?.GetComponent<TMP_Text>();
            v.current = root.Find("ValueRow/Current")?.GetComponent<TMP_Text>();
            v.next = root.Find("ValueRow/Next")?.GetComponent<TMP_Text>();
            v.cost = root.Find("CostRow/Cost")?.GetComponent<TMP_Text>();
            v.costRow = root.Find("CostRow")?.gameObject;
            v.selectFrame = root.Find("SelectFrame")?.gameObject;
            var pipRoot = root.Find("LevelCol/Pips");
            var list = new List<Image>();
            if (pipRoot != null) foreach (Transform t in pipRoot) { var im = t.GetComponent<Image>(); if (im != null) list.Add(im); }
            v.pips = list.ToArray();
            int idx = i;
            if (v.select != null) v.select.onClick.AddListener(() => { selected = idx; RefreshAll(); });
            if (v.upgrade != null) v.upgrade.onClick.AddListener(() => Upgrade(idx));
            views[i] = v;
        }
        dName = FindText("Window/DetailStrip/Col_Selected/DetailName");
        dLevel = FindText("Window/DetailStrip/Col_Selected/DetailLevel");
        dEffects = FindText("Window/DetailStrip/Col_Effects/DetailEffects");
        dStats = FindText("Window/DetailStrip/Col_Stats/DetailStats");
    }

    protected override void OnOpened() => RefreshAll();
    protected override void OnCurrencyChanged(int amount) => RefreshAll();

    static string Key(Row r) => "StatUpgrade." + r.kind;
    static int Level(Row r) => RunManager.Instance != null ? RunManager.Instance.GetUpgradeLevel(Key(r)) : 0;
    static int MaxLevel(Row r) => r.costs != null ? r.costs.Length : 0;

    void Upgrade(int i)
    {
        var r = rows[i];
        int lv = Level(r);
        if (lv >= MaxLevel(r)) return;
        if (!Spend(r.costs[lv])) { NotEnoughMoney(); return; }
        RunManager.Instance.SetUpgradeLevel(Key(r), lv + 1);

        // 두 캐릭터 모두 올린다 (교체해도 같은 능력치). 캐릭터 관리자가 없으면 지금 캐릭터만.
        var targets = new List<Stats>();
        var cm = CharacterManager.Instance;
        if (cm != null)
        {
            foreach (var go in new[] { cm.characterA, cm.characterB })
                if (go != null) { var s = go.GetComponent<Stats>(); if (s != null && !targets.Contains(s)) targets.Add(s); }
        }
        if (targets.Count == 0) { var p = Player(); if (p.stats != null) targets.Add(p.stats); }
        foreach (var s in targets) Apply(s, r);

        selected = i;
        RefreshAll();
        OnBought();   // [2026-11-20] 인물 대사
    }

    static void Apply(Stats s, Row r)
    {
        switch (r.kind)
        {
            case StatKind.MaxHp: s.IncreaseMaxHp(Mathf.RoundToInt(r.perLevel)); break;
            case StatKind.Attack: s.Atk += r.perLevel; break;
            case StatKind.Defense: s.Def += r.perLevel; break;
            case StatKind.MoveSpeed: s.spd += r.perLevel; break;
            case StatKind.Critical: s.critChance += r.perLevel; s.critMultiplier += r.critMultiplierPerLevel; break;
        }
    }

    static string Value(Stats s, Row r, int extraLevels)
    {
        if (s == null) return "-";
        float add = r.perLevel * extraLevels;
        switch (r.kind)
        {
            case StatKind.MaxHp: return (s.maxHp + add).ToString("0");
            case StatKind.Attack: return (s.Atk + add).ToString("0.#");
            case StatKind.Defense: return (s.Def + add).ToString("0.#");
            case StatKind.MoveSpeed: return (s.spd + add).ToString("0.#");
            default:
                float cc = Mathf.Min(s.critChance + add, s.maxCritChance);
                return $"{cc * 100:0}% · {s.critMultiplier + r.critMultiplierPerLevel * extraLevels:0.#}배";
        }
    }

    static string PerLevelText(Row r)
    {
        switch (r.kind)
        {
            case StatKind.MaxHp: return $"최대 체력 +{r.perLevel:0}";
            case StatKind.Attack: return $"공격력 +{r.perLevel:0.#}";
            case StatKind.Defense: return $"방어력 +{r.perLevel:0.#}";
            case StatKind.MoveSpeed: return $"이동속도 +{r.perLevel:0.#}";
            default: return $"치명타 확률 +{r.perLevel * 100:0}% · 배율 +{r.critMultiplierPerLevel:0.#}배";
        }
    }

    void RefreshAll()
    {
        if (views == null) return;
        int money = Money;
        var st = Player().stats;
        string up = ColorUtility.ToHtmlStringRGB(ShopColors.Up);

        for (int i = 0; i < rows.Length; i++)
        {
            var v = views[i];
            if (v == null) continue;
            var r = rows[i];
            int lv = Level(r), max = MaxLevel(r);
            bool full = lv >= max;
            SetText(v.name, r.label);
            SetText(v.desc, r.description);
            SetText(v.level, full ? "최대 단계" : $"{lv}단계 / {max}단계");
            for (int p = 0; p < v.pips.Length; p++)
            {
                v.pips[p].gameObject.SetActive(p < max);
                v.pips[p].color = p < lv ? ShopColors.Gold : ShopColors.Line;
            }
            SetText(v.current, Value(st, r, 0));
            if (v.next != null) { v.next.text = full ? "최대" : Value(st, r, 1); v.next.color = full ? ShopColors.Gold : ShopColors.Up; }
            if (v.costRow != null) v.costRow.SetActive(!full);
            SetText(v.cost, full ? "" : r.costs[lv].ToString("#,0"));
            if (full) SetButton(v.upgrade, false, "최대 단계");
            else SetButton(v.upgrade, money >= r.costs[lv], money >= r.costs[lv] ? "강화" : "재화 부족");
            if (v.selectFrame != null) v.selectFrame.SetActive(i == selected);
        }

        if (selected < 0 || selected >= rows.Length) selected = 0;
        var sr = rows[selected];
        int slv = Level(sr), smax = MaxLevel(sr);
        SetText(dName, sr.label);
        SetText(dLevel, slv >= smax ? $"최대 단계 ({smax}단계)" : $"{slv}단계 / {smax}단계 · 다음 비용 {sr.costs[slv]:#,0}");
        SetText(dEffects, slv >= smax
            ? "더 강화할 수 없습니다"
            : $"<color=#{up}>▲ 다음 단계: {PerLevelText(sr)}</color>\n단계가 오를수록 비용이 늘어납니다\n강화는 이번 판 동안 유지됩니다");
        SetText(dStats, slv >= smax ? $"{sr.label}  {Value(st, sr, 0)}"
            : $"{sr.label}  {Value(st, sr, 0)} → <color=#{up}>{Value(st, sr, 1)}</color>\n남은 재화  {money:#,0} → {Mathf.Max(0, money - sr.costs[slv]):#,0}");
    }
}
