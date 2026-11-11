using UnityEngine;

/// <summary>
/// [2026-11-11 추가] 프롤로그(로비 첫 대화)가 끝나면 화면 가운데에 조작법 창을 띄우고, 아무 키나 누르면 닫는다.
///
/// - 창 자체는 씬에 만들어 둔 오브젝트(battle Canvas 아래 ControlsGuidePopup)다.
///   글자·키·위치는 에디터에서 직접 고치면 된다 (코드는 보이기/숨기기만 담당).
/// - 떠 있는 동안 플레이어 조작은 잠긴다. 대화를 넘기던 키에 바로 닫히지 않도록 closeDelay 동안은 입력을 무시.
/// - 런마다 로비가 새로 만들어지므로 한 판에 한 번 나온다 (LobbyIntroDialogue가 호출).
/// - [2026-11-11] 전용 캔버스(ControlsGuideCanvas, 그리기 순서 400)에 있고, 떠 있는 동안은
///   CombatUIGate 를 잠가 전투 UI·미니맵을 숨긴다. 뒤 화면은 Dim(검정 72%)으로 어둡게.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ControlsGuidePopup : MonoBehaviour
{
    public static ControlsGuidePopup Instance { get; private set; }
    /// <summary>[2026-11-11] 지금 창이 떠 있는지 (영복이 대사 등이 끼어들지 않게)</summary>
    public static bool IsShowing => Instance != null && Instance.showing;

    [Tooltip("창이 뜬 뒤 이 시간(초)이 지나야 키 입력으로 닫힘")]
    public float closeDelay = 0.5f;

    CanvasGroup group;
    bool showing;
    float shownAt;
    PlayerController lockedPlayer;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;
    }

    void OnDestroy()
    {
        Close();
        if (Instance == this) Instance = null;
    }

    /// <summary>창을 띄운다 (씬에 창이 없으면 아무것도 안 함)</summary>
    public static void ShowNow()
    {
        if (Instance != null) Instance.Show();
    }

    void Show()
    {
        if (showing) return;
        showing = true;
        shownAt = Time.unscaledTime;
        group.alpha = 1f;
        CombatUIGate.Block();   // [2026-11-11] 전투 UI·미니맵 숨김
        // [2026-11-11] 안전장치: 창이 전투 UI 안에 들어 있으면(예전 배치) 위에서 같이 꺼져 버려
        // 창은 안 보이고 조작만 잠기는 상태가 된다 → 이 경우 바로 되돌리고 경고만 남김
        if (!gameObject.activeInHierarchy)
        {
            CombatUIGate.Unblock();
            showing = false;
            Debug.LogWarning("[ControlsGuidePopup] 창이 전투 UI 안에 있어 같이 숨겨집니다. ControlsGuideCanvas 밑으로 옮겨야 합니다.");
            return;
        }
        lockedPlayer = PlayerController.Instance;
        if (lockedPlayer != null) lockedPlayer.LockControl(this);
    }

    void Update()
    {
        if (!showing) return;
        if (Time.unscaledTime - shownAt < closeDelay) return;
        if (Input.anyKeyDown) Close();
    }

    void Close()
    {
        if (!showing) return;
        showing = false;
        if (group != null) group.alpha = 0f;
        CombatUIGate.Unblock();
        if (lockedPlayer != null) lockedPlayer.ResumeControl(this);
        lockedPlayer = null;
    }
}
