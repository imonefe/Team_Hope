using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 어둠(어둠 + 빛줄기) 상태를 한곳에서 관리하는 정적 클래스.
///
/// - 방 프리팹에 붙은 RoomDarkness 가 켜지면 등록되고, 꺼지거나 방이 지워지면 빠진다.
/// - 등록된 것이 있으면 어둠이 서서히 켜지고, 없으면 서서히 꺼진다. (방 전환 때 부드럽게)
/// - 실제 화면 처리는 DarknessScreenFeature(렌더러 기능)가 여기 값을 읽어서 한다.
/// - 편집 중(재생 안 함)에는 전환 없이 RoomDarkness 의 값을 바로 보여준다. (게임 화면 미리보기)
/// </summary>
public static class DarknessSystem
{
    /// <summary>어둠 한 벌의 설정 값. RoomDarkness 가 만들어 넘겨준다.</summary>
    public struct Settings
    {
        public float darkness;          // 0 = 평소 밝기, 1 = 완전 깜깜
        public Color ambientTint;       // 어두운 곳의 색감
        public float maxBrightness;     // 빛이 겹칠 때 최대 밝기 (1 = 원래 밝기)

        public static Settings Lerp(Settings a, Settings b, float t)
        {
            return new Settings
            {
                darkness = Mathf.Lerp(a.darkness, b.darkness, t),
                ambientTint = Color.Lerp(a.ambientTint, b.ambientTint, t),
                maxBrightness = Mathf.Lerp(a.maxBrightness, b.maxBrightness, t),
            };
        }
    }

    static readonly List<RoomDarkness> sources = new List<RoomDarkness>();
    static float fade;                 // 재생 중 전환 값 (0 = 꺼짐, 1 = 켜짐)
    static Settings current;           // 재생 중 화면에 쓰이는 값 (방이 바뀌면 새 값으로 서서히 이동)
    static bool hasCurrent;
    static GameObject driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sources.Clear();
        fade = 0f;
        hasCurrent = false;
        driver = null;
    }

    public static void Register(RoomDarkness source)
    {
        if (source == null || sources.Contains(source)) return;
        sources.Add(source);
        if (Application.isPlaying) EnsureDriver();
    }

    public static void Unregister(RoomDarkness source)
    {
        sources.Remove(source);
    }

    /// <summary>지금 적용 중인 RoomDarkness (가장 나중에 켜진 것). 없으면 null.</summary>
    public static RoomDarkness ActiveSource
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

    /// <summary>어둠이 얼마나 켜져 있는지 (0~1). 0이면 렌더러 기능이 아무것도 하지 않는다.</summary>
    public static float Fade
    {
        get
        {
            if (Application.isPlaying) return fade;
            RoomDarkness s = ActiveSource;
            return s != null && s.previewInEditor ? 1f : 0f;
        }
    }

    public static bool IsActive => Fade > 0.001f;

    /// <summary>지금 화면에 쓸 설정 값.</summary>
    public static Settings Current
    {
        get
        {
            if (!Application.isPlaying || !hasCurrent)
            {
                RoomDarkness s = ActiveSource;
                if (s != null) return s.ToSettings();
            }
            return current;
        }
    }

    /// <summary>재생 중 매 프레임 호출 — 켜고 끄기와 방 사이 값 변화를 부드럽게.</summary>
    static void Tick(float dt)
    {
        RoomDarkness s = ActiveSource;
        if (s != null)
        {
            Settings target = s.ToSettings();
            if (!hasCurrent || fade <= 0.001f)
            {
                current = target;      // 꺼진 상태에서 켜질 땐 값은 바로, 밝기만 서서히
                hasCurrent = true;
            }
            else
            {
                current = Settings.Lerp(current, target, 1f - Mathf.Exp(-dt * 6f));
            }
            fade = s.fadeInTime <= 0f ? 1f : Mathf.MoveTowards(fade, 1f, dt / s.fadeInTime);
        }
        else
        {
            float t = lastFadeOutTime;
            fade = t <= 0f ? 0f : Mathf.MoveTowards(fade, 0f, dt / t);
        }
        if (s != null) lastFadeOutTime = s.fadeOutTime;
    }

    static float lastFadeOutTime = 0.4f;

    static void EnsureDriver()
    {
        if (driver != null) return;
        driver = new GameObject("[DarknessSystem]");
        driver.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        Object.DontDestroyOnLoad(driver);
        driver.AddComponent<Driver>();
    }

    /// <summary>재생 중 Tick 을 불러주는 숨은 오브젝트. (시간 정지 중에도 전환되도록 실제 시간 사용)</summary>
    class Driver : MonoBehaviour
    {
        void Update() { Tick(Time.unscaledDeltaTime); }
        void OnDestroy() { if (driver == gameObject) driver = null; }
    }
}
