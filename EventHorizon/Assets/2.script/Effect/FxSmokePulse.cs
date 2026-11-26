using UnityEngine;

/// <summary>
/// [2026-11-27] 흡입식 강화제 JJW · 가스 구름 — 과열이 들어가는 박자(0.5초)마다 구름 속을 달아오르게 한다.
///  AugmentRuntime.GasCloud 가 과열을 넣을 때마다 Pulse() 를 부름. 셰이더 EventHorizon/FX/ToonSmokePuff 의 _Pulse 값을 넣음.
///  프리팹은 Editor/AugmentFxV2Builder (KW2_Gas_Cloud).
/// </summary>
public class FxSmokePulse : MonoBehaviour
{
    public Renderer[] renderers;
    [Tooltip("달아오른 뒤 식는 시간 (초)")] public float coolTime = 0.6f;
    [Tooltip("키워드 색 (보통 색)")] public Color color = new Color(0.91f, 0.33f, 0.16f, 1f);

    static readonly int PPulse = Shader.PropertyToID("_Pulse"), PRatio = Shader.PropertyToID("_Ratio");
    MaterialPropertyBlock mpb;
    Vector4 ratio;
    float since = 99f;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        Color l = color.linear;
        float mx = Mathf.Max(0.0001f, Mathf.Max(l.r, Mathf.Max(l.g, l.b)));
        ratio = new Vector4(l.r / mx, l.g / mx, l.b / mx, 1f);
        Set(0f);
    }

    public void Pulse() { since = 0f; }

    void LateUpdate()
    {
        since += Time.deltaTime;
        float k = Mathf.Clamp01(since / Mathf.Max(0.01f, coolTime));
        Set(Mathf.Pow(1f - k, 1.5f));
    }

    void Set(float v)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(PPulse, v); mpb.SetVector(PRatio, ratio);
            r.SetPropertyBlock(mpb);
        }
    }
}
