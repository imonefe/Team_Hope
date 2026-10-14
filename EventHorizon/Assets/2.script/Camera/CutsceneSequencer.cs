using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Com.LuisPedroFonseca.ProCamera2D;

/// <summary>
/// 플레이어 입력 없이 초 단위로 자동 진행되는 컷신용 시퀀서.
/// (F키/클릭으로 넘기는 Npc.cs의 대사 시스템과는 별개 — 보스 등장 같은 논-인터랙티브 연출용)
///
/// 인스펙터에 스텝을 순서대로 나열해두면 위에서부터 하나씩 실행된다.
/// 코드를 새로 짜지 않고, 스텝 추가/순서 변경/값 조정만으로 새 연출을 만들 수 있게 하는 게 목적.
/// </summary>
// [2026-09-29: ScreenTransition 스텝은 ScreenFader.cs가 죽은 코드로 삭제되면서 함께 제거됨.
// 화면 암전 연출이 필요하면 TransitionCanvas.cs(방 전환용 페이드)를 참고해서 새로 추가할 것.]
public enum CutsceneStepType
{
    Wait,               // 그냥 대기
    ZoomTo,             // 카메라 줌 (절대 크기로)
    FocusOn,            // 카메라 포커스를 특정 대상으로 이동
    PlayAnimatorTrigger,// 애니메이터 트리거 실행
    SlowMotion,         // 슬로우모션 진입 → 유지 → 복귀
    Shake,              // 카메라 흔들림
    Dialogue,           // 대사 한 줄 출력
}

[System.Serializable]
public class CutsceneStep
{
    public string label; // 리스트에서 알아보기 쉽게 다는 메모 — 실행 로직과는 무관
    public CutsceneStepType type = CutsceneStepType.Wait;

    // Wait
    public float waitSeconds = 1f;

    // ZoomTo (orthographic size 절대값)
    public float zoomSize = 3f;
    public float zoomDuration = 0.5f;

    // FocusOn
    public Transform focusTarget;
    public float focusDuration = 0.6f;

    // PlayAnimatorTrigger
    public Animator animator;
    public string triggerName;

    // SlowMotion
    public float slowMotionScale = 0.2f;
    public float slowMotionHoldSeconds = 1f;
    public float slowMotionRampSeconds = 0.25f;

    // Shake
    public float shakeDuration = 0.3f;
    public float shakeStrength = 0.3f;

    // Dialogue
    public string speakerName;
    [TextArea(2, 4)] public string dialogueText;
    public float dialogueHoldSeconds = 2f;
}

public class CutsceneSequencer : MonoBehaviour
{
    [Header("참조 (씬에 있는 카메라/UI 컴포넌트 드래그)")]
    public ProCamera2D proCam;
    public ProCamera2DShake proCamShake;
    public CutsceneDialogueBox dialogueBox;

    [Header("컷신 스텝 목록 (위에서부터 순서대로 재생)")]
    public List<CutsceneStep> steps = new List<CutsceneStep>();

    [Header("씬 시작하자마자 자동 재생할지")]
    public bool playOnStart = false;

    Transform currentFocusTarget;
    Coroutine playRoutine;
    float baseFixedDeltaTime;

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>처음부터 재생. 이미 재생 중이면 멈추고 처음부터 다시 시작한다.</summary>
    public void Play()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>재생 중단. timeScale이 슬로우모션 도중에 눌린 채로 있었다면 정상 복구한다.</summary>
    public void Stop()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);
        playRoutine = null;
        RestoreTime();
    }

    // [2026-10-10 추가] 시간 복구 — 메뉴 등 창이 시간을 멈추고 있으면 0을 유지하고, 물리 간격은 항상 원래 값으로
    void RestoreTime()
    {
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime;
        Time.timeScale = GamePause.IsPaused ? 0f : 1f;
    }

    IEnumerator PlayRoutine()
    {
        foreach (var step in steps)
            yield return RunStep(step);

        playRoutine = null;
    }

    IEnumerator RunStep(CutsceneStep step)
    {
        switch (step.type)
        {
            case CutsceneStepType.Wait:
                yield return new WaitForSeconds(step.waitSeconds);
                break;

            case CutsceneStepType.ZoomTo:
                if (proCam != null)
                {
                    float currentHalfHeight = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
                    proCam.Zoom(step.zoomSize - currentHalfHeight, step.zoomDuration);
                }
                yield return new WaitForSeconds(step.zoomDuration);
                break;

            case CutsceneStepType.FocusOn:
                if (proCam != null && step.focusTarget != null)
                {
                    var previous = currentFocusTarget;
                    proCam.AddCameraTarget(step.focusTarget, 1f, 1f, step.focusDuration, Vector2.zero);
                    if (previous != null && previous != step.focusTarget)
                        proCam.RemoveCameraTarget(previous, step.focusDuration);
                    currentFocusTarget = step.focusTarget;
                }
                yield return new WaitForSeconds(step.focusDuration);
                break;

            case CutsceneStepType.PlayAnimatorTrigger:
                if (step.animator != null && !string.IsNullOrEmpty(step.triggerName))
                    step.animator.SetTrigger(step.triggerName);
                break;

            case CutsceneStepType.SlowMotion:
                yield return SlowMotionStep(step);
                break;

            case CutsceneStepType.Shake:
                if (proCamShake != null)
                    proCamShake.Shake(step.shakeDuration, new Vector2(step.shakeStrength, step.shakeStrength));
                yield return new WaitForSeconds(step.shakeDuration);
                break;

            case CutsceneStepType.Dialogue:
                if (dialogueBox != null)
                {
                    dialogueBox.Show(step.speakerName, step.dialogueText);
                    yield return new WaitForSecondsRealtime(step.dialogueHoldSeconds);
                    dialogueBox.Hide();
                }
                else
                {
                    yield return new WaitForSeconds(step.dialogueHoldSeconds);
                }
                break;
        }
    }

    IEnumerator SlowMotionStep(CutsceneStep step)
    {
        yield return RampTimeScale(1f, step.slowMotionScale, step.slowMotionRampSeconds);

        // 유지 구간 — 메뉴가 열려 있는 동안은 시간을 세지 않고, 닫히면 슬로모션 배율을 다시 적용
        float held = 0f;
        while (held < step.slowMotionHoldSeconds)
        {
            if (!GamePause.IsPaused)
            {
                held += Time.unscaledDeltaTime;
                SetTimeScale(step.slowMotionScale);
            }
            yield return null;
        }

        yield return RampTimeScale(step.slowMotionScale, 1f, step.slowMotionRampSeconds);
    }

    IEnumerator RampTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetTimeScale(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            // [2026-10-10] 메뉴 등 창이 시간을 멈춘 동안에는 진행도 덮어쓰기도 하지 않는다
            // (예전엔 다음 프레임에 timeScale을 다시 써서 메뉴 뒤에서 게임이 흘렀음)
            if (!GamePause.IsPaused)
            {
                t += Time.unscaledDeltaTime;
                SetTimeScale(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            }
            yield return null;
        }
        SetTimeScale(to);
    }

    void SetTimeScale(float scale)
    {
        if (GamePause.IsPaused) return;
        Time.timeScale = scale;
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime * scale;
    }

    // [2026-09-29 추가] 컷신 재생 도중(특히 SlowMotion 스텝 진행 중) 이 오브젝트가
    // SetActive(false)로 비활성화되면 PlayRoutine 코루틴이 그 자리에서 멈추고 OnDestroy도
    // 호출되지 않아 timeScale이 눌린 채로 영구히 남는 버그가 있었다. Stop()과 동일하게
    // 비활성화 시점에도 복구해준다.
    void OnDisable()
    {
        RestoreTime();
        playRoutine = null; // 멈춘 코루틴 참조 정리 — 이후 Play()가 새로 시작할 수 있도록
    }

    void OnDestroy()
    {
        RestoreTime();
    }
}
