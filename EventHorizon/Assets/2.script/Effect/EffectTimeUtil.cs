using UnityEngine;

/// <summary>
/// 이펙트 오브젝트 자동 파괴 시간 계산 공용 유틸.
/// SpriteAnimEffect / SpawnEffectTrigger 등이 공통으로 사용.
/// </summary>
public static class EffectTimeUtil
{
    // [2026-09-29 추가] "lifetime을 명시했으면 그대로 쓰고, 아니면 자동으로 구한 길이에
    // extraTime을 더해서 쓴다"는 최종 판단 규칙 자체가 AutoPlayEffect.cs에도 거의 그대로
    // 중복 구현돼 있었다(자동 길이를 Animator 클립이 아니라 Timeline/파티클 길이로 구하는 것만 다름).
    // 그 공통 규칙만 따로 뽑아서 양쪽이 같이 쓰게 함.
    public static float ResolveDestroyTime(float lifetime, float autoDuration, float extraTime)
    {
        return lifetime > 0f ? lifetime : autoDuration + extraTime;
    }

    /// <summary>
    /// lifetime > 0 이면 그대로 사용.
    /// lifetime <= 0 이면 go에 붙은 Animator의 첫 애니메이션 클립 길이를 자동으로 사용(못 찾으면 기본 1초),
    /// 자동으로 구한 경우에만 extraTime을 더해서 반환.
    /// </summary>
    public static float ResolveAutoDestroyTime(GameObject go, float lifetime, float extraTime)
    {
        if (lifetime > 0f) return lifetime;

        float autoDuration = 0f;
        Animator animator = go.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips.Length > 0)
                autoDuration = clips[0].length;
        }
        if (autoDuration <= 0f) autoDuration = 1f;

        return ResolveDestroyTime(lifetime, autoDuration, extraTime);
    }
}
