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

    [Header("이동할 방 (선택, 2026-10-17 추가)")]
    [Tooltip("비워두면 노드맵을 연다. 방 프리팹을 넣으면 노드맵 대신 그 방으로 바로 이동한다 (예: 로비 → 첫 방)")]
    public GameObject targetRoom;

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
        if (targetRoom != null)
            StartCoroutine(LoadRoomWithFade());
        else
            StartCoroutine(OpenMapWithFade());
    }

    // [2026-10-17 추가] 노드맵을 거치지 않고 정해진 방으로 바로 이동 (로비 → 첫 방)
    // 방이 바뀌면 이 포탈(지금 방의 자식)도 같이 지워지므로, 페이드는 씬에 계속 남는
    // TransitionCanvas 쪽에서 돌린다 (포탈에서 돌리면 화면이 까만 채로 멈춤)
    IEnumerator LoadRoomWithFade()
    {
        if (RoomManager.Instance == null)
        {
            Debug.LogWarning("[Portal] RoomManager 없음!");
            yield break;
        }

        transitioning = true;
        GameObject room = targetRoom;
        if (TransitionCanvas.Instance != null)
            TransitionCanvas.Instance.StartCoroutine(TransitionCanvas.Instance.Transition(() =>
            {
                RoomManager.Instance.LoadRoom(room);
            }));
        else
            RoomManager.Instance.LoadRoom(room);
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
