using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using Com.LuisPedroFonseca.ProCamera2D;

/// <summary>
/// DialogueBeat(대화 연동 연출) 테스트 씬을 자동으로 만들어 주는 에디터 도구.
/// 메뉴: Tools > 대화 연출 테스트 씬 만들기
///
/// 만들어지는 것 (Assets/test/DialogueBeatTest/ 폴더):
///   - DialogueBeatTest.unity          : 테스트 씬
///   - Generated/                      : 임시 그림(네모/원), 감정 애니메이션(한 번 재생 4개 + 유지용 반복 4개) + 애니메이터, 감정 파티클 4개
/// 이미 있으면 덮어쓴다.
/// </summary>
public static class DialogueBeatTestSceneBuilder
{
    const string Root = "Assets/_Test/DialogueBeatTest";
    const string Gen = Root + "/Generated";
    const string ScenePath = Root + "/DialogueBeatTest.unity";
    const string FontPath = "Assets/Etc/Font/BlackGFCRedSpirit SDF.asset";
    const string ParticleMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    const string SpriteUnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    static readonly string[] Emotes = { "Surprise", "Angry", "Laugh", "Sweat" };

    [MenuItem("Tools/대화 연출 테스트 씬 만들기")]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("대화 연출 테스트 씬", ScenePath + " 가 이미 있습니다. 새로 만들까요?", "새로 만들기", "취소"))
            return;

        EnsureFolder(Gen);

        // ── 1. 에셋 만들기 ──────────────────────────────
        Sprite square = MakeSprite("Square", 4, false);
        Sprite circle = MakeSprite("Circle", 64, true);
        AnimatorController ctrl = MakeAnimator();
        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
        if (particleMat == null)
        {
            particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(particleMat, Gen + "/ParticleMat.mat");
        }
        GameObject fxSurprise = MakeParticle("FX_놀람", new Color(1f, 0.9f, 0.2f), 18, 3f, 0f, particleMat);
        GameObject fxAngry = MakeParticle("FX_분노", new Color(1f, 0.25f, 0.15f), 30, 4f, -0.3f, particleMat);
        GameObject fxSweat = MakeParticle("FX_땀", new Color(0.45f, 0.8f, 1f), 8, 1.5f, 1.2f, particleMat);
        GameObject fxLaugh = MakeParticle("FX_웃음", new Color(0.5f, 1f, 0.45f), 14, 2f, -0.5f, particleMat);
        Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMatPath);

        // ── 2. 새 씬 ────────────────────────────────────
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 바닥
        GameObject ground = MakeSpriteObject("Ground", null, square, new Color(0.25f, 0.27f, 0.3f), spriteMat, -1);
        ground.transform.position = new Vector3(0f, -1.25f, 0f);
        ground.transform.localScale = new Vector3(16f, 0.5f, 1f);

        // 캐릭터 2명 (루트 = 위치/카메라 기준, Visual = DOTween 으로 움직일 그림, EmoteIcon = 애니메이션으로 튀어나오는 감정 표시)
        Transform hero = MakeCharacter("주인공", new Vector3(-2.5f, -1f, 0f), 1f, new Vector3(0.8f, 1.2f, 1f), new Color(0.35f, 0.6f, 1f), square, circle, ctrl, spriteMat);
        hero.gameObject.tag = "Player";
        Transform boss = MakeCharacter("보스", new Vector3(2.5f, -1f, 0f), -1f, new Vector3(1.3f, 1.9f, 1f), new Color(0.85f, 0.2f, 0.25f), square, circle, ctrl, spriteMat);

        // 카메라 (ProCamera2D + 흔들림 + CameraFollow — 게임 카메라와 같은 구성)
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0.3f, -10f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        camGo.AddComponent<AudioListener>();
        UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.SetRenderer(1); // 게임 카메라와 같은 렌더러 (Renderer3D_LightingTest)
        ProCamera2D pc = camGo.AddComponent<ProCamera2D>();
        pc.CameraTargets.Add(new CameraTarget { TargetTransform = hero, TargetInfluenceH = 1f, TargetInfluenceV = 1f, TargetOffset = new Vector2(2.5f, 1.3f) });
        camGo.AddComponent<ProCamera2DShake>();
        camGo.AddComponent<CameraFollow>();

        // 효과음
        GameObject audioGo = new GameObject("AudioManager");
        AudioSource bgm = audioGo.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        audioGo.AddComponent<AudioManager>().bgmSource = bgm;

        // 대화 UI + NPC
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject canvasGo = new GameObject("DialogueCanvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform prt = (RectTransform)panel.transform;
        prt.anchorMin = new Vector2(0f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(-200f, 240f);
        prt.anchoredPosition = new Vector2(0f, 40f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        RectTransform trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(48f, 32f);
        trt.offsetMax = new Vector2(-48f, -32f);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 42f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.text = "";
        panel.SetActive(false);

        NPC npc = canvasGo.AddComponent<NPC>();
        npc.textComponent = tmp;
        npc.textSpeed = 0.04f;
        npc.dialogueCanvas = panel;
        npc.portraits = new Image[0];
        npc.dialogueSets = new[]
        {
            new NPC.DialogueSet
            {
                conditionKey = "",
                lines = new[]
                {
                    "......",
                    "주인공: 여긴... 어디지?",
                    "보스: 허가도 없이 올려다보지 마라. 불쾌하다. 애송이.",
                    "주인공: 뭐, 뭐라고?!",
                    "보스: 후후... 하하하하!",
                    "(테스트 끝 — 다음으로 넘기면 대화가 닫힙니다. 그 뒤 Enter 로 다시 시작)",
                },
                speakerIndex = new[] { 0, 0, 1, 0, 1, 0 },
            }
        };

        // ── 3. 연출(DialogueBeat) — 전부 꺼진 상태로 만들고 NPC 의 Cutscenes 에 등록 ──
        Transform beatsRoot = new GameObject("DialogueBeats").transform;
        var beats = new List<GameObject>();
        var startLines = new List<int>();
        AudioClip sfxConfirm = Clip("Assets/5. Sound/SFX/UI/버튼_짧게/UI_확인.wav");
        AudioClip sfxCancel = Clip("Assets/5. Sound/SFX/UI/버튼_짧게/UI_취소.wav");
        AudioClip sfxNode = Clip("Assets/5. Sound/SFX/UI/버튼_짧게/UI_노드선택.wav");
        AudioClip sfxHeavy = Clip("Assets/5. Sound/SFX/공격/근접/금속휘두르기_무겁게_A.wav");
        AudioClip sfxSwish = Clip("Assets/5. Sound/SFX/공격/근접/Swish_03.wav");

        // 줄 1: 주인공 놀람 — 애니메이션 + 파티클 + 효과음 + 깡충 + 카메라
        DialogueBeat b = NewBeat(beatsRoot, "1_주인공_놀람", 1, beats, startLines);
        SetAnim(b, hero, "Surprise", 0f);
        SetParticle(b, fxSurprise, hero, new Vector3(0f, 1.7f, 0f), 0f);
        b.sfx = sfxConfirm;
        SetMotion(b, hero, BeatMotion.Jump, 0.5f, 0.35f, 0f);
        SetCamera(b, hero, 0.6f, 1.0f, 0f);

        // 줄 2: 보스 분노 — 분노 애니메이션을 2.5초 동안 유지(HoldBool) + 붉은 파티클 + 묵직한 소리 + 부들부들 + 화면 흔들림 + 카메라
        b = NewBeat(beatsRoot, "2_보스_분노(2.5초 유지)", 2, beats, startLines);
        SetAnimHold(b, boss, "AngryHold", 0f, 2.5f);
        SetParticle(b, fxAngry, boss, new Vector3(0f, 2.2f, 0f), 0f);
        b.sfx = sfxHeavy;
        SetMotion(b, boss, BeatMotion.Shake, 1f, 0.5f, 0f);
        b.repeatMotionWhileHold = true;   // 화난 2.5초 동안 계속 부들부들
        b.shake = true; b.shakeStrength = 0.4f; b.shakeDuration = 0.35f;
        SetCamera(b, boss, 0.6f, 2.2f, 0f);

        // 줄 2 (같은 줄, 0.35초 뒤): 주인공 움찔 — 뒤로 움찔 + 휙 소리 + 땀
        b = NewBeat(beatsRoot, "2_주인공_움찔(0.35초뒤)", 2, beats, startLines);
        SetMotion(b, hero, BeatMotion.Punch, -0.6f, 0.3f, 0.35f);   // 음수 = 보는 방향의 반대(뒤)로
        b.sfx = sfxSwish; b.sfxDelay = 0.35f;
        SetParticle(b, fxSweat, hero, new Vector3(0.4f, 1.5f, 0f), 0.4f);

        // 줄 3: 주인공 당황 — 당황 애니메이션을 1.5초 동안 유지(HoldBool) + 땀 + 눌림 + 카메라
        b = NewBeat(beatsRoot, "3_주인공_당황(1.5초 유지)", 3, beats, startLines);
        SetAnimHold(b, hero, "SweatHold", 0f, 1.5f);
        SetParticle(b, fxSweat, hero, new Vector3(0.4f, 1.5f, 0f), 0f);
        b.sfx = sfxCancel;
        SetMotion(b, hero, BeatMotion.Squash, 0.8f, 0.5f, 0f);
        b.repeatMotionWhileHold = true;   // 당황한 1.5초 동안 계속 움찔움찔 (0.5초 × 3번)
        SetCamera(b, hero, 0.5f, 1.0f, 0f);

        // 줄 4: 보스 웃음 — 애니메이션 + 초록 파티클 + 깡충 + 약한 흔들림 + 카메라
        b = NewBeat(beatsRoot, "4_보스_웃음", 4, beats, startLines);
        SetAnim(b, boss, "Laugh", 0f);
        SetParticle(b, fxLaugh, boss, new Vector3(0f, 2.2f, 0f), 0f);
        b.sfx = sfxNode;
        SetMotion(b, boss, BeatMotion.Jump, 0.3f, 0.3f, 0f);
        b.shake = true; b.shakeStrength = 0.15f; b.shakeDuration = 0.2f;
        SetCamera(b, boss, 0.6f, 1.6f, 0f);

        // 줄 4 (같은 줄, 0.4초 / 0.8초 뒤): 보스 두 번 더 깡충 — 같은 줄에 연출 여러 개를 시간차로 겹치는 예
        b = NewBeat(beatsRoot, "4_보스_웃음_2번째(0.4초뒤)", 4, beats, startLines);
        SetMotion(b, boss, BeatMotion.Jump, 0.3f, 0.3f, 0.4f);
        b.sfx = sfxNode; b.sfxDelay = 0.4f;
        b = NewBeat(beatsRoot, "4_보스_웃음_3번째(0.8초뒤)", 4, beats, startLines);
        SetMotion(b, boss, BeatMotion.Jump, 0.3f, 0.3f, 0.8f);
        b.sfx = sfxNode; b.sfxDelay = 0.8f;

        npc.cutscenes = beats.ToArray();
        npc.cutsceneStartLines = startLines.ToArray();
        npc.cutsceneEndLines = new int[0];

        // 테스트 시작기
        GameObject starter = new GameObject("TestStarter");
        starter.AddComponent<DialogueBeatTestStarter>().npc = npc;

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[대화 연출 테스트 씬] 완성: " + ScenePath + " — 플레이를 누르면 대화가 자동으로 시작됩니다.");
        if (font == null) Debug.LogWarning("[대화 연출 테스트 씬] 폰트를 못 찾아 기본 폰트를 씁니다: " + FontPath);
    }

    // ───────────── 연출 설정 도우미 ─────────────
    static DialogueBeat NewBeat(Transform parent, string name, int line, List<GameObject> beats, List<int> lines)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        DialogueBeat b = go.AddComponent<DialogueBeat>();
        b.particlePrefab = null;
        b.cameraFocus = null;
        b.motion = BeatMotion.None;
        go.SetActive(false);   // 꺼둬야 함 — NPC 가 해당 줄에서 켜면 그때 재생
        beats.Add(go);
        lines.Add(line);
        return b;
    }

    static void SetAnim(DialogueBeat b, Transform character, string trigger, float delay)
    {
        b.animator = character.GetComponent<Animator>();
        b.animationMode = BeatAnimMode.Trigger;
        b.animatorTrigger = trigger;
        b.animationDelay = delay;
    }

    // 유지 모드: Bool 파라미터를 켜서 holdSeconds 동안 그 상태로 둠
    static void SetAnimHold(DialogueBeat b, Transform character, string boolName, float delay, float holdSeconds)
    {
        b.animator = character.GetComponent<Animator>();
        b.animationMode = BeatAnimMode.HoldBool;
        b.animatorTrigger = boolName;
        b.animationDelay = delay;
        b.animationHoldSeconds = holdSeconds;
    }

    static void SetParticle(DialogueBeat b, GameObject prefab, Transform at, Vector3 offset, float delay)
    {
        b.particlePrefab = prefab;
        b.particlePoint = at;
        b.particleOffset = offset;
        b.attachToPoint = true;
        b.particleLifetime = 2f;
        b.particleDelay = delay;
    }

    static void SetMotion(DialogueBeat b, Transform character, BeatMotion motion, float strength, float duration, float delay)
    {
        b.motionTarget = character.Find("Visual");
        b.motion = motion;
        b.motionStrength = strength;
        b.motionDuration = duration;
        b.motionDelay = delay;
    }

    static void SetCamera(DialogueBeat b, Transform focus, float moveTime, float hold, float delay)
    {
        b.cameraFocus = focus.Find("CameraPoint");
        b.cameraMoveTime = moveTime;
        b.cameraHoldSeconds = hold;
        b.cameraDelay = delay;
    }

    // ───────────── 씬 오브젝트 ─────────────
    static Transform MakeCharacter(string name, Vector3 pos, float facing, Vector3 bodySize, Color color,
                                   Sprite square, Sprite circle, AnimatorController ctrl, Material mat)
    {
        GameObject root = new GameObject(name);
        root.transform.position = pos;
        root.transform.localScale = new Vector3(facing, 1f, 1f);   // -1 = 왼쪽을 봄

        GameObject visual = MakeSpriteObject("Visual", root.transform, square, color, mat, 0);
        visual.transform.localPosition = new Vector3(0f, bodySize.y * 0.5f, 0f);
        visual.transform.localScale = bodySize;
        // 눈 (보는 방향 표시)
        GameObject eye = MakeSpriteObject("Eye", visual.transform, square, Color.white, mat, 1);
        eye.transform.localPosition = new Vector3(0.25f, 0.25f, 0f);
        eye.transform.localScale = new Vector3(0.18f, 0.12f, 1f);

        GameObject icon = MakeSpriteObject("EmoteIcon", root.transform, circle, new Color(1f, 1f, 1f, 0f), mat, 5);
        icon.transform.localPosition = new Vector3(0f, bodySize.y + 0.55f, 0f);
        icon.transform.localScale = new Vector3(0f, 0f, 1f);   // z 는 1 로 (애니메이션은 x, y 만 바꿈)

        GameObject camPoint = new GameObject("CameraPoint");
        camPoint.transform.SetParent(root.transform, false);
        camPoint.transform.localPosition = new Vector3(0f, bodySize.y * 0.6f, 0f);

        Animator anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        return root.transform;
    }

    static GameObject MakeSpriteObject(string name, Transform parent, Sprite sprite, Color color, Material mat, int order)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (mat != null) sr.sharedMaterial = mat;
        return go;
    }

    static AudioClip Clip(string path)
    {
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning("[대화 연출 테스트 씬] 효과음 없음 (건너뜀): " + path);
        return c;
    }

    // ───────────── 생성 에셋 ─────────────
    static Sprite MakeSprite(string name, int size, bool circle)
    {
        string path = Gen + "/" + name + ".png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 1f;
                if (circle)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    a = Mathf.Clamp01(r - d);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = size;          // 1칸 = 1유닛
        imp.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 감정 애니메이션: 머리 위 EmoteIcon 이 감정 색으로 "뿅" 커졌다가 사라짐
    static AnimatorController MakeAnimator()
    {
        string ctrlPath = Gen + "/EmoteAnimator.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle");
        idle.motion = MakeClip("Idle", Color.clear, true);
        sm.defaultState = idle;

        Color[] colors =
        {
            new Color(1f, 0.9f, 0.2f, 1f),   // Surprise
            new Color(1f, 0.2f, 0.15f, 1f),  // Angry
            new Color(0.4f, 1f, 0.4f, 1f),   // Laugh
            new Color(0.45f, 0.8f, 1f, 1f),  // Sweat
        };

        for (int i = 0; i < Emotes.Length; i++)
        {
            string e = Emotes[i];
            ctrl.AddParameter(e, AnimatorControllerParameterType.Trigger);
            AnimatorState st = sm.AddState(e);
            st.motion = MakeClip(e, colors[i], false);

            AnimatorStateTransition into = sm.AddAnyStateTransition(st);
            into.AddCondition(AnimatorConditionMode.If, 0f, e);
            into.hasExitTime = false;
            into.duration = 0f;
            into.canTransitionToSelf = true;   // 같은 감정을 연달아 보내도 처음부터 다시 재생

            AnimatorStateTransition back = st.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;

            // 유지(Hold) 버전: Bool 이 켜져 있는 동안 계속 반복 → 꺼지면 바로 평소 상태로
            string holdName = e + "Hold";
            ctrl.AddParameter(holdName, AnimatorControllerParameterType.Bool);
            AnimatorState hold = sm.AddState(holdName);
            hold.motion = MakeHoldClip(e, colors[i]);

            AnimatorStateTransition holdIn = sm.AddAnyStateTransition(hold);
            holdIn.AddCondition(AnimatorConditionMode.If, 0f, holdName);
            holdIn.hasExitTime = false;
            holdIn.duration = 0f;
            holdIn.canTransitionToSelf = false;   // 켜져 있는 동안 매 프레임 처음부터 다시 시작하지 않도록

            AnimatorStateTransition holdOut = hold.AddTransition(idle);
            holdOut.AddCondition(AnimatorConditionMode.IfNot, 0f, holdName);
            holdOut.hasExitTime = false;
            holdOut.duration = 0f;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static AnimationClip MakeClip(string name, Color c, bool loop)
    {
        string path = Gen + "/Emote_" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { name = "Emote_" + name };

        AnimationCurve scale, alpha;
        if (loop)
        {
            scale = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            alpha = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        }
        else
        {
            scale = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 0.75f), new Keyframe(0.25f, 0.55f), new Keyframe(0.8f, 0.55f), new Keyframe(1f, 0.7f));
            alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
        }

        Bind(clip, typeof(Transform), "m_LocalScale.x", scale);
        Bind(clip, typeof(Transform), "m_LocalScale.y", scale);
        Bind(clip, typeof(SpriteRenderer), "m_Color.r", AnimationCurve.Constant(0f, 1f, c.r));
        Bind(clip, typeof(SpriteRenderer), "m_Color.g", AnimationCurve.Constant(0f, 1f, c.g));
        Bind(clip, typeof(SpriteRenderer), "m_Color.b", AnimationCurve.Constant(0f, 1f, c.b));
        Bind(clip, typeof(SpriteRenderer), "m_Color.a", alpha);

        AnimationClipSettings set = AnimationUtility.GetAnimationClipSettings(clip);
        set.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, set);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // 유지용 반복 애니메이션: 감정 표시가 떠 있는 채로 두근두근 커졌다 작아졌다 반복
    static AnimationClip MakeHoldClip(string name, Color c)
    {
        string path = Gen + "/Emote_" + name + "Hold.anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { name = "Emote_" + name + "Hold" };

        var scale = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 0.9f), new Keyframe(0.5f, 0.5f));   // 크게 두근두근
        Bind(clip, typeof(Transform), "m_LocalScale.x", scale);
        Bind(clip, typeof(Transform), "m_LocalScale.y", scale);
        Bind(clip, typeof(SpriteRenderer), "m_Color.r", AnimationCurve.Constant(0f, 0.5f, c.r));
        Bind(clip, typeof(SpriteRenderer), "m_Color.g", AnimationCurve.Constant(0f, 0.5f, c.g));
        Bind(clip, typeof(SpriteRenderer), "m_Color.b", AnimationCurve.Constant(0f, 0.5f, c.b));
        Bind(clip, typeof(SpriteRenderer), "m_Color.a", AnimationCurve.Constant(0f, 0.5f, 1f));

        AnimationClipSettings set = AnimationUtility.GetAnimationClipSettings(clip);
        set.loopTime = true;   // 반복 재생 (Loop Time 체크)
        AnimationUtility.SetAnimationClipSettings(clip, set);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void Bind(AnimationClip clip, System.Type type, string prop, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("EmoteIcon", type, prop), curve);
    }

    // 감정 파티클: 한 번 "팡" 터지고 스스로 사라짐
    static GameObject MakeParticle(string name, Color color, int count, float speed, float gravity, Material mat)
    {
        string path = Gen + "/" + name + ".prefab";
        GameObject go = new GameObject(name);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.sortingOrder = 10;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
