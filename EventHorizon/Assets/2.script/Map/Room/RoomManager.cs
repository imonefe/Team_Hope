using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [SerializeField] private GameObject startRoomPrefab;

    private GameObject currentRoomObj;

    // 게임 시작 시점에 이미 씬에 존재하던 최상위(루트) 오브젝트들.
    // 맵 전환 시 이 목록에 없는 루트 오브젝트(스킬/히트박스/이펙트/스폰된 몹/드랍 오브젝트 등
    // 런타임에 생성된 것들)는 전부 정리 대상이 된다.
    private readonly HashSet<GameObject> persistentRoots = new HashSet<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        // 이 시점(첫 방이 열리기 전)에 씬에 있는 루트 오브젝트 = 게임 전체에서 유지돼야 할 것들
        // (플레이어, 각종 매니저, 배경, 카메라, UI 캔버스 등). 맵 전환 정리 대상에서 항상 제외한다.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            persistentRoots.Add(root);

        if (startRoomPrefab != null)
        {
            LoadRoom(startRoomPrefab);
        }
        else
        {
            Debug.LogError("Start Room Prefab이 설정되지 않음");
        }
    }

    public void LoadRoom(GameObject roomPrefab)
    {
        if (roomPrefab == null)
        {
            Debug.LogError("roomPrefab is null");
            return;
        }

        // 이전 맵에서 스폰된 잔여물(스킬/히트박스/이펙트/적/드랍 오브젝트 등) 정리
        CleanupLeftoverRuntimeObjects();

        // 이전 방 제거
        if (currentRoomObj != null)
        {
            Destroy(currentRoomObj);
        }

        // 새 방 생성
        currentRoomObj = Instantiate(roomPrefab);

        // Room 컴포넌트
        Room room = currentRoomObj.GetComponent<Room>();

        if (room == null)
        {
            Debug.LogError("Room 컴포넌트 없음");
            return;
        }

        // 플레이어 이동
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            playerObj.transform.position = room.GetSpawnPosition();

            // [2026-10-10 추가] 포탈에서 점프·낙하 중이던 속도가 새 방까지 이어지지 않게 초기화
            Rigidbody2D playerRb = playerObj.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.velocity = Vector2.zero;
                playerRb.position = playerObj.transform.position;
            }

            // 방 전환으로 플레이어가 멀리 순간이동했으므로, 카메라도 서서히 쫓아가게 두지 말고
            // 즉시 플레이어 위치로 스냅시킨다. (CameraFollow.cs 참고)
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.SnapToTargets();
        }

        // 🔥 핵심: 스포너 갱신
        TestMaster testMaster = FindObjectOfType<TestMaster>();

        if (testMaster != null)
        {
            testMaster.RefreshSpawners();
        }
    }

    // 맵 전환 시 호출 — 이전 맵에서 스폰된 것들 중 게임 시작부터 있던 영구 오브젝트가 아닌 건 전부 삭제.
    // (스킬 프리팹, Hitbox, 각종 이펙트/파티클, MonsterSpawner가 만든 몬스터, 드랍된 Box/아이템,
    //  투사체 등 — Room 프리팹의 자식이 아니라 씬 루트에 독립적으로 생성되는 모든 런타임 소환물)
    void CleanupLeftoverRuntimeObjects()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            if (root == currentRoomObj) continue;      // 방 자체는 바로 아래에서 따로 Destroy됨
            if (persistentRoots.Contains(root)) continue; // 플레이어/매니저 등 영구 오브젝트는 보호
            Destroy(root);
        }
    }
}