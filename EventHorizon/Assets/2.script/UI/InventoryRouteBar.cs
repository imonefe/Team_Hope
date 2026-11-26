using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-26 v2 · 시안 1-B] 인벤토리 맨 위 진행 줄 — 이번 스테이지(StageFlow 의 지금 그룹) 방들을 마름모로.
///  지나온 방 = 회색으로 채움, 지금 방 = 금색, 남은 방 = 빈 마름모 (전투 회색 · 중간보스/보스 붉은 테 · 상점 푸른 테).
///  견본 Node(자식 Edge · Fill) · Line 은 편집기에서 만든 것 (꺼 둠). 열릴 때마다 다시 그린다.
/// </summary>
public class InventoryRouteBar : MonoBehaviour
{
    public RectTransform nodeTemplate;
    public RectTransform lineTemplate;
    public float combatSize = 11f, midBossSize = 16f, bossSize = 20f;
    public Color combatEdge = new Color(0.557f, 0.541f, 0.518f, 1f);   // 8E8A84
    public Color bossEdge = new Color(0.69f, 0.204f, 0.122f, 1f);      // B0341F
    public Color shopEdge = new Color(0.435f, 0.659f, 0.863f, 1f);     // 6FA8DC
    public Color nowColor = new Color(0.89f, 0.76f, 0.494f, 1f);       // E3C27E
    public Color doneColor = new Color(0.341f, 0.322f, 0.298f, 1f);    // 57524C
    public Color emptyFill = new Color(0.07f, 0.07f, 0.07f, 1f);
    public Color lineTodo = new Color(0.91f, 0.9f, 0.88f, 0.18f);

    readonly List<GameObject> made = new List<GameObject>();

    void OnEnable() => Draw();

    void Draw()
    {
        foreach (var g in made) if (g != null) Destroy(g);
        made.Clear();
        if (nodeTemplate == null || lineTemplate == null) return;
        nodeTemplate.gameObject.SetActive(false);
        lineTemplate.gameObject.SetActive(false);
        if (!StageFlow.Active) return;

        // 지금 칸의 그룹 (예: 1S) 안의 방만. 아직 시작 전이면 첫 방의 그룹
        var run = StageFlow.Run;
        int cur = StageFlow.Current;
        string group = null;
        var ce = StageFlow.At(cur >= 0 ? cur : 0);
        if (ce != null) group = ce.group;
        var idx = new List<int>();
        for (int i = 0; i < run.Count; i++)
            if (run[i] != null && run[i].IsNode && (string.IsNullOrEmpty(group) || run[i].group == group)) idx.Add(i);

        for (int j = 0; j < idx.Count; j++)
        {
            int i = idx[j];
            var e = run[i];
            bool now = i == cur, done = i < cur;
            float s = e.kind == StageKind.Boss ? bossSize : e.kind == StageKind.MidBoss ? midBossSize : combatSize;
            Color edge = now ? nowColor : (e.kind == StageKind.Boss || e.kind == StageKind.MidBoss) ? bossEdge : e.kind == StageKind.Shop ? shopEdge : combatEdge;
            var n = Instantiate(nodeTemplate, nodeTemplate.parent);
            n.gameObject.SetActive(true);
            n.name = "Node_" + (e.label ?? e.id);
            float box = s * 1.42f;   // 45° 돌린 정사각형이 차지하는 폭
            var le = n.GetComponent<LayoutElement>() ?? n.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = box; le.preferredHeight = le.minHeight = box;
            var edgeImg = n.Find("Edge") as RectTransform;
            var fillImg = n.Find("Fill") as RectTransform;
            if (edgeImg != null) { edgeImg.sizeDelta = new Vector2(s, s); edgeImg.GetComponent<Image>().color = edge; }
            if (fillImg != null) { fillImg.sizeDelta = new Vector2(s - 3f, s - 3f); fillImg.GetComponent<Image>().color = now ? nowColor : done ? doneColor : emptyFill; }
            made.Add(n.gameObject);
            if (j < idx.Count - 1)
            {
                var l = Instantiate(lineTemplate, lineTemplate.parent);
                l.gameObject.SetActive(true);
                l.GetComponent<Image>().color = done ? doneColor : lineTodo;
                made.Add(l.gameObject);
            }
        }
    }
}
