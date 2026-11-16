using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 로비 무기 진열대 제거 — 에디터 전용 (한 번 실행용).
///
/// 메뉴: EventHorizon → 로비 무기 진열대 제거
///   LobbyRoom 프리팹 안의 무기 진열대(WeaponStand) 3개를 방에서 뺀다.
///   진열대 프리팹·스크립트 파일 자체는 지우지 않는다 (되살릴 때 다시 끌어다 놓으면 됨).
/// </summary>
public static class RemoveWeaponStands
{
    const string LobbyPath = "Assets/1.Prefab/Room/Lobby/LobbyRoom.prefab";

    [MenuItem("EventHorizon/로비 무기 진열대 제거")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(LobbyPath);
        try
        {
            var targets = new List<GameObject>();
            foreach (var s in root.GetComponentsInChildren<WeaponStand>(true))
            {
                // 진열대가 다른 프리팹 안에 들어 있으면 그 바깥 프리팹 통째로
                var go = PrefabUtility.GetOutermostPrefabInstanceRoot(s.gameObject);
                if (go == null || go == root) go = s.gameObject;
                if (!targets.Contains(go)) targets.Add(go);
            }
            foreach (var go in targets)
            {
                Debug.Log("[무기 진열대 제거] " + go.name);
                Object.DestroyImmediate(go);
            }
            PrefabUtility.SaveAsPrefabAsset(root, LobbyPath);
            Debug.Log($"[무기 진열대 제거] 로비에서 {targets.Count}개를 뺐습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
