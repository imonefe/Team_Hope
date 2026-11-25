#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-25] 키워드 5개 단계 특수 보상 연출 프리팹 만들기.
///  - 게임에 이미 있는 이펙트 프리팹을 복사해 색만 바꿔 저장 (새 이펙트를 만들지 않음)
///  - 저장 위치: Assets/Etc/Resources/KeywordFx/ (재질 복사본은 그 아래 Mat/)
///  - 복사본에서는 스크립트 · 충돌체 · 물리를 뺌 (연출 전용)
///  - 메뉴: Tools/Event Horizon/키워드 연출 프리팹 다시 만들기 — 프리팹이 하나라도 없으면 에디터가 열릴 때 자동으로 만듦
/// </summary>
[InitializeOnLoad]
public static class KeywordFxBuilder
{
    const string OutDir = "Assets/Etc/Resources/KeywordFx";
    const string MatDir = OutDir + "/Mat";

    struct Spec
    {
        public string name, src, hex; public float intensity; public bool straighten;
        public Spec(string n, string s, string h, float i = 2f, bool straight = false) { name = n; src = s; hex = h; intensity = i; straighten = straight; }
    }

    static readonly Spec[] Specs =
    {
        new Spec("KW_Dance_SwordWave", "Assets/1.Prefab/Enemy/MidBoss/FX_Archer_Streak.prefab",     "E8542A", 2.5f),   // 가열 블레이드: 불타는 검기
        new Spec("KW_Stasis_Wave",     "Assets/1.Prefab/Effect/Enemy/FX_RicoImpact.prefab",         "6E8BFF"),   // 정지: 시간 충격파
        new Spec("KW_Execute_Slash",   "Assets/1.Prefab/Skill/sword_skill_1step1.prefab",           "D23A4A"),   // 처형: 처형 참격
        new Spec("KW_Fracture_Crack",  "Assets/1.Prefab/Effect/Player/FX_ShieldShatter.prefab",     "9B7BFF"),   // 균열: 바닥 금 파동
        new Spec("KW_Sheath_Hit",      "Assets/1.Prefab/Effect/Combat/sword_hit.prefab",            "5999FF"),   // 보급 단검용 검집: 푸른 피격 참격
    };

    // [2026-11-25] 더 이상 쓰지 않는 것 (만들 때 지움)
    static readonly string[] Obsolete = { "KW_Overload_Aura", "KW_Combo_Strike", "KW_Combo_SwordWave", "KW_Overheat_Fire", "KW_Plunder_Orb", "KW_Overheat_Flame", "KW_Overheat_FloorSlash", "KW_Plunder_Ring" };

    static readonly string[] TintProps = { "_Color", "_BaseColor", "_TintColor", "_MainColor", "_tint", "_GlowColor", "_RingColor" };

    static KeywordFxBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var s in Specs)
                if (!File.Exists($"{OutDir}/{s.name}.prefab")) { Build(); return; }
        };
    }

    [MenuItem("Tools/Event Horizon/키워드 연출 프리팹 다시 만들기")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Etc/Resources")) AssetDatabase.CreateFolder("Assets/Etc", "Resources");
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Etc/Resources", "KeywordFx");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(OutDir, "Mat");

        foreach (var o in Obsolete) AssetDatabase.DeleteAsset($"{OutDir}/{o}.prefab");
        foreach (var guid in AssetDatabase.FindAssets("", new[] { MatDir }))   // 재질 · 펴진 메시 모두
        {
            string mp = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var o in Obsolete) if (Path.GetFileName(mp).StartsWith(o + "_")) { AssetDatabase.DeleteAsset(mp); break; }
        }

        int made = 0;
        foreach (var s in Specs)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(s.src);
            if (src == null) { Debug.LogWarning($"[키워드 연출] 원본이 없음: {s.src}"); continue; }
            ColorUtility.TryParseHtmlString("#" + s.hex, out Color c);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = s.name;
            go.transform.position = Vector3.zero;

            // 연출 전용: 스크립트 · 충돌체 · 물리 제거 (복사한 스킬 프리팹이 피해를 주지 않게)
            //   아지랑이(화면 일그러짐) 스크립트 ScreenDistortSource 는 보이는 효과라 남김
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name != "ScreenDistortSource") Object.DestroyImmediate(mb);
            if (s.straighten) Straighten(go, s.name);
            foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(rb);

            int mi = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SpriteRenderer sr) { sr.color = new Color(c.r, c.g, c.b, sr.color.a); }
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = TintedCopy(mats[i], c, s.intensity, $"{s.name}_{mi++}");
                r.sharedMaterials = mats;
                if (r is ParticleSystemRenderer psr && psr.trailMaterial != null)
                    psr.trailMaterial = TintedCopy(psr.trailMaterial, c, s.intensity, $"{s.name}_{mi++}");
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, main.startColor.color.a));
            }

            PrefabUtility.SaveAsPrefabAsset(go, $"{OutDir}/{s.name}.prefab");
            Object.DestroyImmediate(go);
            made++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[키워드 연출] 프리팹 {made}개를 {OutDir} 에 만들었습니다.");
    }

    // ══════════ [2026-11-25] 곡선 궤적을 1자로 펴기 ══════════
    //  메시 정점을 '원호 중심 기준 각도 · 반지름'으로 바꾼 뒤, 각도 → 가로 위치, 반지름 → 세로 위치로 다시 놓는다.
    //  (휘어진 띠 → 같은 두께의 가로 띠) · 원호 위에서 나오던 입자(불씨)는 펴진 띠 위에서 나오게 바꾸고, 계속 나오게 반복으로.
    static void Straighten(GameObject go, string name)
    {
        var made = new System.Collections.Generic.Dictionary<Mesh, Mesh>();
        MeshRenderer firstMr = null;
        int mi = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var src = mf.sharedMesh;
            if (src == null) continue;
            if (!made.TryGetValue(src, out var straight))
            {
                straight = StraightMesh(src);
                if (straight != null)
                {
                    straight.name = $"{name}_Line{mi}";
                    string path = $"{MatDir}/{name}_Line{mi++}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(straight, path);
                }
                made[src] = straight;
            }
            if (straight != null) mf.sharedMesh = straight;
            if (firstMr == null) firstMr = mf.GetComponent<MeshRenderer>();
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main; main.loop = true;
            var sh = ps.shape;
            if (sh.shapeType == ParticleSystemShapeType.Mesh && sh.mesh != null && made.TryGetValue(sh.mesh, out var m2) && m2 != null) sh.mesh = m2;
            else if ((sh.shapeType == ParticleSystemShapeType.Circle || sh.shapeType == ParticleSystemShapeType.Donut || sh.shapeType == ParticleSystemShapeType.SingleSidedEdge) && firstMr != null)
            {
                sh.shapeType = ParticleSystemShapeType.MeshRenderer;
                sh.meshRenderer = firstMr;
            }
        }
        Debug.Log($"[키워드 연출] {name}: 곡선 메시 {made.Count}개를 1자로 폈습니다.");
    }

    /// <summary>궤적 리본 메시를 1자로 편다. 텍스처 좌표 u = 궤적 방향, v = 폭 방향 (sword_trail1 · sword_trail1_glow 확인)
    ///  가로 = (u − 0.5) × 중심선 길이, 세로 = (v − 0.5) × 가장 넓은 폭 → 길이 · 두께는 원래 그대로, 휘어짐만 없어짐</summary>
    static Mesh StraightMesh(Mesh src)
    {
        Vector3[] v; Vector2[] uv;
        try { v = src.vertices; uv = src.uv; }
        catch (System.Exception e) { Debug.LogWarning($"[키워드 연출] 메시를 읽을 수 없어 곡선 그대로 씀: {src.name} ({e.Message})"); return null; }
        if (v == null || v.Length < 4 || uv == null || uv.Length != v.Length) { Debug.LogWarning($"[키워드 연출] 텍스처 좌표가 없어 곡선 그대로 씀: {src.name}"); return null; }

        // u 가 같은 정점끼리 묶어 중심선 · 폭 구하기
        var groups = new System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<int>>();
        for (int i = 0; i < v.Length; i++)
        {
            int key = Mathf.RoundToInt(uv[i].x * 1000f);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new System.Collections.Generic.List<int>();
            list.Add(i);
        }
        float length = 0f, width = 0f; Vector3 prev = Vector3.zero; bool hasPrev = false;
        float vMin = float.MaxValue, vMax = float.MinValue;
        foreach (var kv in groups)
        {
            Vector3 mid = Vector3.zero; int lo = kv.Value[0], hi = kv.Value[0];
            foreach (int i in kv.Value)
            {
                mid += v[i];
                if (uv[i].y < uv[lo].y) lo = i;
                if (uv[i].y > uv[hi].y) hi = i;
                vMin = Mathf.Min(vMin, uv[i].y); vMax = Mathf.Max(vMax, uv[i].y);
            }
            mid /= kv.Value.Count;
            if (hasPrev) length += Vector3.Distance(prev, mid);
            prev = mid; hasPrev = true;
            float dv = uv[hi].y - uv[lo].y;
            if (dv > 0.3f) width = Mathf.Max(width, Vector3.Distance(v[lo], v[hi]) / dv);
        }
        if (length < 1e-3f || width < 1e-4f) return null;
        float vMid = (vMin + vMax) * 0.5f;
        var outV = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
            outV[i] = new Vector3((uv[i].x - 0.5f) * length, (uv[i].y - vMid) * width, v[i].z);
        var m = Object.Instantiate(src);
        m.vertices = outV;
        m.RecalculateBounds();
        return m;
    }

    static Material TintedCopy(Material m, Color c, float intensity, string name)
    {
        if (m == null) return null;
        // 스프라이트 기본 재질(내장)은 그대로 둠 — 색은 SpriteRenderer.color 로 이미 바꿈
        string srcPath = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith("Resources/unity_builtin") || srcPath == "Library/unity default resources") return m;
        var copy = new Material(m) { name = name };
        bool changed = false;
        foreach (var p in TintProps)
            if (copy.HasProperty(p)) { var old = copy.GetColor(p); copy.SetColor(p, new Color(c.r * intensity, c.g * intensity, c.b * intensity, old.a)); changed = true; }
        if (!changed) { Object.DestroyImmediate(copy); return m; }
        string path = $"{MatDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }
}
#endif
