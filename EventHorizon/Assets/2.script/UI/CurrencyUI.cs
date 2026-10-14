using UnityEngine;
using TMPro;

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
    public TMP_Text amountText;
    [Tooltip("숫자 앞에 붙일 텍스트 (필요 없으면 비워둠)")]
    public string prefix = "";

    bool subscribed = false;

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

        RunManager.Instance.OnCurrencyChanged += Refresh;
        subscribed = true;
        Refresh(RunManager.Instance.Amount);
    }

    void OnDisable()
    {
        if (subscribed && RunManager.Instance != null)
            RunManager.Instance.OnCurrencyChanged -= Refresh;
        subscribed = false;
    }

    void Refresh(int amount)
    {
        if (amountText != null)
            amountText.text = $"{prefix}{amount}";
    }
}
