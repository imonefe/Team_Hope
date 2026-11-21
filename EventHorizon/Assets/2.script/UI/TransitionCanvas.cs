using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 전환 시 페이드 인/아웃 연출
/// Canvas 오브젝트에 붙이기
/// </summary>
public class TransitionCanvas : PersistentSingleton<TransitionCanvas>
{

    [Header("페이드 설정")]
    public Image fadeImage;        // 전체 화면 덮는 검정 Image
    public float fadeDuration = 0.6f;

    // [2026-11-19 추가] 중간 보스 방 드나들 때 쓰는 회로 덮개 연출 (CircuitCover 오브젝트, 셰이더 EventHorizon/UI/CircuitCover)
    [Header("회로 전환 (중간 보스 방)")]
    [Tooltip("전체 화면 Image (머티리얼 M_CircuitCover). 비어 있으면 기존 페이드로 대신함")]
    public Image circuitImage;
    [Tooltip("회로가 화면을 다 덮기까지 걸리는 시간 (초)")]
    public float circuitCoverDuration = 1.1f;
    [Tooltip("다 덮인 채로 머무는 시간 (초)")]
    public float circuitHoldTime = 0.25f;
    [Tooltip("회로가 걷히는 시간 (초)")]
    public float circuitRevealDuration = 0.8f;

    Material circuitMat;
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int UTimeId = Shader.PropertyToID("_UTime");

    void Awake()
    {
        // 중복이면 스스로 파괴, 아니면 유일한 인스턴스로 등록 + 씬 전환에도 유지 (PersistentSingleton)
        if (!InitSingleton()) return;

        // 시작 시 투명하게
        SetAlpha(0f);
        gameObject.SetActive(true);

        // [2026-11-19] 회로 덮개: 머티리얼을 복사해서 진행값을 바꿔도 원본 에셋은 그대로
        if (circuitImage != null && circuitImage.material != null)
        {
            circuitMat = new Material(circuitImage.material);
            circuitImage.material = circuitMat;
        }
        SetCircuit(0f, false);

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // [2026-10-10 추가] 페이드 도중 재시작 등으로 전환 코루틴이 끊기면, 씬을 넘어 유지되는 이 화면이
    // 어두운(클릭도 막는) 상태로 남을 수 있었다 → 새 씬이 열릴 때마다 투명하게 되돌린다
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
        ResetFade();
    }

    public void ResetFade()
    {
        StopAllCoroutines();
        SetAlpha(0f);
        SetCircuit(0f, false);   // [2026-11-19]
    }

    /// <summary>
    /// [2026-11-19 추가] 회로가 화면을 덮음 → 콜백 → 회로가 걷힘. 중간 보스 방 드나들 때 RoomManager 에서 호출.
    /// 회로 덮개가 없으면 기존 페이드(Transition)로 대신한다.
    /// </summary>
    public IEnumerator CircuitTransition(System.Action onMidpoint)
    {
        if (circuitImage == null || circuitMat == null)
        {
            yield return Transition(onMidpoint);
            yield break;
        }

        yield return CircuitRun(0f, 1f, circuitCoverDuration);   // 덮기

        onMidpoint?.Invoke();                                     // 다 가려진 순간 방 전환

        float t = 0f;
        while (t < circuitHoldTime)                               // 잠깐 머묾 (회로 빛은 계속 흐름)
        {
            t += Time.unscaledDeltaTime;
            circuitMat.SetFloat(UTimeId, Time.unscaledTime);
            yield return null;
        }

        yield return CircuitRun(1f, 0f, circuitRevealDuration);  // 걷기
        SetCircuit(0f, false);
    }

    IEnumerator CircuitRun(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0 이어도 작동
            float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetCircuit(Mathf.Lerp(from, to, k), true);
            yield return null;
        }
        SetCircuit(to, true);
    }

    void SetCircuit(float progress, bool show)
    {
        if (circuitImage == null) return;
        circuitImage.enabled = show;
        circuitImage.raycastTarget = show;   // 덮여 있는 동안 UI 클릭 막기
        if (circuitMat != null)
        {
            circuitMat.SetFloat(ProgressId, progress);
            circuitMat.SetFloat(UTimeId, Time.unscaledTime);
        }
    }

    /// <summary>[2026-11-21] 노이즈·블러 전환이 진행 중인지 (로비 대화가 이게 끝날 때까지 기다림)</summary>
    public static bool GlitchBusy { get; private set; }

    /// <summary>
    /// [2026-11-21 추가] 노이즈 + 블러가 짙어지며 어두워짐 → 콜백(방 이동) → 블러·노이즈가 걷히며 밝아짐.
    /// 그동안과 끝난 뒤 대화가 열릴 때까지(최대 holdControlMax 초) 조작 잠금. 튜토리얼 → 로비에서 사용.
    /// </summary>
    public IEnumerator GlitchTransition(System.Action onMidpoint, float outTime = 1.3f, float hold = 0.35f, float inTime = 1.5f, float holdControlMax = 3f)
    {
        GlitchBusy = true;
        object lockKey = this;
        var pc = PlayerController.Instance;
        if (pc != null) pc.LockControl(lockKey);

        // 1) 노이즈·블러가 먼저 오르고, 뒤따라 어두워짐
        for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
        {
            float k = t / outTime;
            PauseGlitchEffect.TransitionNoise = Mathf.SmoothStep(0f, 0.9f, Mathf.Clamp01(k * 1.4f));
            MenuBackdrop.SetTransition(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.25f)));
            SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.35f) / 0.65f)));
            yield return null;
        }
        SetAlpha(1f);
        onMidpoint?.Invoke();
        for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;

        // 2) 밝아지면서 블러·노이즈가 서서히 걷힘
        for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
        {
            float k = t / inTime;
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.5f)));
            MenuBackdrop.SetTransition(1f - Mathf.SmoothStep(0f, 1f, k));
            PauseGlitchEffect.TransitionNoise = 0.9f * (1f - Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        SetAlpha(0f);
        MenuBackdrop.SetTransition(0f);
        PauseGlitchEffect.TransitionNoise = 0f;
        GlitchBusy = false;

        // 3) 대화가 열릴 때까지 조작 잠금 유지 (대화가 열리면 대화가 잠금을 맡음)
        for (float t = 0f; t < holdControlMax && !NPC.AnyTalking; t += Time.unscaledDeltaTime) yield return null;
        if (pc != null) pc.ResumeControl(lockKey);
    }

    /// <summary>
    /// 페이드 아웃(어두워짐) → 콜백 → 페이드 인(밝아짐)
    /// RoomManager 에서 호출
    /// </summary>
    public IEnumerator Transition(System.Action onMidpoint)
    {
        // 페이드 아웃 (0 → 1)
        yield return Fade(0f, 1f);

        // 완전히 어두워진 시점에 방 전환
        onMidpoint?.Invoke();

        // 페이드 인 (1 → 0)
        yield return Fade(1f, 0f);
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0 이어도 작동
            SetAlpha(Mathf.Lerp(from, to, elapsed / fadeDuration));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;

        // 알파가 0(완전 투명)일 때는 레이캐스트를 막지 않아야 평소에 UI 클릭이 정상 동작한다.
        // 실제로 화면을 덮고 있는 동안(페이드 중 / 암전 상태)에만 입력을 막는다.
        fadeImage.raycastTarget = a > 0.01f;
    }
}