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
    public enum ShapeStyle { AshFlakes, Round }

    [Header("모양")]
    [Tooltip("재 조각: 불규칙한 재·파편·부스러기 모양 8종이 돌면서 팔랑임 / 둥근 점: 예전 모양")]
    public ShapeStyle shapeStyle = ShapeStyle.AshFlakes;
    [Tooltip("직접 만든 이미지를 쓰고 싶을 때 (넣으면 위 모양 대신 이 이미지 한 장을 씀)")]
    public Texture particleTexture;
    [Tooltip("잔불이 도는 속도 (초당 각도, 최소~최대. 음수 = 반대 방향)")]
    public Vector2 spinSpeed = new Vector2(-90f, 90f);
    [Tooltip("종이처럼 뒤집히며 팔랑이는 빠르기 (초당 횟수)")]
    public Vector2 tumbleSpeed = new Vector2(0.3f, 1.2f);
    [Tooltip("뒤집힐 때 가장 얇아지는 폭 (1 = 안 뒤집힘, 0.25 = 거의 옆면까지)")]
    [Range(0.05f, 1f)] public float tumbleMinWidth = 0.25f;

    [Header("생성")]
    [Tooltip("초당 생성 개수")]
    public float spawnRate = 6f;
    [Tooltip("동시에 존재할 수 있는 최대 개수")]
    public int maxParticles = 36;
    [Tooltip("켜질 때 미리 돌려두는 시간(초). 처음부터 화면에 불씨가 떠 있게 (아래 '등장 바람'을 켜면 무시됨)")]
    public float prewarmSeconds = 4f;
    [Tooltip("움직임 전체 빠르기 (1 = 기본, 0.7 = 0.7배 느리게). 올라가는 속도·흔들림·깜빡임·수명이 모두 함께 느려진다")]
    [Range(0.1f, 2f)] public float motionSpeed = 0.7f;

    [Header("등장 바람 (켜질 때 불씨가 휘날려 들어오는 연출)")]
    [Tooltip("켜면 미리 돌려두지 않고, 켜지는 순간 바람에 실려 불씨가 휘날려 들어온다 (일시정지 메뉴처럼 갑자기 켜지는 화면용)")]
    public bool introGust = false;
    [Tooltip("바람이 잦아드는 데 걸리는 시간(초)")]
    public float gustDuration = 1f;
    [Tooltip("바람 처음 순간의 생성량 배율")]
    public float gustSpawnBoost = 4f;
    [Tooltip("바람 처음 순간의 이동 속도 배율")]
    public float gustSpeedBoost = 3f;
    [Tooltip("켜질 때 전체가 서서히 나타나는 시간(초)")]
    public float wholeFadeIn = 0.4f;
    [Tooltip("생성 위치: 화면 아래쪽 기준 높이 범위 (0 = 맨 아래, 음수 = 화면 밖 아래)")]
    public Vector2 spawnHeight = new Vector2(-0.05f, 0.08f);

    [Header("수명 / 사라지는 높이")]
    public Vector2 lifetime = new Vector2(5.5f, 11f);
    [Tooltip("이 높이(화면 비율)부터 서서히 사라지기 시작 → 두 번째 값에서 완전히 사라짐")]
    public Vector2 fadeHeight = new Vector2(0.55f, 0.95f);
    [Tooltip("생겨나서 완전히 보이기까지 시간(초)")]
    public float fadeInTime = 0.3f;

    [Header("잔불 (둥글고 부드럽게, 느리게)")]
    [Tooltip("크기 (픽셀, 캔버스 기준)")]
    public Vector2 emberSize = new Vector2(22f, 46f);
    [Tooltip("올라가는 속도 (화면 높이 비율 / 초)")]
    public Vector2 emberRiseSpeed = new Vector2(0.03f, 0.07f);

    [Header("불씨 (작고 밝고 빠르게, 위로 길쭉)")]
    [Range(0f, 1f)] public float sparkRatio = 0.25f;
    public Vector2 sparkSize = new Vector2(4f, 8f);
    public Vector2 sparkRiseSpeed = new Vector2(0.08f, 0.18f);
    [Tooltip("불씨가 움직이는 방향으로 늘어나는 배율")]
    public float sparkStretch = 3.5f;

    [Header("흔들림 / 바람")]
    [Tooltip("좌우로 살랑이는 폭 (픽셀)")]
    public Vector2 swayAmount = new Vector2(10f, 45f);
    [Tooltip("살랑이는 빠르기 (초당 횟수)")]
    public Vector2 swayFrequency = new Vector2(0.18f, 0.6f);
    [Tooltip("전체를 한쪽으로 미는 바람 (화면 너비 비율 / 초, 음수 = 왼쪽)")]
    public float wind = 0f;
    [Tooltip("사선으로 올라가기: 위로 1 올라갈 때 옆으로 얼마나 가는지 (0 = 똑바로 위, -0.7 = 우측 아래 → 좌측 위, 양수 = 좌측 아래 → 우측 위). 0 이 아니면 옆 가장자리에서도 생겨남")]
    [Range(-2f, 2f)] public float diagonal = -0.7f;

    [Header("색 (수명에 따라)")]
    public Gradient colorOverLife = DefaultGradient();
    [Tooltip("밝기 깜빡임 정도 (0 = 없음)")]
    [Range(0f, 1f)] public float flicker = 0.35f;
    [Tooltip("전체 밝기 배율")]
    [Range(0f, 2f)] public float brightness = 0.7f;
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
        public int shape;           // 모양 번호 (0~6 잔불, 7 불씨 꼬리)
        public float rotation;      // 각도 (도)
        public float spin;          // 도는 속도 (도 / 초)
        public float tumbleFreq, tumblePhase;
    }

    readonly List<Particle> particles = new List<Particle>();
    float spawnAccum;
    float time;
    float aspectCache = 16f / 9f;
    float enabledTime;          // 켜진 뒤 지난 실제 시간 (등장 바람·전체 페이드용)

    static Texture2D generatedDot;
    static Texture2D generatedAtlas;
    const int AtlasCols = 4, AtlasRows = 2, AtlasCell = 64;

    bool UseAtlas => particleTexture == null && shapeStyle == ShapeStyle.AshFlakes;

    public override Texture mainTexture
    {
        get
        {
            if (particleTexture != null) return particleTexture;
            if (shapeStyle == ShapeStyle.AshFlakes)
            {
                if (generatedAtlas == null) generatedAtlas = MakeFlakeAtlas();
                return generatedAtlas;
            }
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
        enabledTime = introGust ? 0f : 9999f;   // 등장 바람을 안 쓰면 처음부터 '다 나타난 상태'
        if (!introGust)
        {
            // 미리 돌려두기 (0.1초 단위)
            float t = 0f;
            while (t < prewarmSeconds)
            {
                Simulate(0.1f);
                t += 0.1f;
            }
        }
        SetVerticesDirty();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (enabledTime < 9999f) enabledTime += dt;
        Simulate(dt * motionSpeed);
        SetVerticesDirty();
    }

    // 등장 바람 세기 (1 = 막 켜진 순간 → 0 = 잦아듦)
    float GustAmount()
    {
        if (!introGust || gustDuration <= 0f || enabledTime >= gustDuration) return 0f;
        float k = 1f - enabledTime / gustDuration;
        return k * k;
    }

    void Simulate(float dt)
    {
        if (dt <= 0f) return;
        time += dt;

        float gust = GustAmount();
        float spawnMul = 1f + (gustSpawnBoost - 1f) * gust;
        float speedMul = 1f + (gustSpeedBoost - 1f) * gust;
        int cap = Mathf.RoundToInt(maxParticles * (1f + gust));

        // 생성
        spawnAccum += spawnRate * spawnMul * dt;
        while (spawnAccum >= 1f)
        {
            spawnAccum -= 1f;
            if (particles.Count < cap) particles.Add(NewParticle());
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
            float mdt = dt * speedMul;   // 등장 바람 동안은 더 빠르게 휘날림
            p.rotation += p.spin * mdt;
            p.y += p.rise * mdt;
            p.x += wind * mdt + p.rise * diagonal / aspectCache * mdt;   // 사선: 세로 속도 × 비율 (픽셀 기준)
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
            spark = spark,
            shape = spark ? 7 : Random.Range(0, 7),
            rotation = Random.value * 360f,
            spin = Random.Range(spinSpeed.x, spinSpeed.y),
            tumbleFreq = Random.Range(tumbleSpeed.x, tumbleSpeed.y),
            tumblePhase = Random.value * Mathf.PI * 2f
        };
        return p;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float whole = wholeFadeIn > 0f ? Mathf.Clamp01(enabledTime / wholeFadeIn) : 1f;   // 켜질 때 전체 페이드인

        // 바닥 붉은 빛 (아래 진하고 위로 갈수록 투명)
        if (bottomGlow && glowColor.a > 0f && glowHeight > 0f)
        {
            float pulse = 1f - glowPulse * 0.5f * (1f + Mathf.Sin(time * 1.3f) * 0.6f + Mathf.Sin(time * 3.7f) * 0.4f);
            Color bottom = glowColor; bottom.a *= pulse * whole;
            Color top = glowColor; top.a = 0f;
            Vector2 uvC = UseAtlas ? new Vector2(0.5f / AtlasCols, 1f - 0.5f / AtlasRows) : new Vector2(0.5f, 0.5f);   // 모양 가운데(불투명한 곳)만 사용 → 단색
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
            c.a *= a * (p.spark ? 1f : 0.9f) * whole;
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
            else if (UseAtlas)
            {
                // 잔불(재 조각): 빙글 돌면서, 종이처럼 뒤집혀 폭이 얇아졌다 넓어짐
                float rad = p.rotation * Mathf.Deg2Rad;
                up = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                halfW *= Mathf.Max(tumbleMinWidth, Mathf.Abs(Mathf.Cos(time * p.tumbleFreq * Mathf.PI * 2f + p.tumblePhase)));
            }
            Vector2 right = new Vector2(up.y, -up.x);

            // 그림판에서 이 모양이 있는 칸
            Vector2 uv0 = Vector2.zero, uv1 = Vector2.one;
            if (UseAtlas)
            {
                int col = p.shape % AtlasCols;
                int row = p.shape / AtlasCols;                      // 0 = 위 줄
                float u0 = (float)col / AtlasCols, u1 = (float)(col + 1) / AtlasCols;
                float v0 = (float)(AtlasRows - 1 - row) / AtlasRows, v1 = (float)(AtlasRows - row) / AtlasRows;
                uv0 = new Vector2(u0, v0); uv1 = new Vector2(u1, v1);
            }

            Vector2 center = new Vector2(px, py);
            Vector2 a0 = center - right * halfW - up * halfH;
            Vector2 a1 = center - right * halfW + up * halfH;
            Vector2 a2 = center + right * halfW + up * halfH;
            Vector2 a3 = center + right * halfW - up * halfH;

            int s0 = vh.currentVertCount;
            vh.AddVert(a0, c, new Vector2(uv0.x, uv0.y));
            vh.AddVert(a1, c, new Vector2(uv0.x, uv1.y));
            vh.AddVert(a2, c, new Vector2(uv1.x, uv1.y));
            vh.AddVert(a3, c, new Vector2(uv1.x, uv0.y));
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

    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Rnd(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // 재 조각 모양 8종 그림판 (4칸 × 2줄, 칸마다 64픽셀). 항상 같은 모양이 나오도록 씨앗 고정
    //  0~2 재 조각(불규칙한 판, 가장자리 밝게) / 3~4 찢긴 파편(각진 조각) / 5 휘어진 부스러기(초승달)
    //  6 작은 알갱이 묶음 / 7 불씨 꼬리(앞쪽 = 위가 굵고 뒤로 가늘게)
    static Texture2D MakeFlakeAtlas()
    {
        int n = AtlasCell;
        int w = n * AtlasCols, h = n * AtlasRows;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        Color[] px = new Color[w * h];

        for (int idx = 0; idx < AtlasCols * AtlasRows; idx++)
        {
            System.Random rng = new System.Random(idx * 7919 + 13);
            int cellX = (idx % AtlasCols) * n;
            int cellY = (AtlasRows - 1 - idx / AtlasCols) * n;   // 0번 줄이 그림 위쪽

            // 모양 값 미리 정하기
            float aspect = 1f;
            float[] amp = new float[10], ph = new float[10];
            Vector2[] pts = null;
            if (idx <= 2)
            {
                aspect = Rnd(rng, 0.7f, 1f);
                for (int k = 2; k < 10; k++) { amp[k] = Rnd(rng, 0.05f, 0.22f) / Mathf.Pow(k, 0.8f); ph[k] = Rnd(rng, 0f, Mathf.PI * 2f); }
            }
            else if (idx <= 4)
            {
                int k = rng.Next(3, 6);
                float baseRot = Rnd(rng, 0f, Mathf.PI * 2f);
                float[] angs = new float[k];
                for (int i = 0; i < k; i++) angs[i] = Mathf.Repeat((i + Rnd(rng, -0.3f, 0.3f)) * Mathf.PI * 2f / k + baseRot, Mathf.PI * 2f);
                System.Array.Sort(angs);
                pts = new Vector2[k];
                for (int i = 0; i < k; i++) pts[i] = new Vector2(Mathf.Cos(angs[i]), Mathf.Sin(angs[i])) * Rnd(rng, 0.45f, 0.82f);
            }
            float stain = Rnd(rng, 0f, 10f);

            for (int py = 0; py < n; py++)
            {
                for (int pxl = 0; pxl < n; pxl++)
                {
                    float u = (pxl + 0.5f) / n * 2f - 1f;
                    float v = ((n - 1 - py) + 0.5f) / n * 2f - 1f;   // 위쪽 = -1, 아래쪽 = +1
                    float a = 0f, shade = 1f;

                    if (idx <= 4)
                    {
                        float L, r;
                        if (idx <= 2)
                        {
                            float uu = u / aspect;
                            float th = Mathf.Atan2(v, uu);
                            L = Mathf.Sqrt(uu * uu + v * v);
                            r = 1f;
                            for (int k = 2; k < 10; k++) r += amp[k] * Mathf.Sin(k * th + ph[k]);
                            r *= 0.6f;
                        }
                        else
                        {
                            float th = Mathf.Atan2(v, u);
                            L = Mathf.Sqrt(u * u + v * v);
                            float dx = Mathf.Cos(th), dy = Mathf.Sin(th);
                            float best = 10f;
                            for (int i = 0; i < pts.Length; i++)
                            {
                                Vector2 p0 = pts[i], p1 = pts[(i + 1) % pts.Length];
                                float ex = p1.x - p0.x, ey = p1.y - p0.y;
                                float den = dx * ey - dy * ex;
                                if (Mathf.Abs(den) < 1e-6f) continue;
                                float rr = (p0.x * ey - p0.y * ex) / den;
                                if (rr <= 0f) continue;
                                float t = ((dx * rr - p0.x) * ex + (dy * rr - p0.y) * ey) / (ex * ex + ey * ey);
                                if (t >= -0.001f && t <= 1.001f && rr < best) best = rr;
                            }
                            r = best > 5f ? 0.01f : best;
                        }
                        float d = L / Mathf.Max(r, 0.001f);
                        a = 1f - SS(0.88f, 1f, d);
                        shade = 0.8f + 0.2f * SS(0.45f, 0.95f, d);                                 // 가장자리가 더 밝게 (타는 테두리)
                        shade *= 0.85f + 0.15f * Mathf.Sin(u * 9f + stain) * Mathf.Sin(v * 7f + stain * 1.3f);   // 재 얼룩
                        a = Mathf.Max(a, (1f - SS(1f, 1.45f, d)) * 0.22f);                            // 은은한 번짐
                    }
                    else if (idx == 5)
                    {
                        float L1 = Mathf.Sqrt(u * u + v * v);
                        float L2 = Mathf.Sqrt((u - 0.26f) * (u - 0.26f) + (v + 0.1f) * (v + 0.1f));
                        a = (1f - SS(0.55f, 0.63f, L1)) * SS(0.5f, 0.58f, L2);
                        shade = 0.75f + 0.25f * SS(0.3f, 0.68f, L1);
                        a = Mathf.Max(a, (1f - SS(0.7f, 1f, L1)) * SS(0.5f, 0.6f, L2) * 0.2f);
                    }
                    else if (idx == 6)
                    {
                        Vector3[] blobs = { new Vector3(-0.25f, 0.1f, 0.32f), new Vector3(0.22f, -0.15f, 0.26f), new Vector3(0.1f, 0.35f, 0.18f) };
                        foreach (Vector3 bl in blobs)
                        {
                            float L = Mathf.Sqrt((u - bl.x) * (u - bl.x) + (v - bl.y) * (v - bl.y));
                            a = Mathf.Max(a, 1f - SS(bl.z * 0.75f, bl.z, L));
                            a = Mathf.Max(a, (1f - SS(bl.z, bl.z * 1.6f, L)) * 0.2f);
                        }
                        shade = 0.9f;
                    }
                    else
                    {
                        float wv = 0.2f * Mathf.Pow(Mathf.Clamp01(1f - (v + 1f) * 0.5f), 0.7f) + 0.03f;
                        a = Mathf.Exp(-(u / wv) * (u / wv) * 2.5f) * (1f - SS(0.85f, 1f, Mathf.Abs(v)));
                        shade = 1f;
                    }

                    shade = Mathf.Clamp01(shade);
                    px[(cellY + py) * w + cellX + pxl] = new Color(shade, shade, shade, Mathf.Clamp01(a));
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
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
