using UnityEngine;

/// <summary>
/// 저체력 화면 효과(가장자리 회붉은 노이즈 + 심한 왜곡) 설정 (프로젝트 전체 공용 1개).
/// 반드시 Resources 폴더 안에 "LowHpGlitchSettings" 라는 이름으로 있어야 LowHpGlitchEffect 가 자동으로 찾아서 쓴다.
/// (현재 위치: Assets/Etc/Resources/LowHpGlitchSettings.asset)
/// 모양(색, 왜곡 정도, 블록, 알갱이 등)은 Resources/LowHpGlitchScreen.mat 머티리얼에서 조절한다.
/// </summary>
[CreateAssetMenu(fileName = "LowHpGlitchSettings", menuName = "Game/Low Hp Glitch Settings")]
public class LowHpGlitchSettings : ScriptableObject
{
    [Tooltip("저체력 효과 전체 켜기/끄기")]
    public bool effectEnabled = true;

    [Header("HP 기준")]
    [Tooltip("HP 비율이 이 값 이하가 되면 효과가 켜짐 (0.2 = 20%)")]
    [Range(0.01f, 1f)] public float hpThreshold = 0.2f;

    [Header("세기 (0~1)")]
    [Tooltip("기준선에 막 걸쳤을 때 세기")]
    [Range(0f, 1f)] public float minIntensity = 0.6f;
    [Tooltip("HP 가 거의 없을 때 세기")]
    [Range(0f, 1f)] public float maxIntensity = 1f;
    [Tooltip("효과가 켜지는 데 걸리는 시간(초)")]
    public float fadeInTime = 0.4f;
    [Tooltip("회복해서 효과가 꺼지는 데 걸리는 시간(초)")]
    public float fadeOutTime = 0.5f;

    [Header("심장박동 맥동")]
    [Tooltip("초당 맥동 횟수 (0 = 끔)")]
    public float pulseRate = 1.1f;
    [Tooltip("맥동으로 세기가 줄었다 늘었다 하는 정도 (0~1)")]
    [Range(0f, 1f)] public float pulseAmount = 0.25f;

    [Header("지직거림")]
    [Tooltip("노이즈 무늬를 새로 섞는 간격(초)")]
    public float glitchInterval = 0.07f;
    [Tooltip("매 프레임 세기를 무작위로 깎는 정도 (0 = 깜빡임 없음)")]
    [Range(0f, 0.6f)] public float flicker = 0.15f;

    [Header("심장 박동 소리 (2026-10-21 추가)")]
    [Tooltip("맥동에 맞춰 울릴 박동 소리 한 번 (쿵-쿵). 비우면 소리 없음")]
    public AudioClip heartbeatClip;
    [Tooltip("박동 소리 크기 (0~1). 실제 크기 = 이 값 × 효과 세기 × 효과음 볼륨 설정")]
    [Range(0f, 1f)] public float heartbeatVolume = 0.8f;
}
