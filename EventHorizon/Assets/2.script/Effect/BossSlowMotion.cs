using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-11-22 추가] 보스급(중간 보스 · 보스) 처치 연출 — 게임 속도를 30%로 떨어뜨렸다가 1.2초(실제 시간)에 걸쳐 원래대로.
/// BossController.OnTrueDeath() 가 Play() 를 부른다.
/// - 메뉴 · 인벤토리 등 창이 시간을 멈춘 동안(GamePause)은 속도를 건드리지 않고 기다린다.
/// - 씬이 바뀌거나 중간에 다시 불리면 처음부터 다시.
/// </summary>
public class BossSlowMotion : MonoBehaviour
{
    public const float StartScale = 0.3f;
    public const float RecoverSeconds = 1.2f;

    static BossSlowMotion runner;
    Coroutine playing;

    public static void Play()
    {
        if (runner == null)
        {
            var go = new GameObject("[BossSlowMotion]");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<BossSlowMotion>();
        }
        if (runner.playing != null) runner.StopCoroutine(runner.playing);
        runner.playing = runner.StartCoroutine(runner.Run());
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < RecoverSeconds)
        {
            if (!GamePause.IsPaused)
            {
                float k = t / RecoverSeconds;
                Time.timeScale = Mathf.Lerp(StartScale, 1f, k * k);   // 처음엔 느리게 머물다 빨리 돌아옴
                Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime * Time.timeScale;
                t += Time.unscaledDeltaTime;
            }
            yield return null;
        }
        if (!GamePause.IsPaused) Time.timeScale = 1f;
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime;
        playing = null;
    }

    void OnDestroy()
    {
        if (runner == this) runner = null;
    }
}
