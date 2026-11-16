#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Spine.Unity;

// [2026-11-17 추가] 스파인 대기 동작 확인용 시험 장면을 만든다. 기존 장면은 건드리지 않는다.
public static class SpineTestSceneBuilder
{
    const string DataPath  = "Assets/4. Art/Illustration/Spine/baek_idle/baek_idle_SkeletonData.asset";
    const string ScenePath = "Assets/_Test/SpineTest_BaekIdle.unity";

    [MenuItem("EventHorizon/스파인 시험 장면 만들기 (백이연)")]
    public static void Build()
    {
        var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(DataPath);
        if (data == null) { Debug.LogError("[스파인 시험] 뼈대 자료를 찾지 못함: " + DataPath); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 뼈대 자료 배율(0.01)은 대화창 화면용 기준이라 건드리지 않는다. 대신 이 시험 오브젝트만 줄인다 (51칸 → 약 6칸)

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 3.3f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        camGo.transform.position = new Vector3(0f, 3f, -10f);
        camGo.tag = "MainCamera";

        SkeletonAnimation sa = SkeletonAnimation.NewSkeletonAnimationGameObject(data).skeletonAnimation;
        sa.gameObject.name = "백이연_대기";
        sa.transform.localScale = Vector3.one * 0.12f;
        sa.AnimationName = "idle";
        sa.loop = true;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[스파인 시험] 장면 생성: " + ScenePath + " — 재생 버튼을 누르면 대기 동작이 돈다.");
    }
}
#endif
