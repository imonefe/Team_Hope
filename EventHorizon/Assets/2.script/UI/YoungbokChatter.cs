using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-10-27 추가] 영복 잡담 통신창 — 화면 왼쪽 가운데에 영복 얼굴과 짧은 대사를 잠깐 띄운다 (디자인: 시안 E "정비 통신창").
///
/// ■ 씬에 따로 놓을 필요 없음
///   게임이 시작되면 스스로 하나 만들어지고(씬이 바뀌어도 유지), Etc/Resources/YoungbokChatter 프리팹에서
///   영복 일러스트(portrait)만 가져온다. UI(테두리·주사선·글자)는 전부 코드로 만든다.
///
/// ■ 언제 말하나 (대사는 아래 "대사 목록"에서 고친다)
///   - 무기를 망치/건틀릿/검으로 바꿔 들 때마다 (우선 대사)
///   - 첫 전투 방, 퍼즈 게이지가 처음 가득 찼을 때, 상자를 처음 열었을 때 (한 판에 한 번)
///   - 남은 적 3/2/1 (방마다, 다른 대사를 덮어씀), 체력 30% 아래 (방마다), 방 정리 후 문 열림 (방마다)
///   - 노드 지도를 처음 열었을 때, 보스방 들어가기 직전
///   - 레전더리 상자가 생겼을 때 (매번), 상자 열 때 5%, 치명타 5%, 피해를 입었을 때 2%
///
/// ■ 다른 스크립트에서 부르는 곳
///   Box.cs(상자 열기·레전더리 등장), Hitbox.cs(플레이어 치명타), Room.cs(방 정리) — 각각 한 줄씩.
///   나머지(무기 교체·남은 적·체력·게이지·노드 지도·방 입장)는 여기서 직접 살핀다.
///
/// ■ 규칙
///   - 한 번에 하나만. 일반 대사는 앞 대사가 사라진 뒤 minGap(8초)이 지나야 나온다 (안 되면 그냥 건너뜀).
///   - 우선 대사(무기 교체, 남은 적, 레전더리 상자, 보스방 앞)는 떠 있는 대사를 바로 덮어쓴다.
///   - 대화·컷신 중, ESC 메뉴 등으로 시간이 멈춘 동안에는 나오지 않는다 (노드 지도 안내만 예외).
/// </summary>
public class YoungbokChatter : MonoBehaviour
{
    [Header("영복 일러스트 (Etc/Resources/YoungbokChatter 프리팹에서 지정)")]
    public Sprite portrait;

    // ── 대사 목록 ───────────────────────────────────────────
    const string LineHammer = "망치는 공격(X)을 꾹 누르면 모아 치기가 돼.";
    const string LineGauntlet = "건틀릿은 쉬지 않고 칠수록 빨라져. 손 멈추지 마.";
    const string LineSword = "검은 세 번 이어서 베는 게 제일 깔끔해.";
    const string LineFirstBattle = "맞기 직전에 대시하면 피할 수 있어.";
    const string LineGaugeFull = "게이지 찼다! Q를 누르면 세상이 잠깐 멈춰. 그동안은 너만 움직일 수 있어.";
    const string LineFirstBox = "그거 내가 넣어 둔 거야. 잘 써.";
    const string LineLeft3 = "셋 남았어. 거의 다 왔어.";
    const string LineLeft2 = "둘 남았다. 집중해!";
    const string LineLeft1 = "하나 남았어. 마무리해!";
    const string LineLowHp = "많이 망가졌어! 문 근처에 회복 장치 있을 거야.";
    const string LineRoomClear = "문 열렸다. 다음 서가로 가자.";
    const string LineMap = "길은 네가 골라. 어디로 가든 주인의 서가로 이어져 있어.";
    const string LineBossRoom = "이 앞은 주인의 서가야. …꼭 돌아와, 헤네시.";
    static readonly string[] LinesLegendary = { "저 상자 빛 보여? 진짜 좋은 거 들었다!", "와, 전설급이다. 내가 저런 걸 언제 넣어 뒀지?" };
    static readonly string[] LinesBox = { "그거 고르느라 사흘 밤 샜어.", "쓸 만한 거 나왔어? 아니면 다음 상자!", "포장은 대충 했어도 속은 알차." };
    static readonly string[] LinesCrit = { "방금 그거, 제대로 들어갔다!", "약점을 정확히 찔렀네.", "그 한 방, 서고 기록에 남겠는데?" };
    static readonly string[] LinesHurt = { "아야! 그거 내가 고친 부품이라고!", "조심해, 부품 구하기 힘들어.", "맞으면서 배우는 거야… 근데 너무 많이 배우진 마." };

    // ── 확률·시간 ───────────────────────────────────────────
    const float BoxChance = 0.05f, CritChance = 0.05f, HurtChance = 0.02f;
    const float LowHpRatio = 0.3f;
    const float MinGap = 8f;                 // 일반 대사 사이 최소 간격(초, 실제 시간)
    const float PollInterval = 0.25f;        // 남은 적·체력 등을 살피는 간격

    // ── 모양 (디자인 기준 해상도 1280×720) ─────────────────
    static readonly Color Gold = new Color(0.851f, 0.682f, 0.353f, 1f);           // #D9AE5A
    static readonly Color Ground = new Color(22f / 255f, 20f / 255f, 18f / 255f, 0.94f);
    static readonly Color PortraitBg = new Color(43f / 255f, 38f / 255f, 32f / 255f, 1f);
    static readonly Color TextColor = new Color(0.937f, 0.902f, 0.812f, 1f);      // #EFE6CF
    const float BoxW = 428f, BoxH = 122f, LeftX = 40f;
    const float FaceSize = 82f;
    const float BodySize = 16f, HeaderSize = 11f;
    const float OffsetY = 200f;              // [2026-10-28] 화면 가운데보다 200 위에 표시
    const float UiScale = 0.8f;              // [2026-10-27] 시안보다 20% 작게 (통신창 전체를 같은 비율로 축소)

    static YoungbokChatter instance;

    // UI
    RectTransform root;
    CanvasGroup group;
    TextMeshProUGUI header, body;
    RectTransform headerRt, bodyRt, barsRt, columnRt;
    Image portraitImg;

    // 상태
    Coroutine playing;
    bool showing;
    bool showingAllowedOverMap;
    float lastEndTime = -999f;
    readonly HashSet<string> onceRun = new HashSet<string>();
    readonly HashSet<string> onceRoom = new HashSet<string>();

    Room currentRoom;
    int lastEnemyCount;
    bool sawEnemiesInRoom;
    int lastHp = -1;
    bool wasMapOpen;
    PlayerController hookedPlayer;
    float pollTimer;

    // ════════════════ 만들기 ════════════════

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();

    static YoungbokChatter Ensure()
    {
        if (instance != null) return instance;
        var template = Resources.Load<YoungbokChatter>("YoungbokChatter");
        instance = template != null ? Instantiate(template) : new GameObject().AddComponent<YoungbokChatter>();
        instance.name = "YoungbokChatter";
        DontDestroyOnLoad(instance.gameObject);
        instance.BuildUI();
        return instance;
    }

    void OnEnable()
    {
        RoomManager.OnRoomLoaded += HandleRoomLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        RoomManager.OnRoomLoaded -= HandleRoomLoaded;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (hookedPlayer != null) hookedPlayer.OnWeaponChanged -= HandleWeaponChanged;
    }

    // 새 판(씬 다시 불러오기) → "한 판에 한 번" 기록 초기화
    void HandleSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        onceRun.Clear();
        onceRoom.Clear();
        currentRoom = null;
        lastHp = -1;
        lastEnemyCount = 0;
        sawEnemiesInRoom = false;
        HideNow();
    }

    // ════════════════ 다른 스크립트가 부르는 곳 ════════════════

    /// <summary>상자를 열었을 때 (Box.cs). 처음 한 번은 확정, 이후 5%.</summary>
    public static void OnBoxOpened()
    {
        var c = Ensure();
        if (!c.onceRun.Contains("box"))
        {
            if (c.Say(LineFirstBox, false)) c.onceRun.Add("box");
        }
        else if (Random.value < BoxChance) c.Say(Pick(LinesBox), false);
    }

    /// <summary>레전더리 상자가 나타났을 때 (Box.cs). 매번, 떠 있는 대사를 덮어씀.</summary>
    public static void OnLegendaryBoxAppeared() => Ensure().Say(Pick(LinesLegendary), true);

    /// <summary>플레이어 공격이 치명타로 들어갔을 때 (Hitbox.cs). 5%.</summary>
    public static void OnPlayerCrit()
    {
        if (Random.value < CritChance) Ensure().Say(Pick(LinesCrit), false);
    }

    /// <summary>방 정리로 문이 열렸을 때 (Room.cs). 적이 있었던 방에서만.</summary>
    public static void OnRoomCleared(Room room)
    {
        var c = Ensure();
        if (room != c.currentRoom || !c.sawEnemiesInRoom) return;
        if (c.onceRoom.Add("clear")) c.Say(LineRoomClear, false);
    }

    // ════════════════ 직접 살피는 것들 ════════════════

    void HandleRoomLoaded(Room room, bool isFirstRoom)
    {
        currentRoom = room;
        onceRoom.Clear();
        lastEnemyCount = 0;
        sawEnemiesInRoom = false;
        HideNow();

        if (room == null) return;
        if (room.isBossRoom)
            StartCoroutine(SayLater(1.0f, LineBossRoom, true, "bossroom"));
        else if (room.GetComponentInChildren<MonsterSpawner>() != null && !onceRun.Contains("firstbattle"))
            StartCoroutine(SayLater(1.2f, LineFirstBattle, false, "firstbattle"));
    }

    IEnumerator SayLater(float delay, string line, bool priority, string onceKey)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (onceKey == "firstbattle" && onceRun.Contains(onceKey)) yield break;
        if (Say(line, priority) && onceKey == "firstbattle") onceRun.Add(onceKey);
    }

    // [2026-10-27 변경] 무기를 바꿔 들 때마다 안내 (한 판에 한 번 → 매번). 떠 있는 대사를 덮어쓰는 우선 대사
    void HandleWeaponChanged(Weapontype w)
    {
        string line = w == Weapontype.Hammer ? LineHammer
                    : w == Weapontype.Glove ? LineGauntlet
                    : w == Weapontype.Sword ? LineSword : null;
        if (line != null) Say(line, true);
    }

    void Update()
    {
        // 무기 교체 이벤트 연결 (플레이어는 씬이 다시 열릴 때마다 새로 생긴다)
        var pc = PlayerController.Instance;
        if (pc != hookedPlayer)
        {
            if (hookedPlayer != null) hookedPlayer.OnWeaponChanged -= HandleWeaponChanged;
            hookedPlayer = pc;
            if (hookedPlayer != null) hookedPlayer.OnWeaponChanged += HandleWeaponChanged;
            lastHp = -1;
        }

        // 떠 있는 동안 막히면(대화·컷신 시작, 메뉴) 바로 내린다
        if (showing && IsBlocked(showingAllowedOverMap)) HideNow();

        pollTimer -= Time.unscaledDeltaTime;
        if (pollTimer > 0f) return;
        pollTimer = PollInterval;

        PollMap();
        if (pc == null) return;
        PollEnemies();
        PollHp(pc);
        PollGauge();
    }

    void PollMap()
    {
        bool open = MapManager.Instance != null && MapManager.Instance.IsMapOpen;
        if (open && !wasMapOpen && !onceRun.Contains("map"))
        {
            if (Say(LineMap, false, allowOverMap: true)) onceRun.Add("map");
        }
        wasMapOpen = open;
    }

    void PollEnemies()
    {
        if (currentRoom == null) return;
        int count = 0;
        foreach (var e in FindObjectsOfType<Enemy>())
            if (e != null && e.gameObject.activeInHierarchy) count++;
        foreach (var b in FindObjectsOfType<BossController>())
            if (b != null && b.gameObject.activeInHierarchy) count++;

        if (count > 0) sawEnemiesInRoom = true;

        // 아직 나올 적이 남아 있으면(밟지 않은 스포너 등) "N 남았다"고 하지 않는다
        if (count < lastEnemyCount && count >= 1 && count <= 3 && !currentRoom.HasPendingSpawns())
        {
            string key = "left" + count;
            if (onceRoom.Add(key))
                Say(count == 3 ? LineLeft3 : count == 2 ? LineLeft2 : LineLeft1, true);
        }
        lastEnemyCount = count;
    }

    void PollHp(PlayerController pc)
    {
        Stats st = pc.GetComponent<Stats>();
        if (st == null || st.maxHp <= 0) return;
        int hp = st.currentHp;

        if (lastHp >= 0 && hp < lastHp && hp > 0)
        {
            if ((float)hp / st.maxHp < LowHpRatio && onceRoom.Add("lowhp"))
                Say(LineLowHp, false);
            else if (Random.value < HurtChance)
                Say(Pick(LinesHurt), false);
        }
        lastHp = hp;
    }

    void PollGauge()
    {
        if (onceRun.Contains("gauge") || PauseManager.Instance == null) return;
        if (PauseManager.Instance.GaugeValue >= 100f && Say(LineGaugeFull, false))
            onceRun.Add("gauge");
    }

    // ════════════════ 말하기 ════════════════

    static bool IsBlocked(bool allowOverMap)
    {
        if (CutsceneSequencer.IsAnyPlaying) return true;
        if (NPC.AnyTalking) return true;
        bool mapOpen = MapManager.Instance != null && MapManager.Instance.IsMapOpen;
        if (mapOpen) return !allowOverMap;
        if (Time.timeScale == 0f) return true;   // ESC 메뉴·인벤토리·게임오버 등
        return false;
    }

    /// <summary>대사를 띄운다. 실제로 띄웠으면 true.</summary>
    bool Say(string line, bool priority, bool allowOverMap = false)
    {
        if (string.IsNullOrEmpty(line) || root == null) return false;
        if (IsBlocked(allowOverMap)) return false;
        if (!priority && (showing || Time.unscaledTime - lastEndTime < MinGap)) return false;

        SetText(line);
        showingAllowedOverMap = allowOverMap;
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Run(line.Length));
        return true;
    }

    IEnumerator Run(int chars)
    {
        showing = true;
        root.gameObject.SetActive(true);
        float hold = Mathf.Clamp(2.4f + chars * 0.07f, 3f, 6f);

        // 왼쪽에서 살짝 밀려 들어옴
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.22f);
            float e = 1f - (1f - k) * (1f - k);
            group.alpha = e;
            root.anchoredPosition = new Vector2(LeftX - 30f * (1f - e), OffsetY);
            yield return null;
        }
        group.alpha = 1f;
        root.anchoredPosition = new Vector2(LeftX, OffsetY);

        t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        HideNow();
    }

    void HideNow()
    {
        if (playing != null) { StopCoroutine(playing); playing = null; }
        if (showing) lastEndTime = Time.unscaledTime;
        showing = false;
        if (group != null) group.alpha = 0f;
        if (root != null) root.gameObject.SetActive(false);
    }

    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

    // ════════════════ UI 만들기 (시안 E) ════════════════

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // 노드 지도(100) 위, ESC 메뉴(500)·화면 전환(2000) 아래
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite cut = MakeCutCornerSprite();

        // 바깥 금색 테두리 (모서리 두 곳이 잘린 판)
        root = NewRect("Box", transform);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(BoxW, BoxH);
        root.anchoredPosition = new Vector2(LeftX, OffsetY);
        root.localScale = Vector3.one * UiScale;   // 왼쪽 가운데 기준으로 줄어듦
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var frame = root.gameObject.AddComponent<Image>();
        frame.sprite = cut; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 0.5f;
        frame.color = Gold; frame.raycastTarget = false;

        // 안쪽 어두운 판 + 주사선
        var inner = NewRect("Inner", root);
        Stretch(inner, 1f);
        var innerImg = inner.gameObject.AddComponent<Image>();
        innerImg.sprite = cut; innerImg.type = Image.Type.Sliced; innerImg.pixelsPerUnitMultiplier = 0.5f;
        innerImg.color = Ground; innerImg.raycastTarget = false;
        inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var scan = NewRect("Scan", inner);
        Stretch(scan, 0f);
        var scanImg = scan.gameObject.AddComponent<Image>();
        scanImg.sprite = MakeScanSprite(); scanImg.type = Image.Type.Tiled;
        scanImg.color = new Color(Gold.r, Gold.g, Gold.b, 0.06f); scanImg.raycastTarget = false;

        // 얼굴 칸 (금색 1px 테두리 + 가운데 맞춘 일러스트)
        const float innerW = BoxW - 2f;
        var faceFrame = NewRect("Face", inner);
        faceFrame.sizeDelta = new Vector2(FaceSize, FaceSize);
        faceFrame.anchoredPosition = new Vector2(-innerW / 2f + 12f + FaceSize / 2f, -2f);
        var ff = faceFrame.gameObject.AddComponent<Image>();
        ff.color = Gold; ff.raycastTarget = false;

        var faceMask = NewRect("Mask", faceFrame);
        Stretch(faceMask, 1f);
        var fm = faceMask.gameObject.AddComponent<Image>();
        fm.color = PortraitBg; fm.raycastTarget = false;
        faceMask.gameObject.AddComponent<RectMask2D>();

        var pic = NewRect("Portrait", faceMask);
        portraitImg = pic.gameObject.AddComponent<Image>();
        portraitImg.raycastTarget = false;
        portraitImg.color = new Color(1f, 1f, 1f, 0.92f);
        if (portrait != null)
        {
            portraitImg.sprite = portrait;
            // 시안 E와 같은 자리: 원본 일러스트에서 얼굴 중심(가로 45%, 위에서 27.6%)이 칸 가운데에 오고,
            // 얼굴 영역 높이(원본의 33.8%)가 118px 이 되도록 키운다
            float aspect = portrait.rect.width / portrait.rect.height;
            float h = 118f / 0.3382f;
            float w = h * aspect;
            pic.sizeDelta = new Vector2(w, h);
            pic.anchoredPosition = new Vector2((0.5f - 0.4515f) * w, -(0.5f - 0.2757f) * h);
        }
        else portraitImg.enabled = false;

        // 오른쪽 글자 칸
        const float colX = 12f + FaceSize + 12f;
        const float colW = innerW - colX - 16f;
        columnRt = NewRect("Column", inner);
        columnRt.anchorMin = columnRt.anchorMax = new Vector2(0f, 0.5f);
        columnRt.pivot = new Vector2(0f, 0.5f);
        columnRt.sizeDelta = new Vector2(colW, 80f);
        columnRt.anchoredPosition = new Vector2(colX, -2f);

        TMP_FontAsset font = null;
        var prompt = Resources.Load<InteractPrompt>("InteractPrompt");
        if (prompt != null && prompt.labelText != null) font = prompt.labelText.font;

        header = NewText("Header", columnRt, font, HeaderSize, Gold, "통신 · 영복");
        header.characterSpacing = 6f;
        headerRt = header.rectTransform;
        headerRt.anchorMin = headerRt.anchorMax = new Vector2(0f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);

        // 신호 막대 4개
        barsRt = NewRect("Bars", columnRt);
        barsRt.anchorMin = barsRt.anchorMax = new Vector2(0f, 1f);
        barsRt.pivot = new Vector2(0f, 1f);
        barsRt.sizeDelta = new Vector2(14f, 10f);
        float[] hs = { 3f, 5f, 8f, 10f };
        for (int i = 0; i < 4; i++)
        {
            var b = NewRect("Bar" + i, barsRt);
            b.anchorMin = b.anchorMax = new Vector2(0f, 0f);
            b.pivot = new Vector2(0f, 0f);
            b.sizeDelta = new Vector2(2f, hs[i]);
            b.anchoredPosition = new Vector2(i * 4f, 0f);
            var bi = b.gameObject.AddComponent<Image>();
            bi.color = i == 3 ? new Color(Gold.r, Gold.g, Gold.b, 0.35f) : Gold;
            bi.raycastTarget = false;
        }

        body = NewText("Body", columnRt, font, BodySize, TextColor, "");
        body.enableWordWrapping = true;
        body.lineSpacing = 4f;
        bodyRt = body.rectTransform;
        bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0f, 1f);
        bodyRt.pivot = new Vector2(0f, 1f);
        bodyRt.sizeDelta = new Vector2(colW, 60f);

        group.alpha = 0f;
        root.gameObject.SetActive(false);
    }

    // 머리글 + 본문 묶음이 글자 칸 가운데(세로)에 오도록 배치
    void SetText(string line)
    {
        float colW = columnRt.sizeDelta.x;
        header.ForceMeshUpdate();
        float headerW = header.preferredWidth;
        float headerH = Mathf.Max(header.preferredHeight, 12f);

        body.text = line;
        Vector2 pref = body.GetPreferredValues(line, colW, 0f);
        float bodyH = pref.y;

        const float gap = 6f;
        float total = headerH + gap + bodyH;
        columnRt.sizeDelta = new Vector2(colW, total);

        headerRt.sizeDelta = new Vector2(headerW + 2f, headerH);
        headerRt.anchoredPosition = Vector2.zero;
        barsRt.anchoredPosition = new Vector2(headerW + 10f, -(headerH - 10f) / 2f);
        bodyRt.sizeDelta = new Vector2(colW, bodyH);
        bodyRt.anchoredPosition = new Vector2(0f, -(headerH + gap));
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, string text)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
        t.text = text;
        t.enableWordWrapping = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        return t;
    }

    // 왼쪽 위·오른쪽 아래 모서리가 비스듬히 잘린 판 (9분할, 모서리 7px → 표시 14px)
    static Sprite MakeCutCornerSprite()
    {
        const int N = 32, C = 7;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                // 왼쪽 위 (x 작고 y 큼) / 오른쪽 아래 (x 크고 y 작음)
                float dTL = (fx + (N - fy)) - C;          // 0 이하면 잘림
                float dBR = ((N - fx) + fy) - C;
                float a = Mathf.Clamp01(Mathf.Min(dTL, dBR) / 1.414f + 0.5f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    // 4px 간격 가로줄 (위아래로 반복)
    static Sprite MakeScanSprite()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
        tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0) });
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, 1, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
