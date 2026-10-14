using UnityEngine;

/// <summary>
/// 몬스터 소환 이펙트 프리팹들에 붙이는 스크립트.
/// 스프라이트 애니메이션 특정한 애니메이션 이벤트가 SpawnNow() 를 호출하면
/// 그 타이밍에 몬스터가 스폰된다. (이펙트가 먼저 재생되고 나서)
///
/// 사용법:
/// 1. 이펙트 프리팹에 이 스크립트를 붙인다.
/// 2. 스폰 애니메이션 클립에서 몬스터가 나타날 프레임에
///    애니메이션 이벤트를 걸어 Function 에 SpawnNow 를 지정한다.
/// 3. MonsterSpawner 의 Spawn Effect Prefab 슬롯에 이 프리팹을 넣는다.
/// </summary>
public class SpawnEffectTrigger : MonoBehaviour
{
    [Header("생존 시간 (0 = 애니메이션 길이 자동)")]
    public float lifetime = 0f;

    [Tooltip("자동 파괴 시 추가 시간 (초)")]
    public float extraTime = 0f;

    [Header("안전장치")]
    [Tooltip("이 시간이 지나도 애니메이션 이벤트가 안 오면 강제 스폰 (0 = 사용 안 함)")]
    public float fallbackTime = 2f;

    // 스포너가 전달하는 콜백 (실제 스폰은 여기서 실행)
    System.Action onSpawn;
    bool spawned = false;
    float timer = 0f;

    /// <summary>MonsterSpawner 가 호출해서 스폰 동작을 전달한다.</summary>
    public void Init(System.Action spawnAction)
    {
        onSpawn = spawnAction;
    }

    void Start()
    {
        Destroy(gameObject, EffectTimeUtil.ResolveAutoDestroyTime(gameObject, lifetime, extraTime));
    }

    void Update()
    {
        // 애니메이션 이벤트가 오지 않는 경우를 대비한 안전장치
        if (spawned || fallbackTime <= 0f) return;

        timer += Time.deltaTime;
        if (timer >= fallbackTime)
            SpawnNow();
    }

    /// <summary>애니메이션 이벤트에서 호출. 이 시점에 몬스터가 스폰된다.</summary>
    public void SpawnNow()
    {
        if (spawned) return;
        spawned = true;
        onSpawn?.Invoke();
    }
}
