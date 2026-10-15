using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Spine.Unity;

/// <summary>
/// [프로토타입 / 테스트 전용] 캐릭터 C.
/// 씬에 있는 실제 플레이어 캐릭터와는 완전히 별개의 테스트용 오브젝트로,
/// "대기(Idle)는 스파인, 공격(Attack)은 스프라이트 애니메이션"으로 전환했을 때
/// 실제로 얼마나 끊겨 보이는지(포즈 컷, 움직임 밀도 차이) 확인하기 위해 만들었다.
///
/// 구조:
///  - SpineVisual (자식) : SkeletonAnimation. 평소에는 이쪽을 켜두고 idleAnimationName을 루프 재생.
///  - SpriteVisual (자식) : SpriteRenderer + Animator(컨트롤러 없음).
///    공격 키를 누르면 attackClip 하나를 PlayableGraph로 직접 재생 — 별도 AnimatorController 없이
///    클립 하나만 테스트하기 위한 방식이라 실제 게임 로직(콤보/애니메이션 이벤트)과는 무관하다.
///
/// 사용법: attackKey를 누르면 스파인 → 스프라이트로 전환되고, attackClip 길이만큼 재생한 뒤
/// 다시 스파인 idle로 돌아온다. 반복해서 눌러보면서 전환 순간이 어색한지 눈으로 확인하면 된다.
/// </summary>
public class CharacterC_SpineSpriteTest : MonoBehaviour
{
    [Header("스파인 (대기)")]
    [Tooltip("스파인 스켈레톤을 렌더링하는 자식 오브젝트")]
    public GameObject spineVisual;
    [Tooltip("spineVisual에 붙어있는 SkeletonAnimation 컴포넌트")]
    public SkeletonAnimation spineAnimation;
    [Tooltip("대기 상태로 재생할 스파인 애니메이션 이름")]
    public string idleAnimationName = "animation";

    [Header("스프라이트 (공격)")]
    [Tooltip("스프라이트 프레임을 렌더링하는 자식 오브젝트")]
    public GameObject spriteVisual;
    [Tooltip("spriteVisual에 붙어있는 Animator (컨트롤러는 비워둬도 됨 — 클립을 직접 재생하므로)")]
    public Animator spriteAnimator;
    [Tooltip("공격 시 재생할 스프라이트 애니메이션 클립 (예: 실제 검격 Atk 클립을 그대로 연결해서 테스트)")]
    public AnimationClip attackClip;

    [Header("테스트 조작")]
    [Tooltip("이 키를 누르면 스파인 idle → 스프라이트 공격 재생 → 다시 스파인 idle 순으로 전환된다")]
    public KeyCode attackKey = KeyCode.Space;

    PlayableGraph graph;
    bool isAttacking = false;

    void Start()
    {
        ShowSpine();
    }

    void Update()
    {
        if (!isAttacking && Input.GetKeyDown(attackKey))
            StartCoroutine(PlayAttack());
    }

    void ShowSpine()
    {
        if (spineVisual != null) spineVisual.SetActive(true);
        if (spriteVisual != null) spriteVisual.SetActive(false);

        if (spineAnimation != null && spineAnimation.AnimationState != null)
            spineAnimation.AnimationState.SetAnimation(0, idleAnimationName, true);
    }

    void ShowSprite()
    {
        if (spineVisual != null) spineVisual.SetActive(false);
        if (spriteVisual != null) spriteVisual.SetActive(true);
    }

    IEnumerator PlayAttack()
    {
        if (attackClip == null || spriteAnimator == null)
        {
            Debug.LogWarning("[CharacterC] attackClip 또는 spriteAnimator가 비어있어서 재생할 수 없습니다.");
            yield break;
        }

        isAttacking = true;
        ShowSprite();

        // 별도 AnimatorController 없이 클립 하나만 PlayableGraph로 직접 재생 (프로토타입 전용)
        if (!graph.IsValid())
            graph = PlayableGraph.Create("CharacterC_AttackGraph");

        var clipPlayable = AnimationClipPlayable.Create(graph, attackClip);
        var output = AnimationPlayableOutput.Create(graph, "CharacterC_Attack", spriteAnimator);
        output.SetSourcePlayable(clipPlayable);
        graph.Play();

        yield return new WaitForSeconds(attackClip.length);

        graph.Stop();

        isAttacking = false;
        ShowSpine();
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
