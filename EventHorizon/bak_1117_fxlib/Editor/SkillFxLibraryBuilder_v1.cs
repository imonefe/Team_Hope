// [2026-11-17 신규] 스킬용 기본 이펙트 모음 생성기
// 메뉴: EventHorizon/이펙트 모음/1. 프리팹 만들기, 2. 미리보기 장면 만들기
//
// - 이펙트 13종 × 색 4종(주황·하늘·보라·흰) = 프리팹 52개를 한 번에 만든다.
// - 저장 위치: Assets/1.Prefab/Effect/Library/<이펙트이름>/FX_Lib_<이펙트>_<색>.prefab
// - 재질·그림: Assets/4. Art/VFX/Library/ (재질은 흰색 하나를 모두 공유, 색은 파티클 색으로만 바꾼다)
// - 크기 기준: player.prefab 충돌체 높이 2.35 (발밑 = 몸 중심에서 약 -1.2)
//   · 바닥형(불기둥·바닥 폭발·연쇄 분출)은 원점이 "발밑"
//   · 나머지는 원점이 "중심"(총구·구체·착탄 지점)
//   · 앞쪽 방향 = +X. 왼쪽을 볼 때는 루트 크기 x를 -1로.
// - 이미 같은 이름의 프리팹이 있으면 프로젝트 최상위 bak_1117_fxlib/ 에 복사해 둔 뒤 덮어쓴다(GUID 유지).
// - 기존 프리팹·재질·스크립트는 건드리지 않는다.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SkillFxLibraryBuilder
{
    // ───────── 경로 ─────────
    const string PrefabRoot = "Assets/1.Prefab/Effect/Library";
    const string ArtRoot    = "Assets/4. Art/VFX/Library";
    const string TexDir     = ArtRoot + "/Textures";
    const string MatDir     = ArtRoot + "/Materials";
    const string ScenePath  = "Assets/_Test/FxLibrary_Preview.unity";
    const string BakDir     = "bak_1117_fxlib"; // 프로젝트 최상위

    // 재질 원본(설정 복사용). 더하기 혼합 / 알파 혼합 URP 파티클 재질
    const string SrcAdditive = "Assets/4. Art/VFX/Skill/M_ShieldShard.mat";
    const string SrcAlpha    = "Assets/4. Art/VFX/Enemy/2sBoss/cloud.mat";

    // 정렬 순서. 플레이어 스프라이트가 5000 이므로 그 앞에 그린다.
    const int OrderSmoke = 5100, OrderGround = 5101, OrderGlow = 5102,
              OrderFlame = 5103, OrderSpark = 5104, OrderFlare = 5105;

    // ───────── 색 ─────────
    struct Pal
    {
        public string name; public Color hot, main, dark, smoke;
        public Pal(string n, Color h, Color m, Color d, Color s) { name = n; hot = h; main = m; dark = d; smoke = s; }
    }

    static readonly Pal[] Palettes =
    {
        new Pal("Orange", new Color(1f, .95f, .78f), new Color(1f, .55f, .15f), new Color(.65f, .13f, .03f), new Color(.22f, .19f, .17f)),
        new Pal("Sky",    new Color(.88f, 1f, 1f),   new Color(.30f, .75f, 1f), new Color(.05f, .22f, .62f), new Color(.17f, .20f, .25f)),
        new Pal("Violet", new Color(1f, .88f, 1f),   new Color(.72f, .32f, 1f), new Color(.32f, .06f, .55f), new Color(.21f, .17f, .25f)),
        new Pal("White",  new Color(1f, 1f, 1f),     new Color(.88f, .92f, 1f), new Color(.42f, .47f, .58f), new Color(.22f, .22f, .24f)),
    };

    // ───────── 재질 (생성 시 채움) ─────────
    static Material mGlow, mDot, mFlare, mRing, mShock, mFlame, mShard, mSmoke;

    delegate void BuildFn(Transform root, Pal p);

    struct FxDef
    {
        public string name; public bool loop; public BuildFn build;
        public FxDef(string n, bool l, BuildFn b) { name = n; loop = l; build = b; }
    }

    static FxDef[] Defs()
    {
        return new[]
        {
            new FxDef("Muzzle",              false, BuildMuzzle),
            new FxDef("Muzzle_Rapid",        false, BuildMuzzleRapid),
            new FxDef("Tracer",              true,  BuildTracer),
            new FxDef("Impact_Bullet",       false, BuildImpactBullet),
            new FxDef("FlamePillar",         false, (r, p) => BuildFlamePillar(r, p, false)),
            new FxDef("FlamePillar_Loop",    true,  (r, p) => BuildFlamePillar(r, p, true)),
            new FxDef("GroundBurst",         false, BuildGroundBurst),
            new FxDef("GroundEruption_Line", false, BuildEruptionLine),
            new FxDef("Orb_Sustain",         true,  BuildOrbSustain),
            new FxDef("Orb_Shatter",         false, BuildOrbShatter),
            new FxDef("Charge",              true,  BuildCharge),
            new FxDef("Shockwave",           false, BuildShockwave),
            new FxDef("Arc_Zap",             false, BuildArcZap),
        };
    }

    // ═════════════════════════ 메뉴 ═════════════════════════

    [MenuItem("EventHorizon/이펙트 모음/1. 프리팹 만들기")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabRoot); EnsureFolder(MatDir); EnsureFolder(TexDir);
        if (!SetupTextures()) return;
        SetupMaterials();

        int count = 0;
        foreach (var def in Defs())
        {
            string dir = PrefabRoot + "/" + def.name;
            EnsureFolder(dir);
            foreach (var pal in Palettes)
            {
                string fxName = "FX_Lib_" + def.name + "_" + pal.name;
                var root = new GameObject(fxName);
                try
                {
                    def.build(root.transform, pal);
                    if (!def.loop)
                    {
                        var ape = root.AddComponent<AutoPlayEffect>();
                        ape.lifetime = TotalTime(root);   // 지연 시작 층까지 포함한 길이
                        ape.extraTime = 0.1f;
                    }
                    SavePrefab(root, dir + "/" + fxName + ".prefab");
                    count++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[이펙트 모음] 프리팹 {count}개 생성 완료 → {PrefabRoot}");
        EditorUtility.DisplayDialog("이펙트 모음", $"프리팹 {count}개를 만들었습니다.\n{PrefabRoot}", "확인");
    }

    [MenuItem("EventHorizon/이펙트 모음/2. 미리보기 장면 만들기")]
    public static void BuildPreviewScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var defs = Defs();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(defs[0].name, Palettes[0].name)) == null)
            BuildAll();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 11f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.06f, .06f, .08f);
        cam.transform.position = new Vector3(0, 0, -20);
        cam.nearClipPlane = .1f; cam.farClipPlane = 100f;

        var loopGo = new GameObject("FxPreview");
        var loop = loopGo.AddComponent<FxPreviewLoop>();
        var slots = new List<FxPreviewLoop.Slot>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        const int cols = 4; const float dx = 7.5f, dy = 5.4f;
        for (int i = 0; i < defs.Length; i++)
        {
            int c = i % cols, r = i / cols;
            var pos = new Vector3((c - (cols - 1) / 2f) * dx, 8f - r * dy, 0);
            var slot = new FxPreviewLoop.Slot
            {
                name = defs[i].name,
                position = pos,
                loop = defs[i].loop,
                move = defs[i].name == "Tracer",
                variants = new GameObject[Palettes.Length],
            };
            for (int k = 0; k < Palettes.Length; k++)
                slot.variants[k] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(defs[i].name, Palettes[k].name));
            slots.Add(slot);

            // 이름표
            var label = new GameObject("Label_" + defs[i].name);
            label.transform.position = pos + new Vector3(0, -1.9f, 0);
            var tm = label.AddComponent<TextMesh>();
            tm.text = defs[i].name; tm.font = font; tm.fontSize = 48; tm.characterSize = .07f;
            tm.anchor = TextAnchor.UpperCenter; tm.color = new Color(.75f, .75f, .8f);
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        loop.slots = slots.ToArray();
        loop.colorNames = Array.ConvertAll(Palettes, p => p.name);

        EnsureFolder("Assets/_Test");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeObject = loopGo;
        Debug.Log("[이펙트 모음] 미리보기 장면 저장 → " + ScenePath + "  (재생 후 FxPreview의 '색 번호'를 바꿔 보세요)");
    }

    static string PrefabPath(string fx, string col) => $"{PrefabRoot}/{fx}/FX_Lib_{fx}_{col}.prefab";

    // ═════════════════════════ 이펙트 정의 ═════════════════════════
    // 공통 규칙: 앞 = +X, 위 = +Y, 화면 = XY 평면. 시간 단위 초.

    // 1. 총구 섬광 — 앞으로 터지는 십자 섬광 + 원뿔 불티 + 연기 한 줌 (0.12초)
    static void BuildMuzzle(Transform root, Pal p)
    {
        var flash = Layer(root, "flash_long", mFlare, OrderFlare);
        Burst(flash, 1); Life(flash, .07f); Size3(flash, 2.0f, .7f); Pos(flash, .55f, 0);
        Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var flash2 = Layer(root, "flash_star", mFlare, OrderFlare);
        Burst(flash2, 1); Life(flash2, .06f); Size(flash2, 1.1f); Rot(flash2, 45f, 45f);
        Col(flash2, p.hot); FadeOut(flash2, p.hot, p.main);

        var glow = Layer(root, "glow", mGlow, OrderGlow);
        Burst(glow, 1); Life(glow, .09f); Size(glow, 1.3f); Pos(glow, .3f, 0);
        Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var tongue = Layer(root, "tongue", mFlame, OrderFlame);
        Burst(tongue, 2); Life(tongue, .08f, .1f); Size(tongue, .7f, .9f);
        Rot(tongue, -90f, -90f);                // 불꽃 그림의 위쪽을 앞(+X)으로
        Speed(tongue, 1f, 2f); Cone(tongue, 5f, .02f, Fwd);
        Flipbook(tongue); FireColor(tongue, p);

        var sparks = Layer(root, "sparks", mDot, OrderSpark);
        Burst(sparks, 8); Life(sparks, .08f, .16f); Size(sparks, .07f, .11f);
        Speed(sparks, 8f, 14f); Cone(sparks, 18f, .05f, Fwd); Stretch(sparks, .035f);
        Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);

        var smoke = Layer(root, "smoke", mSmoke, OrderSmoke);
        Burst(smoke, 3, .03f); Life(smoke, .4f, .6f); Size(smoke, .35f, .5f); Grow(smoke, 1f, 2.2f);
        Speed(smoke, .8f, 1.8f); Cone(smoke, 25f, .05f, Fwd); Drag(smoke, .4f); RandRot(smoke);
        SmokeColor(smoke, p, .35f);
    }

    // 2. 연사 섬광 — 작고 짧게 (0.06초)
    static void BuildMuzzleRapid(Transform root, Pal p)
    {
        var flash = Layer(root, "flash_long", mFlare, OrderFlare);
        Burst(flash, 1); Life(flash, .045f); Size3(flash, 1.2f, .45f); Pos(flash, .35f, 0);
        Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var glow = Layer(root, "glow", mGlow, OrderGlow);
        Burst(glow, 1); Life(glow, .06f); Size(glow, .75f); Pos(glow, .2f, 0);
        Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var sparks = Layer(root, "sparks", mDot, OrderSpark);
        Burst(sparks, 3); Life(sparks, .06f, .1f); Size(sparks, .06f, .08f);
        Speed(sparks, 9f, 13f); Cone(sparks, 12f, .03f, Fwd); Stretch(sparks, .03f);
        Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);
    }

    // 3. 탄 궤적 — 총알에 자식으로 붙인다(반복). 움직이면 꼬리가 남는다.
    static void BuildTracer(Transform root, Pal p)
    {
        var head = Layer(root, "head", mGlow, OrderGlow, loop: true);
        Rate(head, 40); Life(head, .06f); Size(head, .45f, .55f); Col(head, p.main);
        FadeOut(head, p.hot, p.main);

        var core = Layer(root, "core", mDot, OrderSpark, loop: true);
        Rate(core, 40); Life(core, .05f); Size(core, .22f); Col(core, p.hot);

        var tail = Layer(root, "tail", mDot, OrderFlame, loop: true);
        RateDist(tail, 45f);
        World(tail); Life(tail, .14f); Size(tail, .16f); Shrink(tail);
        Col(tail, p.main); FadeOut(tail, p.hot, p.dark);

        var bits = Layer(root, "bits", mDot, OrderSpark, loop: true);
        RateDist(bits, 6f);
        World(bits); Life(bits, .15f, .3f); Size(bits, .04f, .07f);
        Speed(bits, .5f, 1.5f); SphereShape(bits, .05f);
        Col(bits, p.hot); FadeOut(bits, p.hot, p.main);
    }

    // 4. 착탄 — 섬광 + 반구로 튀는 불티 + 작은 고리 (0.35초). 맞은 면의 바깥 = -X 기준.
    static void BuildImpactBullet(Transform root, Pal p)
    {
        var flash = Layer(root, "flash", mFlare, OrderFlare);
        Burst(flash, 1); Life(flash, .06f); Size(flash, 1.1f); RandRot(flash);
        Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var glow = Layer(root, "glow", mGlow, OrderGlow);
        Burst(glow, 1); Life(glow, .14f); Size(glow, 1.0f); Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var ring = Layer(root, "ring", mRing, OrderGround);
        Burst(ring, 1); Life(ring, .18f); Size(ring, .25f); Grow(ring, 1f, 4.4f);
        Col(ring, p.main); FadeOut(ring, p.hot, p.main);

        var sparks = Layer(root, "sparks", mDot, OrderSpark);
        Burst(sparks, 14); Life(sparks, .15f, .35f); Size(sparks, .05f, .09f);
        Speed(sparks, 4f, 10f); Cone(sparks, 65f, .05f, Back); Gravity(sparks, 1.5f); Stretch(sparks, .03f);
        Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);
    }

    // 5. 솟구치는 불기둥 — 바닥에서 위로 (원점 = 발밑). loop=true면 계속 탄다.
    static void BuildFlamePillar(Transform root, Pal p, bool loop)
    {
        float dur = loop ? 1f : 1.2f;

        var flames = Layer(root, "flames", mFlame, OrderFlame, loop, dur);
        Rate(flames, 42); if (!loop) Burst(flames, 10);
        Life(flames, .5f, .8f); Size(flames, 1.0f, 1.5f); SizeCurve(flames, 0, .55f, .25f, 1f, 1f, .45f);
        Speed(flames, 2.6f, 4.2f); Cone(flames, 7f, .35f, Up); Rot(flames, -10f, 10f);
        Flipbook(flames); FireColor(flames, p);

        var core = Layer(root, "core_glow", mGlow, OrderGlow, loop, dur);
        Rate(core, 14); Life(core, .3f, .45f); Size(core, 1.5f, 2f); Speed(core, 1.5f, 2.5f); Cone(core, 5f, .3f, Up);
        Col(core, p.main, .5f); FadeOut(core, p.main, p.dark);

        var embers = Layer(root, "embers", mDot, OrderSpark, loop, dur);
        Rate(embers, 30); Life(embers, .6f, 1.2f); Size(embers, .04f, .09f);
        Speed(embers, 3f, 6.5f); Cone(embers, 20f, .5f, Up); Noise(embers, 1.2f, 1.5f);
        Col(embers, p.hot); FireColor(embers, p);

        var smoke = Layer(root, "smoke", mSmoke, OrderSmoke, loop, dur);
        Rate(smoke, 6); Delay(smoke, .15f); Life(smoke, 1f, 1.4f); Size(smoke, .8f, 1.1f); Grow(smoke, 1f, 2.2f);
        Speed(smoke, 2f, 3f); Cone(smoke, 12f, .4f, Up); Pos(smoke, 0, 1.2f); RandRot(smoke);
        SmokeColor(smoke, p, .25f);

        var ring = Layer(root, "ground_ring", mRing, OrderGround);
        Burst(ring, 1); Life(ring, .35f); Size3(ring, .5f, .15f); GrowXY(ring, 1f, 4.6f);
        Col(ring, p.main); FadeOut(ring, p.hot, p.main);

        var floor = Layer(root, "ground_glow", mGlow, OrderGround, loop, dur);
        if (loop) Rate(floor, 3); else Burst(floor, 1);
        Life(floor, loop ? 1f : 1.3f); Size3(floor, 2.6f, .7f); Pos(floor, 0, .05f);
        Col(floor, p.main, .7f); FadeInOut(floor, p.main);
    }

    // 6. 바닥 폭발 — 납작한 충격파 + 위로 튀었다 떨어지는 파편 + 불꽃 + 연기 (원점 = 발밑)
    static void BuildGroundBurst(Transform root, Pal p)
    {
        var flash = Layer(root, "flash", mFlare, OrderFlare);
        Burst(flash, 1); Life(flash, .09f); Size(flash, 2.4f); Pos(flash, 0, .35f);
        Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var glow = Layer(root, "glow", mGlow, OrderGlow);
        Burst(glow, 1); Life(glow, .16f); Size3(glow, 3f, 1.8f); Pos(glow, 0, .4f);
        Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var shock = Layer(root, "shock_ring", mShock, OrderGround);
        Burst(shock, 1); Life(shock, .38f); Size3(shock, .6f, .18f); GrowXY(shock, 1f, 7f, true);
        Col(shock, p.main); FadeOut(shock, p.hot, p.main);

        var fire = Layer(root, "fire_puffs", mFlame, OrderFlame);
        Burst(fire, 9); Life(fire, .35f, .5f); Size(fire, .8f, 1.3f); Speed(fire, 2f, 4.5f);
        Cone(fire, 45f, .4f, Up); Rot(fire, -25f, 25f); Flipbook(fire); FireColor(fire, p);

        var debris = Layer(root, "debris", mShard, OrderSpark);
        Burst(debris, 18); Life(debris, .6f, .95f); Size(debris, .09f, .2f); RandRot(debris); Spin(debris, 720f);
        Speed(debris, 6f, 11f); Cone(debris, 35f, .4f, Up); Gravity(debris, 3f);
        Col(debris, p.main); FadeLate(debris, p.main, p.dark);

        var sparks = Layer(root, "sparks", mDot, OrderSpark);
        Burst(sparks, 20); Life(sparks, .2f, .45f); Size(sparks, .05f, .09f);
        Speed(sparks, 7f, 14f); Cone(sparks, 45f, .3f, Up); Gravity(sparks, 2f); Stretch(sparks, .03f);
        Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);

        var smoke = Layer(root, "smoke", mSmoke, OrderSmoke);
        Burst(smoke, 8, .04f); Life(smoke, .8f, 1.2f); Size(smoke, .9f, 1.2f); Grow(smoke, 1f, 2.2f);
        Speed(smoke, 1.5f, 3.2f); Cone(smoke, 50f, .4f, Up); Drag(smoke, .5f); RandRot(smoke);
        SmokeColor(smoke, p, .4f);
    }

    // 7. 연쇄 분출 — 바닥을 따라 앞으로 5번 이어서 솟는다 (원점 = 발밑, 길이 약 5칸)
    static void BuildEruptionLine(Transform root, Pal p)
    {
        for (int i = 0; i < 5; i++)
        {
            var g = new GameObject("erupt_" + (i + 1)).transform;
            g.SetParent(root, false);
            g.localPosition = new Vector3(.4f + i * 1.15f, 0, 0);
            float d = i * .08f, s = 0.85f + i * .07f;

            var col = Layer(g, "column", mFlame, OrderFlame);
            Burst(col, 5); Delay(col, d); Life(col, .3f, .42f); Size(col, .75f * s, 1.05f * s);
            Speed(col, 4f * s, 6.5f * s); Cone(col, 8f, .2f, Up); Rot(col, -8f, 8f); Flipbook(col); FireColor(col, p);

            var glow = Layer(g, "glow", mGlow, OrderGlow);
            Burst(glow, 1); Delay(glow, d); Life(glow, .2f); Size3(glow, 1.4f * s, 1.8f * s); Pos(glow, 0, .6f);
            Col(glow, p.main, .8f); FadeOut(glow, p.main, p.dark);

            var ring = Layer(g, "ring", mRing, OrderGround);
            Burst(ring, 1); Delay(ring, d); Life(ring, .25f); Size3(ring, .3f, .1f); GrowXY(ring, 1f, 5f);
            Col(ring, p.main); FadeOut(ring, p.hot, p.main);

            var debris = Layer(g, "debris", mShard, OrderSpark);
            Burst(debris, 6); Delay(debris, d); Life(debris, .45f, .7f); Size(debris, .07f, .15f); RandRot(debris); Spin(debris, 600f);
            Speed(debris, 4f, 8f); Cone(debris, 30f, .2f, Up); Gravity(debris, 3f);
            Col(debris, p.main); FadeLate(debris, p.main, p.dark);

            var smoke = Layer(g, "smoke", mSmoke, OrderSmoke);
            Burst(smoke, 2); Delay(smoke, d + .05f); Life(smoke, .6f, .9f); Size(smoke, .6f, .8f); Grow(smoke, 1f, 2f);
            Speed(smoke, 1.2f, 2f); Cone(smoke, 20f, .2f, Up); Drag(smoke, .4f); RandRot(smoke);
            SmokeColor(smoke, p, .3f);
        }
    }

    // 8. 유지 구체 — 계속 떠 있는 에너지 구체(투사체·소환물). 반복.
    static void BuildOrbSustain(Transform root, Pal p)
    {
        var core = Layer(root, "core", mGlow, OrderGlow + 2, loop: true);
        Rate(core, 24); Life(core, .22f); Size(core, .95f, 1.15f); Col(core, p.hot); FadeInOut(core, p.hot);

        var shell = Layer(root, "shell", mGlow, OrderGlow, loop: true);
        Rate(shell, 10); Life(shell, .45f); Size(shell, 1.9f, 2.2f); Col(shell, p.main, .45f); FadeInOut(shell, p.main);

        var ringA = Layer(root, "orbit_A", mRing, OrderFlame, loop: true);
        Rate(ringA, 5); Life(ringA, .6f); Size3(ringA, 1.9f, .55f); Rot(ringA, 18f, 22f); Spin(ringA, 30f, 60f);
        Col(ringA, p.main, .8f); FadeInOut(ringA, p.main);

        var ringB = Layer(root, "orbit_B", mRing, OrderFlame, loop: true);
        Rate(ringB, 5); Life(ringB, .6f); Size3(ringB, 1.6f, .5f); Rot(ringB, -40f, -35f); Spin(ringB, -60f, -30f);
        Col(ringB, p.hot, .6f); FadeInOut(ringB, p.hot);

        var motes = Layer(root, "motes_in", mDot, OrderSpark, loop: true);
        Rate(motes, 28); Life(motes, .55f); Size(motes, .05f, .09f); Speed(motes, -2.2f, -1.4f); SphereShape(motes, 1.3f);
        Col(motes, p.hot); FadeInOut(motes, p.hot);
    }

    // 9. 파열 구체 — 0.25초 쪼그라들었다가 깨져 흩어진다
    static void BuildOrbShatter(Transform root, Pal p)
    {
        const float T = .25f;
        var core = Layer(root, "core_shrink", mGlow, OrderGlow + 2);
        Burst(core, 1); Life(core, T); Size(core, 1.4f); SizeCurve(core, 0, 1f, .7f, .7f, 1f, .35f);
        Col(core, p.hot); FadeInOut(core, p.hot, .1f, .95f);

        var gather = Layer(root, "motes_in", mDot, OrderSpark);
        Burst(gather, 22); Life(gather, T); Size(gather, .05f, .09f); Speed(gather, -6f, -4.5f); SphereShape(gather, 1.5f);
        Stretch(gather, .03f); Col(gather, p.hot); FadeInOut(gather, p.hot);

        var flash = Layer(root, "flash", mFlare, OrderFlare);
        Burst(flash, 1); Delay(flash, T); Life(flash, .1f); Size(flash, 3.2f); Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var glow = Layer(root, "burst_glow", mGlow, OrderGlow);
        Burst(glow, 1); Delay(glow, T); Life(glow, .18f); Size(glow, 2.8f); Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var ring = Layer(root, "ring", mShock, OrderGround);
        Burst(ring, 1); Delay(ring, T); Life(ring, .3f); Size(ring, .5f); Grow(ring, 1f, 7f);
        Col(ring, p.main); FadeOut(ring, p.hot, p.main);

        var shards = Layer(root, "shards", mShard, OrderSpark);
        Burst(shards, 24); Delay(shards, T); Life(shards, .5f, .8f); Size(shards, .12f, .26f); RandRot(shards); Spin(shards, 900f);
        Speed(shards, 4f, 9f); SphereShape(shards, .3f); Gravity(shards, 1.2f); Drag(shards, .25f);
        Col(shards, p.main); FadeLate(shards, p.hot, p.dark);

        var sparks = Layer(root, "sparks", mDot, OrderSpark);
        Burst(sparks, 20); Delay(sparks, T); Life(sparks, .2f, .4f); Size(sparks, .05f, .08f);
        Speed(sparks, 8f, 14f); SphereShape(sparks, .2f); Stretch(sparks, .03f);
        Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);
    }

    // 10. 모으기 — 기 모으는 동안 계속(반복). 손 위치에 붙인다.
    static void BuildCharge(Transform root, Pal p)
    {
        var motes = Layer(root, "motes_in", mDot, OrderSpark, loop: true);
        Rate(motes, 40); Life(motes, .5f); Size(motes, .05f, .08f); Speed(motes, -3.6f, -3f); SphereShape(motes, 1.7f);
        Stretch(motes, .04f); Col(motes, p.hot); FadeInOut(motes, p.hot, .3f, 1f);

        var core = Layer(root, "core", mGlow, OrderGlow + 1, loop: true);
        Rate(core, 12); Life(core, .5f); Size(core, 1.1f); SizeCurve(core, 0, .3f, 1f, 1f);
        Col(core, p.main); FadeInOut(core, p.hot);

        var ring = Layer(root, "ring_in", mRing, OrderGround, loop: true);
        Rate(ring, 2.5f); Life(ring, .5f); Size(ring, 2.4f); SizeCurve(ring, 0, 1f, 1f, .15f);
        Col(ring, p.main); FadeInOut(ring, p.main, .5f, .9f);
    }

    // 11. 충격파 — 화면 평면으로 퍼지는 고리
    static void BuildShockwave(Transform root, Pal p)
    {
        var shock = Layer(root, "shock", mShock, OrderGround);
        Burst(shock, 1); Life(shock, .35f); Size(shock, .4f); Grow(shock, 1f, 9f, true);
        Col(shock, p.main); FadeOut(shock, p.hot, p.main);

        var ring = Layer(root, "ring_thin", mRing, OrderFlame);
        Burst(ring, 1, .03f); Life(ring, .4f); Size(ring, .3f); Grow(ring, 1f, 14f, true);
        Col(ring, p.hot, .8f); FadeOut(ring, p.hot, p.main);

        var flash = Layer(root, "flash", mGlow, OrderGlow);
        Burst(flash, 1); Life(flash, .1f); Size(flash, 1.8f); Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var sparks = Layer(root, "sparks_ring", mDot, OrderSpark);
        Burst(sparks, 24); Life(sparks, .22f, .3f); Size(sparks, .05f, .08f); Speed(sparks, 7f, 9f);
        CircleShape(sparks, .3f); Stretch(sparks, .03f); Col(sparks, p.hot); FadeOut(sparks, p.hot, p.main);
    }

    // 12. 전류 — 꺾이며 튀는 짧은 번개선
    static void BuildArcZap(Transform root, Pal p)
    {
        var flash = Layer(root, "flash", mFlare, OrderFlare);
        Burst(flash, 1); Life(flash, .06f); Size(flash, .9f); RandRot(flash); Col(flash, p.hot); FadeOut(flash, p.hot, p.main);

        var glow = Layer(root, "glow", mGlow, OrderGlow);
        Burst(glow, 2, .08f); Life(glow, .08f); Size(glow, 1.1f); Col(glow, p.main); FadeOut(glow, p.main, p.dark);

        var arcs = Layer(root, "arcs", mDot, OrderSpark);
        Burst(arcs, 7); Life(arcs, .18f, .28f); Size(arcs, .06f); Speed(arcs, 7f, 11f); CircleShape(arcs, .05f);
        Noise(arcs, 7f, 7f, 4f); Col(arcs, p.hot);
        var tr = arcs.trails;
        tr.enabled = true; tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.lifetime = new ParticleSystem.MinMaxCurve(.55f); tr.minVertexDistance = .04f;
        tr.dieWithParticles = true; tr.sizeAffectsWidth = false; tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(.07f, AnimationCurve.Linear(0, 1, 1, 0));
        arcs.GetComponent<ParticleSystemRenderer>().trailMaterial = mGlow;
        FadeOut(arcs, p.hot, p.main);

        var bits = Layer(root, "bits", mDot, OrderSpark);
        Burst(bits, 10); Life(bits, .1f, .2f); Size(bits, .04f, .06f); Speed(bits, 4f, 9f); SphereShape(bits, .05f);
        Stretch(bits, .03f); Col(bits, p.hot); FadeOut(bits, p.hot, p.main);
    }

    // ═════════════════════════ 층 도우미 ═════════════════════════

    static readonly Vector3 Up = new Vector3(-90, 0, 0);   // 도형 +Z → 월드 +Y
    static readonly Vector3 Fwd = new Vector3(0, 90, 0);   // 도형 +Z → +X
    static readonly Vector3 Back = new Vector3(0, -90, 0); // 도형 +Z → -X

    static ParticleSystem Layer(Transform parent, string name, Material mat, int order, bool loop = false, float duration = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.duration = duration; m.loop = loop; m.playOnAwake = true;
        m.startSpeed = 0f; m.startLifetime = 1f; m.startSize = 1f; m.startRotation = 0f;
        m.startColor = Color.white; m.gravityModifier = 0f;
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.maxParticles = 200;
        m.stopAction = ParticleSystemStopAction.None;

        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; em.rateOverDistance = 0f;
        var sh = ps.shape; sh.enabled = false;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.sharedMaterial = mat;
        r.sortingOrder = order;
        r.maxParticleSize = 3f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void RateDist(ParticleSystem ps, float r) { var em = ps.emission; em.rateOverDistance = r; }

    static void Burst(ParticleSystem ps, int n, float time = 0f)
    {
        var em = ps.emission;
        var list = new ParticleSystem.Burst[em.burstCount + 1];
        em.GetBursts(list);
        list[em.burstCount] = new ParticleSystem.Burst(time, (short)n);
        em.SetBursts(list);
        var m = ps.main; if (m.maxParticles < n * 2) m.maxParticles = n * 2;
    }
    static void Rate(ParticleSystem ps, float r) { var em = ps.emission; em.rateOverTime = r; }
    static void Life(ParticleSystem ps, float a, float b = -1) { var m = ps.main; m.startLifetime = b < 0 ? new ParticleSystem.MinMaxCurve(a) : new ParticleSystem.MinMaxCurve(a, b); }
    static void Size(ParticleSystem ps, float a, float b = -1) { var m = ps.main; m.startSize3D = false; m.startSize = b < 0 ? new ParticleSystem.MinMaxCurve(a) : new ParticleSystem.MinMaxCurve(a, b); }
    static void Size3(ParticleSystem ps, float x, float y) { var m = ps.main; m.startSize3D = true; m.startSizeX = x; m.startSizeY = y; m.startSizeZ = 1f; }
    static void Speed(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(a, b); }
    static void Gravity(ParticleSystem ps, float g) { var m = ps.main; m.gravityModifier = g; }
    static void Delay(ParticleSystem ps, float d) { var m = ps.main; m.startDelay = d; }
    static void World(ParticleSystem ps) { var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; }
    static void Rot(ParticleSystem ps, float degA, float degB) { var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(degA * Mathf.Deg2Rad, degB * Mathf.Deg2Rad); }
    static void RandRot(ParticleSystem ps) => Rot(ps, 0f, 360f);
    static void Pos(ParticleSystem ps, float x, float y) { ps.transform.localPosition = new Vector3(x, y, 0); }

    static void Spin(ParticleSystem ps, float degPerSec) => Spin(ps, -degPerSec, degPerSec);
    static void Spin(ParticleSystem ps, float a, float b)
    {
        var rl = ps.rotationOverLifetime; rl.enabled = true;
        rl.z = new ParticleSystem.MinMaxCurve(a * Mathf.Deg2Rad, b * Mathf.Deg2Rad);
    }

    static void Cone(ParticleSystem ps, float angle, float radius, Vector3 rot)
    {
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle; sh.radius = Mathf.Max(.0001f, radius); sh.radiusThickness = 1f; sh.rotation = rot;
    }
    static void SphereShape(ParticleSystem ps, float radius)
    {
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius; sh.radiusThickness = 0f; // 표면에서만 (안으로 빨려드는 입자가 고르게)
    }
    static void CircleShape(ParticleSystem ps, float radius)
    {
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = radius; sh.radiusThickness = 0f; sh.rotation = Vector3.zero; // XY 평면(화면)
    }

    static void Stretch(ParticleSystem ps, float velScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = velScale; r.lengthScale = 1.5f;
    }
    static void Drag(ParticleSystem ps, float dampen)
    {
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 0f; lv.dampen = dampen;
    }
    static void Noise(ParticleSystem ps, float strength, float freq, float scroll = .5f)
    {
        var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = scroll;
        n.quality = ParticleSystemNoiseQuality.Medium; n.damping = true;
    }
    static void Flipbook(ParticleSystem ps)
    {
        var ts = ps.textureSheetAnimation; ts.enabled = true;
        ts.mode = ParticleSystemAnimationMode.Grid; ts.numTilesX = 4; ts.numTilesY = 4;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
        ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 3f / 16f); // 시작 칸을 조금씩 다르게
        ts.cycleCount = 1;
    }

    // 크기 곡선 (시간, 값) 쌍
    static void SizeCurve(ParticleSystem ps, params float[] tv)
    {
        var so = ps.sizeOverLifetime; so.enabled = true; so.separateAxes = false;
        so.size = new ParticleSystem.MinMaxCurve(1f, Curve(tv));
    }
    static void Grow(ParticleSystem ps, float from, float to, bool easeOut = false)
    {
        // 값은 시작 크기의 배수. 곡선은 0~1 범위만 쓰므로 배수를 곱해서 넣는다.
        var so = ps.sizeOverLifetime; so.enabled = true; so.separateAxes = false;
        so.size = new ParticleSystem.MinMaxCurve(to, GrowCurve(from / to, easeOut));
    }
    static void GrowXY(ParticleSystem ps, float from, float to, bool easeOut = true)
    {
        var so = ps.sizeOverLifetime; so.enabled = true; so.separateAxes = true;
        var c = GrowCurve(from / to, easeOut);
        so.x = new ParticleSystem.MinMaxCurve(to, c);
        so.y = new ParticleSystem.MinMaxCurve(to, c);
        so.z = new ParticleSystem.MinMaxCurve(1f);
    }
    static void Shrink(ParticleSystem ps) => SizeCurve(ps, 0, 1f, 1f, 0f);

    static AnimationCurve GrowCurve(float start, bool easeOut)
    {
        if (!easeOut) return AnimationCurve.Linear(0, start, 1, 1);
        var a = new Keyframe(0, start, 3f * (1 - start), 3f * (1 - start));
        var b = new Keyframe(1, 1, 0, 0);
        return new AnimationCurve(a, b);
    }
    static AnimationCurve Curve(float[] tv)
    {
        var keys = new Keyframe[tv.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(tv[i * 2], tv[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
        for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
        return c;
    }

    // ───── 색 ─────
    static void Col(ParticleSystem ps, Color c, float alpha = 1f)
    {
        var m = ps.main; c.a = alpha; m.startColor = c;
    }
    static void SetGradient(ParticleSystem ps, Gradient g)
    {
        var col = ps.colorOverLifetime; col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(g);
    }
    static Gradient Grad(Color[] cols, float[] ct, float[] at, float[] av)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i], ct[i]);
        var ak = new GradientAlphaKey[at.Length];
        for (int i = 0; i < at.Length; i++) ak[i] = new GradientAlphaKey(av[i], at[i]);
        g.SetKeys(ck, ak);
        return g;
    }
    // 시작색 위에 곱해지므로, 시작색을 흰색으로 두고 그라데이션에서 색을 정한다.
    static float CurA(ParticleSystem ps) => ps.main.startColor.color.a;
    static void FadeOut(ParticleSystem ps, Color from, Color to)
    {
        Col(ps, Color.white, CurA(ps));
        SetGradient(ps, Grad(new[] { from, to }, new[] { 0f, 1f }, new[] { 0f, .5f, 1f }, new[] { 1f, .7f, 0f }));
    }
    static void FadeLate(ParticleSystem ps, Color from, Color to)
    {
        Col(ps, Color.white, CurA(ps));
        SetGradient(ps, Grad(new[] { from, to }, new[] { 0f, 1f }, new[] { 0f, .75f, 1f }, new[] { 1f, 1f, 0f }));
    }
    static void FadeInOut(ParticleSystem ps, Color c, float inT = .2f, float outT = .7f)
    {
        Col(ps, Color.white, CurA(ps));
        SetGradient(ps, Grad(new[] { c, c }, new[] { 0f, 1f }, new[] { 0f, inT, outT, 1f }, new[] { 0f, 1f, 1f, 0f }));
    }
    static void FireColor(ParticleSystem ps, Pal p)
    {
        Col(ps, Color.white, 1f);
        SetGradient(ps, Grad(new[] { p.hot, p.main, p.dark }, new[] { 0f, .35f, 1f },
                             new[] { 0f, .08f, .6f, 1f }, new[] { 0f, 1f, .8f, 0f }));
    }
    static void SmokeColor(ParticleSystem ps, Pal p, float alpha)
    {
        Col(ps, Color.white, 1f);
        var lit = Color.Lerp(p.smoke, p.main, .25f);
        SetGradient(ps, Grad(new[] { lit, p.smoke }, new[] { 0f, 1f },
                             new[] { 0f, .15f, 1f }, new[] { 0f, alpha, 0f }));
    }

    // 지연 포함 전체 길이
    static float TotalTime(GameObject root)
    {
        float t = 0f;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            var m = ps.main;
            float end = m.startDelay.constantMax + m.duration + m.startLifetime.constantMax;
            if (end > t) t = end;
        }
        return t;
    }

    // ═════════════════════════ 그림 · 재질 ═════════════════════════

    static readonly string[] TexNames =
        { "T_Lib_Glow", "T_Lib_Dot", "T_Lib_Flare", "T_Lib_Ring", "T_Lib_ShockRing", "T_Lib_FlameSheet", "T_Lib_Smoke", "T_Lib_Shard" };

    static bool SetupTextures()
    {
        foreach (var n in TexNames)
        {
            string path = TexDir + "/" + n + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                EditorUtility.DisplayDialog("이펙트 모음", "그림이 없습니다:\n" + path, "확인");
                return false;
            }
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
            if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
            if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; dirty = true; }
            if (imp.alphaSource != TextureImporterAlphaSource.FromInput) { imp.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
            if (dirty) imp.SaveAndReimport();
        }
        return true;
    }

    static void SetupMaterials()
    {
        mGlow  = Mat("M_Lib_Glow",      "T_Lib_Glow",       true, 1.6f);
        mDot   = Mat("M_Lib_Dot",       "T_Lib_Dot",        true, 2.2f);
        mFlare = Mat("M_Lib_Flare",     "T_Lib_Flare",      true, 2.5f);
        mRing  = Mat("M_Lib_Ring",      "T_Lib_Ring",       true, 1.8f);
        mShock = Mat("M_Lib_ShockRing", "T_Lib_ShockRing",  true, 1.4f);
        mFlame = Mat("M_Lib_Flame",     "T_Lib_FlameSheet", true, 1.8f);
        mShard = Mat("M_Lib_Shard",     "T_Lib_Shard",      true, 1.8f);
        mSmoke = Mat("M_Lib_Smoke",     "T_Lib_Smoke",      false, 1f);
        AssetDatabase.SaveAssets();
    }

    static Material Mat(string name, string tex, bool additive, float intensity)
    {
        string path = MatDir + "/" + name + ".mat";
        var src = AssetDatabase.LoadAssetAtPath<Material>(additive ? SrcAdditive : SrcAlpha);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = src != null ? new Material(src) : NewParticleMat(additive);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (src != null) m.CopyPropertiesFromMaterial(src);

        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + tex + ".png");
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
        // 원본 재질에 켜져 있을 수 있는 부가 기능 끄기
        if (m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 0f);
        if (m.HasProperty("_FlipbookBlending")) m.SetFloat("_FlipbookBlending", 0f);
        if (m.HasProperty("_CameraFadingEnabled")) m.SetFloat("_CameraFadingEnabled", 0f);
        if (m.HasProperty("_DistortionEnabled")) m.SetFloat("_DistortionEnabled", 0f);
        if (m.HasProperty("_ColorMode")) m.SetFloat("_ColorMode", 0f);
        m.DisableKeyword("_SOFTPARTICLES_ON"); m.DisableKeyword("_FLIPBOOKBLENDING_ON");
        m.DisableKeyword("_FADING_ON"); m.DisableKeyword("_DISTORTION_ON");
        m.DisableKeyword("_COLOROVERLAY_ON"); m.DisableKeyword("_COLORCOLOR_ON"); m.DisableKeyword("_COLORADDSUBDIFF_ON");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 원본 재질을 못 찾을 때의 대비책: URP 파티클 무광 셰이더로 직접 만든다
    static Material NewParticleMat(bool additive)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", 1f);
        m.SetFloat("_DstBlendAlpha", additive ? 1f : 10f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // ═════════════════════════ 저장 ═════════════════════════

    static void SavePrefab(GameObject root, string path)
    {
        if (File.Exists(path))
        {
            string bak = Path.Combine(BakDir, path.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(bak));
            File.Copy(path, bak, true);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
