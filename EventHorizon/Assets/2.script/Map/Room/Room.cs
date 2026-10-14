using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField] private Portal portal;
    [SerializeField] private BoxSpawner[] boxSpawners;
    [SerializeField] private MonsterSpawner[] spawners;

    [Header("몬스터 감지")]
    public LayerMask monsterLayer;
    public Vector2 checkSize = new Vector2(50, 50);

    [Header("보스 방 카메라 (선택)")]
    [Tooltip("체크하면 이 방에 들어왔을 때 카메라가 플레이어를 따라가지 않고 방 전체를 잡아준다. " +
             "보스를 잡아 포탈이 열리면(OpenPortal) 자동으로 원래 플레이어 추적으로 돌아온다.")]
    public bool isBossRoom = false;
    [Tooltip("카메라에 담을 방의 가로×세로 크기(월드 단위). 방 전체가 화면 안에 들어오도록 넉넉하게 설정 — 에디터에서 눈으로 보며 조절 필요")]
    public Vector2 roomCameraSize = new Vector2(24f, 14f);
    [Tooltip("카메라가 바라볼 중심점(비워두면 이 Room 오브젝트 자신의 위치를 중심으로 사용)")]
    public Transform roomCameraCenter;

    bool portalOpened = false;
    bool cleanupMode = false; // CleanupTrigger 발동 후 몬스터만 감지

    public Vector3 GetSpawnPosition()
    {
        PlayerSpawner ps = GetComponentInChildren<PlayerSpawner>();
        return ps != null ? ps.GetSpawnPosition() : transform.position;
    }

    void Start()
    {
        if (spawners == null || spawners.Length == 0)
            spawners = GetComponentsInChildren<MonsterSpawner>();

        if (portal != null) portal.SetActive(false);
        foreach (var bs in boxSpawners)
            if (bs != null) bs.SetActive(false);

        if (isBossRoom && CameraFollow.Instance != null)
        {
            Vector3 center = roomCameraCenter != null ? roomCameraCenter.position : transform.position;
            CameraFollow.Instance.HoldRoom(center, roomCameraSize);
        }

        InvokeRepeating(nameof(CheckClear), 0.5f, 0.2f);
    }

    void CheckClear()
    {
        if (portalOpened) return;

        // CleanupTrigger 발동 후 → 몬스터만 감지
        if (cleanupMode)
        {
            if (!HasAliveMonster())
                OpenPortal();
            return;
        }

        // 스포너 없는 방 → 즉시 오픈
        if (spawners == null || spawners.Length == 0)
        {
            OpenPortal();
            return;
        }

        // 아직 스폰 안 된 스포너 있으면 대기
        foreach (var s in spawners)
            if (s != null && !s.hasSpawned) return;

        // 전부 스폰됨 → 몬스터 전멸 확인
        if (!HasAliveMonster())
            OpenPortal();
    }

    bool HasAliveMonster()
    {
        // 잡몹(Enemy 컴포넌트) 감지
        foreach (var e in FindObjectsOfType<Enemy>())
            if (e != null && e.gameObject.activeInHierarchy)
                return true;

        // 보스(BossController 컴포넌트) 감지 — 레이어 무관
        foreach (var b in FindObjectsOfType<BossController>())
            if (b != null && b.gameObject.activeInHierarchy)
                return true;

        return false;
    }

    public void OpenPortal()
    {
        portalOpened = true;
        CancelInvoke(nameof(CheckClear));
        if (portal != null) portal.SetActive(true);
        foreach (var bs in boxSpawners)
            if (bs != null) bs.SetActive(true);

        if (isBossRoom && CameraFollow.Instance != null)
            CameraFollow.Instance.ReleaseRoom();
    }

    // CleanupTrigger 에서 호출 — 미활성 스포너 제거
    public void RemoveUntriggeredSpawners()
    {
        if (spawners == null) return;

        // [2026-10-10 수정] 트리거는 밟았지만 스폰 이펙트가 끝나지 않아 아직 생성 전인 스포너도 "발동됨"으로 남긴다.
        // (예전엔 그런 스포너를 지워서 몬스터가 나오기 전에 포탈이 열렸음)
        // 제외된(아직 안 밟은) 스포너는 이후 밟아도 스폰하지 않게 막는다 (포탈이 열린 뒤 되돌아가면 새로 나오던 문제)
        foreach (var s in spawners)
            if (s != null && !s.hasSpawned && !s.IsTriggered) s.DisableSpawning();

        spawners = System.Array.FindAll(spawners,
            s => s != null && (s.hasSpawned || s.IsTriggered));

        bool pendingSpawn = System.Array.Exists(spawners, s => !s.hasSpawned);
        if (pendingSpawn) return; // 생성이 끝나면 CheckClear가 전멸 여부를 보고 포탈을 연다

        if (spawners.Length == 0 || !HasAliveMonster())
            OpenPortal();
    }

    public void ActivateCleanupMode()
    {
        cleanupMode = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, checkSize);

        // 보스방 카메라가 실제로 잡을 범위를 씬 뷰에서 미리 확인할 수 있도록 표시
        // (Play 모드에 안 들어가도 roomCameraCenter/roomCameraSize 값을 눈으로 보며 조절 가능)
        if (isBossRoom)
        {
            Vector3 camCenter = roomCameraCenter != null ? roomCameraCenter.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(camCenter, new Vector3(roomCameraSize.x, roomCameraSize.y, 0f));

            // 카메라 중심점도 작은 십자로 표시
            float crossSize = 0.5f;
            Gizmos.DrawLine(camCenter + Vector3.left * crossSize, camCenter + Vector3.right * crossSize);
            Gizmos.DrawLine(camCenter + Vector3.up * crossSize, camCenter + Vector3.down * crossSize);
        }
    }
}