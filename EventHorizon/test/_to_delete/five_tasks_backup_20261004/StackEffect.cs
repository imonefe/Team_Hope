using UnityEngine;

/// <summary>
/// 스택 쌓일수록 강해지는 효과
/// 스택 수는 RunManager 에서 관리 (ScriptableObject 직접 수정 불가 문제 해결)
/// </summary>
[CreateAssetMenu(fileName = "New StackEffect", menuName = "Game/Effect/Stack")]
public class StackEffect : ItemEffect
{
    [Header("스택 설정")]
    public int maxStack = 10;

    [Header("스택당 스탯 변화")]
    public StatModifier modifierPerStack;

    public override void Apply(Stats stats, MonoBehaviour runner)
    {
        if (RunManager.Instance == null)
        {
            Debug.LogWarning("[StackEffect] RunManager 없음!");
            return;
        }

        // RunManager 에서 스택 추가 시도
        if (!RunManager.Instance.TryAddStack(this, maxStack))
        {
            Debug.Log($"[StackEffect] 최대 스택 도달 ({maxStack})");
            return;
        }

        int current = RunManager.Instance.GetStack(this);
        modifierPerStack.Apply(stats);
        Debug.Log($"[StackEffect] 스택 {current}/{maxStack}");
    }
}