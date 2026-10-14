using UnityEngine;

/// <summary>
/// 입체 원뿔 빛줄기. 3D 배경용. (2D 화면용 납작한 빛줄기는 LightBeam)
///
/// 이 오브젝트의 위치가 광원이고, 아래(로컬 -Y)로 원뿔이 뻗는다. 회전으로 각도를 정한다.
/// 원뿔 모양은 아래 값으로 직접 만들어 쓰므로, 크기(Scale)는 1 로 두고 반지름·길이 값으로 조절하는 것을 권장한다.
///
/// 하는 일
///  1) 원뿔 빛 안개: 면이 카메라를 정면으로 볼수록 진하고 비스듬할수록 옅어서, 어느 각도에서 봐도 테두리가 흐린 빛기둥으로 보임
///  2) RoomDarkness 로 어두워진 방에서는 이 빛이 닿는 곳만 밝혀줌
///  3) 원뿔 안에서만 먼지가 천천히 떠다님 (재생 중에 만들어짐)
///
/// 3D 카메라가 보는 층(원경/중경/근경/Room3D 등)에 두어야 보인다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightBeam3D : MonoBehaviour
{
    [Header("원뿔 모양")]
    [Tooltip("광원 쪽 반지름 (월드 단위, 크기 1 기준)")]
    [Min(0f)] public float startRadius = 1f;
    [Tooltip("끝 쪽 반지름")]
    [Min(0.001f)] public float endRadius = 6f;
    [Tooltip("빛줄기 길이")]
    [Min(0.01f)] public float length = 30f;
    [Tooltip("둘레를 몇 조각으로 나눌지. 클수록 둥글고 매끈함")]
    [Range(8, 96)] public int segments = 40;

    [Header("빛 색과 세기")]
    public Color color = new Color(1f, 0.93f, 0.78f, 1f);
    [Tooltip("어두운 방에서 이 빛이 닿은 곳을 얼마나 밝히는지")]
    [Range(0f, 3f)] public float lightIntensity = 1f;
    [Tooltip("빛줄기 자체가 얼마나 보이는지 (빛 안개). 0이면 안 보이고 밝히기만 함")]
    [Range(0f, 2f)] public float hazeIntensity = 0.2f;

    [Header("흐림")]
    [Tooltip("테두리 흐림 정도. 1이면 가운데부터 서서히 옅어짐, 작을수록 테두리가 또렷함")]
    [Range(0.05f, 1f)] public float edgeSoftness = 0.8f;
    [Tooltip("광원 쪽에서 서서히 나타나는 구간 (길이 비율)")]
    [Range(0f, 1f)] public float startFade = 0.05f;
    [Tooltip("끝 쪽에서 서서히 사라지는 구간 (길이 비율)")]
    [Range(0f, 1f)] public float endFade = 0.4f;
    [Tooltip("광원에서 멀어질수록 약해지는 정도")]
    [Range(0f, 1f)] public float falloff = 0.5f;
    [Tooltip("카메라가 빛줄기에 아주 가까워지면 옅어지는 거리 (0 = 사용 안 함)")]
    [Min(0f)] public float nearFadeDistance = 0f;

    [Header("일렁임")]
    [Range(0f, 1f)] public float shimmer = 0.3f;
    public float shimmerSpeed = 0.15f;
    [Range(0.5f, 20f)] public float shimmerScale = 3f;
    [Range(0f, 1f)] public float flicker = 0f;
    public float flickerSpeed = 5f;

    [Header("먼지")]
    public bool dust = true;
    [Range(0, 400)] public int dustCount = 60;
    [Tooltip("먼지 크기 (최소, 최대) — 월드 단위. 3D 배경은 멀리 있어서 2D 보다 크게")]
    public Vector2 dustSize = new Vector2(0.12f, 0.3f);
    public Vector2 dustLifetime = new Vector2(5f, 10f);
    [Min(0f)] public float dustSpeed = 0.2f;
    [Min(0f)] public float dustWander = 0.6f;
    [Range(0f, 3f)] public float dustBrightness = 1f;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int LightIntensityId = Shader.PropertyToID("_LightIntensity");
    static readonly int HazeIntensityId = Shader.PropertyToID("_HazeIntensity");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int StartFadeId = Shader.PropertyToID("_StartFade");
    static readonly int EndFadeId = Shader.PropertyToID("_EndFade");
    static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
    static readonly int ShimmerId = Shader.PropertyToID("_Shimmer");
    static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
    static readonly int ShimmerScaleId = Shader.PropertyToID("_ShimmerScale");
    static readonly int FlickerId = Shader.PropertyToID("_Flicker");
    static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    MeshRenderer meshRenderer;
    MaterialPropertyBlock props;
    Mesh mesh;
    ParticleSystem dustSystem;
    float seed;
    bool meshDirty = true;

    void OnEnable()
    {
        seed = Mathf.Abs(GetInstanceID() * 0.137f) % 100f;
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer.sharedMaterial == null)
        {
            Material m = Resources.Load<Material>("LightBeam3D");
            if (m != null) meshRenderer.sharedMaterial = m;
        }
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        meshDirty = true;
        RebuildMesh();
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

    void OnDestroy()
    {
        if (mesh != null)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }

    void OnValidate()
    {
        if (dustSize.y < dustSize.x) dustSize.y = dustSize.x;
        if (dustLifetime.y < dustLifetime.x) dustLifetime.y = dustLifetime.x;
        meshDirty = true;      // 모양 다시 만들기는 OnValidate 안에서 하지 않고 다음 갱신 때
        Apply();
        if (Application.isPlaying && dustSystem != null) ConfigureDust(dustSystem);
    }

    void Update()
    {
        if (meshDirty) RebuildMesh();
    }

    // ── 원뿔 옆면 만들기 (뚜껑 없음). 위 = 광원(uv.y=1), 아래 = 끝(uv.y=0) ──
    void RebuildMesh()
    {
        meshDirty = false;
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mesh == null)
        {
            mesh = new Mesh { name = "LightBeam3D Cone (자동 생성)" };
            mesh.hideFlags = HideFlags.DontSave;
        }

        int n = Mathf.Clamp(segments, 8, 96);
        int count = (n + 1) * 2;
        Vector3[] v = new Vector3[count];
        Vector3[] nr = new Vector3[count];
        Vector2[] uv = new Vector2[count];
        int[] tri = new int[n * 6];

        float r0 = Mathf.Max(0f, startRadius);
        float r1 = Mathf.Max(0.001f, endRadius);
        float len = Mathf.Max(0.01f, length);

        for (int i = 0; i <= n; i++)
        {
            float u = (float)i / n;
            float a = u * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 normal = (dir * len + Vector3.up * (r1 - r0)).normalized;   // 옆면에 수직, 바깥쪽

            v[i * 2] = dir * r0;                                   // 위 (광원)
            v[i * 2 + 1] = dir * r1 + Vector3.down * len;          // 아래 (끝)
            nr[i * 2] = normal;
            nr[i * 2 + 1] = normal;
            uv[i * 2] = new Vector2(u, 1f);
            uv[i * 2 + 1] = new Vector2(u, 0f);
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 6;
            tri[t] = a; tri[t + 1] = c; tri[t + 2] = b;
            tri[t + 3] = b; tri[t + 4] = c; tri[t + 5] = d;
        }

        mesh.Clear();
        mesh.vertices = v;
        mesh.normals = nr;
        mesh.uv = uv;
        mesh.triangles = tri;
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        if (dustSystem != null) ConfigureDust(dustSystem);
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
        props.SetFloat(EdgeSoftnessId, edgeSoftness);
        props.SetFloat(StartFadeId, startFade);
        props.SetFloat(EndFadeId, endFade);
        props.SetFloat(FalloffId, falloff);
        props.SetFloat(NearFadeId, nearFadeDistance);
        props.SetFloat(ShimmerId, shimmer);
        props.SetFloat(ShimmerSpeedId, shimmerSpeed);
        props.SetFloat(ShimmerScaleId, shimmerScale);
        props.SetFloat(FlickerId, flicker);
        props.SetFloat(FlickerSpeedId, flickerSpeed);
        props.SetFloat(SeedId, seed);
        meshRenderer.SetPropertyBlock(props);
    }

    // ── 먼지 (원뿔 안에서만) ──

    void BuildDust()
    {
        if (!dust || dustCount <= 0 || dustSystem != null) return;

        Material dustMat = Resources.Load<Material>("LightDust");
        if (dustMat == null)
        {
            Debug.LogWarning("[LightBeam3D] Resources/LightDust 머티리얼이 없어 먼지를 만들지 못했습니다.", this);
            return;
        }

        GameObject go = new GameObject("Dust (자동 생성)");
        go.hideFlags = HideFlags.DontSave;
        go.layer = gameObject.layer;
        go.SetActive(false);
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
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(dustCount * 1.5f));
        if (!ps.isPlaying) main.prewarm = true;

        var emission = ps.emission;
        emission.enabled = dust;
        emission.rateOverTime = dustCount / ((lifeMin + lifeMax) * 0.5f);

        // 원뿔 부피 안에서 생겨남. 입자 원뿔은 +Z 로 뻗으므로 X 축으로 90도 돌려 아래(-Y)로 향하게
        float r0 = Mathf.Max(0.001f, startRadius);
        float len = Mathf.Max(0.01f, length);
        float angle = Mathf.Atan2(Mathf.Max(0f, endRadius - startRadius), len) * Mathf.Rad2Deg;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.radius = r0;
        shape.angle = Mathf.Clamp(angle, 0f, 89f);
        shape.length = len;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.randomDirectionAmount = 1f;

        var noise = ps.noise;
        noise.enabled = dustWander > 0f;
        noise.strength = dustWander;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.1f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.sizeAmount = 0.6f;

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
        Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        const int n = 16;
        Vector3 prevTop = Vector3.zero, prevBot = Vector3.zero;
        for (int i = 0; i <= n; i++)
        {
            float a = (float)i / n * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 top = dir * startRadius;
            Vector3 bot = dir * endRadius + Vector3.down * length;
            if (i > 0) { Gizmos.DrawLine(prevTop, top); Gizmos.DrawLine(prevBot, bot); }
            if (i % 4 == 0) Gizmos.DrawLine(top, bot);
            prevTop = top; prevBot = bot;
        }
    }
}
