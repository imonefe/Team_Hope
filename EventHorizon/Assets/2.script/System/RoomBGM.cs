using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 방 종류에 따라 배경음을 바꾼다 (로비 / 배틀 / 보스방).
///
/// - SampleScene 처럼 한 씬 안에서 방 프리팹을 바꿔 끼우는 씬에 하나만 놓는다.
///   (인트로처럼 씬 하나에 곡 하나면 SceneBGM 을 쓰면 됨)
/// - RoomManager 가 새 방을 열 때마다(RoomManager.OnRoomLoaded) 방 종류를 보고 곡을 고른다.
///     [2026-11-19] 튜토리얼 → 튜토리얼 곡 / 로비·상점 → 로비 곡 / 보스 코어가 있는 방 → 보스 곡
///     / 그 밖의 보스방(중간 보스) → 중간 보스 곡 / 나머지 → 배틀 곡
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
    [Tooltip("[2026-11-19] 튜토리얼 방 (비우면 로비 곡)")]
    public AudioClip tutorialBgm;
    [Tooltip("[2026-11-19] 중간 보스 방 (비우면 보스 곡)")]
    public AudioClip midBossBgm;

    // [2026-11-24] 2스테이지(폐철장) 전용 곡. 로비 방은 두 스테이지가 같은 프리팹이라,
    // 지금 진행 중인 스테이지 구성 칸의 묶음 이름(StagePlan 의 group = "2S")으로 구분한다. 비운 칸은 위 1스테이지 곡을 그대로 씀
    [Header("[2026-11-24] 2스테이지 (폐철장) — 비우면 위 곡")]
    [Tooltip("2스테이지 로비 · 상점")]
    public AudioClip stage2LobbyBgm;
    [Tooltip("2스테이지 전투방")]
    public AudioClip stage2BattleBgm;
    [Tooltip("2스테이지 보스방")]
    public AudioClip stage2BossBgm;
    [Tooltip("2스테이지 중간 보스 방 (비우면 2스테이지 보스 곡)")]
    public AudioClip stage2MidBossBgm;
    [Tooltip("이 묶음 이름의 칸을 진행 중이면 2스테이지 곡")]
    public string stage2Group = "2S";

    [Header("바뀔 때 (초)")]
    [Tooltip("앞 곡이 작아지며 사라지는 시간")]
    public float fadeOutTime = 0.6f;
    [Tooltip("새 곡이 커지며 나타나는 시간")]
    public float fadeInTime = 0.8f;

    Coroutine fading;
    // [2026-11-24] 이 씬에서 처음 여는 방(새 게임 · 다시 하기)이면 같은 곡이어도 처음부터. 그 뒤로는 같은 곡이면 끊지 않고 이어서 반복
    bool firstRoom = true;

    void OnEnable() { RoomManager.OnRoomLoaded += OnRoomLoaded; }
    void OnDisable()
    {
        RoomManager.OnRoomLoaded -= OnRoomLoaded;
        // 연출 도중 꺼지면 코루틴이 멈추므로 '연출 중' 표시를 직접 풀어 준다
        if (fading != null)
        {
            fading = null;
            if (AudioManager.Instance != null) AudioManager.Instance.bgmFading = false;
        }
    }

    void OnRoomLoaded(Room room, bool isStartRoom)
    {
        if (room == null) return;

        // [2026-11-19 수정] 방 종류 판별 (위에서부터 먼저 맞는 것)
        //  튜토리얼(TutorialController 있음) → 튜토리얼 곡
        //  시작 방 · 이름에 Lobby/Shop → 로비 곡 (상점도 같은 곡)
        //  보스 코어(BossCore)가 있는 방 = 최종 보스방 → 보스 곡
        //  isBossRoom 이거나 보스가 있는 그 밖의 방 = 중간 보스방 → 중간 보스 곡
        //  나머지 → 전투 곡
        bool s2 = IsStage2(room);
        AudioClip lobby = Pick(s2, stage2LobbyBgm, lobbyBgm);
        AudioClip boss = Pick(s2, stage2BossBgm, bossBgm);
        AudioClip mid = s2 ? (stage2MidBossBgm != null ? stage2MidBossBgm : boss) : (midBossBgm != null ? midBossBgm : bossBgm);
        AudioClip clip;
        if (room.GetComponentInChildren<TutorialController>(true) != null) clip = tutorialBgm != null ? tutorialBgm : lobbyBgm;
        else if (isStartRoom || room.name.Contains("Lobby") || room.name.Contains("Shop")) clip = lobby;
        else if (room.GetComponentInChildren<BossCore>(true) != null) clip = boss;
        else if (room.isBossRoom || room.GetComponentInChildren<BossController>(true) != null) clip = mid;
        else clip = Pick(s2, stage2BattleBgm, battleBgm);

        Debug.Log($"[RoomBGM] {room.name} → {(clip != null ? clip.name : "없음")} (2스테이지: {s2})");   // [2026-11-25] 확인용 (출시 빌드에서는 안 찍힘)
        Play(clip, firstRoom);
        firstRoom = false;
    }

    static AudioClip Pick(bool stage2, AudioClip s2Clip, AudioClip normal) => stage2 && s2Clip != null ? s2Clip : normal;

    // [2026-11-24] 2스테이지인지: 스테이지 구성대로 진행 중이면 지금 칸의 묶음 이름, 아니면(테스트로 방을 바로 연 경우) 방 이름에 2S
    bool IsStage2(Room room)
    {
        // [2026-11-25] 방 이름에 2S 가 있으면 무조건 2스테이지 (테스트로 방을 바로 열어 진행 칸이 1스테이지에 머물러 있어도 2스테이지 곡)
        if (room.name.Contains("2S")) return true;
        // 두 스테이지가 같이 쓰는 방(로비 등)은 진행 중인 칸의 묶음 이름으로
        if (StageFlow.Active)
        {
            var e = StageFlow.CurrentEntry;
            if (e != null) return e.group == stage2Group;
        }
        return false;
    }

    /// <summary>곡을 바꾼다 (같은 곡이 이미 나오고 있으면 그대로 둠)</summary>
    public void Play(AudioClip clip) => Play(clip, false);

    public void Play(AudioClip clip, bool restart)
    {
        if (clip == null) return;
        AudioManager am = AudioManager.GetOrCreate();
        if (am == null || am.bgmSource == null) return;
        AudioSource src = am.bgmSource;
        if (!restart && src.clip == clip && src.isPlaying) return;

        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Switch(am, src, clip, restart));
    }

    IEnumerator Switch(AudioManager am, AudioSource src, AudioClip clip, bool restart)
    {
        am.bgmFading = true;   // [2026-10-19] 연출하는 동안 AudioManager 가 크기를 되돌리지 않게

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
        AudioManager.PlayBGM(clip, restart);
        if (fadeInTime > 0f)
        {
            for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
            {
                src.volume = Mathf.Lerp(0f, am.bgmVolume, t / fadeInTime);
                yield return null;
            }
        }
        src.volume = am.bgmVolume;
        am.bgmFading = false;
        fading = null;
    }
}
