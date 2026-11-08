using UnityEngine;

/// <summary>
/// [2026-11-09] 스킬 효과음 재생 도우미.
/// - 화면 기준(2D)으로 재생하고, 설정 창의 효과음 크기(GameSettings.SfxVolume)를 따른다.
/// - 음높이를 살짝 흔들어 같은 소리가 반복돼도 덜 기계적으로 들리게 할 수 있다.
/// - 재생용 오브젝트를 따로 만들어 쓰므로, 부른 오브젝트(보호막 등)가 바로 사라져도 소리가 끊기지 않는다.
/// </summary>
public static class SkillSfx
{
    public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
    {
        if (clip == null) return;
        var go = new GameObject("SkillSfx_" + clip.name);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        src.volume = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
        src.clip = clip;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.05f, src.pitch) + 0.1f);
    }

    /// <summary>목록에서 무작위 하나 (비어 있으면 null)</summary>
    public static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}
