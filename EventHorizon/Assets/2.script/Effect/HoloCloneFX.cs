using UnityEngine;

// [Faust 2026-11-09] 홀로그램 분신 한 장의 등장 → 유지 → 퇴장 연출 (SG_Hologram 머티리얼 값을 MaterialPropertyBlock 으로 조절)
// 분신 프리팹 루트에 붙임. 자식 SpriteRenderer 들에 적용.
public class HoloCloneFX : MonoBehaviour
{
    [Header("시간 (초)")]
    public float appearTime = 0.06f;      // 글리치 세게 → 정상, 투명 → 보임
    public float holdTime = 0.16f;
    public float fadeTime = 0.1f;         // 다시 글리치 세게 + 사라짐

    [Header("글리치")]
    public float baseGlitch = 0.06f;      // 평소 가로 밀림 (머티리얼 GlitchStrength.x 와 같은 의미)
    public float burstGlitch = 0.35f;     // 등장·퇴장 때 밀림
    [Range(0, 1)] public float flickerChance = 0.15f; // 프레임마다 깜빡일 확률
    public float flickerOpacity = 0.4f;

    [Header("이동")]
    public float slideDistance = 0.4f;    // 사는 동안 앞(로컬 +X)으로 미끄러지는 거리

    static readonly int ID_Op = Shader.PropertyToID("_Opacity");
    static readonly int ID_GS = Shader.PropertyToID("_GlitchStrength");
    SpriteRenderer[] rends;
    MaterialPropertyBlock mpb;
    float t0;
    Vector3 startPos;

    void OnEnable()
    {
        rends = GetComponentsInChildren<SpriteRenderer>(true);
        mpb = new MaterialPropertyBlock();
        t0 = Time.time;
        startPos = transform.position;
        Apply(0f, burstGlitch);
    }

    void Update()
    {
        float t = Time.time - t0;
        float total = appearTime + holdTime + fadeTime;
        float op, gl;
        if (t < appearTime) { float k = t / Mathf.Max(appearTime, 1e-4f); op = k; gl = Mathf.Lerp(burstGlitch, baseGlitch, k); }
        else if (t < appearTime + holdTime) { op = 1f; gl = baseGlitch; }
        else if (t < total) { float k = (t - appearTime - holdTime) / Mathf.Max(fadeTime, 1e-4f); op = 1f - k; gl = Mathf.Lerp(baseGlitch, burstGlitch, k); }
        else { Apply(0f, baseGlitch); enabled = false; return; }
        if (Random.value < flickerChance) op *= flickerOpacity;
        Apply(op, gl);
        // 로컬 +X(바라보는 방향)로 미끄러짐 — 좌우 반전된 경우 lossyScale 부호를 따름
        float dir = Mathf.Sign(transform.lossyScale.x);
        transform.position = startPos + Vector3.right * dir * slideDistance * Mathf.Clamp01(t / total);
    }

    void Apply(float opacity, float glitch)
    {
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(ID_Op, opacity);
            mpb.SetVector(ID_GS, new Vector4(glitch, 0, 0, 0));
            r.SetPropertyBlock(mpb);
        }
    }
}
