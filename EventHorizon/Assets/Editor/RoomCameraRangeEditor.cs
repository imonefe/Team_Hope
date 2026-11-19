using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [2026-11-19 추가] 보스방·중간보스 방의 카메라 고정 범위를 씬 뷰에서 보고 바로 고치는 도구.
///
/// - 씬 뷰의 기즈모 버튼이 꺼져 있어도, 방을 선택하지 않아도 항상 그린다.
///   (일반 씬과 프리팹 편집 화면 모두. 보스방 표시(isBossRoom)가 켜진 방만)
/// - 하늘색 상자 = roomCameraSize (반드시 화면에 들어올 범위). 모서리·변의 점을 끌어 크기를 바꾼다.
/// - 초록 상자   = 실제 게임 화면 (화면 비율 16:9 로 넓힌 범위, CameraFollow.HoldRoom 과 같은 계산)
/// - 가운데 네모 점을 끌면 카메라 중심(CameraCenter)이 움직인다.
/// 값은 Room 컴포넌트(roomCameraSize)와 CameraCenter 위치에 저장된다. 되돌리기(Ctrl+Z) 가능.
/// </summary>
[InitializeOnLoad]
public static class RoomCameraRangeEditor
{
    const float Aspect = 16f / 9f;
    static readonly BoxBoundsHandle box = new BoxBoundsHandle();
    static readonly Color Cyan = new Color(0.3f, 0.9f, 1f, 1f);
    static readonly Color Green = new Color(0.3f, 1f, 0.4f, 0.9f);

    static RoomCameraRangeEditor()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView view)
    {
        if (Application.isPlaying) return;
        Room[] rooms;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null) rooms = stage.prefabContentsRoot.GetComponentsInChildren<Room>(true);
        else rooms = Object.FindObjectsOfType<Room>();

        foreach (Room r in rooms)
            if (r != null && r.isBossRoom) DrawRoom(r);
    }

    static void DrawRoom(Room r)
    {
        Transform centerT = r.roomCameraCenter != null ? r.roomCameraCenter : r.transform;
        Vector3 c = centerT.position;
        Vector2 size = r.roomCameraSize;

        // 실제 화면 (초록)
        Vector2 screen = new Vector2(Mathf.Max(size.x, size.y * Aspect), Mathf.Max(size.y, size.x / Aspect));
        Handles.color = Green;
        Handles.DrawWireCube(c, new Vector3(screen.x, screen.y, 0f));

        // 잡을 범위 (하늘색) — 크기 조절 손잡이
        box.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        box.center = c;
        box.size = new Vector3(size.x, size.y, 0f);
        box.SetColor(Cyan);
        EditorGUI.BeginChangeCheck();
        box.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(r, "카메라 범위 크기");
            r.roomCameraSize = new Vector2(Mathf.Max(1f, box.size.x), Mathf.Max(1f, box.size.y));
            Undo.RecordObject(centerT, "카메라 범위 크기");
            centerT.position = new Vector3(box.center.x, box.center.y, c.z);
            EditorUtility.SetDirty(r);
            EditorUtility.SetDirty(centerT);
        }

        // 중심 이동 (가운데 네모)
        float hs = HandleUtility.GetHandleSize(c) * 0.12f;
        Handles.color = Cyan;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(c, hs, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(centerT, "카메라 중심 이동");
            centerT.position = new Vector3(moved.x, moved.y, c.z);
            EditorUtility.SetDirty(centerT);
        }

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Cyan } };
        Handles.Label(c + new Vector3(-size.x * 0.5f, size.y * 0.5f + HandleUtility.GetHandleSize(c) * 0.3f, 0f),
            $"카메라 범위 {size.x:0.#} × {size.y:0.#}   (초록 = 실제 화면 {screen.x:0.#} × {screen.y:0.#})", style);
    }
}
