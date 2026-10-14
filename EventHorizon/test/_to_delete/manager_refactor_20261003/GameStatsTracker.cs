using UnityEngine;

/// <summary>
/// 게임오버 화면에 표시할 이번 런의 통계(플레이 타임 / 처치한 적·보스 수)를 추적한다.
/// ItemManager와 같은 패턴(PersistentSingleton)이라 방을 넘나들어도 값이 유지된다.
/// 스테이지 진행도는 MapManager.CurrentStageIndex를 그대로 가져다 쓰므로 여기서 따로 관리하지 않는다.
/// </summary>
public class GameStatsTracker : PersistentSingleton<GameStatsTracker>
{
    public int EnemiesKilled { get; private set; }
    public int BossesKilled { get; private set; }

    float runStartTime;

    void Awake()
    {
        InitSingleton();
        runStartTime = Time.time;
    }

    /// <summary>이번 런 시작부터 지금까지 흐른 시간(초). Time.timeScale=0(메뉴/일시정지) 동안은 흐르지 않는다.</summary>
    public float GetPlayTime()
    {
        return Mathf.Max(0f, Time.time - runStartTime);
    }

    public void RegisterEnemyKilled() { EnemiesKilled++; }
    public void RegisterBossKilled() { BossesKilled++; }

    /// <summary>다시하기(런 재시작) 시 호출.</summary>
    public void ResetRun()
    {
        EnemiesKilled = 0;
        BossesKilled = 0;
        runStartTime = Time.time;
    }

    /// <summary>초 단위 값을 "12:34" 형식 문자열로 변환.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        int m = total / 60;
        int s = total % 60;
        return $"{m:00}:{s:00}";
    }
}
