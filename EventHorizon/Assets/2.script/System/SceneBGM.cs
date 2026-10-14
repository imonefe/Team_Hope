using UnityEngine;

/// <summary>
/// 씬에 하나 배치해서 "이 씬에서 틀 BGM"을 지정하는 컴포넌트.
/// 씬이 시작되면(Start) AudioManager.PlayBGM 으로 재생한다.
///  - 씬에 AudioManager 가 없으면 자동으로 만들어서 재생 (인트로 씬 등)
///  - 이전 씬과 같은 곡이면 끊기지 않고 그대로 이어서 재생됨 (재시작/새 게임 때도 안 끊김)
///  - 다른 곡이면 바로 교체
/// 볼륨은 AudioManager 의 bgmVolume 을 따른다.
/// </summary>
public class SceneBGM : MonoBehaviour
{
    [Tooltip("이 씬에서 재생할 BGM (Assets/5. Sound/BGM 안의 파일을 드래그)")]
    public AudioClip bgm;

    [Tooltip("체크하면 bgm 이 비어 있을 때 이전 씬에서 나오던 BGM 을 끈다 (조용한 씬용)")]
    public bool stopIfEmpty = false;

    void Start()
    {
        if (bgm == null)
        {
            if (stopIfEmpty) AudioManager.StopBGM();
            return;
        }

        AudioManager.GetOrCreate();
        AudioManager.PlayBGM(bgm);
    }
}
