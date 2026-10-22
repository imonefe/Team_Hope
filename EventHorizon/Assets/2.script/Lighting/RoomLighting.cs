using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Spine.Unity;

/// <summary>
/// 이 컴포넌트가 붙은 방에 들어가면 씬의 "환경광·하늘·안개"만 어둡게 만든다. 빛 연출은 방 안에 둔 빛으로 따로 한다.
/// 방 프리팹의 최상위(Room 컴포넌트가 있는 오브젝트)에 붙이면 된다.
///
/// 어두워지는 것
///  - 환경광(앰비언트): 지정한 어두운 색으로 (모든 3D 물체의 바탕 밝기)
///  - 반사광: 지정한 세기로 (기본 0)
///  - 안개: 지정한 어두운 색으로 (선택: 시작·끝 거리도 바꿈)
///  - 하늘(스카이박스): 지정한 밝기 배율로 (원본 머티리얼은 건드리지 않고 복사본만 바꿈)
///  - 기본 스프라이트(Sprite-Lit-Default): 이 방에 있는 동안만 빛을 받는 셰이더로 바꿔서 같이 어두워짐
/// 그대로인 것
///  - 모든 빛(Light 컴포넌트): 세기·색 등 어떤 값도 코드가 바꾸지 않음. 씬·프리팹에 세팅한 값 그대로
///
/// 참고
///  - 재생 중에만 적용된다. (편집 중에 씬 조명을 바꾸면 그 값이 씬 파일에 저장돼 버리기 때문)
///  - 방을 나가면 서서히 원래 값으로 돌아온다.
///  - 2D 스프라이트는 Sprite-Lit-Default 를 쓰는 것만 바꾼다. 다른 셰이더(툰 셰이더, URP Lit 등)는 원래 빛을 받으므로 그대로.
/// </summary>
[DisallowMultipleComponent]
public class RoomLighting : MonoBehaviour
{
    [Header("환경광 (앰비언트)")]
    [Tooltip("모든 3D 물체의 바탕 밝기. 검정에 가까울수록 빛이 닿지 않는 곳이 깜깜해짐")]
    public Color ambientColor = new Color(0.02f, 0.025f, 0.035f, 1f);
    [Tooltip("반사광 세기 (0 = 반사 없음)")]
    [Range(0f, 1f)] public float reflectionIntensity = 0f;

    [Header("안개")]
    [Tooltip("어두운 방의 안개 색. 완전 검정보다 아주 어두운 남색 정도가 공간감이 남음")]
    public Color fogColor = new Color(0.015f, 0.02f, 0.03f, 1f);
    [Tooltip("켜면 아래 시작·끝 거리로 바꿈 (선형 안개일 때)")]
    public bool overrideFogDistance = false;
    [Min(0f)] public float fogStart = 30f;
    [Min(0f)] public float fogEnd = 150f;

    [Header("하늘")]
    [Tooltip("하늘(스카이박스) 밝기 배율. 0 = 깜깜, 1 = 원래 밝기")]
    [Range(0f, 1f)] public float skyboxBrightness = 0.08f;

    [Header("2D 스프라이트")]
    [Tooltip("조명을 받지 않던 기본 스프라이트(Sprite-Lit-Default)도 이 방에서는 빛을 받게 함 → 어두워지고, 방 안의 빛 근처만 밝아짐")]
    public bool spriteLighting = true;
    [Tooltip("빛이 하나도 없을 때 스프라이트가 유지하는 최소 밝기 (형태만 겨우 보이게)")]
    [Range(0f, 1f)] public float spriteMinBrightness = 0.06f;

    [Header("스파인 캐릭터 (보스·궁수 등) — 2026-10-21")]
    [Tooltip("빛이 하나도 없을 때 스파인 캐릭터가 유지하는 최소 밝기. 스프라이트보다 높게 두면 어두운 방에서도 형태·색이 살아 있음")]
    [Range(0f, 1f)] public float spineMinBrightness = 0.45f;
    [Tooltip("켜면 빛의 색은 무시하고 밝기만 받음 → 캐릭터 원래 색·채도 유지 (끄면 파란 환경광 등 빛 색까지 받아 채도가 낮아 보일 수 있음)")]
    public bool spineKeepColor = true;

    [Header("전환")]
    [Min(0f)] public float fadeInTime = 0.5f;
    [Min(0f)] public float fadeOutTime = 0.5f;

    void OnEnable() { if (Application.isPlaying) LightingDarkness.Register(this); }
    void OnDisable() { LightingDarkness.Unregister(this); }
}

/// <summary>
/// RoomLighting 들의 실제 처리. 원래 값을 기억해 두었다가 어둡게 하고, 방을 나가면 되돌린다.
/// 다른 스크립트(FogHpColorizer 등)가 안개 색을 바꿔도, 그 바뀐 값 위에 어둠을 덮어쓰도록
/// 매 프레임 마지막(LateUpdate)에 적용한다.
/// </summary>
public static class LightingDarkness
{
    static readonly List<RoomLighting> sources = new List<RoomLighting>();
    static GameObject driver;
    static float fade;                 // 0 = 원래 밝기, 1 = 완전히 어두움
    static bool captured;              // 원래 값을 기억해 둔 상태인지

    // 원래 값
    static AmbientMode baseAmbientMode;
    static Color baseAmbientLight, baseAmbientSky, baseAmbientEquator, baseAmbientGround;
    static float baseAmbientIntensity, baseReflection, baseFogStart, baseFogEnd;
    static Color sourceFog;            // 어둠을 덮기 전 안개 색 (다른 스크립트가 바꾸면 따라감)
    static Color lastAppliedFog;
    static bool hasAppliedFog;

    static float rescanTimer;

    static SphericalHarmonicsL2 baseProbe;
    static Material baseSkybox, skyboxInstance;
    static float baseSkyExposure = 1f; static Color baseSkyTint = Color.white;
    static Material litSpriteMat;
    static readonly Dictionary<Renderer, Material> swapped = new Dictionary<Renderer, Material>();
    static readonly int SpriteBlendId = Shader.PropertyToID("_EH_SpriteLightBlend");
    static readonly int SpriteMinId = Shader.PropertyToID("_EH_SpriteMinLight");
    static readonly int SpineMinId = Shader.PropertyToID("_EH_SpineMinLight");
    static readonly int SpineKeepColorId = Shader.PropertyToID("_EH_SpineKeepColor");
    static float curSpineMin = 0.45f; static bool curSpineKeepColor = true;
    static float curSky = 1f, curSpriteMin = 0.06f; static bool curSpriteLighting = true;
    // [2026-10-21 추가] 스파인 캐릭터(보스·궁수 등)용: 원래 머티리얼 → 빛 받는 머티리얼(SpineLit3D) 바꿔 끼우기
    static Material litSpineTemplate;
    static readonly Dictionary<Material, Material> spineLitCache = new Dictionary<Material, Material>();
    static readonly Dictionary<SkeletonRenderer, List<Material>> spineSwapped = new Dictionary<SkeletonRenderer, List<Material>>();

    // 마지막으로 쓰던 설정 (방을 나가 서서히 밝아지는 동안에도 계속 쓰기 위해)
    static Color curAmbient, curFog;
    static float curReflection = 0f, curFogStart, curFogEnd, lastFadeOut = 0.5f;
    static bool curOverrideFog;
    static RoomLighting curSource;
    // [2026-10-15] 플레이 종료·게임 종료 중인지. 이때는 씬 조명 설정(RenderSettings)이 먼저 사라져 있어서 건드리면 오류가 난다
    static bool quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sources.Clear(); swapped.Clear(); skyboxInstance = null; baseSkybox = null;
        driver = null; fade = 0f; captured = false; hasAppliedFog = false; curSource = null;
        quitting = false;
        spineSwapped.Clear(); spineLitCache.Clear();
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // [2026-10-21 추가] 씬을 새로 불러오면(다시하기 등) 옛 씬에서 기억해 둔 값을 버린다.
    // 예전엔 기억해 둔 상태(captured)를 그대로 들고 가서, 새 씬의 하늘·조명 원래 값을 다시 기억하지 않았고
    // 옛 씬의 하늘 복사본만 어둡게 하느라 새 씬은 밝은 채로 남았다.
    // 어둠 정도(fade)는 유지 → 새 씬의 첫 방에서 원래 값을 다시 기억하자마자 바로 어둡게 (밝게 번쩍이지 않음)
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !captured) return;
        if (skyboxInstance != null) Object.Destroy(skyboxInstance);
        skyboxInstance = null; baseSkybox = null;
        swapped.Clear();
        spineSwapped.Clear();
        captured = false; hasAppliedFog = false; curSource = null;
    }

    static void OnQuitting() { quitting = true; }

    public static void Register(RoomLighting s)
    {
        if (s == null || sources.Contains(s)) return;
        sources.Add(s);
        EnsureDriver();
    }

    public static void Unregister(RoomLighting s) { sources.Remove(s); }


    static RoomLighting Active
    {
        get
        {
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (sources[i] == null) { sources.RemoveAt(i); continue; }
                if (sources[i].isActiveAndEnabled) return sources[i];
            }
            return null;
        }
    }

    static void EnsureDriver()
    {
        if (driver != null) return;
        driver = new GameObject("[LightingDarkness]");
        driver.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        Object.DontDestroyOnLoad(driver);
        driver.AddComponent<Driver>();
    }

    class Driver : MonoBehaviour
    {
        void LateUpdate() { Tick(Time.unscaledDeltaTime); }
        void OnDestroy()
        {
            if (driver == gameObject) driver = null;
            if (quitting)
            {
                // [2026-10-15] 종료 중: 씬 조명은 유니티가 알아서 원래대로 돌리므로 건드리지 않는다.
                // 스프라이트 밝기 전역값만은 플레이가 끝나도 편집 화면에 남기 때문에 되돌린다.
                Shader.SetGlobalFloat(SpriteBlendId, 0f);
                captured = false; hasAppliedFog = false; fade = 0f;
                return;
            }
            RestoreAll();
        }
    }

    static void Tick(float dt)
    {
        RoomLighting s = Active;
        if (s != null)
        {
            if (!captured) Capture();
            if (s != curSource) { curSource = s; RescanLights(); }
            curAmbient = s.ambientColor; curFog = s.fogColor; curReflection = s.reflectionIntensity;
            curOverrideFog = s.overrideFogDistance; curFogStart = s.fogStart; curFogEnd = s.fogEnd;
            lastFadeOut = s.fadeOutTime;
            curSky = s.skyboxBrightness; curSpriteLighting = s.spriteLighting; curSpriteMin = s.spriteMinBrightness;
            curSpineMin = s.spineMinBrightness; curSpineKeepColor = s.spineKeepColor;
            fade = s.fadeInTime <= 0f ? 1f : Mathf.MoveTowards(fade, 1f, dt / s.fadeInTime);
        }
        else
        {
            if (!captured) return;
            curSource = null;
            fade = lastFadeOut <= 0f ? 0f : Mathf.MoveTowards(fade, 0f, dt / lastFadeOut);
            if (fade <= 0f) { RestoreAll(); return; }
        }

        // 새로 생긴 빛도 잡도록 가끔 다시 찾음
        rescanTimer -= dt;
        if (rescanTimer <= 0f) RescanLights();

        Apply();
    }

    static void Capture()
    {
        baseAmbientMode = RenderSettings.ambientMode;
        baseAmbientLight = RenderSettings.ambientLight;
        baseAmbientSky = RenderSettings.ambientSkyColor;
        baseAmbientEquator = RenderSettings.ambientEquatorColor;
        baseAmbientGround = RenderSettings.ambientGroundColor;
        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseReflection = RenderSettings.reflectionIntensity;
        baseFogStart = RenderSettings.fogStartDistance;
        baseFogEnd = RenderSettings.fogEndDistance;
        sourceFog = RenderSettings.fogColor;
        hasAppliedFog = false;
        baseProbe = RenderSettings.ambientProbe;

        // 하늘: 원본 머티리얼은 그대로 두고 복사본을 만들어 바꿈
        baseSkybox = RenderSettings.skybox;
        if (baseSkybox != null)
        {
            skyboxInstance = new Material(baseSkybox) { name = baseSkybox.name + " (어두운 방)" };
            if (skyboxInstance.HasProperty("_Exposure")) baseSkyExposure = skyboxInstance.GetFloat("_Exposure");
            if (skyboxInstance.HasProperty("_Tint")) baseSkyTint = skyboxInstance.GetColor("_Tint");
            RenderSettings.skybox = skyboxInstance;
        }
        captured = true;
    }

    static void RescanLights()
    {
        rescanTimer = 1f;
        // 새로 생긴 스프라이트도 빛을 받게 바꿔 줌 (빛 자체는 건드리지 않음)
        if (curSpriteLighting) SwapSprites();
    }


    /// <summary>조명을 받지 않는 기본 스프라이트를, 빛을 받는 스프라이트 셰이더로 잠시 바꿈 (방을 나가면 되돌림)</summary>
    static void SwapSprites()
    {
        if (litSpriteMat == null) litSpriteMat = Resources.Load<Material>("SpriteLit3D");
        if (litSpriteMat == null) return;
        foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
        {
            if (r == null) continue;
            if (!(r is SpriteRenderer) && r.GetType().Name != "TilemapRenderer") continue;
            if (r.gameObject.layer == 5) continue;                                          // UI 층 제외
            Material m = r.sharedMaterial;
            if (m == null || m.name != "Sprite-Lit-Default") continue;                      // 이미 바꿨거나(다른 스크립트가 잠시 바꾼 경우 포함) 대상 아님
            // 다른 스크립트(피격 반짝임 등)가 원래 머티리얼로 되돌려 놓은 경우도 여기서 다시 바꿈
            if (!swapped.ContainsKey(r)) swapped[r] = m;
            r.sharedMaterial = litSpriteMat;
        }
        SwapSpines();
    }

    /// <summary>[2026-10-21 추가] 스파인(SkeletonRenderer)은 매 프레임 머티리얼을 다시 정해서 직접 바꾸면 덮어써지므로,
    /// 스파인 공식 기능(CustomMaterialOverride)으로 '원래 머티리얼 → 빛 받는 머티리얼'을 등록한다. 스파인 파일·원본 머티리얼은 그대로.</summary>
    static void SwapSpines()
    {
        if (litSpineTemplate == null) litSpineTemplate = Resources.Load<Material>("SpineLit3D");
        if (litSpineTemplate == null) return;
        foreach (SkeletonRenderer sr in Object.FindObjectsOfType<SkeletonRenderer>())
        {
            if (sr == null || spineSwapped.ContainsKey(sr)) continue;
            if (sr.gameObject.layer == 5 || sr.skeletonDataAsset == null || sr.skeletonDataAsset.atlasAssets == null) continue;   // UI 층 제외
            List<Material> list = new List<Material>();
            foreach (AtlasAssetBase atlas in sr.skeletonDataAsset.atlasAssets)
            {
                if (atlas == null) continue;
                foreach (Material m in atlas.Materials)
                {
                    if (m == null || m.shader == null || m.shader.name != "Spine/Skeleton") continue;   // 기본 스파인 셰이더만
                    if (!spineLitCache.TryGetValue(m, out Material lit) || lit == null)
                    {
                        lit = new Material(litSpineTemplate) { name = m.name + " (빛 받음)" };
                        lit.mainTexture = m.mainTexture;
                        bool straight = m.HasProperty("_StraightAlphaInput") && m.GetFloat("_StraightAlphaInput") > 0.5f;
                        lit.SetFloat("_StraightAlphaInput", straight ? 1f : 0f);
                        if (straight) lit.EnableKeyword("_STRAIGHT_ALPHA_INPUT"); else lit.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                        lit.renderQueue = m.renderQueue;
                        spineLitCache[m] = lit;
                    }
                    sr.CustomMaterialOverride[m] = lit;
                    list.Add(m);
                }
            }
            spineSwapped[sr] = list;
        }
    }

    static void RestoreSprites()
    {
        foreach (var kv in swapped) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
        swapped.Clear();
        foreach (var kv in spineSwapped)
            if (kv.Key != null)
                foreach (Material m in kv.Value) kv.Key.CustomMaterialOverride.Remove(m);
        spineSwapped.Clear();
    }

    static void Apply()
    {
        float f = Mathf.Clamp01(fade);

        // 환경광: 단색 모드로 바꿔서 어두운 색으로
        RenderSettings.ambientMode = AmbientMode.Flat;
        Color baseFlat = baseAmbientMode == AmbientMode.Flat ? baseAmbientLight : baseAmbientSky;
        RenderSettings.ambientLight = Color.Lerp(baseFlat * baseAmbientIntensity, curAmbient, f);
        RenderSettings.reflectionIntensity = Mathf.Lerp(baseReflection, curReflection, f);
        // URP 셰이더는 환경광을 이 값(구면 조화)으로 읽으므로 직접 넣어 줌
        SphericalHarmonicsL2 dark = new SphericalHarmonicsL2();
        dark.AddAmbientLight(curAmbient);
        RenderSettings.ambientProbe = baseProbe * (1f - f) + dark * f;

        // 하늘
        if (skyboxInstance != null)
        {
            float k = Mathf.Lerp(1f, curSky, f);
            if (skyboxInstance.HasProperty("_Exposure")) skyboxInstance.SetFloat("_Exposure", baseSkyExposure * k);
            else if (skyboxInstance.HasProperty("_Tint")) skyboxInstance.SetColor("_Tint", baseSkyTint * k);
        }

        // 2D 스프라이트 (바꿔 끼운 셰이더가 이 두 값을 읽음)
        Shader.SetGlobalFloat(SpriteBlendId, curSpriteLighting ? f : 0f);
        Shader.SetGlobalFloat(SpriteMinId, curSpriteMin);
        Shader.SetGlobalFloat(SpineMinId, curSpineMin);
        Shader.SetGlobalFloat(SpineKeepColorId, curSpineKeepColor ? 1f : 0f);

        // 안개: 다른 스크립트가 바꿨으면 그 값을 새 기준으로 삼음
        Color nowFog = RenderSettings.fogColor;
        if (!hasAppliedFog || nowFog != lastAppliedFog) sourceFog = nowFog;
        lastAppliedFog = Color.Lerp(sourceFog, curFog, f);
        RenderSettings.fogColor = lastAppliedFog;
        hasAppliedFog = true;
        if (curOverrideFog)
        {
            RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, curFogStart, f);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, curFogEnd, f);
        }
        else
        {
            RenderSettings.fogStartDistance = baseFogStart;
            RenderSettings.fogEndDistance = baseFogEnd;
        }
    }

    static void RestoreAll()
    {
        if (!captured) return;
        RenderSettings.ambientMode = baseAmbientMode;
        RenderSettings.ambientLight = baseAmbientLight;
        RenderSettings.ambientSkyColor = baseAmbientSky;
        RenderSettings.ambientEquatorColor = baseAmbientEquator;
        RenderSettings.ambientGroundColor = baseAmbientGround;
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.reflectionIntensity = baseReflection;
        RenderSettings.fogStartDistance = baseFogStart;
        RenderSettings.fogEndDistance = baseFogEnd;
        if (hasAppliedFog && RenderSettings.fogColor == lastAppliedFog) RenderSettings.fogColor = sourceFog;
        RenderSettings.ambientProbe = baseProbe;
        if (skyboxInstance != null)
        {
            if (RenderSettings.skybox == skyboxInstance) RenderSettings.skybox = baseSkybox;
            Object.Destroy(skyboxInstance);
            skyboxInstance = null;
        }
        Shader.SetGlobalFloat(SpriteBlendId, 0f);
        RestoreSprites();
        captured = false; hasAppliedFog = false; fade = 0f;
    }
}
