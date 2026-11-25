using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HP 바 인근에 배치되는 스킬 3칸 UI.
/// 현재 캐릭터의 CharacterSkills 를 읽어 아이콘·쿨타임을 표시하고,
/// 캐릭터가 태그 전환되면 그 캐릭터의 스킬로 교체한다(빈 칸도 반영).
///
/// 씬 구성:
/// - HP 바 옆에 스킬 박스 3개 배치
/// - slotIcons: 각 슬롯 아이콘 Image
/// - cooldownOverlays: 각 슬롯 쿨타임 표시 Image (Filled 타입 권장)
/// - 입력 키로 스킬 발동 (키는 이후 조정)
/// </summary>
public class SkillBarUI : MonoBehaviour
{
    [Header("슬롯 3칸")]
    public Image[] slotIcons = new Image[3];
    [Tooltip("쿨타임 표시용. 쿨타임 진행에 맞춰 세로 스케일이 위에서부터 줄어듦(RectTransform pivot Y = 1 권장). 스킬 사용 시 활성화, 쿨타임 끝나면 비활성화됨. 없으면 생략")]
    public Image[] cooldownOverlays = new Image[3];
    [Tooltip("쿨타임 남은 시간(초)을 보여주는 텍스트. cooldownOverlays와 함께 켜지고 꺼짐. 없으면 생략")]
    public TextMeshProUGUI[] cooldownTexts = new TextMeshProUGUI[3];

    [Header("발동 키 — [2026-11-11] 지금은 쓰지 않음. 실제 키는 설정의 조작 키(KeyBindings 스킬 1·2)")]
    public KeyCode[] skillKeys = { KeyCode.A, KeyCode.S };

    CharacterSkills current;

    // [2026-11-25] 스킬이 바뀌면 아이콘 다시 그림 (예전엔 시작 · 캐릭터 교체 때만 그려서 상점에서 산 스킬 아이콘이 안 바뀌었음)
    readonly Skill[] shownSkill = new Skill[3];
    // [2026-11-25] 지금 못 쓰는 스킬(대상 없음)은 아이콘을 어둡게
    [Header("[2026-11-25] 못 쓸 때 아이콘 색")]
    public Color unusableTint = new Color(0.38f, 0.38f, 0.38f, 1f);
    readonly bool[] usable = { true, true, true };
    float usableTimer;
    // [2026-11-25] 마우스를 올린 스킬 칸 (정보 팝업)
    int hoverSlot = -1;

    void OnEnable()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged += OnCharacterChanged;
    }
    void OnDisable()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= OnCharacterChanged;
    }

    void Start()
    {
        // 시작 시 현재 캐릭터로 초기화
        if (CharacterManager.Instance != null)
            current = CharacterManager.Instance.CurrentSkills;
        RefreshIcons();
    }

    void OnCharacterChanged(GameObject character)
    {
        // 태그 전환 → 새 캐릭터의 스킬로 교체
        current = character != null ? character.GetComponent<CharacterSkills>() : null;
        RefreshIcons();
    }

    void Update()
    {
        if (current == null) { HideHover(); return; }
        RefreshIfChanged();
        UpdateUsable();
        UpdateHover();

        // 스킬 발동
        int n = Mathf.Min(CharacterSkills.SlotCount, current.skillSlots != null ? current.skillSlots.Length : 0);   // [2026-11-20] 2칸
        for (int i = 0; i < n; i++)
        {
            if (KeyBindings.Down(KeyAction.Skill1 + i))   // [2026-11-11] 설정에서 바꾼 키
                current.Activate(i);
        }

        // 쿨타임 표시: 사용하는 순간 이미지·텍스트 활성화, 쿨타임 끝나면 둘 다 비활성화(재사용 가능 알림)
        for (int i = 0; i < n; i++)
        {
            float remain = current.GetCooldownRemaining(i); // 남은 시간(초). 0이면 사용 가능
            bool onCooldown = remain > 0f;

            if (i < cooldownOverlays.Length && cooldownOverlays[i] != null)
                cooldownOverlays[i].gameObject.SetActive(onCooldown);

            if (i < cooldownTexts.Length && cooldownTexts[i] != null)
            {
                cooldownTexts[i].gameObject.SetActive(onCooldown);
                if (onCooldown)
                    cooldownTexts[i].text = Mathf.CeilToInt(remain).ToString();
            }
        }
    }

    /// <summary>현재 캐릭터의 스킬 아이콘 갱신 (빈 칸도 반영)</summary>
    void RefreshIcons()
    {
        for (int i = 0; i < slotIcons.Length; i++)
        {
            Skill s = current != null ? current.GetSkill(i) : null;
            if (i < shownSkill.Length) shownSkill[i] = s;
            if (slotIcons[i] == null) continue;
            UIUtil.SetIcon(slotIcons[i], s != null ? s.icon : null);
        }
        usableTimer = 0f;
    }

    // [2026-11-25] 칸의 스킬이 바뀌었으면 (상점 구매 · 획득) 아이콘 다시 그림
    void RefreshIfChanged()
    {
        for (int i = 0; i < slotIcons.Length && i < shownSkill.Length; i++)
            if (current.GetSkill(i) != shownSkill[i]) { RefreshIcons(); return; }
    }

    // [2026-11-25] 0.2초마다 '지금 쓸 수 있는지' 확인 → 못 쓰면 아이콘을 어둡게 (재사용 대기 중은 기존 덮개가 표시)
    void UpdateUsable()
    {
        usableTimer -= Time.unscaledDeltaTime;
        if (usableTimer > 0f) return;
        usableTimer = 0.2f;
        for (int i = 0; i < slotIcons.Length && i < usable.Length; i++)
        {
            if (slotIcons[i] == null) continue;
            usable[i] = current.GetSkill(i) == null || current.HasTargetFor(i);
            slotIcons[i].color = usable[i] ? Color.white : unusableTint;
        }
    }

    // [2026-11-25] 스킬 칸에 마우스를 올리면 이름 · 설명 팝업
    void UpdateHover()
    {
        int over = -1;
        for (int i = 0; i < slotIcons.Length; i++)
            if (slotIcons[i] != null && current.GetSkill(i) != null && UIUtil.MouseOver(slotIcons[i].rectTransform)) { over = i; break; }
        if (over == hoverSlot) return;
        hoverSlot = over;
        if (over >= 0) ItemInfoPopup.Instance?.ShowSkill(current.GetSkill(over));
        else ItemInfoPopup.Instance?.Hide();
    }

    void HideHover()
    {
        if (hoverSlot < 0) return;
        hoverSlot = -1;
        ItemInfoPopup.Instance?.Hide();
    }
}
