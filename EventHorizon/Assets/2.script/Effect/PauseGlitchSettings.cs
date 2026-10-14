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

    [Header("퍼즈 중 화면 넓게 잡기 (PauseCameraZoom)")]
    [Tooltip("퍼즈 중 카메라를 넓게 잡기 켜기/끄기")]
    public bool cameraZoomEnabled = true;
    [Tooltip("얼마나 넓게 잡을지 (1.25 = 25% 더 넓게)")]
    [Range(1f, 2f)] public float zoomScale = 1.25f;
    [Tooltip("넓어지는 데 걸리는 시간(초)")]
    public float zoomInTime = 0.35f;
    [Tooltip("퍼즈가 끝나고 원래대로 돌아오는 시간(초)")]
    public float zoomOutTime = 0.4f;

    [Header("퍼즈 중 멈춘 잔상 (PauseAfterimage) — 색·윤곽·깨지는 조각 모양은 Resources/PauseAfterimage.mat")]
    [Tooltip("퍼즈 중 주인공 잔상 켜기/끄기")]
    public bool afterimageEnabled = true;
    [Tooltip("주인공이 이 거리(월드 단위)만큼 움직일 때마다 잔상을 하나 남김 (가만히 있으면 안 생김)")]
    public float afterimageSpacing = 1.1f;
    [Tooltip("잔상 사이 최소 시간(초) — 아주 빠른 대시에서 너무 촘촘해지지 않게")]
    public float afterimageMinInterval = 0.04f;
    [Tooltip("동시에 남아 있는 최대 개수 (넘으면 가장 오래된 것부터 서서히 사라짐)")]
    public int afterimageMaxCount = 28;
    [Tooltip("가장 최근 잔상의 진하기 (0~1)")]
    [Range(0f, 1f)] public float afterimageAlpha = 0.55f;
    [Tooltip("가장 오래된 잔상의 진하기 배율 (최근 → 오래된 순으로 옅어짐)")]
    [Range(0f, 1f)] public float afterimageOldestScale = 0.45f;
    [Tooltip("잔상 하나가 남아 있는 최소 시간(초) — 이 시간과 최대 시간 사이에서 잔상마다 무작위로 정해짐")]
    public float afterimageLifeMin = 2f;
    [Tooltip("잔상 하나가 남아 있는 최대 시간(초)")]
    public float afterimageLifeMax = 3f;
    [Tooltip("수명이 끝난 잔상도 조각나며 깨지듯 사라지기 (끄면 서서히 사라짐)")]
    public bool afterimageLifeShatter = true;
    [Tooltip("최대 개수를 넘어 밀려난 잔상이 사라지는 시간(초)")]
    public float afterimageFadeTime = 0.25f;
    [Tooltip("퍼즈가 끝날 때 잔상이 조각나며 깨지듯 사라지기 (끄면 그냥 서서히 사라짐)")]
    public bool afterimageShatter = true;
    [Tooltip("잔상 하나가 깨져서 사라지는 시간(초)")]
    public float afterimageShatterTime = 0.45f;
    [Tooltip("오래된 잔상부터 차례로 깨지는 시간차(초, 처음~마지막 사이)")]
    public float afterimageShatterStagger = 0.15f;

    [Header("끝나기 직전 신호")]
    [Tooltip("퍼즈가 이 시간(초)만큼 남으면 깜빡임을 키워서 곧 끝난다는 걸 알려줌 (0 = 끔)")]
    public float endWarningTime = 0f;
    [Tooltip("끝나기 직전 깜빡이는 빠르기 (초당 횟수)")]
    public float endWarningRate = 10f;
}
