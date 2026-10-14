using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// UI 버튼 클릭 효과음 설정 (프로젝트 전체 공용 1개).
/// 반드시 Resources 폴더 안에 "UISoundSettings" 라는 이름으로 있어야 UIClickSound 가 자동으로 찾아서 쓴다.
/// (현재 위치: Assets/Etc/Resources/UISoundSettings.asset)
///
/// 버튼 종류별로 다른 소리가 난다. 종류는 자동으로 판별:
///  1) 버튼에 UIButtonSound 가 붙어 있고 종류를 직접 골랐으면 → 그 종류
///  2) 맵 노드 버튼(MapButton) → 노드 선택
///  3) 버튼 OnClick 에 연결된 함수 이름 → 버튼 글자 → 오브젝트 이름 순서로 아래 단어 목록과 비교
///  4) 어디에도 안 걸리면 → 기본
/// </summary>
[CreateAssetMenu(fileName = "UISoundSettings", menuName = "Game/UI Sound Settings")]
public class UISoundSettings : ScriptableObject
{
    [Header("전체")]
    [Tooltip("UI 클릭 효과음 전체 켜기/끄기")]
    public bool playClickSound = true;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("누를 때마다 음높이를 ±이 값만큼 랜덤으로 살짝 바꿔서 덜 반복적으로 들리게 함 (0 = 끔)")]
    [Range(0f, 0.2f)] public float pitchVariation = 0f;

    [Header("종류별 소리")]
    [Tooltip("기본 — 어느 종류에도 해당하지 않는 일반 버튼 (설정, 크레딧 등)")]
    public AudioClip clickClip;
    [Tooltip("확인 — 시작, 새 게임, 다시하기 등 '앞으로 나아가는' 버튼. 비워두면 기본 소리")]
    public AudioClip confirmClip;
    [Tooltip("취소 — 계속하기(메뉴 닫기), 뒤로, 닫기, 종료 등 '빠져나가는' 버튼. 비워두면 기본 소리")]
    public AudioClip cancelClip;
    [Tooltip("노드 선택 — 맵(노드 캔버스)에서 다음 방을 고르는 버튼. 비워두면 확인 소리")]
    public AudioClip mapNodeClip;

    [Header("자동 판별 단어 (소문자로 비교, 일부만 포함돼도 인정)")]
    [Tooltip("이 단어가 들어가면 '확인' 소리")]
    public string[] confirmKeywords = { "start", "restart", "retry", "new", "play", "confirm", "시작", "새 게임", "다시", "확인" };
    [Tooltip("이 단어가 들어가면 '취소' 소리")]
    public string[] cancelKeywords = { "quit", "exit", "back", "close", "cancel", "resume", "continue", "종료", "뒤로", "닫기", "취소", "계속" };

    [Header("대상")]
    [Tooltip("끄면 Button/Toggle/Dropdown 같은 '버튼류'만 소리가 남. 켜면 직접 만든 클릭 UI(IPointerClickHandler, 예: 아이템 슬롯)도 소리가 남")]
    public bool includeCustomClickables = false;

    [Header("출력 (선택)")]
    [Tooltip("오디오 믹서를 쓰는 경우 연결. 비워두면 기본 출력")]
    public AudioMixerGroup outputMixerGroup;

    /// <summary>종류에 맞는 소리 (비어 있으면 대신 쓸 소리로 대체)</summary>
    public AudioClip GetClip(UISoundType type)
    {
        switch (type)
        {
            case UISoundType.Confirm: return confirmClip != null ? confirmClip : clickClip;
            case UISoundType.Cancel: return cancelClip != null ? cancelClip : clickClip;
            case UISoundType.MapNode:
                if (mapNodeClip != null) return mapNodeClip;
                return confirmClip != null ? confirmClip : clickClip;
            default: return clickClip;
        }
    }
}
