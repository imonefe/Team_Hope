using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 튜토리얼 방 (기획서: 한 방 · 화면 글자 · 해 봐야 다음 구간이 열림 · 건너뛰기 없음).
///
/// 구간마다 통과 조건이 있고, 조건을 채우면 그 구간 끝의 막이(gate)가 꺼지고 다음 구간 안내가 뜬다.
/// 안내 글자의 {Jump} {Dash} 같은 표시는 설정된 키 이름으로 바뀐다 (키 설정을 바꿔도 맞게 나옴).
/// 오브젝트(막이·영역·허수아비·상자·글자)는 에디터 메뉴 "EventHorizon/튜토리얼 방 만들기"가 만들어 둔다.
///
/// [2026-11-18 수정]
/// - 안내는 영복 통신창(YoungbokChatter)으로 말하듯이 띄운다. 아직 정체를 모르는 때라 이름은 "???", 얼굴은 검은 실루엣.
///   (예전 화면 위쪽 글자 판 titleText·guideText 는 연결돼 있으면 같이 채우지만, 프리팹에서는 꺼 둠)
/// - 구간을 통과하면 막이가 노이즈처럼 지직거리며 사라진다. "통과!" 글자는 뺐다.
/// - 마지막 구간을 끝내면 문으로 나가는 대신, 마무리 대사 뒤 화면 전환과 함께 로비 방으로 이동한다.
/// </summary>
public class TutorialController : MonoBehaviour
{
    public enum Condition
    {
        Jump,          // 점프 한 번
        DoubleJump,    // 공중 점프 (점프 2회)
        Dash,          // 대시
        DoubleDash,    // 대시 2회 연속 (쿨타임 안에)
        Reach,         // reachZone 에 닿기
        DummyDamage,   // 허수아비에 피해 일정량
        WeaponSwap,    // 무기 교체 키
        WeaponSkill,   // 0번(무기 스킬) 칸 스킬 사용
        AnySkill,      // 아무 스킬 사용
        Pause,         // 퍼즈 사용
        BoxAndInventory, // 상자 열기 + 인벤토리 열기
        DummyHits,     // [2026-11-18] 허수아비를 정해진 횟수만큼 때리기
        SkillAndPause, // [2026-11-18] 스킬 한 번 + 퍼즈 한 번 (순서 상관없음)
        AttackSkillPause,     // [2026-11-21] 허수아비 하나로: 정해진 횟수 때리기 → 스킬 → 퍼즈 (guide → guide2 → guide3)
        AugmentAndInventory,  // [2026-11-21] 상자 대신: 증강 3개 중 하나 고르기 → 인벤토리 열기 (여기서 얻은 증강은 로비로 갈 때 빠짐)
        CombatBasics,         // [2026-11-21] 마지막 구간: 공격 → 스킬 → 무기 교체 → 퍼즈 (퍼즈 중에 설명) → 증강 고르기 → 인벤토리
    }

    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 3)] public string guide;
        [Tooltip("[2026-11-18] 스킬·퍼즈처럼 두 동작 구간에서, 앞 동작(스킬)을 마치면 바뀌는 두 번째 안내")]
        [TextArea(2, 3)] public string guide2;
        [Tooltip("[2026-11-21] 공격·스킬·퍼즈 구간의 세 번째 안내 (퍼즈)")]
        [TextArea(2, 3)] public string guide3;
        [Tooltip("[2026-11-21] CombatBasics: 무기 교체 안내")]
        [TextArea(2, 3)] public string guideSwap;
        [Tooltip("[2026-11-21] CombatBasics: 퍼즈가 켜져 있는 동안 보여 줄 설명 (퍼즈가 풀리면 다음 안내로)")]
        [TextArea(2, 3)] public string guidePaused;
        [Tooltip("[2026-11-21] CombatBasics: 퍼즈가 끝난 뒤 증강 고르기 안내 (고르기 창이 같이 열림, 여기서 얻은 증강은 로비로 갈 때 빠짐)")]
        [TextArea(2, 3)] public string guideAugment;
        [Tooltip("[2026-11-21] CombatBasics: 증강을 고른 뒤 인벤토리 안내")]
        [TextArea(2, 3)] public string guideInventory;
        public Condition condition;
        [Tooltip("이 구간을 통과하면 꺼지는 막이")]
        public GameObject gate;
        [Tooltip("구덩이에 빠졌을 때 되돌아오는 위치")]
        public Transform checkpoint;
        [Tooltip("Reach 조건용 영역")]
        public TutorialZone reachZone;
        [Tooltip("DummyDamage 조건용 허수아비")]
        public Stats dummy;
        public int damageNeeded = 50;
        [Tooltip("DummyHits 조건용 — 허수아비를 때려야 하는 횟수")]
        public int hitsNeeded = 3;
        [Tooltip("BoxAndInventory 조건용 상자 (구간이 시작될 때 열 수 있게 됨)")]
        public Box box;
        [Tooltip("구간이 시작될 때 퍼즈 게이지를 가득 채움 (게이지 모으기 생략)")]
        public bool fillPauseGauge;
        [Tooltip("[2026-11-18] 구간이 시작될 때 플레이어에게 바로 넣어 줄 스킬 (줍기 생략)")]
        public Skill grantSkill;
        [Tooltip("[2026-11-18] 이 구간을 통과하면 같이 사라지는 막이 (구간을 합쳤을 때)")]
        public List<GameObject> moreGates = new List<GameObject>();
        [Tooltip("[2026-11-18] 플레이어가 이 영역(TutorialZone · Reach)에 들어와야 안내 통신창이 뜨고 구간이 시작됨. 비우면 앞 구간이 끝나자마자 시작")]
        public TutorialZone startZone;
        [Tooltip("[2026-11-19] 플레이어가 이 영역(TutorialZone · Reach)까지 오면 조건을 못 채웠어도 통과로 친다 (예: 한 번 점프로 벽을 넘은 경우). 비우면 조건으로만 통과")]
        public TutorialZone passZone;
        [Tooltip("[2026-11-20] 구간이 시작될 때 조작 안내보다 먼저 한 줄씩 보여 줄 대사 (스토리). 다 보여 준 뒤 조작 안내로 바뀜")]
        [TextArea(2, 3)] public string[] preLines;
        [Tooltip("[2026-11-20] 통신창 본문 아래에 항상 붙는 짧은 키 안내 (스토리 대사가 나오는 동안에도). {Jump} 처럼 쓰면 설정 키로 바뀜. 비우면 없음")]
        public string keyHint;
    }

    public List<Step> steps = new List<Step>();

    [Header("화면 글자")]
    public TMP_Text titleText;
    public TMP_Text guideText;
    public string doneTitle = "튜토리얼 완료";
    [TextArea] public string doneGuide = "오른쪽 끝의 문으로 나가세요";
    [Tooltip("[2026-11-20] 튜토리얼을 끝내면 차례로 보여 줄 마무리 대사. 비우면 doneGuide 한 줄. 마지막 줄이 뜬 뒤 lobbyDelay 초 뒤 로비로")]
    [TextArea(2, 3)] public string[] doneLines;
    [Tooltip("[2026-11-20] 앞 대사·마무리 대사 한 줄을 보여 주는 시간 (초, 메뉴로 멈춘 시간은 세지 않음)")]
    public float storyLineSeconds = 3f;
    [Tooltip("[2026-11-20] 키 안내 줄 색 · 크기(%)")]
    public Color keyHintColor = new Color(0.56f, 0.72f, 0.87f, 1f);
    public int keyHintSizePercent = 85;

    [Header("통신창 안내 (2026-11-18 추가)")]
    [Tooltip("통신창 머리글에 나오는 이름. 아직 정체를 모르므로 ???")]
    public string speakerName = "???";
    [Tooltip("켜면 통신창 얼굴을 검은 실루엣으로 가린다")]
    public bool hideSpeakerFace = true;
    [Tooltip("[2026-11-18] 켜면 통신창을 화면 가운데 위에 띄운다 (튜토리얼 한정, 평소 잡담은 왼쪽 그대로)")]
    public bool guideTopCenter = true;

    [Header("튜토리얼 끝 → 로비 (2026-11-18 추가)")]
    [Tooltip("튜토리얼을 끝내면 이동할 방 (LobbyRoom). 비워 두면 이동하지 않음")]
    public GameObject lobbyRoom;
    [Tooltip("마무리 대사가 뜬 뒤 로비로 넘어가기까지 기다리는 시간 (초)")]
    public float lobbyDelay = 3.5f;

    [Header("막이 사라짐 노이즈 (2026-11-18 추가)")]
    [Tooltip("막이가 지직거리다 사라지기까지 걸리는 시간 (초)")]
    public float gateGlitchTime = 0.6f;
    [Tooltip("지직거릴 때 옆으로 흔들리는 폭 (월드)")]
    public float gateGlitchShake = 0.25f;
    [Tooltip("함께 튀어나오는 가로 띠 조각 수")]
    public int gateGlitchSlices = 6;

    int index = -1;
    int dummyDamage;
    int dummyHits;
    bool boxOpened, inventoryAfterBox;
    bool skillUsed;   // [2026-11-18] SkillAndPause 용 (스킬을 썼는지)
    bool attackDone;  // [2026-11-21] AttackSkillPause 용 (허수아비를 다 때렸는지)
    int phase;        // [2026-11-21] CombatBasics 진행 단계 0 공격 · 1 스킬 · 2 무기 교체 · 3 퍼즈 켜기 · 4 퍼즈 중 · 5 증강 고르기 · 6 인벤토리
    Stats watchedDummy;
    PlayerController player;
    bool waitingStart;   // [2026-11-18] 시작 영역에 들어오기 전 (안내 숨김 · 조건 검사 안 함)
    Coroutine storyLines;   // [2026-11-20] 앞 대사·마무리 대사를 보여 주는 중

    void Start()
    {
        KeyBindings.Changed += RefreshText;
        Begin(0);
    }

    void OnDestroy()
    {
        KeyBindings.Changed -= RefreshText;
        AugmentSandbox.End();   // [2026-11-21] 다른 길로 방을 떠나도 연습용 증강은 남기지 않음
        YoungbokChatter.HideGuide();   // [2026-11-18] 방이 바뀌면 안내 창도 내림
        if (watchedDummy != null) watchedDummy.OnHpChanged -= OnDummyHp;
    }

    public Step Current => index >= 0 && index < steps.Count ? steps[index] : null;

    // [2026-11-18 수정] 구간 전환: 시작 영역이 있으면 들어올 때까지 안내를 숨기고 기다린다
    void Begin(int i)
    {
        index = i;
        if (storyLines != null) { StopCoroutine(storyLines); storyLines = null; }   // [2026-11-20] 앞 구간 대사가 남아 있으면 끊음
        if (Current == null && doneLines != null && doneLines.Length > 0) { storyLines = StartCoroutine(PlayStoryLines(doneLines, true)); return; }
        if (watchedDummy != null) { watchedDummy.OnHpChanged -= OnDummyHp; watchedDummy = null; }
        Step s = Current;
        waitingStart = s != null && s.startZone != null && !s.startZone.reached;
        if (waitingStart) RefreshText();
        else Activate();
    }

    // 구간 실제 시작 (안내 표시 + 조건 준비)
    void Activate()
    {
        waitingStart = false;
        dummyDamage = 0;
        dummyHits = 0;
        boxOpened = inventoryAfterBox = false;
        skillUsed = false;
        attackDone = false;
        phase = 0;
        if (watchedDummy != null) { watchedDummy.OnHpChanged -= OnDummyHp; watchedDummy = null; }

        Step s = Current;
        if (s != null)
        {
            if (s.reachZone != null) s.reachZone.reached = false;
            if (s.passZone != null) s.passZone.reached = false;
            if (s.dummy != null) { watchedDummy = s.dummy; watchedDummy.OnHpChanged += OnDummyHp; }
            if (s.box != null) s.box.SetActive(true);
            // 스킬·퍼즈 구간은 스킬을 쓴 뒤에 게이지를 채움 (퍼즈 안내가 나올 때)
            if (s.fillPauseGauge && s.condition != Condition.SkillAndPause && s.condition != Condition.AttackSkillPause && s.condition != Condition.CombatBasics && PauseManager.Instance != null) PauseManager.Instance.AddGauge(100f);
            if (s.grantSkill != null) GrantSkill(s.grantSkill);
            // [2026-11-21] 증강 체험: 지금 상태를 기억해 두고(로비로 갈 때 되돌림) 고르기 창을 연다
            if (s.condition == Condition.AugmentAndInventory)
            {
                AugmentSandbox.Begin();
                AugmentOfferDirector.RequestTutorialOffer();
            }
            if (s.preLines != null && s.preLines.Length > 0) { storyLines = StartCoroutine(PlayStoryLines(s.preLines, false)); return; }
        }
        RefreshText();
    }

    // [2026-11-20] 스토리 대사를 한 줄씩 (구간 조건은 그동안에도 검사함 — 먼저 해 버려도 막히지 않게)
    // holdLast = 마지막 줄을 그대로 둠 (마무리 대사). 아니면 끝난 뒤 조작 안내로 바꿈
    IEnumerator PlayStoryLines(string[] lines, bool holdLast)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string line = WithHint(Keys(lines[i]), holdLast ? null : Current);
            if (titleText != null && Current == null) titleText.text = doneTitle;
            if (guideText != null) guideText.text = line;
            YoungbokChatter.ShowGuide(speakerName, line, hideSpeakerFace, guideTopCenter);
            if (holdLast && i == lines.Length - 1) break;
            float t = 0f;
            while (t < storyLineSeconds) { t += Time.deltaTime; yield return null; }
        }
        storyLines = null;
        if (!holdLast) RefreshText();
    }

    void RefreshText()
    {
        // [2026-11-18 수정] 다음 구간 시작 영역에 들어갈 때까지 앞 구간 안내를 그대로 띄워 둔다
        // (구간 하나 = 이 구간 시작 영역 ~ 다음 구간 시작 영역)
        if (waitingStart) return;
        if (storyLines != null) return;   // [2026-11-20] 스토리 대사를 보여 주는 중에는 조작 안내로 덮지 않음 (키 설정이 바뀌어도)
        Step s = Current;
        string text = s == null ? doneGuide
            : (s.condition == Condition.SkillAndPause && skillUsed && !string.IsNullOrEmpty(s.guide2)) ? s.guide2 : s.guide;
        // [2026-11-21] 공격 → 스킬 → 퍼즈 / 증강 고른 뒤 → 인벤토리
        if (s != null && s.condition == Condition.AttackSkillPause)
            text = !attackDone ? s.guide : (!skillUsed || string.IsNullOrEmpty(s.guide3)) ? (string.IsNullOrEmpty(s.guide2) ? s.guide : s.guide2) : s.guide3;
        if (s != null && s.condition == Condition.AugmentAndInventory && AugmentSandbox.GainedCount > 0 && !string.IsNullOrEmpty(s.guide2))
            text = s.guide2;
        if (s != null && s.condition == Condition.CombatBasics)
            text = PhaseText(s);
        string line = WithHint(Keys(text), s);
        if (titleText != null) titleText.text = s != null ? $"{index + 1} / {steps.Count}  {s.title}" : doneTitle;
        if (guideText != null) guideText.text = line;
        YoungbokChatter.ShowGuide(speakerName, line, hideSpeakerFace, guideTopCenter);   // [2026-11-18] 통신창으로 말하듯이
    }

    // [2026-11-21] CombatBasics 단계별 안내 (비어 있는 칸은 앞 안내를 그대로)
    string PhaseText(Step s)
    {
        string Pick(string a, string fallback) => string.IsNullOrEmpty(a) ? fallback : a;
        switch (phase)
        {
            case 0: return s.guide;
            case 1: return Pick(s.guide2, s.guide);
            case 2: return Pick(s.guideSwap, s.guide);
            case 3: return Pick(s.guide3, s.guide);
            case 4: return Pick(s.guidePaused, s.guide3);
            case 5: return Pick(s.guideAugment, s.guide);
            default: return Pick(s.guideInventory, s.guide);
        }
    }

    void SetPhase(int p)
    {
        phase = p;
        RefreshText();
    }

    bool SkillPressed() =>
        (KeyBindings.Down(KeyAction.Skill1) && HasSkill(0))
        || (KeyBindings.Down(KeyAction.Skill2) && HasSkill(1))
        || (KeyBindings.Down(KeyAction.Skill3) && HasSkill(2));

    // [2026-11-20] 본문 아래에 키 안내 줄을 붙인다
    string WithHint(string line, Step s)
    {
        if (s == null || string.IsNullOrEmpty(s.keyHint)) return line;
        string hex = ColorUtility.ToHtmlStringRGB(keyHintColor);
        return line + $"\n<size={keyHintSizePercent}%><color=#{hex}>" + Keys(s.keyHint) + "</color></size>";
    }

    /// <summary>{Jump} 처럼 쓴 동작 이름을 지금 설정된 키 이름으로 바꾼다</summary>
    static string Keys(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (KeyAction a in System.Enum.GetValues(typeof(KeyAction)))
            text = text.Replace("{" + a + "}", "<b>" + KeyBindings.KeyName(a) + "</b>");
        return text;
    }

    void OnDummyHp(int current, int max)
    {
        int dmg = Mathf.Max(0, max - current);
        dummyDamage += dmg;
        if (dmg > 0) dummyHits++;   // [2026-11-18] 맞은 횟수
    }

    void Update()
    {
        Step s = Current;
        if (s == null) return;
        if (player == null || !player.isActiveAndEnabled) player = FindPlayer();
        if (player == null) return;

        if (waitingStart)
        {
            if (s.startZone == null || s.startZone.reached) Activate();
            return;
        }

        if (Done(s))
        {
            // [2026-11-18 수정] 막이는 노이즈와 함께 사라짐 · "통과!" 글자 제거
            if (s.gate != null) StartCoroutine(GlitchOut(s.gate));
            if (s.moreGates != null)
                foreach (GameObject g in s.moreGates) if (g != null) StartCoroutine(GlitchOut(g));
            Begin(index + 1);
            if (Current == null && (lobbyRoom != null || (MapManager.Instance != null && MapManager.Instance.PlanActive))) StartCoroutine(GoToLobby());
        }
    }

    // ── [2026-11-18 추가] 막이 노이즈 사라짐 ──
    // 충돌은 바로 끄고(곧장 지나갈 수 있게), gateGlitchTime 동안 옆으로 지직 흔들리며 깜빡이다 사라진다.
    // 같은 그림·색으로 가로 띠 조각 몇 개를 만들어 찢어진 화면처럼 튀게 한 뒤 지운다.
    IEnumerator GlitchOut(GameObject gate)
    {
        foreach (Collider2D c in gate.GetComponentsInChildren<Collider2D>()) c.enabled = false;
        // [2026-11-18] 문 그림이 자식(Door)에 있어도 동작하게
        SpriteRenderer sr = gate.GetComponent<SpriteRenderer>();
        if (sr == null) sr = gate.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) { gate.SetActive(false); yield break; }

        Transform tr = sr.transform;
        Vector2 size = sr.sprite.bounds.size;   // 그림 자체 크기 (로컬)
        Vector3 basePos = tr.position;
        Color baseColor = sr.color;

        // 띠 조각: 막이와 같은 그림, 높이 2~6% · 막이보다 한 칸 앞
        var slices = new List<SpriteRenderer>();
        for (int i = 0; i < gateGlitchSlices; i++)
        {
            var go = new GameObject("GlitchSlice");
            go.transform.SetParent(tr, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sr.sprite;
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder + 1;
            r.color = baseColor;
            slices.Add(r);
        }

        float t = 0f, tick = 0f;
        while (t < gateGlitchTime)
        {
            t += Time.deltaTime;
            tick -= Time.deltaTime;
            float k = Mathf.Clamp01(t / gateGlitchTime);   // 0 → 1 (끝으로 갈수록 약해짐)
            if (tick <= 0f)
            {
                tick = 0.04f;
                tr.position = basePos + new Vector3(Random.Range(-1f, 1f) * gateGlitchShake * (1f - k * 0.5f), 0f, 0f);
                Color c = baseColor;
                c.a = baseColor.a * (Random.value < 0.3f ? 0.15f : Random.Range(0.5f, 1.1f)) * (1f - k);
                sr.color = c;
                foreach (var r in slices)
                {
                    bool on = Random.value < 0.6f * (1f - k * 0.6f);
                    r.enabled = on;
                    if (!on) continue;
                    // 그림 크기 기준 좌표 (문 그림이든 하얀 상자든 같은 비율로 찢어짐)
                    float h = Random.Range(0.02f, 0.06f);
                    r.transform.localPosition = new Vector3(Random.Range(-0.6f, 0.6f) * size.x, Random.Range(-0.48f, 0.48f) * size.y, 0f);
                    r.transform.localScale = new Vector3(Random.Range(1f, 2.2f), h, 1f);
                    Color sc = baseColor;
                    sc.a = Mathf.Min(1f, baseColor.a * 1.4f) * (1f - k * 0.7f);
                    r.color = sc;
                }
            }
            yield return null;
        }

        foreach (var r in slices) if (r != null) Destroy(r.gameObject);
        tr.position = basePos;
        sr.color = baseColor;
        gate.SetActive(false);
    }

    // ── [2026-11-18 추가] 튜토리얼 끝 → 로비 ──

    IEnumerator GoToLobby()
    {
        // 마무리 대사를 읽을 시간 (메뉴·인벤토리로 멈춘 시간은 세지 않음)
        while (storyLines != null) yield return null;   // [2026-11-20] 마무리 대사가 끝까지 나온 뒤
        float t = 0f;
        while (t < lobbyDelay) { t += Time.deltaTime; yield return null; }

        if (RoomManager.Instance == null) yield break;
        GameObject room = lobbyRoom;
        // [2026-11-21] 노이즈·블러가 짙어지며 어두워짐 → (튜토리얼 증강 정리 + 로비로) → 블러·노이즈가 걷히며 밝아짐 → 대화 (그동안 조작 잠금)
        // 방이 바뀌면 이 오브젝트도 지워지므로, 연출은 씬에 남는 TransitionCanvas 쪽에서 돌린다
        // [2026-11-21] 스테이지 구성으로 진행 중이면 켜 둔 다음 칸으로 (로비를 꺼 두었으면 그다음, 남은 칸이 없으면 클리어)
        System.Action move = () =>
        {
            AugmentSandbox.End();
            if (MapManager.Instance != null && MapManager.Instance.PlanActive) MapManager.Instance.GoNext();
            else RoomManager.Instance.LoadRoom(room);
        };
        if (TransitionCanvas.Instance != null)
            TransitionCanvas.Instance.StartCoroutine(TransitionCanvas.Instance.GlitchTransition(move));
        else
            move();
    }

    bool Done(Step s)
    {
        // [2026-11-19] 통과 영역까지 왔으면 조건과 상관없이 통과 (조건 동작을 안 하고 지나가도 막히지 않게)
        if (s.passZone != null && s.passZone.reached) return true;
        switch (s.condition)
        {
            case Condition.Jump:        return player.JumpCount >= 1;
            case Condition.DoubleJump:  return player.JumpCount >= 2;
            case Condition.Dash:        return player.IsDashing;
            case Condition.DoubleDash:  return player.DeshCount >= 2;
            case Condition.Reach:       return s.reachZone != null && s.reachZone.reached;
            case Condition.DummyDamage: return dummyDamage >= s.damageNeeded;
            case Condition.DummyHits:   return dummyHits >= s.hitsNeeded;
            case Condition.WeaponSwap:  return KeyBindings.Down(KeyAction.WeaponSwap);
            case Condition.WeaponSkill: return KeyBindings.Down(KeyAction.Skill1) && HasSkill(0);
            case Condition.AnySkill:
                return (KeyBindings.Down(KeyAction.Skill1) && HasSkill(0))
                    || (KeyBindings.Down(KeyAction.Skill2) && HasSkill(1))
                    || (KeyBindings.Down(KeyAction.Skill3) && HasSkill(2));
            case Condition.Pause:       return PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            case Condition.SkillAndPause:
                // [2026-11-18] 순서대로: 스킬 먼저 → 성공하면 퍼즈 안내(guide2)로 바꾸고 게이지 채움 → 퍼즈
                if (!skillUsed)
                {
                    if ((KeyBindings.Down(KeyAction.Skill1) && HasSkill(0))
                        || (KeyBindings.Down(KeyAction.Skill2) && HasSkill(1))
                        || (KeyBindings.Down(KeyAction.Skill3) && HasSkill(2)))
                    {
                        skillUsed = true;
                        if (s.fillPauseGauge && PauseManager.Instance != null) PauseManager.Instance.AddGauge(100f);
                        RefreshText();
                    }
                    return false;
                }
                return PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            case Condition.AttackSkillPause:
                if (!attackDone)
                {
                    if (dummyHits < s.hitsNeeded) return false;
                    attackDone = true;
                    RefreshText();
                    return false;
                }
                if (!skillUsed)
                {
                    if ((KeyBindings.Down(KeyAction.Skill1) && HasSkill(0))
                        || (KeyBindings.Down(KeyAction.Skill2) && HasSkill(1))
                        || (KeyBindings.Down(KeyAction.Skill3) && HasSkill(2)))
                    {
                        skillUsed = true;
                        if (s.fillPauseGauge && PauseManager.Instance != null) PauseManager.Instance.AddGauge(100f);
                        RefreshText();
                    }
                    return false;
                }
                return PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            case Condition.CombatBasics:
            {
                bool paused = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
                switch (phase)
                {
                    case 0: if (dummyHits >= s.hitsNeeded) SetPhase(1); return false;
                    case 1: if (SkillPressed()) SetPhase(2); return false;
                    case 2:
                        if (KeyBindings.Down(KeyAction.WeaponSwap))
                        {
                            if (s.fillPauseGauge && PauseManager.Instance != null) PauseManager.Instance.AddGauge(100f);
                            SetPhase(3);
                        }
                        return false;
                    case 3: if (paused) SetPhase(4); return false;            // 퍼즈가 켜지면 → 퍼즈 설명
                    case 4:                                                   // 퍼즈가 풀리면 → 증강 고르기 (창을 연다)
                        if (!paused)
                        {
                            SetPhase(5);
                            AugmentSandbox.Begin();   // 지금 상태를 기억해 두고 로비로 갈 때 되돌림
                            AugmentOfferDirector.RequestTutorialOffer();
                        }
                        return false;
                    case 5: if (AugmentSandbox.GainedCount > 0) SetPhase(6); return false;   // 하나 고르면 → 인벤토리 안내
                    default: return !paused && KeyBindings.Down(KeyAction.Inventory);
                }
            }
            case Condition.AugmentAndInventory:
                if (!boxOpened && AugmentSandbox.GainedCount > 0) { boxOpened = true; RefreshText(); }
                if (boxOpened && KeyBindings.Down(KeyAction.Inventory)) inventoryAfterBox = true;
                return inventoryAfterBox;
            case Condition.BoxAndInventory:
                if (s.box != null && s.box.IsOpened) boxOpened = true;
                if (boxOpened && KeyBindings.Down(KeyAction.Inventory)) inventoryAfterBox = true;
                return inventoryAfterBox;
        }
        return false;
    }

    // [2026-11-18] 줍기 없이 스킬을 바로 넣어 줌 (이미 같은 스킬이 있으면 그대로)
    void GrantSkill(Skill prefab)
    {
        if (player == null || !player.isActiveAndEnabled) player = FindPlayer();
        if (player == null) return;
        CharacterSkills cs = player.GetComponent<CharacterSkills>();
        if (cs == null) return;
        for (int i = 0; i < cs.skillSlots.Length; i++)
            if (cs.GetSkill(i) != null) return;   // [2026-11-18] 이미 스킬이 있으면 그걸로 연습 (슬롯 가득 경고 방지)
        cs.AcquireSkill(prefab);
    }

    bool HasSkill(int slot)
    {
        CharacterSkills cs = player.GetComponent<CharacterSkills>();
        return cs != null && cs.GetSkill(slot) != null;
    }

    static PlayerController FindPlayer()
    {
        foreach (PlayerController pc in FindObjectsOfType<PlayerController>())
            if (pc.isActiveAndEnabled && pc.CompareTag("Player")) return pc;
        return null;
    }

    /// <summary>구덩이에 빠진 플레이어를 지금 구간 시작 위치로 (피해 없음)</summary>
    public void Respawn(GameObject who)
    {
        Step s = Current;
        Transform cp = s != null && s.checkpoint != null ? s.checkpoint : (steps.Count > 0 ? steps[steps.Count - 1].checkpoint : null);
        if (cp == null || who == null) return;
        who.transform.position = cp.position;
        Rigidbody2D rb = who.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
    }
}
