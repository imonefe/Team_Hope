using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전투 UI(배틀 캔버스 등)를 잠시 꺼야 하는 상황(인벤토리, 대화 등)이 여러 곳에서 동시에
/// 겹칠 수 있어서, 단순 bool이 아니라 카운터로 관리한다.
/// Block()을 호출한 횟수만큼 Unblock()이 불려야 다시 켜진다 — 예: 인벤토리를 연 채로
/// NPC와 대화가 시작돼도, 대화가 먼저 끝나더라도 인벤토리가 열려있는 동안은 계속 잠긴 상태를 유지한다.
///
/// 사용법: 전투 UI를 껐다 켰다 하고 싶은 쪽(Inventory.cs, Npc.cs 등)에서
///   열 때 : CombatUIGate.Block();
///   닫을 때: CombatUIGate.Unblock();
/// 만 호출하면 된다. 실제로 전투 UI 오브젝트를 켜고 끄는 일은 CombatUIVisibility.cs가
/// OnChanged 이벤트를 구독해서 처리하므로, 이 클래스와 전투 UI는 서로 몰라도 된다.
/// </summary>
public static class CombatUIGate
{
    static int blockCount = 0;

    public static bool IsBlocked => blockCount > 0;

    public static event System.Action OnChanged;

    // [2026-09-29 추가] 씬이 완전히 새로 로드되면(재시작 등) blockCount를 무조건 0으로 리셋한다.
    // 인벤토리/NPC 대화 등이 Close() 없이 씬 전환으로 그냥 파괴되면 자기 몫의 Unblock()을
    // 영영 호출 못 해서 blockCount가 어긋난 채로 남을 수 있는데(전투 UI가 계속 숨겨짐),
    // 새 씬에서는 어차피 열려있는 UI가 하나도 없는 게 정상이므로 씬 로드 시점에 강제로
    // 맞춰주는 게 Block/Unblock 호출 쌍을 일일이 맞추는 것보다 안전하다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnSceneLoad()
    {
        blockCount = 0;
        SceneManager.sceneLoaded -= HandleSceneLoaded; // 도메인 리로드 없는 에디터 재실행 대비 중복 구독 방지
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return; // Additive 로드는 기존 씬이 안 죽으므로 건드리지 않음
        if (blockCount == 0) return;
        blockCount = 0;
        OnChanged?.Invoke();
    }

    public static void Block()
    {
        blockCount++;
        if (blockCount == 1)
            OnChanged?.Invoke();
    }

    public static void Unblock()
    {
        if (blockCount <= 0) return;
        blockCount--;
        if (blockCount == 0)
            OnChanged?.Invoke();
    }
}
