using UnityEngine;

/// <summary>
/// [2026-10-23] 빛줄기 + 먼지 연출 (기본 레이어에서 2D 카메라가 그림).
///
/// 볼류메트릭 안개는 3D 카메라에서만 그려지므로, 배경을 기본 레이어에 둔 방(로비 등)에서는 빛줄기가 가려진다.
/// 이 컴포넌트는 배경 레이어를 바꾸지 않고 빛줄기 면과 떠다니는 먼지를 직접 그린다.
///
/// 사용법: 빈 오브젝트에 붙이고 '따라갈 조명'에 스포트라이트를 연결 → 조명 방향으로 빛줄기가 생김.
///  - 조명을 비워 두면 이 오브젝트 위치에서 아래(-Y) 방향으로 나감 (오브젝트를 돌려서 방향 조절)
///  - 편집 중에도 바로 보이므로 씬 화면에서 값을 바꾸며 맞추면 된다
/// </summary>
[ExecuteAlways]
public class LightShaftFX : MonoBehaviour
{
    [Header("빛 방향")]
    [Tooltip("이 조명의 위치·방향을 따라 빛줄기를 만든다 (비우면 이 오브젝트 위치에서 아래로)")]
    public Light sourceLight;
    [Tooltip("조명 위치에서 빛줄기 시작점을 이만큼 옮김 (가로, 세로)")]
    public Vector2 startOffset = Vector2.zero;
    [Tooltip("켜면 '끝 높이'에 닿을 때까지 자동으로 길이를 정함")]
    public bool lengthToFloor = true;
    [Tooltip("빛줄기가 끝나는 높이 (월드 Y). 보통 바닥 높이")]
    public float floorY = 0f;
    [Tooltip("자동 길이를 끄면 이 길이를 씀")]
    public float length = 30f;
    [Tooltip("빛줄기를 그릴 깊이 (월드 Z). 배경 벽보다 앞, 캐릭터(0)와 비슷하게")]
    public float depthZ = 1f;

    [Header("폭")]
    [Tooltip("시작 부분 폭")]
    public float startWidth = 1.5f;
    [Tooltip("조명 퍼짐 각도에 곱하는 값 (0 이면 평행한 빛줄기)")]
    [Range(0f, 2f)] public float spreadScale = 0.6f;
    [Tooltip("조명이 없을 때 끝 부분 폭")]
    public float endWidthNoLight = 6f;

    [Header("빛줄기 모양")]
    public Material shaftMaterial;
    [ColorUsage(false, true)] public Color color = new Color(1f, 0.86f, 0.62f, 1f);
    [Tooltip("밝기")]
    [Range(0f, 3f)] public float intensity = 0.35f;
    [Tooltip("가장자리 부드러움")]
    [Range(0.02f, 1f)] public float edgeSoftness = 0.5f;
    [Tooltip("끝으로 갈수록 흐려지는 정도")]
    [Range(0.1f, 5f)] public float lengthFalloff = 1.4f;
    [Tooltip("시작 부분이 서서히 나타나는 구간 (0~1)")]
    [Range(0f, 0.5f)] public float startFade = 0.1f;
    [Tooltip("세로 줄무늬 일렁임 세기")]
    [Range(0f, 1f)] public float streakAmount = 0.5f;
    [Tooltip("줄무늬 개수")]
    [Range(1f, 20f)] public float streakCount = 5f;
    [Tooltip("일렁임 속도")]
    [Range(0f, 2f)] public float streakSpeed = 0.25f;
    [Tooltip("그리기 순서 (낮을수록 뒤). 캐릭터·타일맵보다 낮게")]
    public int sortingOrder = -100;

    [Header("먼지")]
    public bool dust = true;
    public Material dustMaterial;
    [Tooltip("초당 생기는 먼지 수")]
    public float dustRate = 6f;
    [Tooltip("동시에 있을 수 있는 최대 개수")]
    public int dustMax = 60;
    [Tooltip("먼지 크기 (최소, 최대)")]
    public Vector2 dustSize = new Vector2(0.04f, 0.1f);
    [Tooltip("먼지 수명 초 (최소, 최대)")]
    public Vector2 dustLife = new Vector2(5f, 9f);
    [Tooltip("떠다니는 세기")]
    [Range(0f, 1f)] public float dustDrift = 0.15f;
    [ColorUsage(true, true)] public Color dustColor = new Color(1f, 0.9f, 0.7f, 0.7f);

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int EdgeId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int FalloffId = Shader.PropertyToID("_LengthFalloff");
    static readonly int StartFadeId = Shader.PropertyToID("_StartFade");
    static readonly int StreakAmountId = Shader.PropertyToID("_StreakAmount");
    static readonly int StreakCountId = Shader.PropertyToID("_StreakCount");
    static readonly int StreakSpeedId = Shader.PropertyToID("_StreakSpeed");

    GameObject shaftGo, dustGo;
    MeshFilter mf;
    MeshRenderer mr;
    Mesh mesh;
    ParticleSystem ps;
    MaterialPropertyBlock mpb;
    bool dirty = true;
    bool dustReady;

    void OnEnable()
    {
        Build();
        dirty = true;
    }

    void OnDisable()
    {
        Cleanup();
    }

    void OnValidate()
    {
        dirty = true;
    }

    void LateUpdate()
    {
        if (shaftGo == null || (ps != null) != dust) Build();   // 먼지 켜기/끄기가 바뀌면 다시 만듦
        // 조명이 움직일 수 있으므로 매 프레임 자세를 맞춘다 (모양 값이 바뀌었을 때만 먼지 설정을 다시 함)
        Layout(dirty);
        dirty = false;
    }

    void Build()
    {
        Cleanup();
        dustReady = false;
        const HideFlags flags = HideFlags.DontSave | HideFlags.NotEditable;

        shaftGo = new GameObject("[빛줄기]") { hideFlags = flags };
        shaftGo.layer = gameObject.layer;
        shaftGo.transform.SetParent(transform, false);
        mf = shaftGo.AddComponent<MeshFilter>();
        mr = shaftGo.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mesh = new Mesh { name = "LightShaftMesh", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;
        mpb = new MaterialPropertyBlock();

        if (dust)
        {
            dustGo = new GameObject("[먼지]") { hideFlags = flags };
            dustGo.layer = gameObject.layer;
            dustGo.transform.SetParent(transform, false);
            ps = dustGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Cleanup()
    {
        if (shaftGo != null) DestroySafe(shaftGo);
        if (dustGo != null) DestroySafe(dustGo);
        if (mesh != null) DestroySafe(mesh);
        shaftGo = dustGo = null;
        mesh = null;
        ps = null;
    }

    static void DestroySafe(Object o)
    {
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    void Layout(bool settingsChanged)
    {
        // 1) 시작점·방향·길이·폭 계산 (화면 평면 = XY)
        Vector2 origin, dir;
        float endWidth;
        if (sourceLight != null)
        {
            Vector3 lp = sourceLight.transform.position;
            Vector3 f = sourceLight.transform.forward;
            origin = new Vector2(lp.x, lp.y) + startOffset;
            dir = new Vector2(f.x, f.y);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            dir.Normalize();
        }
        else
        {
            Vector3 p = transform.position;
            origin = new Vector2(p.x, p.y) + startOffset;
            Vector3 d = -transform.up;
            dir = new Vector2(d.x, d.y);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            dir.Normalize();
        }

        float len = length;
        if (lengthToFloor && dir.y < -0.05f)
            len = Mathf.Max(0.5f, (origin.y - floorY) / -dir.y);
        len = Mathf.Max(0.1f, len);

        if (sourceLight != null && sourceLight.type == LightType.Spot)
            endWidth = startWidth + 2f * len * Mathf.Tan(sourceLight.spotAngle * 0.5f * Mathf.Deg2Rad) * spreadScale;
        else
            endWidth = sourceLight != null ? startWidth : endWidthNoLight;

        // 2) 빛줄기 면: 자식을 시작점에 두고, 자식의 -Y 가 빛 방향이 되게 돌림
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
        Transform st = shaftGo.transform;
        st.SetPositionAndRotation(new Vector3(origin.x, origin.y, depthZ), Quaternion.Euler(0f, 0f, angle));
        st.localScale = InverseLossy(transform);

        float h0 = startWidth * 0.5f, h1 = endWidth * 0.5f;
        mesh.Clear();
        mesh.SetVertices(new[] { new Vector3(-h0, 0f, 0f), new Vector3(h0, 0f, 0f), new Vector3(-h1, -len, 0f), new Vector3(h1, -len, 0f) });
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
        mesh.RecalculateBounds();

        mr.sharedMaterial = shaftMaterial;
        mr.sortingOrder = sortingOrder;
        mpb.Clear();
        mpb.SetColor(ColorId, color);
        mpb.SetFloat(IntensityId, intensity);
        mpb.SetFloat(EdgeId, edgeSoftness);
        mpb.SetFloat(FalloffId, lengthFalloff);
        mpb.SetFloat(StartFadeId, startFade);
        mpb.SetFloat(StreakAmountId, streakAmount);
        mpb.SetFloat(StreakCountId, streakCount);
        mpb.SetFloat(StreakSpeedId, streakSpeed);
        mr.SetPropertyBlock(mpb);

        // 3) 먼지: 빛줄기 가운데에 상자 모양으로 뿌림
        if (ps == null) return;
        Transform dt = dustGo.transform;
        Vector2 mid = origin + dir * (len * 0.55f);
        dt.SetPositionAndRotation(new Vector3(mid.x, mid.y, depthZ - 0.05f), Quaternion.Euler(0f, 0f, angle));
        dt.localScale = InverseLossy(transform);
        var shape = ps.shape;
        shape.scale = new Vector3(Mathf.Lerp(h0, h1, 0.55f) * 1.6f, len * 0.8f, 0.1f);

        if (!settingsChanged && dustReady) return;
        SetupDust();
        dustReady = true;
    }

    void SetupDust()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(dustLife.x, dustLife.y);
        main.startSize = new ParticleSystem.MinMaxCurve(dustSize.x, dustSize.y);
        main.startColor = dustColor;
        main.maxParticles = Mathf.Max(1, dustMax);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = dustRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        var noise = ps.noise;
        noise.enabled = dustDrift > 0f;
        noise.strength = dustDrift;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.1f;
        noise.damping = true;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = dustGo.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortingOrder + 1;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play(true);
    }

    // 부모 크기에 영향받지 않게 (빛줄기는 월드 크기로 그림)
    static Vector3 InverseLossy(Transform parent)
    {
        Vector3 s = parent.lossyScale;
        return new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, s.z != 0f ? 1f / s.z : 1f);
    }

    void OnDrawGizmosSelected()
    {
        if (shaftGo == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(shaftGo.transform.position, 0.3f);
    }
}
