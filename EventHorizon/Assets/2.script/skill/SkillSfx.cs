using System.Collections.Generic;
using UnityEngine;

/// <summary>[2026-11-25] 효과음 등급 — 기능 교통정리안 '소리 우선순위'</summary>
public enum SfxTier
{
    Critical = 1,   // 꼭 들려야 함: 피격 · 사이버웨어 선택 · 제로포인트 — 나는 동안 2·3등급 음량을 잠깐 낮춤
    Action = 2,     // 행동 결과: 타격 · 스킬 · 회복 · 무기 교체 · 아이템 — 같은 소리 동시 최대 2개
    Ambient = 3,    // 배경 · 반복: 적 발사 · 스폰 · 파괴 · 착지 — 3등급 전체 동시 최대 3개
}

/// <summary>
/// [2026-11-09] 스킬 효과음 재생 도우미.
/// - 화면 기준(2D)으로 재생하고, 설정 창의 효과음 크기(GameSettings.SfxVolume)를 따른다.
/// - 음높이를 살짝 흔들어 같은 소리가 반복돼도 덜 기계적으로 들리게 할 수 있다.
/// - [2026-11-25] 실제 재생은 SfxBus 가 맡는다 (등급별 동시 재생 수 제한 · 1등급이 날 때 나머지 음량 낮춤 · 묶음 끊기).
///   등급을 주지 않으면 2등급(행동 결과).
/// </summary>
public static class SkillSfx
{
    public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        => SfxBus.Play(clip, volume, pitchJitter, SfxTier.Action, null, null);

    /// <summary>[2026-11-25] 등급 지정 · group 을 주면 같은 묶음의 이전 소리를 끊고 재생 (무기 교체 연타 등)</summary>
    public static void Play(AudioClip clip, float volume, float pitchJitter, SfxTier tier, string group = null)
        => SfxBus.Play(clip, volume, pitchJitter, tier, group, null);

    /// <summary>목록에서 무작위 하나 (비어 있으면 null)</summary>
    public static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}

/// <summary>
/// [2026-11-25 추가] 효과음 관리자 (처음 소리를 낼 때 스스로 생김, 씬이 바뀌어도 유지).
///  - 1등급이 나면 duckHold 동안 2·3등급 음량 × duckLevel
///  - 2등급: 같은 소리 동시 최대 SameClipMax 개 · 3등급: 전체 동시 최대 AmbientMax 개 (넘으면 가장 오래된 것을 끔)
///  - 시간 배율(일시정지)과 상관없이 실제 시간으로 정리
/// </summary>
public class SfxBus : MonoBehaviour
{
    const int SameClipMax = 2;
    const int AmbientMax = 3;
    const float DuckLevel = 0.5f;
    const float DuckHoldMax = 0.5f;      // 1등급 소리 길이 중 이만큼까지만 낮춤 상태 유지
    const float DuckAttack = 0.05f, DuckRelease = 0.3f;

    class Voice { public AudioSource src; public float baseVol; public SfxTier tier; public AudioClip clip; public string group; public float end; }

    static SfxBus inst;
    readonly List<Voice> voices = new List<Voice>();
    float duckUntil = -1f, duck = 1f;

    static SfxBus I
    {
        get
        {
            if (inst == null)
            {
                var go = new GameObject("SfxBus");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<SfxBus>();
            }
            return inst;
        }
    }

    /// <summary>다른 곳에서 낸 1등급 소리(제로포인트 등)도 나머지 음량을 낮추게</summary>
    public static void Duck(float seconds)
    {
        var b = I;
        b.duckUntil = Mathf.Max(b.duckUntil, Time.unscaledTime + seconds);
    }

    /// <summary>position 을 주면 그 자리에서 3D 로 재생 (타격음 — 예전 PlayClipAtPoint 와 같은 거리 감쇠)</summary>
    public static void Play(AudioClip clip, float volume, float pitchJitter, SfxTier tier, string group, Vector3? position)
    {
        if (clip == null) return;
        var b = I;
        b.Clean();
        if (!string.IsNullOrEmpty(group)) b.StopWhere(v => v.group == group, 0);
        if (tier == SfxTier.Action) b.StopWhere(v => v.tier == SfxTier.Action && v.clip == clip, SameClipMax - 1);
        else if (tier == SfxTier.Ambient) b.StopWhere(v => v.tier == SfxTier.Ambient, AmbientMax - 1);

        var go = new GameObject("Sfx_" + clip.name);
        go.transform.SetParent(b.transform, false);
        if (position.HasValue) go.transform.position = position.Value;
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = position.HasValue ? 1f : 0f;
        src.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        float baseVol = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
        src.volume = baseVol * (tier == SfxTier.Critical ? 1f : b.duck);
        src.clip = clip;
        src.Play();
        float len = clip.length / Mathf.Max(0.05f, Mathf.Abs(src.pitch));
        b.voices.Add(new Voice { src = src, baseVol = baseVol, tier = tier, clip = clip, group = group, end = Time.unscaledTime + len });
        if (tier == SfxTier.Critical) Duck(Mathf.Min(len, DuckHoldMax));
    }

    // 조건에 맞는 소리가 keep 개를 넘으면 오래된 것부터 끔
    void StopWhere(System.Predicate<Voice> match, int keep)
    {
        int count = 0;
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            if (!match(voices[i])) continue;
            count++;
            if (count > keep) Kill(i);
        }
    }

    void Kill(int i)
    {
        if (voices[i].src != null) Destroy(voices[i].src.gameObject);
        voices.RemoveAt(i);
    }

    void Clean()
    {
        float now = Time.unscaledTime;
        for (int i = voices.Count - 1; i >= 0; i--)
            if (voices[i].src == null || now > voices[i].end + 0.05f) Kill(i);
    }

    void Update()
    {
        float target = Time.unscaledTime < duckUntil ? DuckLevel : 1f;
        float speed = (1f - DuckLevel) / (target < duck ? DuckAttack : DuckRelease);
        duck = Mathf.MoveTowards(duck, target, speed * Time.unscaledDeltaTime);
        foreach (var v in voices)
            if (v.src != null && v.tier != SfxTier.Critical) v.src.volume = v.baseVol * duck;
        Clean();
    }
}
