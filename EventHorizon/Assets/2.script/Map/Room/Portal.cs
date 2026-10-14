using System.Collections;
using UnityEngine;

public class Portal : ProximityInteractable
{
    [Header("스프라이트")]
    public SpriteRenderer spriteRenderer;
    public Sprite inactiveSprite;
    public Sprite activeSprite;

    [Header("콜라이더")]
    public Collider2D portalCollider;

    [Header("애니메이션")]
    public Animator animator;
    public string openTrigger = "Open";

    bool isActive = false;
    bool transitioning = false; // [2026-10-10] 페이드 진행 중 F를 다시 눌러 전환이 겹치지 않게

    void Awake()
    {
        SetActive(false);
    }

    public void SetActive(bool value)
    {
        isActive = value;

        if (spriteRenderer != null)
            spriteRenderer.sprite = isActive ? activeSprite : inactiveSprite;

        if (portalCollider != null)
            portalCollider.enabled = isActive;

        if (isActive && animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger(openTrigger);
        }
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동.
    protected override bool CanInteract() => isActive && !transitioning;

    protected override void Interact()
    {
        StartCoroutine(OpenMapWithFade());
    }

    IEnumerator OpenMapWithFade()
    {
        if (MapManager.Instance == null)
        {
            Debug.LogWarning("[Portal] MapManager 없음!");
            yield break;
        }

        transitioning = true;
        if (TransitionCanvas.Instance != null)
            yield return StartCoroutine(TransitionCanvas.Instance.Transition(() =>
            {
                MapManager.Instance.OpenMap();
            }));
        else
            MapManager.Instance.OpenMap();
        transitioning = false;
    }
}
