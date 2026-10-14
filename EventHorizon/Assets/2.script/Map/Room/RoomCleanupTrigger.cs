using UnityEngine;

/// <summary>
/// 플레이어가 닿으면 미활성 스포너 제거 후
/// 남은 몬스터 전멸 시 포탈 + 박스 오픈
/// </summary>
public class RoomCleanupTrigger : MonoBehaviour
{
    Room room;
    bool triggered = false;

    void Awake()
    {
        room = GetComponentInParent<Room>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered) return;
        triggered = true;

        room?.RemoveUntriggeredSpawners();
    }
}
