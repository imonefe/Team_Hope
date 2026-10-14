using UnityEngine;

/// <summary>
/// Room.cs 에서 SetActive(true) 호출 시
/// 스폰 포인트 위에 박스 소환
/// </summary>
public class BoxSpawner : MonoBehaviour
{
    [Header("설정")]
    public GameObject[] boxPrefabs;  // 4개 등록
    public Transform spawnPoint;

    [Header("스폰 이펙트 (선택)")]
    public GameObject spawnEffectPrefab;
    public Transform effectPoint;  // 비우면 spawnPoint 위치 사용

    GameObject spawnedBox;

    public void SetActive(bool value)
    {
        if (value)
            Spawn();
        else
            Despawn();
    }

    void Spawn()
    {
        if (spawnedBox != null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        // [2026-10-10 수정] 플레이어에게 새로운 것을 줄 수 있는 상자 종류 중에서만 고른다
        // (예: 희귀 아이템을 다 모았으면 일반·에픽·전설 상자 중에서). 전부 모았으면 아무 상자나(열면 재화).
        spawnedBox = SpawnUtil.InstantiateRandom(boxPrefabs, pos, Box.HasSomethingNew);
        if (spawnedBox == null) spawnedBox = SpawnUtil.InstantiateRandom(boxPrefabs, pos);
        if (spawnedBox == null) return;

        spawnedBox.transform.SetParent(transform, true);

        // 스폰 이펙트 (상자 나오는 자리 가리기용)
        if (spawnEffectPrefab != null)
        {
            Vector3 fxPos = effectPoint != null ? effectPoint.position : pos;
            Instantiate(spawnEffectPrefab, fxPos, Quaternion.identity);
        }

        // 박스 활성화
        Box box = spawnedBox.GetComponent<Box>();
        if (box != null) box.SetActive(true);
    }

    void Despawn()
    {
        if (spawnedBox != null)
        {
            Destroy(spawnedBox);
            spawnedBox = null;
        }
    }
}
