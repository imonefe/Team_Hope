using UnityEngine;

/// <summary>
/// 캐릭터 한 명이 보유하는 스킬 2칸(슬롯 0/1). [2026-11-20] 3칸 → 2칸 (키 A·S)
/// 각 슬롯에는 Skill 프리팹 참조가 들어가며, 비어 있으면 null.
/// characterA / characterB 각각의 캐릭터 오브젝트에 이 컴포넌트를 붙인다.
///
/// 슬롯 배정 규칙 (AcquireSkill 사용 시 자동 적용):
///  - 0번 슬롯: 무기 스킬(Skill.SkillType.Weapon) 전용. 새로 획득하면 이전 것을 덮어쓴다.
///  - 1번 슬롯: 일반 스킬(Skill.SkillType.Normal) 하나. 차 있으면 상점에서 팔고 새로 사야 한다.
/// 특정 슬롯에 직접 지정하고 싶다면 SetSkill(slot, prefab)을 그대로 사용하면 된다.
///
/// 발동: Activate(슬롯) → 프리팹 Instantiate → Skill.Launch()
/// 쿨타임은 슬롯별로 관리한다.
/// </summary>
public class CharacterSkills : MonoBehaviour
{
    /// <summary>[2026-11-20] 스킬 칸 수 (0번 무기 + 1번 일반)</summary>
    public const int SlotCount = 2;

    [Header("보유 스킬 2칸 (0번=무기 전용 [A] / 1번=일반 [S]. 비면 null)")]
    [Tooltip("Skill 스크립트가 붙은 프리팹 2개")]
    public Skill[] skillSlots = new Skill[SlotCount];

    [Tooltip("스킬 생성 기준이 될 시전자 Transform (비우면 이 오브젝트)")]
    public Transform caster;

    // 슬롯별 쿨타임 종료 시각
    float[] cooldownEndTime = new float[SlotCount];

    void Awake()
    {
        if (caster == null) caster = transform;
        FitSlots();
    }

    // [2026-11-20] 예전 프리팹·씬에 3칸으로 저장된 배열을 2칸으로 맞춘다.
    // 1번이 비고 2번에만 스킬이 있었다면 1번으로 옮겨서 잃지 않게 한다.
    void FitSlots()
    {
        if (skillSlots == null) skillSlots = new Skill[SlotCount];
        if (skillSlots.Length != SlotCount)
        {
            var fit = new Skill[SlotCount];
            for (int i = 0; i < SlotCount && i < skillSlots.Length; i++) fit[i] = skillSlots[i];
            if (fit[1] == null && skillSlots.Length > 2) fit[1] = skillSlots[2];
            skillSlots = fit;
        }
        if (cooldownEndTime == null || cooldownEndTime.Length != SlotCount) cooldownEndTime = new float[SlotCount];
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 인스펙터에서도 2칸으로 보이게 (에디터 전용)
        if (skillSlots != null && skillSlots.Length != SlotCount) FitSlots();
    }
#endif

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
        bool free = CombatEvents.CanCastFree();   // [2026-11-21] 증강 '확장 소켓': 재사용 대기 없이 나가는 시전이 남았는지
        if (Time.time < cooldownEndTime[slot] && !free) return false; // 쿨타임 중

        // [2026-11-11] 홀로분신처럼 대상이 필요한 스킬은 화면에 적이 없으면 발동도, 쿨타임도 없이 무시
        HoloCloneSkill holo = prefab.GetComponent<HoloCloneSkill>();
        if (holo != null && !holo.HasEnemyOnScreen(caster != null ? caster : transform)) return false;

        int facing = GetFacing();

        // 프리팹 생성 후 발동
        Skill instance = Instantiate(prefab);
        instance.damageScale = CombatEvents.SkillCast(slot, free);   // [2026-11-21] 증강 (스킬 피해 배율 · 알림)
        instance.Launch(caster, facing);

        // 쿨타임 시작 (증강으로 공짜 시전이면 남은 쿨타임 그대로)
        if (!free) cooldownEndTime[slot] = Time.time + prefab.cooldown;
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

    /// <summary>[2026-11-25] 지금 쓸 수 있는 대상이 있는지 (재사용 대기는 따로) — 홀로 분신처럼 화면에 적이 있어야 하는 스킬은 적이 없으면 false. 스킬 칸 UI 가 어둡게 표시하는 데 씀</summary>
    public bool HasTargetFor(int slot)
    {
        if (slot < 0 || slot >= skillSlots.Length || skillSlots[slot] == null) return false;
        HoloCloneSkill holo = skillSlots[slot].GetComponent<HoloCloneSkill>();
        return holo == null || holo.HasEnemyOnScreen(caster != null ? caster : transform);
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
    /// - Normal: 1번 슬롯이 비어 있으면 배정
    /// 일반 스킬 슬롯(1번)이 이미 차 있으면 배정하지 못하고 false를 반환한다.
    /// </summary>
    public bool AcquireSkill(Skill prefab)
    {
        if (prefab == null) return false;

        if (prefab.skillType == Skill.SkillType.Weapon)
        {
            SetSkill(0, prefab);
            return true;
        }

        // Normal — 1번 칸 (칸 수가 늘면 그만큼 순서대로)
        for (int slot = 1; slot < skillSlots.Length; slot++)
        {
            if (skillSlots[slot] == null)
            {
                SetSkill(slot, prefab);
                return true;
            }
        }

        Debug.LogWarning($"[CharacterSkills] 일반 스킬 슬롯이 이미 가득 찼습니다. '{prefab.skillName}' 획득 실패.");
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
