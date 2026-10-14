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
        InitSingleton();
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
