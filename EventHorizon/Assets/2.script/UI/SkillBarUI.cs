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

    [Header("발동 키 (이후 조정)")]
    public KeyCode[] skillKeys = { KeyCode.Q, KeyCode.W, KeyCode.E };

    CharacterSkills current;

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
        if (current == null) return;

        // 스킬 발동
        for (int i = 0; i < skillKeys.Length && i < 3; i++)
        {
            if (Input.GetKeyDown(skillKeys[i]))
                current.Activate(i);
        }

        // 쿨타임 표시: 사용하는 순간 이미지·텍스트 활성화, 쿨타임 끝나면 둘 다 비활성화(재사용 가능 알림)
        for (int i = 0; i < 3; i++)
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
            if (slotIcons[i] == null) continue;

            Skill s = current != null ? current.GetSkill(i) : null;
            UIUtil.SetIcon(slotIcons[i], s != null ? s.icon : null);
        }
    }
}
