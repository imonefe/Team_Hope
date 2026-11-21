using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-21 추가] 증강 연출 부품 모음 — 기획서 '연출 기획' 표의 공용 부품을 코드 한 줄로 부르게 한 곳.
/// 글자로 알리지 않고 이펙트 · 셰이더 · 화면 표식으로 보여 준다 (떠오르는 글자는 피해 숫자와 회복량만).
///
///  부품                    | 여기서 부르는 것
///  상태 띠 (적 머리 위)     | EnemyStatus 가 직접 그림 (아이콘 + 마름모 칸)
///  몸 색 (적 · 헤네시)       | BodyFlash — 적은 SpriteFlash 셰이더(Enemy.TintFlash), 헤네시는 잔상 셰이더 덧그림
///  대상 표식               | Mark — FX_BossSnipeMark 를 키워드 색으로 (반복 재생, 대상에 붙음)
///  바닥 범위               | Zone (상자, FX_WarningZone 색 바꿈) · Disc (둥근 범위)
///  잔상                    | Ghost — PauseAfterimage 셰이더로 헤네시 그림을 복사
///  구체 · 표시점            | Orb · Dot
///  연결선                  | Line · Streak (빛줄)
///  터짐                    | Burst — FX_RicoImpact 재질 복제본 색 · Sparks
///  원형 파동               | Ring
///  화면 가장자리 · 큰 발동   | ScreenPulse (퍼즈 글리치 화면 세기를 잠깐) · EdgeArrow
///  보유 키워드 단계 상승     | HudPop (오른쪽 아래 아이콘이 커졌다 돌아옴) + Ring
///
/// 이펙트 프리팹 연결은 AugmentOfferDirector 칸에서 가져옴 (씬 오브젝트). 비어 있으면 그 부품만 조용히 빠진다.
/// </summary>
public static class AugmentFx
{
    // ── 연결 ──
    public static GameObject ExplosionPrefab, SmokePrefab, MarkPrefab, ZonePrefab, ShatterPrefab, HealPrefab, StrikePrefab, SlashPrefab, StreakPrefab;

    static Sprite dotSprite, diamondSprite, triSprite;
    static Material spriteMat, ghostMat;
    static readonly int RingColorId = Shader.PropertyToID("_RingColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    public const int SortFront = 5140;

    /// <summary>연출 위치 오프셋 (AugmentOfferDirector 인스펙터 '연출 위치 조정'에서 바꿈)</summary>
    public static AugmentFxOffsets Offsets = new AugmentFxOffsets();

    /// <summary>몸 판정(콜라이더) 범위. 없으면 위치 기준 1×2 상자</summary>
    public static Bounds BodyBounds(Component c)
    {
        if (c == null) return new Bounds();
        Collider2D best = null; float area = 0f;
        foreach (var col in c.GetComponents<Collider2D>())
        {
            if (col == null || col.isTrigger) continue;
            float a = col.bounds.size.x * col.bounds.size.y;
            if (a > area) { area = a; best = col; }
        }
        if (best == null) best = c.GetComponentInChildren<Collider2D>();
        return best != null ? best.bounds : new Bounds(c.transform.position + Vector3.up, new Vector3(1f, 2f, 0f));
    }

    public static Color KwColor(AugmentKeyword k) => ShopColors.Hex(AugmentRules.KeywordHex(k));

    // ══════════════ 기본 재료 ══════════════

    public static Sprite Dot { get { if (dotSprite == null) dotSprite = MakeSprite(32, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) ; }); return dotSprite; } }
    public static Sprite Diamond { get { if (diamondSprite == null) diamondSprite = MakeSprite(32, (x, y) => { float d = Mathf.Abs(x) + Mathf.Abs(y); return d < 0.82f ? 1f : Mathf.Clamp01((1f - d) / 0.18f); }); return diamondSprite; } }
    public static Sprite Triangle { get { if (triSprite == null) triSprite = MakeSprite(32, (x, y) => { float u = (y + 1f) * 0.5f; return Mathf.Abs(x) < u * 0.9f && y < 0.95f ? 1f : 0f; }); return triSprite; } }

    static Sprite MakeSprite(int n, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                px[y * n + x] = new Color(1f, 1f, 1f, alpha(fx, fy));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    public static Material SpriteMat
    {
        get
        {
            if (spriteMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                spriteMat = sh != null ? new Material(sh) : null;
            }
            return spriteMat;
        }
    }

    static Material GhostMat
    {
        get { if (ghostMat == null) ghostMat = Resources.Load<Material>("PauseAfterimage"); return ghostMat; }
    }

    static FxRunner runner;
    static FxRunner Runner
    {
        get
        {
            if (runner == null)
            {
                var go = new GameObject("AugmentFx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<FxRunner>();
            }
            return runner;
        }
    }
    public static Coroutine Run(IEnumerator r) => Runner.StartCoroutine(r);

    static SpriteRenderer NewSprite(string name, Sprite s, Vector3 pos, Color c, float size, int order = SortFront)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = s; r.color = c; r.sortingOrder = order;
        if (SpriteMat != null) r.sharedMaterial = SpriteMat;
        return r;
    }

    static LineRenderer NewLine(string name, int points, float width, Color c, bool loop)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; lr.loop = loop; lr.positionCount = points;
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = c;
        lr.numCapVertices = 2;
        lr.sortingOrder = SortFront;
        if (SpriteMat != null) lr.sharedMaterial = SpriteMat;
        return lr;
    }

    static void SetCircle(LineRenderer lr, Vector3 center, float radius, float yScale = 1f)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * yScale, 0f));
        }
    }

    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ══════════════ 색 입히기 (프리팹 이펙트 공용) ══════════════

    static readonly string[] TintProps = { "_Color", "_BaseColor", "_TintColor", "_MainColor", "_tint", "_GlowColor", "_RingColor" };

    /// <summary>이펙트 프리팹의 재질 색(인스턴스 복제본)과 입자 색을 바꿈. intensity = HDR 밝기</summary>
    public static void Tint(GameObject fx, Color c, float intensity = 2f)
    {
        if (fx == null) return;
        foreach (var r in fx.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer sr) { sr.color = new Color(c.r, c.g, c.b, sr.color.a); continue; }
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (m == null) continue;
                foreach (var prop in TintProps)
                    if (m.HasProperty(prop)) { var old = m.GetColor(prop); m.SetColor(prop, new Color(c.r * intensity, c.g * intensity, c.b * intensity, old.a)); }
            }
        }
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, main.startColor.color.a));
        }
    }

    // ══════════════ 잔상 꼬리 (퍼즈 잔상 응용) ══════════════

    /// <summary>duration 동안 interval 마다 헤네시 그림을 잔상 셰이더로 찍고, 각각은 life 만에 빠르게 지움 → 이어붙은 대시 잔상</summary>
    public static void GhostTrail(SpriteRenderer src, Color c, float duration, float interval = 0.025f, float life = 0.2f, float alpha = 0.7f)
    {
        if (src != null) Run(GhostTrailRoutine(src, c, duration, interval, life, alpha));
    }

    static IEnumerator GhostTrailRoutine(SpriteRenderer src, Color c, float duration, float interval, float life, float alpha)
    {
        float end = Time.time + duration;
        while (Time.time < end && src != null)
        {
            Ghost(src, c, life, alpha);
            yield return new WaitForSeconds(interval);
        }
    }

    // ══════════════ 참격 자국 · 빛줄기 (프리팹 응용) ══════════════

    /// <summary>작은 참격 자국 (난무 참격 이펙트 축소 · 색 바꿈). freezeAfter 초 뒤 그 모습 그대로 멈춰 남음. 반환된 것을 Destroy 하면 사라짐</summary>
    public static GameObject SlashMark(Vector3 pos, Color c, float scale, float angle, float freezeAfter = 0.07f)
    {
        if (SlashPrefab == null) return Orb(pos, c, 0.3f);
        var fx = UnityEngine.Object.Instantiate(SlashPrefab, pos, Quaternion.Euler(0f, 0f, angle));
        fx.transform.localScale *= scale;
        Tint(fx, c, 2.2f);
        Run(FreezeLater(fx, freezeAfter));
        return fx;
    }

    static IEnumerator FreezeLater(GameObject fx, float t)
    {
        yield return new WaitForSeconds(t);
        if (fx == null) yield break;
        foreach (var an in fx.GetComponentsInChildren<Animator>(true)) an.speed = 0f;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true)) ps.Pause(true);
    }

    /// <summary>빛줄기(궁수 횡·종 일격 이펙트를 색 바꿈)가 a → b 로 달림</summary>
    public static void StreakFx(Vector3 a, Vector3 b, Color c, float speed = 40f, float scale = 1f)
    {
        if (StreakPrefab == null) { Streak(a, b, c, Vector3.Distance(a, b) / Mathf.Max(1f, speed), 0.3f); return; }
        Run(StreakFxRoutine(a, b, c, speed, scale));
    }

    static IEnumerator StreakFxRoutine(Vector3 a, Vector3 b, Color c, float speed, float scale)
    {
        Vector3 dir = (b - a).normalized;
        float len = Vector3.Distance(a, b);
        var fx = UnityEngine.Object.Instantiate(StreakPrefab, a, Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
        fx.transform.localScale *= scale;
        Tint(fx, c, 2.5f);
        float d = 0f;
        while (d < len && fx != null)
        {
            d = Mathf.Min(len, d + speed * Time.deltaTime);
            fx.transform.position = a + dir * d;
            yield return null;
        }
        if (fx != null) UnityEngine.Object.Destroy(fx, 0.08f);
    }

    // ══════════════ 원형 파동 ══════════════

    /// <summary>파문: 밖으로 퍼질수록 선이 굵어지고 옅어짐 (타원은 yScale)</summary>
    public static void Ripple(Vector3 center, Color c, float radius, float time = 0.6f, float width = 0.04f, int rings = 1, float yScale = 1f)
    {
        for (int i = 0; i < rings; i++) Run(RippleRoutine(center, c, radius, time, width, i * 0.12f, yScale));
    }

    static IEnumerator RippleRoutine(Vector3 center, Color c, float radius, float time, float width, float delay, float yScale)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var lr = NewLine("FxRipple", 56, width, c, true);
        float t = 0f;
        while (t < time)
        {
            float k = t / time;
            float e = 1f - (1f - k) * (1f - k);
            SetCircle(lr, center, Mathf.Lerp(radius * 0.1f, radius, e), yScale);
            lr.startWidth = lr.endWidth = width * Mathf.Lerp(1f, 3.5f, e);
            lr.startColor = lr.endColor = A(c, Mathf.Lerp(0.9f, 0f, e));
            t += Time.deltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(lr.gameObject);
    }

    // ══════════════ 원형 파동 (기본) ══════════════

    /// <summary>원형 파동: 반경 0 → radius 로 퍼지며 사라짐. rings 개면 시간차로 여러 겹</summary>
    public static void Ring(Vector3 center, Color c, float radius, float time = 0.45f, float width = 0.09f, int rings = 1, float yScale = 1f)
    {
        for (int i = 0; i < rings; i++) Run(RingRoutine(center, c, radius, time, width, i * 0.09f, yScale));
    }

    static IEnumerator RingRoutine(Vector3 center, Color c, float radius, float time, float width, float delay, float yScale)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var lr = NewLine("FxRing", 48, width, c, true);
        float t = 0f;
        while (t < time)
        {
            float k = t / time;
            float e = 1f - (1f - k) * (1f - k);
            SetCircle(lr, center, Mathf.Lerp(radius * 0.15f, radius, e), yScale);
            lr.startWidth = lr.endWidth = width * (1f - k * 0.6f);
            lr.startColor = lr.endColor = A(c, 1f - k);
            t += Time.deltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(lr.gameObject);
    }

    /// <summary>계속 보이는 원 (결계 · 범위 테두리). 반환된 것으로 색 · 반경을 바꾸고, 다 쓰면 Destroy</summary>
    public static FxCircle Circle(Transform follow, float radius, Color c, float width = 0.07f)
    {
        var lr = NewLine("FxCircle", 56, width, c, true);
        var fc = lr.gameObject.AddComponent<FxCircle>();
        fc.Init(lr, follow, radius, c);
        return fc;
    }

    /// <summary>둥근 바닥 범위 (반투명 원판 + 테두리). duration 뒤 사라짐</summary>
    public static GameObject Disc(Vector3 center, float radius, Color c, float duration, float fillAlpha = 0.22f)
    {
        var r = NewSprite("FxDisc", Dot, center, A(c, fillAlpha), radius * 2.2f, SortFront - 20);
        var fc = Circle(r.transform, radius, A(c, 0.85f), 0.06f);
        fc.transform.SetParent(r.transform, true);
        Run(FadeOut(r, duration, fillAlpha));
        return r.gameObject;
    }

    static IEnumerator FadeOut(SpriteRenderer r, float duration, float alpha)
    {
        float t = 0f;
        float fadeIn = 0.12f, fadeOut = 0.25f;
        while (t < duration && r != null)
        {
            float a = alpha * Mathf.Clamp01(t / fadeIn) * (t > duration - fadeOut ? Mathf.Clamp01((duration - t) / fadeOut) : 1f);
            var c = r.color; c.a = a; r.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        if (r != null) UnityEngine.Object.Destroy(r.gameObject);
    }

    // ══════════════ 터짐 · 불티 ══════════════

    /// <summary>터짐: FX_RicoImpact 를 키워드 색 재질 복제본으로</summary>
    public static GameObject Burst(Vector3 pos, Color c, float scale = 1f, float life = 1.5f)
    {
        if (ExplosionPrefab == null) { Sparks(pos, c, 10, 4f * scale, 0.4f); Ring(pos, c, 1.2f * scale); return null; }
        var fx = UnityEngine.Object.Instantiate(ExplosionPrefab, pos, Quaternion.identity);
        fx.transform.localScale *= scale;
        foreach (var r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (r.material != null && r.material.HasProperty(ColorId)) r.material.SetColor(ColorId, c * 3f);
        if (life > 0f) UnityEngine.Object.Destroy(fx, life);
        return fx;
    }

    /// <summary>연기 (색만 바꿈)</summary>
    public static GameObject Smoke(Vector3 pos, Color c, float scale, float life)
    {
        if (SmokePrefab == null) return null;
        var fx = UnityEngine.Object.Instantiate(SmokePrefab, pos, Quaternion.identity);
        fx.transform.localScale *= scale;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, main.startColor.color.a));
        }
        if (life > 0f) UnityEngine.Object.Destroy(fx, life);
        return fx;
    }

    /// <summary>타격 섬광 (검·주먹 충격 이펙트). 없으면 터짐으로 대신</summary>
    public static void Strike(Vector3 pos, Color c, float scale = 1f, int facing = 1)
    {
        if (StrikePrefab == null) { Burst(pos, c, scale * 0.6f, 1.2f); return; }
        var fx = UnityEngine.Object.Instantiate(StrikePrefab, pos, Quaternion.identity);
        var s = fx.transform.localScale * scale; s.x = Mathf.Abs(s.x) * (facing < 0 ? -1 : 1);
        fx.transform.localScale = s;
        foreach (var r in fx.GetComponentsInChildren<Renderer>(true))
        {
            if (r.material == null) continue;
            if (r.material.HasProperty(ColorId)) r.material.SetColor(ColorId, c * 2f);
        }
        UnityEngine.Object.Destroy(fx, 1.5f);
    }

    /// <summary>깨짐 (방패 · 막힌 투사체)</summary>
    public static void Shatter(Vector3 pos, Color c, float scale = 0.6f)
    {
        if (ShatterPrefab == null) { Sparks(pos, c, 12, 5f, 0.35f); return; }
        var fx = UnityEngine.Object.Instantiate(ShatterPrefab, pos, Quaternion.identity);
        fx.transform.localScale *= scale;
        UnityEngine.Object.Destroy(fx, 1.5f);
    }

    /// <summary>회복 이펙트 (색만 바꿈)</summary>
    public static void HealFx(Transform at, Color c)
    {
        if (HealPrefab == null || at == null) { if (at != null) Sparks(at.position + Vector3.up, c, 8, 1.5f, 0.6f, -1.5f); return; }
        var fx = UnityEngine.Object.Instantiate(HealPrefab, at.position, Quaternion.identity, at);
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, main.startColor.color.a));
        }
        UnityEngine.Object.Destroy(fx, 2f);
    }

    /// <summary>작은 불티 · 입자: 사방으로 튀었다가 사라짐 (gravity 음수면 위로 떠오름)</summary>
    public static void Sparks(Vector3 pos, Color c, int count, float speed, float life, float gravity = 6f, float size = 0.16f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 v = UnityEngine.Random.insideUnitCircle.normalized * speed * UnityEngine.Random.Range(0.4f, 1f);
            Run(SparkRoutine(pos, v, c, life * UnityEngine.Random.Range(0.7f, 1.2f), gravity, size * UnityEngine.Random.Range(0.7f, 1.3f)));
        }
    }

    static IEnumerator SparkRoutine(Vector3 pos, Vector2 v, Color c, float life, float gravity, float size)
    {
        var r = NewSprite("FxSpark", Diamond, pos, c, size);
        float t = 0f;
        while (t < life)
        {
            float k = t / life;
            v.y -= gravity * Time.deltaTime;
            r.transform.position += (Vector3)(v * Time.deltaTime);
            r.color = A(c, 1f - k);
            r.transform.localScale = Vector3.one * size * (1f - k * 0.5f);
            t += Time.deltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(r.gameObject);
    }

    // ══════════════ 날아가는 것 · 연결선 ══════════════

    /// <summary>점(결정)이 from 에서 target 으로 날아감 (휘어지며). 도착하면 onArrive</summary>
    public static void Fly(Vector3 from, Transform target, Color c, int count = 1, float time = 0.45f, Action onArrive = null, float size = 0.22f, bool diamond = true)
    {
        for (int i = 0; i < count; i++)
            Run(FlyRoutine(from, target, c, time + i * 0.07f, i == 0 ? onArrive : null, size, diamond));
    }

    static IEnumerator FlyRoutine(Vector3 from, Transform target, Color c, float time, Action onArrive, float size, bool diamond)
    {
        var r = NewSprite("FxFly", diamond ? Diamond : Dot, from, c, size);
        float arcH = UnityEngine.Random.Range(1.4f, 2.4f);
        float t = 0f;
        Vector3 lastTo = target != null ? target.position + Vector3.up : from;
        while (t < time)
        {
            if (target != null) lastTo = target.position + Vector3.up;
            float k = t / time;
            float e = k * k;
            // 곡선: 시작 · 끝 사이 위쪽 조절점을 지나는 2차 베지어
            Vector3 ctrl = (from + lastTo) * 0.5f + Vector3.up * (arcH + Vector3.Distance(from, lastTo) * 0.15f);
            float u = 1f - e;
            Vector3 p = u * u * from + 2f * u * e * ctrl + e * e * lastTo;
            if (r == null) yield break;
            r.transform.position = p;
            r.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        if (r != null) UnityEngine.Object.Destroy(r.gameObject);
        Sparks(lastTo, c, 4, 1.5f, 0.25f, 0f, 0.1f);
        onArrive?.Invoke();
    }

    /// <summary>두 점을 잇는 선이 번쩍였다 사라짐</summary>
    public static void Line(Vector3 a, Vector3 b, Color c, float time = 0.25f, float width = 0.08f)
    {
        Run(LineRoutine(a, b, null, null, c, time, width));
    }

    /// <summary>두 오브젝트를 잇는 선 (따라 움직임)</summary>
    public static void Link(Transform a, Transform b, Color c, float time = 0.3f, float width = 0.07f)
    {
        if (a == null || b == null) return;
        Run(LineRoutine(a.position, b.position, a, b, c, time, width));
    }

    static IEnumerator LineRoutine(Vector3 a, Vector3 b, Transform ta, Transform tb, Color c, float time, float width)
    {
        var lr = NewLine("FxLine", 2, width, c, false);
        float t = 0f;
        while (t < time)
        {
            float k = t / time;
            if (ta != null) a = ta.position + Vector3.up * 0.8f;
            if (tb != null) b = tb.position + Vector3.up * 0.8f;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startColor = lr.endColor = A(c, 1f - k);
            lr.startWidth = lr.endWidth = width * (1f + 0.6f * (1f - k));
            t += Time.deltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(lr.gameObject);
    }

    /// <summary>빛줄: 머리(밝은 점)가 a → b 로 달리고 꼬리가 남았다 사라짐 (돌진 · 관통 · 참격)</summary>
    public static void Streak(Vector3 a, Vector3 b, Color c, float time = 0.18f, float width = 0.18f)
    {
        Run(StreakRoutine(a, b, c, time, width));
    }

    static IEnumerator StreakRoutine(Vector3 a, Vector3 b, Color c, float time, float width)
    {
        var lr = NewLine("FxStreak", 2, width, c, false);
        lr.startColor = A(c, 0f); lr.endColor = c;
        float t = 0f;
        while (t < time)
        {
            float k = t / time;
            lr.SetPosition(0, a); lr.SetPosition(1, Vector3.Lerp(a, b, 1f - (1f - k) * (1f - k)));
            t += Time.deltaTime;
            yield return null;
        }
        float f = 0f;
        while (f < 0.2f)
        {
            float k = f / 0.2f;
            lr.SetPosition(0, Vector3.Lerp(a, b, k)); lr.SetPosition(1, b);
            lr.endColor = A(c, 1f - k);
            lr.startWidth = lr.endWidth = width * (1f - k);
            f += Time.deltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(lr.gameObject);
    }

    // ══════════════ 구체 · 점 ══════════════

    /// <summary>제자리에 떠 있는 구체 (밝은 점 + 둘레 원 + 반짝임). 반환된 것을 Destroy 하면 사라짐</summary>
    public static GameObject Orb(Vector3 pos, Color c, float size = 0.5f)
    {
        var core = NewSprite("FxOrb", Dot, pos, A(c, 0.95f), size);
        var glow = NewSprite("Glow", Dot, pos, A(c, 0.35f), size * 2.4f, SortFront - 1);
        glow.transform.SetParent(core.transform, true);
        var ring = Circle(core.transform, size * 0.75f, A(c, 0.8f), 0.04f);
        ring.transform.SetParent(core.transform, true);
        core.gameObject.AddComponent<FxPulse>().Init(size, 0.12f, 5f);
        return core.gameObject;
    }

    // ══════════════ 대상 표식 · 바닥 범위 ══════════════

    /// <summary>대상 표식 (FX_BossSnipeMark 를 키워드 색으로, 대상에 붙어 반복). 반환된 것을 Destroy 하면 사라짐</summary>
    public static GameObject Mark(Transform target, Color c, float scale = 1f, bool loop = true, float yOffset = 0.9f)
    {
        if (target == null) return null;
        GameObject fx;
        if (MarkPrefab != null)
        {
            fx = UnityEngine.Object.Instantiate(MarkPrefab, target.position + Vector3.up * yOffset, Quaternion.identity);
            fx.transform.localScale *= scale;
            foreach (var r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r.material == null) continue;
                if (r.material.HasProperty(RingColorId)) r.material.SetColor(RingColorId, c * 1.6f);
                if (r.material.HasProperty(ColorId)) r.material.SetColor(ColorId, c * 1.6f);
            }
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.loop = loop;
                var col = main.startColor.color; main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, col.a));
            }
        }
        else
        {
            fx = Circle(null, 0.7f * scale, c, 0.05f).gameObject;
            fx.transform.position = target.position + Vector3.up * yOffset;
        }
        var f = fx.AddComponent<FxFollow>();
        f.target = target; f.offset = Vector3.up * yOffset;
        if (!loop) UnityEngine.Object.Destroy(fx, 1.2f);
        return fx;
    }

    /// <summary>상자 모양 바닥 범위 (FX_WarningZone 색 바꿈). duration 0 = Hide 부를 때까지</summary>
    public static WarningZone Zone(Vector3 center, Vector2 size, Color c, float duration)
    {
        if (ZonePrefab == null) { Disc(center, Mathf.Max(size.x, size.y) * 0.5f, c, duration > 0 ? duration : 1f); return null; }
        var wz = WarningZone.Spawn(ZonePrefab, center, size, duration);
        if (wz == null) return null;
        var rend = wz.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_FillColor", A(c, 0.2f));
            mpb.SetColor("_BandColor", A(c * 0.6f, 0.3f));
            mpb.SetColor("_TextColor", A(c * 2f, 0.55f));
            rend.SetPropertyBlock(mpb);
        }
        wz.blinkDepth = 0.15f;
        return wz;
    }

    // ══════════════ 잔상 · 몸 색 ══════════════

    /// <summary>헤네시(또는 대상) 그림을 잔상 셰이더로 복사. life 동안 있다가 깨지며 사라짐. follow 면 대상을 따라감</summary>
    public static FxGhost Ghost(SpriteRenderer src, Color c, float life, float alpha = 0.75f, bool follow = false, Vector3? at = null)
    {
        if (src == null) return null;
        var go = new GameObject("FxGhost");
        var r = go.AddComponent<SpriteRenderer>();
        var m = GhostMat;
        if (m != null) r.sharedMaterial = m; else if (SpriteMat != null) r.sharedMaterial = SpriteMat;
        var t = src.transform;
        go.transform.SetPositionAndRotation(at ?? t.position, t.rotation);
        go.transform.localScale = t.lossyScale;
        r.sprite = src.sprite; r.flipX = src.flipX; r.flipY = src.flipY;
        r.sortingLayerID = src.sortingLayerID; r.sortingOrder = src.sortingOrder + (follow ? 1 : -1);
        var g = go.AddComponent<FxGhost>();
        g.Init(r, follow ? src : null, c, life, alpha);
        return g;
    }

    internal static void ApplyGhost(SpriteRenderer r, MaterialPropertyBlock b, Color c, float dissolve, float seed)
    {
        r.GetPropertyBlock(b);
        if (r.sprite != null) b.SetTexture(MainTexId, r.sprite.texture);
        b.SetColor(ColorId, c); b.SetColor(RimColorId, Color.Lerp(c, Color.white, 0.6f)); b.SetColor(EdgeColorId, Color.Lerp(c, Color.white, 0.4f));
        b.SetFloat(DissolveId, dissolve); b.SetFloat(SeedId, seed);
        r.SetPropertyBlock(b);
    }

    /// <summary>몸이 색으로 번쩍 (적: SpriteFlash 셰이더 / 헤네시: 잔상 셰이더 덧그림)</summary>
    public static void BodyFlash(Component who, Color c, float amount = 0.7f, float time = 0.25f)
    {
        if (who == null) return;
        var e = who.GetComponent<Enemy>();
        if (e != null) { e.TintFlash(c, amount, time); return; }
        var sr = PlayerSprite(who);
        if (sr != null) Ghost(sr, c, time, amount * 0.8f, true);
    }

    public static SpriteRenderer PlayerSprite(Component who)
    {
        if (who == null) return null;
        var srs = who.GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer best = null; float area = 0f;
        foreach (var s in srs)
        {
            if (s.sprite == null || s.name.StartsWith("Fx") || s.name.StartsWith("Aug")) continue;
            float a = s.bounds.size.x * s.bounds.size.y;
            if (a > area) { area = a; best = s; }
        }
        return best;
    }

    // ══════════════ 기절 별 ══════════════

    public static void StunStars(Transform target, float time, Color c)
    {
        if (target != null) Run(StunRoutine(target, time, c));
    }

    static IEnumerator StunRoutine(Transform target, float time, Color c)
    {
        var col = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
        var stars = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++) stars[i] = NewSprite("FxStunStar", Diamond, target.position, c, 0.2f);
        float t = 0f;
        while (t < time && target != null)
        {
            float top = col != null ? col.bounds.max.y + 0.25f : target.position.y + 1.6f;
            for (int i = 0; i < 3; i++)
            {
                float a = t * 7f + i * Mathf.PI * 2f / 3f;
                stars[i].transform.position = new Vector3(target.position.x + Mathf.Cos(a) * 0.45f, top + Mathf.Sin(a) * 0.12f, target.position.z - 0.5f);
                stars[i].transform.Rotate(0, 0, 360f * Time.deltaTime);
            }
            t += Time.deltaTime;
            yield return null;
        }
        foreach (var s in stars) if (s != null) UnityEngine.Object.Destroy(s.gameObject);
    }

    // ══════════════ 화면 ══════════════

    static float pulseUntil, pulseAmount;

    /// <summary>화면 글리치 잠깐 (큰 발동). 장면 전환 중이면 건너뜀</summary>
    public static void ScreenPulse(float amount = 0.35f, float time = 0.18f)
    {
        if (TransitionCanvas.GlitchBusy) return;
        pulseAmount = Mathf.Max(pulseAmount, amount);
        pulseUntil = Mathf.Max(pulseUntil, Time.unscaledTime + time);
        Runner.pulsing = true;
    }

    internal static void TickPulse()
    {
        if (TransitionCanvas.GlitchBusy) { pulseAmount = 0f; return; }
        if (Time.unscaledTime < pulseUntil)
        {
            float left = pulseUntil - Time.unscaledTime;
            PauseGlitchEffect.TransitionNoise = pulseAmount * Mathf.Clamp01(left / 0.15f);
        }
        else if (pulseAmount > 0f)
        {
            PauseGlitchEffect.TransitionNoise = 0f;
            pulseAmount = 0f;
            Runner.pulsing = false;
        }
    }

    static Canvas hud;
    static Canvas Hud
    {
        get
        {
            if (hud == null)
            {
                var go = new GameObject("AugmentFxHud");
                UnityEngine.Object.DontDestroyOnLoad(go);
                hud = go.AddComponent<Canvas>();
                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.sortingOrder = 300;
                var sc = go.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);
                sc.matchWidthOrHeight = 0.5f;
            }
            return hud;
        }
    }

    /// <summary>보유 키워드 단계 상승: 오른쪽 아래 키워드 아이콘이 커졌다 돌아오며 빛남 (글자 없음)</summary>
    public static void HudPop(Sprite icon, Color c, int tier)
    {
        if (icon == null) return;
        Run(HudPopRoutine(icon, c, tier));
    }

    static IEnumerator HudPopRoutine(Sprite icon, Color c, int tier)
    {
        var go = new GameObject("FxHudPop", typeof(RectTransform));
        go.transform.SetParent(Hud.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-120f, 150f);
        rt.sizeDelta = new Vector2(96f, 96f);
        var glow = new GameObject("Glow", typeof(RectTransform)).AddComponent<Image>();
        glow.transform.SetParent(go.transform, false);
        ((RectTransform)glow.transform).sizeDelta = new Vector2(190f, 190f);
        glow.sprite = Dot; glow.color = A(c, 0.6f); glow.raycastTarget = false;
        var img = go.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
        // 단계 마름모 (3개 = 1칸, 5개 = 2칸)
        for (int i = 0; i < tier; i++)
        {
            var pip = new GameObject("Pip", typeof(RectTransform)).AddComponent<Image>();
            pip.transform.SetParent(go.transform, false);
            var prt = (RectTransform)pip.transform;
            prt.sizeDelta = new Vector2(22f, 22f);
            prt.anchoredPosition = new Vector2((i - (tier - 1) * 0.5f) * 28f, -64f);
            pip.sprite = Diamond; pip.color = c; pip.raycastTarget = false;
        }
        float t = 0f, total = 1.4f;
        while (t < total)
        {
            float k = t / total;
            float s = k < 0.2f ? Mathf.Lerp(0.4f, 1.35f, k / 0.2f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((k - 0.2f) / 0.3f));
            rt.localScale = Vector3.one * s;
            float a = k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
            img.color = new Color(1f, 1f, 1f, a);
            glow.color = A(c, 0.6f * a * (1f - k * 0.5f));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        UnityEngine.Object.Destroy(go);
    }

    /// <summary>화면 밖 대상을 가리키는 가장자리 화살표. 반환된 것을 Destroy 하면 사라짐</summary>
    public static FxEdgeArrow EdgeArrow(Transform target, Color c)
    {
        var go = new GameObject("FxEdgeArrow", typeof(RectTransform));
        go.transform.SetParent(Hud.transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = Triangle; img.color = c; img.raycastTarget = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(40f, 40f);
        var a = go.AddComponent<FxEdgeArrow>();
        a.target = target; a.img = img;
        return a;
    }
}

// ══════════════ 연출 위치 오프셋 ══════════════

/// <summary>[2026-11-21] 증강 연출이 붙는 자리 = 몸 판정(콜라이더) 기준 + 이 값 (월드 칸). 인스펙터에서 조절</summary>
[Serializable]
public class AugmentFxOffsets
{
    [Tooltip("헤네시 몸 가운데 (잔상 판정 · 몸 주변 연출 · 파동)")] public Vector2 playerCenter = Vector2.zero;
    [Tooltip("헤네시 발밑 (연격 반원 게이지 · 발밑 파동 · 결계)")] public Vector2 playerFeet = new Vector2(0f, 0.05f);
    [Tooltip("헤네시 머리 위 (탈취 데이터 결정이 도는 높이)")] public Vector2 playerHead = new Vector2(0f, 0.35f);
    [Tooltip("적 몸 가운데 (맞은 자리 섬광 · 날아가는 불티의 도착점)")] public Vector2 enemyCenter = Vector2.zero;
    [Tooltip("적 머리 위 상태 띠 (머리 끝 기준)")] public Vector2 enemyBadge = new Vector2(0f, 0.1f);
    [Tooltip("대시 출발 · 끝 연출 (발밑 기준)")] public Vector2 dash = new Vector2(0f, 0.4f);
}

// ══════════════ 부품이 쓰는 작은 컴포넌트 ══════════════

public class FxRunner : MonoBehaviour
{
    [NonSerialized] public bool pulsing;
    void Update() { if (pulsing) AugmentFx.TickPulse(); }
}

public class FxFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + offset;
    }
}

public class FxPulse : MonoBehaviour
{
    float size, amp, speed, seed;
    public void Init(float size, float amp, float speed) { this.size = size; this.amp = amp; this.speed = speed; seed = UnityEngine.Random.value * 10f; }
    void Update() { transform.localScale = Vector3.one * size * (1f + amp * Mathf.Sin((Time.time + seed) * speed)); }
}

public class FxCircle : MonoBehaviour
{
    LineRenderer lr; Transform follow; public float radius; public Color color; public float yScale = 1f;
    public Vector3 offset = new Vector3(0f, 0.1f, 0f);
    public void Init(LineRenderer lr, Transform follow, float radius, Color c) { this.lr = lr; this.follow = follow; this.radius = radius; color = c; offset = follow != null && follow.GetComponent<SpriteRenderer>() == null ? new Vector3(0f, 0.1f, 0f) : Vector3.zero; Apply(); }
    public void Show(bool on) { if (lr != null) lr.enabled = on; }
    void LateUpdate() => Apply();
    void Apply()
    {
        if (lr == null) return;
        Vector3 c = follow != null ? follow.position + offset : transform.position;
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * yScale, 0f));
        }
        lr.startColor = lr.endColor = color;
    }
}

public class FxGhost : MonoBehaviour
{
    SpriteRenderer r, follow; Color color; float life, alpha, t, seed; MaterialPropertyBlock b;
    public bool hold;   // true 면 Release 부를 때까지 남음
    public void Init(SpriteRenderer r, SpriteRenderer follow, Color c, float life, float alpha)
    {
        this.r = r; this.follow = follow; color = c; this.life = life; this.alpha = alpha; seed = UnityEngine.Random.value * 100f;
        b = new MaterialPropertyBlock();
        AugmentFx.ApplyGhost(r, b, new Color(c.r, c.g, c.b, alpha), 0f, seed);
    }
    public void Release() { hold = false; if (t < life - 0.3f) t = life - 0.3f; }
    public void SetColor(Color c) { color = c; }
    public SpriteRenderer Renderer => r;
    void LateUpdate()
    {
        if (follow != null)
        {
            transform.SetPositionAndRotation(follow.transform.position, follow.transform.rotation);
            transform.localScale = follow.transform.lossyScale;
            r.sprite = follow.sprite; r.flipX = follow.flipX;
        }
        if (!hold) t += Time.deltaTime;
        float shatter = Mathf.Clamp01((t - (life - 0.3f)) / 0.3f);
        float a = alpha * (follow != null ? 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, life)) : 1f);
        AugmentFx.ApplyGhost(r, b, new Color(color.r, color.g, color.b, a), follow != null ? 0f : shatter, seed);
        r.color = new Color(1f, 1f, 1f, a);
        if (t >= life) Destroy(gameObject);
    }
}

public class FxEdgeArrow : MonoBehaviour
{
    public Transform target; public Image img;
    void LateUpdate()
    {
        var cam = Camera.main;
        if (target == null) { Destroy(gameObject); return; }
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(target.position + Vector3.up);
        bool inside = vp.z > 0 && vp.x > 0.03f && vp.x < 0.97f && vp.y > 0.03f && vp.y < 0.97f;
        img.enabled = !inside;
        if (inside) return;
        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0) d = -d;
        float m = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) / 0.46f;
        Vector2 p = d / Mathf.Max(0.0001f, m) + new Vector2(0.5f, 0.5f);
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = p;
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 8f);
        rt.localScale = Vector3.one * pulse;
    }
}
