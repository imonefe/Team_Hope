using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검 휘두르기 참격 이펙트 (sword_attack_sprite 전용으로 기본값 세팅됨).
/// 캐릭터 스프라이트가 특정 프레임으로 바뀌는 순간을 감지해서 자동 재생하므로
/// PlayerController 나 애니메이션 클립을 수정할 필요가 없습니다.
///
/// 사용법: 플레이어(또는 스프라이트가 붙은 자식 오브젝트)에 Add Component → SwordSlashFX
///  - 머티리얼 4개는 자동으로 채워집니다 (Assets/shader/SwordSlash/).
///  - Scene 뷰에서 이 컴포넌트를 선택하면 각 참격의 모양이 선으로 보입니다.
/// </summary>
[DisallowMultipleComponent]
public class SwordSlashFX : MonoBehaviour
{
    [Header("대상 (비우면 자동으로 찾음)")]
    public SpriteRenderer targetSprite;

    [Header("머티리얼 (텍스처는 머티리얼에서 교체)")]
    public Material arcMaterial;
    public Material wispMaterial;
    public Material emberMaterial;
    public Material flashMaterial;

    [Header("공통")]
    [Tooltip("켜면 Time.timeScale(히트스톱/일시정지) 영향을 받지 않음")]
    public bool useUnscaledTime = false;
    [Range(8, 128)] public int arcSegments = 48;

    [Header("불똥")]
    public int emberPoolSize = 48;
    public Vector2 emberSize = new Vector2(0.04f, 0.09f);
    public Vector2 emberSpeed = new Vector2(1.2f, 3.2f);
    public Vector2 emberLife = new Vector2(0.25f, 0.55f);
    public float emberGravity = 2.5f;
    public float emberDrag = 2.0f;
    [ColorUsage(true, true)] public Color emberHot = new Color(2.0f, 1.7f, 1.1f, 1f);
    [ColorUsage(true, true)] public Color emberCold = new Color(1.3f, 0.25f, 0.05f, 1f);

    [Header("섬광")]
    [ColorUsage(true, true)] public Color flashColor = new Color(2.2f, 1.6f, 1.0f, 1f);

    [Header("참격 목록 (프레임별)")]
    public List<SlashSwing> swings = DefaultSwings();

    [Header("테스트")]
    [Tooltip("플레이 중 이 키를 누르면 Preview Swing 번호의 참격 재생 (None = 끔)")]
    public KeyCode testKey = KeyCode.None;
    public int previewSwing = 0;

    // ------------------------------------------------------------------
    class ArcInstance
    {
        public GameObject go;
        public MeshRenderer arcMr;
        public Mesh arcMesh;
        public MeshRenderer wispMr;
        public Mesh wispMesh;
        public MeshRenderer flashMr;
        public bool playing;
        public float time;
        public float seed;
        public float lastProgress;
        public float emberAccum;
        public float flashStart = -1f;
    }

    class Ember
    {
        public Transform tr;
        public MeshRenderer mr;
        public Vector3 vel;
        public float life;
        public float maxLife;
        public float size;
        public bool alive;
    }

    Transform _root;
    Mesh _quad;
    readonly List<ArcInstance> _arcs = new List<ArcInstance>();
    readonly List<Ember> _embers = new List<Ember>();
    int _emberCursor;
    MaterialPropertyBlock _mpb;
    Sprite _lastSprite;
    bool _dirty = true;

    static readonly int ID_Progress = Shader.PropertyToID("_Progress");
    static readonly int ID_Fade = Shader.PropertyToID("_Fade");
    static readonly int ID_Cool = Shader.PropertyToID("_Cool");
    static readonly int ID_LocalTime = Shader.PropertyToID("_LocalTime");
    static readonly int ID_Seed = Shader.PropertyToID("_Seed");
    static readonly int ID_Intensity = Shader.PropertyToID("_Intensity");
    static readonly int ID_Color = Shader.PropertyToID("_Color");
    static readonly int ID_Alpha = Shader.PropertyToID("_Alpha");

    // ------------------------------------------------------------------
    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        ResolveTarget();
    }

    void OnValidate()
    {
        _dirty = true;
    }

    void OnDisable()
    {
        StopAll();
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
        foreach (var a in _arcs)
        {
            if (a.arcMesh != null) Destroy(a.arcMesh);
            if (a.wispMesh != null) Destroy(a.wispMesh);
        }
        if (_quad != null) Destroy(_quad);
    }

    void ResolveTarget()
    {
        if (targetSprite != null) return;
        var anim = GetComponentInChildren<Animator>();
        if (anim != null) targetSprite = anim.GetComponent<SpriteRenderer>();
        if (targetSprite == null) targetSprite = GetComponentInChildren<SpriteRenderer>();
        if (targetSprite == null) targetSprite = GetComponentInParent<SpriteRenderer>();
    }

    void EnsureBuilt()
    {
        if (_root == null)
        {
            ResolveTarget();
            if (targetSprite == null) return;
            var rootGo = new GameObject("SwordSlashFX_Root");
            _root = rootGo.transform;
            _root.SetParent(targetSprite.transform, false);
            _quad = BuildQuad();
            _dirty = true;
        }

        // 참격 개수 변화 대응
        while (_arcs.Count < swings.Count) _arcs.Add(CreateArc(_arcs.Count));
        while (_arcs.Count > swings.Count)
        {
            var last = _arcs[_arcs.Count - 1];
            if (last.go != null) Destroy(last.go);
            _arcs.RemoveAt(_arcs.Count - 1);
        }

        while (_embers.Count < emberPoolSize) _embers.Add(CreateEmber(_embers.Count));

        if (_dirty)
        {
            for (int i = 0; i < swings.Count; i++) RebuildArc(i);
            _dirty = false;
        }
    }

    ArcInstance CreateArc(int index)
    {
        var a = new ArcInstance();
        a.go = new GameObject("Slash_" + index);
        a.go.transform.SetParent(_root, false);

        a.arcMesh = new Mesh { name = "SlashArc_" + index };
        a.arcMesh.MarkDynamic();
        a.arcMr = CreateMeshObject("Arc", a.go.transform, a.arcMesh);

        a.wispMesh = new Mesh { name = "SlashWisp_" + index };
        a.wispMr = CreateMeshObject("Wisp", a.go.transform, a.wispMesh);

        a.flashMr = CreateMeshObject("Flash", a.go.transform, _quad);
        return a;
    }

    MeshRenderer CreateMeshObject(string name, Transform parent, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false;
        return mr;
    }

    Ember CreateEmber(int index)
    {
        var e = new Ember();
        e.mr = CreateMeshObject("Ember_" + index, _root, _quad);
        e.tr = e.mr.transform;
        return e;
    }

    void RebuildArc(int i)
    {
        var sw = swings[i];
        var a = _arcs[i];
        BuildArcMesh(a.arcMesh, sw, sw.startAngle, sw.endAngle, sw.radius - sw.width, sw.radius);

        float dir = Mathf.Sign(sw.endAngle - sw.startAngle);
        if (dir == 0f) dir = 1f;
        float wr = sw.radius + sw.wispRadiusOffset;
        BuildArcMesh(a.wispMesh, sw,
            sw.startAngle - dir * sw.wispExtraAngle * 0.5f,
            sw.endAngle + dir * sw.wispExtraAngle,
            wr - sw.wispWidth, wr);
    }

    // ------------------------------------------------------------------
    void LateUpdate()
    {
        EnsureBuilt();
        if (_root == null) return;

        // 스프라이트 flipX 대응
        _root.localScale = new Vector3(targetSprite.flipX ? -1f : 1f, targetSprite.flipY ? -1f : 1f, 1f);

        // 프레임 감지
        var s = targetSprite.sprite;
        if (s != _lastSprite)
        {
            _lastSprite = s;
            if (s != null)
            {
                for (int i = 0; i < swings.Count; i++)
                {
                    var sw = swings[i];
                    if (sw.active && !string.IsNullOrEmpty(sw.triggerSprite) && s.name == sw.triggerSprite)
                        Play(i);
                }
            }
        }

        if (testKey != KeyCode.None && Input.GetKeyDown(testKey)) Play(previewSwing);

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        for (int i = 0; i < _arcs.Count && i < swings.Count; i++) UpdateArc(i, dt);
        UpdateEmbers(dt);
    }

    // ------------------------------------------------------------------
    /// <summary>참격 재생 (index = swings 목록 번호)</summary>
    public void Play(int index)
    {
        EnsureBuilt();
        if (_root == null || index < 0 || index >= swings.Count) return;
        if (_dirty) { for (int i = 0; i < swings.Count; i++) RebuildArc(i); _dirty = false; }

        var a = _arcs[index];
        var sw = swings[index];
        a.playing = true;
        a.time = 0f;
        a.seed = UnityEngine.Random.value * 10f;
        a.lastProgress = 0f;
        a.emberAccum = 0f;
        a.flashStart = -1f;

        ApplySorting(a.arcMr, sw.sortingOrderOffset);
        ApplySorting(a.wispMr, sw.sortingOrderOffset);
        ApplySorting(a.flashMr, sw.sortingOrderOffset + 1);

        a.arcMr.sharedMaterial = sw.arcMaterial != null ? sw.arcMaterial : arcMaterial;
        a.wispMr.sharedMaterial = wispMaterial;
        a.flashMr.sharedMaterial = flashMaterial;
    }

    /// <summary>이름으로 재생</summary>
    public void Play(string swingName)
    {
        for (int i = 0; i < swings.Count; i++)
            if (swings[i].name == swingName) { Play(i); return; }
    }

    [ContextMenu("Preview Swing (플레이 중)")]
    void PreviewSwing() => Play(previewSwing);

    public void StopAll()
    {
        foreach (var a in _arcs)
        {
            a.playing = false;
            if (a.arcMr != null) a.arcMr.enabled = false;
            if (a.wispMr != null) a.wispMr.enabled = false;
            if (a.flashMr != null) a.flashMr.enabled = false;
        }
        foreach (var e in _embers)
        {
            e.alive = false;
            if (e.mr != null) e.mr.enabled = false;
        }
    }

    void ApplySorting(Renderer r, int offset)
    {
        if (r == null || targetSprite == null) return;
        r.sortingLayerID = targetSprite.sortingLayerID;
        r.sortingOrder = targetSprite.sortingOrder + offset;
    }

    // ------------------------------------------------------------------
    void UpdateArc(int i, float dt)
    {
        var a = _arcs[i];
        if (!a.playing) return;
        var sw = swings[i];

        a.time += dt;
        float tl = a.time - Mathf.Max(0f, sw.delay);
        if (tl < 0f)
        {
            a.arcMr.enabled = a.wispMr.enabled = a.flashMr.enabled = false;
            return;
        }

        float sweep = Mathf.Max(sw.sweepTime, 0.0001f);
        float total = sweep + sw.holdTime + Mathf.Max(sw.fadeTime, 0.0001f);
        if (tl > total + 0.05f && (!sw.flash || a.flashStart < 0f || tl - a.flashStart > sw.flashTime))
        {
            a.playing = false;
            a.arcMr.enabled = a.wispMr.enabled = a.flashMr.enabled = false;
            return;
        }

        float pr = EaseOut(Mathf.Clamp01(tl / sweep));
        float fade = Mathf.Clamp01((tl - sweep - sw.holdTime) / Mathf.Max(sw.fadeTime, 0.0001f));
        float cool = Mathf.Clamp01((tl - sweep * 0.6f) / Mathf.Max(sw.holdTime + sw.fadeTime * 0.7f, 0.0001f));

        // 본 참격
        a.arcMr.enabled = fade < 1f;
        _mpb.Clear();
        _mpb.SetFloat(ID_Progress, pr);
        _mpb.SetFloat(ID_Fade, fade);
        _mpb.SetFloat(ID_Cool, cool);
        _mpb.SetFloat(ID_LocalTime, tl);
        _mpb.SetFloat(ID_Seed, a.seed);
        _mpb.SetFloat(ID_Intensity, sw.intensity);
        a.arcMr.SetPropertyBlock(_mpb);

        // 보조 잔선
        if (sw.wisp && wispMaterial != null)
        {
            float wpr = EaseOut(Mathf.Clamp01(tl / (sweep / Mathf.Max(sw.wispSpeed, 0.05f))));
            a.wispMr.enabled = fade < 1f;
            _mpb.Clear();
            _mpb.SetFloat(ID_Progress, wpr);
            _mpb.SetFloat(ID_Fade, Mathf.Clamp01(fade * 1.2f));
            _mpb.SetFloat(ID_Cool, cool);
            _mpb.SetFloat(ID_LocalTime, tl);
            _mpb.SetFloat(ID_Seed, a.seed + 3.1f);
            a.wispMr.SetPropertyBlock(_mpb);
        }
        else a.wispMr.enabled = false;

        // 불똥: 휘두르는 머리 위치에서 발생
        if (sw.emberCount > 0 && emberMaterial != null && a.lastProgress < 1f)
        {
            a.emberAccum += (pr - a.lastProgress) * sw.emberCount;
            while (a.emberAccum >= 1f)
            {
                a.emberAccum -= 1f;
                float u = Mathf.Lerp(a.lastProgress, pr, UnityEngine.Random.value);
                SpawnEmber(sw, u);
            }
        }
        a.lastProgress = pr;

        // 섬광
        if (sw.flash && flashMaterial != null)
        {
            if (a.flashStart < 0f && tl >= sweep * 0.8f) a.flashStart = tl;
            if (a.flashStart >= 0f)
            {
                float ft = (tl - a.flashStart) / Mathf.Max(sw.flashTime, 0.01f);
                if (ft <= 1f)
                {
                    a.flashMr.enabled = true;
                    var tr = a.flashMr.transform;
                    tr.localPosition = new Vector3(sw.flashPosition.x, sw.flashPosition.y, 0f);
                    float sc = sw.flashSize * (0.4f + 0.8f * EaseOut(Mathf.Clamp01(ft * 2.5f)));
                    tr.localScale = new Vector3(sc * 1.6f, sc, 1f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, (a.seed - 5f) * 2f);
                    _mpb.Clear();
                    Color c = flashColor;
                    c.a = 1f - ft * ft;
                    _mpb.SetColor(ID_Color, c);
                    a.flashMr.SetPropertyBlock(_mpb);
                }
                else a.flashMr.enabled = false;
            }
        }
        else a.flashMr.enabled = false;
    }

    void SpawnEmber(SlashSwing sw, float u)
    {
        if (_embers.Count == 0) return;
        var e = _embers[_emberCursor];
        _emberCursor = (_emberCursor + 1) % _embers.Count;

        float v = UnityEngine.Random.Range(0.35f, 1.0f);
        float r = sw.radius - sw.width * (1f - v);
        Vector2 p = ArcPoint(sw, AngleAt(sw.startAngle, sw.endAngle, u), r);
        Vector2 p2 = ArcPoint(sw, AngleAt(sw.startAngle, sw.endAngle, Mathf.Min(u + 0.01f, 1f)), r);
        Vector2 tangent = (p2 - p).sqrMagnitude > 1e-8f ? (p2 - p).normalized : Vector2.right;
        Vector2 outward = (p - sw.center).sqrMagnitude > 1e-8f ? (p - sw.center).normalized : Vector2.up;

        float spd = UnityEngine.Random.Range(emberSpeed.x, emberSpeed.y);
        Vector2 vel = tangent * spd * 0.7f + outward * spd * UnityEngine.Random.Range(0.2f, 0.9f);

        e.tr.localPosition = new Vector3(p.x, p.y, 0f);
        e.vel = vel;
        e.maxLife = UnityEngine.Random.Range(emberLife.x, emberLife.y);
        e.life = 0f;
        e.size = UnityEngine.Random.Range(emberSize.x, emberSize.y);
        e.alive = true;
        e.mr.enabled = true;
        e.mr.sharedMaterial = emberMaterial;
        ApplySorting(e.mr, sw.sortingOrderOffset + 1);
    }

    void UpdateEmbers(float dt)
    {
        foreach (var e in _embers)
        {
            if (!e.alive) continue;
            e.life += dt;
            float k = e.life / e.maxLife;
            if (k >= 1f)
            {
                e.alive = false;
                e.mr.enabled = false;
                continue;
            }
            e.vel += Vector3.down * emberGravity * dt;
            e.vel *= Mathf.Clamp01(1f - emberDrag * dt);
            e.tr.localPosition += e.vel * dt;

            float ang = Mathf.Atan2(e.vel.y, e.vel.x) * Mathf.Rad2Deg;
            e.tr.localRotation = Quaternion.Euler(0f, 0f, ang);
            float sz = e.size * (1f - k * 0.6f);
            e.tr.localScale = new Vector3(sz * 2.4f, sz, 1f);

            Color c = Color.Lerp(emberHot, emberCold, k);
            c.a = 1f - k * k;
            _mpb.Clear();
            _mpb.SetColor(ID_Color, c);
            e.mr.SetPropertyBlock(_mpb);
        }
    }

    // ------------------------------------------------------------------
    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    static float AngleAt(float a0, float a1, float u) => Mathf.Lerp(a0, a1, u);

    static Vector2 ArcPoint(SlashSwing sw, float angleDeg, float r)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector2 p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * sw.squash);
        float rot = sw.rotation * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
        p = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
        return p + sw.center;
    }

    void BuildArcMesh(Mesh m, SlashSwing sw, float a0, float a1, float rIn, float rOut)
    {
        int seg = Mathf.Max(arcSegments, 4);
        var verts = new Vector3[(seg + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var cols = new Color[verts.Length];
        var tris = new int[seg * 6];
        for (int k = 0; k <= seg; k++)
        {
            float u = (float)k / seg;
            float ang = AngleAt(a0, a1, u);
            Vector2 pin = ArcPoint(sw, ang, Mathf.Max(rIn, 0f));
            Vector2 pout = ArcPoint(sw, ang, rOut);
            verts[k * 2] = pin;
            verts[k * 2 + 1] = pout;
            uvs[k * 2] = new Vector2(u, 0f);
            uvs[k * 2 + 1] = new Vector2(u, 1f);
            cols[k * 2] = cols[k * 2 + 1] = Color.white;
            if (k < seg)
            {
                int b = k * 2, t = k * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }
        }
        m.Clear();
        m.vertices = verts;
        m.uv = uvs;
        m.colors = cols;
        m.triangles = tris;
        m.RecalculateBounds();
    }

    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "SlashQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------
    // Scene 뷰에서 참격 모양 미리보기 (컴포넌트 선택 시)
    void OnDrawGizmosSelected()
    {
        var sr = targetSprite;
        if (sr == null)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null) sr = anim.GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        }
        if (sr == null) return;

        Color[] palette = { new Color(1f, 0.6f, 0.1f), new Color(0.3f, 0.8f, 1f), new Color(0.5f, 1f, 0.3f), new Color(1f, 0.3f, 0.6f) };
        var flip = new Vector3(sr.flipX ? -1f : 1f, sr.flipY ? -1f : 1f, 1f);
        Gizmos.matrix = sr.transform.localToWorldMatrix * Matrix4x4.Scale(flip);

        for (int i = 0; i < swings.Count; i++)
        {
            var sw = swings[i];
            if (!sw.active) continue;
            Gizmos.color = palette[i % palette.Length];
            const int seg = 32;
            Vector2 prevIn = Vector2.zero, prevOut = Vector2.zero;
            for (int k = 0; k <= seg; k++)
            {
                float ang = AngleAt(sw.startAngle, sw.endAngle, (float)k / seg);
                Vector2 pin = ArcPoint(sw, ang, sw.radius - sw.width);
                Vector2 pout = ArcPoint(sw, ang, sw.radius);
                if (k > 0)
                {
                    Gizmos.DrawLine(prevIn, pin);
                    Gizmos.DrawLine(prevOut, pout);
                }
                else Gizmos.DrawLine(pin, pout); // 시작선
                prevIn = pin; prevOut = pout;
            }
            Gizmos.DrawWireSphere(sw.center, 0.03f);
            if (sw.flash) Gizmos.DrawWireCube(sw.flashPosition, Vector3.one * 0.15f);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }

    // ------------------------------------------------------------------
    // sword_attack_sprite (11프레임, 2048px, PPU 700) 기준 기본값
    public static List<SlashSwing> DefaultSwings()
    {
        return new List<SlashSwing>
        {
            // 1타: 아래 → 뒤 → 머리 위 → 앞 위로 크게 올려베기 (Sword01)
            new SlashSwing("1타 올려베기", "sword_attack_sprite_1",
                new Vector2(-0.18f, 0.09f), 0.95f, 0.42f, 258f, 45f, 1f, 0.07f, 0.12f, 0.35f),

            // 2타: 뒤쪽 위 → 뒤쪽 아래로 휩쓸기 (Sword02)
            new SlashSwing("2타 휩쓸기", "sword_attack_sprite_4",
                new Vector2(-0.30f, 0.09f), 0.92f, 0.40f, 135f, 255f, 0.9f, 0.06f, 0.10f, 0.35f),

            // 3타-1: 뒤쪽 아래 → 앞 → 위로 올려베기 (Sword03 앞부분)
            new SlashSwing("3타 올려베기", "sword_attack_sprite_7",
                new Vector2(-0.10f, 0.12f), 0.90f, 0.38f, 226f, 420f, 0.72f, 0.06f, 0.12f, 0.35f),

            // 3타-2: 마무리 내려베기 (가장 크게 + 섬광)
            new SlashSwing("3타 마무리", "sword_attack_sprite_9",
                new Vector2(-0.20f, 0.05f), 1.05f, 0.50f, 45f, 235f, 1f, 0.07f, 0.16f, 0.45f)
            {
                intensity = 1.5f, emberCount = 18, flash = true,
                flashPosition = new Vector2(0.75f, 0.35f), flashSize = 1.6f
            },
        };
    }
}

[Serializable]
public class SlashSwing
{
    public string name = "Swing";
    public bool active = true;
    [Tooltip("캐릭터 스프라이트가 이 이름으로 바뀌는 순간 재생")]
    public string triggerSprite = "";
    [Tooltip("트리거 후 지연 시간(초)")]
    public float delay = 0f;

    [Header("모양 (스프라이트 기준 로컬 좌표, 유닛)")]
    public Vector2 center = Vector2.zero;
    public float radius = 0.95f;
    public float width = 0.4f;
    [Tooltip("시작 각도 (0 = 오른쪽, 90 = 위, 180 = 왼쪽, 270 = 아래)")]
    public float startAngle = 180f;
    [Tooltip("끝 각도. 시작보다 크면 반시계, 작으면 시계 방향으로 휘두름")]
    public float endAngle = 0f;
    [Range(0.2f, 1.5f)] public float squash = 1f;
    public float rotation = 0f;

    [Header("타이밍 (초)")]
    public float sweepTime = 0.07f;
    public float holdTime = 0.12f;
    public float fadeTime = 0.35f;

    [Header("보기")]
    public float intensity = 1.4f;
    [Tooltip("비우면 공용 Arc Material 사용 (참격마다 다른 텍스처를 쓰고 싶을 때)")]
    public Material arcMaterial;
    public int sortingOrderOffset = 1;

    [Header("보조 잔선")]
    public bool wisp = true;
    public float wispWidth = 0.2f;
    public float wispRadiusOffset = 0.14f;
    public float wispExtraAngle = 20f;
    [Tooltip("1보다 작으면 본 참격보다 느리게 따라옴")]
    public float wispSpeed = 0.85f;

    [Header("불똥")]
    public int emberCount = 10;

    [Header("섬광")]
    public bool flash = false;
    public Vector2 flashPosition = new Vector2(0.8f, 0.3f);
    public float flashSize = 1.4f;
    public float flashTime = 0.25f;

    public SlashSwing() { }

    public SlashSwing(string name, string trigger, Vector2 center, float radius, float width,
        float startAngle, float endAngle, float squash, float sweep, float hold, float fade)
    {
        this.name = name;
        triggerSprite = trigger;
        this.center = center;
        this.radius = radius;
        this.width = width;
        this.startAngle = startAngle;
        this.endAngle = endAngle;
        this.squash = squash;
        sweepTime = sweep;
        holdTime = hold;
        fadeTime = fade;
    }
}
