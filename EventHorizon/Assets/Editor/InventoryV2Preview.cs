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
///  fx NAME T [SHOT] : 이펙트 v2 하나를 플레이어 앞에 띄우고 T초 뒤 일시정지 (SHOT 을 주면 fxshots/SHOT.png 저장) (sparks burst shatter pierce mark floor crack sheath coreS coreP coreT beat floorair)
///  go      : 일시정지 풀기
///  게임 데이터 · 씬은 건드리지 않음 (실행 중 상태만 바뀜)
/// </summary>
[InitializeOnLoad]
public static class InventoryV2Preview
{
    const string File_ = "inv_test.trigger";
    static InventoryV2Preview() { EditorApplication.update += Poll; }

    static double pauseAt = -1, pauseAt2 = -1;
    static string shotName;
    static void Poll()
    {
        if (pauseAt > 0 && EditorApplication.timeSinceStartup >= pauseAt)
        {
            pauseAt = -1;
            if (shotName != null) { Directory.CreateDirectory("fxshots"); ScreenCapture.CaptureScreenshot("fxshots/" + shotName + ".png"); shotName = null; pauseAt2 = EditorApplication.timeSinceStartup + 0.15; }
            else EditorApplication.isPaused = true;
        }
        if (pauseAt2 > 0 && EditorApplication.timeSinceStartup >= pauseAt2) { pauseAt2 = -1; EditorApplication.isPaused = true; }
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
            case "offer":   // 증강 고르기 창 열기 (교체 창 Esc → 되돌아오기 확인용)
            {
                var w = Object.FindObjectOfType<AugmentOfferWindow>(true);
                if (w != null) w.Offer(false); else Debug.LogWarning("[인벤토리 v2 확인] AugmentOfferWindow 없음");
                break;
            }
            case "shot":   // [2026-11-28] shot 이름 초 — 그 시간 뒤 게임 화면을 fxshots/이름.png 로 저장 후 정지
                pauseAt = EditorApplication.timeSinceStartup + (p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 0.5f);
                shotName = p.Length > 1 ? p[1] : "shot";
                break;
            case "close":
                inv.Close();
                break;
            case "go":
                EditorApplication.isPaused = false;
                break;
            case "fx":
                FxTest(p[1], player);
                pauseAt = p.Length > 2 ? EditorApplication.timeSinceStartup + float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : -1;
                shotName = p.Length > 3 ? p[3] : null;   // 4번째 값을 주면 그 순간 게임 화면을 fxshots/이름.png 로 저장 후 정지
                break;
        }
        Debug.Log("[인벤토리 v2 확인] " + cmd + " 완료 (보유 " + inv.passives.Count + ")");
    }

    static void FxTest(string n, GameObject player)
    {
        var b = AugmentFx.BodyBounds(player.transform);
        Vector3 c = b.center, feet = new Vector3(b.center.x, b.min.y, 0f), front = c + Vector3.right * 2.2f;
        ColorUtility.TryParseHtmlString("#E8542A", out var col);
        switch (n)
        {
            case "sparks": AugmentFx.Sparks(front, col, 12, 7f, 0.4f); break;
            case "burst": AugmentFx.Burst(front, col, 1f); break;
            case "shatter": AugmentFx.Shatter(front, col); break;
            case "pierce": AugmentFx.StreakFx(c, c + Vector3.right * 6f, col); break;
            case "mark": AugmentFx.Mark(player.transform, col, 1f, true, 1.6f); break;
            case "floor": AugmentFx.FloorGlow(feet + Vector3.right * 2.5f, 4f, col, 4f); break;
            case "crack": Kw("KW2_Fracture_Node", feet + Vector3.right * 2f, 0f); break;
            case "sheath": Kw("KW2_Sheath_Hit", front, 0f); break;
            case "coreS": case "coreP": case "coreT":
            {
                string k = n == "coreS" ? "Slash" : n == "coreP" ? "Pierce" : null;   // [2026-11-28] 타격(마법사) 제거
                if (k == null) break;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/1.Prefab/Effect/VFX/FX_CoreBreak_{k}.prefab");
                if (pf != null) Object.Instantiate(pf, front + Vector3.right * 1.5f, Quaternion.identity);
                break;
            }
            // [2026-11-27] 실제 코드 경로로 확인: 박자 칼날 검기 / 공중 위치를 준 불타는 바닥(바닥에 붙는지)
            case "beat": RunCo("BeatCone", c, 1, 4f, 0f, new Color(1f, 0.85f, 0.25f)); break;
            case "beatL": RunCo("BeatCone", c, -1, 4f, 0f, new Color(1f, 0.85f, 0.25f)); break;
            case "dance": RunCo("SwordWave", c, 1, 6f, 0f, 0, "KW_Dance_SwordWave", AugmentRuntime.OverheatColor); break;
            case "heat": RunCo("SwordWave", c, 1, 6f, 0f, 0, "KW2_Heat_Wave", AugmentRuntime.OverheatColor); break;
            case "heatL": RunCo("SwordWave", c, -1, 6f, 0f, 0, "KW2_Heat_Wave", AugmentRuntime.OverheatColor); break;
            case "gas": RunCo("GasCloud", player.transform.position + Vector3.right * 2.5f, 3f); break;
            case "floorair": RunCo("FireFloor", c + Vector3.right * 2.5f + Vector3.up * 1.5f, 3f); break;
            default: Debug.LogWarning("[이펙트 v2 확인] 모르는 이름 " + n); break;
        }
    }

    static void RunCo(string method, params object[] args)
    {
        var rt = Object.FindObjectOfType<AugmentRuntime>();
        var mi = typeof(AugmentRuntime).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rt == null || mi == null) { Debug.LogWarning("[이펙트 v2 확인] 실행 못 함: " + method); return; }
        rt.StartCoroutine((System.Collections.IEnumerator)mi.Invoke(rt, args));
    }

    static void Kw(string name, Vector3 pos, float ang)
    {
        var pf = Resources.Load<GameObject>("KeywordFx/" + name);
        if (pf == null) { Debug.LogWarning("[이펙트 v2 확인] 없음 " + name); return; }
        var go = Object.Instantiate(pf, pos, Quaternion.Euler(0, 0, ang));
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(true); ps.Play(true); }
        Object.Destroy(go, 3f);
    }
}
