using UnityEngine;

/// <summary>
/// [2026-11-27] 앞으로 쏘는 원뿔 에너지 (박자 칼날). 아이템 획득 시안의 '에너지 기둥'(strike_burst 셰이더)을 옆으로 눕힌 것.
///  Body 의 x 크기 = 원뿔 길이 (뻗어 나감), 셰이더 값은 시간에 따라:
///   밝은 층 밝기 배수 6 → 30 (흩어질수록 하얗게 타며), 소멸(_disolve) 0 → 1, 무늬 흐름(_offset) 손 → 앞으로, 둘레 회전
///  프리팹은 Editor/AugmentFxV2Builder 가 만든다 (KW2_Beat_Cone). 값은 프리팹 인스펙터에서 조절.
/// </summary>
public class FxConeDriver : MonoBehaviour
{
    public Transform body;
    public Renderer top;
    public Renderer bas;
    [Tooltip("키워드 색 (보통 색)")] public Color color = new Color(0.95f, 0.76f, 0.19f, 1f);
    [Tooltip("원뿔 길이 (월드)")] public float length = 4.2f;
    [Tooltip("전체 시간 (초)")] public float duration = 0.5f;
    [Tooltip("뻗어 나가는 시간 (초)")] public float growTime = 0.12f;
    [Tooltip("둘레 회전 양 (전체 시간 동안 u)")] public float spin = 1.2f;
    [Tooltip("무늬 흐름 (+ = 손에서 앞으로)")] public float flow = 1f;
    public AnimationCurve topMult = new AnimationCurve(new Keyframe(0, 2.5f), new Keyframe(.3f, 3.5f), new Keyframe(.55f, 7f), new Keyframe(.8f, 12f), new Keyframe(1, 12f));   // 무늬(소용돌이)가 보이게 낮춤, 끝으로 갈수록 하얗게
    public AnimationCurve topDissolve = new AnimationCurve(new Keyframe(0, 0f), new Keyframe(.53f, .59f), new Keyframe(1, 1f));
    public AnimationCurve baseDissolve = new AnimationCurve(new Keyframe(0, 0.05f), new Keyframe(.53f, .7f), new Keyframe(1, 1f));   // 밝은 층보다 조금 먼저 사라짐 (어두운 층만 남아 갈색 판으로 보이지 않게)
    public AnimationCurve flowCurve = new AnimationCurve(new Keyframe(0, -.55f), new Keyframe(.53f, .15f), new Keyframe(1, .45f));

    static readonly int PTint = Shader.PropertyToID("_tint"), PDis = Shader.PropertyToID("_disolve"),
                        POff = Shader.PropertyToID("_offset"), PNoise = Shader.PropertyToID("_noisescale");
    MaterialPropertyBlock mpb;
    Vector3 ratio, dark;
    float t;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        Color l = color.linear;
        float mx = Mathf.Max(0.0001f, Mathf.Max(l.r, Mathf.Max(l.g, l.b)));
        ratio = new Vector3(l.r / mx, l.g / mx, l.b / mx);
        dark = new Vector3(Mathf.Max(.006f, ratio.x * ratio.x * ratio.x * .35f), Mathf.Max(.006f, ratio.y * ratio.y * ratio.y * .35f), Mathf.Max(.006f, ratio.z * ratio.z * ratio.z * .35f));
        Apply(0f);
    }

    void Update()
    {
        t += Time.deltaTime;
        Apply(t);
    }

    void Apply(float time)
    {
        float tp = Mathf.Clamp01(time / Mathf.Max(0.01f, duration));
        // 뻗어 나감: 길이 0.25 → 1, 굵기 0.5 → 1 (빠르게 시작해 감속)
        float g = Mathf.Clamp01(time / Mathf.Max(0.01f, growTime)); g = 1f - (1f - g) * (1f - g) * (1f - g);
        if (body != null) body.localScale = new Vector3(length * Mathf.Lerp(0.25f, 1f, g), Mathf.Lerp(0.5f, 1f, g), Mathf.Lerp(0.5f, 1f, g));
        var off = new Vector4(tp * spin, flowCurve.Evaluate(tp) * flow, 0f, 0f);
        if (top != null)
        {
            float m = topMult.Evaluate(tp);
            top.GetPropertyBlock(mpb);
            mpb.SetColor(PTint, new Color(ratio.x * m, ratio.y * m, ratio.z * m, 1f).gamma);   // 재질 색은 선형으로 바뀌어 들어가므로 감마로 넘김
            mpb.SetFloat(PDis, topDissolve.Evaluate(tp));
            mpb.SetVector(POff, off);
            mpb.SetFloat(PNoise, 10.5f + 31f * tp);
            top.SetPropertyBlock(mpb);
        }
        if (bas != null)
        {
            bas.GetPropertyBlock(mpb);
            mpb.SetColor(PTint, new Color(dark.x, dark.y, dark.z, 1f).gamma);
            mpb.SetFloat(PDis, baseDissolve.Evaluate(tp));
            mpb.SetVector(POff, off);
            bas.SetPropertyBlock(mpb);
        }
    }
}
