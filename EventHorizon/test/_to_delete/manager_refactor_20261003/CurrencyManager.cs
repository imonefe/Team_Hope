using UnityEngine;

/// <summary>
/// 런 중 획득하는 재화(코인) 관리. ItemManager와 같은 패턴(PersistentSingleton)을 사용한다.
/// 적/보스를 처치하면 Health.cs / BossController.cs 에서 DropRandom()을 호출해 랜덤 재화를 지급하고,
/// FloatingTextSpawner를 통해 "+N" 팝업도 함께 띄운다.
/// </summary>
public class CurrencyManager : PersistentSingleton<CurrencyManager>
{
    public int Amount { get; private set; }

    /// <summary>재화 값이 바뀔 때마다 호출 (새 총액 전달). 재화 UI가 구독.</summary>
    public event System.Action<int> OnCurrencyChanged;

    [Header("재화 획득 팝업 색")]
    public Color popupColor = new Color(1f, 0.85f, 0.2f, 1f); // 금색

    void Awake()
    {
        InitSingleton();
    }

    void Start()
    {
        // UI가 이 시점에 이미 구독돼 있을 수 있으니 초기값도 한 번 알려준다
        OnCurrencyChanged?.Invoke(Amount);
    }

    public void Add(int amount)
    {
        if (amount == 0) return;
        Amount = Mathf.Max(0, Amount + amount);
        OnCurrencyChanged?.Invoke(Amount);
    }

    /// <summary>적/보스 처치 위치에서 min~max(포함) 사이 랜덤 재화를 지급하고 "+N" 팝업을 띄운다.</summary>
    public void DropRandom(Vector3 position, int min, int max)
    {
        if (max < min) max = min;
        int amount = Random.Range(min, max + 1);
        if (amount <= 0) return;

        Add(amount);
        FloatingTextSpawner.SpawnText(position, $"+{amount}", popupColor);
    }

    /// <summary>다시하기(런 재시작) 시 호출 — 재화를 0으로 초기화.</summary>
    public void ResetRun()
    {
        Amount = 0;
        OnCurrencyChanged?.Invoke(Amount);
    }
}
