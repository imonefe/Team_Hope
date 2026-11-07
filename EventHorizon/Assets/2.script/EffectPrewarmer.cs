// EffectPrewarmer.cs
// 이펙트·적 프리팹이 "처음" 화면에 나올 때 생기는 렉(셰이더 컴파일, 첫 생성 비용)을
// 씬 시작 직후 보이지 않는 곳에서 미리 한 번씩 그려서 없애 주는 스크립트.
//
// ▶ 설정 필요 없음: 씬이 열리면 자동으로 생성되어 동작합니다.
//   - 씬 안의 스크립트들이 참조하는 프리팹(무기 데이터의 이펙트, 대시/점프 이펙트, 적 프리팹 등)을
//     자동으로 찾아서, 스크립트·콜라이더·소리·애니메이터를 끈 "껍데기"만
//     화면 밖 전용 카메라(256x256 렌더 텍스처)로 2프레임 그린 뒤 지웁니다.
//   - 게임 로직(데미지, 스폰, 사운드)은 실행되지 않습니다.
// ▶ 끄고 싶으면: 아래 AutoCreate 를 false 로.
// ▶ 직접 넣고 싶은 프리팹이 있으면: 씬에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤 Extra Prefabs 에 넣으세요.
//   (씬에 이미 하나 있으면 자동 생성은 하지 않습니다.)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-1000)]
public class EffectPrewarmer : MonoBehaviour
{
    public const bool AutoCreate = true;

    [Header("자동 수집")]
    [Tooltip("씬의 스크립트가 참조하는 프리팹을 자동으로 찾아 미리 그립니다.")]
    public bool autoCollect = true;
    [Tooltip("프리팹 안의 스크립트가 참조하는 프리팹까지 몇 단계 따라갈지 (적 → 적의 피격 이펙트 등)")]
    [Range(0, 4)] public int followDepth = 2;

    [Header("직접 지정")]
    public List<GameObject> extraPrefabs = new List<GameObject>();
    [Tooltip("선택: Project Settings > Graphics 에서 저장한 Shader Variant Collection")]
    public ShaderVariantCollection shaderVariants;

    [Header("속도")]
    [Tooltip("한 번에 동시에 그릴 프리팹 수")]
    [Range(1, 32)] public int perBatch = 8;
    [Tooltip("각 묶음을 몇 프레임 동안 그릴지")]
    [Range(1, 10)] public int framesPerBatch = 2;
    [Tooltip("렌더러가 이보다 많은 프리팹(맵·방 등)은 건너뜀")]
    public int skipIfRenderersOver = 300;
    public bool logResult = true;

    public static bool IsDone { get; private set; }
    public static event Action OnPrewarmFinished;

    // 이미 데운 프리팹은 다시 하지 않음 (씬을 다시 로드해도)
    static readonly HashSet<int> s_Warmed = new HashSet<int>();
    static readonly Vector3 kOrigin = new Vector3(0f, -10000f, 0f);

    // ───────────────────────── 자동 생성 ─────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!AutoCreate) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreateIfMissing();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => CreateIfMissing();

    static void CreateIfMissing()
    {
        if (FindObjectOfType<EffectPrewarmer>(true) != null) return;
        new GameObject("[EffectPrewarmer]").AddComponent<EffectPrewarmer>();
    }

    // ───────────────────────── 본체 ─────────────────────────
    IEnumerator Start()
    {
        IsDone = false;
        if (shaderVariants != null && !shaderVariants.isWarmedUp) shaderVariants.WarmUp();

        var prefabs = new List<GameObject>();
        foreach (var p in extraPrefabs) AddPrefab(p, prefabs);
        if (autoCollect) Collect(prefabs);

        if (prefabs.Count == 0) { Finish(0, 0f); yield break; }

        float t0 = Time.realtimeSinceStartup;

        // 전용 카메라 + 작은 렌더 텍스처 (플레이어 화면에는 안 보임)
        var rt = new RenderTexture(256, 256, 24) { name = "PrewarmRT" };
        var camGo = new GameObject("PrewarmCamera");
        camGo.transform.SetParent(transform, false);
        var cam = camGo.AddComponent<Camera>();
        SetupCamera(cam, rt);

        var holder = new GameObject("PrewarmHolder");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false); // 비활성 상태에서 생성 → Awake 실행 전에 스크립트 제거

        int done = 0;
        for (int i = 0; i < prefabs.Count; i += perBatch)
        {
            var spawned = new List<GameObject>();
            holder.SetActive(false);
            for (int j = i; j < Mathf.Min(i + perBatch, prefabs.Count); j++)
            {
                GameObject inst = null;
                try
                {
                    inst = Instantiate(prefabs[j], kOrigin, Quaternion.identity, holder.transform);
                    Strip(inst);
                    spawned.Add(inst);
                }
                catch (Exception e)
                {
                    if (inst != null) Destroy(inst);
                    Debug.LogWarning($"[EffectPrewarmer] {prefabs[j].name} 건너뜀: {e.Message}");
                }
            }
            holder.SetActive(true);
            foreach (var go in spawned)
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    if (!ps.isPlaying) ps.Play(false);

            for (int f = 0; f < framesPerBatch; f++) yield return null;

            foreach (var go in spawned) Destroy(go);
            done += spawned.Count;
        }

        Destroy(holder);
        cam.targetTexture = null;
        Destroy(camGo);
        rt.Release();
        Destroy(rt);
        Finish(done, Time.realtimeSinceStartup - t0);
    }

    void Finish(int count, float sec)
    {
        IsDone = true;
        if (logResult) Debug.Log($"[EffectPrewarmer] 프리팹 {count}개 미리 그리기 완료 ({sec:0.00}초)");
        OnPrewarmFinished?.Invoke();
        if (gameObject.name == "[EffectPrewarmer]") Destroy(gameObject);
        else enabled = false;
    }

    void SetupCamera(Camera cam, RenderTexture rt)
    {
        Camera main = Camera.main;
        if (main != null) cam.CopyFrom(main);
        cam.targetTexture = rt;
        cam.cullingMask = ~0;
        cam.depth = -100;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.transform.position = kOrigin + new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        if (cam.orthographic) cam.orthographicSize = Mathf.Max(cam.orthographicSize, 6f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;

        // 메인 카메라와 같은 URP 렌더러(Renderer Feature 포함)를 쓰게 맞춤
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Base;
        data.renderShadows = true;
        if (main != null)
        {
            var mainData = main.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = mainData.renderPostProcessing;
            var f = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null)
            {
                int idx = (int)f.GetValue(mainData);
                if (idx >= 0) data.SetRenderer(idx);
            }
        }
    }

    // 게임 로직이 돌지 않도록 스크립트·물리·소리·카메라·조명을 떼어냄 (렌더러·파티클·애니메이터만 남김)
    static void Strip(GameObject root)
    {
        // 스크립트끼리 RequireComponent 로 묶여 있을 수 있어,
        // [2026-11-07] 다른 부품이 '반드시 필요'로 잡고 있는 부품은 그 차례에 건너뛰고 다음 반복에서 지움
        // (순서 없이 지우면 "Can't remove ... because ... depends on it" 에러가 콘솔에 찍힘)
        for (int pass = 0; pass < 5; pass++)
        {
            int removed = 0;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || IsRequiredByOther(mb)) continue;
                DestroyImmediate(mb);
                removed++;
            }
            if (removed == 0) break;
        }

        DestroyAll<Joint2D>(root); DestroyAll<Joint>(root);
        DestroyAll<Collider2D>(root); DestroyAll<Collider>(root);
        DestroyAll<Rigidbody2D>(root); DestroyAll<Rigidbody>(root);
        DestroyAll<AudioSource>(root); DestroyAll<AudioListener>(root);
        DestroyAll<Camera>(root); DestroyAll<Light>(root);

        // 애니메이터는 애니메이션 이벤트·StateMachineBehaviour 가 게임 코드를 부를 수 있어 끔
        foreach (var a in root.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (var a in root.GetComponentsInChildren<Animation>(true)) a.enabled = false;
    }

    // 같은 오브젝트의 다른 부품이 [RequireComponent] 로 이 부품의 형식을 요구하는지
    static bool IsRequiredByOther(Component target)
    {
        System.Type t = target.GetType();
        foreach (var other in target.GetComponents<Component>())
        {
            if (other == null || other == target) continue;
            var attrs = (RequireComponent[])System.Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true);
            foreach (var rc in attrs)
            {
                if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) ||
                    (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) ||
                    (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t)))
                    return true;
            }
        }
        return false;
    }

    static void DestroyAll<T>(GameObject root) where T : Component
    {
        foreach (var c in root.GetComponentsInChildren<T>(true))
            if (c != null) DestroyImmediate(c);
    }

    // ───────────────────────── 프리팹 자동 수집 ─────────────────────────
    void Collect(List<GameObject> outList)
    {
        var visitedObj = new HashSet<int>();
        var frontier = new List<UnityEngine.Object>();
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
            if (mb != null && IsUserType(mb.GetType())) frontier.Add(mb);

        for (int depth = 0; depth <= followDepth && frontier.Count > 0; depth++)
        {
            var found = new List<GameObject>();
            foreach (var o in frontier) Scan(o, found, visitedObj, 0);

            var next = new List<UnityEngine.Object>();
            foreach (var go in found)
            {
                if (!AddPrefab(go, outList)) continue;
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && IsUserType(mb.GetType())) next.Add(mb);
            }
            frontier = next;
        }
    }

    bool AddPrefab(GameObject go, List<GameObject> outList)
    {
        if (go == null) return false;
        if (go.scene.IsValid()) return false;            // 씬 오브젝트는 제외 (프리팹 에셋만)
        int id = go.GetInstanceID();
        if (s_Warmed.Contains(id)) return false;
        int rc = go.GetComponentsInChildren<Renderer>(true).Length;
        if (rc == 0 || rc > skipIfRenderersOver) { s_Warmed.Add(id); return false; }
        s_Warmed.Add(id);
        outList.Add(go);
        return true;
    }

    static bool IsUserType(Type t)
    {
        string ns = t.Namespace ?? "";
        string asm = t.Assembly.GetName().Name;
        if (asm.StartsWith("Unity") || asm.StartsWith("spine") || asm.StartsWith("Spine")) return false;
        if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") || ns.StartsWith("Spine") ||
            ns.StartsWith("Cinemachine")) return false;
        return t != typeof(EffectPrewarmer);
    }

    const BindingFlags kFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static void Scan(object obj, List<GameObject> found, HashSet<int> visited, int depth)
    {
        if (obj == null || depth > 6) return;
        if (obj is UnityEngine.Object uo)
        {
            if (uo == null) return;
            if (!visited.Add(uo.GetInstanceID())) return;
        }

        for (Type t = obj.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject)
             && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(kFieldFlags | BindingFlags.DeclaredOnly))
            {
                if (f.IsNotSerialized) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                object v;
                try { v = f.GetValue(obj); } catch { continue; }
                Visit(v, found, visited, depth);
            }
        }
    }

    static void Visit(object v, List<GameObject> found, HashSet<int> visited, int depth)
    {
        if (v == null) return;
        switch (v)
        {
            case GameObject go:
                if (go != null) found.Add(go);
                return;
            case Component c:
                if (c != null && !c.gameObject.scene.IsValid()) found.Add(c.gameObject);
                return;
            case ScriptableObject so:
                if (so != null) Scan(so, found, visited, depth + 1);
                return;
            case UnityEngine.Object _:
                return; // 머티리얼·텍스처 등은 무시
            case string _:
                return;
            case Delegate _:
                return;
            case System.Collections.IList list:
                foreach (var e in list) Visit(e, found, visited, depth + 1);
                return;
        }
        var vt = v.GetType();
        if (vt.IsPrimitive || vt.IsEnum) return;
        if (vt.IsSerializable && (vt.IsClass || (vt.IsValueType && !vt.IsPrimitive)) &&
            vt.Namespace?.StartsWith("UnityEngine") != true)
            Scan(v, found, visited, depth + 1);
    }
}
