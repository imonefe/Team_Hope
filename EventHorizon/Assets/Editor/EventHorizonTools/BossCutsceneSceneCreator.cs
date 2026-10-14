using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventHorizonTools
{
    /// <summary>
    /// 2스테이지 보스 처치 연출(boss_disposaleffect) 확인용 씬을 자동으로 만들어주는 도구.
    /// 메뉴 실행 시: 새 씬 생성 -> 연출 프리팹을 원점에 배치 -> Assets/0.Scenes/에 저장.
    /// </summary>
    public static class BossCutsceneSceneCreator
    {
        private const string PrefabPath = "Assets/1.Prefab/Effect/Enemy/boss_disposaleffect.prefab";
        private const string OutputScenePath = "Assets/0.Scenes/boss2_cutscene.unity";

        [MenuItem("Tools/Event Horizon/보스 연출컷 씬 생성 (2스테이지)")]
        public static void CreateBossCutsceneScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "프리팹을 찾을 수 없음",
                    $"다음 경로에서 프리팹을 찾지 못했습니다:\n{PrefabPath}\n\n프리팹이 이동되었는지 확인해주세요.",
                    "확인");
                return;
            }

            // 저장되지 않은 변경사항이 있으면 먼저 확인
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 새 씬 생성 (기본 카메라 + Directional Light 포함)
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 프리팹을 원점에 배치 (프리팹 연결 유지: PrefabUtility 사용)
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, newScene);
            if (instance != null)
            {
                instance.transform.position = Vector3.zero;
                Selection.activeGameObject = instance;

                // 씬 뷰 카메라를 생성된 오브젝트에 맞춰 포커스 (에디터에서 바로 보이도록)
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            // Assets/0.Scenes/ 폴더가 있는지 확인 후 저장
            if (!AssetDatabase.IsValidFolder("Assets/0.Scenes"))
            {
                Debug.LogWarning("Assets/0.Scenes 폴더를 찾을 수 없어 저장 경로를 다시 확인해주세요.");
            }

            bool saved = EditorSceneManager.SaveScene(newScene, OutputScenePath);
            if (saved)
            {
                Debug.Log($"보스 연출컷 씬 생성 완료: {OutputScenePath}");
                EditorUtility.DisplayDialog(
                    "완료",
                    $"보스 연출컷 씬을 생성했습니다:\n{OutputScenePath}\n\n재생(▶) 버튼을 누르면 연출이 재생됩니다.",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("저장 실패", "씬 저장에 실패했습니다. 콘솔 로그를 확인해주세요.", "확인");
            }
        }
    }
}
