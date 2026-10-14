using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 이펙트 프리팹에 붙이는 공용 스크립트.
/// Playable Director(Timeline)가 연결돼 있으면 그걸 재생,
/// 없으면 자식 파티클을 모두 재생한다.
/// 둘 다 끝나면 스스로 사라진다.
/// lifetime 이 0 이면 자동 계산.
///
/// 단, 반복 재생(Looping) 파티클이면서 이 오브젝트가 다른 오브젝트의 자식으로
/// 붙어있는 경우(예: 히트박스 프리팹 안에 자식으로 넣어둔 차징 이펙트)엔
/// 스스로 파괴 타이머를 걸지 않는다. looping 파티클은 원래 계속 반복되는게 의도이므로,
/// 부모(히트박스 등)가 사라질 때 유니티가 자동으로 같이 지워주는 걸 그대로 이용한다.
/// </summary>
public class AutoPlayEffect : MonoBehaviour
{
    [Header("Timeline (연결시 우선 사용, 없으면 파티클)")]
    public PlayableDirector director;

    [Header("생성 위치 보정값 (변환 전엔 무시)")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("생존 시간 (0 = 자동 계산)")]
    public float lifetime = 0f;

    [Tooltip("자동 계산 시 추가 여유 시간 (초)")]
    public float extraTime = 0.2f;

    void Awake()
    {
        // 위치 보정값 적용
        transform.position += positionOffset;
        float autoTime = 0f;
        bool hasLoopingParticle = false;

        if (director != null)
        {
            // 타임라인 재생
            director.Play();
            autoTime = (float)director.duration;
        }
        else
        {
            // 파티클 재생
            var psList = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Play();

                var main = ps.main;
                if (main.loop) hasLoopingParticle = true;

                float total = main.duration + main.startLifetime.constantMax;
                if (total > autoTime) autoTime = total;
            }
        }

        // 반복 재생 파티클 + 부모가 있는 이펙트는 자동 파괴 타이머를 걸지 않는다.
        // (부모가 사라질 때 유니티가 자식도 같이 파괴해주므로, 부모가 살아있는 동안 계속 반복 재생됨)
        // lifetime 을 직접 지정한 경우엔 그 설정을 존중해서 그대로 타이머를 건다.
        if (hasLoopingParticle && transform.parent != null && lifetime <= 0f)
        {
            return;
        }

        // 생존 시간 결정 — SpriteAnimEffect.cs/SpawnEffectTrigger.cs와 똑같은 "명시값 우선,
        // 아니면 자동값 + 여유시간" 규칙이라 EffectTimeUtil.ResolveDestroyTime()으로 공유한다.
        // (자동값을 구하는 방식만 다름: 여기는 Timeline/파티클 길이, 그쪽은 Animator 클립 길이)
        float destroyTime = EffectTimeUtil.ResolveDestroyTime(lifetime, autoTime, extraTime);
        Destroy(gameObject, destroyTime);
    }
}
