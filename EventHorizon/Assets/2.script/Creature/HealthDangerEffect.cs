using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// HP가 낮아질수록 화면에 붉은 비네트 + 색 보정을 씌워 긴박한 분위기를 연출한다.
/// 실제 색/강도 값은 코드가 아니라 전용 Volume Profile(에디터에서 만든 것)에 들어있고,
/// 이 스크립트는 그 Volume의 weight(0~1, 영향력)만 HP 비율에 맞춰 조절한다.
///
/// (3D 씬 라이트의 색 변화는 이 스크립트가 아니라, 각 라이트 오브젝트에 직접 붙이는
///  LightHpColorizer.cs가 맡는다 — 플레이어의 Stats를 참조해서 라이트 스스로 반응함.)
///
/// 사용법 (자세한 단계는 튜토리얼 문서 참고):
/// 1) 씬에 빈 GameObject를 만들고 Volume 컴포넌트를 추가 (Is Global 체크, Priority는 기존 Global Volume보다 높게, Weight는 0으로 시작).
/// 2) 그 Volume에 쓸 새 Volume Profile을 만들고 Vignette + Color Adjustments 오버라이드를 추가해 원하는 "위험 색감"을 세팅.
/// 3) 이 스크립트를 Stats가 붙은 오브젝트(플레이어)에 붙이고, dangerVolume에 1번에서 만든 Volume을 연결.
/// </summary>
[RequireComponent(typeof(Stats))]
public class HealthDangerEffect : MonoBehaviour
{
    [Header("연출용 Volume (전용 Low-HP Volume — Global Volume과는 별개로 새로 만들 것)")]
    public Volume dangerVolume;

    [Header("HP 비율 기준")]
    [Tooltip("이 비율 이하로 떨어지면 효과가 서서히 시작됨 (0.3 = HP 30%)")]
    [Range(0f, 1f)] public float safeThreshold = 0.3f;

    [Tooltip("이 비율 이하에서는 심장박동처럼 깜빡임(pulse)이 추가로 붙음")]
    [Range(0f, 1f)] public float criticalThreshold = 0.15f;

    [Header("전환 속도")]
    [Tooltip("목표 weight로 부드럽게 다가가는 속도 (클수록 빨리 반응)")]
    public float lerpSpeed = 3f;

    [Header("깜빡임(pulse) 설정 — 위험 구간(criticalThreshold 이하)에서만 작동")]
    public float pulseSpeed = 4f;
    [Range(0f, 1f)] public float pulseAmount = 0.15f;

    Stats stats;
    float targetWeight;
    float currentWeight;
    float currentHpRatio = 1f;

    void Awake()
    {
        stats = GetComponent<Stats>();
    }

    void OnEnable()
    {
        if (stats != null)
            stats.OnHpChanged += OnHpChanged;
    }

    void OnDisable()
    {
        if (stats != null)
            stats.OnHpChanged -= OnHpChanged;
    }

    void Start()
    {
        // 시작 시점 HP 기준으로 초기값을 한 번 맞춰둔다 (이미 낮은 HP로 시작하는 경우 대비).
        if (stats != null)
            OnHpChanged(stats.currentHp, stats.maxHp);
    }

    void OnHpChanged(int cur, int max)
    {
        if (max <= 0)
        {
            targetWeight = 0f;
            currentHpRatio = 1f;
            return;
        }

        currentHpRatio = (float)cur / max;

        // safeThreshold ~ 0 구간을 0 ~ 1로 뒤집어 매핑 (HP가 낮을수록 targetWeight가 1에 가까워짐).
        targetWeight = currentHpRatio >= safeThreshold
            ? 0f
            : Mathf.InverseLerp(safeThreshold, 0f, currentHpRatio);
    }

    void Update()
    {
        if (dangerVolume == null) return;

        currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * lerpSpeed);

        float finalWeight = currentWeight;

        // 위험 구간에서만 심장박동처럼 weight를 살짝 흔들어 긴박감을 더한다.
        if (currentHpRatio <= criticalThreshold && currentWeight > 0f)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * currentWeight;
            finalWeight += pulse;
        }

        dangerVolume.weight = Mathf.Clamp01(finalWeight);
    }
}
