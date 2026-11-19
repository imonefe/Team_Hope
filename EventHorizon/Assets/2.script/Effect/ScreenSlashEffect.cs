using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 가르기(참격) 풀스크린 연출 컨트롤러.
/// - Renderer2D 에 들어있는 "ScreenSlash" 풀스크린 패스를 재생할 때만 켜고, 끝나면 끕니다(평소 비용 0).
/// - 쉐이더 값(M_ScreenSlash)을 커브에 따라 움직입니다.
/// 사용: 씬의 아무 오브젝트에 붙이고 Slash Material 에 M_ScreenSlash 연결 → Play() 호출 또는 F 키.
/// </summary>
[DisallowMultipleComponent]
public class ScreenSlashEffect : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Renderer2D 의 ScreenSlash 패스에 들어간 머티리얼과 같은 것 (Assets/shader/ScreenSlash/M_ScreenSlash)")]
    public Material slashMaterial;
    [Tooltip("Renderer2D 에 등록된 풀스크린 패스 이름")]
    public string rendererFeatureName = "ScreenSlash";

    [Header("Timing")]
    [Tooltip("전체 연출 길이(초). 아래 커브들은 0~1 정규화 시간 기준")]
    public float duration = 1.6f;
    [Tooltip("Time.timeScale 영향 안 받게")]
    public bool useUnscaledTime = true;
    [Tooltip("라인이 다 그어지는 순간 히트스톱(초). 0이면 사용 안 함")]
    public float hitStop = 0.12f;
    [Range(0f, 1f)] public float hitStopTimeScale = 0.02f;

    [Header("Slash Shape")]
    [Range(-180f, 180f)] public float angle = 28f;
    [Tooltip("각도 랜덤 범위(±)")]
    public float angleRandom = 0f;
    [Tooltip("화면 기준 중심 (0~1)")]
    public Vector2 center = new Vector2(0.5f, 0.5f);
    [Tooltip("지정하면 이 오브젝트 위치를 화면 중심으로 사용 (캐릭터/타격 지점)")]
    public Transform followTarget;
    public Camera targetCamera;
    [Tooltip("라인을 오른쪽→왼쪽으로 그림")]
    public bool drawFromRight = false;

    [Header("Max Values (커브가 1일 때 값)")]
    public float maxShift = 0.08f;
    public float maxGap = 0.025f;
    public float maxChroma = 0.012f;
    public float maxContrast = 1.5f;
    public Color flashWhite = Color.white;
    public Color flashBlack = Color.black;

    [Header("Curves (x: 0~1 정규화 시간, y: 0~1)")]
    public AnimationCurve blackFlash = Keys((0f, 1f), (0.03f, 1f), (0.035f, 0f));
    public AnimationCurve whiteFlash = Keys((0f, 0f), (0.03f, 0f), (0.035f, 1f), (0.06f, 0.6f), (0.1f, 0f));
    public AnimationCurve lineProgress = Keys((0f, 0f), (0.04f, 0f), (0.1f, 1f), (1f, 1f));
    public AnimationCurve lineIntensity = Keys((0f, 0f), (0.04f, 1f), (0.65f, 1f), (1f, 0f));
    public AnimationCurve lineWidth = Keys((0f, 1.6f), (0.12f, 1.6f), (0.35f, 0.6f), (1f, 0.4f));
    public AnimationCurve shift = Keys((0f, 0f), (0.08f, 0f), (0.14f, 1f), (0.5f, 0.8f), (0.75f, 0f));
    public AnimationCurve gap = Keys((0f, 0f), (0.08f, 0f), (0.12f, 1f), (0.3f, 0.3f), (0.6f, 0f));
    public AnimationCurve desaturate = Keys((0f, 0f), (0.035f, 1f), (0.55f, 1f), (0.95f, 0f));
    public AnimationCurve contrast = Keys((0f, 0f), (0.035f, 1f), (0.4f, 0.6f), (0.95f, 0f));
    public AnimationCurve invert = Keys((0f, 0f), (0.06f, 0f), (0.07f, 0.85f), (0.22f, 0.85f), (0.3f, 0f));
    public AnimationCurve chroma = Keys((0f, 0f), (0.08f, 1f), (0.3f, 0.2f), (0.6f, 0f));

    [Header("Test")]
    public bool playOnStart = false;
    public KeyCode testKey = KeyCode.F;

    [Header("색 바꾸기 (2026-11-20 추가, 이 효과만)")]
    [Tooltip("켜면 재생하는 동안만 재질의 선 색·틈 색을 아래 값으로 바꾸고, 끝나면 원래 색으로 되돌린다 (같은 재질을 쓰는 처형 컷신·홀로분신에는 영향 없음)")]
    public bool overrideColors = false;
    [ColorUsage(true, true)] public Color lineColor = new Color(4f, 4f, 4f, 1f);
    [ColorUsage(true, true)] public Color gapColor = new Color(3f, 3f, 3f, 1f);

    [Header("연출 켜고 끄기 (2026-11-20 추가, 끄면 그 연출만 빠짐)")]
    [Tooltip("흑백 · 대비 · 반전")]
    public bool useGrade = true;
    [Tooltip("검은/흰 섬광 (번쩍임)")]
    public bool useFlash = true;
    [Tooltip("선을 따라 화면이 어긋남 (크기는 maxShift)")]
    public bool useShift = true;
    [Tooltip("갈라진 틈 (크기는 maxGap)")]
    public bool useGap = true;
    [Tooltip("색 번짐 (크기는 maxChroma)")]
    public bool useChroma = true;

    static readonly int ID_Intensity = Shader.PropertyToID("_Intensity");
    static readonly int ID_Center = Shader.PropertyToID("_SlashCenter");
    static readonly int ID_Angle = Shader.PropertyToID("_SlashAngle");
    static readonly int ID_Progress = Shader.PropertyToID("_SlashProgress");
    static readonly int ID_Reverse = Shader.PropertyToID("_SlashReverse");
    static readonly int ID_LineWidth = Shader.PropertyToID("_LineWidth");
    static readonly int ID_LineIntensity = Shader.PropertyToID("_LineIntensity");
    static readonly int ID_Shift = Shader.PropertyToID("_SplitShift");
    static readonly int ID_Gap = Shader.PropertyToID("_SplitGap");
    static readonly int ID_Desat = Shader.PropertyToID("_Desaturate");
    static readonly int ID_Contrast = Shader.PropertyToID("_Contrast");
    static readonly int ID_Invert = Shader.PropertyToID("_Invert");
    static readonly int ID_Chroma = Shader.PropertyToID("_Chroma");
    static readonly int ID_FlashColor = Shader.PropertyToID("_FlashColor");
    static readonly int ID_FlashAmount = Shader.PropertyToID("_FlashAmount");
    static readonly int ID_LineColor = Shader.PropertyToID("_LineColor");
    static readonly int ID_GapColor = Shader.PropertyToID("_GapColor");
    bool _colorsApplied;
    Color _savedLine, _savedGap;

    const float BaseLineWidth = 0.006f;

    Coroutine _routine;
    ScriptableRendererFeature _feature;
    bool _featureSearched;
    float _savedTimeScale = 1f;
    bool _inHitStop;

    public bool IsPlaying => _routine != null;

    void Awake()
    {
        ResetMaterial();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (testKey != KeyCode.None && Input.GetKeyDown(testKey)) Play();
#endif
    }

    void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        EndHitStop();
        ResetMaterial();
        SetFeatureActive(false);
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (slashMaterial == null)
        {
            Debug.LogWarning("[ScreenSlashEffect] Slash Material 이 비어 있어요. M_ScreenSlash 를 연결하세요.", this);
            return;
        }
        if (_routine != null) { StopCoroutine(_routine); EndHitStop(); }
        _routine = StartCoroutine(Run());
    }

    /// <summary>각도/화면 위치(0~1)를 지정해서 재생 (공격 방향에 맞출 때)</summary>
    public void Play(float slashAngle, Vector2 screenUV)
    {
        angle = slashAngle;
        center = screenUV;
        followTarget = null;
        Play();
    }

    /// <summary>각도 + 월드 위치(타격 지점)로 재생</summary>
    public void PlayAtWorld(float slashAngle, Vector3 worldPos)
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        Vector2 uv = center;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            uv = new Vector2(vp.x, vp.y);
        }
        Play(slashAngle, uv);
    }

    IEnumerator Run()
    {
        SetFeatureActive(true);

        float a = angle + (angleRandom > 0f ? Random.Range(-angleRandom, angleRandom) : 0f);
        Vector2 c = ResolveCenter();

        ApplyColors();
        slashMaterial.SetFloat(ID_Intensity, 1f);
        slashMaterial.SetFloat(ID_Angle, a);
        slashMaterial.SetVector(ID_Center, new Vector4(c.x, c.y, 0f, 0f));
        slashMaterial.SetFloat(ID_Reverse, drawFromRight ? 1f : 0f);

        bool hitStopDone = hitStop <= 0f;
        float hitStopEnd = -1f;

        float t = 0f;
        while (t < duration)
        {
            float n = Mathf.Clamp01(t / duration);
            Apply(n);

            if (!hitStopDone && lineProgress.Evaluate(n) >= 0.99f)
            {
                hitStopDone = true;
                BeginHitStop();
                hitStopEnd = Time.unscaledTime + hitStop;
            }
            if (_inHitStop && Time.unscaledTime >= hitStopEnd)
                EndHitStop();

            yield return null;
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        EndHitStop();
        ResetMaterial();
        SetFeatureActive(false);
        _routine = null;
    }

    void BeginHitStop()
    {
        if (_inHitStop) return;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = hitStopTimeScale;
        _inHitStop = true;
    }

    void EndHitStop()
    {
        if (!_inHitStop) return;
        Time.timeScale = _savedTimeScale;
        _inHitStop = false;
    }

    void Apply(float n)
    {
        var m = slashMaterial;
        m.SetFloat(ID_Progress, lineProgress.Evaluate(n));
        m.SetFloat(ID_LineIntensity, lineIntensity.Evaluate(n));
        m.SetFloat(ID_LineWidth, BaseLineWidth * lineWidth.Evaluate(n));
        // [2026-11-20] 꺼 둔 연출은 0 (ResetMaterial 과 같은 값)
        m.SetFloat(ID_Shift, useShift ? maxShift * shift.Evaluate(n) : 0f);
        m.SetFloat(ID_Gap, useGap ? maxGap * gap.Evaluate(n) : 0f);
        m.SetFloat(ID_Desat, useGrade ? desaturate.Evaluate(n) : 0f);
        m.SetFloat(ID_Contrast, useGrade ? Mathf.Lerp(1f, maxContrast, contrast.Evaluate(n)) : 1f);
        m.SetFloat(ID_Invert, useGrade ? invert.Evaluate(n) : 0f);
        m.SetFloat(ID_Chroma, useChroma ? maxChroma * chroma.Evaluate(n) : 0f);
        if (!useFlash) { m.SetFloat(ID_FlashAmount, 0f); return; }

        float b = blackFlash.Evaluate(n);
        float w = whiteFlash.Evaluate(n);
        if (b >= w)
        {
            m.SetColor(ID_FlashColor, flashBlack);
            m.SetFloat(ID_FlashAmount, b);
        }
        else
        {
            m.SetColor(ID_FlashColor, flashWhite);
            m.SetFloat(ID_FlashAmount, w);
        }
    }

    Vector2 ResolveCenter()
    {
        if (followTarget == null) return center;
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return center;
        Vector3 vp = cam.WorldToViewportPoint(followTarget.position);
        return new Vector2(vp.x, vp.y);
    }

    // [2026-11-20] 이 효과만의 색으로 (재생 시작 때)
    void ApplyColors()
    {
        if (!overrideColors || slashMaterial == null || _colorsApplied) return;
        _savedLine = slashMaterial.GetColor(ID_LineColor);
        _savedGap = slashMaterial.GetColor(ID_GapColor);
        slashMaterial.SetColor(ID_LineColor, lineColor);
        slashMaterial.SetColor(ID_GapColor, gapColor);
        _colorsApplied = true;
    }

    // [2026-11-20] 원래 색으로 (재생 끝·꺼질 때)
    void RestoreColors()
    {
        if (!_colorsApplied || slashMaterial == null) return;
        slashMaterial.SetColor(ID_LineColor, _savedLine);
        slashMaterial.SetColor(ID_GapColor, _savedGap);
        _colorsApplied = false;
    }

    public void ResetMaterial()
    {
        if (slashMaterial == null) return;
        RestoreColors();
        var m = slashMaterial;
        m.SetFloat(ID_Intensity, 0f);
        m.SetFloat(ID_Progress, 0f);
        m.SetFloat(ID_LineIntensity, 0f);
        m.SetFloat(ID_LineWidth, BaseLineWidth);
        m.SetFloat(ID_Shift, 0f);
        m.SetFloat(ID_Gap, 0f);
        m.SetFloat(ID_Desat, 0f);
        m.SetFloat(ID_Contrast, 1f);
        m.SetFloat(ID_Invert, 0f);
        m.SetFloat(ID_Chroma, 0f);
        m.SetFloat(ID_FlashAmount, 0f);
    }

    // ---------- Renderer2D 의 풀스크린 패스 On/Off ----------
    void SetFeatureActive(bool on)
    {
        var f = FindFeature();
        if (f != null) f.SetActive(on);
    }

    ScriptableRendererFeature FindFeature()
    {
        if (_featureSearched) return _feature;
        _featureSearched = true;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return null;

        // URP 14: 렌더러 목록이 internal 이라 리플렉션으로 접근
        var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var list = field?.GetValue(urp) as ScriptableRendererData[];
        if (list != null)
        {
            foreach (var data in list)
            {
                if (data == null) continue;
                foreach (var feat in data.rendererFeatures)
                {
                    if (feat != null && feat.name == rendererFeatureName)
                    {
                        _feature = feat;
                        return _feature;
                    }
                }
            }
        }

        Debug.LogWarning($"[ScreenSlashEffect] 렌더러에서 '{rendererFeatureName}' 패스를 못 찾았어요. " +
                         "Renderer2D 에 Full Screen Pass Renderer Feature 가 있고 켜져 있는지 확인하세요.", this);
        return null;
    }

    // ---------- 커브 헬퍼 ----------
    static AnimationCurve Keys(params (float t, float v)[] k)
    {
        var keys = new Keyframe[k.Length];
        for (int i = 0; i < k.Length; i++)
        {
            float inT = i > 0 ? Slope(k[i - 1], k[i]) : 0f;
            float outT = i < k.Length - 1 ? Slope(k[i], k[i + 1]) : 0f;
            keys[i] = new Keyframe(k[i].t, k[i].v, inT, outT);
        }
        return new AnimationCurve(keys);
    }

    static float Slope((float t, float v) a, (float t, float v) b)
    {
        float dt = b.t - a.t;
        return dt > 1e-5f ? (b.v - a.v) / dt : 0f;
    }
}
