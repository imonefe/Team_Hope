using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [2026-11-24 추가] 보스방 코어 부서짐 연출 프리팹 3개(참격 · 관통 · 타격)를 만들고 보스방 코어에 연결한다 — 에디터 전용.
///  만드는 것
///   - 재질 4. Art/Materials/VFX/CoreBreak/ : M_CoreBreak_Flash · M_CoreBreak_Glyph (더하기) · M_CoreBreak_Shard_속성 (반투명)
///   - 프리팹 1.Prefab/Effect/VFX/FX_CoreBreak_Slash · Pierce · Strike
///       Flash(속성 색 번쩍임) · Smoke(회색 연기) · Shards(검은 파편) · Glyphs(비트맵 글자, 림라이트 색) · Symbol(깃펜) · SymbolGlyphs(깃펜 모양에서 글자)
///   - _Test/Room/Room_BossTest 의 BossCore.breakFx 연결 (궁수 = 관통, 검사 = 참격, 마법사 = 타격)
///  유니티가 켜질 때 한 번 자동 실행 (자동 실행 표시 AutoKey 가 바뀌면 다시 한 번). 다시 만들기: 메뉴 "EventHorizon/보스 코어 부서짐 연출 만들기"
///  (다시 만들면 프리팹 · 재질에서 직접 바꾼 값은 덮어씀)
/// </summary>
public static class CoreBreakFxBuilder
{
    const string AutoKey = "EH_CoreBreakFxBuilder_v2";   // [2026-11-24] v2: 글자 색 = 림라이트 색 → 한 번 다시 만듦
    const string TexDir = "Assets/4. Art/Textures/VFX/CoreBreak/";
    const string MatDir = "Assets/4. Art/Materials/VFX/CoreBreak/";
    const string FxDir = "Assets/1.Prefab/Effect/VFX/";
    const string Room = "Assets/_Test/Room/Room_BossTest.prefab";
    const string SmokeSrc = "Assets/1.Prefab/Effect/VFX/FX_SoftSmoke_Test.prefab";
    const string MatTemplate = "Assets/4. Art/Materials/VFX/Library/M_Lib_Shard.mat";   // URP Particles/Unlit (더하기)

    struct Kind
    {
        public string name, quill; public Color rim; public float quillScale; public BossCoreKind core;
    }
    static readonly Kind[] Kinds =
    {
        new Kind { name = "Slash",  quill = "Assets/4. Art/Textures/Object/MidBossCore/ring_slash_e01_quill.png",  rim = new Color(2.6f, 1.1f, 0.45f, 1f), quillScale = 0.1275f, core = BossCoreKind.Swordsman },
        new Kind { name = "Pierce", quill = "Assets/4. Art/Textures/Object/MidBossCore/ring_pierce_e01_quill.png", rim = new Color(1.25f, 1.41f, 2.6f, 1f), quillScale = 0.12f,   core = BossCoreKind.Archer },
        new Kind { name = "Strike", quill = "Assets/4. Art/Textures/Object/MidBossCore/ring_strike_e01_quill.png", rim = new Color(2.6f, 0.55f, 0.42f, 1f), quillScale = 0.15f,   core = BossCoreKind.Mage },
    };

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetBool(AutoKey, true);
            Build();
        };
    }

    [MenuItem("EventHorizon/보스 코어 부서짐 연출 만들기")]
    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        var tmpl = AssetDatabase.LoadAssetAtPath<Material>(MatTemplate);
        if (tmpl == null) { Debug.LogError("[코어 부서짐] 재질 틀이 없습니다: " + MatTemplate); return; }
        var flashMat = MakeMat(tmpl, "M_CoreBreak_Flash", Tex("core_flash"), true);
        var glyphMat = MakeMat(tmpl, "M_CoreBreak_Glyph", Tex("core_glyphs"), true);
        Material smokeMat = null;
        var smoke = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeSrc);
        if (smoke != null) { var r = smoke.GetComponent<ParticleSystemRenderer>(); if (r != null) smokeMat = r.sharedMaterial; }

        var made = new GameObject[Kinds.Length];
        for (int i = 0; i < Kinds.Length; i++)
        {
            var k = Kinds[i];
            var shardMat = MakeMat(tmpl, "M_CoreBreak_Shard_" + k.name, Tex("core_shards_" + k.name.ToLower()), false);
            made[i] = BuildFx(k, flashMat, glyphMat, shardMat, smokeMat);
        }
        AssetDatabase.SaveAssets();
        LinkRoom(made);
        Debug.Log("[코어 부서짐] 연출 프리팹 3개를 만들고 보스방 코어에 연결했습니다.");
    }

    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + n + ".png");

    static Material MakeMat(Material tmpl, string name, Texture tex, bool additive)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(tmpl);
        m.name = name;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHAMODULATE_ON");
        if (additive)
        {
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        else
        {
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (m.HasProperty("_SrcBlendAlpha")) { m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha); }
        m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)RenderQueue.Transparent;
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject BuildFx(Kind k, Material flashMat, Material glyphMat, Material shardMat, Material smokeMat)
    {
        var root = new GameObject("FX_CoreBreak_" + k.name);
        try
        {
            var fx = root.AddComponent<CoreBreakFx>();

            // 번쩍임 — 속성 색 빛 원 1개
            var flash = NewPS(root, "Flash", flashMat, 3006, false);
            {
                var m = flash.main; m.startLifetime = 0.18f; m.startSpeed = 0f; m.startSize = 2.6f; m.startColor = k.rim; m.maxParticles = 2;
                Burst(flash, 1);
                var sz = flash.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.4f));
                FadeOut(flash, 1f);
            }
            // 연기 — 회색 연기가 퍼지며 사라짐
            if (smokeMat != null)
            {
                var sm = NewPS(root, "Smoke", smokeMat, 3001, false);
                var m = sm.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f); m.startColor = new Color(0.72f, 0.72f, 0.76f, 0.6f); m.maxParticles = 20;
                Burst(sm, 12);
                Sphere(sm, 0.4f);
                var sz = sm.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.6f));
                FadeOut(sm, 0.6f);
            }
            // 파편 — 검은 파편 10개가 튀어 회전하며 떨어짐
            var sh = NewPS(root, "Shards", shardMat, 3004, true);
            {
                var m = sh.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.0f); m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); m.gravityModifier = 1.6f; m.maxParticles = 16;
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                Burst(sh, 10);
                Sphere(sh, 0.35f);
                var rot = sh.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
                Sheet(sh, 5, 1);
                FadeOut(sh, 0.75f);
            }
            // 글자 — 비트맵 글자가 안개 흐름 방향(왼쪽 위)으로 흩날리며 깜빡이다 사라짐
            var gl = NewPS(root, "Glyphs", glyphMat, 3005, true);
            GlyphSetup(gl, 26, new ParticleSystem.MinMaxCurve(1f, 3f), 0.6f, k.rim);

            // 상징(깃펜) + 그 모양에서 흩어지는 글자
            var symGo = new GameObject("Symbol");
            symGo.transform.SetParent(root.transform, false);
            symGo.transform.localScale = Vector3.one * k.quillScale;
            var sr = symGo.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(k.quill);
            sr.sortingOrder = 3003;
            fx.symbol = sr;

            var sg = NewPS(symGo, "SymbolGlyphs", glyphMat, 3005, true);
            sg.transform.localScale = Vector3.one / Mathf.Max(0.0001f, k.quillScale);   // 글자 크기는 상징 크기와 상관없이
            GlyphSetup(sg, 30, new ParticleSystem.MinMaxCurve(0.5f, 1.5f), 0f, k.rim);
            var shape = sg.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.SpriteRenderer; shape.spriteRenderer = sr;
            var mm = sg.main; mm.startDelay = 0.05f; mm.scalingMode = ParticleSystemScalingMode.Hierarchy;

            string path = FxDir + root.name + ".prefab";
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static ParticleSystem NewPS(GameObject parent, string name, Material mat, int order, bool world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = false; m.playOnAwake = true; m.duration = 1f;
        m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.sortingOrder = order; r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission; em.enabled = true;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Sphere(ParticleSystem ps, float radius)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius; s.radiusThickness = 1f;
    }

    static void Sheet(ParticleSystem ps, int x, int y)
    {
        var t = ps.textureSheetAnimation; t.enabled = true;
        t.mode = ParticleSystemAnimationMode.Grid; t.numTilesX = x; t.numTilesY = y;
        t.animation = ParticleSystemAnimationType.WholeSheet;
        t.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // 입자마다 무작위 칸 하나로 고정
        t.cycleCount = 1;
    }

    static void FadeOut(ParticleSystem ps, float fadeStart)
    {
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp01(fadeStart)), new GradientAlphaKey(0f, 1f) });
        c.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void GlyphSetup(ParticleSystem ps, int count, ParticleSystem.MinMaxCurve speed, float sphere, Color rim)
    {
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        m.startSpeed = speed;
        m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        m.maxParticles = count + 4;
        // [2026-11-24] 글자 색 = 그 코어의 림라이트 색 ~ 그보다 조금 밝은 색 사이에서 무작위
        Color bright = Color.Lerp(rim, Color.white * Mathf.Max(rim.r, rim.g, rim.b), 0.35f); bright.a = 1f;
        m.startColor = new ParticleSystem.MinMaxGradient(rim, bright);
        Burst(ps, count);
        if (sphere > 0f) Sphere(ps, sphere);
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(-1.6f, -0.6f); v.y = new ParticleSystem.MinMaxCurve(0.4f, 1.2f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var n = ps.noise; n.enabled = true; n.strength = 0.5f; n.frequency = 2f;
        Sheet(ps, 8, 4);
        // 깜빡이다 사라짐
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.35f, 0.25f), new GradientAlphaKey(1f, 0.4f),
                          new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) });
        c.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void LinkRoom(GameObject[] fx)
    {
        if (!File.Exists(Room)) { Debug.LogWarning("[코어 부서짐] 보스방 프리팹이 없습니다: " + Room); return; }
        var root = PrefabUtility.LoadPrefabContents(Room);
        try
        {
            int n = 0;
            foreach (var core in root.GetComponentsInChildren<BossCore>(true))
                for (int i = 0; i < Kinds.Length; i++)
                    if (Kinds[i].core == core.kind && fx[i] != null) { core.breakFx = fx[i]; n++; }
            PrefabUtility.SaveAsPrefabAsset(root, Room);
            Debug.Log($"[코어 부서짐] 보스방 코어 {n}개에 연출 연결");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
