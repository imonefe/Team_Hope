using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 우측(캐릭터 이미지) + 중앙(스킬 3칸 / 스탯 5줄) UI.
/// 캐릭터 교차 시 이미지·스킬·스탯을 현재 캐릭터 것으로 갱신한다.
///
/// - 캐릭터 이미지: 현재 캐릭터(앞) + 대기 캐릭터(뒤, 어둡게)
/// - 교차: SwapButton 또는 키 입력 → CharacterManager.SwitchCharacter()
/// - 스킬 3칸: 아이콘 + 이름 + 설명
/// - 스탯 5줄: 체력만 슬라이더(현재/최대, DOTween으로 부드럽게 변화), 나머지는 수치
///
/// 무기 표시는 기존 WeaponUI.cs가 담당한다 (이 스크립트는 관여하지 않음).
/// </summary>
public class CharacterPanelUI : MonoBehaviour
{
    [Header("캐릭터 이미지 (겹침)")]
    public Image charFront;             // 현재 캐릭터
    public Image charBack;              // 대기 캐릭터 (어둡게)
    public Sprite portraitA;            // characterA 초상화
    public Sprite portraitB;            // characterB 초상화
    [Tooltip("대기 캐릭터를 어둡게 표현할 색")]
    public Color backTint = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Button swapButton;           // 교차 버튼
    public KeyCode swapKey = KeyCode.V; // 교차 키 (임시) — C는 PlayerController 점프와 겹쳐서 V로 변경

    [Header("스킬 3칸")]
    public Image[] skillIcons = new Image[3];
    public TextMeshProUGUI[] skillNames = new TextMeshProUGUI[3];
    public TextMeshProUGUI[] skillDescs = new TextMeshProUGUI[3];

    [Header("스탯 - 체력 (슬라이더)")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;      // "현재 / 최대"

    [Header("DOTween 트윈 설정 (체력/방어막 슬라이더)")]
    public float tweenDuration = 0.35f;
    public Ease tweenEase = Ease.OutQuad;

    // (방어막/물 속성 UI는 제거됨 — 속성 시스템 자체가 게임에서 빠지면서 함께 제거)

    [Header("스탯 - 나머지 (수치)")]
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI spdText;
    public TextMeshProUGUI focusText;

    Stats subscribedStats;

    void Start()
    {
        if (swapButton != null)
            swapButton.onClick.AddListener(OnSwap);

        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged += OnCharacterChanged;

        RefreshAll();
        SubscribeToCurrentCharacter();
    }

    // 인벤토리 창(이 패널의 상위 InventoryPanel)이 열릴 때마다(비활성 → 활성) 자동 호출됨.
    // 인스펙터에서 스탯을 직접 바꿔놓고 창을 열었다 닫았다 해도 최신값이 반영되게 하기 위함.
    void OnEnable()
    {
        RefreshAll();
        SubscribeToCurrentCharacter();
    }

    void OnDisable()
    {
        UnsubscribeStats();
        if (hpSlider != null) hpSlider.DOKill();
    }

    void OnDestroy()
    {
        if (swapButton != null)
            swapButton.onClick.RemoveListener(OnSwap);

        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= OnCharacterChanged;

        UnsubscribeStats();
    }

    void Update()
    {
        // [2026-10-10] 창이 떠 있거나 대화 중이면 입력을 받지 않음
        if (Time.timeScale == 0f || NPC.AnyTalking) return;
        if (Input.GetKeyDown(swapKey))
            OnSwap();
    }

    // ────────────────────────────────────────────
    // 교차
    // ────────────────────────────────────────────
    void OnSwap()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.SwitchCharacter();
        // 갱신은 OnCharacterChanged 이벤트로 자동 호출됨
    }

    void OnCharacterChanged(GameObject current)
    {
        RefreshAll();
        SubscribeToCurrentCharacter();
    }

    // ────────────────────────────────────────────
    // 체력 실시간 반영 (Stats.OnHpChanged 구독)
    // 패널이 열려있는 동안(인벤토리 창 등) 전투 중 체력이 바뀌어도 이 패널이 바로 따라가도록 함.
    // ────────────────────────────────────────────
    void SubscribeToCurrentCharacter()
    {
        UnsubscribeStats();

        Stats st = CharacterManager.Instance != null ? CharacterManager.Instance.CurrentStats : null;
        if (st != null)
        {
            subscribedStats = st;
            subscribedStats.OnHpChanged += HandleHpChanged;
        }
    }

    void UnsubscribeStats()
    {
        if (subscribedStats != null)
            subscribedStats.OnHpChanged -= HandleHpChanged;
        subscribedStats = null;
    }

    void HandleHpChanged(int current, int max)
    {
        if (hpText != null)
            hpText.text = current + " / " + max;

        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.DOKill();
            hpSlider.DOValue(current, tweenDuration).SetEase(tweenEase);
        }
    }

    // ────────────────────────────────────────────
    // 전체 갱신 (패널이 열릴 때/캐릭터 교차 시 — 트윈 없이 바로 반영)
    // ────────────────────────────────────────────
    void RefreshAll()
    {
        RefreshPortrait();
        RefreshSkills();
        RefreshStats();
    }

    void RefreshPortrait()
    {
        var cm = CharacterManager.Instance;
        if (cm == null) return;

        bool isA = cm.CurrentCharacter == cm.characterA;
        Sprite front = isA ? portraitA : portraitB;
        Sprite back = isA ? portraitB : portraitA;

        if (charFront != null)
        {
            UIUtil.SetIcon(charFront, front);
            charFront.color = Color.white;
        }
        if (charBack != null)
        {
            UIUtil.SetIcon(charBack, back);
            charBack.color = backTint;   // 대기 캐릭터 어둡게
        }
    }

    void RefreshSkills()
    {
        var skills = CharacterManager.Instance != null ? CharacterManager.Instance.CurrentSkills : null;

        for (int i = 0; i < 3; i++)
        {
            Skill s = skills != null ? skills.GetSkill(i) : null;

            if (skillIcons != null && i < skillIcons.Length && skillIcons[i] != null)
                UIUtil.SetIcon(skillIcons[i], s != null ? s.icon : null);
            if (skillNames != null && i < skillNames.Length && skillNames[i] != null)
                skillNames[i].text = s != null ? s.skillName : "";
            if (skillDescs != null && i < skillDescs.Length && skillDescs[i] != null)
                skillDescs[i].text = s != null ? s.description : "";
        }
    }

    void RefreshStats()
    {
        Stats st = CharacterManager.Instance != null ? CharacterManager.Instance.CurrentStats : null;
        if (st == null) return;

        // 체력 (슬라이더 + 현재/최대) — 패널을 처음 열거나 캐릭터를 교차한 시점이라 트윈 없이 바로 반영
        if (hpSlider != null)
        {
            hpSlider.DOKill();
            hpSlider.maxValue = st.maxHp;
            hpSlider.value = st.currentHp;
        }
        if (hpText != null)
            hpText.text = st.currentHp + " / " + st.maxHp;

        // 나머지 스탯 (수치)
        if (atkText != null) atkText.text = Mathf.RoundToInt(st.Atk).ToString();
        if (defText != null) defText.text = Mathf.RoundToInt(st.Def).ToString();
        if (spdText != null) spdText.text = Mathf.RoundToInt(st.spd).ToString();
        if (focusText != null) focusText.text = Mathf.RoundToInt(st.Focus).ToString();
    }
}
