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

    void Awake()
    {
        // 중복이면 스스로 파괴, 아니면 유일한 인스턴스로 등록 + 씬 전환에도 유지 (PersistentSingleton)
        if (!InitSingleton()) return;

        // 시작 시 투명하게
        SetAlpha(0f);
        gameObject.SetActive(true);

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