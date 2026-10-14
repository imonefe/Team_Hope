using UnityEngine;

/// <summary>
/// HP 비율 구간마다 지정한 색으로 이 오브젝트의 Light 색을 서서히 바꾼다.
/// 라이트 오브젝트 자신에게 붙이고, targetStats에 플레이어의 Stats 컴포넌트를 연결해서 사용한다.
/// (화면 전체 색감/비네트는 이 스크립트가 아니라 플레이어에 붙는 HealthDangerEffect.cs가 담당한다.)
/// </summary>
[RequireComponent(typeof(Light))]
public class LightHpColorizer : MonoBehaviour
{
    [Header("HP를 확인할 대상")]
    [Tooltip("플레이어의 Stats 컴포넌트를 드래그해서 연결")]
    public Stats targetStats;

    [Header("HP 비율 구간별 목표 색")]
    [Tooltip("hpRatio는 0(HP 0%) ~ 1(HP 100%). hpRatio가 작은 순서로 등록하면 그 사이 구간은 자동으로 색이 부드럽게 섞인다.\n" +
             "비워두면 기본값(HP 0%=빨강, 30%=주황, 100%=이 라이트의 원래 색)으로 자동 채워진다.")]
    public HpColorStop[] colorStops;

    [Header("밝기도 같이 바꾸고 싶다면")]
    [Tooltip("HP 0%일 때 밝기 배율 (1 = 밝기는 그대로 두고 색만 변함)")]
    [Range(0f, 1f)] public float minIntensityRatio = 1f;

    [Header("전환 속도")]
    [Tooltip("목표 색/밝기로 부드럽게 다가가는 속도 (클수록 빨리 반응)")]
    public float lerpSpeed = 3f;

    Light lightComp;
    Color baseColor;
    float baseIntensity;
    float targetRatio = 1f;
    float currentRatio = 1f;

    void Awake()
    {
        lightComp = GetComponent<Light>();
        baseIntensity = lightComp.intensity;
        baseColor = lightComp.color;

        // colorStops를 인스펙터에서 비워뒀다면, "HP 100%일 때는 원래 색 그대로"가 되도록
        // 이 라이트의 실제 원래 색을 마지막 스탑으로 삼아 기본값을 채운다.
        // (하드코딩된 흰색을 쓰면 원래 색이 흰색이 아닌 라이트는 HP 100%에서도 색이 강제로 바뀌어버림)
        if (colorStops == null || colorStops.Length == 0)
        {
            colorStops = new HpColorStop[]
            {
                new HpColorStop { hpRatio = 0f,   color = new Color(0.9f, 0.15f, 0.1f) },  // HP 0%   : 진한 빨강
                new HpColorStop { hpRatio = 0.3f, color = new Color(0.95f, 0.55f, 0.2f) }, // HP 30%  : 주황
                new HpColorStop { hpRatio = 1f,   color = baseColor },                     // HP 100% : 이 라이트의 원래 색
            };
        }
    }

    void OnEnable()
    {
        if (targetStats != null)
            targetStats.OnHpChanged += OnHpChanged;
    }

    void OnDisable()
    {
        if (targetStats != null)
            targetStats.OnHpChanged -= OnHpChanged;
    }

    void Start()
    {
        if (targetStats != null)
            OnHpChanged(targetStats.currentHp, targetStats.maxHp);
    }

    void OnHpChanged(int cur, int max)
    {
        targetRatio = max > 0 ? (float)cur / max : 1f;
    }

    Color EvaluateColor(float ratio)
    {
        if (colorStops == null || colorStops.Length == 0)
            return baseColor;

        if (ratio <= colorStops[0].hpRatio) return colorStops[0].color;
        if (ratio >= colorStops[colorStops.Length - 1].hpRatio) return colorStops[colorStops.Length - 1].color;

        for (int i = 0; i < colorStops.Length - 1; i++)
        {
            HpColorStop a = colorStops[i];
            HpColorStop b = colorStops[i + 1];
            if (ratio >= a.hpRatio && ratio <= b.hpRatio)
            {
                float t = Mathf.InverseLerp(a.hpRatio, b.hpRatio, ratio);
                return Color.Lerp(a.color, b.color, t);
            }
        }
        return baseColor;
    }

    void Update()
    {
        currentRatio = Mathf.Lerp(currentRatio, targetRatio, Time.deltaTime * lerpSpeed);

        lightComp.color = EvaluateColor(currentRatio);
        lightComp.intensity = Mathf.Lerp(baseIntensity * minIntensityRatio, baseIntensity, currentRatio);
    }
}

/// <summary>인스펙터에서 "이 HP 비율이면 이 색" 한 줄을 나타내는 구조체.</summary>
[System.Serializable]
public struct HpColorStop
{
    [Range(0f, 1f)] public float hpRatio;
    public Color color;
}
