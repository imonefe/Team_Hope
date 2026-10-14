using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    static float curSky = 1f, curSpriteMin = 0.06f; static bool curSpriteLighting = true;

    // 마지막으로 쓰던 설정 (방을 나가 서서히 밝아지는 동안에도 계속 쓰기 위해)
    static Color curAmbient, curFog;
    static float curReflection = 0f, curFogStart, curFogEnd, lastFadeOut = 0.5f;
    static bool curOverrideFog;
    static RoomLighting curSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sources.Clear(); swapped.Clear(); skyboxInstance = null; baseSkybox = null;
        driver = null; fade = 0f; captured = false; hasAppliedFog = false; curSource = null;
    }

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
        void OnDestroy() { if (driver == gameObject) driver = null; RestoreAll(); }
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
    }

    static void RestoreSprites()
    {
        foreach (var kv in swapped) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
        swapped.Clear();
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
