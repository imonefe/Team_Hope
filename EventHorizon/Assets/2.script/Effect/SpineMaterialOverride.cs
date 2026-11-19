using UnityEngine;
using Spine.Unity;

/// <summary>
/// [2026-11-20 추가] 스파인(SkeletonAnimation/SkeletonRenderer) 머티리얼 바꿔치기.
/// 스파인은 매 프레임 아틀라스 머티리얼로 렌더러를 다시 설정해서 Mesh Renderer 에 머티리얼을 직접 넣으면 덮어써진다.
/// 대신 SkeletonRenderer.CustomMaterialOverride 에 "원래 머티리얼 → 바꿀 머티리얼"을 등록해 둔다.
/// 컴포넌트를 끄면 원래 머티리얼로 돌아간다.
/// </summary>
[RequireComponent(typeof(SkeletonRenderer))]
[DefaultExecutionOrder(100)]   // [2026-11-20] 스파인(기본 순서 0)보다 늦게 실행되어 마지막에 머티리얼을 확정
public class SpineMaterialOverride : MonoBehaviour
{
    [System.Serializable]
    public struct Pair
    {
        [Tooltip("스파인 아틀라스의 원래 머티리얼 (예: archer_idle_Material)")]
        public Material original;
        [Tooltip("대신 쓸 머티리얼 (MainTex 에 같은 아틀라스 텍스처를 넣어 둘 것)")]
        public Material replacement;
    }

    public Pair[] pairs;
    SkeletonRenderer sr;

    void OnEnable()
    {
        sr = GetComponent<SkeletonRenderer>();
        if (sr == null || pairs == null) return;
        foreach (var p in pairs)
            if (p.original != null && p.replacement != null) sr.CustomMaterialOverride[p.original] = p.replacement;
        SwapNow();
    }

    // [2026-11-20] 스파인은 켜질 때(Awake) 원래 머티리얼로 메시를 한 번 만들어 두고,
    // 보스가 아직 움직이지 않아 스파인이 갱신되지 않으면 그 상태가 계속 보인다.
    // 그래서 등록과 동시에, 그리고 첫 프레임 직전(Start)에 렌더러 머티리얼을 바로 바꿔 둔다.
    void Start() => SwapNow();

    // [2026-11-20] 그래도 처음에 원래 모습이 남는 경우가 있어서(스파인이 초기화하면서 등록을 잃는 등),
    // 매 프레임 스파인이 그린 뒤에 등록이 살아 있는지 확인하고 렌더러 머티리얼도 다시 맞춤.
    void LateUpdate()
    {
        if (sr == null || pairs == null) return;
        foreach (var p in pairs)
            if (p.original != null && p.replacement != null
                && (!sr.CustomMaterialOverride.TryGetValue(p.original, out var cur) || cur != p.replacement))
                sr.CustomMaterialOverride[p.original] = p.replacement;
        SwapNow();
    }

    void SwapNow()
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr == null || pairs == null) return;
        var mats = mr.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
            foreach (var p in pairs)
                if (p.original != null && p.replacement != null && mats[i] == p.original) { mats[i] = p.replacement; changed = true; }
        if (changed) mr.sharedMaterials = mats;
    }

    void OnDisable()
    {
        if (sr == null || pairs == null) return;
        foreach (var p in pairs)
            if (p.original != null) sr.CustomMaterialOverride.Remove(p.original);
    }
}
