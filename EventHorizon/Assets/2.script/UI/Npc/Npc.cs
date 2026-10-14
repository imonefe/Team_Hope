using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class NPC : MonoBehaviour
{
    [Header("텍스트 설정")]
    public TextMeshProUGUI textComponent;
    public float textSpeed = 0.05f;

    [Header("UI 캔버스")]
    public GameObject dialogueCanvas;
    public GameObject nextCanvasPrefab;   // 대화 후 띄울 상점/스탯 캔버스

    [Header("초상화")]
    public Image[] portraits;

    [Header("대사 세트")]
    public DialogueSet[] dialogueSets;

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
        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    void Update()
    {
        if (!dialogueStarted || lines == null || lines.Length == 0) return;

        // [2026-10-10 추가] ESC 메뉴·인벤토리가 열려 있으면 대사를 넘기지 않음
        // (메뉴의 "계속하기"를 누른 클릭이 대사 넘기기로도 처리되던 문제)
        if (GamePause.IsPaused) return;

        // 시작 첫 프레임은 입력 무시 (트리거 F가 다음 줄로 새는 것 방지)
        if (justStarted) { justStarted = false; return; }

        // F 또는 마우스 클릭으로 다음 줄
        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
        {
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

    IEnumerator TypeLine()
    {
        string line = lines[index].Replace("\\n", "\n");
        textComponent.text = "";

        foreach (char c in line)
        {
            textComponent.text += c;
            yield return new WaitForSeconds(textSpeed);
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

    void EndDialogue()
    {
        dialogueStarted = false;
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
        if (portraits == null || portraits.Length == 0) return;
        if (speakers == null || speakers.Length == 0) return;

        int currentSpeaker = (index < speakers.Length) ? speakers[index] : 0;

        for (int i = 0; i < portraits.Length; i++)
        {
            float brightness = (i == currentSpeaker) ? 1f : 0.3f;
            portraits[i].color = new Color(brightness, brightness, brightness, 1f);
        }
    }
}
