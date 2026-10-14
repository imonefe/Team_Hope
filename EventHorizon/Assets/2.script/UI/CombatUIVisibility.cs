using UnityEngine;

/// <summary>
/// 인벤토리/대화 등으로 CombatUIGate가 잠기면 전투 UI(예: battle Canvas)를 끄고,
/// 전부 풀리면 다시 켠다. combatUIRoots에 꺼야 할 오브젝트를 등록해둔다(여러 개 가능).
///
/// 반드시 항상 활성 상태인 오브젝트(예: InventoryManager)에 붙여야 한다 —
/// 전투 UI 자기 자신에 붙이면, 꺼지는 순간 이 스크립트도 같이 멈춰서(OnDisable만 불리고
/// 다시 못 켜서) 이후 다시 켤 수 없게 된다.
/// </summary>
public class CombatUIVisibility : MonoBehaviour
{
    [Tooltip("인벤토리/대화 중에 꺼야 할 전투 UI 오브젝트들 (예: battle Canvas)")]
    public GameObject[] combatUIRoots;

    void OnEnable()
    {
        CombatUIGate.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        CombatUIGate.OnChanged -= Refresh;
    }

    void Refresh()
    {
        if (combatUIRoots == null) return;

        bool show = !CombatUIGate.IsBlocked;
        foreach (var root in combatUIRoots)
        {
            if (root != null)
                root.SetActive(show);
        }
    }
}
