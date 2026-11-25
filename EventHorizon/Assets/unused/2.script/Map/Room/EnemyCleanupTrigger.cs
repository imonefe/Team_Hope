using UnityEngine;

/// <summary>
/// 플레이어가 닿으면 씬의 모든 Enemy 레이어 오브젝트 삭제
/// 맵 섹션 이동 시 이전 구역 적 정리용
/// </summary>
public class EnemyCleanupTrigger : MonoBehaviour
{
    bool triggered = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered) return;
        triggered = true;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        foreach (var go in FindObjectsOfType<GameObject>())
            if (go.layer == enemyLayer)
                Destroy(go);
    }
}
