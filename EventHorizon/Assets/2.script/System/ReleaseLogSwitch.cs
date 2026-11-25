using UnityEngine;

/// <summary>
/// [2026-11-25 추가] 정식 빌드(개발 빌드 아님)에서는 Debug.Log 출력을 끈다.
/// 웹 빌드는 로그 한 줄마다 브라우저 콘솔에 글자를 쓰느라 느려진다. 특히 피격마다 찍히는 피해 로그(Stats)가 전투 중 부담이 컸음.
/// - 에디터 · 개발 빌드(Development Build 체크)에서는 그대로 나온다.
/// - 오류(Error · Exception)는 정식 빌드에서도 남긴다 — 문제가 생겼을 때 원인을 볼 수 있게.
/// 씬에 둘 필요 없음 (게임 시작 시 자동 실행).
/// </summary>
public static class ReleaseLogSwitch
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (Application.isEditor || Debug.isDebugBuild) return;
        Debug.unityLogger.filterLogType = LogType.Error;   // Log · Warning 은 끄고 Error · Assert · Exception 만
    }
}
