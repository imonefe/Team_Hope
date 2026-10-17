using UnityEngine;

/// <summary>
/// "플레이어가 근처에 있을 때 F키를 누르면 상호작용" 패턴의 공용 베이스.
/// Box.cs / Portal.cs / Item.cs / npcTrigger.cs가 각자 이 패턴(Player 태그 콜라이더 감지 +
/// playerNearby 플래그 + Update에서 F키 감지)을 복붙해서 쓰고 있던 걸 여기로 합쳤다.
///
/// 사용법: MonoBehaviour 대신 이 클래스를 상속받고,
///  - Interact() : F키를 눌렀을 때 실제로 할 동작 (필수 구현)
///  - CanInteract() : "지금 상호작용 가능한 상태인가" 추가 조건이 있으면 재정의
///                     (기본은 항상 true. 예: Box는 열려있지 않을 때만, npcTrigger는 대화 중이 아닐 때만)
///  - OnPlayerEnter(Collider2D) / OnPlayerExit(Collider2D) : 플레이어가 들어오고 나갈 때
///                     추가로 할 일이 있으면 재정의 (예: Item의 근접 팝업 표시/숨김)
///  - PromptLabel : [2026-10-17 추가] 가까이 갔을 때 오브젝트 위에 "[F] 열기" 같은 안내를 띄우려면
///                     안내 문구를 돌려주게 재정의 (기본 null = 안내 없음. 예: Box "열기", npcTrigger "대화")
/// 기존 OnTriggerEnter2D/OnTriggerExit2D/Update를 직접 재정의할 필요는 없다 — 이미 여기서
/// 처리하고, 필요한 지점만 위 3개 훅으로 열어뒀다.
/// </summary>
public abstract class ProximityInteractable : MonoBehaviour
{
    [Tooltip("상호작용 키")]
    public KeyCode interactKey = KeyCode.F;

    [Header("상호작용 안내 (안내 문구가 있는 것만: 상자·NPC 등)")]
    [Tooltip("오브젝트 그림 맨 위에 뜨는 \"[F] 열기\" 글자의 추가 위치 조정 (월드 단위)")]
    public Vector3 promptOffset = Vector3.zero;

    protected bool playerNearby;
    protected GameObject playerObj;
    InteractPrompt prompt;

    /// <summary>지금 상호작용 가능한 상태인가 (기본: 항상 가능). Box/Portal/npcTrigger처럼
    /// "열려있지 않을 때만", "대화 중이 아닐 때만" 같은 추가 조건이 있으면 재정의한다.</summary>
    protected virtual bool CanInteract() => true;

    /// <summary>F키를 눌렀을 때 실제로 할 동작.</summary>
    protected abstract void Interact();

    /// <summary>플레이어가 범위에 들어온 직후 추가로 할 일 (필요한 스크립트만 재정의).</summary>
    protected virtual void OnPlayerEnter(Collider2D other) { }

    /// <summary>플레이어가 범위를 벗어난 직후 추가로 할 일 (필요한 스크립트만 재정의).</summary>
    protected virtual void OnPlayerExit(Collider2D other) { }

    /// <summary>[2026-10-17 추가] 머리 위 안내 문구 ("열기", "대화" 등). null 이면 안내를 띄우지 않는다.</summary>
    protected virtual string PromptLabel => null;

    protected virtual void Update()
    {
        // [2026-10-17 추가] 머리 위 안내 — 지금 F를 누르면 실제로 반응할 때만 보인다
        UpdatePrompt();

        if (!playerNearby) return;
        // [2026-10-10 추가] 창(노드맵·ESC 메뉴·인벤토리·게임오버)이 떠 있거나, 대화 중이거나,
        // 방금 같은 F로 대화를 끝낸 프레임이면 상호작용하지 않는다
        if (Time.timeScale == 0f) return;
        if (NPC.AnyTalking) return;
        if (Time.frameCount == NPC.LastEndFrame) return;
        if (!CanInteract()) return;

        if (Input.GetKeyDown(interactKey))
            Interact();
    }

    void UpdatePrompt()
    {
        string label = PromptLabel;
        if (label == null) return;

        bool show = playerNearby && playerObj != null
                    && Time.timeScale != 0f && !NPC.AnyTalking && CanInteract();
        if (show)
        {
            if (prompt == null) prompt = InteractPrompt.Create(transform);
            if (prompt != null) prompt.Show(interactKey.ToString(), label, promptOffset);
        }
        else if (prompt != null) prompt.Hide();
    }

    protected virtual void OnDisable()
    {
        if (prompt != null) prompt.Hide();
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = true;
        playerObj = other.gameObject;
        OnPlayerEnter(other);
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = false;
        playerObj = null;
        if (prompt != null) prompt.Hide();
        OnPlayerExit(other);
    }
}
