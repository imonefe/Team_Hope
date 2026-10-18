using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 게임 설정 값을 한곳에서 저장·불러오기·적용한다. (정적 클래스 — 씬에 놓을 필요 없음)
///
/// - 게임이 켜질 때(첫 씬을 불러오기 전) 저장된 값을 자동으로 불러와 소리·화면에 적용한다.
/// - 값을 바꿀 때는 Set○○() 를 부른다 → 바로 적용 + 저장 + Changed 알림.
/// - 다른 스크립트는 값을 읽기만 하면 된다.
///     예) GameSettings.ShakeScale (화면 흔들림 배율), GameSettings.ShowDamageNumbers (데미지 숫자 표시)
/// - 무기 칸·조작키 줄처럼 설정에 따라 보였다 숨었다 해야 하는 것은 Changed 를 구독한다 (HudSettingVisibility 참고).
///
/// 저장: PlayerPrefs (키 이름은 전부 "EH_" 로 시작)
/// 소리 적용:
///   전체 음량 = AudioListener.volume (게임의 모든 소리에 곱해짐)
///   배경음    = AudioManager.bgmVolume
///   효과음    = AudioManager.sfxVolume (AudioManager.PlaySFX, 버튼 클릭음, 플레이어 소리, 발소리, 줍기 소리가 따름)
/// </summary>
public static class GameSettings
{
    // ── 기본값 (기본값으로 되돌리기 버튼이 쓰는 값) ─────────────────
    public const float DefaultMasterVolume = 1f;
    public const float DefaultBgmVolume = 0.7f;
    public const float DefaultSfxVolume = 1f;
    public const float DefaultShakeScale = 1f;

    // ── 현재 값 (읽기 전용 — 바꿀 때는 Set○○ 사용) ─────────────────
    public static float MasterVolume { get; private set; } = DefaultMasterVolume;
    public static float BgmVolume { get; private set; } = DefaultBgmVolume;
    public static float SfxVolume { get; private set; } = DefaultSfxVolume;
    /// <summary>화면 흔들림 배율 (0 = 안 흔들림, 1 = 원래대로)</summary>
    public static float ShakeScale { get; private set; } = DefaultShakeScale;
    public static bool Fullscreen { get; private set; } = true;
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }
    /// <summary>체력 바 끝의 무기 정보 칸 표시</summary>
    public static bool ShowWeaponInfo { get; private set; } = true;
    /// <summary>오른쪽 아래 조작키 안내 줄 표시</summary>
    public static bool ShowKeyHints { get; private set; } = true;
    /// <summary>적을 때릴 때 뜨는 데미지 숫자 표시</summary>
    public static bool ShowDamageNumbers { get; private set; } = true;

    /// <summary>설정 값이 하나라도 바뀌면 호출됨</summary>
    public static event Action Changed;

    const string K_Master = "EH_MasterVolume";
    const string K_Bgm = "EH_BgmVolume";
    const string K_Sfx = "EH_SfxVolume";
    const string K_Shake = "EH_ShakeScale";
    const string K_Full = "EH_Fullscreen";
    const string K_ResW = "EH_ResolutionWidth";
    const string K_ResH = "EH_ResolutionHeight";
    const string K_Weapon = "EH_ShowWeaponInfo";
    const string K_Keys = "EH_ShowKeyHints";
    const string K_Damage = "EH_ShowDamageNumbers";

    // 게임 시작 시 자동 실행 (첫 씬을 불러오기 전)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Changed = null;   // 플레이 모드를 다시 시작해도 예전 구독이 남지 않게
        Load();
        ApplyAudio();
        if (PlayerPrefs.HasKey(K_ResW)) ApplyScreen();   // 한 번이라도 저장한 적이 있을 때만 화면을 바꾼다
    }

    // ─────────────────────────────────────────────
    // 불러오기 / 저장
    // ─────────────────────────────────────────────
    static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(K_Master, DefaultMasterVolume);
        BgmVolume = PlayerPrefs.GetFloat(K_Bgm, DefaultBgmVolume);
        SfxVolume = PlayerPrefs.GetFloat(K_Sfx, DefaultSfxVolume);
        ShakeScale = PlayerPrefs.GetFloat(K_Shake, DefaultShakeScale);
        Fullscreen = PlayerPrefs.GetInt(K_Full, Screen.fullScreen ? 1 : 0) == 1;
        ResolutionWidth = PlayerPrefs.GetInt(K_ResW, Screen.width);
        ResolutionHeight = PlayerPrefs.GetInt(K_ResH, Screen.height);
        ShowWeaponInfo = PlayerPrefs.GetInt(K_Weapon, 1) == 1;
        ShowKeyHints = PlayerPrefs.GetInt(K_Keys, 1) == 1;
        ShowDamageNumbers = PlayerPrefs.GetInt(K_Damage, 1) == 1;
    }

    static void Save()
    {
        PlayerPrefs.SetFloat(K_Master, MasterVolume);
        PlayerPrefs.SetFloat(K_Bgm, BgmVolume);
        PlayerPrefs.SetFloat(K_Sfx, SfxVolume);
        PlayerPrefs.SetFloat(K_Shake, ShakeScale);
        PlayerPrefs.SetInt(K_Full, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(K_ResW, ResolutionWidth);
        PlayerPrefs.SetInt(K_ResH, ResolutionHeight);
        PlayerPrefs.SetInt(K_Weapon, ShowWeaponInfo ? 1 : 0);
        PlayerPrefs.SetInt(K_Keys, ShowKeyHints ? 1 : 0);
        PlayerPrefs.SetInt(K_Damage, ShowDamageNumbers ? 1 : 0);
        PlayerPrefs.Save();
    }

    static void Commit()
    {
        Save();
        Changed?.Invoke();
    }

    // ─────────────────────────────────────────────
    // 적용
    // ─────────────────────────────────────────────
    static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
        AudioManager.SetBGMVolume(BgmVolume);   // AudioManager 가 아직 없으면 무시됨 (생길 때 AudioManager.Awake 가 직접 읽어 감)
        AudioManager.SetSFXVolume(SfxVolume);
    }

    static void ApplyScreen()
    {
#if !UNITY_EDITOR
        // 에디터의 게임 화면은 해상도·전체 화면을 바꿀 수 없으므로 실제 빌드에서만 적용
        if (ResolutionWidth <= 0 || ResolutionHeight <= 0) return;
        Screen.SetResolution(ResolutionWidth, ResolutionHeight,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    // ─────────────────────────────────────────────
    // 바꾸기 (설정 창이 부름)
    // ─────────────────────────────────────────────
    public static void SetMasterVolume(float v)
    {
        MasterVolume = Mathf.Clamp01(v);
        AudioListener.volume = MasterVolume;
        Commit();
    }

    public static void SetBgmVolume(float v)
    {
        BgmVolume = Mathf.Clamp01(v);
        AudioManager.SetBGMVolume(BgmVolume);
        Commit();
    }

    public static void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        AudioManager.SetSFXVolume(SfxVolume);
        Commit();
    }

    public static void SetShakeScale(float v)
    {
        ShakeScale = Mathf.Clamp01(v);
        Commit();
    }

    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
        ApplyScreen();
        Commit();
    }

    public static void SetResolution(int width, int height)
    {
        ResolutionWidth = width;
        ResolutionHeight = height;
        ApplyScreen();
        Commit();
    }

    public static void SetShowWeaponInfo(bool on) { ShowWeaponInfo = on; Commit(); }
    public static void SetShowKeyHints(bool on) { ShowKeyHints = on; Commit(); }
    public static void SetShowDamageNumbers(bool on) { ShowDamageNumbers = on; Commit(); }

    /// <summary>모든 설정을 처음 값으로 되돌린다 (해상도는 모니터 기본 해상도, 전체 화면 켬)</summary>
    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMasterVolume;
        BgmVolume = DefaultBgmVolume;
        SfxVolume = DefaultSfxVolume;
        ShakeScale = DefaultShakeScale;
        Fullscreen = true;
        Resolution native = Screen.currentResolution;
        ResolutionWidth = native.width;
        ResolutionHeight = native.height;
        ShowWeaponInfo = true;
        ShowKeyHints = true;
        ShowDamageNumbers = true;
        ApplyAudio();
        ApplyScreen();
        Commit();
    }

    /// <summary>고를 수 있는 해상도 목록 (가로×세로가 같은 것은 하나로, 작은 것부터). 높이 600 미만은 뺀다.</summary>
    public static List<Vector2Int> GetResolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.height < 600) continue;
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        Vector2Int cur = new Vector2Int(ResolutionWidth, ResolutionHeight);
        if (cur.x > 0 && cur.y > 0 && !list.Contains(cur)) list.Add(cur);
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }
}
