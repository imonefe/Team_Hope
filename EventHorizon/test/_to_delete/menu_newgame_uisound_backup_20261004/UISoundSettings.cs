using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// UI 버튼 클릭 효과음 설정 (프로젝트 전체 공용 1개).
/// 반드시 Resources 폴더 안에 "UISoundSettings" 라는 이름으로 있어야 UIClickSound 가 자동으로 찾아서 쓴다.
/// (현재 위치: Assets/Etc/Resources/UISoundSettings.asset)
/// </summary>
[CreateAssetMenu(fileName = "UISoundSettings", menuName = "Game/UI Sound Settings")]
public class UISoundSettings : ScriptableObject
{
    [Header("클릭 효과음")]
    [Tooltip("UI 클릭 효과음 전체 켜기/끄기")]
    public bool playClickSound = true;
    [Tooltip("버튼을 눌렀을 때 나는 기본 소리. 버튼마다 다른 소리를 쓰고 싶으면 그 버튼에 UIButtonSound 를 붙인다")]
    public AudioClip clickClip;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("누를 때마다 음높이를 ±이 값만큼 랜덤으로 살짝 바꿔서 덜 반복적으로 들리게 함 (0 = 끔)")]
    [Range(0f, 0.2f)] public float pitchVariation = 0f;

    [Header("대상")]
    [Tooltip("끄면 Button/Toggle/Dropdown 같은 '버튼류'만 소리가 남. 켜면 직접 만든 클릭 UI(IPointerClickHandler, 예: 아이템 슬롯)도 소리가 남")]
    public bool includeCustomClickables = false;

    [Header("출력 (선택)")]
    [Tooltip("오디오 믹서를 쓰는 경우 연결. 비워두면 기본 출력")]
    public AudioMixerGroup outputMixerGroup;
}
