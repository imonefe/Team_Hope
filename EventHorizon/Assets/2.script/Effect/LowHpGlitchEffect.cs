using UnityEngine;

/// <summary>
/// 플레이어 HP 가 기준(기본 20%) 이하일 때 화면 가장자리에 채도 낮은 회붉은 노이즈 + 심한 왜곡을 씌우는 효과.
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 Resources/LowHpGlitchSettings 를 읽어서 스스로 생성되고,
/// 씬이 바뀌어도 유지된다. 매 프레임 PlayerController.Instance 의 Stats(currentHp / maxHp) 비율만 읽으므로
/// 캐릭터가 바뀌거나 씬이 다시 열려도 따로 연결할 필요가 없다. (기존 스크립트 수정 없음)
///
/// 실제 그림은 렌더러 기능 HitGlitchScreenFeature 가 LowHpGlitchScreen 머티리얼로 그린다.
/// 이 스크립트는 세기(HP 가 낮을수록 세게 + 심장박동 맥동)와 무작위 씨앗, 시간만 정해 준다.
/// </summary>
public class LowHpGlitchEffect : MonoBehaviour
{
    const string SettingsPath = "LowHpGlitchSettings";

    static LowHpGlitchEffect instance;

    // ── 렌더러 기능이 읽어 가는 값 ──
    /// <summary>세기 (0~1)</summary>
    public static float ScreenIntensity { get; private set; }
    /// <summary>노이즈 무늬 씨앗</summary>
    public static float ScreenSeed { get; private set; }
    /// <summary>물결·가로줄 움직임용 시간 (실제 시간, 초)</summary>
    public static float EffectTime { get; private set; }
    /// <summary>지금 저체력 효과를 그려야 하는지</summary>
    public static bool ScreenActive => instance != null && ScreenIntensity > 0f;

    LowHpGlitchSettings settings;
    Stats playerStats;
    float level;          // 부드럽게 따라가는 세기 (0~1)
    float nextSeedTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        LowHpGlitchSettings s = Resources.Load<LowHpGlitchSettings>(SettingsPath);
        if (s == null)
        {
            Debug.LogWarning("[LowHpGlitchEffect] Resources/LowHpGlitchSettings 가 없어서 저체력 화면 효과를 끕니다.");
            return;
        }

        GameObject go = new GameObject("[LowHpGlitchEffect]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LowHpGlitchEffect>();
        instance.settings = s;
    }

    void OnDestroy()
    {
        if (instance == this) ScreenIntensity = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 플레이어 Stats 찾기 (캐릭터 교체·씬 이동 대응: 바뀌었으면 다시 잡음)
        if (PlayerController.Instance != null)
        {
            if (playerStats == null || playerStats.gameObject != PlayerController.Instance.gameObject)
                playerStats = PlayerController.Instance.GetComponent<Stats>();
        }
        else playerStats = null;

        // 목표 세기: 기준 이하일 때만, HP 가 낮을수록 세게
        float target = 0f;
        if (settings.effectEnabled && playerStats != null && playerStats.maxHp > 0)
        {
            float ratio = (float)playerStats.currentHp / playerStats.maxHp;
            if (ratio <= settings.hpThreshold)
            {
                float t = settings.hpThreshold > 0f ? Mathf.Clamp01(1f - ratio / settings.hpThreshold) : 1f;
                target = Mathf.Lerp(settings.minIntensity, settings.maxIntensity, t);
            }
        }

        // 부드럽게 따라감
        float speed = target > level
            ? (settings.fadeInTime > 0f ? 1f / settings.fadeInTime : 1000f)
            : (settings.fadeOutTime > 0f ? 1f / settings.fadeOutTime : 1000f);
        level = Mathf.MoveTowards(level, target, speed * dt);

        if (level <= 0f)
        {
            ScreenIntensity = 0f;
            return;
        }

        // 심장박동 맥동 + 무작위 깜빡임
        float pulse = 1f;
        if (settings.pulseRate > 0f)
            pulse = 1f - settings.pulseAmount * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * settings.pulseRate * Mathf.PI * 2f));
        float flick = Random.Range(1f - settings.flicker, 1f);

        ScreenIntensity = level * pulse * flick;
        EffectTime = Time.unscaledTime % 1000f;

        if (Time.unscaledTime >= nextSeedTime)
        {
            nextSeedTime = Time.unscaledTime + settings.glitchInterval;
            ScreenSeed = Random.Range(0f, 1000f);
        }
    }
}
