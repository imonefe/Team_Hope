using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 밑에서 서서히 올라오는 잔불·불씨 파티클 (UI 전용).
///
/// 노드 캔버스·게임메뉴 같은 "Screen Space - Overlay" 캔버스에는 유니티 기본 파티클 시스템이 그려지지 않는다.
/// 그래서 이 스크립트는 파티클을 UI 그래픽(사각형 메쉬)으로 직접 그린다.
///  → 어느 캔버스에 넣어도 보이고, 하이어라키 순서대로 앞뒤가 정해진다 (배경 바로 아래 자식으로 두면 배경 위·버튼 아래).
///  → 실제 시간 기준으로 움직이므로 일시정지(시간 0) 중인 메뉴에서도 계속 움직인다.
///
/// 사용법: 캔버스 안에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤, RectTransform 을 화면 전체로 늘린다.
///        (프리팹 Assets/1.Prefab/UI/UI_Embers.prefab 을 끌어다 놓으면 끝)
/// 모양은 전부 인스펙터에서 조절. 점 이미지를 따로 넣지 않으면 부드러운 원형 점을 코드로 만들어 쓴다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIEmberParticles : MaskableGraphic
{
    [Header("점 이미지 (비워두면 부드러운 원형 점을 자동 생성)")]
    public Texture particleTexture;

    [Header("생성")]
    [Tooltip("초당 생성 개수")]
    public float spawnRate = 20f;
    [Tooltip("동시에 존재할 수 있는 최대 개수")]
    public int maxParticles = 120;
    [Tooltip("켜질 때 미리 돌려두는 시간(초). 처음부터 화면에 불씨가 떠 있게")]
    public float prewarmSeconds = 4f;
    [Tooltip("생성 위치: 화면 아래쪽 기준 높이 범위 (0 = 맨 아래, 음수 = 화면 밖 아래)")]
    public Vector2 spawnHeight = new Vector2(-0.05f, 0.08f);

    [Header("수명 / 사라지는 높이")]
    public Vector2 lifetime = new Vector2(3.5f, 7f);
    [Tooltip("이 높이(화면 비율)부터 서서히 사라지기 시작 → 두 번째 값에서 완전히 사라짐")]
    public Vector2 fadeHeight = new Vector2(0.55f, 0.95f);
    [Tooltip("생겨나서 완전히 보이기까지 시간(초)")]
    public float fadeInTime = 0.3f;

    [Header("잔불 (둥글고 부드럽게, 느리게)")]
    [Tooltip("크기 (픽셀, 캔버스 기준)")]
    public Vector2 emberSize = new Vector2(14f, 32f);
    [Tooltip("올라가는 속도 (화면 높이 비율 / 초)")]
    public Vector2 emberRiseSpeed = new Vector2(0.05f, 0.12f);

    [Header("불씨 (작고 밝고 빠르게, 위로 길쭉)")]
    [Range(0f, 1f)] public float sparkRatio = 0.25f;
    public Vector2 sparkSize = new Vector2(4f, 8f);
    public Vector2 sparkRiseSpeed = new Vector2(0.14f, 0.3f);
    [Tooltip("불씨가 움직이는 방향으로 늘어나는 배율")]
    public float sparkStretch = 3.5f;

    [Header("흔들림 / 바람")]
    [Tooltip("좌우로 살랑이는 폭 (픽셀)")]
    public Vector2 swayAmount = new Vector2(10f, 45f);
    [Tooltip("살랑이는 빠르기 (초당 횟수)")]
    public Vector2 swayFrequency = new Vector2(0.25f, 0.9f);
    [Tooltip("전체를 한쪽으로 미는 바람 (화면 너비 비율 / 초, 음수 = 왼쪽)")]
    public float wind = 0f;
    [Tooltip("사선으로 올라가기: 위로 1 올라갈 때 옆으로 얼마나 가는지 (0 = 똑바로 위, -0.7 = 우측 아래 → 좌측 위, 양수 = 좌측 아래 → 우측 위). 0 이 아니면 옆 가장자리에서도 생겨남")]
    [Range(-2f, 2f)] public float diagonal = -0.7f;

    [Header("색 (수명에 따라)")]
    public Gradient colorOverLife = DefaultGradient();
    [Tooltip("밝기 깜빡임 정도 (0 = 없음)")]
    [Range(0f, 1f)] public float flicker = 0.35f;
    [Tooltip("전체 밝기 배율")]
    [Range(0f, 2f)] public float brightness = 1f;
    [Tooltip("채도 (0 = 색 없음·회백색 재, 1 = 원래 불씨 색)")]
    [Range(0f, 1f)] public float saturation = 0f;

    [Header("바닥 붉은 빛 (선택)")]
    [Tooltip("화면 맨 아래 은은한 붉은 빛 (끄면 파티클만)")]
    public bool bottomGlow = false;
    public Color glowColor = new Color(1f, 0.32f, 0.08f, 0.22f);
    [Tooltip("바닥 빛 높이 (화면 높이 비율)")]
    [Range(0f, 1f)] public float glowHeight = 0.22f;
    [Tooltip("바닥 빛 일렁임 정도")]
    [Range(0f, 1f)] public float glowPulse = 0.3f;

    struct Particle
    {
        public float x, y;          // 0~1 (화면 비율), y 는 아래 → 위
        public float age, life;
        public float size;
        public float rise;          // 화면 높이 비율 / 초
        public float swayAmp, swayFreq, swayPhase;
        public float flickerSeed;
        public bool spark;
    }

    readonly List<Particle> particles = new List<Particle>();
    float spawnAccum;
    float time;
    float aspectCache = 16f / 9f;

    static Texture2D generatedDot;

    public override Texture mainTexture
    {
        get
        {
            if (particleTexture != null) return particleTexture;
            if (generatedDot == null) generatedDot = MakeDot(64);
            return generatedDot;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;   // 클릭은 통과
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        particles.Clear();
        spawnAccum = 0f;
        // 미리 돌려두기 (0.1초 단위)
        float t = 0f;
        while (t < prewarmSeconds)
        {
            Simulate(0.1f);
            t += 0.1f;
        }
        SetVerticesDirty();
    }

    void Update()
    {
        Simulate(Time.unscaledDeltaTime);
        SetVerticesDirty();
    }

    void Simulate(float dt)
    {
        if (dt <= 0f) return;
        time += dt;

        // 생성
        spawnAccum += spawnRate * dt;
        while (spawnAccum >= 1f)
        {
            spawnAccum -= 1f;
            if (particles.Count < maxParticles) particles.Add(NewParticle());
        }

        // 이동 / 수명
        aspectCache = Aspect();
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            p.age += dt;
            if (p.age >= p.life || p.y > fadeHeight.y + 0.05f || p.x < -0.15f || p.x > 1.15f)
            {
                particles.RemoveAt(i);
                continue;
            }
            p.y += p.rise * dt;
            p.x += wind * dt + p.rise * diagonal / aspectCache * dt;   // 사선: 세로 속도 × 비율 (픽셀 기준)
            particles[i] = p;
        }
    }

    // 화면 가로/세로 비율 (사선 이동을 픽셀 기준으로 맞추기 위해)
    float Aspect()
    {
        Rect r = rectTransform.rect;
        return r.height > 1f ? r.width / r.height : 16f / 9f;
    }

    Particle NewParticle()
    {
        bool spark = Random.value < sparkRatio;

        // 생성 위치: 기본은 화면 아래. 사선이면 들어오는 쪽 옆 가장자리에서도 (드나드는 양에 비례해서)
        float sx = Random.value;
        float sy = Random.Range(spawnHeight.x, spawnHeight.y);
        if (Mathf.Abs(diagonal) > 0.001f)
        {
            float aspect = Aspect();
            float sideMax = Mathf.Max(0.05f, fadeHeight.x * 0.8f);     // 옆에서 생기는 높이 범위 (너무 높으면 바로 사라지므로)
            float bottomWeight = aspect;                                // 아래 가장자리 길이 × 세로 속도(1)
            float sideWeight = sideMax * Mathf.Abs(diagonal);           // 옆 가장자리 길이 × 가로 속도
            if (Random.value < sideWeight / (bottomWeight + sideWeight))
            {
                sx = diagonal < 0f ? 1f + Random.Range(0f, 0.03f) : -Random.Range(0f, 0.03f);   // 화면 밖 옆에서
                sy = Random.Range(spawnHeight.x, sideMax);
            }
        }

        Particle p = new Particle
        {
            x = sx,
            y = sy,
            age = 0f,
            life = Random.Range(lifetime.x, lifetime.y) * (spark ? 0.6f : 1f),
            size = spark ? Random.Range(sparkSize.x, sparkSize.y) : Random.Range(emberSize.x, emberSize.y),
            rise = spark ? Random.Range(sparkRiseSpeed.x, sparkRiseSpeed.y) : Random.Range(emberRiseSpeed.x, emberRiseSpeed.y),
            swayAmp = Random.Range(swayAmount.x, swayAmount.y) * (spark ? 0.5f : 1f),
            swayFreq = Random.Range(swayFrequency.x, swayFrequency.y),
            swayPhase = Random.value * Mathf.PI * 2f,
            flickerSeed = Random.value * 100f,
            spark = spark
        };
        return p;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;

        // 바닥 붉은 빛 (아래 진하고 위로 갈수록 투명)
        if (bottomGlow && glowColor.a > 0f && glowHeight > 0f)
        {
            float pulse = 1f - glowPulse * 0.5f * (1f + Mathf.Sin(time * 1.3f) * 0.6f + Mathf.Sin(time * 3.7f) * 0.4f);
            Color bottom = glowColor; bottom.a *= pulse;
            Color top = glowColor; top.a = 0f;
            Vector2 uvC = new Vector2(0.5f, 0.5f);   // 점 텍스처 가운데(가장 밝은 곳)만 사용 → 단색
            int s = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), bottom, uvC);
            vh.AddVert(new Vector3(r.xMin, r.yMin + r.height * glowHeight), top, uvC);
            vh.AddVert(new Vector3(r.xMax, r.yMin + r.height * glowHeight), top, uvC);
            vh.AddVert(new Vector3(r.xMax, r.yMin), bottom, uvC);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }

        for (int i = 0; i < particles.Count; i++)
        {
            Particle p = particles[i];
            float t01 = p.age / p.life;

            // 투명도: 나타나기 × 높이에 따라 사라지기 × 수명 끝에서 사라지기
            float a = fadeInTime > 0f ? Mathf.Clamp01(p.age / fadeInTime) : 1f;
            a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeHeight.x, fadeHeight.y, p.y));
            a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, t01));
            if (a <= 0.003f) continue;

            // 깜빡임
            float fl = 1f - flicker * (0.5f + 0.5f * Mathf.Sin(time * (p.spark ? 23f : 11f) + p.flickerSeed)) * Mathf.PerlinNoise(p.flickerSeed, time * 2f);

            Color c = colorOverLife.Evaluate(t01) * color;   // color = 인스펙터의 Graphic 색 (전체 색조)
            c.r *= brightness * fl; c.g *= brightness * fl; c.b *= brightness * fl;
            c.a *= a * (p.spark ? 1f : 0.9f);
            if (p.spark) { c.r = Mathf.Min(1f, c.r * 1.2f); c.g = Mathf.Min(1f, c.g * 1.25f + 0.1f); }

            // 채도 빼기: 밝기만 남기고 색을 회색 쪽으로
            if (saturation < 1f)
            {
                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                c.r = Mathf.Lerp(lum, c.r, saturation);
                c.g = Mathf.Lerp(lum, c.g, saturation);
                c.b = Mathf.Lerp(lum, c.b, saturation);
            }

            // 위치: 위로 + 좌우로 살랑
            float sway = Mathf.Sin(time * p.swayFreq * Mathf.PI * 2f + p.swayPhase) * p.swayAmp;
            float px = r.xMin + p.x * r.width + sway;
            float py = r.yMin + p.y * r.height;

            // 크기: 수명 끝으로 갈수록 작아짐
            float size = p.size * Mathf.Lerp(1f, 0.4f, t01);
            float halfW = size * 0.5f;
            float halfH = p.spark ? size * 0.5f * sparkStretch : size * 0.5f;

            // 불씨는 움직이는 방향(위 + 흔들림 방향)으로 기울임
            Vector2 up = Vector2.up;
            if (p.spark)
            {
                float swayVel = Mathf.Cos(time * p.swayFreq * Mathf.PI * 2f + p.swayPhase) * p.swayAmp * p.swayFreq * Mathf.PI * 2f;
                up = new Vector2(swayVel + wind * r.width + p.rise * diagonal * r.height, p.rise * r.height).normalized;
            }
            Vector2 right = new Vector2(up.y, -up.x);

            Vector2 center = new Vector2(px, py);
            Vector2 a0 = center - right * halfW - up * halfH;
            Vector2 a1 = center - right * halfW + up * halfH;
            Vector2 a2 = center + right * halfW + up * halfH;
            Vector2 a3 = center + right * halfW - up * halfH;

            int s0 = vh.currentVertCount;
            vh.AddVert(a0, c, new Vector2(0f, 0f));
            vh.AddVert(a1, c, new Vector2(0f, 1f));
            vh.AddVert(a2, c, new Vector2(1f, 1f));
            vh.AddVert(a3, c, new Vector2(1f, 0f));
            vh.AddTriangle(s0, s0 + 1, s0 + 2);
            vh.AddTriangle(s0, s0 + 2, s0 + 3);
        }
    }

    static Gradient DefaultGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.3f),
                new GradientColorKey(new Color(0.95f, 0.28f, 0.06f), 0.7f),
                new GradientColorKey(new Color(0.55f, 0.08f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0.6f, 1f)
            });
        return g;
    }

    // 부드러운 원형 점 (가운데 밝은 심 + 바깥 번짐)
    static Texture2D MakeDot(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        Color[] px = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2((x - half) / half, (y - half) / half).magnitude;   // 0 가운데 → 1 가장자리
                float core = Mathf.Clamp01(1f - d / 0.35f);
                float glow = Mathf.Clamp01(1f - d);
                float a = Mathf.Clamp01(core * core * 0.9f + Mathf.Pow(glow, 1.6f) * 0.7f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
