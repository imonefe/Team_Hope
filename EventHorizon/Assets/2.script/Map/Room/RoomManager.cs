using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    // [2026-10-18 추가] 새 방을 열 때마다 알림 (새 방, 게임 시작 때 여는 첫 방인지). RoomBGM 이 방 종류별 배경음을 바꾸는 데 씀
    public static event System.Action<Room, bool> OnRoomLoaded;

    [SerializeField] private GameObject startRoomPrefab;

    private GameObject currentRoomObj;
    private bool currentIsLobby;   // [2026-11-07] 지금 방이 로비인지 (로비 → 전투로 넘어갈 때 퍼즈 게이지 초기화용)

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

        // [2026-11-21] 스테이지 구성(일시정지 화면 '스테이지 설정')이 있으면 켜 둔 첫 칸부터 시작
        if (MapManager.Instance != null && MapManager.Instance.BeginPlannedRun())
        {
        }
        else if (startRoomPrefab != null)
        {
            LoadRoom(startRoomPrefab);
        }
        else
        {
            Debug.LogError("Start Room Prefab이 설정되지 않음");
        }
    }

    // [2026-11-07] 로비 판정: 게임 시작 방이거나, 허수아비(TrainingDummy)가 놓인 방
    bool IsLobbyRoom(GameObject roomPrefab)
    {
        return roomPrefab == startRoomPrefab || roomPrefab.GetComponentInChildren<TrainingDummy>(true) != null;
    }

    public void LoadRoom(GameObject roomPrefab)
    {
        if (roomPrefab == null)
        {
            Debug.LogError("roomPrefab is null");
            return;
        }

        // [2026-11-19] 중간 보스 방에 들어가 있는 동안 다른 방으로 가면(사망 → 로비 등) 맡겨 둔 원래 방은 버린다
        if (InSubRoom) AbandonSubRoom();

        // [2026-11-07] 로비에서 전투 방으로 넘어가면 퍼즈 게이지 초기화 (로비 허수아비로 채운 게이지를 들고 가지 않게)
        bool nextIsLobby = IsLobbyRoom(roomPrefab);
        if (currentIsLobby && !nextIsLobby) PauseManager.Instance?.ResetForNewRun();
        currentIsLobby = nextIsLobby;

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
            FaceRight(playerObj.transform);   // [2026-11-24] 방에 들어가면 항상 오른쪽을 봄

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

        OnRoomLoaded?.Invoke(room, roomPrefab == startRoomPrefab);   // [2026-10-18 추가]
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
            if (root == savedRoomObj || suspendedRoots.Contains(root)) continue;   // [2026-11-19] 중간 보스 방에 간 동안 맡겨 둔 원래 방의 것
            Destroy(root);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // [2026-11-19 추가] 중간 보스 방 다녀오기 (MidBossGate 코어에서 호출)
    //  들어갈 때: 지금 방과 그 방에서 생긴 것들(드랍 아이템 등)을 지우지 않고 꺼 둔 채 중간 보스 방을 연다
    //  보스를 쓰러뜨리면: 잠깐 기다렸다가 처치 기록(RunManager "MidBoss.<종류>")을 남기고,
    //                    중간 보스 방을 지운 뒤 꺼 둔 원래 방을 다시 켜고 코어 앞 자리로 돌려보낸다
    //  돌아온 뒤 OnSubRoomReturned 로 알림 → 코어가 부서지는 연출을 한다
    // ════════════════════════════════════════════════════════════════
    public static event System.Action<BossCoreKind, bool> OnSubRoomReturned;

    [Header("중간 보스 방 (2026-11-19)")]
    [Tooltip("중간 보스를 쓰러뜨린 뒤 원래 방으로 돌아가기까지 기다리는 시간 (초)")]
    [SerializeField] private float subRoomReturnDelay = 2.5f;

    public bool InSubRoom => savedRoomObj != null;

    GameObject savedRoomObj;
    Vector3 savedPlayerPos;
    BossCoreKind subKind;
    readonly List<GameObject> suspendedRoots = new List<GameObject>();
    Coroutine subWatch;

    public void EnterSubRoom(GameObject subRoomPrefab, BossCoreKind kind, Vector3 returnPosition)
    {
        if (subRoomPrefab == null || InSubRoom || subTransitioning) return;
        StartCoroutine(SubRoomTransition(() => DoEnterSubRoom(subRoomPrefab, kind, returnPosition)));   // [2026-11-19] 회로 연출
    }

    // [2026-11-19 추가] 중간 보스 방 드나들기 전환: 회로가 화면을 덮는 동안 플레이어는 조작 잠금 + 피해 무시
    bool subTransitioning;
    IEnumerator SubRoomTransition(System.Action onMidpoint)
    {
        subTransitioning = true;
        PlayerController pc = PlayerController.Instance;
        Health hp = pc != null ? pc.GetComponent<Health>() : null;
        System.Func<bool> prevInvulnerable = null;
        if (pc != null) pc.LockControl(this);
        if (hp != null) { prevInvulnerable = hp.invulnerable; hp.invulnerable = () => true; }

        if (TransitionCanvas.Instance != null)
            yield return TransitionCanvas.Instance.StartCoroutine(TransitionCanvas.Instance.CircuitTransition(onMidpoint));
        else onMidpoint?.Invoke();

        if (hp != null) hp.invulnerable = prevInvulnerable;
        if (pc != null) pc.ResumeControl(this);
        subTransitioning = false;
    }

    void DoEnterSubRoom(GameObject subRoomPrefab, BossCoreKind kind, Vector3 returnPosition)
    {
        savedRoomObj = currentRoomObj;
        savedPlayerPos = returnPosition;
        subKind = kind;

        // 원래 방에서 생긴 것들은 지우지 않고 꺼 둔다 (돌아오면 그대로)
        suspendedRoots.Clear();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root == null || root == currentRoomObj || persistentRoots.Contains(root)) continue;
            if (!root.activeSelf) continue;
            suspendedRoots.Add(root);
            root.SetActive(false);
        }
        if (savedRoomObj != null) savedRoomObj.SetActive(false);

        currentRoomObj = Instantiate(subRoomPrefab);
        Room room = currentRoomObj.GetComponent<Room>();
        if (room != null) room.DisablePortal();   // [2026-11-19] 중간 보스 방 포탈로 나가지 못하게 (돌아가기는 아래 WatchSubBoss 가 함)
        MidBossRoomRoster roster = currentRoomObj.GetComponent<MidBossRoomRoster>();   // [2026-11-19] 공용 방: 들어온 코어 종류의 보스만 남김
        if (roster != null) roster.Activate(kind);
        MovePlayerTo(room != null ? room.GetSpawnPosition() : Vector3.zero);
        TestMaster testMaster = FindObjectOfType<TestMaster>();
        if (testMaster != null) testMaster.RefreshSpawners();
        if (room != null) OnRoomLoaded?.Invoke(room, false);

        if (subWatch != null) StopCoroutine(subWatch);
        subWatch = StartCoroutine(WatchSubBoss());
    }

    IEnumerator WatchSubBoss()
    {
        yield return null;
        // 보스(BossController)가 있으면 보스가 쓰러질 때까지,
        // [2026-11-19] 보스가 없으면(일반 적으로 만든 중간 보스·시험용 몬스터) 방 안 적이 한 번이라도 나온 뒤 모두 사라질 때까지 기다린다.
        // (원래 방의 적은 꺼져 있어서 찾기에 잡히지 않는다)
        // [2026-11-19 수정] 보스를 한 번 찾은 뒤에는 "처치 표시 / 체력 0 / 오브젝트가 지워짐" 중 하나면 처치로 본다
        //  (처치 표시를 놓친 채 보스가 지워지면 영원히 기다리던 문제)
        BossController boss = null;
        Stats bossStats = null;
        bool sawBoss = false;
        bool sawEnemy = false;
        Debug.Log("[RoomManager] 중간 보스 감시 시작");
        while (true)
        {
            if (currentRoomObj == null) { Debug.LogWarning("[RoomManager] 중간 보스 방이 사라져 감시 중단"); yield break; }
            if (!sawBoss)
            {
                foreach (BossController b in currentRoomObj.GetComponentsInChildren<BossController>(false))
                    if (b != null) { boss = b; break; }
                if (boss != null)
                {
                    sawBoss = true;
                    bossStats = boss.GetComponent<Stats>();
                    Debug.Log($"[RoomManager] 중간 보스 찾음: {boss.name}");
                }
            }
            if (sawBoss)
            {
                if (boss == null || boss.IsDefeated || (bossStats != null && bossStats.currentHp <= 0)) break;
            }
            else
            {
                bool alive = false;
                foreach (Enemy e in FindObjectsOfType<Enemy>())
                    if (e != null && e.gameObject.activeInHierarchy) { alive = true; break; }
                if (alive) sawEnemy = true;
                else if (sawEnemy) break;
            }
            yield return null;
        }
        Debug.Log("[RoomManager] 중간 보스 처치 감지 → " + subRoomReturnDelay + "초 뒤 복귀");
        // [2026-11-21] 보스 보상 증강을 고를 때까지 복귀 연출(회로 셰이더)·방 이동을 보류
        yield return null;
        while (AugmentOfferDirector.IsBusy) yield return null;
        // [2026-11-19 수정] 실제 시간으로 센다 (공격 연출의 순간 정지 등으로 게임 시간이 느려진 채 남아도 복귀하도록).
        //  일시정지 메뉴가 열려 있는 동안은 세지 않는다
        float waited = 0f;
        while (waited < subRoomReturnDelay)
        {
            if (!GamePause.IsPaused && Time.timeScale > 0f) waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (RunManager.Instance != null) RunManager.Instance.SetUpgradeLevel(BossCore.DefeatKey(subKind), 1);
        subWatch = null;
        Debug.Log("[RoomManager] 원래 방으로 복귀 시작");
        ReturnFromSubRoom(true);
    }

    /// <summary>중간 보스 방에서 원래 방으로 돌아간다 (defeated = 보스를 쓰러뜨렸는지)</summary>
    public void ReturnFromSubRoom(bool defeated)
    {
        if (!InSubRoom) return;
        if (subWatch != null) { StopCoroutine(subWatch); subWatch = null; }
        StartCoroutine(SubRoomTransition(() => DoReturn(defeated)));   // [2026-11-19] 회로 연출
    }

    void DoReturn(bool defeated)
    {
        if (!InSubRoom) return;
        CleanupLeftoverRuntimeObjects();                 // 중간 보스 방에서 생긴 것들 정리 (맡겨 둔 것은 건드리지 않음)
        if (currentRoomObj != null) Destroy(currentRoomObj);

        currentRoomObj = savedRoomObj;
        savedRoomObj = null;
        currentRoomObj.SetActive(true);
        foreach (GameObject root in suspendedRoots) if (root != null) root.SetActive(true);
        suspendedRoots.Clear();

        if (CameraFollow.Instance != null) CameraFollow.Instance.ReleaseRoom();   // [2026-11-19] 보스방 카메라 고정이 남지 않게
        MovePlayerTo(savedPlayerPos);
        TestMaster testMaster = FindObjectOfType<TestMaster>();
        if (testMaster != null) testMaster.RefreshSpawners();
        Room room = currentRoomObj.GetComponent<Room>();
        if (room != null) OnRoomLoaded?.Invoke(room, false);

        OnSubRoomReturned?.Invoke(subKind, defeated);
    }

    void AbandonSubRoom()
    {
        if (subWatch != null) { StopCoroutine(subWatch); subWatch = null; }
        foreach (GameObject root in suspendedRoots) if (root != null) Destroy(root);
        suspendedRoots.Clear();
        if (savedRoomObj != null) Destroy(savedRoomObj);
        savedRoomObj = null;
    }

    void MovePlayerTo(Vector3 pos)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;
        playerObj.transform.position = pos;
        FaceRight(playerObj.transform);   // [2026-11-24] 중간보스 방 드나들 때도 오른쪽
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.velocity = Vector2.zero; rb.position = pos; }
        if (CameraFollow.Instance != null) CameraFollow.Instance.SnapToTargets();
    }

    /// <summary>[2026-11-24] 플레이어가 오른쪽을 보게 (PlayerController.Flip 과 같은 방식: 가로 크기 부호 = 보는 방향)</summary>
    static void FaceRight(Transform player)
    {
        Vector3 s = player.localScale;
        player.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
    }
}
