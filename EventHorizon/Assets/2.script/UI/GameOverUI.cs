using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임오버 화면. CharacterManager.GameOver()가 Show()를 호출해서 활성화하고,
/// 이번 런의 통계(플레이타임/처치한 적·보스/해결한 스테이지/최종 스텟/보유 재화)를 채워 보여준다.
/// "다시하기" 버튼은 이 스크립트가 알아서 RunManager.RestartRun()에 연결한다.
///
/// [2026-11-23] 화면 개편 — 화면의 90% 이상을 쓰는 큰 판 (1800×1000)
///  - 위: 제목(GAME OVER / Thank you for playing.) + 한 줄 요약
///  - 왼쪽: 기록 6칸 (작은 이름 위, 큰 값 아래) + 최종 능력치 (두 줄씩)
///  - 오른쪽: 이번 판에 얻은 증강 아이콘 (등급 색 테두리 · 강화 별, 개수에 맞춰 칸 크기 자동) + 키워드 개수
///    아이콘에 마우스를 올리면 증강 정보 팝업
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

    [Header("개편 (2026-11-23)")]
    [Tooltip("제목 아래 한 줄 요약")]
    public TMP_Text subtitleText;
    [Tooltip("기록 6번째 칸 — 획득한 증강 수")]
    public TMP_Text augmentCountText;
    [Tooltip("'획득한 증강' 제목 줄")]
    public TMP_Text augmentHeaderText;
    [Tooltip("키워드 개수 줄 (과열 3 · 강탈 2 …)")]
    public TMP_Text keywordLineText;
    [Tooltip("증강 아이콘이 놓이는 곳 (GridLayoutGroup)")]
    public RectTransform augmentGrid;
    [Tooltip("칸 견본 (꺼 둠). 자식 Icon(Image) · Stars(TMP). 자기 Image 는 등급 색 테두리")]
    public GameObject augmentCellTemplate;
    [Tooltip("칸 크기 최대 · 최소 (증강이 많으면 작아짐)")]
    public float cellMax = 112f, cellMin = 56f;
    [Tooltip("증강이 하나도 없을 때 그리드 자리에 띄울 글")]
    public string noAugmentText = "얻은 모듈이 없다.";

    public static readonly Color[] TierColors =
    {
        new Color32(0xB8, 0xBC, 0xC6, 0xFF), new Color32(0x5A, 0xA9, 0xFF, 0xFF),
        new Color32(0xB0, 0x7C, 0xFF, 0xFF), new Color32(0xFF, 0xB5, 0x47, 0xFF),
    };
    const string Muted = "9A9288";
    readonly List<GameObject> cells = new List<GameObject>();

    static string Cell(string label, string value) => $"<size=46%><color=#{Muted}>{label}</color></size>\n{value}";

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

        // [2026-11-23] 칸 모양: 작은 회색 이름 위 · 큰 값 아래
        if (playTimeText != null) playTimeText.text = Cell("플레이 타임", RunManager.FormatTime(playTime));
        if (enemiesKilledText != null) enemiesKilledText.text = Cell("처치한 적", enemies.ToString());
        if (bossesKilledText != null) bossesKilledText.text = Cell("처치한 보스", bosses.ToString());
        if (stageText != null) stageText.text = Cell("해결한 스테이지", stage.ToString());
        if (currencyText != null) currencyText.text = Cell("보유 재화", currency.ToString());
        if (subtitleText != null) subtitleText.text = $"{stage}번째 스테이지에서 멈췄다   <color=#{Muted}>·   {RunManager.FormatTime(playTime)}</color>";

        if (statsText != null)
        {
            // 두 칸씩 한 줄 (<pos> 로 오른쪽 칸 시작 위치를 맞춤)
            statsText.text = finalStats != null
                ? $"<color=#{Muted}>공격력</color>  {finalStats.Atk:0}<pos=50%><color=#{Muted}>방어력</color>  {finalStats.Def:0}\n" +
                  $"<color=#{Muted}>이동속도</color>  {finalStats.spd:0.0}<pos=50%><color=#{Muted}>제로포인트</color>  {finalStats.PauseDuration:0.#}초\n" +
                  $"<color=#{Muted}>치명타 확률</color>  {finalStats.EffectiveCritChance * 100f:0}%<pos=50%><color=#{Muted}>치명타 배율</color>  ×{finalStats.critMultiplier:0.#}"
                : "능력치 정보 없음";
        }

        FillAugments();
    }

    /// <summary>[2026-11-23] 이번 판에 얻은 증강 아이콘을 나열 (개수에 맞춰 칸 크기를 줄여 한 화면에 다 보이게)</summary>
    void FillAugments()
    {
        var owned = new List<ItemData>();
        if (Inventory.Instance != null)
            foreach (var d in Inventory.Instance.passives) if (d != null) owned.Add(d);

        if (augmentCountText != null) augmentCountText.text = Cell("얻은 모듈", owned.Count.ToString());
        if (augmentHeaderText != null) augmentHeaderText.text = owned.Count > 0 ? $"얻은 모듈   <color=#{Muted}>{owned.Count}개</color>" : $"얻은 모듈   <color=#{Muted}>{noAugmentText}</color>";

        if (keywordLineText != null)
        {
            int[] n = AugmentRules.CountKeywords(owned);
            var parts = new List<string>();
            for (int k = 0; k < n.Length; k++)
                if (n[k] > 0) parts.Add($"<color=#{AugmentRules.KeywordHex((AugmentKeyword)k)}>{AugmentRules.KeywordName((AugmentKeyword)k)} {n[k]}</color>");
            keywordLineText.text = string.Join("<color=#555049>   ·   </color>", parts);
            keywordLineText.gameObject.SetActive(parts.Count > 0);
        }

        foreach (var c in cells) if (c != null) Destroy(c);
        cells.Clear();
        if (augmentGrid == null || augmentCellTemplate == null) return;
        augmentCellTemplate.SetActive(false);

        // 칸 크기: 큰 것부터 줄여 가며 다 들어가는 크기를 고름
        var grid = augmentGrid.GetComponent<GridLayoutGroup>();
        float w = augmentGrid.rect.width, h = augmentGrid.rect.height;
        float sp = grid != null ? grid.spacing.x : 14f;
        float size = cellMax;
        for (; size > cellMin; size -= 4f)
        {
            int cols = Mathf.Max(1, Mathf.FloorToInt((w + sp) / (size + sp)));
            int rows = Mathf.CeilToInt(owned.Count / (float)cols);
            if (rows * (size + sp) - sp <= h) break;
        }
        if (grid != null) grid.cellSize = new Vector2(size, size);

        foreach (var d in owned)
        {
            var go = Instantiate(augmentCellTemplate, augmentGrid);
            go.SetActive(true);
            int tier = Mathf.Clamp((int)d.tier, 0, TierColors.Length - 1);
            var frame = go.GetComponent<Image>();
            if (frame != null) frame.color = TierColors[tier];
            var icon = go.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) { icon.sprite = d.icon; icon.enabled = d.icon != null; icon.raycastTarget = false; }
            var stars = go.transform.Find("Stars")?.GetComponent<TMP_Text>();
            int lv = RunManager.Instance != null ? Mathf.Max(1, RunManager.Instance.GetAugmentLevel(d)) : 1;
            if (stars != null) { stars.text = AugmentRules.Stars(lv); stars.raycastTarget = false; stars.fontSize = Mathf.Max(11f, size * 0.15f); }
            var cell = go.GetComponent<GameOverAugmentCell>();
            if (cell == null) cell = go.AddComponent<GameOverAugmentCell>();
            cell.item = d;
            cells.Add(go);
        }
    }

    /// <summary>[2026-11-21] 게임 클리어 — 같은 화면을 'Thank you for playing.' 제목으로 띄운다 (MapManager.GameClear)</summary>
    public void ShowClear(Stats finalStats)
    {
        Show(finalStats);
        if (titleText != null) titleText.text = clearTitle;
        if (subtitleText != null)
        {
            float playTime = RunManager.Instance != null ? RunManager.Instance.GetPlayTime() : 0f;
            subtitleText.text = $"모든 스테이지를 해결했다   <color=#{Muted}>·   {RunManager.FormatTime(playTime)}</color>";
        }
    }

    void OnRetryClicked()
    {
        // [2026-11-01] 판 초기화 후 인트로 씬으로 돌아간다 (예전: 게임 씬을 바로 다시 불러옴)
        StageFlow.SkipTutorialOnce = true;   // [2026-11-28] 다시하기 = 튜토리얼 건너뛰고 로비부터
        RunManager.ReturnToIntro();
    }
}
