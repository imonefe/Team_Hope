using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버튼 하나에 연결되는 맵 데이터
/// Create > Game > Map > MapRoomData 로 생성
/// </summary>
[CreateAssetMenu(fileName = "New MapRoomData", menuName = "Game/Map/MapRoomData")]
public class MapRoomData : ScriptableObject
{
    [Header("맵 정보")]
    public MapTier tier;
    public MapType type;

    [Header("룸 프리팹 목록 (랜덤 선택)")]
    public List<GameObject> roomPrefabs = new List<GameObject>();

    /// <summary>
    /// 사용하지 않은 프리팹 중 랜덤 반환.
    /// 이미 사용된 프리팹은 (타입 상관없이) 다시 나오지 않는다 — 전부 소진되면
    /// 예전처럼 처음부터 다시 뽑지 않고, 더 보여줄 맵이 없다는 뜻으로 null을 반환한다.
    /// roomPrefabs가 부족해서 자주 null이 나온다면, 이 MapRoomData에 룸을 더 추가해야 한다는 신호다.
    /// </summary>
    public GameObject GetRoom(HashSet<GameObject> usedPrefabs)
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0)
        {
            Debug.LogWarning($"[MapRoomData] {name} 에 룸 프리팹이 없어요!");
            return null;
        }

        List<GameObject> available = roomPrefabs.FindAll(p => p != null && !usedPrefabs.Contains(p));

        if (available.Count == 0)
        {
            Debug.LogWarning($"[MapRoomData] {name}의 룸 프리팹이 이번 판에서 이미 전부 사용됐습니다. " +
                              "더 보여줄 새 맵이 없어요 — roomPrefabs에 룸을 더 추가해주세요.");
            return null;
        }

        return available[Random.Range(0, available.Count)];
    }
}
