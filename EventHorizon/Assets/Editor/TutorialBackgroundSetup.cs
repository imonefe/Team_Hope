using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-16 추가] 튜토리얼 방에 회로 배경 판을 놓는다 (셰이더 EventHorizon/TutorialCircuitBG).
/// 방 프리팹을 새로 만들지 않고 배경 판 하나만 추가·갱신하므로, 손으로 고친 방에도 다시 실행해도 된다.
/// 판 크기 = 바닥 타일맵 범위 + 여유. 재질 값(색·밝기·주기)은 재질에서 직접 고치면 되고, 다시 실행해도 재질은 덮어쓰지 않는다.
/// </summary>
public static class TutorialBackgroundSetup
{
    const string RoomPath = "Assets/1.Prefab/Room/Tutorial/TutorialRoom.prefab";
    // [2026-11-18 수정] 배경을 "시뮬레이션 방" 셰이더(TutorialSimRoomBG) 재질로 바꿈. 예전 값: M_TutorialCircuit.mat
    const string MatPath = "Assets/6. Shader/Materials/M_TutorialSimRoom.mat";
    const string WhiteBoxPath = "Assets/1.Prefab/Room/Shop/WhiteBox.png";
    const float Margin = 6f;

    [MenuItem("EventHorizon/튜토리얼 배경 배치")]
    public static void Place()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Shader sh = Shader.Find("EventHorizon/TutorialCircuitBG");
            if (sh == null) { Debug.LogError("[TutorialBG] 셰이더를 못 찾음 (컴파일 확인)"); return; }
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            Tilemap grund = root.GetComponentsInChildren<Tilemap>(true).First(t => t.name.Contains("Grund"));
            grund.CompressBounds();
            Bounds b = grund.GetComponent<TilemapRenderer>().bounds;

            Transform bg = root.transform.Find("CircuitBackground");
            if (bg == null)
            {
                bg = new GameObject("CircuitBackground").transform;
                bg.SetParent(root.transform, false);
                bg.SetAsFirstSibling();
            }
            var sr = bg.GetComponent<SpriteRenderer>();
            if (sr == null) sr = bg.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
            sr.sharedMaterial = mat;
            sr.sortingOrder = -1000;
            sr.color = Color.white;

            // 판 크기: 흰 상자 그림(1×1 기준)을 방 범위만큼 늘림
            Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            bg.position = new Vector3(b.center.x, b.center.y, 5f);
            bg.localScale = new Vector3((b.size.x + Margin * 2) / spriteSize.x, (b.size.y + Margin * 2) / spriteSize.y, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log($"[TutorialBG] 배경 판 배치: 중심 {b.center} 크기 {b.size.x + Margin * 2}×{b.size.y + Margin * 2}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
