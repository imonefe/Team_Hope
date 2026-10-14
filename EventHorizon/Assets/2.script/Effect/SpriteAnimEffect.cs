using UnityEngine;

/// <summary>
/// 소환되면 애니메이션이 재생되고, 끝나면 정해진 시간후에 사라지는 스프라이트 이펙트용.
/// Animator가 붙은 오브젝트에 함께 붙여서 사용.
/// - lifetime 0 = 현재 애니메이션 클립 길이만큼 파괴 시 사용 (자동)
/// - lifetime > 0 = 그 시간(초) 후 파괴 (수동 지정)
/// </summary>
public class SpriteAnimEffect : MonoBehaviour
{
    [Header("생존 시간 (0 = 애니메이션 길이 자동)")]
    public float lifetime = 0f;

    [Tooltip("자동 파괴 시 추가 시간 (초)")]
    public float extraTime = 0f;

    void Start()
    {
        Destroy(gameObject, EffectTimeUtil.ResolveAutoDestroyTime(gameObject, lifetime, extraTime));
    }
}
