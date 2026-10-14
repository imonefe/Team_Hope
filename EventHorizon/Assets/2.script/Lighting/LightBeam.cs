using UnityEngine;

/// <summary>
/// 빛줄기 하나. 사각형 판(Quad)에 붙여 쓰며, 판의 위쪽 가장자리가 광원, 아래쪽이 빛이 떨어지는 끝이다.
/// 회전과 크기(Scale)로 각도·길이·폭을 정한다.
///
/// 하는 일
///  1) 빛줄기 자체를 옅은 빛 안개로 보여줌 (가장자리·끝이 흐릿하게 사라짐, 천천히 일렁임)
///  2) RoomDarkness 로 어두워진 방에서는 이 빛줄기가 닿는 곳만 밝혀줌 (2D·3D 모두)
///  3) 빛 안에 먼지가 천천히 떠다님 (재생 중에 만들어짐)
///
/// 2D 화면에 쓰려면 2D 카메라가 보는 층(예: Default)에, 3D 배경에 쓰려면 3D 카메라가 보는 층(원경/중경/근경 등)에 둔다.
/// 값은 머티리얼을 복사하지 않고 이 오브젝트에만 따로 넣으므로, 빛줄기마다 색과 세기를 다르게 해도 된다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightBeam : MonoBehaviour
{
    [Header("빛 색과 세기")]
    public Color color = new Color(1f, 0.93f, 0.78f, 1f);

    [Tooltip("어두운 방에서 이 빛이 닿은 곳을 얼마나 밝히는지")]
    [Range(0f, 3f)] public float lightIntensity = 1f;

    [Tooltip("빛줄기 자체가 얼마나 보이는지 (빛 안개). 0이면 안 보이고 밝히기만 함")]
    [Range(0f, 2f)] public float hazeIntensity = 0.25f;

    [Header("모양")]
    [Tooltip("광원 쪽 폭 비율. 1이면 직사각형, 작을수록 위가 좁고 아래로 퍼지는 모양")]
    [Range(0.02f, 1f)] public float startWidth = 0.35f;

    [Tooltip("양옆 가장자리 흐림 정도. 1이면 가운데부터 서서히 옅어짐")]
    [Range(0.01f, 1f)] public float edgeSoftness = 0.7f;

    [Tooltip("광원 쪽에서 서서히 나타나는 구간 (길이 비율)")]
    [Range(0f, 1f)] public float startFade = 0.08f;

    [Tooltip("끝 쪽에서 서서히 사라지는 구간 (길이 비율)")]
    [Range(0f, 1f)] public float endFade = 0.45f;

    [Tooltip("광원에서 멀어질수록 약해지는 정도")]
    [Range(0f, 1f)] public float falloff = 0.5f;

    [Header("일렁임")]
    [Tooltip("빛 속에 흐르는 옅은 결 무늬의 진하기")]
    [Range(0f, 1f)] public float shimmer = 0.3f;
    [Tooltip("결 무늬가 흐르는 속도")]
    public float shimmerSpeed = 0.15f;
    [Tooltip("결 무늬 촘촘함")]
    [Range(0.5f, 20f)] public float shimmerScale = 4f;
    [Tooltip("빛 전체가 깜빡이는 정도 (0 = 안 깜빡임)")]
    [Range(0f, 1f)] public float flicker = 0f;
    public float flickerSpeed = 5f;

    [Header("그리는 순서 (2D 카메라용)")]
    [Tooltip("정렬 레이어 이름 (Project Settings > Tags and Layers > Sorting Layers)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    [Header("먼지")]
    public bool dust = true;
    [Tooltip("동시에 떠 있는 먼지 수 (대략)")]
    [Range(0, 300)] public int dustCount = 30;
    [Tooltip("먼지 크기 (최소, 최대) — 월드 단위")]
    public Vector2 dustSize = new Vector2(0.03f, 0.08f);
    [Tooltip("먼지 하나가 떠 있는 시간 (최소, 최대) — 초")]
    public Vector2 dustLifetime = new Vector2(4f, 8f);
    [Tooltip("먼지가 떠다니는 기본 속도")]
    [Min(0f)] public float dustSpeed = 0.06f;
    [Tooltip("먼지가 이리저리 흔들리는 정도")]
    [Min(0f)] public float dustWander = 0.2f;
    [Tooltip("먼지 밝기")]
    [Range(0f, 3f)] public float dustBrightness = 1f;
    [Tooltip("먼지가 퍼져 있는 앞뒤 두께 (월드 단위)")]
    [Min(0f)] public float dustDepth = 0.3f;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int LightIntensityId = Shader.PropertyToID("_LightIntensity");
    static readonly int HazeIntensityId = Shader.PropertyToID("_HazeIntensity");
    static readonly int StartWidthId = Shader.PropertyToID("_StartWidth");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int StartFadeId = Shader.PropertyToID("_StartFade");
    static readonly int EndFadeId = Shader.PropertyToID("_EndFade");
    static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    static readonly int ShimmerId = Shader.PropertyToID("_Shimmer");
    static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
    static readonly int ShimmerScaleId = Shader.PropertyToID("_ShimmerScale");
    static readonly int FlickerId = Shader.PropertyToID("_Flicker");
    static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    MeshRenderer meshRenderer;
    MaterialPropertyBlock props;
    ParticleSystem dustSystem;
    float seed;

    void OnEnable()
    {
        seed = Mathf.Abs(GetInstanceID() * 0.137f) % 100f;
        EnsureDefaults();
        Apply();
        if (Application.isPlaying) BuildDust();
    }

    void OnDisable()
    {
        if (dustSystem != null)
        {
            Destroy(dustSystem.gameObject);
            dustSystem = null;
        }
    }

    void OnValidate()
    {
        if (dustSize.y < dustSize.x) dustSize.y = dustSize.x;
        if (dustLifetime.y < dustLifetime.x) dustLifetime.y = dustLifetime.x;
        Apply();
        if (Application.isPlaying && dustSystem != null) ConfigureDust(dustSystem);
    }

    /// <summary>판 모양과 기본 머티리얼이 비어 있으면 채움.</summary>
    void EnsureDefaults()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == null) mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer.sharedMaterial == null)
        {
            Material m = Resources.Load<Material>("LightBeam");
            if (m != null) meshRenderer.sharedMaterial = m;
        }
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    /// <summary>값을 이 오브젝트의 렌더러에만 넣음 (머티리얼은 공유한 채로).</summary>
    public void Apply()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;
        if (props == null) props = new MaterialPropertyBlock();

        meshRenderer.GetPropertyBlock(props);
        props.SetColor(ColorId, color);
        props.SetFloat(LightIntensityId, lightIntensity);
        props.SetFloat(HazeIntensityId, hazeIntensity);
        props.SetFloat(StartWidthId, startWidth);
        props.SetFloat(EdgeSoftnessId, edgeSoftness);
        props.SetFloat(StartFadeId, startFade);
        props.SetFloat(EndFadeId, endFade);
        props.SetFloat(FalloffId, falloff);
        props.SetFloat(ShimmerId, shimmer);
        props.SetFloat(ShimmerSpeedId, shimmerSpeed);
        props.SetFloat(ShimmerScaleId, shimmerScale);
        props.SetFloat(FlickerId, flicker);
        props.SetFloat(FlickerSpeedId, flickerSpeed);
        props.SetFloat(SeedId, seed);
        meshRenderer.SetPropertyBlock(props);

        int layerId = SortingLayer.NameToID(sortingLayerName);
        if (SortingLayer.IsValid(layerId)) meshRenderer.sortingLayerID = layerId;
        meshRenderer.sortingOrder = sortingOrder;
    }

    // ── 먼지 ──

    void BuildDust()
    {
        if (!dust || dustCount <= 0 || dustSystem != null) return;

        Material dustMat = Resources.Load<Material>("LightDust");
        if (dustMat == null)
        {
            Debug.LogWarning("[LightBeam] Resources/LightDust 머티리얼이 없어 먼지를 만들지 못했습니다.", this);
            return;
        }

        GameObject go = new GameObject("Dust (자동 생성)");
        go.hideFlags = HideFlags.DontSave;
        go.layer = gameObject.layer;
        go.SetActive(false);                              // 설정을 다 넣은 뒤 켜서 재생 시작
        go.transform.SetParent(transform, false);

        dustSystem = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ConfigureDust(dustSystem);
        go.SetActive(true);
    }

    void ConfigureDust(ParticleSystem ps)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (meshRenderer != null)
        {
            r.sortingLayerID = meshRenderer.sortingLayerID;
            r.sortingOrder = meshRenderer.sortingOrder + 1;
        }
        if (props == null) props = new MaterialPropertyBlock();
        MaterialPropertyBlock dp = new MaterialPropertyBlock();
        dp.SetFloat(IntensityId, dustBrightness);
        r.SetPropertyBlock(dp);

        float lifeMin = Mathf.Max(0.1f, dustLifetime.x);
        float lifeMax = Mathf.Max(lifeMin, dustLifetime.y);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, dustSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(dustSize.x, dustSize.y);
        main.startColor = new Color(color.r, color.g, color.b, 1f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;   // 판 크기는 퍼지는 범위에만, 먼지 크기는 그대로
        main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(dustCount * 1.5f));
        if (!ps.isPlaying) main.prewarm = true;               // 처음부터 먼지가 차 있게

        var emission = ps.emission;
        emission.enabled = dust;
        emission.rateOverTime = dustCount / ((lifeMin + lifeMax) * 0.5f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        float zScale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.z));
        shape.scale = new Vector3(1f, 1f, dustDepth / zScale);
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.randomDirectionAmount = 1f;

        var noise = ps.noise;
        noise.enabled = dustWander > 0f;
        noise.strength = dustWander;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.15f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.sizeAmount = 0.6f;                              // 크기가 살짝씩 변해서 반짝이는 느낌

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    void OnDrawGizmosSelected()
    {
        // 빛줄기 윤곽 (판의 위 = 광원)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        float top = Mathf.Max(0.02f, startWidth) * 0.5f;
        Vector3 a = new Vector3(-top, 0.5f, 0f), b = new Vector3(top, 0.5f, 0f);
        Vector3 c = new Vector3(0.5f, -0.5f, 0f), d = new Vector3(-0.5f, -0.5f, 0f);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        Gizmos.DrawWireSphere(new Vector3(0f, 0.5f, 0f), 0.03f);
    }
}
