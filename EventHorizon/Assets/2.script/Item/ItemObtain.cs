using System;
using UnityEngine;

/// <summary>
/// [2026-11-26] 사이버웨어 획득 공통 입구 (바닥 아이템 · 증강 고르기 · 상점 · GEEKS 특전).
///  - 이미 가진 것   → 칸을 쓰지 않고 강화 단계 +1 (최대 단계면 판매가만큼 파편)
///  - 칸이 남음      → 바로 얻음 (효과 적용 · 인벤토리 · 획득 기록)
///  - 8칸이 가득 참  → 교체 창 (ItemSwapWindow): 하나를 버리고(파편으로) 얻거나, 새 것을 포기
///  효과는 실제로 얻는 순간에만 적용한다 (포기하면 아무것도 바뀌지 않음).
/// </summary>
public static class ItemObtain
{
    public enum Result { Granted, LeveledUp, Declined }

    public static bool Owns(ItemData d) => d != null && Inventory.Instance != null && Inventory.Instance.passives.Contains(d);

    /// <param name="player">효과를 받을 캐릭터 (비우면 지금 캐릭터)</param>
    /// <param name="done">끝났을 때 결과 — 교체 창을 거치면 창이 닫힌 뒤에 불림</param>
    /// <param name="cancelReturns">[21:58] 교체 창에서 Esc 가 '포기'가 아니라 '이전 화면으로 돌아가기' (증강 고르기 창). 결과는 똑같이 Declined — 부른 쪽이 창을 다시 보여 줌</param>
    public static void Request(ItemData d, GameObject player, Action<Result> done, bool cancelReturns = false)
    {
        if (d == null) { done?.Invoke(Result.Declined); return; }
        if (Owns(d)) { LevelUp(d, player); done?.Invoke(Result.LeveledUp); return; }
        var inv = Inventory.Instance;
        if (inv == null || !inv.IsFull) { Grant(d, player); done?.Invoke(Result.Granted); return; }

        ItemSwapWindow.Ask(d,
            index =>
            {
                Discard(index, player);
                Grant(d, player);
                done?.Invoke(Result.Granted);
            },
            () => done?.Invoke(Result.Declined),
            cancelReturns);
    }

    /// <summary>칸 검사 없이 얻는다 (효과 적용 → 인벤토리 → 획득 기록). 예전 각 경로의 처리와 같음</summary>
    public static void Grant(ItemData d, GameObject player = null)
    {
        if (d == null) return;
        var go = player != null ? player : CurrentPlayer();
        var stats = go != null ? go.GetComponent<Stats>() : null;
        if (stats != null && d.effects != null)
            foreach (var e in d.effects)
                if (e != null) e.Apply(stats, go.GetComponent<MonoBehaviour>());
        Inventory.Instance?.AddPassive(d);
        RunManager.Instance?.RegisterObtained(d);
    }

    /// <summary>index 칸의 사이버웨어를 버린다 — 상점 판매와 같은 처리 (효과 되돌림 · 획득 기록 지움 · 판매가만큼 파편)</summary>
    public static int Discard(int index, GameObject player = null)
    {
        var inv = Inventory.Instance;
        if (inv == null || index < 0 || index >= inv.passives.Count) return 0;
        var d = inv.passives[index];
        if (d == null) return 0;
        int level = Level(d);
        int shards = AugmentRules.SellShards(d.tier, level);
        if (!inv.RemovePassiveAt(index, d)) return 0;
        var go = player != null ? player : CurrentPlayer();
        var stats = go != null ? go.GetComponent<Stats>() : null;
        if (stats != null && d.effects != null)
            foreach (var e in d.effects)
                if (e != null) e.Revert(stats, go.GetComponent<MonoBehaviour>(), level);
        var rm = RunManager.Instance;
        rm?.ForgetObtained(d);
        if (rm != null && shards > 0)
        {
            rm.AddShards(shards);
            // [17:31] 판매가만큼 파편 — 떨어지는 보랏빛 파편 연출 + 한 줄 안내 (게임이 멈춰 있어도 창이 닫히면 보임)
            if (go != null) CurrencyShardFX.Spawn(go.transform.position, shards * rm.shardFxPerShard, rm.shardColorA, rm.shardColorB);
            ScreenNotice.Show($"{d.itemName} → 모듈 조각 +{shards}");
        }
        AugmentRuntime.Instance?.Refresh();
        return shards;
    }

    /// <summary>같은 것을 또 얻음 → 강화 단계 +1. 이미 최대면 판매가만큼 파편</summary>
    static void LevelUp(ItemData d, GameObject player)
    {
        var rm = RunManager.Instance;
        if (rm == null) return;
        int lv = Level(d);
        if (lv >= AugmentRules.MaxLevel) { rm.AddShards(AugmentRules.SellShards(d.tier, lv)); return; }
        rm.SetAugmentLevel(d, lv + 1);
        // 강화 담당에게 올린 것과 같은 알림 (AugmentUpgradeWindow.Upgrade 참고)
        var go = player != null ? player : CurrentPlayer();
        var stats = go != null ? go.GetComponent<Stats>() : null;
        var runner = go != null ? go.GetComponent<MonoBehaviour>() : null;
        if (d.effects != null)
            foreach (var e in d.effects)
                if (e is IAugmentLevelEffect le) le.OnAugmentLevelChanged(stats, runner, lv, lv + 1);
        Inventory.Instance?.NotifyChanged();
    }

    public static int Level(ItemData d) => RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(d)) : 1;

    /// <summary>판매가 (버렸을 때 받는 파편)</summary>
    public static int DiscardShards(ItemData d) => d == null ? 0 : AugmentRules.SellShards(d.tier, Level(d));

    static GameObject CurrentPlayer()
    {
        var cm = CharacterManager.Instance;
        GameObject go = cm != null ? cm.CurrentCharacter : null;
        if (go == null && PlayerController.Instance != null) go = PlayerController.Instance.gameObject;
        return go;
    }
}
