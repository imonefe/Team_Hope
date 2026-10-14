using UnityEngine;

// idel 상태가 한 바퀴(루프) 돌 때마다 낮은 확률로 idle02Trigger를 발동시켜
// 특수 대기모션(idle02)이 가끔 섞여 나오게 하는 StateMachineBehaviour.
// 애니메이터의 idel 스테이트에 직접 붙는 방식이라 플레이어 오브젝트/프리팹은
// 전혀 건드리지 않는다 (female.controller 안에서만 동작).
public class IdleRandomVariation : StateMachineBehaviour
{
    [Range(0f, 1f)]
    public float chancePerLoop = 0.15f;
    public string triggerName = "idle02Trigger";

    private float lastNormalizedTime;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        lastNormalizedTime = stateInfo.normalizedTime % 1f;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float normalizedTime = stateInfo.normalizedTime % 1f;

        // 루프가 한 바퀴 끝나고 다시 처음으로 돌아오는 순간(0 부근으로 wrap)에만 확률 체크.
        if (normalizedTime < lastNormalizedTime)
        {
            if (Random.value < chancePerLoop)
            {
                animator.SetTrigger(triggerName);
            }
        }

        lastNormalizedTime = normalizedTime;
    }
}
