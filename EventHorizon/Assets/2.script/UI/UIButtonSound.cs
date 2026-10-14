using UnityEngine;

/// <summary>UI 클릭 소리 종류</summary>
public enum UISoundType
{
    Auto,      // 자동 판별 (UISoundSettings 의 단어 목록 기준)
    Default,
    Confirm,
    Cancel,
    MapNode,
}

/// <summary>
/// (선택) 특정 버튼만 클릭 소리 종류를 직접 고르거나, 전용 소리를 쓰거나, 소리를 끄고 싶을 때 그 버튼 오브젝트에 붙인다.
/// 안 붙인 버튼은 자동 판별된 종류의 소리를 쓴다.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour
{
    [Tooltip("체크하면 이 버튼은 클릭 소리가 나지 않음")]
    public bool mute = false;
    [Tooltip("소리 종류. Auto 면 자동 판별 (Default=기본, Confirm=확인, Cancel=취소, MapNode=노드 선택)")]
    public UISoundType type = UISoundType.Auto;
    [Tooltip("이 버튼 전용 소리. 넣으면 종류와 상관없이 이 소리 사용")]
    public AudioClip clickClip;
    [Tooltip("기본 볼륨에 곱해지는 배율")]
    [Range(0f, 2f)] public float volumeScale = 1f;
}
