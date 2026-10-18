using UnityEngine;

/// <summary>
/// BGM / SFX 통합 관리
/// 어디서든 AudioManager.PlaySFX() 로 호출 가능
/// </summary>
public class AudioManager : PersistentSingleton<AudioManager>
{
    [Header("BGM")]
    public AudioSource bgmSource;

    [Header("볼륨")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    void Awake()
    {
        if (!InitSingleton()) return;

        // [2026-10-18 추가] 설정 창에서 저장한 배경음·효과음 크기를 불러온다 (인스펙터 값보다 우선)
        bgmVolume = GameSettings.BgmVolume;
        sfxVolume = GameSettings.SfxVolume;

        // 코드로 자동 생성된 경우(씬에 AudioManager가 없는 인트로 등)엔 BGM용 AudioSource도 직접 만든다
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }
    }

    /// <summary>
    /// AudioManager가 있으면 그대로, 없으면 새로 만들어서 반환.
    /// (SceneBGM 이 씬에 AudioManager가 없을 때 사용 — 만들어진 건 씬이 바뀌어도 유지됨)
    /// </summary>
    public static AudioManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        new GameObject("AudioManager").AddComponent<AudioManager>(); // AddComponent 순간 Awake → Instance 등록
        return Instance;
    }

    // ── BGM ──────────────────────────────────────────────
    public static void PlayBGM(AudioClip clip)
    {
        if (Instance == null || clip == null) return;

        if (Instance.bgmSource.clip == clip && Instance.bgmSource.isPlaying) return;

        Instance.bgmSource.clip = clip;
        Instance.bgmSource.volume = Instance.bgmVolume;
        Instance.bgmSource.loop = true;
        Instance.bgmSource.Play();
    }

    public static void StopBGM()
    {
        Instance?.bgmSource.Stop();
    }

    public static void SetBGMVolume(float vol)
    {
        if (Instance == null) return;
        Instance.bgmVolume = Mathf.Clamp01(vol);
        Instance.bgmSource.volume = Instance.bgmVolume;
    }

    // ── SFX ──────────────────────────────────────────────
    /// <summary>
    /// SFX 재생. position을 안 넘기면 카메라 위치 기준(2D), 넘기면 그 월드 위치 기준(3D)으로 재생.
    /// </summary>
    public static void PlaySFX(AudioClip clip, Vector3? position = null)
    {
        if (Instance == null || clip == null) return;
        Vector3 pos = position ?? Camera.main.transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, Instance.sfxVolume);
    }

    public static void SetSFXVolume(float vol)
    {
        if (Instance != null)
            Instance.sfxVolume = Mathf.Clamp01(vol);
    }
}
