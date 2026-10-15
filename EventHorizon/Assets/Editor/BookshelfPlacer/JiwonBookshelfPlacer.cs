// 책장 프리팹 - 원본 씬 위치 배치 스크립트 (jiwon)
//
// 사용법
//  1) 책장 프리팹 .unitypackage 들을 먼저 임포트합니다.
//  2) 배치할 씬을 엽니다.
//  3) 상단 메뉴 Tools > 책장 프리팹 > 원본 씬 위치에 배치 를 누릅니다.
//     (중간 low 1 포함, 원본 씬의 책장 전부를 원본 자리에 놓습니다)
//
// 동작
//  - "책장 배치 (원본 좌표)" 라는 빈 오브젝트(원점, 회전 없음, 크기 1)를 만들고
//    그 아래에 프리팹을 원본 씬과 같은 월드 좌표 / 회전으로 놓습니다.
//  - 크기(Scale)는 건드리지 않습니다. 프리팹(모델) 기본 크기 그대로 = 원본 씬과 같은 크기.
//  - 프리팹은 GUID로 찾기 때문에 폴더를 옮겨도 동작합니다.
//  - 임포트하지 않은 책장은 건너뛰고 Console 에 알려줍니다.
//  - Ctrl+Z 로 한 번에 되돌릴 수 있습니다.
//
// 좌표 출처: 원본 프로젝트 SampleScene (2026-10-14 05:03 저장본)
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class JiwonBookshelfPlacer
{
    struct Entry
    {
        public string name;       // 배치될 오브젝트 이름
        public string prefabGuid; // 책장 프리팹 패키지 안의 프리팹 GUID
        public Vector3 position;  // 월드 좌표
        public Quaternion rotation;

        public Entry(string name, string prefabGuid, Vector3 position, Quaternion rotation)
        {
            this.name = name;
            this.prefabGuid = prefabGuid;
            this.position = position;
            this.rotation = rotation;
        }
    }

    const string GuidShelf1 = "0170d2eaac26fe74dfb0a65b4c2c15e0"; // 책장 1 최종.prefab
    const string GuidShelf2 = "ad8ee8c5dea3bdbc5da2dcf5e0088470"; // 책장 2 최종 2.prefab
    const string GuidShelf3 = "5f016afbc56690637bb29c0b20333dbf"; // 책장 3 최종.prefab
    const string GuidMiddle1 = "5180fbaf570b6de81cb8b8f2e6e232ac"; // 중간 low 1.prefab

    // Rotation 은 원본 씬 값 그대로 (Quaternion x, y, z, w)
    // Inspector 에 보이는 값: 중간 low 1 = (-1.732, 170.07, 0.303)
    static readonly Entry Middle1 =
        new Entry("중간 low 1", GuidMiddle1, new Vector3(-57.428436f, 2.1498756f, 75.245575f), new Quaternion(0.0013272171f, 0.9961342f, 0.015285344f, 0.086493775f));

    static readonly Entry[] All =
    {
        // 원본에서는 Room01/BG 아래 (로컬 Y 7.78, Room01 이 Y -3.2) -> 월드 좌표로 환산
        new Entry("책장 1 최종", GuidShelf1, new Vector3(20.2f, 4.58f, 55.8f), new Quaternion(0f, 0.9999686f, 0f, -0.007926862f)),
        new Entry("책장 1 최종 (1)", GuidShelf1, new Vector3(79.9f, 4.58f, 55.8f), new Quaternion(0f, 0.9999686f, 0f, -0.007926862f)),
        // 원본에서는 Hierarchy 최상위
        new Entry("책장 1 최종", GuidShelf1, new Vector3(114.7f, 4.2f, 53.1f), new Quaternion(0f, 0.99987245f, 0f, -0.0159718f)),
        new Entry("책장 2 최종 2", GuidShelf2, new Vector3(36.9f, 2.2f, 88.5f), new Quaternion(0f, 0.9974386f, 0f, 0.07152808f)),
        new Entry("책장 3 최종", GuidShelf3, new Vector3(66.17288f, 1.8000002f, 90.1f), new Quaternion(0f, 0.9957148f, 0f, -0.09247795f)),
        Middle1,
    };

    [MenuItem("Tools/책장 프리팹/원본 씬 위치에 배치")]
    static void PlaceAll()
    {
        Place(All);
    }

    static void Place(Entry[] entries)
    {
        // 1) 임포트된 프리팹부터 확인
        var prefabs = new GameObject[entries.Length];
        int found = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(entries[i].prefabGuid);
            if (!string.IsNullOrEmpty(path))
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefabs[i] == null)
                Debug.LogWarning("[책장 배치] '" + entries[i].name + "' 프리팹을 찾지 못해 건너뜁니다. 해당 .unitypackage 를 먼저 임포트하세요.");
            else
                found++;
        }

        if (found == 0)
        {
            EditorUtility.DisplayDialog("책장 배치", "프리팹을 하나도 찾지 못했어요.\n책장 .unitypackage 를 먼저 임포트한 뒤 다시 실행하세요.", "확인");
            return;
        }

        // 2) 정리용 부모 오브젝트 (원점, 회전 없음, 크기 1 -> 자식의 로컬 좌표 = 월드 좌표)
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        var parent = new GameObject("책장 배치 (원본 좌표)");
        Undo.RegisterCreatedObjectUndo(parent, "책장 배치");

        // 3) 배치 (위치와 회전만, 크기는 프리팹 기본값 유지)
        for (int i = 0; i < entries.Length; i++)
        {
            if (prefabs[i] == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "책장 배치");

            go.name = entries[i].name;
            go.transform.SetPositionAndRotation(entries[i].position, entries[i].rotation);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = parent;
        EditorGUIUtility.PingObject(parent);
        Debug.Log("[책장 배치] " + found + "개를 원본 좌표에 배치했어요.");
    }
}
#endif
