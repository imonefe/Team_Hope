using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [2026-10-10 추가] 창(ESC 메뉴, 인벤토리, 노드맵, 게임오버)이 게임 시간을 멈추는 것을 한곳에서 관리한다.
///
/// 예전에는 창마다 Time.timeScale 에 0/1 을 직접 써서, 두 창이 겹치면 먼저 닫힌 창이 1 을 써버려
/// 아직 열려 있는 창 뒤에서 게임이 다시 흘러갔다(예: 노드맵을 연 채 인벤토리를 열었다 닫으면 게임 재개).
/// 이제는 "누가 멈췄는지"를 기억해 두고, 멈춘 쪽이 전부 풀려야만 시간이 다시 흐른다.
///
/// 사용법 (창을 여는 쪽):
///   열 때 : GamePause.Push(GamePause.Inventory);
///   닫을 때: GamePause.Pop(GamePause.Inventory);
/// 같은 이름으로 두 번 Push 해도 한 번으로 친다. Time.timeScale 을 직접 쓰지 말 것.
///
/// 씬이 새로 로드되면(재시작 등) 기록을 모두 지우고 시간을 원래대로 돌린다.
/// (Q 퍼즈는 시간 자체를 멈추지 않고 적이 각자 멈추는 방식이라 여기와 무관하다)
/// </summary>
public static class GamePause
{
    public const string Menu = "Menu";           // ESC 메뉴
    public const string Inventory = "Inventory"; // 인벤토리
    public const string Map = "Map";             // 노드맵(분기 선택)
    public const string GameOver = "GameOver";   // 게임오버 화면

    static readonly HashSet<object> owners = new HashSet<object>();
    static float defaultFixedDeltaTime = 0.02f;

    /// <summary>창 때문에 게임 시간이 멈춰 있는지</summary>
    public static bool IsPaused => owners.Count > 0;

    /// <summary>특정 창이 지금 시간을 멈추고 있는지 (예: GamePause.Has(GamePause.GameOver))</summary>
    public static bool Has(object owner) => owner != null && owners.Contains(owner);

    /// <summary>프로젝트 설정의 고정 물리 간격 (슬로모션 등에서 원래대로 돌릴 때 사용)</summary>
    public static float DefaultFixedDeltaTime => defaultFixedDeltaTime;

    public static event System.Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        owners.Clear();
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        SceneManager.sceneLoaded -= HandleSceneLoaded; // 도메인 리로드 없는 에디터 재실행 대비
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        Clear();
    }

    public static void Push(object owner)
    {
        if (owner == null) return;
        if (owners.Add(owner)) Apply();
    }

    public static void Pop(object owner)
    {
        if (owner == null) return;
        if (owners.Remove(owner)) Apply();
    }

    /// <summary>모든 정지 해제 (재시작·게임 종료 등)</summary>
    public static void Clear()
    {
        owners.Clear();
        Apply();
    }

    static void Apply()
    {
        if (owners.Count > 0)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }
        OnChanged?.Invoke();
    }
}
