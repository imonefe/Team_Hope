using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class NPC : MonoBehaviour
{
    [Header("텍스트 설정")]
    public TextMeshProUGUI textComponent;
    public float textSpeed = 0.025f;   // [2026-10-21] 0.05 → 0.025 (2배 빠르게)

    [Header("UI 캔버스")]
    public GameObject dialogueCanvas;
    public GameObject nextCanvasPrefab;   // 대화 후 띄울 상점/스탯 캔버스

    [Header("초상화")]
    public Image[] portraits;

    [Header("이름표 (2026-10-17 추가)")]
    [Tooltip("말하는 사람 이름을 띄울 이름표 오브젝트 (이름이 비어 있으면 숨김)")]
    public GameObject namePlate;
    public TextMeshProUGUI nameText;
    [Tooltip("portraits 와 같은 순서로 말하는 사람 이름 (대사의 speakerIndex 번호로 고름)")]
    public string[] speakerNames;

    [Header("대사 세트")]
    public DialogueSet[] dialogueSets;

    [Header("다음 표시 (2026-10-21 추가) — 대사가 다 나오면 끝 글자 옆에 깜빡이는 마름모")]
    public bool showNextIndicator = true;
    [Tooltip("마름모 크기 (픽셀)")]
    public float nextIndicatorSize = 11f;
    [Tooltip("끝 글자 오른쪽에서 떨어진 거리 (x), 글자 기준선에서 올린 높이 (y)")]
    public Vector2 nextIndicatorOffset = new Vector2(14f, 8f);
    [Tooltip("위아래로 흔들리는 폭 (픽셀)")]
    public float nextIndicatorBob = 3f;
    [Tooltip("1초에 몇 번 흔들리고 깜빡이는지")]
    public float nextIndicatorSpeed = 1.6f;

    [Header("꾹 눌러 빨리 넘기기 (2026-10-21 추가)")]
    [Tooltip("키·마우스 버튼을 이 시간(초) 이상 누르고 있으면 빨리 넘기기 시작")]
    public float holdDelay = 0.4f;
    [Tooltip("빨리 넘기는 동안 다음 대사로 넘어가는 간격(초). 넘어간 대사는 타이핑 없이 한 번에 보임")]
    public float holdRepeat = 0.1f;

    [Header("건너뛰기 버튼 (2026-10-21 추가)")]
    [Tooltip("대화 중 화면 오른쪽 위에 '건너뛰기' 버튼을 띄운다 (마우스로 눌러야만 건너뜀)")]
    public bool showSkipButton = true;
    public string skipLabel = "건너뛰기 >>";
    [Tooltip("화면 오른쪽 위 모서리에서 떨어진 거리 (1920×1080 기준 픽셀)")]
    public Vector2 skipButtonOffset = new Vector2(-40f, -40f);
    public Vector2 skipButtonSize = new Vector2(190f, 50f);
    public float skipFontSize = 24f;
    public Color skipBackColor = new Color(0f, 0f, 0f, 0.45f);
    public Color skipTextColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("컷신 (선택)")]
    public GameObject[] cutscenes;
    public int[] cutsceneStartLines;
    public int[] cutsceneEndLines;

    private string[] lines;
    private int[] speakers;
    private int index = 0;
    private bool dialogueStarted = false;
    private bool justStarted = false;
    private PlayerController currentPlayer;
    private bool holding;               // [2026-10-21] 대화 중에 새로 누른 키를 계속 누르고 있는지
    private float holdTime;
    private float repeatTimer;
    private RectTransform nextIndicator; // [2026-10-21] 코드로 만든 '다음' 표시
    private Image nextIndicatorImage;
    private RectTransform skipButton;   // [2026-10-21] 코드로 만든 건너뛰기 버튼
    private Canvas skipCanvas;

    public bool IsTalking => dialogueStarted;

    // [2026-10-10 추가] 지금 대화 중인 NPC가 하나라도 있는지 (인벤토리·상호작용 등에서 확인용)
    static readonly System.Collections.Generic.HashSet<NPC> talking = new System.Collections.Generic.HashSet<NPC>();
    public static bool AnyTalking
    {
        get { talking.RemoveWhere(n => n == null); return talking.Count > 0; }
    }

    /// <summary>[2026-10-10 추가] 마지막으로 대화가 끝난 프레임. 같은 F 입력으로 대화가 다시 열리거나 다른 상호작용이 실행되지 않게 함</summary>
    public static int LastEndFrame = -1;

    void OnDisable()
    {
        talking.Remove(this);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        if (nextIndicator != null) nextIndicator.gameObject.SetActive(false);
    }

    [System.Serializable]
    public class DialogueSet
    {
        public string conditionKey;

        [TextArea(2, 10)]
        public string[] lines;

        [Tooltip("각 줄을 누가 말하는지 (portraits 인덱스)")]
        public int[] speakerIndex;
    }

    public void SetCondition(string condition = null, PlayerController player = null)
    {
        DialogueSet targetSet = string.IsNullOrEmpty(condition)
            ? dialogueSets.FirstOrDefault()
            : dialogueSets.FirstOrDefault(d => d.conditionKey == condition);

        if (targetSet != null)
        {
            lines = targetSet.lines;
            speakers = targetSet.speakerIndex;
            index = 0;
            StartDialogue(player);
        }
    }

    public void StartDialogue(PlayerController player)
    {
        currentPlayer = player;

        if (lines == null || lines.Length == 0) return;

        bool alreadyOpen = dialogueStarted;
        dialogueStarted = true;
        justStarted = true;
        holding = false;
        talking.Add(this);

        // 플레이어 조작 잠금 (이 NPC 이름으로 잠금 — 메뉴·인벤토리를 열고 닫아도 대화 잠금은 유지됨)
        if (currentPlayer != null)
            currentPlayer.LockControl(this);

        if (dialogueCanvas != null)
            dialogueCanvas.SetActive(true);

        // 대화가 시작될 때 전투 UI를 잠근다 (이미 대화 중이었다면 중복으로 잠그지 않음)
        if (!alreadyOpen)
            CombatUIGate.Block();

        UpdatePortraits();
        ShowSkipButton(true);
        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    void Update()
    {
        if (!dialogueStarted || lines == null || lines.Length == 0) return;

        UpdateNextIndicator();

        // [2026-10-10 추가] ESC 메뉴·인벤토리가 열려 있으면 대사를 넘기지 않음
        // (메뉴의 "계속하기"를 누른 클릭이 대사 넘기기로도 처리되던 문제)
        if (GamePause.IsPaused) return;

        // 시작 첫 프레임은 입력 무시 (트리거 F가 다음 줄로 새는 것 방지)
        if (justStarted) { justStarted = false; holding = false; return; }

        // [2026-10-21] 꾹 누르고 있으면 빨리 넘기기 (대화 중에 새로 누른 키만 — 아래 '누름'에서 holding 을 켬)
        if (holding)
        {
            if (!Input.anyKey || Input.GetKey(KeyCode.Escape)) holding = false;
            else
            {
                float dt = Time.unscaledDeltaTime;
                holdTime += dt;
                if (holdTime >= holdDelay)
                {
                    repeatTimer -= dt;
                    if (repeatTimer <= 0f)
                    {
                        repeatTimer = holdRepeat;
                        FastForwardStep();
                        if (!dialogueStarted) return;   // 마지막 줄을 넘겨서 대화가 끝남
                    }
                }
            }
        }

        // [2026-10-21 수정] F·클릭만 → 아무 키나 아무 마우스 버튼으로 다음 줄 (ESC 는 메뉴용이라 제외)
        // 건너뛰기 버튼 위를 누른 클릭은 버튼이 처리하므로 대사 넘기기로 치지 않는다
        bool mouseDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        if (mouseDown && PointerOnSkipButton()) return;
        if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape))
        {
            // 누르기 시작 → 계속 누르고 있으면 holdDelay 뒤부터 빨리 넘기기
            holding = true;
            holdTime = 0f;
            repeatTimer = 0f;

            string line = lines[index].Replace("\\n", "\n");

            if (textComponent.text == line)
                NextLine();
            else
            {
                StopAllCoroutines();
                textComponent.text = line;
            }
        }
    }

    // ── [2026-10-21 추가] '다음' 표시 ──────────────────────
    // 대사가 다 나왔을 때만 끝 글자 오른쪽에 마름모를 띄우고 위아래로 흔들며 깜빡인다
    void UpdateNextIndicator()
    {
        if (!showNextIndicator || textComponent == null) return;

        string line = lines[index].Replace("\\n", "\n");
        bool full = textComponent.text == line && line.Length > 0;
        if (!full)
        {
            if (nextIndicator != null && nextIndicator.gameObject.activeSelf) nextIndicator.gameObject.SetActive(false);
            return;
        }

        EnsureNextIndicator();
        if (!nextIndicator.gameObject.activeSelf) nextIndicator.gameObject.SetActive(true);

        // 끝 글자 위치 (글상자 안 좌표). 글자 정보가 아직 없으면 글상자 오른쪽 아래
        Vector2 basePos;
        TMP_TextInfo info = textComponent.textInfo;
        int last = -1;
        if (info != null)
            for (int i = info.characterCount - 1; i >= 0; i--)
                if (info.characterInfo[i].isVisible) { last = i; break; }
        if (last >= 0)
        {
            TMP_CharacterInfo ci = info.characterInfo[last];
            basePos = new Vector2(ci.topRight.x, ci.baseLine);
        }
        else
        {
            Rect r = textComponent.rectTransform.rect;
            basePos = new Vector2(r.xMax, r.yMin);
        }

        float wave = Mathf.Sin(Time.unscaledTime * nextIndicatorSpeed * Mathf.PI * 2f);
        nextIndicator.localPosition = new Vector3(basePos.x + nextIndicatorOffset.x,
                                                  basePos.y + nextIndicatorOffset.y + wave * nextIndicatorBob, 0f);
        Color c = textComponent.color;
        c.a *= 0.55f + 0.45f * (0.5f + 0.5f * wave);
        nextIndicatorImage.color = c;
    }

    // 새 그림 없이 사각형 이미지를 45도 돌린 마름모 (대사 글상자의 자식이라 대화창과 같이 켜지고 꺼짐)
    void EnsureNextIndicator()
    {
        if (nextIndicator != null) return;
        var go = new GameObject("NextIndicator", typeof(RectTransform), typeof(Image));
        nextIndicator = (RectTransform)go.transform;
        nextIndicator.SetParent(textComponent.rectTransform, false);
        nextIndicator.anchorMin = nextIndicator.anchorMax = textComponent.rectTransform.pivot;
        nextIndicator.pivot = new Vector2(0.5f, 0.5f);
        nextIndicator.sizeDelta = new Vector2(nextIndicatorSize, nextIndicatorSize);
        nextIndicator.localRotation = Quaternion.Euler(0f, 0f, 45f);
        nextIndicatorImage = go.GetComponent<Image>();
        nextIndicatorImage.raycastTarget = false;
    }

    // 빨리 넘기기 한 번: 타이핑 중이면 다 보여 주고, 다 보였으면 다음 줄로 넘어가서 바로 다 보여 준다
    void FastForwardStep()
    {
        string line = lines[index].Replace("\\n", "\n");
        if (textComponent.text != line)
        {
            StopAllCoroutines();
            textComponent.text = line;
            return;
        }

        NextLine();
        if (!dialogueStarted || lines == null) return;
        StopAllCoroutines();
        textComponent.text = lines[index].Replace("\\n", "\n");
    }

    IEnumerator TypeLine()
    {
        string line = lines[index].Replace("\\n", "\n");
        textComponent.text = "";

        // [2026-10-21 수정] '한 글자 → textSpeed 만큼 대기' 대신 흐른 시간으로 보여줄 글자 수를 계산한다.
        // (대기가 프레임 단위로 끊겨서 0.025초로 줄여도 실제로는 0.033초가 되던 문제 → 이제 한 프레임에 여러 글자도 나옴)
        int shown = 0;
        float t = 0f;
        while (shown < line.Length)
        {
            int target = textSpeed > 0f ? Mathf.Min(line.Length, Mathf.FloorToInt(t / textSpeed) + 1) : line.Length;
            if (target > shown)
            {
                shown = target;
                textComponent.text = line.Substring(0, shown);
            }
            if (shown >= line.Length) break;
            yield return null;
            t += Time.deltaTime;
        }
    }

    void NextLine()
    {
        if (index < lines.Length - 1)
        {
            index++;
            HandleCutscene(index);
            UpdatePortraits();
            StopAllCoroutines();
            StartCoroutine(TypeLine());
        }
        else
        {
            EndDialogue();
        }
    }

    // ── [2026-10-21 추가] 건너뛰기 ─────────────────────────
    /// <summary>남은 대사를 전부 건너뛰고 대화를 끝낸다 (건너뛰기 버튼이 부름)</summary>
    public void Skip()
    {
        if (!dialogueStarted || lines == null || lines.Length == 0) return;
        if (GamePause.IsPaused) return;   // 메뉴가 열려 있으면 무시

        StopAllCoroutines();
        ApplySkippedCutscenes(index + 1, lines.Length - 1);
        index = lines.Length - 1;
        EndDialogue();
    }

    // 건너뛴 줄들에서 켜지고 꺼졌을 컷신 오브젝트의 '마지막 상태'만 맞춘다.
    // DialogueBeat(한 번 재생하고 스스로 꺼지는 대사 연출)는 건너뛸 때 재생하지 않는다.
    void ApplySkippedCutscenes(int from, int to)
    {
        if (cutscenes == null) return;
        for (int i = 0; i < cutscenes.Length; i++)
        {
            GameObject c = cutscenes[i];
            if (c == null) continue;

            bool on = c.activeSelf;
            bool changed = false;
            for (int line = from; line <= to; line++)
            {
                if (cutsceneStartLines != null && i < cutsceneStartLines.Length && cutsceneStartLines[i] == line) { on = true; changed = true; }
                if (cutsceneEndLines != null && i < cutsceneEndLines.Length && cutsceneEndLines[i] == line) { on = false; changed = true; }
            }
            if (!changed) continue;
            if (on && c.GetComponent<DialogueBeat>() != null) continue;
            c.SetActive(on);
        }
    }

    void ShowSkipButton(bool show)
    {
        if (show) EnsureSkipButton();
        if (skipButton != null) skipButton.gameObject.SetActive(show);
    }

    // 대사 글자가 있는 캔버스(맨 위 캔버스)의 오른쪽 위에 버튼을 만든다. 새 그림 없이 반투명 사각형 + 대사 글꼴
    void EnsureSkipButton()
    {
        if (!showSkipButton || skipButton != null || textComponent == null) return;
        Canvas canvas = textComponent.canvas;
        if (canvas == null) return;
        skipCanvas = canvas.rootCanvas;

        var go = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        skipButton = (RectTransform)go.transform;
        skipButton.SetParent(skipCanvas.transform, false);
        skipButton.anchorMin = skipButton.anchorMax = skipButton.pivot = Vector2.one;
        skipButton.anchoredPosition = skipButtonOffset;
        skipButton.sizeDelta = skipButtonSize;
        skipButton.SetAsLastSibling();

        var bg = go.GetComponent<Image>();
        bg.color = skipBackColor;
        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(Skip);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(skipButton, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = textComponent.font;
        label.text = skipLabel;
        label.fontSize = skipFontSize;
        label.color = skipTextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    bool PointerOnSkipButton()
    {
        if (skipButton == null || !skipButton.gameObject.activeInHierarchy) return false;
        Camera cam = skipCanvas != null && skipCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? skipCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(skipButton, Input.mousePosition, cam);
    }

    void EndDialogue()
    {
        dialogueStarted = false;
        ShowSkipButton(false);
        if (nextIndicator != null) nextIndicator.gameObject.SetActive(false);
        lines = null;
        talking.Remove(this);
        LastEndFrame = Time.frameCount;

        if (dialogueCanvas != null)
            dialogueCanvas.SetActive(false);

        // 대화가 끝나면 전투 UI 잠금을 해제한다.
        CombatUIGate.Unblock();

        // 대화 후 상점/스탯 캔버스 띄우기
        if (nextCanvasPrefab != null)
            Instantiate(nextCanvasPrefab, Vector3.zero, Quaternion.identity);

        // 조작 복원
        if (currentPlayer != null)
            currentPlayer.ResumeControl(this);
    }

    void HandleCutscene(int lineIndex)
    {
        if (cutscenes == null) return;

        // [2026-10-10 수정] 배열 길이가 서로 달라도 예외가 나지 않게 범위 확인
        if (cutsceneStartLines != null)
            for (int i = 0; i < cutsceneStartLines.Length && i < cutscenes.Length; i++)
                if (cutsceneStartLines[i] == lineIndex && cutscenes[i] != null)
                    cutscenes[i].SetActive(true);

        if (cutsceneEndLines != null)
            for (int i = 0; i < cutsceneEndLines.Length && i < cutscenes.Length; i++)
                if (cutsceneEndLines[i] == lineIndex && cutscenes[i] != null)
                    cutscenes[i].SetActive(false);
    }

    void UpdatePortraits()
    {
        UpdateSpeakerName();

        if (portraits == null || portraits.Length == 0) return;
        if (speakers == null || speakers.Length == 0) return;

        int currentSpeaker = (index < speakers.Length) ? speakers[index] : 0;

        for (int i = 0; i < portraits.Length; i++)
        {
            float brightness = (i == currentSpeaker) ? 1f : 0.3f;
            portraits[i].color = new Color(brightness, brightness, brightness, 1f);
        }
    }

    // [2026-10-17 추가] 지금 말하는 사람 이름을 이름표에 띄움. 이름이 없으면 이름표를 숨김
    void UpdateSpeakerName()
    {
        if (nameText == null && namePlate == null) return;

        int speaker = (speakers != null && index < speakers.Length) ? speakers[index] : 0;
        string speakerName = (speakerNames != null && speaker >= 0 && speaker < speakerNames.Length)
            ? speakerNames[speaker] : null;
        bool show = !string.IsNullOrEmpty(speakerName);

        if (nameText != null) nameText.text = show ? speakerName : "";
        if (namePlate != null) namePlate.SetActive(show);
    }
}
