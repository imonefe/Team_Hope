using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노드 캔버스(마인드맵) 흐름 관리.
///
/// 예전 방식: 씬에 캔버스를 미리 배치해두고 SetActive로 껐다 켰다 했음.
/// 지금 방식: 캔버스를 "스테이지별 프리팹 풀"(stagePools)에 등록해두고,
/// 필요한 시점에 그 중 하나를 랜덤으로 골라 Instantiate 해서 쓴다.
///
/// - 같은 스테이지를 플레이하는 동안(= 아직 보스를 못 잡은 동안)에는 생성된 캔버스를
///   Destroy 하지 않고 SetActive(false)로만 꺼둔다. 그래야 노드별 진행 상태
///   (isCompleted / isCurrent / isSelectable)가 다음에 열었을 때도 그대로 유지된다.
/// - 보스 노드를 선택해서 들어갔던 방을 클리어하고, 포탈을 통해 다시 OpenMap()이
///   호출되면 그때 비로소 캔버스를 실제로 Destroy 하고, 다음 스테이지 풀에서
///   새 캔버스를 랜덤으로 다시 뽑는다.
///
/// 빈 오브젝트에 붙이기.
/// </summary>
public class MapManager : PersistentSingleton<MapManager>
{

    [System.Serializable]
    public class StageCanvasPool
    {
        [Tooltip("인스펙터 구분용 이름 (로직에는 사용 안 함)")]
        public string stageName;

        [Tooltip("이 스테이지에 처음 들어왔을 때(혹은 이전 스테이지 보스를 잡고 넘어왔을 때) 먼저 들어가는 로비 방.\n" +
                 "보드게임으로 치면 '시작 칸' — 노드 캔버스에는 노드로 표시되지 않는다.\n" +
                 "이 방 안의 포탈을 이용해야 비로소 아래 canvasPrefabs 중 하나가 열린다.")]
        public GameObject lobbyPrefab;

        [Tooltip("이 스테이지에서 등장할 수 있는 노드 캔버스 프리팹들.\n" +
                 "이 스테이지의 캔버스가 처음 필요해질 때 이 중 하나를 랜덤으로 Instantiate 한다.")]
        public GameObject[] canvasPrefabs;
    }

    [Header("스테이지 순서대로 등록 (0번 = 1스테이지)")]
    public StageCanvasPool[] stagePools;

    [Header("생성된 노드 캔버스가 배치될 부모 (비워두면 이 오브젝트 바로 밑)")]
    public Transform canvasParent;

    [Header("현재 캔버스에서 자동으로 찾은 시작 노드 (읽기 전용 / 확인용)")]
    [SerializeField] List<MapButton> startButtons = new List<MapButton>();

    /// <summary>0부터 시작하는 현재 스테이지 인덱스. 보스를 잡을 때마다 1씩 증가한다.</summary>
    public int CurrentStageIndex { get; private set; } = 0;

    GameObject currentCanvasInstance; // 현재 스테이지 동안 유지되는 캔버스 인스턴스
    MapButton currentButton;          // 지금 플레이 중인(마지막으로 선택한) 노드
    bool mapPausing = false;          // [2026-10-10] 노드맵이 시간 정지·조작 잠금·전투 UI 숨김을 걸고 있는지

    // 이번 스테이지에서 이미 사용한 룸 프리팹 기록 (타입 상관없이, 중복 방 방지)
    HashSet<GameObject> usedRoomPrefabs = new HashSet<GameObject>();

    void Awake()
    {
        // 중복이면 스스로 파괴, 아니면 유일한 인스턴스로 등록 + 씬 전환에도 유지 (PersistentSingleton)
        if (!InitSingleton()) return;
    }

    // ── 마인드맵 열기 ─────────────────────────────────────
    public void OpenMap()
    {
        // 방금 클리어하고 나온 노드가 보스 노드였다면 → 스테이지 전환.
        // 이때는 캔버스를 바로 띄우지 않고, 새 스테이지의 로비로 먼저 보낸다.
        // (로비는 노드가 아니라 그냥 들어가 있는 방 — 로비 안의 포탈을 다시 써야
        //  비로소 아래 SpawnRandomCanvas()로 넘어가서 캔버스가 열린다)
        if (currentButton != null && currentButton.roomData != null &&
            currentButton.roomData.type == MapType.Boss)
        {
            AdvanceToNextStage();
            GoToLobby();
            return;
        }

        if (currentCanvasInstance == null)
            SpawnRandomCanvas();

        // [2026-10-10 수정] 캔버스를 못 만들었으면(풀이 비어 있음 등) 시간을 멈추지 않는다
        // (예전엔 화면에 아무것도 없이 게임만 멈춘 채 남았음)
        if (currentCanvasInstance == null) return;

        currentCanvasInstance.SetActive(true);
        BeginMapPause();
        RefreshAllButtons();
    }

    // [2026-10-10 추가] 노드맵이 떠 있는 동안: 시간 정지(다른 창과 겹쳐도 안전한 GamePause), 플레이어 조작 잠금,
    // 전투 UI 숨김. 예전엔 timeScale만 0으로 해서 노드맵 중에도 점프·대시·공격 입력이 쌓였다가 방에 들어가는 순간 한꺼번에 나갔음.
    void BeginMapPause()
    {
        if (mapPausing) return;
        mapPausing = true;
        GamePause.Push(GamePause.Map);
        CombatUIGate.Block();
        if (PlayerController.Instance != null) PlayerController.Instance.LockControl(this);
    }

    void EndMapPause()
    {
        if (!mapPausing) return;
        mapPausing = false;
        GamePause.Pop(GamePause.Map);   // 다른 창(ESC 메뉴 등)이 열려 있으면 시간은 계속 멈춰 있음
        CombatUIGate.Unblock();
        if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
    }

    /// <summary>노드맵이 지금 열려 있는지</summary>
    public bool IsMapOpen => mapPausing;

    // ── 스테이지 로비로 이동 ──────────────────────────────
    // 로비는 보드게임의 "시작 칸" 같은 개념 — 노드 캔버스에 버튼으로 그려지지 않고,
    // 스테이지에 처음 들어왔을 때(혹은 이전 스테이지 보스를 잡고 넘어왔을 때) 자동으로
    // 들어가 있는 방이다. 이 방 안의 포탈을 이용해야 그 스테이지의 노드 캔버스(분기 선택)가 열린다.
    void GoToLobby()
    {
        if (stagePools == null || stagePools.Length == 0) return;

        int idx = Mathf.Clamp(CurrentStageIndex, 0, stagePools.Length - 1);
        GameObject lobby = stagePools[idx].lobbyPrefab;

        if (lobby == null)
        {
            Debug.LogWarning($"[MapManager] {idx}번 스테이지에 로비 프리팹이 등록되지 않았습니다. 로비 없이 노드 캔버스를 바로 엽니다.");
            OpenMap(); // 폴백: currentButton이 이미 null이라 재귀해도 이번엔 바로 캔버스로 진행됨
            return;
        }

        EndMapPause();
        RoomManager.Instance.LoadRoom(lobby);
    }

    // ── 캔버스 랜덤 소환 ──────────────────────────────────
    void SpawnRandomCanvas()
    {
        if (stagePools == null || stagePools.Length == 0)
        {
            Debug.LogWarning("[MapManager] stagePools가 비어있습니다. 인스펙터에서 스테이지별 노드 캔버스 프리팹을 등록하세요.");
            return;
        }

        int idx = Mathf.Clamp(CurrentStageIndex, 0, stagePools.Length - 1);
        StageCanvasPool pool = stagePools[idx];
        if (pool == null || pool.canvasPrefabs == null || pool.canvasPrefabs.Length == 0)
        {
            Debug.LogWarning($"[MapManager] {idx}번 스테이지({(pool != null ? pool.stageName : "")})에 등록된 노드 캔버스 프리팹이 없습니다.");
            return;
        }

        GameObject prefab = pool.canvasPrefabs[Random.Range(0, pool.canvasPrefabs.Length)];
        Transform parent = canvasParent != null ? canvasParent : transform;
        currentCanvasInstance = Instantiate(prefab, parent);

        // 맵 화면도 열릴 때(SetActive(true)) 등장 애니메이션이 재생되도록 미리 붙여둔다.
        if (currentCanvasInstance.GetComponent<UIPanelAnimator>() == null)
            currentCanvasInstance.AddComponent<UIPanelAnimator>();

        AutoDetectStartButtons();
        currentButton = null; // 새 캔버스 = 새 진행. 시작 노드들만 선택 가능한 상태로 시작
    }

    // ── 시작 노드 자동 탐지 ───────────────────────────────
    // 다른 어떤 버튼의 connectedButtons 에도 걸려있지 않은 버튼 = 시작 노드로 취급한다.
    void AutoDetectStartButtons()
    {
        startButtons = new List<MapButton>();
        if (currentCanvasInstance == null) return;

        var all = currentCanvasInstance.GetComponentsInChildren<MapButton>(true);
        var referenced = new HashSet<MapButton>();
        foreach (var b in all)
            foreach (var c in b.connectedButtons)
                if (c != null) referenced.Add(c);

        foreach (var b in all)
            if (!referenced.Contains(b))
                startButtons.Add(b);
    }

    // ── 다음 스테이지로 전환 ──────────────────────────────
    void AdvanceToNextStage()
    {
        if (currentCanvasInstance != null)
        {
            Destroy(currentCanvasInstance);
            currentCanvasInstance = null;
        }

        CurrentStageIndex++;
        if (stagePools != null && stagePools.Length > 0 && CurrentStageIndex >= stagePools.Length)
        {
            // 등록된 마지막 스테이지 이후 — 일단 마지막 풀을 계속 재사용한다.
            // (엔딩/게임 클리어 처리는 별도로 붙여야 함)
            CurrentStageIndex = stagePools.Length - 1;
        }

        usedRoomPrefabs.Clear();
        currentButton = null;
    }

    // ── 버튼 선택 ─────────────────────────────────────────
    public void SelectButton(MapButton selected)
    {
        StartCoroutine(SelectButtonRoutine(selected));
    }

    IEnumerator SelectButtonRoutine(MapButton selected)
    {
        // 이전 현재 버튼 → 완료 처리
        if (currentButton != null)
        {
            currentButton.isCompleted = true;
            currentButton.isCurrent = false;
            currentButton.isSelectable = false;
            currentButton.RefreshVisual();
        }

        currentButton = selected;
        currentButton.isCurrent = true;
        currentButton.isSelectable = false;

        // 룸 프리팹 가져오기
        GameObject prefab = null;
        if (selected.roomData != null)
        {
            prefab = selected.roomData.GetRoom(usedRoomPrefabs);
            if (prefab != null)
                usedRoomPrefabs.Add(prefab); // 이제 타입 상관없이 한 번 나온 맵은 이번 스테이지 동안 다시 안 나옴
        }

        // 페이드 아웃 → 마인드맵 닫고(비활성화만, 삭제 아님) 방 로드 → 페이드 인
        if (TransitionCanvas.Instance != null)
        {
            yield return TransitionCanvas.Instance.Transition(() =>
            {
                if (currentCanvasInstance != null) currentCanvasInstance.SetActive(false);
                EndMapPause();
                if (prefab != null) RoomManager.Instance.LoadRoom(prefab);
            });
        }
        else
        {
            if (currentCanvasInstance != null) currentCanvasInstance.SetActive(false);
            EndMapPause();
            if (prefab != null) RoomManager.Instance.LoadRoom(prefab);
        }

        RefreshAllButtons();
    }

    // ── 전체 버튼 상태 갱신 ───────────────────────────────
    void RefreshAllButtons()
    {
        if (currentCanvasInstance == null) return;

        var allButtons = currentCanvasInstance.GetComponentsInChildren<MapButton>(true);
        foreach (var btn in allButtons)
        {
            if (!btn.isCompleted && !btn.isCurrent)
                btn.isSelectable = false;
        }

        if (currentButton == null)
        {
            foreach (var btn in startButtons)
                if (btn != null) btn.isSelectable = true;
        }
        else
        {
            foreach (var btn in currentButton.connectedButtons)
            {
                if (btn != null && !btn.isCompleted)
                    btn.isSelectable = true;
            }
        }

        // 지금은 못 가지만, 지금 갈 수 있는 노드에서 이어지는 길 위에 있어 나중에 갈 수 있는 노드 표시
        // (여기 안 들어가는 나머지 미완료 노드 = 이제 다시는 갈 수 없는 노드)
        var later = new HashSet<MapButton>();
        var queue = new Queue<MapButton>();
        foreach (var btn in allButtons)
            if (btn.isSelectable) queue.Enqueue(btn);
        while (queue.Count > 0)
        {
            var from = queue.Dequeue();
            foreach (var next in from.connectedButtons)
            {
                if (next == null || next.isSelectable || next.isCompleted || next.isCurrent) continue;
                if (later.Add(next)) queue.Enqueue(next);
            }
        }
        foreach (var btn in allButtons)
            btn.isReachableLater = later.Contains(btn);

        foreach (var btn in allButtons)
            btn.RefreshVisual();
    }

    // ── 런 초기화 (죽었을 때 / 새 게임) ───────────────────
    // RunManager.RestartRun() 에서 씬을 다시 불러오기 "직전"에 호출된다.
    // 첫 방은 씬이 다시 열리면서 RoomManager.Start() 가 startRoomPrefab 으로 알아서 불러오므로,
    // 여기서는 진행 기록만 지우고 방 이동/노드 캔버스 열기는 하지 않는다.
    // (예전에는 여기서 GoToLobby()를 불렀는데, 로비 프리팹이 비어 있으면 폴백으로 OpenMap()이 실행돼
    //  노드 캔버스가 이 오브젝트(씬 전환에도 유지됨) 밑에 새로 생성 → 새 게임 시작하자마자 노드 화면이 켜져 있던 버그)
    public void ResetRun()
    {
        // 진행 중이던 노드 선택(페이드 → 방 로드) 코루틴이 새 판까지 이어지지 않도록 중단
        StopAllCoroutines();

        if (currentCanvasInstance != null)
        {
            Destroy(currentCanvasInstance);
            currentCanvasInstance = null;
        }

        // 씬을 다시 불러오면 GamePause·CombatUIGate는 저절로 초기화되므로 기록만 지운다
        mapPausing = false;

        CurrentStageIndex = 0;
        currentButton = null;
        usedRoomPrefabs.Clear();
        startButtons.Clear();
    }
}
