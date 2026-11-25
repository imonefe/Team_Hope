using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-25 추가] GEEKS 2026 특전 창 — 로비 프롤로그(스토리)가 끝나면 무작위 사이버웨어 5개를 주고 이 창으로 알린다.
///
/// - 창 모양은 프리팹(1.Prefab/UI/Canvas/특전/Resources/GeeksPerkCanvas)에 있다. 에디터 도구 GeeksPerkCanvasBuilder 가 만든다.
/// - 설정 창의 'GEEKS 2026 특전' 스위치(GameSettings.GeeksPerk)를 끄면 주지 않는다.
/// - 한 판에 한 번 (RunManager 기록 "Perk.GEEKS2026").
/// - 창이 떠 있는 동안 게임은 멈추고, 카드에 마우스를 올리면 사이버웨어 설명이 나온다.
/// - Space · Enter · 클릭으로 닫는다 (뜬 뒤 closeDelay 초가 지나야 받음).
/// 부르는 곳: LobbyIntroDialogue (프롤로그 대화가 끝난 뒤, 조작법 창 전에).
/// </summary>
public class GeeksPerkWindow : MonoBehaviour
{
    public const string ResourcePath = "GeeksPerkCanvas";
    public const int Count = 5;
    const string RunRecord = "Perk.GEEKS2026";

    [System.Serializable]
    public class Card
    {
        public RectTransform root;
        public Image frame;
        public Image icon;
        public TMP_Text name;
        public TMP_Text tier;
        public TMP_Text keywords;
    }

    [Header("칸 (GeeksPerkCanvasBuilder 가 연결)")]
    public CanvasGroup group;
    public RectTransform panel;
    public Card[] cards = new Card[Count];

    [Header("연출")]
    [Tooltip("창이 뜬 뒤 이 시간(초)이 지나야 닫기 입력을 받음 — 대화를 넘기던 키에 바로 닫히지 않게")]
    public float closeDelay = 0.6f;
    public float fadeTime = 0.25f;
    [Tooltip("카드가 하나씩 나타나는 간격(초)")]
    public float cardInterval = 0.08f;

    bool showing;
    float shownAt;
    int hover = -1;
    List<ItemData> items;
    PlayerController lockedPlayer;

    /// <summary>특전을 주고 창을 띄운 뒤, 닫힐 때까지 기다린다. 설정이 꺼져 있거나 이미 받았으면 바로 끝남.</summary>
    public static IEnumerator GrantAndShow()
    {
        if (!GameSettings.GeeksPerk) yield break;
        var rm = RunManager.Instance;
        if (rm != null && rm.GetUpgradeLevel(RunRecord) > 0) yield break;
        var window = AugmentOfferDirector.Instance != null ? AugmentOfferDirector.Instance.window : null;
        if (window == null) yield break;

        var prefab = Resources.Load<GeeksPerkWindow>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("[GEEKS 특전] 프리팹이 없습니다 — 메뉴 EventHorizon/GEEKS 2026 특전 창 만들기"); yield break; }

        var got = window.GrantRandom(Count);
        if (got.Count == 0) yield break;
        if (rm != null) rm.SetUpgradeLevel(RunRecord, 1);

        var inst = Instantiate(prefab);
        inst.Show(got);
        while (inst != null && inst.showing) yield return null;
    }

    void Show(List<ItemData> got)
    {
        items = got;
        showing = true;
        shownAt = Time.unscaledTime;
        GamePause.Push(this);
        CombatUIGate.Block();
        lockedPlayer = PlayerController.Instance;
        if (lockedPlayer != null) lockedPlayer.LockControl(this);

        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            bool on = i < got.Count;
            c.root.gameObject.SetActive(on);
            if (!on) continue;
            var d = got[i];
            Color tc = ShopColors.Tier(d.tier);
            if (c.frame != null) c.frame.color = tc;
            if (c.icon != null) { c.icon.sprite = d.icon; c.icon.enabled = d.icon != null; c.icon.preserveAspect = true; }
            if (c.name != null) c.name.text = d.itemName;
            if (c.tier != null) { c.tier.text = AugmentRules.TierName(d.tier); c.tier.color = tc; }
            if (c.keywords != null) c.keywords.text = KeywordLine(d);

            // 하나씩 아래에서 떠오르며 나타남
            var cg = c.root.GetComponent<CanvasGroup>() ?? c.root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            Vector2 rest = c.root.anchoredPosition;
            c.root.anchoredPosition = rest + new Vector2(0f, -30f);
            float delay = fadeTime * 0.5f + i * cardInterval;
            cg.DOFade(1f, 0.3f).SetDelay(delay).SetUpdate(true);
            c.root.DOAnchorPos(rest, 0.35f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (group != null) { group.alpha = 0f; group.DOFade(1f, fadeTime).SetUpdate(true); }
        if (panel != null) { panel.localScale = Vector3.one * 0.95f; panel.DOScale(1f, fadeTime).SetEase(Ease.OutCubic).SetUpdate(true); }
    }

    static string KeywordLine(ItemData d)
    {
        if (d.keywords == null || d.keywords.Length == 0) return "";
        var parts = new List<string>();
        foreach (var k in d.keywords) parts.Add($"<color=#{AugmentRules.KeywordHex(k)}>{AugmentRules.KeywordName(k)}</color>");
        return string.Join(" · ", parts);
    }

    void Update()
    {
        if (!showing) return;
        UpdateHover();
        if (Time.unscaledTime - shownAt < closeDelay) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0))
            Close();
    }

    // 카드에 마우스를 올리면 사이버웨어 설명
    void UpdateHover()
    {
        int over = -1;
        for (int i = 0; i < cards.Length && items != null && i < items.Count; i++)
            if (cards[i] != null && cards[i].root != null && UIUtil.MouseOver(cards[i].root)) { over = i; break; }
        if (over == hover) return;
        hover = over;
        if (over >= 0) ItemInfoPopup.Instance?.Show(items[over], Input.mousePosition);
        else ItemInfoPopup.Instance?.Hide();
    }

    void Close()
    {
        if (!showing) return;
        showing = false;
        if (hover >= 0) { hover = -1; ItemInfoPopup.Instance?.Hide(); }
        GamePause.Pop(this);
        CombatUIGate.Unblock();
        if (lockedPlayer != null) lockedPlayer.ResumeControl(this);
        lockedPlayer = null;
        YoungbokChatter.OnItemPicked();   // 처음 얻은 물건이면 "사이버웨어는 [I] 누르면…" 안내
        if (group != null) group.DOFade(0f, fadeTime).SetUpdate(true).OnComplete(() => Destroy(gameObject));
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (!showing) return;   // 창이 떠 있는 채로 사라지면 멈춤 · 잠금을 풀어 둠
        showing = false;
        GamePause.Pop(this);
        CombatUIGate.Unblock();
        if (lockedPlayer != null) lockedPlayer.ResumeControl(this);
    }
}
