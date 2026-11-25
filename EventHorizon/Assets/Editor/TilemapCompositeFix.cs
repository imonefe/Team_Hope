using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-25 추가] 방 프리팹의 바닥(ground 층) 타일맵 충돌체가 타일 한 칸씩 따로 되어 있으면 이음매에 캐릭터가 걸린다.
/// → 합치기 장치(CompositeCollider2D, 다각형)를 붙여 바닥 전체를 한 덩어리 충돌체로 만든다. 이미 합쳐진 타일맵은 건드리지 않음.
///  - Unity 창을 누르면 한 번 자동 실행 (AutoKey), 메뉴로 다시 실행 가능
///  - 대상: Assets/1.Prefab/Room 아래 모든 방 프리팹 (발견 당시 AugmentTestRoom 의 Tilemap_Ground 하나)
/// </summary>
public static class TilemapCompositeFix
{
    const string AutoKey = "EH_TilemapCompositeFix_v1";

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

    [MenuItem("EventHorizon/방 바닥 타일맵 충돌체 합치기")]
    static void Run()
    {
        int ground = LayerMask.NameToLayer("ground");
        int fixedCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/1.Prefab/Room" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int n = 0;
            foreach (var tc in root.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                if (tc.gameObject.layer != ground) continue;
                if (tc.usedByComposite && tc.GetComponent<CompositeCollider2D>() != null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(tc)) continue;   // 다른 프리팹 안의 것은 그 프리팹에서
                var rb = tc.GetComponent<Rigidbody2D>() ?? tc.gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
                var cc = tc.GetComponent<CompositeCollider2D>() ?? tc.gameObject.AddComponent<CompositeCollider2D>();
                cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
                tc.usedByComposite = true;
                n++;
            }
            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[TilemapCompositeFix] {Path.GetFileNameWithoutExtension(path)}: 바닥 타일맵 {n}개 합침");
                fixedCount += n;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[TilemapCompositeFix] 끝 — {fixedCount}개");
    }
}
