using System.Collections;
using UnityEngine;

// [Faust 2026-11-08] 쉴드 피격/파괴 연출
//  - Ripple(dir): 맞은 쪽 쉴드 표면에 프레넬 링 파문 (쉴드 메쉬를 한 겹 더 그려서 RingTex 를 맞은 자리로 옮겨 퍼뜨림)
//  - Shatter(): 유리처럼 깨지는 조각 이펙트 프리팹 생성
// FX_Shield_Future 루트에 붙음. PlayerShield 가 불러 줌.
public class ShieldImpactFX : MonoBehaviour
{
    [Header("피격 파문")]
    public Material rippleMaterial;          // M_ShieldHitRipple (SG_FresnelDissolve + 링 텍스처)
    public Mesh rippleMesh;                  // 비우면 자식 MeshFilter 메쉬 사용
    public float rippleScale = 1.04f;        // 쉴드 층보다 살짝 크게
    public int rippleSortingOrder = 5;
    public float rippleTime = 0.4f;
    public float startRadius = 0.03f;        // UV 기준 (세로 0~1 = 반구 아래~위)
    public float endRadius = 0.5f;
    public float rippleIntensity = 6f;       // 처음 밝기 → 0 으로
    [Range(0.5f, 1f)] public float hitEdgeU = 0.56f; // 0.5 = 가장자리, 0.75 = 정중앙
    public float verticalJitter = 0.15f;     // 맞는 높이 랜덤 폭

    [Header("파괴")]
    public GameObject shatterPrefab;         // FX_ShieldShatter

    static readonly int ID_Til = Shader.PropertyToID("_RingTiling");
    static readonly int ID_Off = Shader.PropertyToID("_RingOffset");
    static readonly int ID_Spd = Shader.PropertyToID("_RingScrollSpeed");
    static readonly int ID_Int = Shader.PropertyToID("_FresnelIntensity");
    static readonly int ID_Dis = Shader.PropertyToID("_Dissolve");

    /// dir = 넉백 방향(Health.lastHitDir). 공격은 그 반대쪽에서 들어온 것으로 봄. 0 이면 랜덤.
    public void Ripple(int dir)
    {
        if (rippleMaterial == null) return;
        if (rippleMesh == null) { var mf = GetComponentInChildren<MeshFilter>(); if (mf) rippleMesh = mf.sharedMesh; }
        if (rippleMesh == null) return;
        float side = dir != 0 ? -Mathf.Sign(dir) : (Random.value < 0.5f ? -1f : 1f);
        // 월드 기준 왼/오른쪽 → 쉴드 로컬(플레이어가 뒤집혀 있어도 맞게)
        float localX = transform.InverseTransformPoint(transform.position + Vector3.right * side).x;
        float u = localX < 0f ? hitEdgeU : 1.5f - hitEdgeU;   // 반구 UV: 0.5 = 로컬 -X, 1 = +X
        float v = 0.5f + Random.Range(-verticalJitter, verticalJitter);
        StartCoroutine(RippleRoutine(new Vector2(u, v)));
    }

    IEnumerator RippleRoutine(Vector2 hit)
    {
        var go = new GameObject("shield_hit_ripple");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * rippleScale;
        go.AddComponent<MeshFilter>().sharedMesh = rippleMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = rippleMaterial;
        mr.sortingOrder = rippleSortingOrder;
        var mpb = new MaterialPropertyBlock();
        float t = 0f;
        while (t < rippleTime && go != null)
        {
            float k = t / rippleTime;
            float r = Mathf.Lerp(startRadius, endRadius, 1f - (1f - k) * (1f - k));   // 빨리 퍼지다 느려짐
            // 링 텍스처 중심(0.5)을 맞은 자리로, 반지름 r 만큼 크게. 가로는 반구에서 0.5 = 180도라 2배
            Vector2 s = new Vector2(1f / r, 0.5f / r);
            mpb.SetVector(ID_Til, s);
            mpb.SetVector(ID_Off, new Vector2(0.5f - hit.x * s.x, 0.5f - hit.y * s.y));
            mpb.SetVector(ID_Spd, Vector2.zero);
            mpb.SetFloat(ID_Int, rippleIntensity * (1f - k) * (1f - k));
            mpb.SetFloat(ID_Dis, 0f);
            mr.SetPropertyBlock(mpb);
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    public void Shatter()
    {
        if (shatterPrefab == null) return;
        var fx = Instantiate(shatterPrefab, transform.position, Quaternion.identity);
        // 쉴드 크기에 맞춰 조각 퍼지는 구 크기 조절 (프리팹은 쉴드 크기 2.4 기준)
        float s = Mathf.Abs(transform.lossyScale.x) / 2.4f;
        if (s > 0f) fx.transform.localScale = Vector3.one * s;
    }
}
