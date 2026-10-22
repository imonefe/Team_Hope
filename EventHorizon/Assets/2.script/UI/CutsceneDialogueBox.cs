using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 시간 기반 컷신(CutsceneSequencer)에서 쓰는 대사창.
/// Npc.cs의 클릭컶-넘김 대사 시스템과는 완전히 별개 — 여기선 지정한 시간만큼 떠 있다가 자동으로 사라진다.
/// </summary>
public class CutsceneDialogueBox : MonoBehaviour
{
    [Header("UI 참조")]
    public CanvasGroup canvasGroup;
    public Text speakerText;
    public Text lineText;

    [Header("타이핑 효과 (0이면 한 번에 표시)")]
    public float typeSpeed = 0.015f;   // [2026-10-21] 0.03 → 0.015 (2배 빠르게)

    Coroutine typingRoutine;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    public void Show(string speaker, string line)
    {
        if (speakerText != null) speakerText.text = speaker;

        if (typingRoutine != null) StopCoroutine(typingRoutine);

        if (typeSpeed > 0f)
            typingRoutine = StartCoroutine(TypeRoutine(line));
        else if (lineText != null)
            lineText.text = line;

        DOTween.Kill(canvasGroup);
        canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
    }

    public void Hide()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        DOTween.Kill(canvasGroup);
        canvasGroup.DOFade(0f, 0.2f).SetUpdate(true);
    }

    IEnumerator TypeRoutine(string line)
    {
        if (lineText == null) yield break;

        lineText.text = "";
        // [2026-10-21 수정] 흐른 시간으로 보여줄 글자 수 계산 (프레임 단위로 끊겨 느려지지 않게)
        int shown = 0;
        float t = 0f;
        while (shown < line.Length)
        {
            int target = Mathf.Min(line.Length, Mathf.FloorToInt(t / typeSpeed) + 1);
            if (target > shown)
            {
                shown = target;
                lineText.text = line.Substring(0, shown);
            }
            if (shown >= line.Length) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }
    }
}
