using UnityEngine;

/// <summary>
/// [2026-11-26] 이펙트 시안 v2 프리팹의 색 입히기 (기준서 8장 방식).
///  재질은 흰색 × HDR 배수로 공유하고, 입자 시작색에 '색 비율'(가장 큰 채널 = 1)을 넣어 최종 색 = 비율 × 배수.
///  그래서 진한 부분은 1로 잘려 하얗게, 옅은 가장자리만 키워드 색으로 남는다.
///  top = 밝은 층(가산), dark = 어두운 바탕층(반투명) — 같은 색상의 아주 어두운 값.
///  프리팹은 Editor/AugmentFxV2Builder 가 만든다.
/// </summary>
public class FxTint : MonoBehaviour
{
    public ParticleSystem[] top;
    public ParticleSystem[] dark;
    [Tooltip("만들 때 정한 기본 색 (Apply 를 안 부르면 이 색)")]
    public Color baseColor = Color.white;
    [Tooltip("이 시간 뒤 스스로 지움 (0 = 지우지 않음)")]
    public float lifetime = 0f;

    void Start()
    {
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    /// <summary>키워드 색(보통 색) → 밝은 층 비율 · 어두운 층 색. alpha 는 전체 진하기</summary>
    public void Apply(Color c, float alpha = 1f)
    {
        Ratio(c, out Color r, out Color d);
        Set(top, r, alpha);
        Set(dark, d, alpha);
    }

    public static void Ratio(Color c, out Color ratio, out Color dark)
    {
        Color l = c.linear;
        float mx = Mathf.Max(0.0001f, Mathf.Max(l.r, Mathf.Max(l.g, l.b)));
        var r = new Vector3(l.r / mx, l.g / mx, l.b / mx);
        var d = new Vector3(Mathf.Max(0.006f, r.x * r.x * r.x * 0.35f), Mathf.Max(0.006f, r.y * r.y * r.y * 0.35f), Mathf.Max(0.006f, r.z * r.z * r.z * 0.35f));
        ratio = new Color(r.x, r.y, r.z, 1f).gamma;   // 입자 색은 감마로 넣으면 선형으로 바뀌어 곱해짐
        dark = new Color(d.x, d.y, d.z, 1f).gamma;
    }

    static void Set(ParticleSystem[] list, Color c, float alpha)
    {
        if (list == null) return;
        foreach (var ps in list)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startColor = new Color(c.r, c.g, c.b, alpha);
        }
    }

    /// <summary>프리팹을 꺼내 색 입히고 재생 (scale 은 루트 배율에 곱함)</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Color c, float scale = 1f, float alpha = 1f)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, rot);
        go.transform.localScale = prefab.transform.localScale * scale;
        var t = go.GetComponent<FxTint>();
        if (t != null) t.Apply(c, alpha);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(true); ps.Play(true); }
        return go;
    }
}

/// <summary>[2026-11-26] 표식 v2: 크게 나타나 조여 들고(0.22초) 맥박 · 안쪽 별빛 회전</summary>
public class FxMarkMotion : MonoBehaviour
{
    public Transform spin;
    public float appearFrom = 1.9f, appearTime = 0.22f, pulse = 0.08f, pulseSpeed = 10f, spinSpeed = 90f;
    Vector3 s0; float t;
    void Start() { s0 = transform.localScale; }
    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / appearTime); k = 1f - (1f - k) * (1f - k) * (1f - k);
        float s = Mathf.Lerp(appearFrom, 1f, k) * (1f + pulse * Mathf.Sin(t * pulseSpeed));
        transform.localScale = s0 * s;
        if (spin != null) spin.localRotation = Quaternion.Euler(0f, 0f, t * spinSpeed);
    }
}
