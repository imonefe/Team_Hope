using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// battle Canvas 한쪽에 붙어서 현재 보유 재화를 표시하는 간단한 UI.
/// RunManager.OnCurrencyChanged를 구독해서 값이 바뀔 때마다 자동 갱신된다.
///
/// OnEnable()이 RunManager.Awake()보다 먼저 실행되는 경우(스크립트 실행 순서가
/// 보장되지 않으므로 실제로 발생할 수 있음) RunManager.Instance가 아직 null이라
/// 구독 자체가 누락될 수 있다. 그래서 모든 오브젝트의 Awake()가 끝난 뒤 실행이
/// 보장되는 Start()에서 한 번 더 구독을 시도해서 이 문제를 피한다.
/// </summary>
public class CurrencyUI : MonoBehaviour
{
    // [2026-11-21 추가] 어떤 재화를 보여줄지 — 일반 재화 / 증강 파편 (인벤토리에서 아이콘과 함께 한 줄씩)
    public enum Kind { Currency = 0, Shards = 1 }
    [Tooltip("Currency = 일반 재화, Shards = 증강 파편")]
    public Kind kind = Kind.Currency;

    public TMP_Text amountText;
    [Tooltip("숫자 앞에 붙일 텍스트 (필요 없으면 비워둠)")]
    public string prefix = "";

    [Header("[2026-11-25] 마우스를 올리면 이름 · 짧은 설명 (인벤토리)")]
    public bool hoverInfo = true;
    public string currencyTitle = "재화";
    [TextArea] public string currencyDesc = "적과 상자에서 얻습니다. 상점에서 물건을 살 때 씁니다.";
    public string shardTitle = "사이버웨어 파편";
    [TextArea] public string shardDesc = "보스 · 정예 적에게서 얻습니다. 상점에서 가진 사이버웨어를 강화할 때 씁니다.";

    bool subscribed = false;
    bool hovering;

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (RunManager.Instance == null) return;

        if (kind == Kind.Shards) RunManager.Instance.OnShardsChanged += Refresh;
        else RunManager.Instance.OnCurrencyChanged += Refresh;
        subscribed = true;
        Refresh(kind == Kind.Shards ? RunManager.Instance.Shards : RunManager.Instance.Amount);
    }

    void OnDisable()
    {
        if (hovering) { hovering = false; ItemInfoPopup.Instance?.Hide(); }
        if (subscribed && RunManager.Instance != null)
        {
            RunManager.Instance.OnCurrencyChanged -= Refresh;
            RunManager.Instance.OnShardsChanged -= Refresh;
        }
        subscribed = false;
    }

    // 숫자 칸 또는 옆 아이콘(CurrencyIcon / ShardIcon)에 마우스가 있으면 설명창
    void Update()
    {
        if (!hoverInfo) return;
        var rt = transform as RectTransform;
        var icon = transform.parent != null ? transform.parent.Find(kind == Kind.Shards ? "ShardIcon" : "CurrencyIcon") as RectTransform : null;
        bool over = UIUtil.MouseOver(rt) || (icon != null && UIUtil.MouseOver(icon));
        if (over == hovering) return;
        hovering = over;
        if (!over) { ItemInfoPopup.Instance?.Hide(); return; }
        Image img = icon != null ? icon.GetComponent<Image>() : null;
        bool shard = kind == Kind.Shards;
        int have = RunManager.Instance == null ? 0 : (shard ? RunManager.Instance.Shards : RunManager.Instance.Amount);
        ItemInfoPopup.Instance?.ShowInfo(img != null ? img.sprite : null, shard ? shardTitle : currencyTitle, shard ? shardDesc : currencyDesc,
            $"보유 {have:#,0}", ShopColors.Hex("E3C27E"));
    }

    void Refresh(int amount)
    {
        if (amountText != null)
            amountText.text = $"{prefix}{amount}";
    }
}
