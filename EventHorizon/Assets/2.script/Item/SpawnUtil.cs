using UnityEngine;

/// <summary>
/// 스폰 관련 공용 유틸. Box / BoxSpawner 등이 공통으로 사용.
/// </summary>
public static class SpawnUtil
{
    /// <summary>
    /// 배열에서 무작위 프리팹 하나를 골라 지정 위치에 생성.
    /// 배열이 null/비어있으면 아무것도 생성하지 않고 null 반환.
    /// </summary>
    public static GameObject InstantiateRandom(GameObject[] prefabs, Vector3 pos)
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        int idx = Random.Range(0, prefabs.Length);
        return Object.Instantiate(prefabs[idx], pos, Quaternion.identity);
    }

    /// <summary>
    /// [2026-10-10 추가] 조건(canPick)을 통과한 프리팹 중에서만 무작위로 골라 생성. 통과한 게 없으면 null.
    /// </summary>
    public static GameObject InstantiateRandom(GameObject[] prefabs, Vector3 pos, System.Predicate<GameObject> canPick)
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        var pool = new System.Collections.Generic.List<GameObject>();
        foreach (var p in prefabs)
            if (p != null && (canPick == null || canPick(p))) pool.Add(p);
        if (pool.Count == 0) return null;
        return Object.Instantiate(pool[Random.Range(0, pool.Count)], pos, Quaternion.identity);
    }
}
