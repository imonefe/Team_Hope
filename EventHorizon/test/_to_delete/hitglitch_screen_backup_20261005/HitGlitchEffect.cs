using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 피격될 때마다 화면 가장자리에 붉은 글리치를 띄우는 전체 화면 효과.
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 Resources/HitGlitchSettings 를 읽어서 스스로 생성되고,
/// 씬이 바뀌어도 유지된다. 씬이 열릴 때마다 플레이어(PlayerController)의 Stats 를 찾아
/// "HP 가 줄었다" 신호(OnHpChanged)를 듣는다. → 기존 스크립트(Stats, Health, PlayerController) 수정 없음.
///
/// 화면 맨 위에 덮이는 오버레이 캔버스 + 화면 전체 Image 에 HitGlitchEdge 셰이더를 씌우고,
/// 셰이더의 _Intensity(세기)를 피격 순간 올렸다가 서서히 0 으로 내린다.
/// 모양(색, 두께, 칸 개수 등)은 Resources/HitGlitchEdge.mat 머티리얼에서,
/// 시간/세기는 Resources/HitGlitchSettings 에셋에서 조절한다.
///
/// 코드에서 직접 띄우기: HitGlitchEffect.Play(0.8f);   // 0~1 세기
/// </summary>
public class HitGlitchEffect : MonoBehaviour
{
    const string SettingsPath = "HitGlitchSettings";

    static HitGlitchEffect instance;

    HitGlitchSettings settings;
    Material mat;
    Image image;

    Stats playerStats;
    int lastHp = -1;
    int lastMaxHp = -1;

    float intensity;      // 지금 세기 (0~1)
    float peak;           // 이번 피격의 최고 세기
    float elapsed;        // 피격 후 지난 시간 (실제 시간 — 일시정지/슬로모션 영향 없음)
    float nextSeedTime;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int SeedId = Shader.PropertyToID("_Seed");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        HitGlitchSettings s = Resources.Load<HitGlitchSettings>(SettingsPath);
        if (s == null || s.material == null)
        {
            Debug.LogWarning("[HitGlitchEffect] Resources/HitGlitchSettings (또는 그 안의 머티리얼)이 없어서 피격 글리치를 끕니다.");
            return;
        }

        // 화면 맨 위에 덮이는 오버레이 캔버스
        GameObject go = new GameObject("[HitGlitchEffect]");
        DontDestroyOnLoad(go);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = s.sortingOrder;

        // 화면 전체를 덮는 Image (마우스 클릭은 통과)
        GameObject imgGo = new GameObject("Glitch", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        RectTransform rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = imgGo.AddComponent<Image>();
        img.raycastTarget = false;
        img.material = new Material(s.material);   // 원본 머티리얼 에셋은 건드리지 않도록 복제본 사용
        img.enabled = false;                       // 평소엔 꺼둠 (그리는 비용 0)

        instance = go.AddComponent<HitGlitchEffect>();
        instance.settings = s;
        instance.image = img;
        instance.mat = img.material;
        instance.mat.SetFloat(IntensityId, 0f);

        SceneManager.sceneLoaded += (scene, mode) => { if (instance != null) instance.FindPlayer(); };
        instance.FindPlayer();
    }

    /// <summary>코드에서 직접 글리치를 띄운다. strength 0~1 (1 = 최대 세기)</summary>
    public static void Play(float strength = 1f)
    {
        if (instance == null || instance.settings == null || !instance.settings.effectEnabled) return;
        instance.Trigger(Mathf.Clamp01(strength));
    }

    // 씬이 열릴 때마다 플레이어 Stats 를 다시 찾아 연결 (재시작/씬 이동 대응)
    void FindPlayer()
    {
        Unsubscribe();
        if (PlayerController.Instance == null) return;

        playerStats = PlayerController.Instance.GetComponent<Stats>();
        if (playerStats == null) return;

        lastHp = playerStats.currentHp;
        lastMaxHp = playerStats.maxHp;
        playerStats.OnHpChanged += OnHpChanged;

        // 이전 씬에서 남은 효과는 즉시 정리
        intensity = 0f;
        Apply();
    }

    void Unsubscribe()
    {
        if (playerStats != null) playerStats.OnHpChanged -= OnHpChanged;
        playerStats = null;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void OnHpChanged(int cur, int max)
    {
        // HP 가 줄었고, 최대 HP 는 그대로일 때만 "피격"으로 본다 (아이템으로 최대 HP 가 줄어든 경우 제외)
        bool hit = lastHp >= 0 && cur < lastHp && max == lastMaxHp;
        int damage = lastHp - cur;
        lastHp = cur;
        lastMaxHp = max;

        if (!hit || settings == null || !settings.effectEnabled) return;

        // 받은 피해가 클수록 세게: 최대 HP 의 fullDamageRatio 만큼 맞으면 최대 세기
        float ratio = max > 0 ? (float)damage / max : 1f;
        float t = settings.fullDamageRatio > 0f ? Mathf.Clamp01(ratio / settings.fullDamageRatio) : 1f;
        Trigger(t);
    }

    void Trigger(float strength01)
    {
        float target = Mathf.Lerp(settings.minIntensity, settings.maxIntensity, strength01);
        peak = Mathf.Max(target, intensity);   // 연속으로 맞으면 더 약해지지 않게
        elapsed = 0f;
        intensity = peak;
        nextSeedTime = 0f;
        Apply();
    }

    void Update()
    {
        if (intensity <= 0f || settings == null) return;

        elapsed += Time.unscaledDeltaTime;
        float t = settings.duration > 0f ? Mathf.Clamp01(elapsed / settings.duration) : 1f;
        float fade = 1f - t;
        intensity = peak * fade * fade;   // 처음엔 빠르게, 끝에선 천천히 사라짐
        if (t >= 1f) intensity = 0f;

        // 일정 간격마다 무늬를 새로 섞어서 "지직" 거리는 느낌
        if (Time.unscaledTime >= nextSeedTime)
        {
            nextSeedTime = Time.unscaledTime + settings.glitchInterval;
            mat.SetFloat(SeedId, Random.Range(0f, 1000f));
        }
        Apply();
    }

    void Apply()
    {
        if (mat == null || image == null) return;
        float flicker = intensity > 0f ? Random.Range(1f - settings.flicker, 1f) : 1f;
        mat.SetFloat(IntensityId, intensity * flicker);
        image.enabled = intensity > 0f;
    }
}
