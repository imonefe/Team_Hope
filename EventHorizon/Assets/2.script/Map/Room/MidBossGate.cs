using System.Collections;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// [2026-11-19 추가] 일반 방에 놓이는 중간 보스 입장 코어.
///
/// - F키로 상호작용하면 midBossRoomPrefab 방으로 들어간다 (RoomManager.EnterSubRoom, 화면 전환 연출 포함).
/// - 그 방의 보스를 쓰러뜨리면 원래 방의 이 코어 앞으로 돌아오고,
///   플레이어 조작을 잠근 채 부서지는 애니메이션을 재생한 뒤 부서진 상태로 남는다.
/// - 처치 기록은 RunManager "MidBoss.<종류>" (보스방의 BossCore 와 같은 키) → 이미 쓰러뜨린 종류의 코어는 처음부터 부서진 상태.
/// - 잠김 상태는 없다 (언제든 들어갈 수 있음).
/// 프리팹: 이 컴포넌트 + 상호작용용 트리거 콜라이더 + 자식 SkeletonAnimation (스파인 코어)
/// </summary>
public class MidBossGate : ProximityInteractable
{
    [Header("중간 보스")]
    public BossCoreKind kind = BossCoreKind.Swordsman;
    [Tooltip("들어갈 중간 보스 방 프리팹 (Room + PlayerSpawner + 중간 보스가 들어 있는 방)")]
    public GameObject midBossRoomPrefab;

    [Header("스파인")]
    public SkeletonAnimation skeleton;
    [SpineAnimation(dataField: "skeleton")] public string idleAnim = "idle";
    [SpineAnimation(dataField: "skeleton")] public string breakAnim = "break";
    [Tooltip("부서진 뒤 반복할 애니메이션 (비우면 부서지는 애니메이션의 마지막 장면에서 멈춤)")]
    [SpineAnimation(dataField: "skeleton")] public string brokenAnim = "";
    [Tooltip("부서지는 애니메이션이 없을 때 기다리는 시간 (초)")]
    public float breakFallbackTime = 1.2f;

    [Header("부서진 뒤")]
    [Tooltip("살아 있을 때만 켜 둘 것 (빛, 입자 등)")]
    public GameObject[] activeOnly;
    [Tooltip("부서지면 코어를 아예 숨김")]
    public bool hideWhenBroken = false;

    bool broken;
    bool breaking;
    static MidBossGate entered;   // 지금 들어가 있는 코어

    protected override string PromptLabel => "들어가기";

    protected override bool CanInteract() =>
        !broken && !breaking && midBossRoomPrefab != null && RoomManager.Instance != null && !RoomManager.Instance.InSubRoom;

    void Awake()
    {
        if (skeleton == null) skeleton = GetComponentInChildren<SkeletonAnimation>(true);
    }

    void OnEnable() { RoomManager.OnSubRoomReturned += OnReturned; }
    void OnDisable() { RoomManager.OnSubRoomReturned -= OnReturned; }

    void Start()
    {
        bool defeated = RunManager.Instance != null && RunManager.Instance.GetUpgradeLevel(BossCore.DefeatKey(kind)) > 0;
        if (defeated) SetBroken(true);
        else PlayLoop(idleAnim);
    }

    protected override void Interact()
    {
        if (!CanInteract()) return;
        entered = this;
        Vector3 back = playerObj != null ? playerObj.transform.position : transform.position;
        RoomManager.Instance.EnterSubRoom(midBossRoomPrefab, kind, back);
    }

    void OnReturned(BossCoreKind k, bool defeated)
    {
        if (entered != this) return;
        entered = null;
        if (defeated && k == kind) StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        breaking = true;
        PlayerController pc = PlayerController.Instance;
        if (pc != null) pc.LockControl(this);

        yield return new WaitForSecondsRealtime(0.4f);   // 화면이 밝아지는 동안 잠깐

        float wait = breakFallbackTime;
        if (HasAnim(breakAnim))
        {
            var entry = skeleton.AnimationState.SetAnimation(0, breakAnim, false);
            wait = entry.Animation.Duration;
        }
        yield return new WaitForSeconds(wait);

        SetBroken(false);
        if (pc != null) pc.ResumeControl(this);
        breaking = false;
    }

    void SetBroken(bool instant)
    {
        broken = true;
        if (activeOnly != null) foreach (var g in activeOnly) if (g != null) g.SetActive(false);
        if (hideWhenBroken) { gameObject.SetActive(false); return; }
        if (HasAnim(brokenAnim)) PlayLoop(brokenAnim);
        else if (instant && HasAnim(breakAnim))
        {
            // 처음부터 부서진 상태: 부서지는 애니메이션의 마지막 장면으로
            var entry = skeleton.AnimationState.SetAnimation(0, breakAnim, false);
            entry.TrackTime = entry.Animation.Duration;
        }
    }

    void PlayLoop(string anim)
    {
        if (HasAnim(anim)) skeleton.AnimationState.SetAnimation(0, anim, true);
    }

    bool HasAnim(string anim) =>
        skeleton != null && !string.IsNullOrEmpty(anim) && skeleton.Skeleton != null &&
        skeleton.Skeleton.Data.FindAnimation(anim) != null;
}
