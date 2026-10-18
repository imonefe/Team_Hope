using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 방 종류에 따라 배경음을 바꾼다 (로비 / 배틀 / 보스방).
///
/// - SampleScene 처럼 한 씬 안에서 방 프리팹을 바꿔 끼우는 씬에 하나만 놓는다.
///   (인트로처럼 씬 하나에 곡 하나면 SceneBGM 을 쓰면 됨)
/// - RoomManager 가 새 방을 열 때마다(RoomManager.OnRoomLoaded) 방 종류를 보고 곡을 고른다.
///     처음 여는 방이거나 이름에 "Lobby" 가 들어가면 → 로비 곡
///     Room 의 isBossRoom 이 체크돼 있으면        → 보스 곡
///     그 밖의 방                                  → 배틀 곡
/// - 같은 곡이면 끊지 않고 이어서 틀고, 다른 곡이면 앞 곡을 줄였다가 새 곡을 키운다.
///   (ESC 메뉴로 게임이 멈춰 있어도 동작하도록 실제 시간 기준)
/// - 볼륨은 AudioManager 의 배경음 크기(설정 창의 '배경음')를 따른다.
/// - 곡 칸이 비어 있으면 그 방에서는 곡을 바꾸지 않는다.
/// </summary>
public class RoomBGM : MonoBehaviour
{
    [Header("곡 (Assets/5. Sound/BGM 안의 파일을 끌어다 놓기)")]
    public AudioClip lobbyBgm;
    public AudioClip battleBgm;
    public AudioClip bossBgm;

    [Header("바뀔 때 (초)")]
    [Tooltip("앞 곡이 작아지며 사라지는 시간")]
    public float fadeOutTime = 0.6f;
    [Tooltip("새 곡이 커지며 나타나는 시간")]
    public float fadeInTime = 0.8f;

    Coroutine fading;

    void OnEnable() { RoomManager.OnRoomLoaded += OnRoomLoaded; }
    void OnDisable() { RoomManager.OnRoomLoaded -= OnRoomLoaded; }

    void OnRoomLoaded(Room room, bool isStartRoom)
    {
        if (room == null) return;

        AudioClip clip;
        if (isStartRoom || room.name.Contains("Lobby")) clip = lobbyBgm;
        else if (room.isBossRoom) clip = bossBgm;
        else clip = battleBgm;

        Play(clip);
    }

    /// <summary>곡을 바꾼다 (같은 곡이 이미 나오고 있으면 그대로 둠)</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        AudioManager am = AudioManager.GetOrCreate();
        if (am == null || am.bgmSource == null) return;
        AudioSource src = am.bgmSource;
        if (src.clip == clip && src.isPlaying) return;

        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Switch(am, src, clip));
    }

    IEnumerator Switch(AudioManager am, AudioSource src, AudioClip clip)
    {
        // 앞 곡 줄이기
        if (src.isPlaying && fadeOutTime > 0f)
        {
            float start = src.volume;
            for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
            {
                src.volume = Mathf.Lerp(start, 0f, t / fadeOutTime);
                yield return null;
            }
        }

        // 새 곡 시작 후 키우기 (목표 볼륨은 매 프레임 다시 읽어서, 그사이 설정을 바꿔도 맞춰짐)
        AudioManager.PlayBGM(clip);
        if (fadeInTime > 0f)
        {
            for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
            {
                src.volume = Mathf.Lerp(0f, am.bgmVolume, t / fadeInTime);
                yield return null;
            }
        }
        src.volume = am.bgmVolume;
        fading = null;
    }
}
