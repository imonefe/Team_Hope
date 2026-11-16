using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 무기 교체(Tab) 목록 채우기 — 에디터 전용.
///
/// 메뉴: EventHorizon → 무기 교체 목록 채우기 (열린 씬)
///   지금 열려 있는 씬의 모든 플레이어(PlayerController)의 weapons 목록을 [검, 건틀릿, 망치] 로 채우고 씬을 저장한다.
///   첫 칸(검)이 게임 시작 무기이므로 시작 무기는 그대로다. 순서를 바꾸고 싶으면 인스펙터에서 weapons 순서만 바꾸면 된다.
/// </summary>
public static class WeaponSwapSetup
{
    static readonly string[] WeaponPaths =
    {
        "Assets/3. Data/WeaponData/Sword.asset",
        "Assets/3. Data/WeaponData/Glove.asset",
        "Assets/3. Data/WeaponData/Hammer.asset",
    };

    [MenuItem("EventHorizon/무기 교체 목록 채우기 (열린 씬)")]
    public static void Fill()
    {
        var list = WeaponPaths.Select(p => AssetDatabase.LoadAssetAtPath<WeaponData>(p)).ToArray();
        if (list.Any(w => w == null)) { Debug.LogError("[무기 교체] WeaponData(Sword/Glove/Hammer)를 찾지 못함"); return; }

        int count = 0;
        foreach (var pc in Object.FindObjectsOfType<PlayerController>(true))
        {
            var so = new SerializedObject(pc);
            var prop = so.FindProperty("weapons");
            prop.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(pc.gameObject.scene);
            count++;
        }
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[무기 교체] 플레이어 {count}명의 weapons 목록을 검·건틀릿·망치로 채우고 씬을 저장했습니다.");
    }
}
