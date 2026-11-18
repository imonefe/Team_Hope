using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [2026-11-18 추가] 튜토리얼 영역(TutorialZone)을 씬 창에서 보고 고치기
/// - 씬 창 위쪽 기즈모 버튼이 꺼져 있어도 항상 보임 (게임 창에는 안 보임)
///   · 시작/도착 영역 = 하늘색, 떨어지면 되살아나는 영역 = 빨간색, 영역 이름 표시
///   · 각 구간 시작 영역 위에 "번호. 제목", 그 구간 범위(시작 영역 ~ 다음 시작 영역)를 색 띠로 표시
/// - 영역을 선택하면 테두리 손잡이로 크기 조절 (콜라이더 크기·위치가 같이 바뀜), 위치는 기본 이동 도구로
/// - Ctrl+Z 로 되돌리기 가능
/// </summary>
[CustomEditor(typeof(TutorialZone))]
[CanEditMultipleObjects]
public class TutorialZoneEditor : Editor
{
    readonly BoxBoundsHandle handle = new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };

    void OnSceneGUI()
    {
        var zone = (TutorialZone)target;
        BoxCollider2D col = zone.GetComponent<BoxCollider2D>();
        if (col == null) return;

        using (new Handles.DrawingScope(TutorialZoneSceneDraw.ZoneColor(zone), zone.transform.localToWorldMatrix))
        {
            handle.center = col.offset;
            handle.size = new Vector3(col.size.x, col.size.y, 0f);
            EditorGUI.BeginChangeCheck();
            handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(col, "튜토리얼 영역 크기");
                col.offset = handle.center;
                col.size = new Vector2(Mathf.Max(0.2f, handle.size.x), Mathf.Max(0.2f, handle.size.y));
            }
        }
    }
}

[InitializeOnLoad]
static class TutorialZoneSceneDraw
{
    static TutorialZoneSceneDraw() { SceneView.duringSceneGui += Draw; }

    public static Color ZoneColor(TutorialZone z)
        => z.kind == TutorialZone.Kind.Reach ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.35f, 0.3f);

    static void Draw(SceneView view)
    {
        if (Event.current.type != EventType.Repaint) return;
        StageHandle stage = StageUtility.GetCurrentStageHandle();

        // 영역 상자
        foreach (TutorialZone z in stage.FindComponentsOfType<TutorialZone>())
        {
            if (!z.gameObject.activeInHierarchy) continue;
            if (!TryRect(z, out Rect r)) continue;
            Color c = ZoneColor(z);
            DrawRect(r, new Color(c.r, c.g, c.b, 0.15f), c);
            Handles.Label(new Vector3(r.xMin, r.yMin, 0f), z.name, Style(c, false));
        }

        // 구간 번호 · 제목 · 범위 띠
        foreach (TutorialController ctrl in stage.FindComponentsOfType<TutorialController>())
        {
            if (ctrl.steps == null) continue;
            for (int i = 0; i < ctrl.steps.Count; i++)
            {
                TutorialController.Step s = ctrl.steps[i];
                if (s == null || s.startZone == null || !TryRect(s.startZone, out Rect r)) continue;
                float endX = r.center.x + 8f;
                for (int j = i + 1; j < ctrl.steps.Count; j++)
                    if (ctrl.steps[j] != null && ctrl.steps[j].startZone != null && TryRect(ctrl.steps[j].startZone, out Rect n)) { endX = n.xMin; break; }

                Color c = Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 1f);
                Rect band = Rect.MinMaxRect(r.xMin, r.yMin, Mathf.Max(endX, r.xMax), r.yMin + 0.35f);
                DrawRect(band, new Color(c.r, c.g, c.b, 0.45f), new Color(c.r, c.g, c.b, 0.9f));
                Handles.Label(new Vector3(r.xMin, r.yMax + 1.2f + (i % 2) * 1.6f, 0f), $"{i + 1}. {s.title}", Style(c, true));   // 이웃 구간 글자가 겹치지 않게 한 칸씩 엇갈림
            }
        }
    }

    static bool TryRect(TutorialZone z, out Rect r)
    {
        r = default;
        BoxCollider2D col = z.GetComponent<BoxCollider2D>();
        if (col == null) return false;
        Vector3 a = z.transform.TransformPoint(col.offset - col.size * 0.5f);
        Vector3 b = z.transform.TransformPoint(col.offset + col.size * 0.5f);
        r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return true;
    }

    static void DrawRect(Rect r, Color fill, Color line)
    {
        var v = new[] { new Vector3(r.xMin, r.yMin, 0f), new Vector3(r.xMin, r.yMax, 0f), new Vector3(r.xMax, r.yMax, 0f), new Vector3(r.xMax, r.yMin, 0f) };
        Handles.DrawSolidRectangleWithOutline(v, fill, line);
    }

    static GUIStyle Style(Color c, bool bold)
    {
        var st = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        st.normal.textColor = c;
        return st;
    }
}
