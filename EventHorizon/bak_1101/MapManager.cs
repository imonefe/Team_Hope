using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
/// [2026-10-15 추가] 스타트룸 노드와 내 위치 핀
/// - 캔버스 안에 isStartRoom 이 체크된 버튼(1번 노드)이 있으면, 캔버스가 열릴 때부터 그 버튼이 '지금 있는 곳'이다.
///   (없는 캔버스는 예전처럼 다른 노드가 가리키지 않는 버튼들이 처음 고를 수 있는 노드)
/// - 지금 있는 노드 위에 금색 마름모 핀(MapPositionMarker)이 떠 있고, 노드를 고르면 핀이 그 노드로 옮겨 간 뒤 화면이 전환된다.
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

    [Header("내 위치 핀 (지금 있는 노드 위에 뜨는 금색 마름모)")]
    [Tooltip("핀 그림. 비워 두면 Resources 폴더의 UINodeMarker 그림을 쓴다")]
    public Sprite positionMarkerSprite;
    [Tooltip("핀 크기 (픽셀, 1920×1080 기준). 그림 비율은 그대로 유지된다")]
    public Vector2 positionMarkerSize = new Vector2(64f, 96f);
    [Tooltip("노드 가운데에서 핀 아래 꼭짓점까지의 높이 (노드 크기 180 기준, 80 이면 노드 위 꼭짓점 바로 위)")]
    public float positionMarkerHeight = 80f;
    [Tooltip("핀이 위아래로 흔들리는 폭 (픽셀)")]
    public float positionMarkerBob = 8f;
    [Tooltip("핀이 1초에 몇 번 흔들리는지")]
    public float positionMarkerBobSpeed = 0.9f;
    [Tooltip("다른 노드를 골랐을 때 핀이 옮겨 가는 시간 (초, 실제 시간). 0 이면 옮겨 가는 연출 없이 바로 전환")]
    public float positionMarkerMoveTime = 0.35f;
    [Tooltip("옮겨 가는 동안 살짝 뛰어오르는 높이 (픽셀, 0 = 직선)")]
    public float positionMarkerHop = 30f;

    [Header("방향키 조작 (2026-10-21 추가)")]
    [Tooltip("상하좌우 방향키로 갈 수 있는 노드를 고르고, 스페이스·F·엔터로 결정")]
    public bool keyboardNavigation = true;

    [Header("키 안내 (2026-10-21 추가, 캔버스가 만들어질 때 코드로 붙음)")]
    [Tooltip("키 안내 글꼴 (비우면 TMP 기본 글꼴 — 한글이 안 나올 수 있음)")]
    public TMP_FontAsset uiFont;
    [Tooltip("키 안내를 띄울지 (방향키 조작이 꺼져 있으면 안 띄움)")]
    public bool showKeyGuide = true;
    [Tooltip("키 상자 그림 (전투 화면 조작키 안내와 같은 그림)")]
    public Sprite keyBoxSprite;
    [Tooltip("화면 오른쪽 아래 모서리에서 떨어진 거리")]
    public Vector2 keyGuideOffset = new Vector2(-36f, 30f);
    public float keyGuideFontSize = 22f;
    public float keyBoxHeight = 30f;
    public Color keyGuideColor = new Color(0.95f, 0.92f, 0.85f, 1f);

    /// <summary>0부터 시작하는 현재 스테이지 인덱스. 보스를 잡을 때마다 1씩 증가한다.</summary>
    public int CurrentStageIndex { get; private set; } = 0;

    GameObject currentCanvasInstance; // 현재 스테이지 동안 유지되는 캔버스 인스턴스
    MapButton currentButton;          // 지금 플레이 중인(마지막으로 선택한) 노드
    bool mapPausing = false;          // [2026-10-10] 노드맵이 시간 정지·조작 잠금·전투 UI 숨김을 걸고 있는지
    MapPositionMarker positionMarker; // [2026-10-15] 지금 있는 노드 위의 핀 (캔버스 안에 만들어지고 캔버스와 함께 사라짐)
    bool selecting = false;           // [2026-10-15] 핀이 옮겨 가는 중·화면 전환 중에 노드가 또 눌리지 않도록
    MapButton keyFocus;               // [2026-10-21] 방향키로 고른 노드

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
        selecting = false;

        // [2026-10-15] 스타트룸 노드가 있으면 처음부터 그 노드에 서 있는 상태로 시작
        // → RefreshAllButtons 가 스타트룸에서 이어진 노드들만 고를 수 있게 만든다
        MapButton startRoom = FindStartRoomButton();
        if (startRoom != null)
        {
            currentButton = startRoom;
            startRoom.isCurrent = true;
            startRoom.isSelectable = false;
            startRoom.isCompleted = false;
        }

        CreatePositionMarker();
        CreateMapLabels();
    }

    // [2026-10-15] 캔버스 안에서 isStartRoom 이 체크된 버튼 (없으면 null)
    MapButton FindStartRoomButton()
    {
        if (currentCanvasInstance == null) return null;
        foreach (var b in currentCanvasInstance.GetComponentsInChildren<MapButton>(true))
            if (b.isStartRoom) return b;
        return null;
    }

    // [2026-10-15] 캔버스 안에 내 위치 핀을 만든다 (캔버스가 사라지면 같이 사라짐)
    void CreatePositionMarker()
    {
        positionMarker = null;
        if (currentCanvasInstance == null) return;

        Sprite sprite = positionMarkerSprite != null ? positionMarkerSprite : Resources.Load<Sprite>("UINodeMarker");
        if (sprite == null)
        {
            Debug.LogWarning("[MapManager] 내 위치 핀 그림이 없습니다. (Resources 폴더의 UINodeMarker 또는 인스펙터의 positionMarkerSprite)");
            return;
        }

        positionMarker = MapPositionMarker.Create(currentCanvasInstance.transform, sprite, positionMarkerSize,
                                                  positionMarkerHeight, positionMarkerBob, positionMarkerBobSpeed);
        positionMarker.SnapTo(currentButton);
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
        if (selecting) return;   // [2026-10-15] 핀이 옮겨 가는 중·화면 전환 중에는 무시
        StartCoroutine(SelectButtonRoutine(selected));
    }

    IEnumerator SelectButtonRoutine(MapButton selected)
    {
        selecting = true;

        // [2026-10-15] 핀이 지금 노드에서 고른 노드로 옮겨 간 뒤에 화면 전환
        if (positionMarker != null && positionMarkerMoveTime > 0f)
            yield return positionMarker.MoveTo(selected, positionMarkerMoveTime, positionMarkerHop);

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
        selecting = false;
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

        // [2026-10-21] 고를 수 있는 노드가 바뀌었으니 방향키 선택은 처음부터
        SetKeyFocus(null);

        // [2026-10-15] 핀을 지금 있는 노드 위로 (지금 있는 노드가 없으면 숨김)
        if (positionMarker != null)
            positionMarker.SnapTo(currentButton);
    }

    // ── [2026-10-21 추가] 방향키로 노드 고르기 ─────────────
    void Update()
    {
        if (!keyboardNavigation || !mapPausing || selecting) return;
        if (currentCanvasInstance == null || !currentCanvasInstance.activeInHierarchy) return;
        // ESC 메뉴·인벤토리·게임오버가 노드맵 위에 떠 있으면 무시
        if (GamePause.Has(GamePause.Menu) || GamePause.Has(GamePause.Inventory) || GamePause.Has(GamePause.GameOver)) return;

        Vector2 dir = Vector2.zero;
        if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector2.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector2.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector2.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector2.right;

        if (dir != Vector2.zero)
        {
            MoveKeyFocus(dir);
            return;
        }

        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)
                    || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!confirm) return;

        if (keyFocus != null && keyFocus.isSelectable) SelectButton(keyFocus);
        else MoveKeyFocus(Vector2.zero);   // 아직 아무것도 안 골랐으면 결정 대신 가장 가까운 노드를 먼저 고름
    }

    // 지금 고른 노드(없으면 지금 있는 노드)에서 dir 방향에 있는 '갈 수 있는 노드' 중 가장 가까운 것을 고른다.
    // 그 방향에 없으면: 아직 아무것도 안 골랐을 때만 가장 가까운 노드를 고름 (첫 입력이 헛돌지 않게)
    void MoveKeyFocus(Vector2 dir)
    {
        var all = currentCanvasInstance.GetComponentsInChildren<MapButton>(false);
        Vector2 from = keyFocus != null ? (Vector2)keyFocus.transform.position
                     : currentButton != null ? (Vector2)currentButton.transform.position
                     : (Vector2)currentCanvasInstance.transform.position;

        MapButton best = null, nearest = null;
        float bestScore = float.MaxValue, nearestDist = float.MaxValue;
        foreach (var b in all)
        {
            if (b == null || !b.isSelectable || b == keyFocus) continue;
            Vector2 v = (Vector2)b.transform.position - from;
            float dist = v.magnitude;
            if (dist < nearestDist) { nearestDist = dist; nearest = b; }
            if (dir == Vector2.zero || dist < 0.01f) continue;

            float along = Vector2.Dot(v, dir);               // 누른 방향으로 얼마나 가는지
            if (along <= 0f) continue;
            float side = Mathf.Abs(v.x * dir.y - v.y * dir.x); // 옆으로 얼마나 벗어나는지 (많이 벗어날수록 불리)
            float score = along + side * 2f;
            if (score < bestScore) { bestScore = score; best = b; }
        }

        if (best == null && keyFocus == null) best = nearest;
        if (best != null) SetKeyFocus(best);
    }

    /// <summary>방향키 선택 표시를 이 노드로 옮긴다 (null = 선택 없음). 마우스를 올렸을 때도 불림</summary>
    public void SetKeyFocus(MapButton b)
    {
        if (keyFocus == b) return;
        if (keyFocus != null) keyFocus.keyFocused = false;
        keyFocus = b;
        if (keyFocus != null) keyFocus.keyFocused = true;
    }

    // ── [2026-10-21 추가] 키 안내 (캔버스 안에 만들어지고 캔버스와 함께 사라짐) ──
    void CreateMapLabels()
    {
        if (currentCanvasInstance == null) return;
        Transform root = currentCanvasInstance.transform;

        if (!showKeyGuide || !keyboardNavigation) return;

        RectTransform g = NewRect("KeyGuide", root, new Vector2(1f, 0f), new Vector2(1f, 0f));
        g.anchoredPosition = keyGuideOffset;
        g.sizeDelta = new Vector2(10f, keyBoxHeight);

        // 오른쪽부터 거꾸로 쌓는다:  [←][↑][↓][→] 선택    [Space][F][Enter] 결정
        float x = 0f;
        x = AddGuideGroup(g, x, new[] { "Space", "F", "Enter" }, "결정");
        x -= 28f;
        x = AddGuideGroup(g, x, new[] { "←", "↑", "↓", "→" }, "선택");
        g.sizeDelta = new Vector2(-x, keyBoxHeight);
        g.SetAsLastSibling();
    }

    // x = 지금까지 채운 오른쪽 끝 (0에서 왼쪽으로 음수로 늘어남). 그룹을 붙이고 새 왼쪽 끝을 돌려준다
    float AddGuideGroup(RectTransform parent, float x, string[] keys, string label)
    {
        RectTransform lr = NewRect("Label", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        TextMeshProUGUI lt = NewText(lr, label, keyGuideFontSize, keyGuideColor, TextAlignmentOptions.MidlineRight);
        lt.ForceMeshUpdate();
        float lw = lt.preferredWidth;
        lr.sizeDelta = new Vector2(lw, keyBoxHeight);
        lr.anchoredPosition = new Vector2(x, 0f);
        x -= lw + 8f;

        for (int i = keys.Length - 1; i >= 0; i--)
        {
            RectTransform k = NewRect("Key_" + keys[i], parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            TextMeshProUGUI kt = NewText(k, keys[i], keyGuideFontSize * 0.8f, keyGuideColor, TextAlignmentOptions.Center);
            kt.ForceMeshUpdate();
            float w = Mathf.Max(keyBoxHeight, kt.preferredWidth + 14f);
            k.sizeDelta = new Vector2(w, keyBoxHeight);
            k.anchoredPosition = new Vector2(x, 0f);
            if (keyBoxSprite != null)
            {
                // 글자 뒤에 키 상자 (글자는 자식이 아니라 같은 오브젝트라 상자를 따로 만듦)
                RectTransform box = NewRect("Box", k, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one;
                box.offsetMin = box.offsetMax = Vector2.zero;
                box.SetAsFirstSibling();
                Image img = box.gameObject.AddComponent<Image>();
                img.sprite = keyBoxSprite;
                img.type = Image.Type.Sliced;
                img.color = keyGuideColor;
                img.raycastTarget = false;
            }
            x -= w + 4f;
        }
        return x + 4f;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        return r;
    }

    // 글자는 이 오브젝트의 자식으로 (키 상자 그림 위에 그려지게)
    TextMeshProUGUI NewText(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform r = NewRect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) t.font = uiFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
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
        selecting = false;
        positionMarker = null;

        CurrentStageIndex = 0;
        currentButton = null;
        usedRoomPrefabs.Clear();
        startButtons.Clear();
    }
}
