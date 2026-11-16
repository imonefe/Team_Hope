using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

/// <summary>
/// [2026-11-17 추가] 대화·상점 초상 칸의 스파인 표시 부품이 원래 그림(Image)의 색을 그대로 따라가게 한다.
/// Npc.cs 는 말하는 사람만 밝게 하려고 초상 Image 의 색을 바꾸는데, Image 는 꺼 두고 스파인이 대신 보이므로
/// 그 색을 매 프레임 옮겨 준다. (Npc.cs 수정 없음)
/// 편집기 메뉴 "EventHorizon/대화 초상 스파인 배치" 가 자동으로 붙인다.
/// </summary>
[RequireComponent(typeof(SkeletonGraphic))]
public class SpinePortraitTint : MonoBehaviour
{
    [Tooltip("색을 가져올 원래 초상 그림. 비워 두면 부모의 Image")]
    public Graphic source;

    SkeletonGraphic target;

    void Awake()
    {
        target = GetComponent<SkeletonGraphic>();
        if (source == null && transform.parent != null) source = transform.parent.GetComponent<Image>();
    }

    void LateUpdate()
    {
        if (source == null || target == null) return;
        if (target.color != source.color) target.color = source.color;
    }
}
