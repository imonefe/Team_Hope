using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 몬스터 스포너
/// 트리거 콜라이더를 직접 지정하거나
/// 이 오브젝트의 콜라이더를 사용
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    public GameObject enemyPrefab;
    public Transform spawnPoint;

    [Header("스폰 이펙트 (선택)")]
    public GameObject spawnEffectPrefab;

    [Header("바닥 스냅")]
    [Tooltip("켜면 스폰 지점에서 아래로 레이캐스트해 바닥 위에 스폰")]
    public bool snapToGround = true;
    [Tooltip("바닥 탐색 최대 거리")]
    public float groundRayDistance = 30f;
    [Tooltip("바닥에서 띄울 높이 (발이 묻히면 올림)")]
    public float groundOffsetY = 0.4f;

    [Header("트리거 설정")]
    [Tooltip("트리거로 사용할 콜라이더 (비우면 이 오브젝트의 콜라이더 사용)")]
    public Collider2D triggerCollider;
    [Tooltip("플레이어가 닿으면 자동 스폰")]
    public bool triggerOnEnter = true;
    [Tooltip("한 번만 발동")]
    public bool triggerOnce = true;

    bool triggered = false;
    bool spawningDisabled = false; // [2026-10-10] 방 정리 트리거로 제외된 스포너는 이후 밟아도 스폰하지 않음

    /// <summary>플레이어가 트리거를 밟았는지 (스폰 이펙트 재생 중이라 아직 hasSpawned 가 false 여도 true)</summary>
    public bool IsTriggered => triggered;

    /// <summary>이 스포너가 더 이상 스폰하지 않도록 막는다 (Room.RemoveUntriggeredSpawners에서 호출)</summary>
    public void DisableSpawning() { spawningDisabled = true; }

    // 스폰 위치 아래 바닥 높이 (몸이 바닥에 묻히지 않게 콜라이더 기준으로 맞출 때 사용)
    bool resolvedHasGround = false;
    float resolvedGroundY = 0f;
    public bool hasSpawned = false;  // 실제로 스폰된 적 있는지
    private List<GameObject> spawnedMonsters = new List<GameObject>();
    private Vector3 resolvedSpawnPos;  // 바닥 스냅까지 반영된 최종 스폰 좌표

    void Awake()
    {
        // 트리거 콜라이더가 다른 오브젝트에 있으면 릴레이 연결
        if (triggerCollider != null && triggerCollider.gameObject != gameObject)
        {
            TriggerRelay relay = triggerCollider.gameObject.GetComponent<TriggerRelay>();
            if (relay == null)
                relay = triggerCollider.gameObject.AddComponent<TriggerRelay>();
            if (!relay.spawners.Contains(this))
                relay.spawners.Add(this);
        }
    }

    // ── 트리거 수신 ──────────────────────────────────────
    public void OnPlayerEntered()
    {
        if (!triggerOnEnter) return;
        if (spawningDisabled) return;
        if (triggerOnce && triggered) return;

        triggered = true;
        SpawnMonster();
    }

    // 이 오브젝트가 직접 트리거일 때
    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerCollider != null && triggerCollider.gameObject != gameObject) return;
        if (!other.CompareTag("Player")) return;
        OnPlayerEntered();
    }

    // ── 수동 스폰 ────────────────────────────────────────
    public void SpawnMonster()
    {
        if (enemyPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[MonsterSpawner] 스폰 설정 안됨");
            return;
        }

        // 바닥 위치를 먼저 확정 (이펙트와 몬스터가 같은 자리에 나오도록)
        resolvedSpawnPos = GetGroundedPosition(spawnPoint.position);
        ResolveGround(spawnPoint.position);

        // 스폰 이펙트가 있으면 먼저 재생하고,
        // 이펙트의 애니메이션 이벤트(SpawnNow) 타이밍에 몬스터를 생성한다.
        if (spawnEffectPrefab != null)
        {
            GameObject fx = Instantiate(spawnEffectPrefab, resolvedSpawnPos, Quaternion.identity);

            SpawnEffectTrigger trigger = fx.GetComponent<SpawnEffectTrigger>()
                                      ?? fx.GetComponentInChildren<SpawnEffectTrigger>();
            if (trigger != null)
            {
                trigger.Init(DoSpawn);
                return; // 실제 스폰은 애니메이션 이벤트가 호출
            }
            // 트리거 스크립트가 없으면 기존처럼 즉시 스폰
        }

        DoSpawn();
    }

    // 실제 몬스터 생성
    void DoSpawn()
    {
        if (enemyPrefab == null || spawnPoint == null) return;

        hasSpawned = true;
        GameObject monster = Instantiate(enemyPrefab, resolvedSpawnPos, Quaternion.identity);
        SnapBodyToGround(monster);
        spawnedMonsters.Add(monster);
    }

    void ResolveGround(Vector3 from)
    {
        resolvedHasGround = false;
        if (!snapToGround) return;
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, groundRayDistance, LayerMask.GetMask("ground", "platform"));
        if (hit.collider == null) return;
        resolvedHasGround = true;
        resolvedGroundY = hit.point.y;
    }

    // [2026-10-10 추가] 적마다 기준점과 발(콜라이더 아래끝) 사이 거리가 달라서, 바닥 위 groundOffsetY 고정값으로 놓으면
    // 몸이 바닥에 0.2~0.4칸 묻힌 채 생겨 튀어 오르거나 한쪽 통과 발판 아래로 빠질 수 있었다.
    // → 생성 직후 몸(통과 판정이 아닌 콜라이더)의 아래끝을 바닥 바로 위에 맞춘다.
    void SnapBodyToGround(GameObject monster)
    {
        if (!resolvedHasGround || monster == null) return;
        Collider2D body = null;
        foreach (var c in monster.GetComponents<Collider2D>())
            if (c != null && c.enabled && !c.isTrigger) { body = c; break; }
        if (body == null) return;

        Physics2D.SyncTransforms();
        float delta = (resolvedGroundY + 0.02f) - body.bounds.min.y;
        monster.transform.position += new Vector3(0f, delta, 0f);
        Rigidbody2D rb = monster.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = monster.transform.position;
    }

    /// <summary>스폰 지점에서 아래로 레이캐스트해 바닥 위 좌표를 반환. 바닥이 없으면 원래 좌표.</summary>
    Vector3 GetGroundedPosition(Vector3 from)
    {
        if (!snapToGround) return from;

        RaycastHit2D hit = Physics2D.Raycast(
            from, Vector2.down, groundRayDistance,
            LayerMask.GetMask("ground", "platform"));

        if (hit.collider == null) return from; // 바닥 못 찾으면 그대로

        return new Vector3(from.x, hit.point.y + groundOffsetY, from.z);
    }

    // 에디터에서 스폰 지점과 바닥 스냅 결과를 표시
    void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPoint.position, 0.2f);

        if (!snapToGround) return;

        Vector3 grounded = GetGroundedPosition(spawnPoint.position);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(spawnPoint.position, grounded);
        Gizmos.DrawWireSphere(grounded, 0.25f);
    }

    public bool IsCleared()
    {
        if (!hasSpawned) return false; // 스폰된 적 없으면 전멸 아님
        spawnedMonsters.RemoveAll(m => m == null);
        return spawnedMonsters.Count == 0;
    }

}

/// <summary>
/// 트리거 콜라이더가 다른 오브젝트에 있을 때
/// 자동으로 붙는 릴레이 컴포넌트
/// 여러 스포너를 동시에 지원
/// </summary>
public class TriggerRelay : MonoBehaviour
{
    [HideInInspector]
    public List<MonsterSpawner> spawners = new List<MonsterSpawner>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        foreach (var spawner in spawners)
            spawner?.OnPlayerEntered();
    }
}