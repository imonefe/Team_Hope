using UnityEngine;

/// <summary>
/// HP 비율 구간마다 지정한 색으로 씬 전체 Fog(RenderSettings.fogColor)를 서서히 바꾼다.
/// LightHpColorizer.cs와 완전히 같은 방식 — 아무 오브젝트에나 붙이고 targetStats에
/// 플레이어의 Stats 컴포넌트를 연결해서 사용한다.
/// (Lighting 창 Environment 탭에서 Fog 체크가 켜져 있어야 화면에 실제로 보임)
/// </summary>
public class FogHpColorizer : MonoBehaviour
{
    [Header("HP를 확인할 대상")]
    [Tooltip("플레이어의 Stats 컴포넌트를 드래그해서 연결")]
    public Stats targetStats;

    [Header("HP 비율 구간별 목표 Fog 색")]
    [Tooltip("hpRatio는 0(HP 0%) ~ 1(HP 100%). hpRatio가 작은 순서로 등록하면 그 사이 구간은 자동으로 색이 부드럽게 섞인다.\n" +
             "비워두면 기본값(HP 0%=빨강, 30%=주황, 100%=원래 Fog 색)으로 자동 채워진다.")]
    public HpColorStop[] colorStops;

    [Header("전환 속도")]
    [Tooltip("목표 색으로 부드럽게 다가가는 속도 (클수록 빨리 반응)")]
    public float lerpSpeed = 3f;

    Color baseFogColor;
    float targetRatio = 1f;
    float currentRatio = 1f;

    void Awake()
    {
        // 시작할 때의 Fog 색을 "원래 색"으로 기억해둔다.
        baseFogColor = RenderSettings.fogColor;

        // colorStops를 인스펙터에서 비워뒀다면, "HP 100%일 때는 원래 색 그대로"가 되도록
        // 실제 Fog의 원래 색을 마지막 스탑으로 삼아 기본값을 채운다.
        if (colorStops == null || colorStops.Length == 0)
        {
            colorStops = new HpColorStop[]
            {
                new HpColorStop { hpRatio = 0f,   color = new Color(0.9f, 0.15f, 0.1f) },  // HP 0%   : 진한 빨강
                new HpColorStop { hpRatio = 0.3f, color = new Color(0.95f, 0.55f, 0.2f) }, // HP 30%  : 주황
                new HpColorStop { hpRatio = 1f,   color = baseFogColor },                  // HP 100% : 원래 Fog 색
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

        // 이 컴포넌트가 꺼지면 Fog 색을 원래대로 되돌려둔다.
        RenderSettings.fogColor = baseFogColor;
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
            return baseFogColor;

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
        return baseFogColor;
    }

    void Update()
    {
        currentRatio = Mathf.Lerp(currentRatio, targetRatio, Time.deltaTime * lerpSpeed);
        RenderSettings.fogColor = EvaluateColor(currentRatio);
    }
}
