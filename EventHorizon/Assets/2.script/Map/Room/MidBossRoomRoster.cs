using System;
using UnityEngine;

/// <summary>
/// [2026-11-19 추가] 중간 보스 공용 방(01_mid_boss)에서, 들어온 코어 종류에 맞는 보스만 남기는 목록.
///
/// - 방 프리팹 안에 보스들을 미리 놓아 두고 모두 꺼 둔다 (위치·크기는 에디터에서 잡는다).
/// - 코어로 들어오면 RoomManager 가 Activate(종류) 를 부른다 → 같은 종류의 보스만 켜고 나머지는 지운다.
/// - 방 루트(Room 이 있는 오브젝트)에 붙인다.
/// </summary>
public class MidBossRoomRoster : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public BossCoreKind kind;
        [Tooltip("방 안에 미리 놓아 둔 보스 (꺼 둘 것)")]
        public GameObject boss;
    }

    public Entry[] bosses;

    public void Activate(BossCoreKind kind)
    {
        if (bosses == null) return;
        foreach (Entry e in bosses)
        {
            if (e == null || e.boss == null) continue;
            if (e.kind == kind) e.boss.SetActive(true);
            else Destroy(e.boss);
        }
    }

#if UNITY_EDITOR
    // [2026-11-19 추가] 꺼 둔 보스는 Unity 가 콜라이더 외곽선을 그리지 않으므로, 꺼진 보스의 콜라이더를 대신 그린다 (에디터 전용)
    static readonly Color ColliderColor = new Color(0.57f, 0.96f, 0.54f, 0.9f);

    void OnDrawGizmos()
    {
        if (Application.isPlaying || bosses == null) return;
        foreach (Entry e in bosses)
        {
            if (e == null || e.boss == null || e.boss.activeInHierarchy) continue;
            Gizmos.color = ColliderColor;
            foreach (Collider2D c in e.boss.GetComponentsInChildren<Collider2D>(true))
            {
                if (c == null || !c.enabled) continue;
                Gizmos.matrix = c.transform.localToWorldMatrix;
                if (c is BoxCollider2D b) Gizmos.DrawWireCube(b.offset, b.size);
                else if (c is CircleCollider2D ci) DrawCircle(ci.offset, ci.radius);
                else if (c is CapsuleCollider2D cap) Gizmos.DrawWireCube(cap.offset, cap.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.color = ColliderColor;
            UnityEditor.Handles.Label(e.boss.transform.position + Vector3.up * 0.3f, e.boss.name + " (꺼짐)");
        }
    }

    static void DrawCircle(Vector2 center, float r)
    {
        const int seg = 32;
        Vector3 prev = center + new Vector2(r, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
