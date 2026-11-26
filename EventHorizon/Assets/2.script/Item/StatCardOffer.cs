using System.Collections.Generic;
using UnityEngine;

/// <summary>[2026-11-26] 다음 방에서 받을 보상 (문 선택에서 정함 → 방에 들어설 때 AugmentOfferDirector 가 읽음)</summary>
public enum RoomRewardKind { Augment, Stat }

public static class RoomReward
{
    /// <summary>다음에 들어갈 전투방 보상. 문 선택을 거치지 않으면(예전 노드 지도 · 튜토리얼) 사이버웨어</summary>
    public static RoomRewardKind Next = RoomRewardKind.Augment;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Next = RoomRewardKind.Augment;

    /// <summary>읽고 나면 기본값(사이버웨어)으로 되돌림</summary>
    public static RoomRewardKind Take()
    {
        var k = Next;
        Next = RoomRewardKind.Augment;
        return k;
    }
}

/// <summary>
/// [2026-11-26] 스탯 카드 고르기 — 증강 고르기 창(AugmentOfferWindow)의 일반 카드 모드로 3장을 띄우고, 고른 카드의 스탯을 바로 적용.
/// </summary>
public static class StatCardOffer
{
    public const string Title = "스탯 카드";
    public const string Subtitle = "스탯 방 보상 · 하나를 고르세요";

    /// <summary>창을 열었으면 true (후보가 없거나 창이 없으면 false)</summary>
    public static bool Open(AugmentOfferWindow window)
    {
        var pool = StatCardPool.Load();
        if (window == null || pool == null)
        {
            if (pool == null) Debug.LogWarning("[스탯 카드] Resources/StatCardPool 이 없습니다 — 메뉴 EventHorizon/스탯 카드 기본 세트 만들기");
            return false;
        }
        var picks = pool.Pick(AugmentOfferWindow.CardCount);
        if (picks.Count == 0) return false;
        var views = new List<AugmentOfferWindow.CardView>();
        foreach (var c in picks)
            views.Add(new AugmentOfferWindow.CardView
            {
                header = Spaced(AugmentRules.TierName(c.tier)),
                color = ShopColors.Tier(c.tier),
                icon = c.icon,
                name = c.cardName,
                body = c.BodyText(),
                footerLeft = c.IsTradeoff ? StatCardData.TradeoffMark : "",
                footerRight = "칸을 쓰지 않음"
            });
        return window.OfferCustom(Title, Subtitle, views, i =>
        {
            if (i < 0 || i >= picks.Count) return;
            Apply(picks[i]);
        });
    }

    public static void Apply(StatCardData c)
    {
        if (c == null) return;
        var cm = CharacterManager.Instance;
        GameObject go = cm != null ? cm.CurrentCharacter : null;
        if (go == null && PlayerController.Instance != null) go = PlayerController.Instance.gameObject;
        var stats = go != null ? go.GetComponent<Stats>() : null;
        if (stats == null) return;
        // 최대 체력은 따로: 줄어드는 카드(대가)에서 IncreaseMaxHp(음수)를 쓰면 지금 체력까지 깎여 0 으로 살아남을 수 있음
        var m = c.modifier;
        int hpDelta = m.hp + Mathf.RoundToInt(stats.maxHp * m.hpPercent);
        m.hp = 0; m.hpPercent = 0f;
        m.Apply(stats);
        if (hpDelta > 0) stats.IncreaseMaxHp(hpDelta);
        else if (hpDelta < 0) stats.DecreaseMaxHp(-hpDelta);
        ScreenNotice.Show(c.cardName);
    }

    static string Spaced(string s) => string.Join(" ", s.ToCharArray());
}
