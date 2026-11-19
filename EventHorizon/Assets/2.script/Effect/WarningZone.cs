using UnityEngine;

// [Faust 2026-11-17] 보스 패턴 경고 박스
// 반투명 박스 + 위아래 WARNING 글자 띠(흐름) + 좌우 사선 줄무늬 + 약한 글리치. 그림은 셰이더(SG_WarningZone)가 그림.
// 이 스크립트는 ① 박스 크기를 셰이더에 넘겨 글자가 늘어나지 않게 하고 ② 등장·깜빡임·퇴장 타이밍만 맡음.
// 박스 크기 = 루트 오브젝트 Scale X·Y (월드 단위). 예: Scale (6, 3, 1) → 가로 6, 세로 3.
[ExecuteAlways]
public class WarningZone : MonoBehaviour
{
    public Renderer boxRenderer;                 // 비우면 자식에서 찾음

    [Header("시간 (초)")]
    public float fadeIn = 0.12f;
    public float duration = 1.2f;                // 경고 유지 시간 (0 = Hide() 부를 때까지 계속)
    public float fadeOut = 0.15f;
    public bool destroyOnEnd = true;

    [Header("깜빡임")]
    public float blinkSpeed = 4f;                // 초당 깜빡임 수
    [Range(0, 1)] public float blinkDepth = 0.3f;
    public float blinkFasterAtEnd = 2.5f;        // 끝날 무렵 깜빡임 배속 (공격 직전 재촉)

    [Header("글리치")]
    public float burstGlitch = 0.12f;            // 등장 순간 가로 밀림 (평소 세기는 머티리얼 GlitchStrength)
    public float burstTime = 0.15f;

    static readonly int BoxSizeId = Shader.PropertyToID("_BoxSize");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int GlitchId = Shader.PropertyToID("_GlitchStrength");

    MaterialPropertyBlock mpb;
    float t, hideAt = -1f;

    // 코드에서 쓰기: WarningZone.Spawn(경고프리팹, 중심, new Vector2(가로, 세로), 1.2f);
    public static WarningZone Spawn(GameObject prefab, Vector3 center, Vector2 size, float duration)
    {
        var go = Instantiate(prefab, center, Quaternion.identity);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var wz = go.GetComponent<WarningZone>();
        if (wz != null) wz.duration = duration;
        return wz;
    }

    public void Hide() { if (hideAt < 0f) hideAt = t; }

    void OnEnable() { t = 0f; hideAt = -1f; Apply(); }

    void Update()
    {
        if (Application.isPlaying)
        {
            t += Time.deltaTime;
            if (hideAt < 0f && duration > 0f && t >= fadeIn + duration) hideAt = t;
            if (hideAt >= 0f && t >= hideAt + fadeOut)
            {
                if (destroyOnEnd) { Destroy(gameObject); return; }
                gameObject.SetActive(false); return;
            }
        }
        Apply();
    }

    void Apply()
    {
        if (boxRenderer == null) boxRenderer = GetComponentInChildren<Renderer>();
        if (boxRenderer == null || boxRenderer.sharedMaterial == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        boxRenderer.GetPropertyBlock(mpb);

        Vector3 s = boxRenderer.transform.lossyScale;
        mpb.SetVector(BoxSizeId, new Vector4(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f, 0f));

        float op = 1f;
        Vector4 g = boxRenderer.sharedMaterial.GetVector(GlitchId);
        if (Application.isPlaying)
        {
            op = fadeIn > 0f ? Mathf.Clamp01(t / fadeIn) : 1f;
            if (hideAt >= 0f) op *= fadeOut > 0f ? 1f - Mathf.Clamp01((t - hideAt) / fadeOut) : 0f;
            float speed = blinkSpeed;
            if (duration > 0f) speed *= Mathf.Lerp(1f, blinkFasterAtEnd, Mathf.Clamp01((t - fadeIn) / duration));
            op *= 1f - blinkDepth * (0.5f + 0.5f * Mathf.Sin(t * speed * Mathf.PI * 2f));
            if (burstTime > 0f && t < burstTime) g.x += burstGlitch * (1f - t / burstTime);
        }
        mpb.SetFloat(OpacityId, op);
        mpb.SetVector(GlitchId, g);
        boxRenderer.SetPropertyBlock(mpb);
    }
}
