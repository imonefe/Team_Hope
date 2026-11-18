using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 튜토리얼용 영역 (트리거).
/// - Reach: 플레이어가 들어오면 reached = true (발판 내려가기 등 "여기까지 오면 통과")
/// - Respawn: 플레이어가 들어오면 지금 구간 시작 위치로 되돌림 (구덩이 바닥, 피해 없음)
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialZone : MonoBehaviour
{
    public enum Kind { Reach, Respawn }
    public Kind kind = Kind.Reach;
    public TutorialController controller;

    [HideInInspector] public bool reached;

    void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

    // [2026-11-19] 이미 영역 안에 서 있는 채로 구간이 시작돼도(들어오는 순간을 놓쳐도) 도착으로 친다
    void OnTriggerStay2D(Collider2D other)
    {
        if (kind == Kind.Reach && !reached && other.CompareTag("Player")) reached = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (kind == Kind.Reach) reached = true;
        else if (controller != null) controller.Respawn(other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject);
    }
}
