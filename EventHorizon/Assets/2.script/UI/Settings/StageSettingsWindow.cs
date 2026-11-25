using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-21 추가] 일시정지 화면 '스테이지 설정' 창.
///
/// - StagePlan 의 칸을 튜토리얼부터 마지막까지 그대로 나열하고, 칸마다 켜기/끄기를 고른다.
/// - 켜 둔 칸만 차례로 밟고 넘어간다 (노드 지도에도 켜 둔 칸만 나옴).
/// - 바꾼 값은 바로 저장되지만, 지금 판에는 적용되지 않고 '새 게임'을 누른 다음 판부터 적용된다.
/// - 창 모양(패널 · 제목 · 버튼 · 줄 본보기)은 프리팹(Resources/StageSettingsCanvas)에 있고,
///   줄은 순서표 칸 수만큼 본보기를 복제해 채운다.
/// - 조작: ↑↓ 칸 고르기 · Space/Enter 켜기·끄기 · Esc 닫기 (마우스 클릭도 됨)
/// </summary>
public class StageSettingsWindow : MonoBehaviour
{
    public const string ResourcePath = "StageSettingsCanvas";
    static StageSettingsWindow instance;
    static int closedFrame = -1;

    [Header("순서표 (비우면 MapManager 에 연결된 것)")]
    public StagePlan plan;

    [Header("줄 본보기 (꺼 둠 — 칸마다 복제)")]
    public TMP_Text headerTemplate;
    public RectTransform lineTemplate;
    public TMP_Text labelTemplate;
    public SettingToggle toggleTemplate;
    public TMP_Text stateTemplate;

    [Header("배치 — 패널 안쪽을 두 칸으로 똑같이 나눈 격자 (패널 왼쪽 위 기준)")]
    [Tooltip("패널 좌우 여백 (제목 · 제목 밑줄과 같은 48)")]
    public float margin = 48f;
    [Tooltip("두 칸 사이 간격")]
    public float columnGap = 48f;
    [Tooltip("첫 줄(묶음 제목) 위치 — 제목 밑줄(-108) 아래")]
    public float top = -128f;
    [Tooltip("이 묶음부터 두 번째 칸에 놓음")]
    public string secondColumnFromGroup = "2S";
    [Tooltip("이름이 묶음 제목보다 안쪽으로 들어가는 폭")]
    public float indent = 24f;
    [Tooltip("켬/끔 글자 칸 너비 (칸 오른쪽 끝에 붙음)")]
    public float stateWidth = 56f;
    [Tooltip("스위치와 켬/끔 글자 사이")]
    public float toggleGap = 12f;
    [Tooltip("묶음 제목 위 → 첫 줄 가운데")]
    public float headerStep = 70f;
    [Tooltip("묶음 제목 위 → 밑줄")]
    public float lineOffset = 40f;
    [Tooltip("줄 사이 (가운데 → 가운데)")]
    public float rowStep = 44f;

    [Header("버튼")]
    public Button allOnButton;
    public Button closeButton;

    [Header("고른 줄 표시")]
    public Color focusColor = new Color(1f, 0.85f, 0.45f, 1f);

    class Row
    {
        public StagePlan.Entry entry;
        public TMP_Text label;
        public SettingToggle toggle;
        public Color baseColor;
    }

    readonly List<Row> rows = new List<Row>();
    int focus;
    bool built;

    /// <summary>열려 있거나 이번 프레임에 Esc 로 막 닫혔으면 true (ESC 메뉴가 같이 닫히지 않게)</summary>
    public static bool BlocksEscape => (instance != null && instance.gameObject.activeSelf) || Time.frameCount == closedFrame;

    public static void Open()
    {
        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) { Debug.LogWarning("[StageSettingsWindow] Resources 에 '" + ResourcePath + "' 프리팹이 없습니다."); return; }
            GameObject go = Instantiate(prefab);
            go.name = prefab.name;
            instance = go.GetComponent<StageSettingsWindow>();
            if (instance == null) { Destroy(go); return; }
        }
        instance.Show();
    }

    void Awake()
    {
        if (instance == null) instance = this;
        if (allOnButton != null) allOnButton.onClick.AddListener(AllOn);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        UIButtonJuice.Attach(allOnButton);
        UIButtonJuice.Attach(closeButton);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Show()
    {
        gameObject.SetActive(true);
        GameSfx.WindowOpen();   // [2026-11-25] 창 열림 소리
        Build();
        Refresh();
        focus = Mathf.Clamp(focus, 0, Mathf.Max(0, rows.Count - 1));
        ShowFocus();
    }

    public void Close()
    {
        closedFrame = Time.frameCount;
        gameObject.SetActive(false);
    }

    void Build()
    {
        if (built) return;
        built = true;
        StagePlan p = plan != null ? plan : (MapManager.Instance != null ? MapManager.Instance.stagePlan : null);
        if (p == null || labelTemplate == null || toggleTemplate == null) { Debug.LogWarning("[StageSettingsWindow] 순서표나 줄 본보기가 비어 있습니다."); return; }

        // 패널 안쪽 폭을 두 칸으로 똑같이 나눔
        RectTransform panel = (RectTransform)labelTemplate.rectTransform.parent;
        float inner = panel.rect.width - margin * 2f;
        float colW = (inner - columnGap) * 0.5f;
        float toggleW = ((RectTransform)toggleTemplate.transform).sizeDelta.x;
        float x = margin, y = top;
        string group = null;
        bool second = false;
        foreach (var e in p.entries)
        {
            if (e == null) continue;
            if (e.group != group)
            {
                group = e.group;
                if (!second && !string.IsNullOrEmpty(secondColumnFromGroup) && group == secondColumnFromGroup)
                {
                    second = true; x = margin + colW + columnGap; y = top;
                }
                if (headerTemplate != null)
                {
                    var h = Clone(headerTemplate.rectTransform, x, y);
                    h.sizeDelta = new Vector2(colW, h.sizeDelta.y);
                    h.GetComponent<TMP_Text>().text = group;
                }
                if (lineTemplate != null)
                {
                    var l = Clone(lineTemplate, x, y - lineOffset);
                    l.sizeDelta = new Vector2(colW, l.sizeDelta.y);   // 밑줄 = 칸 너비 그대로
                }
                y -= headerStep;
            }

            var row = new Row { entry = e };
            float stateX = x + colW - stateWidth;
            float toggleX = stateX - toggleGap - toggleW;
            var lr = Clone(labelTemplate.rectTransform, x + indent, y);
            lr.sizeDelta = new Vector2(toggleX - (x + indent) - 12f, lr.sizeDelta.y);
            row.label = lr.GetComponent<TMP_Text>();
            row.label.text = e.label;
            row.baseColor = row.label.color;

            var tr = Clone((RectTransform)toggleTemplate.transform, toggleX, y);
            row.toggle = tr.GetComponent<SettingToggle>();
            if (stateTemplate != null)
            {
                var sr = Clone(stateTemplate.rectTransform, stateX, y);
                sr.sizeDelta = new Vector2(stateWidth, sr.sizeDelta.y);
                row.toggle.stateText = sr.GetComponent<TMP_Text>();
            }
            var captured = row;
            row.toggle.OnChanged = on => { StageSettings.Set(captured.entry, on); focus = rows.IndexOf(captured); ShowFocus(); };
            rows.Add(row);
            y -= rowStep;
        }
    }

    // 본보기를 같은 부모 아래에 복제해 (왼쪽 위 기준) 위치에 놓는다
    static RectTransform Clone(RectTransform tpl, float x, float y)
    {
        var rt = Instantiate(tpl, tpl.parent);
        rt.gameObject.SetActive(true);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, rt.pivot.y);
        rt.anchoredPosition = new Vector2(x, y);
        return rt;
    }

    void Refresh()
    {
        foreach (var r in rows) r.toggle.SetValue(StageSettings.IsOn(r.entry), false);
    }

    void AllOn()
    {
        foreach (var r in rows) { StageSettings.Set(r.entry, true); r.toggle.SetValue(true, false); }
    }

    void ShowFocus()
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].label != null) rows[i].label.color = i == focus ? focusColor : rows[i].baseColor;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (rows.Count == 0) return;
        int before = focus;
        if (Input.GetKeyDown(KeyCode.UpArrow)) focus = (focus - 1 + rows.Count) % rows.Count;
        if (Input.GetKeyDown(KeyCode.DownArrow)) focus = (focus + 1) % rows.Count;
        if (focus != before) { UIClickSound.PlayClick(); ShowFocus(); }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            var r = rows[focus];
            UIClickSound.PlayClick();
            r.toggle.SetValue(!r.toggle.IsOn, true);
        }
    }
}
