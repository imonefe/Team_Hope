using UnityEngine;

// [Faust 2026-11-08] 미래형 쉴드 연출: 켜질 때 디졸브로 나타남 + 프레넬 밝기 맥동 + 가끔 깜빡임
// SG_FresnelDissolve 머티리얼을 쓰는 렌더러들에 MaterialPropertyBlock으로 값만 덮어씀 (머티리얼 원본은 안 바뀜)
public class ShieldPulse : MonoBehaviour
{
    [Header("나타나기 (켜질 때 Dissolve 1 → 0)")]
    public float appearTime = 0.35f;

    [Header("밝기 맥동")]
    public float pulseSpeed = 3f;        // 초당 진동 속도
    public float pulseAmount = 0.25f;    // 0.25 = ±25%

    [Header("깜빡임 (홀로그램 느낌)")]
    public float flickerChancePerSec = 1.5f; // 초당 깜빡일 확률(횟수)
    public float flickerDepth = 0.6f;        // 깜빡일 때 밝기 몇 % 까지 떨어질지 (0.6 = 40%로)
    public float flickerTime = 0.05f;

    [Header("파손 연출 (PlayerShield 가 조절)")]
    [HideInInspector] public float damageDissolve = 0f;  // 보호막이 깎일수록 커짐 → 구멍이 뚫려 보임
    public float hitFlashAmount = 1.5f;                 // 맞을 때 순간 밝기 +150%
    public float hitFlashTime = 0.12f;
    float flashUntil, breakStart = -1f, breakDur;

    [Header("만료 경고 깜빡임 (PlayerShield 가 조절)")]
    [HideInInspector] public float blinkRate = 0f;   // 초당 깜빡임 횟수, 0 = 깜빡임 없음 [2026-11-09]
    public float blinkDepth = 0.15f;                 // 꺼진 순간 밝기 (0.15 = 15%)
    bool quietFade;                                  // 시간 만료로 조용히 사라지는 중

    public void HitFlash() { flashUntil = Time.time + hitFlashTime; }
    /// 시간 만료: 조각·번쩍임·부풀기 없이 디졸브로만 사라짐 [2026-11-09]
    public void FadeAway(float time) { quietFade = true; blinkRate = 0f; breakStart = Time.time; breakDur = Mathf.Max(0.01f, time); }
    public void BreakAway(float time) { breakStart = Time.time; breakDur = Mathf.Max(0.01f, time); }

    Renderer[] rends;
    float[] baseIntensity;
    MaterialPropertyBlock mpb;
    float t0, flickerUntil;
    Vector3 baseScale;
    public float breakPopScale = 0.15f;   // 깨질 때 순간 부풀기 (+15%)
    static readonly int ID_Int = Shader.PropertyToID("_FresnelIntensity");
    static readonly int ID_Dis = Shader.PropertyToID("_Dissolve");

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>(true);
        baseIntensity = new float[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            var m = rends[i].sharedMaterial;
            baseIntensity[i] = m != null && m.HasProperty(ID_Int) ? m.GetFloat(ID_Int) : 1f;
        }
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable() { t0 = Time.time; baseScale = transform.localScale; }

    void Update()
    {
        float t = Time.time - t0;
        float dissolve = appearTime > 0f ? 1f - Mathf.Clamp01(t / appearTime) : 0f;
        dissolve = Mathf.Max(dissolve, damageDissolve);
        if (breakStart >= 0f) dissolve = Mathf.Max(dissolve, Mathf.Clamp01((Time.time - breakStart) / breakDur));
        if (flickerChancePerSec > 0f && Random.value < flickerChancePerSec * Time.deltaTime) flickerUntil = Time.time + flickerTime;
        float flick = Time.time < flickerUntil ? 1f - flickerDepth : 1f;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        if (Time.time < flashUntil) pulse *= 1f + hitFlashAmount;
        if (blinkRate > 0f && Mathf.Repeat(Time.time * blinkRate, 1f) >= 0.5f) flick *= blinkDepth;   // 만료 경고 깜빡임
        if (breakStart >= 0f && !quietFade && Random.value < 0.5f) flick *= 0.4f;   // 깨질 때 심하게 깜빡임
        if (breakStart >= 0f && !quietFade)
        {
            float bk = Mathf.Clamp01((Time.time - breakStart) / breakDur);
            transform.localScale = baseScale * (1f + breakPopScale * Mathf.Sin(bk * Mathf.PI * 0.5f));
            pulse *= 1f + 2f * (1f - bk);   // 깨지는 순간 번쩍
        }
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            rends[i].GetPropertyBlock(mpb);
            mpb.SetFloat(ID_Int, baseIntensity[i] * pulse * flick);
            mpb.SetFloat(ID_Dis, dissolve);
            rends[i].SetPropertyBlock(mpb);
        }
    }
}
