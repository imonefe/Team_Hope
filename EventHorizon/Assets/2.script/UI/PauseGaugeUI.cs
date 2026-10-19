using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퍼즈 게이지 UI
/// Canvas 에 붙이기
///
/// 평소엔 gaugeSlider가 "충전된 게이지"(0~100)를 보여주다가, 퍼즈가 발동되는 순간부터는
/// 같은 슬라이더를 "남은 퍼즈 시간 비율"로 전환해서 보여준다. 그래서 발동하자마자
/// 확 비는 게 아니라, 퍼즈 지속시간에 비례해서 서서히 줄어든다.
///
/// [2026-10-19 추가]
/// - 게이지가 다 차서 쓸 수 있는 상태면 채움 이미지 둘레가 맥박처럼 은은하게 빛난다
///   (새 이미지 없이 채움 이미지에 Outline 효과 2겹을 코드로 붙여서 표현).
/// - gaugeFill 이 비어 있으면 슬라이더의 채움 이미지(Fill Rect)를 자동으로 찾아 쓴다.
/// </summary>
public class PauseGaugeUI : MonoBehaviour
{
    [Header("게이지 바")]
    public Slider gaugeSlider;
    [Tooltip("게이지 채워지는 Image (비우면 gaugeSlider 의 Fill Rect 에서 자동으로 찾음)")]
    public Image gaugeFill;

    [Header("퍼즈 중 표시")]
    public GameObject pausePanel;    // 퍼즈 중에만 보이는 패널
    public Slider timeSlider;    // 남은 시간 바
    public Slider distSlider;    // 남은 거리 바

    [Header("색상")]
    public Color normalColor = new Color(0.3f, 0.6f, 1.0f);
    public Color readyColor = new Color(1.0f, 0.9f, 0.2f);
    public Color pausingColor = new Color(0.5f, 1.0f, 0.8f);

    [Header("가득 찼을 때 빛 (2026-10-19 추가)")]
    [Tooltip("빛 색 (투명도는 아래 최소·최대 값으로 맥박)")]
    public Color glowColor = new Color(1f, 0.92f, 0.6f, 1f);
    [Tooltip("빛 두께 (px). 바깥 겹은 이 값의 2배")]
    public float glowSize = 2f;
    [Tooltip("1초에 몇 번 맥박치는지")]
    public float glowPulsePerSecond = 1.2f;
    [Range(0f, 1f)] public float glowMinAlpha = 0.2f;
    [Range(0f, 1f)] public float glowMaxAlpha = 0.85f;
    [Tooltip("맥박 때 바 세로 두께가 커지는 정도 (0.15 = 15%). 0 이면 두께는 그대로")]
    [Range(0f, 0.5f)] public float glowThicknessPulse = 0.15f;

    Outline innerGlow, outerGlow;
    Vector3 sliderBaseScale = Vector3.one;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);

        if (gaugeFill == null && gaugeSlider != null && gaugeSlider.fillRect != null)
            gaugeFill = gaugeSlider.fillRect.GetComponent<Image>();

        if (gaugeSlider != null) sliderBaseScale = gaugeSlider.transform.localScale;

        // 빛: 채움 이미지에 윤곽 효과 2겹 (안쪽 진하게, 바깥 옅게)
        if (gaugeFill != null)
        {
            innerGlow = gaugeFill.gameObject.AddComponent<Outline>();
            outerGlow = gaugeFill.gameObject.AddComponent<Outline>();
            innerGlow.effectDistance = new Vector2(glowSize, glowSize);
            outerGlow.effectDistance = new Vector2(glowSize * 2f, glowSize * 2f);
            innerGlow.useGraphicAlpha = outerGlow.useGraphicAlpha = true;
            SetGlow(false, 0f);
        }
    }

    void Update()
    {
        if (PauseManager.Instance == null) return;

        PauseManager pm = PauseManager.Instance;
        bool isPaused = pm.IsPaused;
        bool ready = !isPaused && pm.GaugeValue >= 100f;
        float gauge = pm.GaugeValue;

        // 게이지 바
        // 퍼즈 중엔 "남은 퍼즈 시간 비율"로, 평소엔 "충전된 게이지 비율"로 표시한다.
        // (GaugeValue는 퍼즈 발동 즉시 0으로 리셋되므로, 그대로 쓰면 발동하자마자 슬라이더가 확 비어버린다)
        if (gaugeSlider != null)
        {
            if (isPaused && pm.MaxTime > 0f)
                gaugeSlider.value = pm.RemainingTime / pm.MaxTime;
            else if (!isPaused)
                gaugeSlider.value = gauge / 100f;
        }

        if (gaugeFill != null)
        {
            gaugeFill.color = isPaused ? pausingColor
                            : ready ? readyColor
                            : normalColor;
        }

        // 가득 찼을 때 맥박 빛 (ESC 메뉴로 멈춰 있어도 움직이도록 실제 시간 기준)
        if (ready)
        {
            float t = (Mathf.Sin(Time.unscaledTime * glowPulsePerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
            SetGlow(true, t);
        }
        else SetGlow(false, 0f);

        // 퍼즈 패널
        if (pausePanel != null) pausePanel.SetActive(isPaused);

        if (!isPaused) return;

        // 남은 시간 / 거리 (별도 슬라이더가 연결돼 있을 때만 갱신)
        if (timeSlider != null)
        {
            timeSlider.maxValue = pm.MaxTime;
            timeSlider.value = pm.RemainingTime;
        }

        if (distSlider != null)
        {
            distSlider.maxValue = pm.MaxDist;
            distSlider.value = pm.RemainingDist;
        }
    }

    // t: 0~1 맥박 위치
    void SetGlow(bool on, float t)
    {
        if (innerGlow != null)
        {
            innerGlow.enabled = outerGlow.enabled = on;
            if (on)
            {
                float a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);
                Color c = glowColor;
                c.a = a; innerGlow.effectColor = c;
                c.a = a * 0.45f; outerGlow.effectColor = c;
            }
        }

        if (gaugeSlider != null)
        {
            float k = on ? 1f + glowThicknessPulse * t : 1f;
            gaugeSlider.transform.localScale = new Vector3(sliderBaseScale.x, sliderBaseScale.y * k, sliderBaseScale.z);
        }
    }
}
