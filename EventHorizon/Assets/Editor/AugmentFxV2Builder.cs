using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [2026-11-26] 이펙트 시안 v2 (통과한 9종) 프리팹 · 재질 만들기 — 에디터 전용.
///  기준서(이펙트_제작_기준서.md) 방식: 흑백 텍스처 × HDR 배수 재질(흰색 공유) + 입자 색 = 색 비율 (FxTint),
///  밝은 층(가산) 밑에 같은 모양 어두운 층(반투명), 아지랑이는 sword_hit 의 trail_distort 복제.
///  재질 템플릿: 가산 = M_ShieldShard, 반투명 = trail_light (팀 재질 복사, 원본 그대로)
///  만드는 것
///   Etc/Resources/AugmentFxV2/  FX2_HitSparks · FX2_Burst · FX2_Shatter · FX2_Pierce · FX2_Mark · FX2_FireFloor
///   Etc/Resources/KeywordFx/    KW2_Fracture_Node · KW2_Sheath_Hit
///   KeywordFx/KW2_Gas_Cloud — [2026-11-27] 흡입식 강화제 JJW 가스 구름 (만화풍 2톤 연기 뭉치 · 모양 섞임 · 과열 박자)
///   KeywordFx/KW2_Heat_Wave — [2026-11-27] 가열 블레이드 불타는 검기 (만화풍 3톤 초승달 + 노이즈 소멸 · 잔상 · 불티, 셰이더 ToonFireCrescent)
///   KeywordFx/KW2_Beat_Cone — [2026-11-27] 박자 칼날: 앞으로 쏘는 원뿔 에너지 (아이템 획득 시안의 에너지 기둥 방식, FxConeDriver)
///   1.Prefab/Effect/VFX/FX_CoreBreak_Slash · Pierce · Strike — 같은 파일 내용 교체 (CoreBreakFx · 깃펜 Symbol 유지, 보스방 연결 그대로)
///   재질 4. Art/Materials/VFX/AugmentV2/
///  메뉴 EventHorizon/이펙트 v2 만들기 (프로젝트 폴더에 fx_v2.trigger 가 있으면 자동 1회). 다시 실행하면 덮어씀
/// </summary>
[InitializeOnLoad]
public static class AugmentFxV2Builder
{
    const string Trigger = "fx_v2.trigger";
    const string OutDir = "Assets/Etc/Resources/AugmentFxV2";
    const string KwDir = "Assets/Etc/Resources/KeywordFx";
    const string MatDir = "Assets/4. Art/Materials/VFX/AugmentV2";
    const string AddTemplate = "Assets/4. Art/Materials/VFX/Skill/M_ShieldShard.mat";
    const string AlphaTemplate = "Assets/4. Art/Materials/VFX/Skill/trail_light.mat";
    const string DistortSrc = "Assets/1.Prefab/Effect/Combat/sword_hit.prefab";
    const string CoreDir = "Assets/1.Prefab/Effect/VFX";

    // 텍스처
    const string TFlash = "Assets/4. Art/Textures/VFX/Enemy/T_RicoFlash.png";
    const string TRays = "Assets/4. Art/Textures/VFX/Enemy/T_RicoRays.png";
    const string TRing = "Assets/4. Art/Textures/VFX/Skill/T_ShieldHitRing.png";
    const string TDot = "Assets/4. Art/Textures/Shader/light11.png";
    const string TKira = "Assets/4. Art/Textures/Shader/light12.jpg";
    const string TSoft = "Assets/4. Art/Textures/VFX/Pack/Circle_Soft.png";
    const string TSmoke = "Assets/4. Art/Textures/VFX/Player/T_SoftSmokePuff.png";
    const string TShard = "Assets/4. Art/Textures/VFX/Skill/T_ShieldShard.png";
    const string TSlash = "Assets/4. Art/Textures/Shader/sword_hit4.png";
    const string TRay = "Assets/4. Art/Textures/VFX/Enemy/T_SnipeRay.png";
    const string TSnDot = "Assets/4. Art/Textures/VFX/Enemy/T_SnipeDot.png";
    const string TStar = "Assets/4. Art/Textures/VFX/Common/light4.png";
    const string TCurtain = "Assets/4. Art/Textures/VFX/Enemy/ringlight.png";
    const string THit = "Assets/4. Art/Textures/VFX/Common/hit.png";
    const string TCrack = "Assets/4. Art/Textures/VFX/Portal/T_PortalShard.png";

    const float AddGain = 1f;   // [2026-11-26] 밝은 층 재질 배수 전체 보정 (1 = 시안 값 그대로)
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static GameObject distortSrc;
    static int order;

    static AugmentFxV2Builder() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Trigger)) { EditorApplication.update -= Poll; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Poll;
        File.Delete(Trigger);
        Build();
    }

    [MenuItem("EventHorizon/이펙트 v2 만들기")]
    public static void Build()
    {
        mats.Clear();
        foreach (var d in new[] { OutDir, MatDir }) if (!AssetDatabase.IsValidFolder(d)) Directory.CreateDirectory(d);
        AssetDatabase.Refresh();
        var dsrc = PrefabUtility.LoadPrefabContents(DistortSrc);
        try
        {
            distortSrc = FindChild(dsrc.transform, "trail_distort")?.gameObject;
            if (distortSrc == null) Debug.LogWarning("[이펙트 v2] sword_hit 안에서 trail_distort 를 못 찾음 — 아지랑이 없이 만듦");
            Save(HitSparks(), OutDir + "/FX2_HitSparks.prefab");
            Save(Burst(), OutDir + "/FX2_Burst.prefab");
            Save(Shatter(), OutDir + "/FX2_Shatter.prefab");
            Save(Pierce(), OutDir + "/FX2_Pierce.prefab");
            Save(Mark(), OutDir + "/FX2_Mark.prefab");
            Save(FireFloor(), OutDir + "/FX2_FireFloor.prefab");
            Save(FractureNode(), KwDir + "/KW2_Fracture_Node.prefab");
            Save(SheathHit(), KwDir + "/KW2_Sheath_Hit.prefab");
            CoreBreak("Slash", Hex("E39A4B"), 0.15f);
            CoreBreak("Pierce", Hex("6FA8DC"), 0.15f);
            CoreBreak("Strike", Hex("E8542A"), 0.3f);
            BeatCone();
            HeatWave();
            GasCloudFx();
        }
        finally { PrefabUtility.UnloadPrefabContents(dsrc); distortSrc = null; }
        AssetDatabase.SaveAssets();
        Debug.Log("[이펙트 v2] 프리팹 12개 완료 (AugmentFxV2 6 · KeywordFx 3 · 코어 파괴 3)");
    }

    // ════════════════════ 1. 불티 — 적을 벨 때 ════════════════════
    static GameObject HitSparks()
    {
        var root = Root("FX2_HitSparks", 0, 1.0f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        top.Add(Flash(root, "flash", 0.9f, 0f, 0.08f, 7f, false));
        // 큰 불티 — 앞쪽(+x)으로
        top.Add(Sparks(root, "sparks_big", 6, new Vector2(9, 14), new Vector2(.3f, .45f), new Vector2(.2f, .3f), 0.25f, 9f, cone: 40f, mult: 5f));
        // 잔 불티 — 사방 + 떨어짐
        top.Add(Sparks(root, "sparks_small", 16, new Vector2(4, 9), new Vector2(.12f, .22f), new Vector2(.3f, .6f), 0.8f, 7f, mult: 5f));
        top.Add(Glints(root, "glints", 4, 0.5f, 0.06f, 0.35f, 0.3f));
        Distort(root, 0.4f);
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 2. 터짐 — 과열 5개 처치 ════════════════════
    static GameObject Burst()
    {
        var root = Root("FX2_Burst", 0, 2.0f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        // 0) 빨려듦
        var imp = Layer(root, "implode", Mat("soft_add", TSoft, true, 3f), 1, 0.06f, 1.6f, 0f);
        SizeCurve(imp, Key(0, 1f), Key(1, 0.4f)); AlphaCurve(imp, Key(0, 0f), Key(1, 1f)); top.Add(imp);
        // 4) 어두운 연기 (가장 늦게 사라짐) + 밝은 테두리
        dark.Add(Smoke(root, "smoke_dark", 7, 0.06f, 1.1f, new Vector2(1.3f, 2.3f), 0.5f, false, 11));
        top.Add(Smoke(root, "smoke_rim", 7, 0.06f, 0.55f, new Vector2(0.8f, 1.4f), 0.5f, true, 11));
        // 1) 섬광 + 빛줄기
        top.Add(Flash(root, "flash", 2.2f, 0.05f, 0.2f, 7f, true));
        // 2) 충격 고리 + 바닥 고리
        RingPair(root, "ring", 4.5f, 0.07f, 0.45f, 1f, top, dark);
        RingPair(root, "ring_floor", 5.8f, 0.1f, 0.5f, 0.22f, top, dark, yOffset: -0.6f);
        // 3) 불티 · 5) 떠오르는 불씨
        top.Add(Sparks(root, "sparks", 14, new Vector2(8, 15), new Vector2(.22f, .4f), new Vector2(.35f, .6f), 1.0f, 7f, delay: 0.06f, mult: 5f));
        top.Add(Glints(root, "embers", 6, 1.1f, 0.2f, 0.6f, 0.8f));
        Distort(root, 1.6f);
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 3. 파편 — 방패 깨짐 ════════════════════
    static GameObject Shatter()
    {
        var root = Root("FX2_Shatter", 0, 1.2f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        var edge = Layer(root, "edge", Mat("ring_add", TRing, true, 3f), 1, 0.1f, 1.4f, 0f);
        AlphaCurve(edge, Key(0, 1f), Key(1, 0f)); top.Add(edge);
        top.Add(Flash(root, "flash", 1.4f, 0f, 0.12f, 6f, true));
        // 파편: 어두운 층과 밝은 층을 같은 무작위 씨앗으로 (같은 자리 · 같은 회전)
        dark.Add(Shards(root, "shards_dark", 11, 1.15f, false, 21));
        top.Add(Shards(root, "shards", 11, 1f, true, 21));
        top.Add(Sparks(root, "sparks", 10, new Vector2(6, 11), new Vector2(.14f, .26f), new Vector2(.2f, .4f), 0.3f, 7f, mult: 5f));
        RingPair(root, "ring", 2.2f, 0.02f, 0.3f, 1f, top, dark);
        Distort(root, 0.8f);
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 4. 선 — 관통 돌진 (길이 1 = 루트 x 배율로 늘림, 앞끝 = +x) ════════════════════
    static GameObject Pierce()
    {
        var root = Root("FX2_Pierce", 0, 0.9f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);   // 코드가 x 배율 = 길이로 늘림 (불티 · 끝 번쩍은 안 늘어나게 따로)
        root = body.transform.parent.gameObject;
        var R0 = root; root = body;
        // 띠: sword_hit4 (양 끝이 뾰족한 칼자국) 를 길게 — 루트 기준 가운데 0.5
        var bd = Layer(root, "band_dark", Mat("slash_alpha", TSlash, false, 1f), 1, 0.5f, 1f, 0f, local: true);
        Size3(bd, 1.04f, 0.9f); bd.transform.localPosition = new Vector3(0.5f, 0, 0);
        AlphaCurve(bd, Key(0, .95f), Key(.6f, .7f), Key(1, 0f)); dark.Add(bd);
        // 밝은 띠: sword_hit4 는 속이 검은(가장자리만 흰) 그림이라 가산으로는 테두리만 보임 → 부드러운 원을 길게 늘인 타원 (양 끝이 가늘게 모임)
        var b = Layer(root, "band", Mat("soft_add35", TSoft, true, 3.5f), 1, 0.45f, 1f, 0f, local: true);
        Size3(b, 1.05f, 0.5f); b.transform.localPosition = new Vector3(0.55f, 0, 0);
        AlphaCurve(b, Key(0, 1f), Key(.5f, .6f), Key(1, 0f)); SizeCurve3(b, 1f, Key(0, .6f), Key(.15f, 1f), Key(1, .3f)); top.Add(b);
        // 하얀 심 (가는 광선)
        var core = Layer(root, "core", Mat("ray_add", TRay, true, 12f), 1, 0.3f, 1f, 0f, local: true);
        Size3(core, 0.95f, 0.12f); core.transform.localPosition = new Vector3(0.55f, 0, 0);
        AlphaCurve(core, Key(0, 1f), Key(1, 0f)); top.Add(core);
        // 지나가는 가는 선
        var lines = Layer(root, "speedlines", Mat("dot_add", TDot, true, 4f), 7, 0.25f, 0.06f, 0f, local: true);
        Size3(lines, 0.35f, 0.04f); var sh = lines.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1f, 0.6f, 0); sh.position = new Vector3(0.5f, 0, 0);
        var m = lines.main; m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f); var vel = lines.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.x = new ParticleSystem.MinMaxCurve(3f, 5f); vel.y = 0f; vel.z = 0f;
        sh.randomDirectionAmount = 0f; AlphaCurve(lines, Key(0, .8f), Key(1, 0f)); top.Add(lines);
        // 앞끝 번쩍 · 불티 (코드가 tip 을 끝점으로 옮김)
        root = R0;
        var tip = new GameObject("tip"); tip.transform.SetParent(root.transform, false); tip.transform.localPosition = new Vector3(1f, 0, 0);
        top.Add(Flash(tip, "flash", 1.0f, 0.04f, 0.1f, 6f, false));
        top.Add(Sparks(tip, "sparks", 10, new Vector2(6, 12), new Vector2(.15f, .28f), new Vector2(.2f, .35f), 0.3f, 7f, delay: 0.04f, cone: 35f, mult: 5f));
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 5. 표식 — 약점 · 처형 대상 (반복) ════════════════════
    static GameObject Mark()
    {
        var root = Root("FX2_Mark", 5, 0f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        dark.Add(Hold(root, "back", Mat("sndot_alpha", TSnDot, false, 1f), 1.4f, .85f));
        top.Add(Hold(root, "ring", Mat("ring_add", TRing, true, 3.5f), 0.7f, 1f));
        top.Add(Hold(root, "ring_in", Mat("ring_add2", TRing, true, 2f), 0.45f, .6f));
        var spin = new GameObject("Spin"); spin.transform.SetParent(root.transform, false);
        for (int i = 0; i < 4; i++)
        {
            var tick = Hold(spin, "tick" + i, Mat("dot_add6", TDot, true, 6f), 0.35f, 1f, local: true);
            Size3(tick, 0.35f, 0.07f);
            float a = i * 90f * Mathf.Deg2Rad; tick.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.5f;
            var mm = tick.main; mm.startRotation = -a; top.Add(tick);
        }
        var star = Hold(spin, "star", Mat("star_add", TStar, true, 7f), 0.9f, 1f, local: true); top.Add(star);
        top.Add(Hold(root, "core", Mat("sndot_add", TSnDot, true, 5f), 0.55f, 1f));
        var fl = Flash(root, "appear", 1.2f, 0f, 0.12f, 6f, false); top.Add(fl);
        var mo = root.AddComponent<FxMarkMotion>(); mo.spin = spin.transform;
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 6. 바닥 — 불 줄이고 불씨 (폭 3.2, 루트 x 배율로 맞춤, 원점 = 발밑) ════════════════════
    static GameObject FireFloor()
    {
        var root = Root("FX2_FireFloor", -20, 0f, out var tint, sortingLayer: "근경2D");
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        var scorch = Loop(root, "scorch", Mat("sndot_alpha", TSnDot, false, 1f), 0.6f, 4f, 1.5f); Size3(scorch, 4.0f, 0.55f); AlphaCurve(scorch, Key(0, 0f), Key(.3f, .9f), Key(.7f, .9f), Key(1, 0f)); dark.Add(scorch);
        var line = Loop(root, "glow_line", Mat("soft_add22", TSoft, true, 2.2f), 0.3f, 8f, 0.3f); Size3(line, 3.4f, 0.22f); AlphaCurve(line, Key(0, 0f), Key(.3f, 1f), Key(1, 0f)); top.Add(line);
        // 불꽃 결: ringlight 한 장을 길게 늘이면 양 끝이 잘려 보여, 짧은 조각을 줄 따라 흩어 뿌림 (조각마다 나타났다 사라짐)
        var cur = Loop(root, "glow_curtain", Mat("curtain_add", TCurtain, true, 1.6f), 0.5f, 16f, 0.4f); Size3(cur, 1f, 0.25f); cur.transform.localPosition = new Vector3(0, 0.12f, 0);
        { var cm0 = cur.main; cm0.startSizeX = new ParticleSystem.MinMaxCurve(0.7f, 1.2f); var csh = cur.shape; csh.enabled = true; csh.shapeType = ParticleSystemShapeType.Box; csh.scale = new Vector3(2.4f, 0f, 0f); }
        var cm = cur.main; cm.startRotation = Mathf.PI; AlphaCurve(cur, Key(0, 0f), Key(.35f, .6f), Key(1, 0f)); top.Add(cur);
        // 올라오는 불씨: 흔들리며 위로, 식어 사라짐
        var emb = Loop(root, "embers", Mat("dot_add8", TDot, true, 8f), 0f, 18f, 0f);
        var em = emb.main; em.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f); em.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.0f); em.maxParticles = 60;
        Size3(emb, 0.1f, 0.24f); em.startSizeXMultiplier = 1f; em.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); em.startSizeY = new ParticleSystem.MinMaxCurve(0.2f, 0.36f);
        var es = emb.shape; es.enabled = true; es.shapeType = ParticleSystemShapeType.Box; es.scale = new Vector3(3.2f, 0.05f, 0); es.rotation = Vector3.zero;
        var ev = emb.velocityOverLifetime; ev.enabled = true; ev.space = ParticleSystemSimulationSpace.Local; ev.x = 0; ev.y = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); ev.z = 0;
        var en = emb.noise; en.enabled = true; en.strength = 0.35f; en.frequency = 1.2f; en.scrollSpeed = 0.6f;
        var eg = new Gradient(); eg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.35f, .35f, .35f), 1f) },
                                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0f, 1f) });
        var ec = emb.colorOverLifetime; ec.enabled = true; ec.color = eg;
        var esh = emb.GetComponent<ParticleSystemRenderer>(); esh.renderMode = ParticleSystemRenderMode.Billboard;
        top.Add(emb);
        // 가끔 튀는 불티
        var pop = Sparks(root, "pops", 3, new Vector2(3, 5), new Vector2(.1f, .16f), new Vector2(.2f, .3f), 0.8f, 5f, cone: 30f, up: true, mult: 6f);
        var pm = pop.main; pm.loop = true; pm.duration = 0.9f; var pe = pop.emission; pe.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });
        var ps2 = pop.shape; ps2.position = Vector3.zero; top.Add(pop);
        // 아주 옅은 연기
        var sm = Loop(root, "smoke", Mat("smoke_alpha", TSmoke, false, 1f), 2.0f, 1.2f, 1.2f); sm.transform.localPosition = new Vector3(0, 0.6f, 0);
        var smm = sm.main; smm.startSpeed = 0.4f; AlphaCurve(sm, Key(0, 0f), Key(.3f, .18f), Key(1, 0f)); var sms = sm.shape; sms.enabled = true; sms.shapeType = ParticleSystemShapeType.Box; sms.scale = new Vector3(2.6f, 0.2f, 0);
        dark.Add(sm);
        var dist = Distort(root, 1.2f); if (dist != null) { foreach (var p in dist.GetComponentsInChildren<ParticleSystem>(true)) { var mm = p.main; mm.loop = true; } dist.transform.localPosition = new Vector3(0, 0.8f, 0); }
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        return root;
    }

    // ════════════════════ 7. 균열 — 금이 지나는 자리 하나 (코드가 0.04초 간격으로 5번 놓음) ════════════════════
    static GameObject FractureNode()
    {
        var root = Root("KW2_Fracture_Node", -20, 1.2f, out var tint, sortingLayer: "근경2D");
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        var crack = Layer(root, "crack", Mat("crack_add", TCrack, true, 1.5f), 1, 1.0f, 1.5f, 0f); Size3(crack, 1.6f, 0.4f);
        AlphaCurve(crack, Key(0, 0f), Key(.1f, .5f), Key(.7f, .5f), Key(1, 0f)); top.Add(crack);
        // 틈에서 솟는 빛 기둥 (어두운 층 + 밝은 층 + 하얀 줄)
        var cd = Layer(root, "pillar_dark", Mat("sndot_alpha", TSnDot, false, 1f), 1, 0.35f, 1f, 0f); Size3(cd, 0.7f, 1.6f); cd.transform.localPosition = new Vector3(0, 0.7f, 0);
        SizeCurve3(cd, 1f, Key(0, .4f), Key(1, 1f)); AlphaCurve(cd, Key(0, .8f), Key(1, 0f)); dark.Add(cd);
        var cb = Layer(root, "pillar", Mat("soft_add4", TSoft, true, 4f), 1, 0.35f, 1f, 0f); Size3(cb, 0.38f, 1.6f); cb.transform.localPosition = new Vector3(0, 0.7f, 0);
        SizeCurve3(cb, 1f, Key(0, .4f), Key(1, 1f)); AlphaCurve(cb, Key(0, 1f), Key(1, 0f)); top.Add(cb);
        var cl = Layer(root, "pillar_line", Mat("dot_add8", TDot, true, 8f), 1, 0.3f, 1f, 0f, local: true); Size3(cl, 1.7f, 0.09f); cl.transform.localPosition = new Vector3(0, 0.8f, 0);
        var clm = cl.main; clm.startRotation = Mathf.PI / 2f; AlphaCurve(cl, Key(0, 1f), Key(1, 0f)); top.Add(cl);
        top.Add(Flash(root, "flash", 0.7f, 0f, 0.1f, 6f, false));
        top.Add(Sparks(root, "sparks", 4, new Vector2(3, 6), new Vector2(.1f, .2f), new Vector2(.25f, .4f), 1.2f, 5f, cone: 25f, up: true, mult: 5f));
        dark.Add(Shards(root, "shards_dark", 2, 0.36f, false, 41, up: true));
        top.Add(Shards(root, "shards", 2, 0.32f, true, 41, up: true));
        top.Add(Glints(root, "glints", 2, 0.4f, 0.15f, 0.5f, 0.7f));
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        tint.baseColor = Hex("9B7BFF"); ApplyBase(tint);
        return root;
    }

    // ════════════════════ 9. 보급 단검용 검집 ════════════════════
    static GameObject SheathHit()
    {
        var root = Root("KW2_Sheath_Hit", 0, 0f, out var tint);
        var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
        var sd = Layer(root, "slash_dark", Mat("slash_alpha", TSlash, false, 1f), 1, 0.15f, 1f, 0f, local: true); Size3(sd, 2.6f, 0.35f);
        var sdm = sd.main; sdm.startRotation = 28f * Mathf.Deg2Rad; AlphaCurve(sd, Key(0, .9f), Key(1, 0f)); dark.Add(sd);
        var s = Layer(root, "slash", Mat("soft_add5", TSoft, true, 5f), 1, 0.15f, 1f, 0f, local: true); Size3(s, 2.4f, 0.2f);   // sword_hit4 는 속이 검어 가산이면 테두리만 보여 타원으로
        var sm = s.main; sm.startRotation = 28f * Mathf.Deg2Rad; AlphaCurve(s, Key(0, 1f), Key(1, 0f)); top.Add(s);
        var hd = Layer(root, "hit_dark", Mat("hit_alpha", THit, false, 1f), 1, 0.3f, 2.3f, 0f); Sheet(hd, 3, 1, false); AlphaCurve(hd, Key(0, .8f), Key(1, .4f)); dark.Add(hd);
        var h = Layer(root, "hit", Mat("hit_add", THit, true, 7f), 1, 0.3f, 2.2f, 0f); Sheet(h, 3, 1, false); AlphaCurve(h, Key(0, 1f), Key(1, 1f)); top.Add(h);
        var hm = h.main; hm.startRotation = 20f * Mathf.Deg2Rad; var hdm = hd.main; hdm.startRotation = 20f * Mathf.Deg2Rad;
        top.Add(Flash(root, "flash", 0.9f, 0f, 0.08f, 7f, false));
        top.Add(Sparks(root, "sparks", 9, new Vector2(7, 12), new Vector2(.13f, .24f), new Vector2(.18f, .32f), 0.3f, 8f, cone: 50f, mult: 5f));
        top.Add(Glints(root, "glints", 3, 0.5f, 0.08f, 0.3f, 0.25f));
        tint.top = top.ToArray(); tint.dark = dark.ToArray();
        tint.baseColor = Hex("5999FF"); ApplyBase(tint);
        return root;
    }

    // ════════════════════ 10. 박자 칼날 — 앞으로 쏘는 원뿔 에너지 ════════════════════
    //  [2026-11-27] 아이템 획득 시안의 에너지 기둥(strike_burst 셰이더)을 원뿔로 눕혀 창처럼 찌름 (밑동 = 손, 뾰족한 끝 = 앞).
    //  메시 = 건틀릿 강타 원뿔(strike.fbx), 밝은 층 = 소용돌이 무늬(yhsnoise1, 가산), 어두운 층 = 강타 무늬(strike_texture, 반투명)
    //  움직임(뻗음 · 밝기 · 소멸 · 흐름)은 FxConeDriver. 손 쪽 번쩍 + 앞으로 튀는 불티 + 끝의 반짝임
    const string ConeMesh = "Assets/4. Art/VFX/Skill/strike.fbx";
    const string ConeTopTemplate = "Assets/4. Art/Materials/VFX/Skill/strike_burst1_top.mat";
    const string ConeBaseTemplate = "Assets/4. Art/Materials/VFX/Skill/strike_burst1_base.mat";
    const string TSwirl = "Assets/4. Art/Textures/Shader/yhsnoise1.png";
    const string TStrike = "Assets/4. Art/Textures/Shader/strike_texture.png";
    static void BeatCone()
    {
        Mesh mesh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ConeMesh)) if (o is Mesh mm) { mesh = mm; break; }
        if (mesh == null) { Debug.LogWarning("[이펙트 v2] 원뿔 메시 없음: " + ConeMesh); return; }
        const float L = 4.2f, R = 0.55f;   // 길이 · 손 쪽 밑동 반지름 (월드)

        var root = Root("KW2_Beat_Cone", 0, 0f, out var tint);
        var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);

        // 메시의 긴 축 · 넓은 끝을 찾아, 넓은 끝(밑동) = 원점(손), 뾰족한 끝 = +x 로 눕힘 → 창처럼 찌름 (Body 안에서 길이 1)
        var b = mesh.bounds; var sz = b.size;
        int ax = sz.x >= sz.y && sz.x >= sz.z ? 0 : (sz.y >= sz.z ? 1 : 2);
        float lo = b.min[ax], hi = b.max[ax], span = hi - lo, rLo = 0f, rHi = 0f;
        foreach (var p in mesh.vertices)
        {
            var d = p - b.center; d[ax] = 0f; float r = d.magnitude;
            if (p[ax] < lo + span * 0.12f) rLo = Mathf.Max(rLo, r);
            if (p[ax] > hi - span * 0.12f) rHi = Mathf.Max(rHi, r);
        }
        bool wideAtLo = rLo > rHi; float rW = Mathf.Max(rLo, rHi, 0.0001f);
        Vector3 axis = Vector3.zero; axis[ax] = 1f;
        var q = Quaternion.FromToRotation(wideAtLo ? axis : -axis, Vector3.right);   // 넓은 끝 → 뾰족한 끝 = 앞
        Vector3 sc = Vector3.one * (R / rW); sc[ax] = 1f / span;
        Vector3 baseEnd = b.center; baseEnd[ax] = wideAtLo ? lo : hi;

        var topMat = ConeMat("cone_top", ConeTopTemplate, TSwirl);
        var baseMat = ConeMat("cone_base", ConeBaseTemplate, TStrike);
        Renderer MakeCone(string name, Material mat, int ord)
        {
            var go = new GameObject(name); go.transform.SetParent(body.transform, false);
            go.transform.localRotation = q; go.transform.localScale = sc;
            go.transform.localPosition = -(q * Vector3.Scale(baseEnd, sc));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = ord;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }
        var baseR = MakeCone("cone_base", baseMat, order++);
        var topR = MakeCone("cone_top", topMat, order++);

        var top = new List<ParticleSystem>();
        top.Add(Flash(root, "flash", 1.1f, 0f, 0.1f, 6f, false));                                     // 손 쪽 번쩍
        top.Add(Sparks(root, "sparks", 14, new Vector2(10, 16), new Vector2(.18f, .3f), new Vector2(.18f, .3f), 0.3f, 7f, cone: 22f, mult: 5f));   // 앞으로
        var gl = Glints(root, "glints", 3, 0.4f, 0.08f, 0.3f, 0.2f); gl.transform.localPosition = new Vector3(L * 0.97f, 0f, 0f); top.Add(gl);   // 창끝
        tint.top = top.ToArray(); tint.dark = new ParticleSystem[0];
        tint.baseColor = Hex("F2C230"); ApplyBase(tint);

        var drv = root.AddComponent<FxConeDriver>();
        drv.body = body.transform; drv.top = topR; drv.bas = baseR; drv.color = Hex("F2C230"); drv.length = L;
        drv.flow = -1f;   // 메시를 뒤집어 무늬 v 방향도 뒤집힘 → 그대로 손에서 창끝으로 흐르게
        body.transform.localScale = new Vector3(L, 1f, 1f);
        Save(root, KwDir + "/KW2_Beat_Cone.prefab");
    }

    /// <summary>strike_burst 재질 복사 (팀 원본은 그대로) + 무늬 텍스처만 바꿈. 색 · 소멸 · 흐름은 FxConeDriver 가 매 프레임 넣음</summary>
    static Material ConeMat(string name, string template, string tex)
    {
        string path = $"{MatDir}/M2_{name}.mat";
        var src = AssetDatabase.LoadAssetAtPath<Material>(template);
        if (src == null) { Debug.LogWarning("[이펙트 v2] 재질 템플릿 없음: " + template); return null; }
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        if (t != null && m.HasProperty("_swordtexture")) m.SetTexture("_swordtexture", t);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ════════════════════ 11. 가열 블레이드 — 불타는 검기 (코드가 루트를 옮김: SwordWave) ════════════════════
    //  [2026-11-27] 시안 "C 몸통 + A 방식 소멸" 확정:
    //   검무 초승달 메쉬(SM_Crescent)에 전용 셰이더 EventHorizon/FX/ToonFireCrescent —
    //   만화풍 3톤(앞날 하얀노랑 · 주황 · 뒷날 짙은 빨강), 뒷날은 불꽃 혀처럼 너울, 날아가는 중부터 꼬리 쪽이 잘게 녹아 사라지며 녹는 가장자리가 빛남.
    //   본체 + 잔상 2장(지나온 자리), 지나간 자리에서 불티가 흔들리며 솟음. 움직임 = FxToonFireDriver
    //   + 꼬리: 칼날 전체가 끌림 (높이마다 칼날 뒷면에서 시작 · 끝 높이 75% · 약 3m), 셰이더 EventHorizon/FX/ToonFireTail
    const string CrescentMesh = "Assets/4. Art/VFX/Common/Mesh/SM_Crescent.fbx";
    const string TEmberSheet = "Assets/4. Art/Textures/VFX/Pack/Tx_Ember_01.png";
    static void HeatWave()
    {
        Mesh mesh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(CrescentMesh)) if (o is Mesh mm) { mesh = mm; break; }
        var shader = Shader.Find("EventHorizon/FX/ToonFireCrescent");
        if (mesh == null || shader == null) { Debug.LogWarning("[이펙트 v2] 가열 블레이드: 메시 또는 셰이더 없음"); return; }
        const float HeightW = 2.0f, FrontX = 0.3f;   // 검기 높이 (월드, KFx 가 1.1배) · 앞날 끝 = 루트 + 0.3

        var root = Root("KW2_Heat_Wave", 0, 0f, out var tint);
        // 메시 방향 맞추기: 가장 얇은 축 = 화면 안쪽(z), 가장 긴 축 = 위(y), 볼록한 쪽 = 앞(+x)
        var b = mesh.bounds; var sz = b.size;
        int thin = sz.x <= sz.y && sz.x <= sz.z ? 0 : (sz.y <= sz.z ? 1 : 2);
        int tall = -1; for (int i = 0; i < 3; i++) if (i != thin && (tall < 0 || sz[i] > sz[tall])) tall = i;
        Vector3 thinV = Vector3.zero, tallV = Vector3.zero; thinV[thin] = 1f; tallV[tall] = 1f;
        var q = Quaternion.Inverse(Quaternion.LookRotation(thinV, tallV));
        float s = HeightW / sz[tall];
        var verts = mesh.vertices; var pts = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) pts[i] = q * ((verts[i] - b.center) * s);
        float ymax = 0f; foreach (var p in pts) ymax = Mathf.Max(ymax, Mathf.Abs(p.y));
        float tipX = 0f, midX = 0f; int tn = 0, mn = 0;
        foreach (var p in pts) { if (Mathf.Abs(p.y) > ymax * 0.85f) { tipX += p.x; tn++; } if (Mathf.Abs(p.y) < ymax * 0.15f) { midX += p.x; mn++; } }
        if (tn > 0 && mn > 0 && tipX / tn > midX / mn) { q = Quaternion.Euler(0f, 180f, 0f) * q; for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(-pts[i].x, pts[i].y, -pts[i].z); }
        float maxX = float.MinValue; foreach (var p in pts) maxX = Mathf.Max(maxX, p.x);
        Vector3 pos = new Vector3(FrontX - maxX, 0f, 0f) - q * (b.center * s);

        var mat = ToonFireMat(shader);
        // 꼬리: 칼날 뒷면 곡선을 높이 32칸으로 따서, 그 곡선에서 뒤로 뻗는 띠 메쉬 (앞 테두리가 칼날 뒷면과 정확히 맞음)
        var tailShader = Shader.Find("EventHorizon/FX/ToonFireTail");
        Renderer tailR = null;
        if (tailShader != null)
        {
            const int N = 32; const float TailLen = 3.2f;
            float ymin = float.MaxValue, ymax2 = float.MinValue;
            foreach (var p in pts) { ymin = Mathf.Min(ymin, p.y); ymax2 = Mathf.Max(ymax2, p.y); }
            float bw = (ymax2 - ymin) / (N - 1);
            var tv = new Vector3[N * 2]; var tuv = new Vector2[N * 2]; var tri = new List<int>();
            float lastBack = 0f;
            for (int i = 0; i < N; i++)
            {
                float y = Mathf.Lerp(ymin, ymax2, i / (float)(N - 1));
                float bx = float.MaxValue;
                foreach (var p in pts) if (Mathf.Abs(p.y - y) <= bw * 0.75f) bx = Mathf.Min(bx, p.x);
                if (bx == float.MaxValue) bx = lastBack; lastBack = bx;
                float x0 = bx + (FrontX - maxX) + 0.02f;   // 칼날 뒷면 바로 안쪽 (칼날이 덮음)
                tv[i * 2] = new Vector3(x0, y, 0f); tv[i * 2 + 1] = new Vector3(x0 - TailLen, y, 0f);
                float vv = i / (float)(N - 1); tuv[i * 2] = new Vector2(0f, vv); tuv[i * 2 + 1] = new Vector2(1f, vv);
                if (i > 0) { int a = (i - 1) * 2, c = i * 2; tri.AddRange(new[] { a, a + 1, c, c, a + 1, c + 1 }); }
            }
            string meshPath = $"{MatDir}/KW2_HeatTail_Mesh.asset";
            var tm = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (tm == null) { tm = new Mesh { name = "KW2_HeatTail_Mesh" }; AssetDatabase.CreateAsset(tm, meshPath); }
            tm.Clear(); tm.vertices = tv; tm.uv = tuv; tm.triangles = tri.ToArray(); tm.RecalculateBounds(); EditorUtility.SetDirty(tm);
            string tmPath = $"{MatDir}/M2_toonfire_tail.mat";
            var tmat = AssetDatabase.LoadAssetAtPath<Material>(tmPath);
            if (tmat == null) { tmat = new Material(tailShader); AssetDatabase.CreateAsset(tmat, tmPath); } else tmat.shader = tailShader;
            tmat.SetFloat("_Len", TailLen); tmat.renderQueue = 3000; EditorUtility.SetDirty(tmat);
            var tg = new GameObject("tail"); tg.transform.SetParent(root.transform, false);
            tg.AddComponent<MeshFilter>().sharedMesh = tm;
            var trr = tg.AddComponent<MeshRenderer>(); trr.sharedMaterial = tmat; trr.sortingOrder = order++;
            trr.shadowCastingMode = ShadowCastingMode.Off; trr.receiveShadows = false;
            tailR = trr;
        }
        else Debug.LogWarning("[이펙트 v2] 가열 블레이드: 꼬리 셰이더 없음 — 꼬리 없이 만듦");
        Renderer Blade(string name, int ord)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.transform.localRotation = q; go.transform.localScale = Vector3.one * s; go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = ord;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }
        var g2 = Blade("ghost2", order++); var g1 = Blade("ghost1", order++); var main = Blade("blade", order++);

        // 불티: 지나간 자리에서 흔들리며 솟다 식어 사라짐 (월드 공간 · 위로 오르는 힘은 월드 기준)
        var top = new List<ParticleSystem>();
        var em = Loop(root, "embers", Mat("ember_add6", TEmberSheet, true, 6f), 0.7f, 45f, 0.14f);
        { var m = em.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
          m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
          var sh = em.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0f, 1.6f, 0f);
          var v = em.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); v.y = new ParticleSystem.MinMaxCurve(1.0f, 2.4f); v.z = 0f;
          var n = em.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 1.5f; n.scrollSpeed = 0.8f;
          var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.4f, .4f, .4f), 1f) },
                                            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
          var col = em.colorOverLifetime; col.enabled = true; col.color = g; }
        Sheet(em, 2, 2, true); top.Add(em);
        tint.top = top.ToArray(); tint.dark = new ParticleSystem[0];
        tint.baseColor = Hex("E8542A"); ApplyBase(tint);

        var drv = root.AddComponent<FxToonFireDriver>();
        drv.main = main; drv.ghosts = new[] { g1, g2 }; drv.color = Hex("E8542A");
        drv.tail = tailR; drv.tailLife = 0.14f; drv.tailShrink = 0.25f;   // [2026-11-27] 꼬리 시안 "길게 3m · 끝 높이 75%"
        Save(root, KwDir + "/KW2_Heat_Wave.prefab");
    }

    static Material ToonFireMat(Shader shader)
    {
        string path = $"{MatDir}/M2_toonfire.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ════════════════════ 12. 흡입식 강화제 JJW · 가스 분사 — 대시 끝자리 가스 구름 ════════════════════
    //  [2026-11-27] 시안 "그을린 연기 유지형 + 뭉치 모양 섞음": 만화풍 2톤 검붉은 연기 뭉치(셰이더 ToonSmokePuff)가
    //  판정 상자(4.2 × 1.8) 안에서 계속 생겼다 끊기며 끓음. 처음 22개가 한꺼번에 피며 속이 달아오름, 0.5초 과열 박자마다 FxSmokePulse 로 달아오름, 불티
    static void GasCloudFx()
    {
        var shader = Shader.Find("EventHorizon/FX/ToonSmokePuff");
        if (shader == null) { Debug.LogWarning("[이펙트 v2] 가스 구름: 셰이더 없음"); return; }
        string mp = $"{MatDir}/M2_toonsmoke.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null)
        {
            mat = new Material(shader); AssetDatabase.CreateAsset(mat, mp);
            mat.SetFloat("_HotMult", 2f); mat.SetFloat("_HotBase", 0.025f); mat.SetFloat("_PulseAmt", 0f); mat.SetFloat("_FirstAmt", 0.15f);   // 처음 만들 때만 (깜빡임 없음 · 속은 은은하게)
        }
        else mat.shader = shader;   // [2026-11-27] 이미 있으면 값은 그대로 둠 — 인스펙터에서 직접 조절한 값을 다시 만들기가 덮어쓰지 않게
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);

        var root = Root("KW2_Gas_Cloud", 0, 0f, out var tint);
        var streams = new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomXYZ };
        ParticleSystem Puffs(string name, bool first)
        {
            var ps = first ? Layer(root, name, mat, 22, 1.2f, 1f, 0f) : Loop(root, name, mat, 1.35f, 25f, 1f);
            var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 80;
            m.startLifetime = first ? new ParticleSystem.MinMaxCurve(1.0f, 1.4f) : new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.4f);                       // 사각형 = 뭉치 지름 × 2
            m.startColor = first ? new Color(1f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 1f);   // 빨강 채널 = 처음 피어난 뭉치 (속 달아오름)
            if (first) { var e = ps.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) }); }
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3.6f, 1.05f, 0f); sh.position = new Vector3(0f, -0.15f, 0f);
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f); v.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); v.z = 0f;
            SizeCurve(ps, Key(0, 1f), Key(.5f, 2.3f), Key(1, 2.7f));                     // 부풀어 오름
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.SetActiveVertexStreams(streams); r.alignment = ParticleSystemRenderSpace.View;
            return ps;
        }
        var p1 = Puffs("puffs_first", true);
        var p2 = Puffs("puffs", false);
        // 불티: 구름 안에서 흔들리며 솟음
        var em = Loop(root, "embers", Mat("ember_add6", TEmberSheet, true, 6f), 0.85f, 23f, 0.12f);
        { var m = em.main; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
          m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
          var sh = em.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3.8f, 1.1f, 0f); sh.position = new Vector3(0f, -0.2f, 0f);
          var v = em.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); v.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); v.z = 0f;
          var n = em.noise; n.enabled = true; n.strength = 0.5f; n.frequency = 1.5f; n.scrollSpeed = 0.8f;
          var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.4f, .4f, .4f), 1f) },
                                            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
          var col = em.colorOverLifetime; col.enabled = true; col.color = g; }
        Sheet(em, 2, 2, true);
        tint.top = new[] { em }; tint.dark = new ParticleSystem[0];
        tint.baseColor = Hex("E8542A"); ApplyBase(tint);
        var pulse = root.AddComponent<FxSmokePulse>();
        pulse.renderers = new Renderer[] { p1.GetComponent<ParticleSystemRenderer>(), p2.GetComponent<ParticleSystemRenderer>() };
        pulse.color = Hex("E8542A");
        Save(root, KwDir + "/KW2_Gas_Cloud.prefab");
    }

    // ════════════════════ 8. 코어 파괴 — 같은 파일 내용 교체 ════════════════════
    static void CoreBreak(string kind, Color col, float shake)
    {
        string path = $"{CoreDir}/FX_CoreBreak_{kind}.prefab";
        if (!File.Exists(path)) { Debug.LogWarning("[이펙트 v2] 없음: " + path); return; }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c = root.transform.GetChild(i);
                if (c.name != "Symbol") UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            var sg = root.GetComponent<SortingGroup>(); if (sg == null) sg = root.AddComponent<SortingGroup>();   // ?? 는 유니티 가짜 null 을 못 걸러냄
            sg.sortingLayerName = "근경2D"; sg.sortingOrder = 0;
            order = 0;
            var sym = root.transform.Find("Symbol")?.GetComponent<SpriteRenderer>(); if (sym != null) { sym.sortingLayerName = "근경2D"; sym.sortingOrder = 50; }
            var top = new List<ParticleSystem>(); var dark = new List<ParticleSystem>();
            dark.Add(Smoke(root, "smoke_dark", 6, 0.05f, 1.2f, new Vector2(1.4f, 2.6f), 0.6f, false, 51));
            top.Add(Smoke(root, "smoke_rim", 6, 0.05f, 0.6f, new Vector2(0.9f, 1.6f), 0.6f, true, 51));
            if (kind == "Slash")
            {
                var b = Layer(root, "slash", Mat("soft_add5", TSoft, true, 5f), 1, 0.25f, 1f, 0f, local: true); Size3(b, 3.4f, 0.35f);
                var bm = b.main; bm.startRotation = -35f * Mathf.Deg2Rad; AlphaCurve(b, Key(0, 1f), Key(1, 0f)); top.Add(b);
            }
            else if (kind == "Pierce")
            {
                var r = Layer(root, "thrust", Mat("ray_add9", TRay, true, 9f), 1, 0.2f, 1f, 0f, local: true); Size3(r, 4f, 0.18f); r.transform.localPosition = new Vector3(0.6f, 0, 0);
                SizeCurve3(r, 1f, Key(0, .5f), Key(1, 1.5f)); AlphaCurve(r, Key(0, 1f), Key(1, 0f)); top.Add(r);
                var tp = new GameObject("tip"); tp.transform.SetParent(root.transform, false); tp.transform.localPosition = new Vector3(1.2f, 0, 0);
                top.Add(Flash(tp, "flash", 1.0f, 0f, 0.2f, 6f, false));
            }
            else
            {
                RingPair(root, "impact", 5f, 0f, 0.35f, 1f, top, dark);
                RingPair(root, "impact_floor", 6f, 0.03f, 0.45f, 0.2f, top, dark, yOffset: -0.8f);
            }
            top.Add(Flash(root, "flash", 2.6f, 0.02f, 0.22f, 7f, true));
            RingPair(root, "ring", 4f, 0.05f, 0.45f, 1f, top, dark);
            dark.Add(Shards(root, "shards_dark", 14, 1.15f, false, 52));
            top.Add(Shards(root, "shards", 14, 1f, true, 52));
            top.Add(Sparks(root, "sparks", 16, new Vector2(7, 14), new Vector2(.18f, .36f), new Vector2(.3f, .6f), 0.8f, 7f, delay: 0.03f, mult: 5f));
            top.Add(Glints(root, "glints", 10, 1.3f, 0.18f, 0.7f, 1.2f));
            Distort(root, 1.8f);
            foreach (var r in root.GetComponentsInChildren<ParticleSystem>(true)) if (r.name == "rays" && !top.Contains(r)) top.Add(r);
            rayCache.Clear();
            var tint = root.GetComponent<FxTint>(); if (tint == null) tint = root.AddComponent<FxTint>();
            tint.top = top.ToArray(); tint.dark = dark.ToArray(); tint.baseColor = col; ApplyBase(tint);
            var cb = root.GetComponent<CoreBreakFx>(); if (cb != null) { cb.shakeMagnitude = shake; cb.lifetime = 1.8f; }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ════════════════════ 층 만들기 도우미 ════════════════════
    static GameObject Root(string name, int sortOrder, float lifetime, out FxTint tint, string sortingLayer = "근경2D")
    {
        order = 0;
        var go = new GameObject(name);
        var sg = go.AddComponent<SortingGroup>(); sg.sortingLayerName = sortingLayer; sg.sortingOrder = sortOrder;
        tint = go.AddComponent<FxTint>(); tint.lifetime = lifetime;
        return go;
    }

    /// <summary>버스트 1번짜리 층 (빌보드)</summary>
    static ParticleSystem Layer(GameObject parent, string name, Material mat, int count, float life, float size, float delay, bool local = false)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main;
        m.duration = Mathf.Max(0.05f, life + delay); m.loop = false; m.playOnAwake = true;
        m.startDelay = delay; m.startLifetime = life; m.startSpeed = 0f; m.startSize = size;
        m.simulationSpace = ParticleSystemSimulationSpace.Local; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.maxParticles = Mathf.Max(1, count); m.startColor = Color.white;
        var e = ps.emission; e.enabled = true; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortingOrder = order++;
        r.alignment = local ? ParticleSystemRenderSpace.Local : ParticleSystemRenderSpace.View;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    /// <summary>계속 나오는 층</summary>
    static ParticleSystem Loop(GameObject parent, string name, Material mat, float life, float rate, float size)
    {
        var ps = Layer(parent, name, mat, 40, Mathf.Max(0.05f, life), size, 0f);
        var m = ps.main; m.loop = true; m.duration = 1f; m.maxParticles = 40;
        var e = ps.emission; e.SetBursts(new ParticleSystem.Burst[0]); e.rateOverTime = rate;
        return ps;
    }

    /// <summary>표식용: 한 번 나와 계속 남는 층</summary>
    static ParticleSystem Hold(GameObject parent, string name, Material mat, float size, float alpha, bool local = false)
    {
        var ps = Layer(parent, name, mat, 1, 100000f, size, 0f, local);
        var m = ps.main; m.loop = false; m.duration = 1f; m.startColor = new Color(1, 1, 1, alpha);
        return ps;
    }

    static ParticleSystem Flash(GameObject parent, string name, float size, float delay, float life, float mult, bool rays)
    {
        var f = Layer(parent, name, Mat("flash_add" + mult, TFlash, true, mult), 1, life, size, delay);
        var m = f.main; m.startRotation = 20f * Mathf.Deg2Rad;
        SizeCurve(f, Key(0, .45f), Key(.25f, 1.1f), Key(1, .85f)); AlphaCurve(f, Key(0, 1f), Key(.4f, .85f), Key(1, 0f));
        if (rays)
        {
            var r = Layer(f.gameObject, "rays", Mat("rays_add" + mult, TRays, true, mult * 0.8f), 1, life * 1.4f, size * 1.7f, 0f);
            var rm = r.main; rm.startRotation = 35f * Mathf.Deg2Rad;
            SizeCurve(r, Key(0, .35f), Key(.3f, 1f), Key(1, 1.3f)); AlphaCurve(r, Key(0, 1f), Key(.5f, .7f), Key(1, 0f));
            // 같은 색을 받도록 부모 층 목록에는 FxTint 가 자식까지 넣지 않으므로, 부모와 같은 시작색을 쓰게 하위 입자로 묶음
            var sub = f.subEmitters; sub.enabled = false;
            r.transform.SetParent(parent.transform, true);
            var rr = r.main; rr.startDelay = delay;
            rayCache.Add(r);
        }
        return f;
    }
    static readonly List<ParticleSystem> rayCache = new List<ParticleSystem>();

    static ParticleSystem Sparks(GameObject parent, string name, int count, Vector2 speed, Vector2 size, Vector2 life, float gravity, float drag,
                                 float delay = 0f, float cone = 0f, bool up = false, float mult = 5f)
    {
        var ps = Layer(parent, name, Mat("dot_add" + mult, TDot, true, mult), count, 1f, 1f, delay);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y); m.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); m.gravityModifier = gravity; m.maxParticles = count;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var sh = ps.shape; sh.enabled = true;
        if (cone > 0f)
        {
            sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cone; sh.radius = 0.05f;
            sh.rotation = up ? new Vector3(-90f, 0f, 0f) : new Vector3(0f, 90f, 0f);   // 원뿔 축: 위(+y) 또는 앞(+x)
        }
        else { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.05f; sh.arc = 360f; }
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 100f; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
        SizeCurve(ps, Key(0, .6f), Key(.05f, 1f), Key(1, 0f));
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.5f;
        return ps;
    }

    static ParticleSystem Glints(GameObject parent, string name, int count, float spread, float delay, float life, float rise)
    {
        var ps = Layer(parent, name, Mat("kira_add", TKira, true, 9f), count, life, 1f, delay);
        var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(.25f, .45f); m.startSpeed = 0f; m.startDelay = new ParticleSystem.MinMaxCurve(delay, delay + 0.2f);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(spread * 2f, spread, 0f);
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.x = 0; v.y = rise / Mathf.Max(0.05f, life); v.z = 0;
        SizeCurve(ps, Key(0, 0f), Key(.15f, 1f), Key(.6f, .7f), Key(1, 0f));
        return ps;
    }

    static ParticleSystem Smoke(GameObject parent, string name, int count, float delay, float life, Vector2 size, float spread, bool rim, int seed)
    {
        var ps = Layer(parent, name, rim ? Mat("smoke_add", TSmoke, true, 2.5f) : Mat("smoke_alpha", TSmoke, false, 1f), count, life, 1f, delay);
        var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(size.x * (rim ? .6f : 1f), size.y * (rim ? .6f : 1f));
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = spread; sh.arc = 360f;
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.x = 0; v.y = 0.4f; v.z = 0;
        var ro = ps.rotationOverLifetime; ro.enabled = true; ro.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        SizeCurve(ps, Key(0, .5f), Key(1, 1.3f));
        if (rim) AlphaCurve(ps, Key(0, 0f), Key(.1f, .6f), Key(1, 0f)); else AlphaCurve(ps, Key(0, 0f), Key(.1f, .9f), Key(.7f, .6f), Key(1, 0f));
        return ps;
    }

    static ParticleSystem Shards(GameObject parent, string name, int count, float sizeMul, bool bright, int seed, bool up = false)
    {
        var ps = Layer(parent, name, bright ? Mat("shard_add", TShard, true, 4f) : Mat("shard_alpha", TShard, false, 1f), count, 0.75f, 1f, 0f);
        var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(.3f * sizeMul, .6f * sizeMul); m.startSpeed = up ? new ParticleSystem.MinMaxCurve(3f, 5f) : new ParticleSystem.MinMaxCurve(3f, 7f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.gravityModifier = up ? 1.0f : 0.65f; m.simulationSpace = ParticleSystemSimulationSpace.World;
        ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed;
        var sh = ps.shape; sh.enabled = true;
        if (up) { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.1f; sh.rotation = new Vector3(-90f, 0, 0); }
        else { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.arc = 360f; }
        var ro = ps.rotationOverLifetime; ro.enabled = true; ro.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
        Sheet(ps, 4, 4, true);
        // 끝으로 갈수록 옅어지다 0.8 무렵 한 번 밝아지고 사라짐 (밝은 층만)
        if (bright) AlphaCurve(ps, Key(0, 1f), Key(.7f, .6f), Key(.8f, 1f), Key(1, 0f)); else AlphaCurve(ps, Key(0, .95f), Key(.85f, .7f), Key(1, 0f));
        return ps;
    }

    static void RingPair(GameObject parent, string name, float size, float delay, float life, float ys, List<ParticleSystem> top, List<ParticleSystem> dark, float yOffset = 0f)
    {
        var d = Layer(parent, name + "_dark", Mat("ring_alpha", TRing, false, 1f), 1, life, 1f, delay);
        SizeCurve3(d, size * 1.06f, Key(0, .07f), Key(.3f, .7f), Key(1, 1f), ys); AlphaCurve(d, Key(0, .8f), Key(1, 0f)); d.transform.localPosition = new Vector3(0, yOffset, 0); dark.Add(d);
        var t = Layer(parent, name, Mat("ring_add45", TRing, true, 4.5f), 1, life, 1f, delay);
        SizeCurve3(t, size, Key(0, .07f), Key(.3f, .7f), Key(1, 1f), ys); AlphaCurve(t, Key(0, 1f), Key(.5f, .35f), Key(1, 0f)); t.transform.localPosition = new Vector3(0, yOffset, 0); top.Add(t);
    }

    static GameObject Distort(GameObject parent, float scale)
    {
        if (distortSrc == null) return null;
        var go = UnityEngine.Object.Instantiate(distortSrc, parent.transform, false);
        go.name = "distort"; go.transform.localPosition = Vector3.zero; go.transform.localScale *= scale;
        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.sortingOrder = -10;
        return go;
    }

    // ── 모듈 도우미 ──
    static Keyframe Key(float t, float v) => new Keyframe(t, v);
    static void SizeCurve(ParticleSystem ps, params Keyframe[] k)
    {
        var s = ps.sizeOverLifetime; s.enabled = true; s.separateAxes = false; s.size = new ParticleSystem.MinMaxCurve(1f, Lin(k));
    }
    static void SizeCurve3(ParticleSystem ps, float mult, params Keyframe[] k) => SizeCurve3(ps, mult, k, 1f);
    static void SizeCurve3(ParticleSystem ps, float mult, Keyframe a, Keyframe b, Keyframe c, float ys) => SizeCurve3(ps, mult, new[] { a, b, c }, ys);
    static void SizeCurve3(ParticleSystem ps, float mult, Keyframe[] k, float ys)
    {
        var s = ps.sizeOverLifetime; s.enabled = true; s.separateAxes = true;
        s.x = new ParticleSystem.MinMaxCurve(mult, Lin(k)); s.y = new ParticleSystem.MinMaxCurve(mult * ys, Lin(k)); s.z = new ParticleSystem.MinMaxCurve(1f, Lin(k));
        var m = ps.main; if (!m.startSize3D) { m.startSize3D = true; m.startSizeX = 1f; m.startSizeY = 1f; m.startSizeZ = 1f; }
    }
    static void Size3(ParticleSystem ps, float x, float y)
    {
        var m = ps.main; m.startSize3D = true; m.startSizeX = x; m.startSizeY = y; m.startSizeZ = 1f;
    }
    static void AlphaCurve(ParticleSystem ps, params Keyframe[] k)
    {
        var g = new Gradient();
        var ak = new GradientAlphaKey[k.Length]; for (int i = 0; i < k.Length; i++) ak[i] = new GradientAlphaKey(k[i].value, k[i].time);
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
        var c = ps.colorOverLifetime; c.enabled = true; c.color = g;
    }
    static void Sheet(ParticleSystem ps, int x, int y, bool randomFrame)
    {
        var t = ps.textureSheetAnimation; t.enabled = true; t.mode = ParticleSystemAnimationMode.Grid; t.numTilesX = x; t.numTilesY = y;
        t.animation = ParticleSystemAnimationType.WholeSheet;
        if (randomFrame) t.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        else t.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 0.999f));
    }
    static AnimationCurve Lin(Keyframe[] k)
    {
        var c = new AnimationCurve(k);
        for (int i = 0; i < c.length; i++) { AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear); }
        return c;
    }

    // 재질: 팀 재질 복사 + 텍스처 · HDR 배수만 바꿈 (이름이 같으면 재사용)
    static Material Mat(string key, string texPath, bool additive, float mult)
    {
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        string path = $"{MatDir}/M2_{key}.mat";
        var tmpl = AssetDatabase.LoadAssetAtPath<Material>(additive ? AddTemplate : AlphaTemplate);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tmpl == null) throw new Exception("[이펙트 v2] 재질 템플릿 없음: " + (additive ? AddTemplate : AlphaTemplate));
        if (tex == null) Debug.LogWarning("[이펙트 v2] 텍스처 없음: " + texPath);
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) { existing = new Material(tmpl); AssetDatabase.CreateAsset(existing, path); }
        else existing.CopyPropertiesFromMaterial(tmpl);
        existing.SetTexture("_BaseMap", tex); if (existing.HasProperty("_MainTex")) existing.SetTexture("_MainTex", tex);
        if (additive) mult *= AddGain;
        else
        {
            // 반투명(어두운 바탕) 템플릿 trail_light 는 발광(_EMISSION, 41·12·0.6 HDR)과 '색 더하기' 모드가 켜져 있어
            // 어두워야 할 층이 노랗게 빛났음 → 발광 끄고 색 곱하기 모드로
            existing.DisableKeyword("_EMISSION"); existing.DisableKeyword("_COLORADDSUBDIFF_ON");
            if (existing.HasProperty("_EmissionColor")) existing.SetColor("_EmissionColor", Color.black);
            if (existing.HasProperty("_EmissionEnabled")) existing.SetFloat("_EmissionEnabled", 0f);
            if (existing.HasProperty("_ColorMode")) existing.SetFloat("_ColorMode", 0f);
            if (existing.HasProperty("_BaseColorAddSubDiff")) existing.SetVector("_BaseColorAddSubDiff", Vector4.zero);
            if (existing.HasProperty("_ColorAddSubDiff")) existing.SetVector("_ColorAddSubDiff", Vector4.zero);
            existing.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        existing.SetColor("_BaseColor", new Color(mult, mult, mult, 1f));
        if (existing.HasProperty("_Color")) existing.SetColor("_Color", new Color(mult, mult, mult, 1f));
        EditorUtility.SetDirty(existing);
        mats[key] = existing;
        return existing;
    }

    static void ApplyBase(FxTint t)
    {
        FxTint.Ratio(t.baseColor, out Color r, out Color d);
        if (t.top != null) foreach (var p in t.top) if (p != null) { var m = p.main; m.startColor = new Color(r.r, r.g, r.b, m.startColor.color.a); }
        if (t.dark != null) foreach (var p in t.dark) if (p != null) { var m = p.main; m.startColor = new Color(d.r, d.g, d.b, m.startColor.color.a); }
    }

    static void Save(GameObject root, string path)
    {
        // 빛줄기(rays) 층도 같은 색을 받도록 FxTint 밝은 층 목록에 넣음
        var tint = root.GetComponent<FxTint>();
        if (tint != null)
        {
            var list = new List<ParticleSystem>(tint.top ?? new ParticleSystem[0]);
            foreach (var r in root.GetComponentsInChildren<ParticleSystem>(true)) if (r.name == "rays" && !list.Contains(r)) list.Add(r);
            tint.top = list.ToArray();
            if (tint.baseColor == Color.white) tint.baseColor = Hex("E8542A");
            ApplyBase(tint);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        rayCache.Clear();
    }

    static Transform FindChild(Transform t, string prefix)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name.StartsWith(prefix)) return c;
        return null;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
