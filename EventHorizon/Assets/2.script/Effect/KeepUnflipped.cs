using UnityEngine;

/// <summary>
/// [2026-11-19 추가] 부모가 좌우 반전(localScale.x &lt; 0)돼도 이 오브젝트는 반전되지 않게 되돌린다.
/// Skill.Launch 가 왼쪽 시전 시 스킬 전체의 localScale.x 를 음수로 뒤집는데,
/// 화면 왜곡 셰이더(유리조각·충격파)는 UV 방향으로 화면을 밀기 때문에 뒤집히면 왜곡 모양이 달라진다.
/// 이 컴포넌트를 이펙트 자식에 붙이면 위치는 방향을 따라가고, 모양은 좌우 어느 쪽에서 시전해도 같다.
/// </summary>
public class KeepUnflipped : MonoBehaviour
{
    void LateUpdate()
    {
        Transform p = transform.parent;
        if (p == null) return;
        float parentSign = Mathf.Sign(p.lossyScale.x);
        Vector3 s = transform.localScale;
        float want = Mathf.Abs(s.x) * parentSign; // 부모가 -면 자신도 - → 결과적으로 +
        if (!Mathf.Approximately(s.x, want))
        {
            s.x = want;
            transform.localScale = s;
        }
    }
}
