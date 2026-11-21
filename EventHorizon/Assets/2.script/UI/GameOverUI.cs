using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임오버 화면. CharacterManager.GameOver()가 Show()를 호출해서 활성화하고,
/// 이번 런의 통계(플레이타임/처치한 적·보스/해결한 스테이지/최종 스텟/보유 재화)를 채워 보여준다.
/// "다시하기" 버튼은 이 스크립트가 알아서 RunManager.RestartRun()에 연결한다.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance;

    [Header("텍스트 필드 (비워두면 그 항목은 표시 안 함)")]
    public TMP_Text playTimeText;
    public TMP_Text enemiesKilledText;
    public TMP_Text bossesKilledText;
    public TMP_Text stageText;
    public TMP_Text statsText;
    public TMP_Text currencyText;

    [Header("제목 (2026-11-21) — 게임 클리어 때 바뀜")]
    public TMP_Text titleText;
    [Tooltip("켜 둔 마지막 스테이지를 끝냈을 때 제목")]
    public string clearTitle = "Thank you for playing.";
    string gameOverTitle;

    [Header("버튼")]
    public Button retryButton;

    void Awake()
    {
        Instance = this;
        if (titleText != null) gameOverTitle = titleText.text;   // [2026-11-21] 원래 제목 (GAME OVER) 기억
        gameObject.SetActive(false);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        // 버튼 호버/클릭 연출 + 게임오버 화면 등장 애니메이션
        UIButtonJuice.Attach(retryButton);
        if (GetComponent<UIPanelAnimator>() == null)
            gameObject.AddComponent<UIPanelAnimator>();
    }

    /// <summary>게임오버 시 CharacterManager.GameOver()에서 호출. finalStats는 마지막으로 죽은 캐릭터의 Stats.</summary>
    public void Show(Stats finalStats)
    {
        if (titleText != null && gameOverTitle != null) titleText.text = gameOverTitle;   // [2026-11-21]
        gameObject.SetActive(true);
        GamePause.Push(GamePause.GameOver);

        float playTime = RunManager.Instance != null ? RunManager.Instance.GetPlayTime() : 0f;
        int enemies = RunManager.Instance != null ? RunManager.Instance.EnemiesKilled : 0;
        int bosses = RunManager.Instance != null ? RunManager.Instance.BossesKilled : 0;
        int stage = MapManager.Instance != null ? MapManager.Instance.CurrentStageIndex + 1 : 1;
        int currency = RunManager.Instance != null ? RunManager.Instance.Amount : 0;

        if (playTimeText != null) playTimeText.text = $"플레이 타임   {RunManager.FormatTime(playTime)}";
        if (enemiesKilledText != null) enemiesKilledText.text = $"처치한 적   {enemies}";
        if (bossesKilledText != null) bossesKilledText.text = $"처치한 보스   {bosses}";
        if (stageText != null) stageText.text = $"해결한 스테이지   {stage}";
        if (currencyText != null) currencyText.text = $"보유 재화   {currency}";

        if (statsText != null)
        {
            statsText.text = finalStats != null
                ? $"공격력 {finalStats.Atk:0}   방어력 {finalStats.Def:0}   " +
                  $"이동속도 {finalStats.spd:0.0}   퍼즈 {finalStats.PauseDuration:0.#}초   " +
                  $"치명타 확률 {finalStats.EffectiveCritChance * 100f:0}%   치명타 배율 {finalStats.critMultiplier:0.#}배"
                : "스탯 정보 없음";
        }
    }

    /// <summary>[2026-11-21] 게임 클리어 — 같은 화면을 'Thank you for playing.' 제목으로 띄운다 (MapManager.GameClear)</summary>
    public void ShowClear(Stats finalStats)
    {
        Show(finalStats);
        if (titleText != null) titleText.text = clearTitle;
    }

    void OnRetryClicked()
    {
        // [2026-11-01] 판 초기화 후 인트로 씬으로 돌아간다 (예전: 게임 씬을 바로 다시 불러옴)
        RunManager.ReturnToIntro();
    }
}
