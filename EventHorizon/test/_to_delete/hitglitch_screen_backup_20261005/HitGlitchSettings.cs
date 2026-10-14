using UnityEngine;

/// <summary>
/// 피격 글리치 효과 설정 (프로젝트 전체 공용 1개).
/// 반드시 Resources 폴더 안에 "HitGlitchSettings" 라는 이름으로 있어야 HitGlitchEffect 가 자동으로 찾아서 쓴다.
/// (현재 위치: Assets/Etc/Resources/HitGlitchSettings.asset)
/// 모양(색, 두께, 글리치 칸 등)은 여기 연결된 머티리얼(HitGlitchEdge.mat)에서 조절한다.
/// </summary>
[CreateAssetMenu(fileName = "HitGlitchSettings", menuName = "Game/Hit Glitch Settings")]
public class HitGlitchSettings : ScriptableObject
{
    [Tooltip("피격 글리치 전체 켜기/끄기")]
    public bool effectEnabled = true;

    [Tooltip("HitGlitchEdge 셰이더를 쓰는 머티리얼. 색, 두께, 글리치 모양은 이 머티리얼에서 조절")]
    public Material material;

    [Header("시간 / 세기")]
    [Tooltip("피격 후 효과가 완전히 사라지기까지 걸리는 시간(초). 일시정지·슬로모션과 상관없이 실제 시간 기준")]
    public float duration = 0.45f;
    [Tooltip("약하게 맞았을 때의 세기 (0~1)")]
    [Range(0f, 1f)] public float minIntensity = 0.55f;
    [Tooltip("세게 맞았을 때의 세기 (0~1)")]
    [Range(0f, 1f)] public float maxIntensity = 1f;
    [Tooltip("최대 HP 의 몇 % 를 한 번에 잃으면 최대 세기로 볼지 (0.2 = 20%)")]
    [Range(0.01f, 1f)] public float fullDamageRatio = 0.2f;

    [Header("지직거림")]
    [Tooltip("글리치 무늬를 새로 섞는 간격(초). 작을수록 빠르게 지직거림")]
    public float glitchInterval = 0.05f;
    [Tooltip("매 프레임 세기를 무작위로 깎는 정도 (0 = 깜빡임 없음)")]
    [Range(0f, 0.6f)] public float flicker = 0.25f;

    [Header("그리는 순서")]
    [Tooltip("다른 UI 와의 앞뒤 순서. 일시정지 메뉴(500)보다 낮게, 전투 UI 보다 높게")]
    public int sortingOrder = 90;
}
