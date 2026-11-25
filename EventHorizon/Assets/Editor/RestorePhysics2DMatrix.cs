using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-25 추가] 오늘 실행 중에 코드로 바뀐 2D 물리 충돌표(dropping 층 칸)를 원래대로 되돌린다.
/// 에디터가 열려 있는 동안 메모리에 남은 값이 다시 저장되지 않도록, Unity 창을 누르면 한 번만 실행된다.
/// (dropping 층은 이제 쓰지 않음 — 발판 내려가기는 PlayerController.StartDrop 에서 콜라이더끼리만 충돌을 끈다)
/// </summary>
public static class RestorePhysics2DMatrix
{
    const string AutoKey = "EH_RestorePhysics2DMatrix_v1";

    [InitializeOnLoadMethod]
    static void Auto()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Run();
            EditorPrefs.SetBool(AutoKey, true);
        };
    }

    [MenuItem("EventHorizon/2D 물리 충돌표 되돌리기 (dropping 층)")]
    static void Run()
    {
        int drop = LayerMask.NameToLayer("dropping");
        if (drop < 0) return;
        foreach (string n in new[] { "Player", "Enemy", "Item", "BossBody", "Box_sp" })
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) Physics2D.IgnoreLayerCollision(drop, l, false);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[RestorePhysics2DMatrix] dropping 층 충돌표를 원래대로 되돌림");
    }
}
