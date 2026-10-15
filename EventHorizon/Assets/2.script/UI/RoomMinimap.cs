using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using TMPro;

/// <summary>
/// [2026-10-15 추가] 화면 우측 상단 미니맵 — 지금 있는 방 하나만 보여준다.
///
/// - 방이 바뀌면(새 Room이 생기면) 그 방의 충돌 타일맵을 읽어 "타일 1칸 = 1픽셀" 그림으로 방 모양을 만든다.
/// - 그 위에 점으로 표시: 플레이어(하늘색), 잡몹(빨강), 보스(크고 깜빡이는 빨강), 열린 포탈(보라).
/// - [2026-10-15] 방 전체를 한 화면에 다 넣지 않고, 플레이어 주변 viewHeight 칸만 확대해서 보여주며 플레이어를 따라간다.
/// - 전투 UI가 숨겨질 때(노드맵·대화·컷신 등 CombatUIGate가 막을 때)는 같이 숨는다.
/// - UI는 전부 코드로 만든다. 씬의 빈 오브젝트에 이 스크립트 하나만 붙이면 된다.
/// </summary>
public class RoomMinimap : MonoBehaviour
{
    [Header("위치·크기 (1920×1080 화면 기준 픽셀)")]
    public Vector2 panelSize = new Vector2(364f, 234f);
    public Vector2 margin = new Vector2(28f, 28f);
    [Tooltip("미니맵 안쪽 여백")]
    public float padding = 10f;

    [Header("확대 (미니맵 카메라)")]
    [Tooltip("미니맵에 보이는 세로 범위 (월드 칸 수). 작을수록 크게 확대된다. 방이 이보다 작으면 방 전체를 보여줌")]
    public float viewHeight = 16f;
    [Tooltip("플레이어를 따라가는 부드러움 (클수록 빠르게 따라감)")]
    public float followSpeed = 8f;
    [Tooltip("[2026-10-15] 미니맵 카메라 위치 조정 (월드 칸 단위). y를 음수로 하면 카메라가 내려가 플레이어 아래쪽이 더 보인다. 플레이 중에 바꿔도 바로 반영")]
    public Vector2 viewOffset = Vector2.zero;
    [Tooltip("[2026-10-15] 켜 두면 방 바깥 빈 공간이 보이지 않게 방 가장자리에서 카메라가 멈춘다. 끄면 viewOffset 만큼 방 밖까지도 움직인다")]
    public bool clampToRoom = true;

    [Header("색")]
    public Color backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.62f);
    public Color frameColor = new Color(0.86f, 0.72f, 0.42f, 0.85f);
    public Color wallColor = new Color(0.78f, 0.8f, 0.86f, 0.85f);
    public Color playerColor = new Color(0.45f, 0.9f, 1f, 1f);
    public Color enemyColor = new Color(1f, 0.32f, 0.28f, 1f);
    public Color bossColor = new Color(1f, 0.15f, 0.2f, 1f);
    public Color portalColor = new Color(0.75f, 0.45f, 1f, 1f);

    [Header("테두리 그림 (낡은 놋쇠 관측 장치) — [2026-10-15 추가]")]
    [Tooltip("테두리 그림. 비워 두면 Resources 폴더의 UIMinimapFrame 그림을 쓴다. 그림이 없으면 예전처럼 frameColor 단색 테두리")]
    public Sprite frameSprite;
    [Tooltip("테두리 그림이 미니맵 안쪽 바깥으로 나가는 크기 (x=왼쪽, y=오른쪽, z=위, w=아래 / 픽셀). 그림을 바꾸면 같이 맞춘다")]
    public Vector4 frameArtExtents = new Vector4(24f, 24f, 32f, 24f);
    [Tooltip("위쪽 이름판에 쓸 글자 (비우면 글자 없음)")]
    public string frameLabel = "AREA SCAN";
    [Tooltip("이름판 글꼴. 비워 두면 TextMeshPro 기본 글꼴 (한글은 예비 글꼴인 돋움으로 나옴)")]
    public TMP_FontAsset frameLabelFont;
    public Color frameLabelColor = new Color(0.82f, 0.75f, 0.58f, 1f);
    public float frameLabelSize = 17f;

    [Header("점 위치 조정 — [2026-10-15 추가]")]
    [Tooltip("문(포탈) 점 위치 조정 (월드 칸 단위). 점은 문 판정 범위의 가운데에 찍히고, 여기서 더 옮길 수 있다")]
    public Vector2 portalDotOffset = Vector2.zero;

    [Header("점 크기 (픽셀)")]
    public float playerDotSize = 14f;
    public float enemyDotSize = 11f;
    public float bossDotSize = 21f;
    public float portalDotSize = 16f;

    [Header("켜고 끄는 키")]
    public KeyCode toggleKey = KeyCode.M;

    [Header("목록 갱신 간격(초) — 적 목록을 다시 찾는 주기")]
    public float listRefreshInterval = 0.4f;

    // UI
    Canvas canvas;
    CanvasGroup group;
    RectTransform mapArea;   // 방 그림 + 점이 들어가는 영역
    RawImage layoutImage;
    RectTransform dotLayer;
    Sprite dotSprite;
    Texture2D layoutTexture;

    // 상태
    Room currentRoom;
    Rect worldRect;          // 방 그림이 차지하는 월드 범위
    bool userHidden;
    float refreshTimer;
    readonly List<Transform> enemies = new List<Transform>();
    readonly List<Transform> bosses = new List<Transform>();
    readonly List<Portal> portals = new List<Portal>();
    readonly List<Image> dotPool = new List<Image>();
    int dotsUsed;

    void Awake()
    {
        BuildUI();
    }

    void BuildUI()
    {
        var cgo = new GameObject("MinimapCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // 인벤토리(10)·메뉴(500)·게임오버(600)보다 아래
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = cgo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        // 바깥 테두리 (패널보다 2픽셀 큼)
        var frame = MakeImage(cgo.transform, "Frame", frameColor);
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = frt.pivot = Vector2.one;
        frt.sizeDelta = panelSize + Vector2.one * 4f;
        frt.anchoredPosition = -margin + Vector2.one * 2f;

        var panel = MakeImage(frame.transform, "Panel", backgroundColor);
        var prt = panel.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.one * 2f; prt.offsetMax = -Vector2.one * 2f;
        panel.gameObject.AddComponent<RectMask2D>();

        var area = new GameObject("MapArea", typeof(RectTransform));
        area.transform.SetParent(panel.transform, false);
        mapArea = (RectTransform)area.transform;
        // 기준점(pivot)을 방 그림의 왼쪽 아래로 — 위치를 옮겨서 플레이어 주변을 패널 가운데에 오게 한다
        mapArea.anchorMin = mapArea.anchorMax = new Vector2(0.5f, 0.5f);
        mapArea.pivot = Vector2.zero;

        var rawGo = new GameObject("Layout", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(mapArea, false);
        layoutImage = rawGo.GetComponent<RawImage>();
        layoutImage.raycastTarget = false;
        var lrt = layoutImage.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        var dots = new GameObject("Dots", typeof(RectTransform));
        dots.transform.SetParent(mapArea, false);
        dotLayer = (RectTransform)dots.transform;
        dotLayer.anchorMin = Vector2.zero; dotLayer.anchorMax = Vector2.one;
        dotLayer.offsetMin = Vector2.zero; dotLayer.offsetMax = Vector2.zero;

        BuildFrameArt(cgo.transform, frame);

        dotSprite = MakeCircleSprite(32);
        group.alpha = 0f;
    }

    // [2026-10-15 추가] 단색 테두리 대신 테두리 그림을 미니맵 위에 덮는다 (그림 가운데는 비어 있어 지도가 보임)
    void BuildFrameArt(Transform canvasRoot, Image plainFrame)
    {
        Sprite art = frameSprite != null ? frameSprite : Resources.Load<Sprite>("UIMinimapFrame");
        if (art == null) return;            // 그림이 없으면 예전 단색 테두리 그대로

        plainFrame.color = Color.clear;     // 단색 테두리는 숨김 (안쪽 패널·지도는 그대로)

        var img = MakeImage(canvasRoot, "FrameArt", Color.white);
        img.sprite = art;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;   // 화면 오른쪽 위 기준
        Vector4 e = frameArtExtents;
        rt.sizeDelta = panelSize + new Vector2(e.x + e.y, e.z + e.w);
        rt.anchoredPosition = -margin + new Vector2(e.y, e.z);  // 패널 오른쪽 위 모서리에서 그림 여백만큼 바깥

        if (string.IsNullOrEmpty(frameLabel)) return;
        var go = new GameObject("FrameLabel", typeof(RectTransform));
        go.transform.SetParent(rt, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        if (frameLabelFont != null) label.font = frameLabelFont;
        label.text = frameLabel;
        label.fontSize = frameLabelSize;
        label.color = frameLabelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        var lrt = label.rectTransform;
        // 이름판 가운데 = 패널 왼쪽 위에서 오른쪽 90, 위로 17 (그림 기준, 패널 크기에 맞춰 가로 비율 조정)
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(120f * panelSize.x / 364f, 22f);
        lrt.anchoredPosition = new Vector2(e.x + 90f * panelSize.x / 364f, -(e.z - 17f));
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    // 가장자리가 부드러운 원 모양 점 그림
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && Time.timeScale > 0f) userHidden = !userHidden;

        // 방이 바뀌었는지 확인 (RoomManager가 이전 방을 지우고 새 방을 만든다)
        if (currentRoom == null)
        {
            currentRoom = FindObjectOfType<Room>();
            if (currentRoom != null) RebuildLayout(currentRoom);
        }

        bool show = currentRoom != null && !userHidden && !CombatUIGate.IsBlocked;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (!show && group.alpha <= 0f) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = listRefreshInterval;
            RefreshLists();
        }
        DrawDots();

        // 플레이어를 따라 방 그림을 움직인다 (패널 밖으로 빈 공간이 보이지 않게 가장자리에서 멈춤)
        mapArea.anchoredPosition = Vector2.Lerp(mapArea.anchoredPosition, TargetOffset(false),
                                                1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
    }

    // 플레이어가 패널 가운데에 오도록 하는 방 그림의 위치 (패널 가운데 기준)
    Vector2 TargetOffset(bool snap)
    {
        Vector2 size = mapArea.sizeDelta;
        Vector2 half = panelSize * 0.5f; // 패널(테두리 안쪽) 크기의 절반
        Transform player = CurrentPlayer();
        Vector2 uv = new Vector2(0.5f, 0.5f);
        if (player != null && worldRect.width > 0f && worldRect.height > 0f)
        {
            // [2026-10-15] 카메라가 비추는 곳 = 플레이어 위치 + viewOffset
            Vector2 focus = (Vector2)player.position + viewOffset;
            uv = new Vector2((focus.x - worldRect.x) / worldRect.width, (focus.y - worldRect.y) / worldRect.height);
            if (clampToRoom) { uv.x = Mathf.Clamp01(uv.x); uv.y = Mathf.Clamp01(uv.y); }
        }
        Vector2 pos = -Vector2.Scale(uv, size);
        if (!clampToRoom) return pos;   // 방 밖 빈 공간이 보여도 그대로 따라감
        // 그림이 패널보다 크면 가장자리에서 멈추고, 작으면 가운데 정렬
        pos.x = size.x > half.x * 2f ? Mathf.Clamp(pos.x, half.x - size.x, -half.x) : -size.x * 0.5f;
        pos.y = size.y > half.y * 2f ? Mathf.Clamp(pos.y, half.y - size.y, -half.y) : -size.y * 0.5f;
        return pos;
    }

    static Transform CurrentPlayer()
    {
        var cm = CharacterManager.Instance;
        if (cm != null && cm.CurrentCharacter != null) return cm.CurrentCharacter.transform;
        return PlayerController.Instance != null ? PlayerController.Instance.transform : null;
    }

    // ── 방 모양 그림 만들기 ───────────────────────────────
    void RebuildLayout(Room room)
    {
        var cells = new HashSet<Vector2Int>();
        var maps = room.GetComponentsInChildren<Tilemap>();
        // 충돌 타일맵(벽·바닥)이 있으면 그것만, 없으면 전부
        var solid = new List<Tilemap>();
        foreach (var tm in maps) if (tm.GetComponent<TilemapCollider2D>() != null) solid.Add(tm);
        if (solid.Count == 0) solid.AddRange(maps);

        foreach (var tm in solid)
        {
            var b = tm.cellBounds;
            foreach (var p in b.allPositionsWithin)
            {
                if (!tm.HasTile(p)) continue;
                Vector3 w = tm.GetCellCenterWorld(p);
                cells.Add(new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y)));
            }
        }

        if (cells.Count == 0)
        {
            // 타일맵이 없는 방 — 방 크기 설정으로 빈 사각형
            Vector3 c = room.transform.position;
            worldRect = new Rect(c.x - room.checkSize.x * 0.5f, c.y - room.checkSize.y * 0.5f, room.checkSize.x, room.checkSize.y);
            SetTexture(new Texture2D(1, 1));
            layoutTexture.SetPixel(0, 0, Color.clear); layoutTexture.Apply();
        }
        else
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x; if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x; if (c.y > maxY) maxY = c.y;
            }
            int w = maxX - minX + 1, h = maxY - minY + 1;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            Color32 wall = wallColor;
            foreach (var c in cells) pixels[(c.y - minY) * w + (c.x - minX)] = wall;
            tex.SetPixels32(pixels);
            tex.Apply();
            SetTexture(tex);
            worldRect = new Rect(minX, minY, w, h);
        }

        // 확대 배율: 세로 viewHeight 칸이 패널에 들어가게. 방이 더 작으면 방 전체가 꽉 차는 배율을 쓴다
        Vector2 avail = panelSize - Vector2.one * (padding * 2f);
        float fitScale = Mathf.Min(avail.x / worldRect.width, avail.y / worldRect.height);
        float zoomScale = avail.y / Mathf.Max(1f, viewHeight);
        float scale = Mathf.Max(fitScale, zoomScale);
        mapArea.sizeDelta = new Vector2(worldRect.width, worldRect.height) * scale;
        mapArea.anchoredPosition = TargetOffset(true);

        refreshTimer = 0f;
    }

    void SetTexture(Texture2D tex)
    {
        if (layoutTexture != null) Destroy(layoutTexture);
        layoutTexture = tex;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        layoutImage.texture = tex;
    }

    // ── 표시할 대상 목록 ─────────────────────────────────
    void RefreshLists()
    {
        enemies.Clear(); bosses.Clear(); portals.Clear();
        foreach (var e in FindObjectsOfType<Enemy>())
            if (e.gameObject.activeInHierarchy) enemies.Add(e.transform);
        foreach (var b in FindObjectsOfType<BossController>())
            if (b.gameObject.activeInHierarchy) bosses.Add(b.transform);
        if (currentRoom != null)
            portals.AddRange(currentRoom.GetComponentsInChildren<Portal>());
    }

    void DrawDots()
    {
        dotsUsed = 0;
        foreach (var p in portals)
            if (p != null && p.portalCollider != null && p.portalCollider.enabled)
                PlaceDot((Vector3)((Vector2)p.portalCollider.bounds.center + portalDotOffset), portalColor, portalDotSize);   // [2026-10-15] 문 판정 범위 가운데 (예전: 포탈 기준점 = 바닥)
        foreach (var e in enemies)
            if (e != null) PlaceDot(e.position, enemyColor, enemyDotSize);
        float blink = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
        foreach (var b in bosses)
            if (b != null) PlaceDot(b.position, new Color(bossColor.r, bossColor.g, bossColor.b, bossColor.a * blink), bossDotSize);

        Transform player = CurrentPlayer();
        if (player != null) PlaceDot(player.position, playerColor, playerDotSize); // 맨 위에 그리도록 마지막

        for (int i = dotsUsed; i < dotPool.Count; i++)
            if (dotPool[i].enabled) dotPool[i].enabled = false;
    }

    void PlaceDot(Vector3 world, Color color, float size)
    {
        Image dot;
        if (dotsUsed < dotPool.Count) dot = dotPool[dotsUsed];
        else
        {
            dot = MakeImage(dotLayer, "Dot", color);
            dot.sprite = dotSprite;
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            dotPool.Add(dot);
        }
        dot.transform.SetSiblingIndex(dotsUsed);
        dotsUsed++;

        float u = Mathf.Clamp01((world.x - worldRect.x) / worldRect.width);
        float v = Mathf.Clamp01((world.y - worldRect.y) / worldRect.height);
        var r2 = dot.rectTransform;
        r2.anchoredPosition = new Vector2(u * mapArea.sizeDelta.x, v * mapArea.sizeDelta.y);
        r2.sizeDelta = new Vector2(size, size);
        dot.color = color;
        dot.enabled = true;
    }

    void OnDestroy()
    {
        if (layoutTexture != null) Destroy(layoutTexture);
        if (dotSprite != null) { Destroy(dotSprite.texture); Destroy(dotSprite); }
    }
}
