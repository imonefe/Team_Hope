using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-27] 가열 블레이드 검기 (만화풍 3톤 불 초승달) 움직임. 셰이더 EventHorizon/FX/ToonFireCrescent 에 값을 넣는다.
///  - 소멸 문턱: 나온 지 (수명 × 0.35) 부터 빠르게 올라 꼬리부터 녹아 사라짐 (시안 A 방식)
///  - 잔상 2장: 루트가 지나온 자리(0.015초 · 0.03초 전)에 그림
///  - 꼬리: 칼날 전체가 끌림 (속도 · 날아온 거리 · 멈춘 뒤 시간을 ToonFireTail 셰이더에 넣음)
///  루트를 옮기는 건 AugmentRuntime.SwordWave. 프리팹은 Editor/AugmentFxV2Builder (KW2_Heat_Wave), 값은 인스펙터에서 조절.
/// </summary>
public class FxToonFireDriver : MonoBehaviour
{
    public Renderer main;
    public Renderer[] ghosts;
    public float[] ghostDelay = { 0.015f, 0.03f };
    public float[] ghostAlpha = { 0.55f, 0.3f };
    [Tooltip("키워드 색 (보통 색)")] public Color color = new Color(0.91f, 0.33f, 0.16f, 1f);
    [Tooltip("뒷날 띠 색 (보통 색)")] public Color darkColor = new Color(0.63f, 0.2f, 0.1f, 1f);
    [Tooltip("몸통 수명 (초)")] public float life = 0.5f;
    public AnimationCurve threshold = new AnimationCurve(new Keyframe(0, -0.1f), new Keyframe(0.35f, 0.15f), new Keyframe(1, 1.05f));

    [Header("꼬리 (칼날 전체가 끌림 · 셰이더 ToonFireTail)")]
    public Renderer tail;
    [Tooltip("꼬리 수명 (초) — 날아가는 속도 × 수명 = 꼬리 길이")] public float tailLife = 0.14f;
    [Tooltip("끝에서 줄어드는 높이 비율 (0.25 = 끝 높이 75%)")] [Range(0f, 1f)] public float tailShrink = 0.25f;

    static readonly int PThr = Shader.PropertyToID("_Thr"), PFin = Shader.PropertyToID("_Fin"), PAlpha = Shader.PropertyToID("_Alpha"),
                        PFlow = Shader.PropertyToID("_Flow"), PRatio = Shader.PropertyToID("_Ratio"), PDark = Shader.PropertyToID("_DarkCol"),
                        PSpeed = Shader.PropertyToID("_Speed"), PTravel = Shader.PropertyToID("_Travel"), PSince = Shader.PropertyToID("_Since"),
                        PLife = Shader.PropertyToID("_Life"), PShrink = Shader.PropertyToID("_Shrink");
    MaterialPropertyBlock mpb;
    Vector4 ratio, dark;
    float t, seed;
    Vector3 start, prev; float flySpeed, since;   // 꼬리: 출발점 · 이전 위치 · 날던 속도(월드) · 멈춘 뒤 시간
    readonly List<(float t, Vector3 p)> trail = new List<(float, Vector3)>();

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        Color l = color.linear;
        float mx = Mathf.Max(0.0001f, Mathf.Max(l.r, Mathf.Max(l.g, l.b)));
        ratio = new Vector4(l.r / mx, l.g / mx, l.b / mx, 1f);
        Color d = darkColor.linear; dark = new Vector4(d.r, d.g, d.b, 1f);
        seed = Random.value * 10f;
        trail.Add((0f, transform.position));
        start = prev = transform.position;
        Apply(main, 0f, 1f);
        ApplyTail(0f);
        if (ghosts != null) foreach (var g in ghosts) if (g != null) Apply(g, -1f, 0f);
    }

    void LateUpdate()
    {
        t += Time.deltaTime;
        trail.Add((t, transform.position));
        if (trail.Count > 64) trail.RemoveAt(0);
        Apply(main, t, 1f);
        // 꼬리: 움직이면 그 속도를 기억, 멈추면 멈춘 뒤 시간을 셈
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float v = (transform.position - prev).magnitude / dt; prev = transform.position;
        if (v > 0.5f) { flySpeed = Mathf.Max(flySpeed, v); since = 0f; } else if (flySpeed > 0f) since += Time.deltaTime;
        ApplyTail(since);
        if (ghosts == null) return;
        for (int i = 0; i < ghosts.Length; i++)
        {
            var g = ghosts[i]; if (g == null) continue;
            float dly = i < ghostDelay.Length ? ghostDelay[i] : 0.02f * (i + 1);
            float tg = t - dly;
            if (tg < 0f) { Apply(g, -1f, 0f); continue; }
            // 잔상 = 본체와 같은 모양을 루트가 tg 시점에 있던 자리에 (본체의 루트 기준 위치 그대로)
            g.transform.position = PosAt(tg) + (main != null ? main.transform.position - transform.position : Vector3.zero);
            Apply(g, tg, i < ghostAlpha.Length ? ghostAlpha[i] : 0.3f);
        }
    }

    Vector3 PosAt(float time)
    {
        for (int i = trail.Count - 1; i > 0; i--)
        {
            var b = trail[i]; var a = trail[i - 1];
            if (a.t <= time) return Vector3.Lerp(a.p, b.p, Mathf.InverseLerp(a.t, b.t, time));
        }
        return trail[0].p;
    }

    void ApplyTail(float sinceStop)
    {
        if (tail == null) return;
        float sc = Mathf.Max(0.0001f, Mathf.Abs(tail.transform.lossyScale.x));   // 월드 → 꼬리 오브젝트 단위
        tail.GetPropertyBlock(mpb);
        mpb.SetVector(PRatio, ratio); mpb.SetVector(PDark, dark);
        mpb.SetFloat(PSpeed, (flySpeed > 0f ? flySpeed : 20f) / sc);
        mpb.SetFloat(PTravel, (transform.position - start).magnitude / sc);
        mpb.SetFloat(PSince, sinceStop);
        mpb.SetFloat(PLife, tailLife);
        mpb.SetFloat(PShrink, tailShrink);
        tail.SetPropertyBlock(mpb);
    }

    void Apply(Renderer r, float age, float alpha)
    {
        if (r == null) return;
        r.GetPropertyBlock(mpb);
        mpb.SetVector(PRatio, ratio); mpb.SetVector(PDark, dark);
        mpb.SetFloat(PThr, age < 0f ? 2f : threshold.Evaluate(age / Mathf.Max(0.01f, life)));
        mpb.SetFloat(PFin, Mathf.Clamp01(age / 0.04f));
        mpb.SetFloat(PAlpha, alpha);
        mpb.SetFloat(PFlow, Time.time + seed);
        r.SetPropertyBlock(mpb);
    }
}
