using UnityEngine;

/// <summary>
/// 퍼즈(Q키)가 발동되어 있는 동안 화면을 푸르게 물들이고, 가장자리에 약한 노이즈 + 움직이는 번개를 씌우는 효과.
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 Resources/PauseGlitchSettings 를 읽어서 스스로 생성되고,
/// 씬이 바뀌어도 유지된다. PauseManager.Instance.IsPaused 값만 지켜보므로 PauseManager 는 수정하지 않았다.
///
/// 실제 그림은 렌더러 기능 HitGlitchScreenFeature 가 PauseGlitchScreen 머티리얼로 그린다.
/// 이 스크립트는 세기(켜질 때 번쩍 → 유지 → 끝나기 직전 깜빡 → 끝나는 순간 섬광·충격파 → 사라짐)와
/// 무작위 씨앗, 번개용 시간만 정해 준다.
/// </summary>
public class PauseGlitchEffect : MonoBehaviour
{
    const string SettingsPath = "PauseGlitchSettings";

    static PauseGlitchEffect instance;

    // ── 렌더러 기능이 읽어 가는 값 ──
    /// <summary>노이즈 세기 (0~1)</summary>
    public static float ScreenIntensity { get; private set; }
    /// <summary>화면 전체 푸른 색 세기 (0~1)</summary>
    public static float ColorIntensity { get; private set; }
    /// <summary>번개 세기 (0~1)</summary>
    public static float BoltIntensity { get; private set; }
    /// <summary>노이즈 무늬 씨앗</summary>
    public static float ScreenSeed { get; private set; }
    /// <summary>번개 배치 씨앗 (퍼즈가 켜질 때마다 바뀜)</summary>
    public static float BoltSeed { get; private set; }
    /// <summary>번개 움직임용 시간 (실제 시간, 초)</summary>
    public static float BoltTime { get; private set; }
    /// <summary>끝 섬광 세기 (0~1)</summary>
    public static float FlashIntensity { get; private set; }
    /// <summary>끝 충격파 진행 (0 = 시작, 1 = 끝까지 퍼짐)</summary>
    public static float ShockProgress { get; private set; }
    /// <summary>끝 충격파 세기 (0~1)</summary>
    public static float ShockIntensity { get; private set; }
    /// <summary>지금 퍼즈 효과를 그려야 하는지</summary>
    public static bool ScreenActive => instance != null &&
        (ScreenIntensity > 0f || BoltIntensity > 0f || ColorIntensity > 0f || FlashIntensity > 0f || ShockIntensity > 0f);

    PauseGlitchSettings settings;

    bool wasPaused;
    float sinceStart;     // 퍼즈가 켜진 뒤 지난 시간 (실제 시간)
    float level = 0f;     // 꺼질 때 서서히 내리기 위한 전체 배율 (0~1)
    float nextSeedTime;
    float flashTime = -1f;   // 끝 섬광이 시작된 뒤 지난 시간 (-1 = 꺼짐)
    float shockTime = -1f;   // 끝 충격파가 시작된 뒤 지난 시간 (-1 = 꺼짐)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        PauseGlitchSettings s = Resources.Load<PauseGlitchSettings>(SettingsPath);
        if (s == null)
        {
            Debug.LogWarning("[PauseGlitchEffect] Resources/PauseGlitchSettings 가 없어서 퍼즈 화면 효과를 끕니다.");
            return;
        }

        GameObject go = new GameObject("[PauseGlitchEffect]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseGlitchEffect>();
        instance.settings = s;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            ScreenIntensity = 0f;
            BoltIntensity = 0f;
            ColorIntensity = 0f;
            FlashIntensity = 0f;
            ShockIntensity = 0f;
        }
    }

    void Update()
    {
        bool paused = settings.effectEnabled
                      && PauseManager.Instance != null
                      && PauseManager.Instance.IsPaused;
        float dt = Time.unscaledDeltaTime;

        // 퍼즈가 방금 켜짐 → 처음부터 번쩍
        if (paused && !wasPaused)
        {
            sinceStart = 0f;
            level = 1f;
            nextSeedTime = 0f;
            BoltSeed = Random.Range(0f, 1000f);   // 켤 때마다 번개 배치가 달라지게
        }
        // 퍼즈가 방금 꺼짐 → 팡! 섬광 + 충격파
        if (!paused && wasPaused && settings.endBurst)
        {
            flashTime = 0f;
            shockTime = 0f;
        }
        wasPaused = paused;
        UpdateEndBurst(dt);

        if (paused)
        {
            sinceStart += dt;
            level = 1f;
        }
        else
        {
            if (level <= 0f)
            {
                ScreenIntensity = 0f;
                BoltIntensity = 0f;
                ColorIntensity = 0f;
                return;
            }
            level = settings.fadeOutTime > 0f ? Mathf.Max(0f, level - dt / settings.fadeOutTime) : 0f;
        }

        // 켜진 순간 번쩍 → burstDuration 동안 유지 세기로 내려옴
        float b = settings.burstDuration > 0f ? Mathf.Clamp01(sinceStart / settings.burstDuration) : 1f;
        float burst = (1f - b) * (1f - b);
        float noise = Mathf.Lerp(settings.sustainIntensity, settings.burstIntensity, burst);
        float bolt = Mathf.Lerp(settings.boltSustain, settings.boltBurst, burst);
        float color = Mathf.Lerp(settings.colorSustain, settings.colorBurst, burst);

        // 무작위 깜빡임 (노이즈·번개만. 푸른 화면 색은 차분하게 유지)
        float flick = Random.Range(1f - settings.flicker, 1f);

        // 끝나기 직전: 빠르게 깜빡여서 곧 끝난다고 알려줌 (이때는 푸른 색도 같이 깜빡)
        float warn = 1f;
        if (paused && settings.endWarningTime > 0f && PauseManager.Instance.RemainingTime < settings.endWarningTime)
        {
            warn = Mathf.Sin(Time.unscaledTime * settings.endWarningRate * Mathf.PI * 2f) > 0f ? 1f : 0.35f;
        }

        ScreenIntensity = noise * flick * warn * level;
        BoltIntensity = bolt * flick * warn * level;
        ColorIntensity = color * warn * level;
        BoltTime = Time.unscaledTime % 1000f;   // 너무 큰 숫자가 되지 않게 1000초마다 되돌림

        // 무늬 섞기
        if (Time.unscaledTime >= nextSeedTime)
        {
            nextSeedTime = Time.unscaledTime + settings.glitchInterval;
            ScreenSeed = Random.Range(0f, 1000f);
        }
    }

    // 끝 섬광(빠르게 사라짐)과 충격파(퍼지면서 약해짐) 진행
    void UpdateEndBurst(float dt)
    {
        if (flashTime >= 0f)
        {
            flashTime += dt;
            float p = settings.flashDuration > 0f ? flashTime / settings.flashDuration : 1f;
            if (p >= 1f) { flashTime = -1f; FlashIntensity = 0f; }
            else FlashIntensity = settings.flashIntensity * (1f - p) * (1f - p) * (1f - p);
        }
        if (shockTime >= 0f)
        {
            shockTime += dt;
            float p = settings.shockDuration > 0f ? shockTime / settings.shockDuration : 1f;
            if (p >= 1f) { shockTime = -1f; ShockIntensity = 0f; ShockProgress = 1f; }
            else
            {
                ShockProgress = 1f - (1f - p) * (1f - p);   // 처음엔 빠르게 퍼지다 점점 느려짐
                ShockIntensity = settings.shockIntensity * (1f - p);
            }
        }
    }
}
