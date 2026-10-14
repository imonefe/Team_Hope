using UnityEngine;

/// <summary>
/// 모든 아이템 효과의 기본 틀
/// 새 효과 추가할 때 이 클래스 상속받아서 Apply() 구현
/// </summary>
public abstract class ItemEffect : ScriptableObject
{
    public abstract void Apply(Stats stats, MonoBehaviour runner);
}
