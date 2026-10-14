using UnityEngine;

/// <summary>
/// 스킬 획득용 월드 오브젝트. Weapon.cs와 같은 방식으로, 플레이어가 닿으면
/// (F키 없이 즉시) PlayerController.OnTriggerEnter2D에서 이 컴포넌트를 찾아 처리한다.
/// skillPrefab은 CharacterSkills.skillSlots에 들어가는 것과 같은 Skill 프리팹을 그대로 참조하면 된다.
/// </summary>
public class SkillPickup : MonoBehaviour
{
    [Tooltip("획득 시 CharacterSkills.AcquireSkill()에 그대로 전달될 스킬 프리팹")]
    public Skill skillPrefab;
}
