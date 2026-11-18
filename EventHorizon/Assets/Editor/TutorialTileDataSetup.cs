using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-18 추가] 튜토리얼 타일맵을 "데이터 벽"(푸른색 + 데이터 비 + 격자 맥박)으로 바꾸는 메뉴
/// - 재질 2개를 만들고 (이미 있으면 값은 건드리지 않고 그대로 씀)
///   · M_TileData_Blue    : 바닥·벽 (Tilemap_Grund, Tilemap_Platform)
///   · M_TileData_Blue_BG : 뒤 배경 타일 (Tilemap_BG) — 조금 어둡게
/// - 함정 타일(Tilemap_trap)은 눈에 잘 띄어야 하므로 그대로 둠
/// - 원래 재질로 되돌리려면: 각 타일맵의 Tilemap Renderer → Material 을 Sprite-Lit-Default 로
/// - 중간 보스 방: 재질을 복사해서 색 값만 바꿔 같은 방법으로 넣으면 됨
/// - 튜토리얼 방 프리팹이 열려 있으면 열린 화면에 바로 적용 (Ctrl+Z 가능, 자동 저장)
/// </summary>
public static class TutorialTileDataSetup
{
    const string RoomPath = "Assets/1.Prefab/Room/Tutorial/TutorialRoom.prefab";
    const string ShaderName = "EventHorizon/TileDataBlue";
    const string MatDir = "Assets/6. Shader/Materials";
    const string MatMain = MatDir + "/M_TileData_Blue.mat";
    const string MatBG = MatDir + "/M_TileData_Blue_BG.mat";

    [MenuItem("EventHorizon/튜토리얼 타일맵 데이터 재질 적용")]
    public static void Run()
    {
        Shader sh = Shader.Find(ShaderName);
        if (sh == null) { Debug.LogError("[TileData] 셰이더 없음: " + ShaderName); return; }

        Material main = GetOrCreate(MatMain, sh, null);
        Material bg = GetOrCreate(MatBG, sh, m =>
        {
            m.SetFloat("_TileBright", 0.55f);
            m.SetFloat("_RainAmount", 0.6f);
            m.SetFloat("_GridAmount", 0.6f);
        });

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool inStage = stage != null && stage.assetPath == RoomPath;
        GameObject root = inStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            int n = 0;
            foreach (TilemapRenderer r in root.GetComponentsInChildren<TilemapRenderer>(true))
            {
                string name = r.gameObject.name;
                Material m = name == "Tilemap_Grund" || name == "Tilemap_Platform" ? main
                           : name == "Tilemap_BG" ? bg : null;
                if (m == null) continue;
                if (inStage) Undo.RecordObject(r, "타일맵 데이터 재질");
                r.sharedMaterial = m;
                if (inStage) EditorUtility.SetDirty(r);
                // [2026-11-18] 테두리 회로 빛용 칸 지도 (바닥·벽·발판만)
                if (m == main && r.GetComponent<TilemapOutlineMask>() == null)
                {
                    if (inStage) Undo.AddComponent<TilemapOutlineMask>(r.gameObject);
                    else r.gameObject.AddComponent<TilemapOutlineMask>();
                }
                n++;
            }
            if (inStage) EditorSceneManager.MarkSceneDirty(stage.scene);
            else PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log($"[TileData] 튜토리얼 타일맵 {n}개에 데이터 재질 적용 완료");
        }
        finally { if (!inStage) PrefabUtility.UnloadPrefabContents(root); }
    }

    static Material GetOrCreate(string path, Shader sh, System.Action<Material> init)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(sh);
        init?.Invoke(m);
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }
}
