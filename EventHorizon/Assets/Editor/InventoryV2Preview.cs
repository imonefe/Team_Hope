using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-26] 인벤토리 시안 v2 확인용 (실행 중에만). 프로젝트 폴더의 inv_test.trigger 내용대로 한 번 실행하고 파일을 지운다.
///  fill N  : 고유능력이 있는 사이버웨어 N개를 얻고(단계 섞음) 인벤토리를 연다
///  swap    : 안 가진 사이버웨어 하나를 얻으려 함 → 가득 찼으면 교체 창
///  pick I  : 교체 창에서 I번째 칸을 고름 → 확인 창
///  close   : 인벤토리 닫기
///  게임 데이터 · 씬은 건드리지 않음 (실행 중 상태만 바뀜)
/// </summary>
[InitializeOnLoad]
public static class InventoryV2Preview
{
    const string File_ = "inv_test.trigger";
    static InventoryV2Preview() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (!EditorApplication.isPlaying || !File.Exists(File_)) return;
        string cmd = File.ReadAllText(File_).Trim();
        File.Delete(File_);
        try { Run(cmd); }
        catch (System.Exception e) { Debug.LogError("[인벤토리 v2 확인] " + cmd + " 실패: " + e); }
    }

    static List<ItemData> Augments()
    {
        var list = new List<ItemData>();
        foreach (var g in AssetDatabase.FindAssets("t:ItemData"))
        {
            var d = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
            if (d != null && !string.IsNullOrEmpty(d.abilityText) && d.icon != null) list.Add(d);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static void Run(string cmd)
    {
        var inv = Inventory.Instance;
        if (inv == null) { Debug.LogWarning("[인벤토리 v2 확인] Inventory 없음"); return; }
        var player = PlayerController.Instance != null ? PlayerController.Instance.gameObject : null;
        string[] p = cmd.Split(' ');
        switch (p[0])
        {
            case "fill":
            {
                int n = p.Length > 1 ? int.Parse(p[1]) : 7;
                int lv = 0;
                foreach (var d in Augments())
                {
                    if (inv.passives.Count >= n) break;
                    if (inv.passives.Contains(d)) continue;
                    ItemObtain.Grant(d, player);
                    if (RunManager.Instance != null) RunManager.Instance.SetAugmentLevel(d, 1 + (lv++ % 3));
                }
                inv.NotifyChanged();
                inv.Open();
                break;
            }
            case "swap":
                foreach (var d in Augments())
                    if (!inv.passives.Contains(d)) { ItemObtain.Request(d, player, null); break; }
                break;
            case "pick":
            {
                int i = p.Length > 1 ? int.Parse(p[1]) : 0;
                if (i < inv.passives.Count) ItemSwapWindow.PickItem(inv.passives[i]);
                break;
            }
            case "dump":
            {
                var go = GameObject.Find(p[1]);
                if (go == null) { Debug.Log("[dump] 없음 " + p[1]); break; }
                foreach (var t in go.GetComponentsInChildren<RectTransform>(true))
                {
                    var g = t.GetComponent<UnityEngine.UI.Graphic>();
                    var c = new Vector3[4]; t.GetWorldCorners(c);
                    string extra = g == null ? "" : $" col={g.color} en={g.enabled} a={g.canvasRenderer.GetAlpha()} cull={g.canvasRenderer.cull}";
                    var tm = t.GetComponent<TMPro.TMP_Text>();
                    if (tm != null) extra += $" txt='{tm.text}' fs={tm.fontSize} font={(tm.font != null ? tm.font.name : "null")}";
                    Debug.Log($"[dump] {t.name} act={t.gameObject.activeInHierarchy} rect={t.rect} wc0={c[0]} wc2={c[2]} s={t.lossyScale}{extra}");
                }
                break;
            }
            case "close":
                inv.Close();
                break;
        }
        Debug.Log("[인벤토리 v2 확인] " + cmd + " 완료 (보유 " + inv.passives.Count + ")");
    }
}
