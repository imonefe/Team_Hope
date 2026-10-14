using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퍼즈 게이지 UI
/// Canvas 에 붙이기
///
/// 평소엔 gaugeSlider가 "충전된 게이지"(0~100)를 보여주다가, 퍼즈가 발동되는 순간부터는
/// 같은 슬라이더를 "남은 퍼즈 시간 비율"로 전환해서 보여준다. 그래서 발동하자마자
/// 확 비는 게 아니라, 퍼즈 지속시간에 비례해서 서서히 줄어든다.
/// </summary>
public class PauseGaugeUI : MonoBehaviour
{
    [Header("게이지 바")]
    public Slider gaugeSlider;
    public Image gaugeFill;         // 게이지 채워지는 Image

    [Header("퍼즈 중 표시")]
    public GameObject pausePanel;    // 퍼즈 중에만 보이는 패널
    public Slider timeSlider;    // 남은 시간 바
    public Slider distSlider;    // 남은 거리 바

    [Header("색상")]
    public Color normalColor = new Color(0.3f, 0.6f, 1.0f);
    public Color readyColor = new Color(1.0f, 0.9f, 0.2f);
    public Color pausingColor = new Color(0.5f, 1.0f, 0.8f);

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void Update()
    {
        if (PauseManager.Instance == null) return;

        bool isPaused = PauseManager.Instance.IsPaused;
        float gauge = PauseManager.Instance.GaugeValue;

        // 게이지 바
        // 퍼즈 중엔 "남은 퍼즈 시간 비율"로, 평소엔 "충전된 게이지 비율"로 표시한다.
        // (GaugeValue는 퍼즈 발동 즉시 0으로 리셋되므로, 그대로 쓰면 발동하자마자 슬라이더가 확 비어버린다)
        if (gaugeSlider != null)
        {
            if (isPaused && PauseManager.Instance.MaxTime > 0f)
                gaugeSlider.value = PauseManager.Instance.RemainingTime / PauseManager.Instance.MaxTime;
            else if (!isPaused)
                gaugeSlider.value = gauge / 100f;
        }

        if (gaugeFill != null)
        {
            gaugeFill.color = isPaused ? pausingColor
                            : gauge >= 100f ? readyColor
                            : normalColor;
        }

        // 퍼즈 패널
        if (pausePanel != null) pausePanel.SetActive(isPaused);

        if (!isPaused) return;

        // 남은 시간 / 거리 (별도 슬라이더가 연결돼 있을 때만 갱신)
        if (timeSlider != null)
        {
            timeSlider.maxValue = PauseManager.Instance.MaxTime;
            timeSlider.value = PauseManager.Instance.RemainingTime;
        }

        if (distSlider != null)
        {
            distSlider.maxValue = PauseManager.Instance.MaxDist;
            distSlider.value = PauseManager.Instance.RemainingDist;
        }
    }
}
