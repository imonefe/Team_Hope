using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이번 판(런) 동안만 유지되는 데이터를 한 곳에서 관리한다.
/// 예전의 ItemManager + CurrencyManager + GameStatsTracker 를 하나로 합친 것.
///
///  - 아이템 : 이미 얻은 아이템(재스폰 방지), 스택 효과의 현재 스택 수
///  - 재화   : 현재 재화, 처치 시 랜덤 지급 + "+N" 팝업
///  - 통계   : 플레이 타임, 처치한 적/보스 수 (게임오버 화면용)
///
/// 판을 다시 시작할 때는 RunManager.RestartRun() 하나만 부르면
/// 이 데이터 + 맵 진행도를 모두 초기화하고 씬을 다시 불러온다.
/// 새 "판 데이터"를 추가할 때는 아래 ResetRun()에 초기화 한 줄만 넣으면 된다.
/// </summary>
// DefaultExecutionOrder(-1000): 이 싱글턴의 Awake()(→ Instance 세팅)가 씬의 다른 모든
// 오브젝트의 Awake/OnEnable보다 반드시 먼저 실행되도록 강제한다.
[DefaultExecutionOrder(-1000)]
public class RunManager : PersistentSingleton<RunManager>
{
    // ───────── 아이템 ─────────

    // 획득한 아이템 목록
    readonly HashSet<ItemData> obtainedItems = new HashSet<ItemData>();

    // [2026-10-10 추가] 지금 확보돼 있는 아이템 — 바닥에 떨어져 있거나(아직 안 주움), 아직 안 연 상자가 미리 정해둔 것.
    // 이런 아이템은 다른 상자가 또 뽑지 않는다 (같은 아이템이 두 개 나와 하나가 줍는 순간 사라지던 문제 방지).
    // 누가 확보했는지(holder)까지 기억해서, 확보한 쪽이 사라지면 자동으로 풀린다.
    readonly Dictionary<ItemData, HashSet<Object>> itemHolders = new Dictionary<ItemData, HashSet<Object>>();

    // 스택 효과 현재 스택 수 (ScriptableObject 직접 수정 불가 → 여기서 관리)
    readonly Dictionary<StackEffect, int> stackCounts = new Dictionary<StackEffect, int>();

    // ───────── 재화 ─────────

    public int Amount { get; private set; }

    /// <summary>재화 값이 바뀔 때마다 호출 (새 총액 전달). 재화 UI가 구독.</summary>
    public event System.Action<int> OnCurrencyChanged;

    [Header("재화 획득 팝업 색")]
    public Color popupColor = new Color(1f, 0.85f, 0.2f, 1f); // 금색

    // ───────── 통계 ─────────

    public int EnemiesKilled { get; private set; }
    public int BossesKilled { get; private set; }

    float runStartTime;

    // ───────── 초기화 ─────────

    void Awake()
    {
        if (!InitSingleton()) return;
        runStartTime = Time.time;
    }

    void Start()
    {
        // UI가 이 시점에 이미 구독돼 있을 수 있으니 재화 초기값도 한 번 알려준다
        OnCurrencyChanged?.Invoke(Amount);
    }

    // ══════════════ 아이템 ══════════════

    public bool CanSpawn(ItemData data)
    {
        if (data == null) return false;
        return !obtainedItems.Contains(data);
    }

    public void RegisterObtained(ItemData data)
    {
        if (data == null) return;
        obtainedItems.Add(data);
    }

    /// <summary>이번 판에 이미 얻었는지</summary>
    public bool IsObtained(ItemData data) => data != null && obtainedItems.Contains(data);

    /// <summary>holder(바닥의 아이템, 상자 등)가 이 아이템을 확보 — 다른 상자가 같은 아이템을 뽑지 않게</summary>
    public void HoldItem(ItemData data, Object holder)
    {
        if (data == null || holder == null) return;
        if (!itemHolders.TryGetValue(data, out var set))
        {
            set = new HashSet<Object>();
            itemHolders[data] = set;
        }
        set.Add(holder);
    }

    /// <summary>holder가 확보를 해제 (주웠거나, 사라졌거나, 상자가 열렸을 때)</summary>
    public void ReleaseItem(ItemData data, Object holder)
    {
        if (data == null) return;
        if (itemHolders.TryGetValue(data, out var set))
        {
            set.Remove(holder);
            if (set.Count == 0) itemHolders.Remove(data);
        }
    }

    bool IsHeld(ItemData data)
    {
        if (!itemHolders.TryGetValue(data, out var set)) return false;
        set.RemoveWhere(h => h == null); // 파괴된 쪽은 정리
        return set.Count > 0;
    }

    /// <summary>
    /// 플레이어에게 "새" 아이템인지 — 이번 판에 아직 안 얻었고, 바닥이나 다른 상자에 이미 확보돼 있지도 않음.
    /// 상자가 내용물을 고를 때 이 기준을 쓴다.
    /// </summary>
    public bool IsNewItem(ItemData data) => data != null && !obtainedItems.Contains(data) && !IsHeld(data);

    public int GetStack(StackEffect effect)
    {
        return stackCounts.TryGetValue(effect, out int count) ? count : 0;
    }

    public bool TryAddStack(StackEffect effect, int maxStack)
    {
        int current = GetStack(effect);
        if (current >= maxStack) return false;

        stackCounts[effect] = current + 1;
        return true;
    }

    // ══════════════ 재화 ══════════════

    public void AddCurrency(int amount)
    {
        if (amount == 0) return;
        Amount = Mathf.Max(0, Amount + amount);
        OnCurrencyChanged?.Invoke(Amount);
    }

    /// <summary>적/보스 처치 위치에서 min~max(포함) 사이 랜덤 재화를 지급하고 노란 파편 연출을 띄운다.</summary>
    public void DropRandomCurrency(Vector3 position, int min, int max)
    {
        if (max < min) max = min;
        int amount = Random.Range(min, max + 1);
        if (amount <= 0) return;

        AddCurrency(amount);
        // [2026-10-27 변경] "+N" 숫자 팝업 대신 노란 파편이 바닥에 떨어지는 연출 (CurrencyShardFX.cs)
        CurrencyShardFX.Spawn(position, amount);
    }

    // ══════════════ 통계 ══════════════

    /// <summary>이번 런 시작부터 지금까지 흐른 시간(초). Time.timeScale=0(메뉴/일시정지) 동안은 흐르지 않는다.</summary>
    public float GetPlayTime()
    {
        return Mathf.Max(0f, Time.time - runStartTime);
    }

    public void RegisterEnemyKilled() { EnemiesKilled++; }
    public void RegisterBossKilled() { BossesKilled++; }

    /// <summary>초 단위 값을 "12:34" 형식 문자열로 변환.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        int m = total / 60;
        int s = total % 60;
        return $"{m:00}:{s:00}";
    }

    // ══════════════ 판 초기화 / 재시작 ══════════════

    /// <summary>이번 판 데이터를 전부 처음 상태로 되돌린다.</summary>
    public void ResetRun()
    {
        obtainedItems.Clear();
        itemHolders.Clear();
        stackCounts.Clear();

        Amount = 0;
        OnCurrencyChanged?.Invoke(Amount);

        EnemiesKilled = 0;
        BossesKilled = 0;
        runStartTime = Time.time;
    }

    /// <summary>
    /// 판 재시작의 유일한 진입점. 게임오버 "다시하기", 일시정지 메뉴 "새 게임" 등에서 호출.
    /// 판 데이터 + 맵 진행도를 초기화하고, 전역 상태(시간 정지, 전투 UI 잠금)를 풀고 씬을 다시 불러온다.
    /// </summary>
    public static void RestartRun()
    {
        if (Instance != null) Instance.ResetRun();
        MapManager.Instance?.ResetRun();

        // 전역 상태라 씬을 다시 불러와도 저절로 원래대로 돌아오지 않는다
        GamePause.Clear();
        CombatUIGate.Unblock();

        // [2026-10-10 추가] Q 퍼즈 상태·게이지는 씬을 넘어 유지되는 PauseManager에 있어서 새 판까지 남았다
        PauseManager.Instance?.ResetForNewRun();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// [2026-10-21 추가] 판을 처음 상태로 되돌리고 인트로(첫 화면) 씬으로 돌아간다.
    /// 오래 입력이 없을 때 IdleResetWatcher 가 부른다. 초기화 내용은 RestartRun 과 같다.
    /// </summary>
    public static void ReturnToIntro(string introSceneName = "intro")
    {
        if (Instance != null) Instance.ResetRun();
        MapManager.Instance?.ResetRun();

        GamePause.Clear();
        CombatUIGate.Unblock();
        PauseManager.Instance?.ResetForNewRun();

        SceneManager.LoadScene(introSceneName);
    }
}
