using UnityEngine;

/// <summary>
/// 스포트라이트(또는 점광원)에 붙이면, 실행할 때 그 빛 안에 "빛받는 먼지" 파티클을 자동으로 만든다.
/// - 빛의 각도·범위를 읽어서 빛줄기 모양(원뿔) 안에서만 먼지가 생긴다.
/// - 빛의 각도·범위·켜짐을 바꾸면 먼지도 따라 바뀐다. (빛이 꺼지면 먼지도 사라짐)
/// - 먼지는 빛받는 먼지 셰이더(LitDust3D)를 써서, 실제로 빛이 닿는 곳만 반짝이고 그림자 속에서는 안 보인다.
/// - 먼지 오브젝트는 실행 중에만 만들어지고 저장되지 않는다. (프리팹·씬 파일은 그대로)
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class SpotLightDust : MonoBehaviour
{
    [Header("양")]
    [Tooltip("빛줄기 하나에 동시에 떠 있는 먼지 수")]
    [Min(1)] public int dustCount = 250;
    [Tooltip("크고 흐린 빛망울도 같이 만들기")]
    public bool bokeh = false;
    [Tooltip("빛망울 수")]
    [Min(0)] public int bokehCount = 8;

    [Header("빛줄기 안 어디까지 채울지")]
    [Tooltip("빛이 나오는 곳에서 이 거리부터 먼지를 채움 (빛 바로 앞은 너무 밝아서 비움)")]
    [Min(0f)] public float startDistance = 3f;
    [Tooltip("빛 범위(Range) 중 몇 %까지 채울지")]
    [Range(0.05f, 1f)] public float lengthRatio = 0.5f;
    [Tooltip("채우는 길이의 최대값 (범위가 아주 큰 빛용)")]
    [Min(1f)] public float maxLength = 120f;
    [Tooltip("빛줄기 굵기 중 몇 %까지 채울지 (1보다 작으면 가장자리를 조금 비움)")]
    [Range(0.1f, 1f)] public float widthRatio = 0.9f;

    [Header("먼지 모양")]
    public Vector2 dustSize = new Vector2(0.08f, 0.4f);
    public Vector2 bokehSize = new Vector2(0.7f, 2f);
    public Vector2 lifetime = new Vector2(7f, 12f);
    [Tooltip("떠다니는 흔들림 세기")]
    [Min(0f)] public float driftStrength = 0.3f;
    [Tooltip("먼지마다 제각각 천천히 흘러가는 속도")]
    [Min(0f)] public float floatSpeed = 0.15f;
    [Tooltip("떠다니는 흔들림 무늬 크기 (작을수록 큰 흐름)")]
    [Min(0.01f)] public float driftFrequency = 0.15f;

    [Header("밝기")]
    [Tooltip("켜면 빛 세기·거리에 맞춰 먼지 밝기를 자동으로 맞춤 (빛마다 세기가 크게 달라도 먼지가 비슷하게 보임)")]
    public bool autoBrightness = true;
    [Tooltip("자동 밝기일 때 빛줄기 가운데 먼지의 밝기 (0~1, 클수록 밝음)")]
    [Range(0.1f, 0.98f)] public float targetBrightness = 0.75f;

    [Header("그리기")]
    [Tooltip("먼지를 그릴 레이어 이름 (3D 카메라가 찍는 레이어여야 함)")]
    public string layerName = "Room3D";
    [Tooltip("비우면 Resources/LitDust3D 를 씀")]
    public Material dustMaterial;
    [Tooltip("비우면 Resources/LitDust3D_Bokeh 를 씀")]
    public Material bokehMaterial;

    Light lightComp;
    ParticleSystem dustPS, bokehPS;
    float lastAngle = -1f, lastRange = -1f, lastIntensity = -1f;
    LightType lastType;
    bool lastOn;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        lightComp = GetComponent<Light>();
        if (dustMaterial == null) dustMaterial = Resources.Load<Material>("LitDust3D");
        if (bokehMaterial == null) bokehMaterial = Resources.Load<Material>("LitDust3D_Bokeh");

        if (dustPS == null) dustPS = CreateSystem("빛줄기 먼지 (자동 생성)", dustMaterial);
        if (bokeh && bokehPS == null) bokehPS = CreateSystem("빛줄기 빛망울 (자동 생성)", bokehMaterial);
        lastAngle = -1f;    // 다음 프레임에 모양 다시 맞춤
        Sync(true);
    }

    void OnDisable()
    {
        if (dustPS != null) dustPS.gameObject.SetActive(false);
        if (bokehPS != null) bokehPS.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (dustPS != null) Destroy(dustPS.gameObject);
        if (bokehPS != null) Destroy(bokehPS.gameObject);
    }

    void LateUpdate()
    {
        if (lightComp == null) return;
        Sync(false);
    }

    // 빛 상태(켜짐·각도·범위)가 바뀌면 먼지도 맞춰 줌
    void Sync(bool force)
    {
        bool on = lightComp.enabled && lightComp.intensity > 0f && lightComp.gameObject.activeInHierarchy;
        if (force || on != lastOn)
        {
            lastOn = on;
            if (dustPS != null) dustPS.gameObject.SetActive(on);
            if (bokehPS != null) bokehPS.gameObject.SetActive(on && bokeh);
        }
        if (!on) return;

        if (force || !Mathf.Approximately(lastAngle, lightComp.spotAngle) || !Mathf.Approximately(lastRange, lightComp.range) || lastType != lightComp.type)
        {
            // 모양이 바뀜 → 먼지를 새로 채움
            lastAngle = lightComp.spotAngle; lastRange = lightComp.range; lastType = lightComp.type;
            if (dustPS != null) Configure(dustPS, dustCount, dustSize, driftStrength, new Color(0.4f, 0.37f, 0.33f, 1f), false);
            if (bokehPS != null) Configure(bokehPS, bokehCount, bokehSize, driftStrength * 0.4f, new Color(0.5f, 0.5f, 0.5f, 1f), true);
            lastIntensity = -1f;
        }
        if (!Mathf.Approximately(lastIntensity, lightComp.intensity))
        {
            // 세기만 바뀜 → 밝기만 다시 맞춤 (먼지는 그대로)
            lastIntensity = lightComp.intensity;
            ApplyBrightness(dustPS);
            ApplyBrightness(bokehPS);
        }
    }

    ParticleSystem CreateSystem(string name, Material mat)
    {
        GameObject go = new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        int layer = LayerMask.NameToLayer(layerName);
        go.layer = layer >= 0 ? layer : gameObject.layer;
        go.transform.SetParent(transform, false);   // 빛과 같은 위치·방향 (원뿔이 빛 방향 +Z 로 뻗음)

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // 빛이 움직여도 먼지는 빛줄기 안에 머묾
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.randomDirectionAmount = 1f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;

        // 생길 때 서서히 나타나고, 사라질 때 서서히 흐려짐
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 0.5f;
        return ps;
    }

    void Configure(ParticleSystem ps, int count, Vector2 size, float drift, Color dimColor, bool isBokeh)
    {
        bool spot = lightComp.type == LightType.Spot;
        float range = lightComp.range;
        float length = Mathf.Min(range * lengthRatio, maxLength);
        float start = Mathf.Min(startDistance, length * 0.5f);
        float half = Mathf.Clamp(lightComp.spotAngle * 0.5f * widthRatio, 1f, 85f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, Mathf.Max(lifetime.x, lifetime.y));
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, Mathf.Max(size.x, size.y));
        main.startColor = new ParticleSystem.MinMaxGradient(dimColor, Color.white);
        main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(count * 1.3f));

        var emission = ps.emission;
        float avgLife = (lifetime.x + Mathf.Max(lifetime.x, lifetime.y)) * 0.5f;
        emission.rateOverTime = count / Mathf.Max(0.1f, avgLife);

        var shape = ps.shape;
        if (spot)
        {
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = half;
            shape.position = new Vector3(0f, 0f, start);
            shape.radius = Mathf.Max(0.01f, start * Mathf.Tan(half * Mathf.Deg2Rad));
            shape.length = Mathf.Max(0.1f, length - start);
            shape.rotation = Vector3.zero;
        }
        else
        {
            // 점광원: 빛 둘레 구 안에 채움
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.position = Vector3.zero;
            shape.radius = Mathf.Max(0.1f, range * lengthRatio);
        }

        var noise = ps.noise;
        noise.strength = drift;
        noise.frequency = isBokeh ? driftFrequency * 0.5f : driftFrequency;
        noise.scrollSpeed = isBokeh ? 0.05f : 0.1f;

        // 먼지마다 다른 방향으로 천천히 흘러감 (흔들림만으로는 거의 제자리라서)
        float fs = isBokeh ? floatSpeed * 0.4f : floatSpeed;
        var vel = ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(-fs, fs);
        vel.y = new ParticleSystem.MinMaxCurve(-fs, fs);
        vel.z = new ParticleSystem.MinMaxCurve(-fs, fs);

        ps.Play(true);
    }

    // 밝기 자동 맞춤: 빛줄기 가운데 지점에 닿는 빛의 양을 계산해서, 거기 먼지가 targetBrightness 가 되게 셰이더 값을 정함
    void ApplyBrightness(ParticleSystem ps)
    {
        if (ps == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (autoBrightness)
        {
            bool spot = lightComp.type == LightType.Spot;
            float length = Mathf.Min(lightComp.range * lengthRatio, maxLength);
            float start = Mathf.Min(startDistance, length * 0.5f);
            float mid = spot ? start + (length - start) * 0.5f : Mathf.Max(0.5f, lightComp.range * lengthRatio * 0.5f);
            Color c = lightComp.color.linear * lightComp.intensity;   // URP 는 선형 색으로 계산
            float power = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float atMid = power / Mathf.Max(0.01f, mid * mid);          // 거리 제곱에 반비례해서 약해짐
            float gain = -Mathf.Log(1f - targetBrightness) / Mathf.Max(1e-4f, atMid);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat("_LightGain", gain);
            r.SetPropertyBlock(block);
        }
        else r.SetPropertyBlock(null);
    }
}
