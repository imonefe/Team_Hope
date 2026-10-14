using UnityEngine;

/// <summary>
/// 자식 오브젝트 애니메이션 이벤트를 부모로 전달
/// </summary>
public class AnimEventRelay : MonoBehaviour
{
    public PlayerController player;
    public Enemy enemy;

    // ── 플레이어 이벤트 ──────────────────────────────────
    public void PlayerSpawnHitbox() => player?.SpawnHitbox();
    public void OnAttackAnimEnd() => player?.OnAttackAnimEnd();
    public void EndAttack_Anim() => player?.EndAttack_Anim();

    // ── 적 이벤트 (기존 이름 유지 + 신규) ──────────────
    public void SpawnHitbox() => enemy?.SpawnHitbox();   // 기존 이름 유지
    public void EndAttack() => enemy?.EndAttack();     // 기존 이름 유지
    public void EnemySpawnHitbox() => enemy?.SpawnHitbox();
    public void EnemyEndAttack() => enemy?.EndAttack();
    public void EnemyLockDirection() => enemy?.LockDirection();
}