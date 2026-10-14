using UnityEngine;

/// <summary>
/// 캐릭터 한 명이 보유하는 스킬 3칸(슬롯 0/1/2).
/// 각 슬롯에는 Skill 프리팹 참조가 들어가며, 비어 있으면 null.
/// characterA / characterB 각각의 캐릭터 오브젝트에 이 컴포넌트를 붙인다.
///
/// 슬롯 배정 규칙 (AcquireSkill 사용 시 자동 적용):
///  - 0번 슬롯: 무기 스킬(Skill.SkillType.Weapon) 전용. 새로 획득하면 이전 것을 덮어쓴다.
///  - 1~2번 슬롯: 일반 스킬(Skill.SkillType.Normal). 얻은 순서대로 빈 슬롯에 채워진다.
/// 특정 슬롯에 직접 지정하고 싶다면 SetSkill(slot, prefab)을 그대로 사용하면 된다.
///
/// 발동: Activate(슬롯) → 프리팹 Instantiate → Skill.Launch()
/// 쿨타임은 슬롯별로 관리한다.
/// </summary>
public class CharacterSkills : MonoBehaviour
{
    [Header("보유 스킬 3칸 (0번=무기 전용 / 1~2번=일반, 얻은 순서. 비면 null)")]
    [Tooltip("Skill 스크립트가 붙은 프리팹 3개")]
    public Skill[] skillSlots = new Skill[3];

    [Tooltip("스킬 생성 기준이 될 시전자 Transform (비우면 이 오브젝트)")]
    public Transform caster;

    // 슬롯별 쿨타임 종료 시각
    float[] cooldownEndTime = new float[3];

    void Awake()
    {
        if (caster == null) caster = transform;
    }

    /// <summary>슬롯의 스킬 발동. 성공하면 true.</summary>
    public bool Activate(int slot)
    {
        if (slot < 0 || slot >= skillSlots.Length) return false;

        // [2026-10-10 추가] 창이 떠 있거나(시간 정지) 대화·메뉴로 조작이 잠겨 있으면 스킬을 쓰지 않는다
        // (예전엔 ESC 메뉴·인벤토리·노드맵·대화 중에도 A/S/D로 스킬이 나가고 쿨타임이 걸렸음)
        if (Time.timeScale == 0f) return false;
        PlayerController pc = caster != null ? caster.GetComponent<PlayerController>() : null;
        if (pc == null) pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsControlLocked) return false;

        Skill prefab = skillSlots[slot];
        if (prefab == null) return false;                 // 빈 슬롯
        if (Time.time < cooldownEndTime[slot]) return false; // 쿨타임 중

        int facing = GetFacing();

        // 프리팹 생성 후 발동
        Skill instance = Instantiate(prefab);
        instance.Launch(caster, facing);

        // 쿨타임 시작
        cooldownEndTime[slot] = Time.time + prefab.cooldown;
        return true;
    }

    /// <summary>슬롯 쿨타임 진행도 0~1 (UI용). 1이면 사용 가능.</summary>
    public float GetCooldownRatio(int slot)
    {
        if (slot < 0 || slot >= skillSlots.Length) return 1f;
        Skill s = skillSlots[slot];
        if (s == null || s.cooldown <= 0f) return 1f;

        float remain = cooldownEndTime[slot] - Time.time;
        if (remain <= 0f) return 1f;
        return 1f - (remain / s.cooldown);
    }

    /// <summary>슬롯의 남은 쿨타임(초). 0이면 사용 가능(쿨타임 아님). UI 카운트다운 표시용.</summary>
    public float GetCooldownRemaining(int slot)
    {
        if (slot < 0 || slot >= skillSlots.Length) return 0f;
        float remain = cooldownEndTime[slot] - Time.time;
        return remain > 0f ? remain : 0f;
    }

    /// <summary>슬롯에 스킬 직접 세팅 (슬롯 번호를 정확히 알 때 사용). 프리팹 참조 저장.</summary>
    public void SetSkill(int slot, Skill prefab)
    {
        if (slot < 0 || slot >= skillSlots.Length) return;
        skillSlots[slot] = prefab;
        cooldownEndTime[slot] = 0f;
    }

    /// <summary>
    /// 스킬 획득 시 호출하는 진입점. skillType을 보고 슬롯을 자동으로 정해준다.
    /// - Weapon: 0번 슬롯에 배정 (이미 있던 무기 스킬은 교체됨)
    /// - Normal: 1번 → 2번 순서로, 비어있는 슬롯에 얻은 순서 그대로 배정
    /// 일반 스킬 슬롯(1~2번)이 이미 둘 다 차 있으면 배정하지 못하고 false를 반환한다.
    /// </summary>
    public bool AcquireSkill(Skill prefab)
    {
        if (prefab == null) return false;

        if (prefab.skillType == Skill.SkillType.Weapon)
        {
            SetSkill(0, prefab);
            return true;
        }

        // Normal — 1번, 2번 순서로 빈 슬롯을 찾아 얻은 순서대로 채운다
        for (int slot = 1; slot <= 2; slot++)
        {
            if (skillSlots[slot] == null)
            {
                SetSkill(slot, prefab);
                return true;
            }
        }

        Debug.LogWarning($"[CharacterSkills] 일반 스킬 슬롯(1~2번)이 이미 가득 찼습니다. '{prefab.skillName}' 획득 실패.");
        return false;
    }

    public Skill GetSkill(int slot)
    {
        if (slot < 0 || slot >= skillSlots.Length) return null;
        return skillSlots[slot];
    }

    // 시전자 방향 (localScale.x 부호 기준, 없으면 1)
    int GetFacing()
    {
        return caster.localScale.x < 0 ? -1 : 1;
    }
}
