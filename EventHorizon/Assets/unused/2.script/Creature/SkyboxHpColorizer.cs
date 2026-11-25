using UnityEngine;

/// <summary>
/// HP 비율 구간마다 지정한 색으로 스카이박스 머티리얼의 Tint Colour를 서서히 바꾼다.
/// LightHpColorizer / FogHpColorizer와 완전히 같은 방식 — 아무 오브젝트에나 붙이고 targetStats에
/// 플레이어의 Stats 컴포넌트를 연결해서 사용한다.
///
/// 주의: RenderSettings.skybox는 프로젝트의 실제 머티리얼 에셋을 그대로 가리키기 때문에,
/// 스크립트로 직접 값을 바꾸면 에셋 파일 자체가 바뀔 수 있다. 그래서 시작할 때 복제본을 만들어
/// RenderSettings.skybox를 그 복제본으로 바꿔치기하고, 복제본만 조작한다 (원본 .mat 파일은 안전함).
///
/// (이 프로젝트는 Main Camera가 2D Renderer를 쓰고 있어서 Main Camera 화면에는 스카이박스가
///  원래 안 보인다 — 3D 렌더러를 쓰는 카메라(Camera_3DRenderer_Player)에서만 보인다는 점 참고.)
/// </summary>
public class SkyboxHpColorizer : MonoBehaviour
{
    [Header("HP를 확인할 대상")]
    [Tooltip("플레이어의 Stats 컴포넌트를 드래그해서 연결")]
    public Stats targetStats;

    [Header("HP 비율 구간별 목표 Tint 색")]
    [Tooltip("hpRatio는 0(HP 0%) ~ 1(HP 100%). hpRatio가 작은 순서로 등록하면 그 사이 구간은 자동으로 색이 부드럽게 섞인다.\n" +
             "비워두면 기본값(HP 0%=빨강, 30%=주황, 100%=원래 Tint 색)으로 자동 채워진다.")]
    public HpColorStop[] colorStops;

    [Header("전환 속도")]
    [Tooltip("목표 색으로 부드럽게 다가가는 속도 (클수록 빨리 반응)")]
    public float lerpSpeed = 3f;

    const string TintProperty = "_Tint";

    Material skyboxInstance;
    Material originalSkybox;   // [2026-10-10] 파괴될 때 되돌릴 원본 스카이박스 에셋
    Color baseTint = Color.gray;
    float targetRatio = 1f;
    float currentRatio = 1f;

    void Awake()
    {
        if (RenderSettings.skybox != null)
        {
            // 원본 스카이박스 에셋을 건드리지 않도록, 런타임 전용 복제본을 만들어 그걸로 교체한다.
            originalSkybox = RenderSettings.skybox;
            skyboxInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxInstance;

            if (skyboxInstance.HasProperty(TintProperty))
                baseTint = skyboxInstance.GetColor(TintProperty);
        }

        // colorStops를 인스펙터에서 비워뒀다면, "HP 100%일 때는 원래 색 그대로"가 되도록
        // 실제 스카이박스의 원래 Tint 색을 마지막 스탑으로 삼아 기본값을 채운다.
        if (colorStops == null || colorStops.Length == 0)
        {
            colorStops = new HpColorStop[]
            {
                new HpColorStop { hpRatio = 0f,   color = new Color(0.9f, 0.15f, 0.1f) },  // HP 0%   : 진한 빨강
                new HpColorStop { hpRatio = 0.3f, color = new Color(0.95f, 0.55f, 0.2f) }, // HP 30%  : 주황
                new HpColorStop { hpRatio = 1f,   color = baseTint },                      // HP 100% : 원래 Tint 색
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

        if (skyboxInstance != null && skyboxInstance.HasProperty(TintProperty))
            skyboxInstance.SetColor(TintProperty, baseTint);
    }

    // [2026-10-10 추가] 만든 복제본을 정리 (예전엔 재시작할 때마다 하나씩 쌓였음)
    void OnDestroy()
    {
        if (skyboxInstance == null) return;
        if (RenderSettings.skybox == skyboxInstance) RenderSettings.skybox = originalSkybox;
        Destroy(skyboxInstance);
        skyboxInstance = null;
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
            return baseTint;

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
        return baseTint;
    }

    void Update()
    {
        if (skyboxInstance == null || !skyboxInstance.HasProperty(TintProperty)) return;

        currentRatio = Mathf.Lerp(currentRatio, targetRatio, Time.deltaTime * lerpSpeed);
        skyboxInstance.SetColor(TintProperty, EvaluateColor(currentRatio));
    }
}
