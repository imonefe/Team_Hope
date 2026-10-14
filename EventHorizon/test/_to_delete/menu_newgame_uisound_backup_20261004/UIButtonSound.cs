using UnityEngine;

/// <summary>
/// (선택) 특정 버튼만 클릭 소리를 바꾸거나 끄고 싶을 때 그 버튼 오브젝트에 붙인다.
/// 안 붙인 버튼은 UISoundSettings 의 기본 클릭 소리를 그대로 쓴다.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour
{
    [Tooltip("체크하면 이 버튼은 클릭 소리가 나지 않음")]
    public bool mute = false;
    [Tooltip("이 버튼 전용 소리. 비워두면 기본 클릭 소리 사용")]
    public AudioClip clickClip;
    [Tooltip("기본 볼륨에 곱해지는 배율")]
    [Range(0f, 2f)] public float volumeScale = 1f;
}
