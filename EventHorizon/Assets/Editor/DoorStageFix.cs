using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-24] 방마다 제각각이던 문을 스테이지에 맞게 통일한다. 유니티가 켜질 때 한 번 자동 실행.
///   1스테이지(금서고) 방 → 나무 아치 문 (door.png / door02.png, door02 애니메이션) — 기존 1스테이지 전투방과 같은 설정
///   2스테이지 방(폴더 2S, 이름 _2S) → 철문 (door 프리팹 기본 그대로: doorsprit, door 애니메이션)
/// 왜: 2스테이지 방들이 금서고 방을 복사해 만들어져 금서고 문을 달고 있었고, 1스테이지 보스방에는 철문이 달려 있었다.
/// 문의 아래 끝(바닥에 닿는 높이)은 바꾸기 전과 같게 맞춘다.
/// 메뉴: EventHorizon → 문 스테이지별로 맞추기
/// </summary>
[InitializeOnLoad]
public static class DoorStageFix
{
    const string DoorPrefab = "Assets/1.Prefab/item/EtcOBJ/door.prefab";
    const string LibraryRef = "Assets/1.Prefab/Room/1S/GradRoom_01.prefab";   // 금서고 문 설정 본보기
    const string Key = "EH_DoorStageFix_v1";

    static DoorStageFix()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(Key, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorPrefs.SetBool(Key, true);
            Run();
        };
    }

    class LibDoor { public Sprite active, inactive, sr; public RuntimeAnimatorController ctrl; public Vector3 innerPos; }

    [MenuItem("EventHorizon/문 스테이지별로 맞추기")]
    public static void Run()
    {
        var lib = ReadLibraryDoor();
        if (lib == null) { Debug.LogError("[문 맞추기] 본보기 금서고 문을 읽지 못함: " + LibraryRef); return; }

        var paths = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/1.Prefab/Room", "Assets/_Test/Room" }))
            paths.Add(AssetDatabase.GUIDToAssetPath(g));

        int changed = 0;
        foreach (var path in paths)
        {
            bool stage2 = path.Contains("/2S/") || Path.GetFileNameWithoutExtension(path).EndsWith("_2S");
            var root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            try
            {
                foreach (var portal in root.GetComponentsInChildren<Portal>(true))
                {
                    var door = PrefabUtility.GetOutermostPrefabInstanceRoot(portal.gameObject);
                    if (door == null || AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(door)) != DoorPrefab) continue;
                    var sr = portal.spriteRenderer; var anim = portal.animator;
                    if (sr == null) continue;
                    bool isLib = portal.inactiveSprite == lib.inactive;
                    if (stage2 == !isLib) continue;   // 이미 맞음

                    float oldBottom = sr.bounds.min.y;
                    if (stage2)
                    {
                        // 철문 = 문 프리팹 기본값으로 되돌림
                        PrefabUtility.RevertObjectOverride(portal, InteractionMode.AutomatedAction);
                        PrefabUtility.RevertObjectOverride(sr, InteractionMode.AutomatedAction);
                        if (anim != null) PrefabUtility.RevertObjectOverride(anim, InteractionMode.AutomatedAction);
                        if (sr.transform != door.transform) PrefabUtility.RevertObjectOverride(sr.transform, InteractionMode.AutomatedAction);
                    }
                    else
                    {
                        portal.activeSprite = lib.active; portal.inactiveSprite = lib.inactive;
                        sr.sprite = lib.sr;
                        if (anim != null) anim.runtimeAnimatorController = lib.ctrl;
                        if (sr.transform != door.transform) sr.transform.localPosition = lib.innerPos;
                        EditorUtility.SetDirty(portal); EditorUtility.SetDirty(sr); if (anim != null) EditorUtility.SetDirty(anim);
                    }
                    float newBottom = sr.bounds.min.y;
                    door.transform.position += Vector3.up * (oldBottom - newBottom);
                    dirty = true;
                    Debug.Log("[문 맞추기] " + path + " → " + (stage2 ? "철문(2스테이지)" : "금서고 문(1스테이지)"));
                }
                if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); changed++; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[문 맞추기] 바꾼 방 " + changed + "개");
    }

    static LibDoor ReadLibraryDoor()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(LibraryRef);
        if (go == null) return null;
        foreach (var p in go.GetComponentsInChildren<Portal>(true))
        {
            if (p.spriteRenderer == null) continue;
            return new LibDoor
            {
                active = p.activeSprite, inactive = p.inactiveSprite, sr = p.spriteRenderer.sprite,
                ctrl = p.animator != null ? p.animator.runtimeAnimatorController : null,
                innerPos = p.spriteRenderer.transform.localPosition
            };
        }
        return null;
    }
}
