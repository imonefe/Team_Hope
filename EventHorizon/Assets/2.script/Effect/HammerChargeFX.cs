using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-10-28 추가] 망치 차지 연출 — 차지가 무장되는 순간부터 점과 선이 캐릭터를 향해 빨려 들어온다.
/// 차지 시간이 쌓일수록 3단계로 나뉘어 점점 빠르고, 촘촘하고, 밝아진다.
///
/// 사용법: HammerChargeFX.Create(플레이어 Transform, 최대 차지 시간)
///  - PlayerController.SpawnChargeEffect() 에서 망치 무기에 차지 프리팹이 없을 때 대신 부른다.
///  - 돌려받은 GameObject 를 Destroy 하면 끝 (PlayerController.RemoveChargeEffect 가 강타 순간 지운다).
///  - 그림 소스는 쓰지 않는다 — 코드로 만든 둥근 점과 가는 선을 금색으로 물들인다.
///  - 모양·속도·색은 아래 상수에서 조절.
/// </summary>
public class HammerChargeFX : MonoBehaviour
{
    // ── 단계 (진행도 = 무장 후 경과 시간 / 최대 차지 시간) ──────────
    const float Stage2At = 1f / 3f;           // 진행도가 이 값을 넘으면 2단계
    const float Stage3At = 2f / 3f;           // 진행도가 이 값을 넘으면 3단계

    // 단계별 값 (인덱스 0=1단계, 1=2단계, 2=3단계)
    static readonly float[] SpawnInterval = { 0.07f, 0.04f, 0.02f };   // 입자 하나 생성 간격(초)
    static readonly float[] StartSpeed = { 2.5f, 4.5f, 7.5f };         // 처음 속도(유닛/초)
    static readonly float[] Accel = { 6f, 12f, 22f };                  // 가속도(유닛/초²)
    // [2026-10-28] 색은 차지 진행도에 따라 금색 → 주황 → 붉은색으로 점점 변한다 (생성 순간의 색을 가짐)
    static readonly Color ColorStart = new Color(0.851f, 0.682f, 0.353f, 0.80f);  // 금색 #D9AE5A
    static readonly Color ColorMid = new Color(0.937f, 0.459f, 0.200f, 0.92f);    // 주황 #EF7533
    static readonly Color ColorEnd = new Color(0.937f, 0.176f, 0.137f, 1.00f);    // 붉은색 #EF2D23

    // ── 차징 중 화면 흔들림 [2026-10-28] ───────────────
    // 차지하는 동안 짧은 흔들림을 계속 이어 붙여 은은하게 떨리게 한다. 단계마다 세진다.
    static readonly float[] ChargeShake = { 0.02f, 0.04f, 0.07f };   // 단계별 세기
    const float ChargeShakeInterval = 0.1f;   // 이 간격(초)마다 흔들림을 새로 건다
    const float ChargeShakeLength = 0.13f;    // 한 번 거는 흔들림의 길이 (간격보다 살짝 길게 해서 끊김 없이)

    // ── 모양 ─────────────────────────────────────────
    const float SpawnRadiusMin = 1.8f, SpawnRadiusMax = 2.6f;   // 캐릭터 중심에서 생성되는 거리
    const float VanishRadius = 0.08f;         // 이 거리 안으로 들어오면 사라짐 (작을수록 캐릭터 한 점에 정확히 모임)
    const float FadeNearCenter = 0.35f;       // 중심에서 이 거리부터 옅어지기 시작
    // 중심 빛: 입자가 모이는 한 점에 작은 빛이 맺혀 초점이 되고, 단계마다 커진다
    static readonly bool ShowCore = false;              // [2026-10-28] 원이 보이지 않게 꺼 둠 (true로 바꾸면 다시 켜짐)
    static readonly float[] CoreSize = { 0.22f, 0.32f, 0.44f };
    const float CorePulse = 0.15f;            // 맥박 크기 비율
    const float CorePulseSpeed = 10f;
    const float LineRatio = 0.6f;             // 선이 나올 확률 (나머지는 점)
    const float LineWidth = 0.035f;
    const float LineBaseLength = 0.18f;       // 선 기본 길이
    const float LineLengthPerSpeed = 0.045f;  // 속도 1당 늘어나는 길이
    const float DotSizeMin = 0.06f, DotSizeMax = 0.11f;
    const float FadeInTime = 0.08f;
    const int LineTexW = 64, LineTexH = 8;
    const float LineAspect = (float)LineTexW / LineTexH;   // 선 그림이 1×(H/W) 유닛이라 굵기를 맞추는 배율
    const int BurstCount = 14;                // 단계가 오를 때 한 번에 생성하는 개수
    const int MaxParticles = 90;
    const int SortingOrder = 5100;            // 플레이어(4000~5000)보다 위
    const float TowardCamera = 1.5f;          // 다른 효과들과 같은 이유로 카메라 쪽으로 당김
    static readonly Vector3 CenterOffset = Vector3.zero;   // 모이는 중심 미세 조정(몸 판정 중심 기준)

    // ── 내부 ─────────────────────────────────────────
    class P
    {
        public SpriteRenderer sr;
        public Vector2 pos;
        public float speed, accel, age, size;
        public bool isLine, alive;
        public Color color;
    }

    static Material mat;
    static Sprite dotSprite, lineSprite;

    Transform target;
    SpriteRenderer targetRenderer;
    Collider2D targetBody;      // 몸 판정 — 무기 그림까지 포함하는 그림 범위보다 정확한 몸 중심
    SpriteRenderer core;
    float progress;
    float maxChargeTime;
    float elapsed, spawnTimer, shakeTimer;
    int stage = -1;
    readonly List<P> pool = new List<P>();

    public static GameObject Create(Transform player, float maxChargeTime)
    {
        EnsureAssets();
        var go = new GameObject("HammerChargeFX");
        var fx = go.AddComponent<HammerChargeFX>();
        fx.target = player;
        fx.targetRenderer = player.GetComponentInChildren<SpriteRenderer>();
        foreach (var col in player.GetComponents<Collider2D>())
            if (!col.isTrigger) { fx.targetBody = col; break; }
        if (fx.targetBody == null) fx.targetBody = player.GetComponent<Collider2D>();
        fx.maxChargeTime = Mathf.Max(0.1f, maxChargeTime);
        return go;
    }

    Vector2 Center()
    {
        Vector3 c = targetBody != null ? targetBody.bounds.center
                  : targetRenderer != null ? targetRenderer.bounds.center
                  : (target != null ? target.position : transform.position);
        return c + CenterOffset;
    }

    float Depth()
    {
        float z = targetRenderer != null ? targetRenderer.transform.position.z
                : (target != null ? target.position.z : 0f);
        return z - TowardCamera;
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;   // 차지 시간(PlayerController)과 같은 시계를 쓴다
        elapsed += dt;
        progress = Mathf.Clamp01(elapsed / maxChargeTime);
        int s = progress < Stage2At ? 0 : (progress < Stage3At ? 1 : 2);

        Vector2 center = Center();

        // 단계가 오르는 순간: 한 번에 여러 개 생성
        // [2026-10-28] 같은 거리·고른 각도로 뿌리면 줄어드는 원으로 보여서, 각도·거리를 흩어 둔다
        if (s != stage)
        {
            stage = s;
            for (int i = 0; i < BurstCount; i++)
                Spawn(center, Random.Range(0f, Mathf.PI * 2f),
                      Random.Range(SpawnRadiusMin, SpawnRadiusMax), Random.value < LineRatio);
        }

        // 차징 중 은은한 화면 흔들림 (단계마다 세짐)
        shakeTimer += dt;
        if (shakeTimer >= ChargeShakeInterval)
        {
            shakeTimer = 0f;
            CameraFollow.Shake(ChargeShake[stage], ChargeShakeLength);
        }

        // 꾸준히 생성
        spawnTimer += dt;
        float interval = SpawnInterval[stage];
        while (spawnTimer >= interval)
        {
            spawnTimer -= interval;
            Spawn(center, Random.Range(0f, Mathf.PI * 2f),
                  Random.Range(SpawnRadiusMin, SpawnRadiusMax), Random.value < LineRatio);
        }

        // 이동
        float z = Depth();
        if (ShowCore) UpdateCore(center, z);
        for (int i = 0; i < pool.Count; i++)
        {
            P p = pool[i];
            if (!p.alive) continue;

            p.age += dt;
            p.speed += p.accel * dt;

            Vector2 toC = center - p.pos;
            float dist = toC.magnitude;
            float step = p.speed * dt;
            if (dist <= VanishRadius || step >= dist - VanishRadius * 0.5f)
            {
                Kill(p);
                continue;
            }
            Vector2 dir = toC / dist;
            p.pos += dir * step;

            float a = Mathf.Clamp01(p.age / FadeInTime);
            // 중심에 가까워질수록 살짝 옅어지며 빨려 들어가는 느낌
            a *= Mathf.Clamp01((dist - VanishRadius) / FadeNearCenter);
            Color c = p.color; c.a *= a;
            p.sr.color = c;

            Transform t = p.sr.transform;
            t.position = new Vector3(p.pos.x, p.pos.y, z);
            if (p.isLine)
            {
                float len = LineBaseLength + p.speed * LineLengthPerSpeed;
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(len, LineWidth * LineAspect, 1f);
            }
        }
    }

    void Spawn(Vector2 center, float angle, float radius, bool isLine)
    {
        P p = null;
        for (int i = 0; i < pool.Count; i++)
            if (!pool[i].alive) { p = pool[i]; break; }
        if (p == null)
        {
            if (pool.Count >= MaxParticles) return;
            var go = new GameObject("p");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = SortingOrder;
            if (targetRenderer != null) sr.sortingLayerID = targetRenderer.sortingLayerID;
            p = new P { sr = sr };
            pool.Add(p);
        }

        p.alive = true;
        p.isLine = isLine;
        p.age = 0f;
        p.pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        p.speed = StartSpeed[stage] * Random.Range(0.85f, 1.15f);
        p.accel = Accel[stage];
        p.color = ColorAt(progress);
        p.sr.sprite = isLine ? lineSprite : dotSprite;
        p.sr.color = new Color(p.color.r, p.color.g, p.color.b, 0f);
        p.sr.gameObject.SetActive(true);

        Transform t = p.sr.transform;
        t.position = new Vector3(p.pos.x, p.pos.y, Depth());
        if (!isLine)
        {
            float s = Random.Range(DotSizeMin, DotSizeMax);
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(s, s, 1f);
        }
    }

    static Color ColorAt(float t)
    {
        return t < 0.5f ? Color.Lerp(ColorStart, ColorMid, t * 2f)
                        : Color.Lerp(ColorMid, ColorEnd, (t - 0.5f) * 2f);
    }

    void UpdateCore(Vector2 center, float z)
    {
        if (core == null)
        {
            var go = new GameObject("core");
            go.transform.SetParent(transform, false);
            core = go.AddComponent<SpriteRenderer>();
            core.sharedMaterial = mat;
            core.sprite = dotSprite;
            core.sortingOrder = SortingOrder + 1;
            if (targetRenderer != null) core.sortingLayerID = targetRenderer.sortingLayerID;
        }
        float fadeIn = Mathf.Clamp01(elapsed / 0.15f);
        float size = CoreSize[stage] * (1f + Mathf.Sin(elapsed * CorePulseSpeed * (1f + stage * 0.5f)) * CorePulse) * fadeIn;
        core.transform.position = new Vector3(center.x, center.y, z);
        core.transform.localScale = new Vector3(size, size, 1f);
        Color c = ColorAt(progress);
        c.a *= 0.85f * fadeIn;
        core.color = c;
    }

    void Kill(P p)
    {
        p.alive = false;
        p.sr.gameObject.SetActive(false);
    }

    // ── 코드로 만드는 재료 (그림 소스 없음) ─────────────
    static void EnsureAssets()
    {
        if (mat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            mat = new Material(sh) { name = "HammerChargeFX", hideFlags = HideFlags.DontSave };
        }
        if (dotSprite == null)
        {
            // 부드러운 원 (지름 1유닛)
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) / 0.35f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            dotSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect);
            dotSprite.hideFlags = HideFlags.DontSave;
        }
        if (lineSprite == null)
        {
            // 가로로 긴 선 (1×1유닛, 앞쪽=중심 쪽은 진하고 꼬리는 흐려짐, 위아래 가장자리 부드럽게)
            var tex = new Texture2D(LineTexW, LineTexH, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < LineTexH; y++)
                for (int x = 0; x < LineTexW; x++)
                {
                    float u = (x + 0.5f) / LineTexW;                       // 0=꼬리, 1=머리(중심 쪽)
                    float v = Mathf.Abs((y + 0.5f) / LineTexH * 2f - 1f);
                    float a = Mathf.Pow(u, 1.6f) * Mathf.Clamp01((1f - v) / 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            // 피벗을 머리 쪽에 두어, 선이 중심을 향해 꼬리를 끌며 날아가도록
            lineSprite = Sprite.Create(tex, new Rect(0, 0, LineTexW, LineTexH), new Vector2(1f, 0.5f), LineTexW, 0, SpriteMeshType.FullRect);
            lineSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
