using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 방(Room) 프리팹 안의 "BG" 오브젝트 밑에 있는 배경 오브젝트들을
/// Assets/1.Prefab/BG 폴더의 같은 이름 프리팹으로 바꿔 끼우는 에디터 도구.
///
/// 이름 비교: 뒤에 붙은 " (1)", " (2)", "(Clone)" 은 떼고 비교한다.
///   예) "Bookshelf3 (1)" → Bookshelf3 프리팹,  "DistantPillar1(Clone)" → DistantPillar1 프리팹
///
/// 바꿔 끼울 때 옛 오브젝트에서 그대로 옮기는 것:
///   - 이름, 부모, 하이어라키 순서, 켜짐/꺼짐, 레이어, 태그, Static 설정
///   - 위치 / 회전 / 크기 (로컬 값, 소수점까지 그대로)
///   - (옵션) 안쪽 부품(자식) 중 이름이 같은 것의 위치 / 회전 / 크기 / 켜짐 상태 / 레이어
///   - (옵션) 옛 오브젝트에 없던 부품은 꺼둠 → 지금 화면 모습 그대로 유지
///
/// 메뉴: Tools > 방 배경 프리팹 교체
/// </summary>
public class RoomBGReplacer : EditorWindow
{
    const string DefaultRoomPath = "Assets/1.Prefab/Room/graduate_S/Room01.prefab";
    const string DefaultBGFolder = "Assets/1.Prefab/BG";
    const string BGRootName = "BG";

    string roomPath = DefaultRoomPath;
    string bgFolder = DefaultBGFolder;
    bool matchChildren = true;
    bool hideExtraChildren = true;
    Vector2 scroll;
    string lastReport = "";

    [MenuItem("Tools/방 배경 프리팹 교체")]
    static void Open()
    {
        GetWindow<RoomBGReplacer>("방 배경 프리팹 교체");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
        roomPath = EditorGUILayout.TextField("방 프리팹 경로", roomPath);
        bgFolder = EditorGUILayout.TextField("BG 프리팹 폴더", bgFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("옵션", EditorStyles.boldLabel);
        matchChildren = EditorGUILayout.ToggleLeft("안쪽 부품(자식)도 이름이 같으면 위치/회전/크기를 옛 것과 똑같이 맞추기", matchChildren);
        hideExtraChildren = EditorGUILayout.ToggleLeft("옛 오브젝트에 없던 부품은 꺼두기 (지금 모습 그대로 유지)", hideExtraChildren);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. 미리보기 (바꾸지 않음)", GUILayout.Height(30))) Run(false);
            if (GUILayout.Button("2. 실행", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("방 배경 프리팹 교체",
                    roomPath + " 의 배경 오브젝트를 BG 프리팹으로 바꿉니다.\n(실행 전에 프리팹 파일 백업을 권장)", "실행", "취소"))
                    Run(true);
            }
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    static readonly Regex SuffixRegex = new Regex(@"(\s*\(\d+\)|\s*\(Clone\))+$");

    static string BaseName(string n)
    {
        return SuffixRegex.Replace(n, "").Trim();
    }

    void Run(bool apply)
    {
        var sb = new StringBuilder();
        sb.AppendLine(apply ? "■ 실행 결과" : "■ 미리보기 (아직 아무것도 바뀌지 않았음)");

        // BG 프리팹 목록 (이름 → 프리팹)
        var bgPrefabs = new Dictionary<string, GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { bgFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null && !bgPrefabs.ContainsKey(go.name)) bgPrefabs.Add(go.name, go);
        }
        sb.AppendLine("BG 프리팹 " + bgPrefabs.Count + "개: " + string.Join(", ", bgPrefabs.Keys));

        GameObject root = PrefabUtility.LoadPrefabContents(roomPath);
        if (root == null) { lastReport = "방 프리팹을 열 수 없음: " + roomPath; return; }

        try
        {
            Transform bgRoot = root.transform.Find(BGRootName);
            if (bgRoot == null) { sb.AppendLine("!! '" + BGRootName + "' 오브젝트를 못 찾음"); return; }

            // 순회 중에 지우면 순서가 꼬이므로 대상 목록을 먼저 만든다
            var targets = new List<Transform>();
            foreach (Transform child in bgRoot) targets.Add(child);

            int replaced = 0;
            foreach (Transform old in targets)
            {
                string baseName = BaseName(old.name);
                if (!bgPrefabs.TryGetValue(baseName, out GameObject prefab))
                {
                    sb.AppendLine("  - 그대로 둠 (같은 이름 프리팹 없음): " + old.name);
                    continue;
                }
                if (PrefabUtility.IsAnyPrefabInstanceRoot(old.gameObject) &&
                    PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == prefab)
                {
                    sb.AppendLine("  - 그대로 둠 (이미 " + prefab.name + " 프리팹임): " + old.name);
                    continue;
                }

                sb.AppendLine("  ▶ " + old.name + "  →  " + prefab.name + " 프리팹");
                sb.AppendLine("      위치 " + V(old.localPosition) + " / 회전 " + Q(old.localRotation) + " / 크기 " + V(old.localScale));

                GameObject inst = null;
                if (apply)
                {
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bgRoot);
                    inst.name = old.name;
                    inst.transform.SetSiblingIndex(old.GetSiblingIndex());
                    CopyBasic(old.gameObject, inst);
                    SetLayerRecursive(inst.transform, old.gameObject.layer);
                }

                // 안쪽 부품 비교
                var oldChildren = new Dictionary<string, Transform>();
                foreach (Transform oc in old) if (!oldChildren.ContainsKey(oc.name)) oldChildren.Add(oc.name, oc);
                var newChildNames = new HashSet<string>();
                foreach (Transform nc in prefab.transform)
                {
                    newChildNames.Add(nc.name);
                    Transform target = apply ? inst.transform.Find(nc.name) : null;
                    if (oldChildren.TryGetValue(nc.name, out Transform oc))
                    {
                        bool same = Approximately(oc, nc);
                        if (matchChildren)
                        {
                            if (!same) sb.AppendLine("      · 부품 " + nc.name + ": 옛 위치/회전/크기로 맞춤  " + V(oc.localPosition) + " / " + Q(oc.localRotation) + " / " + V(oc.localScale));
                            if (apply && target != null) CopyBasic(oc.gameObject, target.gameObject);
                        }
                        else if (!same) sb.AppendLine("      · 부품 " + nc.name + ": 옛 것과 다름 → 프리팹 기본값 사용");
                    }
                    else if (hideExtraChildren)
                    {
                        sb.AppendLine("      · 부품 " + nc.name + ": 옛 오브젝트에 없던 부품 → 꺼둠");
                        if (apply && target != null) target.gameObject.SetActive(false);
                    }
                    else sb.AppendLine("      · 부품 " + nc.name + ": 옛 오브젝트에 없던 부품 → 새로 보이게 됨");
                }
                foreach (var kv in oldChildren)
                    if (!newChildNames.Contains(kv.Key))
                        sb.AppendLine("      · 주의: 옛 부품 " + kv.Key + " 는 프리팹에 없음 → 사라짐");

                if (apply) Object.DestroyImmediate(old.gameObject);
                replaced++;
            }

            sb.AppendLine((apply ? "바꾼 개수: " : "바뀔 개수: ") + replaced);
            if (apply)
            {
                PrefabUtility.SaveAsPrefabAsset(root, roomPath);
                sb.AppendLine("저장 완료: " + roomPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            lastReport = sb.ToString();
            Debug.Log("[방 배경 프리팹 교체]\n" + lastReport);
        }
    }

    // 위치 / 회전 / 크기 / 켜짐 / 레이어 / 태그 / Static 을 그대로 옮긴다 (부모와 순서는 바깥에서 처리)
    static void CopyBasic(GameObject from, GameObject to)
    {
        to.transform.localPosition = from.transform.localPosition;
        to.transform.localRotation = from.transform.localRotation;
        to.transform.localScale = from.transform.localScale;
        to.SetActive(from.activeSelf);
        to.layer = from.layer;
        to.tag = from.tag;
        GameObjectUtility.SetStaticEditorFlags(to, GameObjectUtility.GetStaticEditorFlags(from));
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursive(c, layer);
    }

    static bool Approximately(Transform a, Transform b)
    {
        return (a.localPosition - b.localPosition).sqrMagnitude < 1e-8f
            && Quaternion.Angle(a.localRotation, b.localRotation) < 0.001f
            && (a.localScale - b.localScale).sqrMagnitude < 1e-8f;
    }

    static string V(Vector3 v) => "(" + v.x.ToString("R") + ", " + v.y.ToString("R") + ", " + v.z.ToString("R") + ")";
    static string Q(Quaternion q) => "(" + q.x.ToString("R") + ", " + q.y.ToString("R") + ", " + q.z.ToString("R") + ", " + q.w.ToString("R") + ")";
}
