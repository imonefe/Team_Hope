// =====================================================================================
//  EventHorizonLightingApplier.cs  (에디터 전용 — 반드시 Assets/…/Editor 폴더 안에 둘 것)
//
//  Event Horizon 프로젝트 SampleScene 의 라이팅 / 안개 / 환경광 / 스카이박스 / 볼륨(후처리)을
//  "지금 열려 있는 씬"에 원본과 똑같은 수치로 적용한다.
//  모든 수치는 원본 SampleScene.unity / 에셋 파일에서 그대로 옮겨 적은 값이다 (반올림 없음).
//
//  메뉴: Tools > EventHorizon 라이팅 > ...
//    1. 에디터 값 그대로 적용          : 원본 씬 인스펙터에 저장된 값 그대로
//    2. 플레이 화면 색(HP 100%)으로 적용 : 원본 게임 실행 중 실제로 보이는 색
//                                        (원본은 HP 연동 스크립트가 플레이 시작 시 색을 덮어씀)
//    3. (선택) 카메라 값 적용           : Main Camera 의 렌더링 관련 값
//
//  모든 변경은 Ctrl+Z 로 되돌릴 수 있다 (생성한 에셋 파일은 제외).
// =====================================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class EventHorizonLightingApplier
{
    const string Root = "Assets/EventHorizonLighting";
    const string ProfileFolder = Root + "/Profiles";
    const string SkyboxShaderName = "Galactic Studios/Ultra Skybox Fog";
    const string SkyboxCubemapPath = Root + "/Skybox/rustig_koppie_4k.hdr";
    const string SkyboxMaterialPath = Root + "/EH_Skybox_nomer.mat";
    const string LightingSettingsPath = Root + "/EH_LightingSettings.lighting";
    const string GlobalProfilePath = ProfileFolder + "/EH_Global Volume Profile.asset";
    const string LowHpProfilePath = ProfileFolder + "/EH_LowHealthVolume Profile.asset";

    // ───────────── 색: 에디터 값 / 플레이 화면(HP 100%) 값 ─────────────
    // 원본 씬에 저장된 값
    static readonly Color FogColor_Editor        = new Color(1f, 0.8311249f, 0.6839622f, 1f);
    static readonly Color NaturalColor_Editor    = new Color(0.8867924f, 0.7862694f, 0.6734603f, 1f);
    static readonly Color ReflectedColor_Editor  = new Color(0.2551175f, 0.39321536f, 0.46226418f, 1f);
    static readonly Color SkyTint_Editor         = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    // 원본 플레이 중 HP 100% 일 때 스크립트(Fog/Light/SkyboxHpColorizer)가 덮어쓰는 값
    static readonly Color FogColor_Play          = new Color(0.7647059f, 0.72156864f, 0.67058825f, 0f);
    static readonly Color NaturalColor_Play      = new Color(0.90588236f, 0.7529412f, 0.5803922f, 1f);
    static readonly Color ReflectedColor_Play    = new Color(0.25490198f, 0.39215687f, 0.4627451f, 1f);
    static readonly Color SkyTint_Play           = new Color(0.5019608f, 0.49803922f, 0.49019608f, 1f);

    [MenuItem("Tools/EventHorizon 라이팅/1. 에디터 값 그대로 적용")]
    static void ApplyEditorValues() => Apply(false);

    [MenuItem("Tools/EventHorizon 라이팅/2. 플레이 화면 색(HP 100%)으로 적용")]
    static void ApplyPlayValues() => Apply(true);

    static readonly List<string> report = new List<string>();

    static void Apply(bool playLook)
    {
        report.Clear();
        report.Add(playLook ? "■ 모드: 플레이 화면 색(HP 100%)" : "■ 모드: 에디터 값 그대로");

        EnsureFolder(Root);
        EnsureFolder(ProfileFolder);

        Material sky = BuildSkyboxMaterial(playLook ? SkyTint_Play : SkyTint_Editor);
        ApplyRenderSettings(sky, playLook ? FogColor_Play : FogColor_Editor);
        ApplyLightingSettingsAsset();

        GameObject group = GetOrCreateRoot("Light", new Vector3(4.1873364f, -2.369138f, 0f), Quaternion.identity, Vector3.one);
        GameObject natural = CreateDirectional(group, "Naturallight",
            new Vector3(-4.1873364f, 2.369138f, 0f),
            new Quaternion(0.40821794f, -0.23456973f, 0.10938166f, 0.8754261f),
            playLook ? NaturalColor_Play : NaturalColor_Editor, 2f);
        GameObject reflected = CreateDirectional(group, "Reflectedlight",
            new Vector3(-4.1873364f, 2.369138f, 0f),
            new Quaternion(-0.5954358f, 0.047071338f, -0.37489584f, 0.709009f),
            playLook ? ReflectedColor_Play : ReflectedColor_Editor, 5f);

        VolumeProfile globalProfile = BuildGlobalProfile();
        VolumeProfile lowHpProfile = BuildLowHealthProfile();
        GameObject globalVol = CreateVolume(group, "Global Volume", new Vector3(-4.1873364f, 2.369138f, 0f), 1f, 0.052f, globalProfile);
        GameObject lowVol = CreateVolume(group, "LowHealthVolume", new Vector3(-6.3183866f, -4.014963f, 12.695545f), 2f, 0f, lowHpProfile);

        DisableOtherDirectionalLights(new[] { natural, reflected });
        DisableOtherGlobalVolumes(new[] { globalVol, lowVol });
        ApplyPlayerPointLights();

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        report.Add("완료 — 씬을 저장(Ctrl+S)해야 유지됩니다.");
        Debug.Log("[EventHorizon 라이팅]\n" + string.Join("\n", report));
    }

    // ───────────── 스카이박스 머티리얼 (원본: Etc/Ultra Skybox Fog/Materials/nomer.mat) ─────────────
    static Material BuildSkyboxMaterial(Color tint)
    {
        Shader shader = Shader.Find(SkyboxShaderName);
        if (shader == null)
        {
            report.Add("!! 셰이더 '" + SkyboxShaderName + "' 를 못 찾음 → 스카이박스 적용 건너뜀 (Skybox 폴더 복사 확인)");
            return null;
        }

        Cubemap cube = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyboxCubemapPath);
        if (cube == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("rustig_koppie_4k"))
            {
                cube = AssetDatabase.LoadAssetAtPath<Cubemap>(AssetDatabase.GUIDToAssetPath(guid));
                if (cube != null) break;
            }
        }
        if (cube == null) report.Add("!! 큐브맵 rustig_koppie_4k.hdr 를 못 찾음 (텍스처 형태가 Cube 인지 확인)");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "EH_Skybox_nomer" };
            AssetDatabase.CreateAsset(mat, SkyboxMaterialPath);
        }
        mat.shader = shader;
        mat.SetColor("_Tint", tint);
        mat.SetFloat("_Exposure", 1f);
        mat.SetFloat("_Rotation", 90f);
        if (cube != null) mat.SetTexture("_Tex", cube);
        mat.SetColor("_FogCol", new Color(0.7169812f, 0.6327133f, 0.58508366f, 0.5f));
        mat.SetFloat("_FogStart", 0.15f);
        mat.SetFloat("_FogEnd", 0.04f);
        mat.SetFloat("_FogIntens", 0.62f);
        mat.SetFloat("_MieIntens", 2f);
        mat.SetColor("_MieTint", new Color(0.8207547f, 0.77800876f, 0.7549395f, 0.5f));
        mat.SetFloat("_MieSize", 1f);
        mat.SetFloat("_SunDir", 0f);
        mat.SetFloat("_FogBottom", 1f);
        mat.SetFloat("_TopFog", 1f);
        mat.SetColor("_TopFogCol", new Color(0.8113208f, 0.8054348f, 0.77687794f, 0.5f));
        mat.SetFloat("_TopFogStart", 0.62f);
        mat.SetFloat("_TopFogEnd", 0.48f);
        mat.SetFloat("_TopFogIntens", 1f);
        EditorUtility.SetDirty(mat);
        report.Add("스카이박스 머티리얼: " + SkyboxMaterialPath);
        return mat;
    }

    // ───────────── Lighting 창 > Environment (원본 RenderSettings) ─────────────
    static void ApplyRenderSettings(Material sky, Color fogColor)
    {
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.sun = null;

        // Environment Lighting: Source = Color (Flat)
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f, 1f);

        // Environment Reflections: Source = Skybox, 128, Intensity 1, Bounces 1
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 128;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.reflectionBounces = 1;

        // Fog: 켬, Linear, 시작 57, 끝 193 (Density 0.01 은 Linear 에선 안 쓰이지만 원본 그대로 넣음)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = 57f;
        RenderSettings.fogEndDistance = 193f;
        RenderSettings.fogDensity = 0.01f;

        // Other Settings
        RenderSettings.haloStrength = 0.5f;
        RenderSettings.flareStrength = 1f;
        RenderSettings.flareFadeSpeed = 3f;

        report.Add("Environment(안개/환경광/반사/스카이박스) 적용");
    }

    // 원본: Baked GI 끔, Realtime GI 끔, Auto Generate 끔 (라이트맵 굽지 않음)
    static void ApplyLightingSettingsAsset()
    {
        LightingSettings ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (ls == null)
        {
            ls = new LightingSettings { name = "EH_LightingSettings" };
            AssetDatabase.CreateAsset(ls, LightingSettingsPath);
        }
        ls.bakedGI = false;
        ls.realtimeGI = false;
        ls.autoGenerate = false;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
        report.Add("Lighting Settings: Baked GI 끔 / Realtime GI 끔 / Auto Generate 끔");
    }

    // ───────────── 라이트 ─────────────
    static GameObject CreateDirectional(GameObject parent, string name, Vector3 localPos, Quaternion localRot, Color color, float intensity)
    {
        GameObject go = GetOrCreateChild(parent, name, localPos, localRot, Vector3.one);
        go.layer = 0;
        Light l = go.GetComponent<Light>();
        if (l == null) l = Undo.AddComponent<Light>(go);
        Undo.RecordObject(l, "EH Light");
        l.enabled = true;
        l.type = LightType.Directional;
        l.color = color;
        l.useColorTemperature = false;
        l.colorTemperature = 6570f;
        l.intensity = intensity;
        l.bounceIntensity = 1f;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.Auto;
        l.cullingMask = -1; // Everything
        l.lightmapBakeType = LightmapBakeType.Realtime;
        report.Add($"라이트 {name}: 색 {color} / 세기 {intensity}");
        return go;
    }

    static void ApplyPlayerPointLights()
    {
        GameObject player = null;
        try { player = GameObject.FindGameObjectWithTag("Player"); } catch (UnityException) { }
        if (player == null) { report.Add("플레이어(태그 Player) 없음 → 플레이어 포인트 라이트 2개 건너뜀"); return; }

        int playerLayer = LayerMask.NameToLayer("Player");

        // Point Light: 흰색, 세기 7, 범위 10, Player 레이어만 비춤
        GameObject p0 = GetOrCreateChild(player, "Point Light", new Vector3(0f, 1.473f, -1.861f), Quaternion.identity, Vector3.one);
        SetupPoint(p0, new Color(1f, 1f, 1f, 1f), playerLayer >= 0 ? (1 << playerLayer) : -1);
        // Point Light (1): 하늘색, 세기 7, 범위 10, 모든 레이어
        GameObject p1 = GetOrCreateChild(player, "Point Light (1)", new Vector3(-0.58f, 0.55f, 0.001f), Quaternion.identity, Vector3.one);
        SetupPoint(p1, new Color(0f, 0.76471734f, 1f, 1f), -1);
        if (playerLayer >= 0) { p0.layer = playerLayer; p1.layer = playerLayer; }
        else report.Add("!! 'Player' 레이어가 없어 Point Light 의 비춤 대상을 Everything 으로 둠");
        report.Add("플레이어 포인트 라이트 2개 → " + player.name + " 밑에 적용 (원본 플레이어 스케일 1.2 기준 위치)");
    }

    static void SetupPoint(GameObject go, Color c, int mask)
    {
        Light l = go.GetComponent<Light>();
        if (l == null) l = Undo.AddComponent<Light>(go);
        Undo.RecordObject(l, "EH Point");
        l.enabled = true;
        l.type = LightType.Point;
        l.color = c;
        l.useColorTemperature = false;
        l.intensity = 7f;
        l.range = 10f;
        l.bounceIntensity = 1f;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.Auto;
        l.cullingMask = mask;
        l.lightmapBakeType = LightmapBakeType.Realtime;
    }

    // ───────────── 볼륨(후처리) ─────────────
    static GameObject CreateVolume(GameObject parent, string name, Vector3 localPos, float priority, float weight, VolumeProfile profile)
    {
        GameObject go = GetOrCreateChild(parent, name, localPos, Quaternion.identity, Vector3.one);
        go.layer = 0; // Default — 카메라 Volume Mask(Default)에 잡혀야 함
        Volume v = go.GetComponent<Volume>();
        if (v == null) v = Undo.AddComponent<Volume>(go);
        Undo.RecordObject(v, "EH Volume");
        v.enabled = true;
        v.isGlobal = true;
        v.priority = priority;
        v.blendDistance = 0f;
        v.weight = weight;
        v.sharedProfile = profile;
        report.Add($"볼륨 {name}: Global / Priority {priority} / Weight {weight}");
        return go;
    }

    // 원본: Assets/0.Scenes/SampleScene/Global Volume Profile.asset — Bloom 하나
    static VolumeProfile BuildGlobalProfile()
    {
        VolumeProfile p = NewProfile(GlobalProfilePath);
        Bloom b = AddComp<Bloom>(p);
        b.threshold.Override(100f);
        b.intensity.Override(48.48f);
        b.scatter.Override(0.133f);
        b.tint.Override(new Color(1f, 1f, 1f, 1f));
        EditorUtility.SetDirty(b);
        return p;
    }

    // 원본: Assets/0.Scenes/SampleScene/LowHealthVolume Profile.asset — Vignette + Color Adjustments
    static VolumeProfile BuildLowHealthProfile()
    {
        VolumeProfile p = NewProfile(LowHpProfilePath);
        Vignette vg = AddComp<Vignette>(p);
        vg.color.Override(new Color(0.16153437f, 0.20391288f, 0.20754719f, 1f));
        vg.intensity.Override(0f);
        vg.smoothness.Override(0.2f);
        EditorUtility.SetDirty(vg);
        ColorAdjustments ca = AddComp<ColorAdjustments>(p);
        ca.colorFilter.Override(new Color(0.14150941f, 0.11414203f, 0.11414203f, 1f));
        EditorUtility.SetDirty(ca);
        return p;
    }

    static VolumeProfile NewProfile(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
            AssetDatabase.DeleteAsset(path); // 다시 실행해도 항상 원본과 같은 상태가 되도록 새로 만든다
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        return p;
    }

    static T AddComp<T>(VolumeProfile p) where T : VolumeComponent
    {
        T c = p.Add<T>(false); // 모든 항목 Override 꺼진 상태로 추가 → 원본에서 켠 항목만 아래에서 켬
        c.name = typeof(T).Name;
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        EditorUtility.SetDirty(p);
        return c;
    }

    // 원본 씬에는 위 두 디렉셔널 라이트 말고 다른 디렉셔널 라이트가 없다 → 같은 모습을 위해 끈다
    static void DisableOtherDirectionalLights(GameObject[] keep)
    {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional || !l.enabled || System.Array.IndexOf(keep, l.gameObject) >= 0) continue;
            Undo.RecordObject(l, "EH Disable Light");
            l.enabled = false;
            report.Add("기존 디렉셔널 라이트 끔: " + l.gameObject.name);
        }
    }

    // 원본 씬에는 위 두 볼륨 말고 다른 Global Volume 이 없다 → 섞이지 않게 끈다
    static void DisableOtherGlobalVolumes(GameObject[] keep)
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!v.isGlobal || !v.enabled || System.Array.IndexOf(keep, v.gameObject) >= 0) continue;
            Undo.RecordObject(v, "EH Disable Volume");
            v.enabled = false;
            report.Add("기존 Global Volume 끔: " + v.gameObject.name);
        }
    }

    // ───────────── (선택) 카메라 ─────────────
    [MenuItem("Tools/EventHorizon 라이팅/3. (선택) 카메라 값 적용 — Main Camera")]
    static void ApplyCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[EventHorizon 라이팅] MainCamera 태그 카메라가 없습니다."); return; }
        Undo.RecordObject(cam, "EH Camera");
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
        cam.orthographic = false;
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cam.allowHDR = true;
        cam.allowMSAA = false;
        cam.allowDynamicResolution = false;
        cam.useOcclusionCulling = false;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "EH Camera Data");
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        data.stopNaN = false;
        data.dithering = false;
        data.renderShadows = true;
        data.volumeLayerMask = 1 << 0; // Default
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("[EventHorizon 라이팅] 카메라 적용: " + cam.name + " — Clear Flags Skybox, FOV 25, HDR 켬, MSAA 끔, 후처리 켬, Volume Mask = Default\n" +
                  "※ 위치/컬링 마스크/렌더러는 프로젝트마다 달라서 바꾸지 않았습니다 (설명서 참고).");
    }

    // ───────────── 도우미 ─────────────
    static GameObject GetOrCreateRoot(string name, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject go = null;
        foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) { go = r; break; }
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "EH Create");
        }
        Undo.RecordObject(go.transform, "EH Transform");
        go.transform.SetLocalPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        return go;
    }

    static GameObject GetOrCreateChild(GameObject parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale)
    {
        Transform t = parent.transform.Find(name);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "EH Create");
            Undo.SetTransformParent(go.transform, parent.transform, "EH Parent");
        }
        else go = t.gameObject;
        Undo.RecordObject(go.transform, "EH Transform");
        go.transform.SetLocalPositionAndRotation(localPos, localRot);
        go.transform.localScale = scale;
        go.SetActive(true);
        return go;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
