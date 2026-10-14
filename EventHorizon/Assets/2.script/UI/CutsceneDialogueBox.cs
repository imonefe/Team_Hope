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
    public float typeSpeed = 0.03f;

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
        foreach (char c in line)
        {
            lineText.text += c;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }
    }
}
