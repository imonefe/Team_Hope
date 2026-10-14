using UnityEngine;
using Spine.Unity;

public class PlayOnP : MonoBehaviour
{
    [Tooltip("비워두면 자기 자신의 SkeletonAnimation을 자동으로 찾습니다")]
    public SkeletonAnimation skeletonAnimation;
    [Tooltip("재생할 애니메이션 이름 (2st_boss_sprite.json 기준: animation)")]
    public string animationName = "animation";
    public bool loop = false;

    void Reset()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponent<SkeletonAnimation>();
    }

    void Awake()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponent<SkeletonAnimation>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (skeletonAnimation == null || skeletonAnimation.AnimationState == null) return;
            skeletonAnimation.AnimationState.SetAnimation(0, animationName, loop);
        }
    }
}
