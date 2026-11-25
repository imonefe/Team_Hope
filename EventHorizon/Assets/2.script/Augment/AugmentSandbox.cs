using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 연습용 증강 칸 — 튜토리얼 증강 체험에서 얻은 증강을 본 게임으로 가져가지 않게 한다.
/// Begin() 때의 능력치 · 인벤토리 · 붙은 효과를 기억했다가 End() 때 그 뒤에 얻은 증강을 모두 빼고 되돌린다.
/// </summary>
public static class AugmentSandbox
{
    static bool active;
    static int invCount;
    static readonly HashSet<Component> comps = new HashSet<Component>();
    static PlayerController pc;
    static float atk, def, spd, itemSpd, pause, cc, cm;
    static int maxHp, hp;

    public static bool Active => active;

    /// <summary>Begin 뒤에 얻은 증강 수</summary>
    public static int GainedCount => active && Inventory.Instance != null ? Mathf.Max(0, Inventory.Instance.passives.Count - invCount) : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { active = false; comps.Clear(); pc = null; }

    public static void Begin()
    {
        pc = PlayerController.Instance;
        if (pc == null || Inventory.Instance == null) return;
        var st = pc.GetComponent<Stats>();
        if (st == null) return;
        active = true;
        invCount = Inventory.Instance.passives.Count;
        comps.Clear();
        foreach (var c in pc.GetComponents<Component>()) comps.Add(c);
        atk = st.Atk; def = st.Def; spd = st.spd; itemSpd = st.itemSpdBonus; pause = st.PauseDuration;
        cc = st.critChance; cm = st.critMultiplier; maxHp = st.maxHp; hp = st.currentHp;
    }

    /// <summary>[2026-11-25] 연습 끝 — 얻은 증강을 되돌리지 않고 그대로 가져감 (튜토리얼 증강은 본 게임으로 가져가기로 결정)</summary>
    public static void Keep()
    {
        active = false;
        comps.Clear();
        pc = null;
    }

    public static void End()
    {
        if (!active) return;
        active = false;
        var inv = Inventory.Instance;
        if (inv != null)
        {
            for (int i = inv.passives.Count - 1; i >= invCount; i--)
            {
                var d = inv.passives[i];
                inv.RemovePassiveAt(i, d);
                RunManager.Instance?.ForgetObtained(d);
            }
        }
        if (pc != null)
        {
            foreach (var c in pc.GetComponents<Component>()) if (!comps.Contains(c)) Object.Destroy(c);
            var st = pc.GetComponent<Stats>();
            if (st != null)
            {
                st.Atk = atk; st.Def = def; st.spd = spd; st.itemSpdBonus = itemSpd; st.PauseDuration = pause;
                st.critChance = cc; st.critMultiplier = cm; st.maxHp = maxHp; st.currentHp = Mathf.Min(hp, maxHp);
            }
        }
        comps.Clear();
        AugmentRuntime.Instance?.Refresh();
    }
}
