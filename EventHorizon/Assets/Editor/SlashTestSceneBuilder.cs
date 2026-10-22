using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// [2026-10-22] 참격 이펙트(SwordSlashFX · ScreenSlashEffect) 테스트 씬을 자동으로 만드는 에디터 도구.
/// 메뉴: Tools > 참격 테스트 씬 만들기
///
/// 만들어지는 것 (Assets/_Test/SlashTest/):
///   - SlashTest.unity : 테스트 씬
///   - Generated/      : 배경 체크무늬 그림, 바닥선 그림
/// 이미 있으면 물어보고 덮어쓴다. 기존 씬·에셋은 건드리지 않는다.
///
/// 화면 가르기는 렌더러에 'ScreenSlash' 풀스크린 패스가 있어야 보인다 (없으면 경고만 띄우고 씬은 만든다).
/// </summary>
public static class SlashTestSceneBuilder
{
    const string Root = "Assets/_Test/SlashTest";
    const string Gen = Root + "/Generated";
    const string ScenePath = Root + "/SlashTest.unity";

    const string SwordSheet = "Assets/4. Art/Character/Player/Sprites/sword_attack_sprite.png";
    const string ClipDir = "Assets/4. Art/Character/Player/Animations/";
    static readonly string[] SwordClips = { "Sword01", "Sword02", "Sword03" };
    const string SwordData = "Assets/3. Data/WeaponData/Sword.asset";
    const string SlashMatDir = "Assets/Etc/Shader/SwordSlash/Materials/";
    const string ScreenSlashMat = "Assets/Etc/Shader/ScreenSlash/M_ScreenSlash.mat";
    const string SpriteUnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string FeatureName = "ScreenSlash";   // (예전 방식) 풀스크린 패스 이름 — 지금은 HitGlitchScreenFeature 의 Slash Material 로 그림

    [MenuItem("Tools/참격 테스트 씬 만들기")]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("참격 테스트 씬", ScenePath + " 가 이미 있습니다. 새로 만들까요?", "새로 만들기", "취소"))
            return;

        EnsureFolder(Gen);

        // ── 1. 재료 ────────────────────────────────────
        Sprite checker = MakeChecker();
        Sprite white = MakeWhite();
        Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMatPath);

        var combos = new List<SlashTestDriver.Combo>();
        foreach (var name in SwordClips)
        {
            var c = ReadClip(name);
            if (c != null) combos.Add(c);
        }
        if (combos.Count == 0)
        {
            EditorUtility.DisplayDialog("참격 테스트 씬", "검 애니메이션(Sword01~03)에서 프레임을 읽지 못했습니다.\n" + ClipDir, "확인");
            return;
        }

        float speed = 1f;
        var sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordData);
        if (sword != null) speed = sword.attackSpeed;

        int rendererIndex = FindRendererIndex(out bool hasFeature, out string rendererName);

        // ── 2. 새 씬 ────────────────────────────────────
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 1.2f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.4f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.1f, 1f);
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        if (rendererIndex >= 0) camData.SetRenderer(rendererIndex);
        camData.renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();

        // 배경 (화면이 갈라지고 밀리는 게 잘 보이게)
        var bg = new GameObject("배경_체크무늬");
        bg.transform.position = new Vector3(0f, 1.2f, 5f);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = checker;
        if (spriteMat != null) bgSr.sharedMaterial = spriteMat;
        bgSr.sortingOrder = -100;

        var floor = new GameObject("바닥선");
        floor.transform.position = new Vector3(0f, -0.95f, 0f);
        floor.transform.localScale = new Vector3(24f, 0.06f, 1f);
        var floorSr = floor.AddComponent<SpriteRenderer>();
        floorSr.sprite = white;
        floorSr.color = new Color(0.9f, 0.75f, 0.45f, 1f);
        if (spriteMat != null) floorSr.sharedMaterial = spriteMat;
        floorSr.sortingOrder = -50;

        // 테스트 검사
        var man = new GameObject("TestSwordsman");
        var sr = man.AddComponent<SpriteRenderer>();
        sr.sprite = combos[0].frames.Length > 0 ? combos[0].frames[0] : null;
        if (spriteMat != null) sr.sharedMaterial = spriteMat;
        sr.sortingOrder = 10;
        // 발바닥을 바닥선에 맞춤
        if (sr.sprite != null)
        {
            float bottom = sr.sprite.bounds.min.y;
            man.transform.position = new Vector3(0f, -0.95f - bottom, 0f);
        }

        var fx = man.AddComponent<SwordSlashFX>();
        fx.targetSprite = sr;
        fx.arcMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlashMatDir + "M_SlashArc.mat");
        fx.wispMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlashMatDir + "M_SlashWisp.mat");
        fx.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlashMatDir + "M_SlashEmber.mat");
        fx.flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlashMatDir + "M_SlashFlare.mat");

        var driver = man.AddComponent<SlashTestDriver>();
        driver.target = sr;
        driver.combos = combos.ToArray();
        driver.speed = speed;

        // 화면 가르기
        var ss = new GameObject("ScreenSlash");
        var slash = ss.AddComponent<ScreenSlashEffect>();
        slash.slashMaterial = AssetDatabase.LoadAssetAtPath<Material>(ScreenSlashMat);
        slash.followTarget = man.transform;
        slash.targetCamera = cam;
        driver.screenSlash = slash;

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();

        string msg = "만들었습니다: " + ScenePath + "\n\n" +
                     "플레이 후  X: 검 공격 / 1·2·3: 그 타만 / G·F: 화면 가르기 / R: 자동 반복\n" +
                     "검 1·2·3타 프레임 " + combos.Count + "개 읽음, 공격 속도 x" + speed + "\n" +
                     "카메라 렌더러: " + rendererName;
        if (!hasFeature)
            msg += "\n\n※ 렌더러의 HitGlitchScreenFeature 에 Slash Material(M_ScreenSlash)이 비어 있어서 화면 가르기는 안 보입니다.\n" +
                   "Settings/Renderer3D_LightingTest → HitGlitchScreenFeature → Slash Material 에 M_ScreenSlash 를 넣어 주세요. (검 참격은 그대로 보입니다)";
        EditorUtility.DisplayDialog("참격 테스트 씬", msg, "확인");
        if (!hasFeature) Debug.LogWarning("[참격 테스트] HitGlitchScreenFeature 의 Slash Material 이 비어 있습니다. 화면 가르기는 보이지 않습니다.");
    }

    // 검 애니메이션 클립에서 '스프라이트가 바뀌는 순간'들을 읽어 온다
    static SlashTestDriver.Combo ReadClip(string clipName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipDir + clipName + ".anim");
        if (clip == null) return null;
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (b.type != typeof(SpriteRenderer) || b.propertyName != "m_Sprite") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            var frames = new List<Sprite>();
            var times = new List<float>();
            foreach (var k in keys)
            {
                if (k.value is Sprite s) { frames.Add(s); times.Add(k.time); }
            }
            if (frames.Count == 0) return null;
            return new SlashTestDriver.Combo
            {
                name = clipName,
                frames = frames.ToArray(),
                times = times.ToArray(),
                length = clip.length,
            };
        }
        return null;
    }

    // 지금 쓰는 URP 에셋의 렌더러 목록에서 ScreenSlash 패스가 있는 렌더러를 찾는다.
    // 없으면 게임 카메라와 같은 Renderer3D_LightingTest(이름에 3D 가 들어간 것), 그것도 없으면 기본 렌더러
    static int FindRendererIndex(out bool hasFeature, out string rendererName)
    {
        hasFeature = false;
        rendererName = "(기본)";
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return -1;

        var so = new SerializedObject(urp);
        var list = so.FindProperty("m_RendererDataList");
        if (list == null || !list.isArray) return -1;

        int fallback = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (data == null) continue;
            foreach (var f in data.rendererFeatures)
            {
                // [2026-10-22] 화면 가르기는 HitGlitchScreenFeature 의 Slash Material 칸으로 그린다
                bool ok = f is HitGlitchScreenFeature hg && hg.slashMaterial != null;
                if (ok)
                {
                    hasFeature = true;
                    rendererName = data.name;
                    return i;
                }
            }
            if (fallback < 0 && data.name.Contains("3D")) { fallback = i; rendererName = data.name; }
        }
        return fallback;
    }

    // 배경: 어두운 체크무늬 + 색 띠 (화면이 갈라지고 색이 갈라지는 게 잘 보이게)
    static Sprite MakeChecker()
    {
        string path = Gen + "/BG_Checker.png";
        const int w = 512, h = 288, cell = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var a = new Color(0.16f, 0.17f, 0.22f, 1f);
        var b = new Color(0.22f, 0.23f, 0.3f, 1f);
        Color[] stripes = { new Color(0.55f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.3f), new Color(0.25f, 0.35f, 0.6f) };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = ((x / cell + y / cell) % 2 == 0) ? a : b;
                int band = y / 24;
                if (band % 4 == 1) c = Color.Lerp(c, stripes[(band / 4) % 3], 0.6f);
                if (x % cell == 0 || y % cell == 0) c *= 0.75f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return ImportSprite(path, 24f);
    }

    static Sprite MakeWhite()
    {
        string path = Gen + "/White.png";
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return ImportSprite(path, 4f);
    }

    static Sprite ImportSprite(string path, float ppu)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
