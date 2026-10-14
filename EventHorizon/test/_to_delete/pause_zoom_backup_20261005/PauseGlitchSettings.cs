using UnityEngine;

/// <summary>
/// 퍼즈 발동 효과(푸른 화면 + 테두리 노이즈 + 번개) 설정 (프로젝트 전체 공용 1개).
/// 반드시 Resources 폴더 안에 "PauseGlitchSettings" 라는 이름으로 있어야 PauseGlitchEffect 가 자동으로 찾아서 쓴다.
/// (현재 위치: Assets/Etc/Resources/PauseGlitchSettings.asset)
/// 모양(색, 번개 길이·개수, 노이즈 양 등)은 Resources/PauseGlitchScreen.mat 머티리얼에서 조절한다.
/// </summary>
[CreateAssetMenu(fileName = "PauseGlitchSettings", menuName = "Game/Pause Glitch Settings")]
public class PauseGlitchSettings : ScriptableObject
{
    [Tooltip("퍼즈 효과 전체 켜기/끄기")]
    public bool effectEnabled = true;

    [Header("푸른 화면 세기 (0~1) — 깜빡이지 않음")]
    [Tooltip("퍼즈가 켜지는 순간 푸른 색 세기")]
    [Range(0f, 1f)] public float colorBurst = 1f;
    [Tooltip("퍼즈가 켜져 있는 동안 푸른 색 세기")]
    [Range(0f, 1f)] public float colorSustain = 0.85f;

    [Header("노이즈 세기 (0~1)")]
    [Tooltip("퍼즈가 켜지는 순간 번쩍하는 세기")]
    [Range(0f, 1f)] public float burstIntensity = 1f;
    [Tooltip("퍼즈가 켜져 있는 동안 유지되는 세기 (노이즈는 얇은 테두리에만 나오므로 세게 둬도 됨)")]
    [Range(0f, 1f)] public float sustainIntensity = 0.8f;
    [Tooltip("번쩍한 뒤 유지 세기까지 내려오는 시간(초)")]
    public float burstDuration = 0.35f;
    [Tooltip("퍼즈가 끝난 뒤 효과가 사라지는 시간(초)")]
    public float fadeOutTime = 0.3f;

    [Header("번개 세기 (0~1)")]
    [Tooltip("퍼즈가 켜지는 순간 번개 세기")]
    [Range(0f, 1f)] public float boltBurst = 1f;
    [Tooltip("퍼즈가 켜져 있는 동안 번개 세기")]
    [Range(0f, 1f)] public float boltSustain = 0.75f;

    [Header("지직거림 (번개 수명·움직임은 PauseGlitchScreen.mat 에서)")]
    [Tooltip("노이즈 무늬를 새로 섞는 간격(초)")]
    public float glitchInterval = 0.06f;
    [Tooltip("매 프레임 세기를 무작위로 깎는 정도 (0 = 깜빡임 없음)")]
    [Range(0f, 0.6f)] public float flicker = 0.15f;

    [Header("시작할 때 연출 (잔상 메아리 / 조각 미끄러짐은 기본 꺼짐)")]
    [Tooltip("퍼즈가 시작되는 순간 화면 조각 + 잔상 켜기")]
    public bool startBurst = true;
    [Tooltip("화면이 가로 띠로 엇갈려 밀리는 세기 (0~1, 0 = 끔)")]
    [Range(0f, 1f)] public float sliceIntensity = 0f;
    [Tooltip("조각이 밀렸다가 제자리로 맞춰지기까지 시간(초)")]
    public float sliceDuration = 0.35f;
    [Tooltip("퍼져 있던 잔상 세기 (0~1)")]
    [Range(0f, 1f)] public float echoIntensity = 1f;
    [Tooltip("잔상이 하나로 모이기까지 시간(초)")]
    public float echoDuration = 0.5f;

    [Header("끝날 때 연출 (팡!)")]
    [Tooltip("퍼즈가 끝나는 순간 섬광 + 충격파 켜기")]
    public bool endBurst = true;
    [Tooltip("섬광 세기 (0~1)")]
    [Range(0f, 1f)] public float flashIntensity = 1f;
    [Tooltip("섬광이 사라지는 시간(초)")]
    public float flashDuration = 0.25f;
    [Tooltip("충격파 세기 (0~1)")]
    [Range(0f, 1f)] public float shockIntensity = 1f;
    [Tooltip("충격파가 화면 끝까지 퍼지는 시간(초)")]
    public float shockDuration = 0.55f;

    [Header("끝나기 직전 신호")]
    [Tooltip("퍼즈가 이 시간(초)만큼 남으면 깜빡임을 키워서 곧 끝난다는 걸 알려줌 (0 = 끔)")]
    public float endWarningTime = 0f;
    [Tooltip("끝나기 직전 깜빡이는 빠르기 (초당 횟수)")]
    public float endWarningRate = 10f;
}
