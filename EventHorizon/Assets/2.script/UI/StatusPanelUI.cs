using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-18 추가] 인벤토리 왼쪽 능력치 판 (시안 C4).
///
/// - 능력치 7줄을 '현재 값 (+아이템·무기로 오른 값)' 으로 보여 준다.
///   오른 값 = 지금 값 - 캐릭터가 처음 가진 값 (Stats.Base* — Stats.Awake 에서 기억).
///   0 이면 괄호를 비우고, 줄었으면 빨간색으로 (-n).
/// - 스킬 3칸에 현재 캐릭터 스킬 아이콘을 넣는다. (키 글자는 각 칸의 KeyLabel, 재화는 CurrencyUI 가 담당)
/// - 창이 열릴 때 / 캐릭터 교차 / 체력이 바뀔 때 갱신. 창이 열려 있는 동안은 0.25초마다 확인.
/// - 무기 칸은 WeaponShowcaseUI 가 따로 담당한다.
/// </summary>
public class StatusPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public TMP_Text value;   // 현재 값
        public TMP_Text bonus;   // (+n)
    }

    [Header("능력치 줄 (순서 고정)")]
    public Row maxHp = new Row();
    public Row attack = new Row();
    public Row defense = new Row();
    public Row moveSpeed = new Row();
    public Row pauseDuration = new Row();
    public Row critChance = new Row();
    public Row critMultiplier = new Row();

    [Header("오른 값 색")]
    public Color upColor = new Color(0.486f, 0.890f, 0.545f, 1f);    // #7CE38B
    public Color downColor = new Color(0.937f, 0.478f, 0.384f, 1f);  // #EF7A62

    [Header("스킬 3칸 아이콘")]
    public Image[] skillIcons = new Image[3];

    [Header("갱신 주기 (초) — 창이 열려 있는 동안")]
    public float refreshInterval = 0.25f;

    Stats subscribed;
    float timer;

    void Start()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged += OnCharacterChanged;
        Refresh();
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= OnCharacterChanged;
    }

    // 인벤토리 창이 열리면 게임이 멈춰 있을 수 있으므로 실제 시간 기준
    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;
        Refresh();
    }

    void OnCharacterChanged(GameObject current)
    {
        Subscribe();
        Refresh();
    }

    void Subscribe()
    {
        Unsubscribe();
        Stats st = CurrentStats();
        if (st == null) return;
        subscribed = st;
        subscribed.OnHpChanged += OnHpChanged;
    }

    void Unsubscribe()
    {
        if (subscribed != null) subscribed.OnHpChanged -= OnHpChanged;
        subscribed = null;
    }

    void OnHpChanged(int cur, int max) => Refresh();

    static Stats CurrentStats()
    {
        return CharacterManager.Instance != null ? CharacterManager.Instance.CurrentStats : null;
    }

    public void Refresh()
    {
        Stats st = CurrentStats();
        if (st == null) return;

        Set(maxHp, st.maxHp, st.BaseMaxHp, "0", "0");
        Set(attack, st.Atk, st.BaseAtk, "0", "0");
        Set(defense, st.Def, st.BaseDef, "0", "0");
        Set(moveSpeed, st.GetEffectiveSpd(), st.BaseSpd, "0.0", "0.0");
        Set(pauseDuration, st.PauseDuration, st.BasePauseDuration, "0.0초", "0.0초");
        Set(critChance, st.EffectiveCritChance * 100f, st.BaseCritChance * 100f, "0'%'", "0'%'");
        Set(critMultiplier, st.critMultiplier, st.BaseCritMultiplier, "×0.0", "0.0");

        RefreshSkills();
    }

    void Set(Row row, float now, float baseValue, string valueFormat, string bonusFormat)
    {
        if (row == null) return;
        if (row.value != null) row.value.text = now.ToString(valueFormat);
        if (row.bonus == null) return;

        float diff = now - baseValue;
        // 표시 자릿수에서 0 으로 보이면 비움
        if (diff.ToString(bonusFormat) == 0f.ToString(bonusFormat) || Mathf.Abs(diff) < 0.0001f)
        {
            row.bonus.text = "";
            return;
        }
        row.bonus.text = diff > 0f ? "(+" + diff.ToString(bonusFormat) + ")" : "(-" + (-diff).ToString(bonusFormat) + ")";
        row.bonus.color = diff > 0f ? upColor : downColor;
    }

    void RefreshSkills()
    {
        if (skillIcons == null) return;
        var skills = CharacterManager.Instance != null ? CharacterManager.Instance.CurrentSkills : null;
        for (int i = 0; i < skillIcons.Length; i++)
        {
            if (skillIcons[i] == null) continue;
            Skill s = skills != null ? skills.GetSkill(i) : null;
            UIUtil.SetIcon(skillIcons[i], s != null ? s.icon : null);
        }
    }
}
