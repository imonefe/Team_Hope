using UnityEngine;
using DG.Tweening;
using Com.LuisPedroFonseca.ProCamera2D;

/// <summary>대화 연출 때 캐릭터가 움직이는 방식</summary>
public enum BeatMotion { None, Jump, Shake, Punch, Squash }

/// <summary>애니메이션을 재생하는 방식</summary>
public enum BeatAnimMode
{
    Trigger,   // 한 번 재생 (애니메이터 Trigger 파라미터)
    HoldBool,  // 정한 시간 동안 그 상태로 유지 (애니메이터 Bool 파라미터를 켰다가 n초 뒤 끔)
}

/// <summary>
/// 대화 연출 한 덩어리. 이 오브젝트가 켜지는 순간(OnEnable) 아래 연출을 DOTween 시퀀스로 재생한다.
///   애니메이션 / 파티클 / 효과음 / 캐릭터 움직임 / 카메라 이동 / 화면 흔들림 — 쓰지 않는 칸은 비워두면 건너뜀.
///
/// [Npc.cs 와 연결 — Npc.cs 수정 없음]
/// 대화 캔버스의 NPC 인스펙터 "컷신 (선택)" 칸을 그대로 쓴다.
///   Cutscenes            : 이 오브젝트
///   Cutscene Start Lines : 연출을 시작할 대사 줄 번호 (0 부터 셈. 단, 0 번 줄은 Npc.cs 가 부르지 않으므로 1 부터)
///   Cutscene End Lines   : 비워둬도 됨 — 연출이 끝나면 스스로 꺼지고, 다음 대화 때 다시 켜져서 또 재생된다.
///
/// 같은 줄에 연출 여러 개 → 이 오브젝트를 여러 개 만들어 같은 줄 번호로 넣으면 동시에 재생된다.
/// 각 항목의 "지연(초)" 으로 줄이 시작되고 몇 초 뒤에 실행할지 정한다.
/// </summary>
public class DialogueBeat : MonoBehaviour
{
    [Header("애니메이션")]
    [Tooltip("신호를 보낼 캐릭터의 Animator")]
    public Animator animator;
    [Tooltip("Trigger = 한 번 재생 / HoldBool = Hold Seconds 동안 그 상태로 유지")]
    public BeatAnimMode animationMode = BeatAnimMode.Trigger;
    [Tooltip("애니메이터 파라미터 이름 (Animator 의 Parameters 에 있는 이름과 똑같이).\n" +
             "Trigger 모드면 Trigger 파라미터, HoldBool 모드면 Bool 파라미터 이름을 넣는다")]
    public string animatorTrigger;
    public float animationDelay = 0f;
    [Tooltip("HoldBool 모드에서 그 상태로 유지할 시간(초). 끝나면 Bool 을 꺼서 평소 상태로 돌아감")]
    public float animationHoldSeconds = 1.5f;

    [Header("파티클")]
    public GameObject particlePrefab;
    [Tooltip("파티클이 나올 기준 위치 (보통 캐릭터). 비우면 이 오브젝트 위치")]
    public Transform particlePoint;
    [Tooltip("기준 위치에서 얼마나 떨어진 곳 (머리 위 = y 를 키움)")]
    public Vector3 particleOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("켜면 파티클이 기준 위치(캐릭터)를 따라다님")]
    public bool attachToPoint = true;
    [Tooltip("이 시간(초) 뒤 파티클 삭제. 0 이면 삭제 안 함")]
    public float particleLifetime = 2f;
    public float particleDelay = 0f;

    [Header("효과음")]
    public AudioClip sfx;
    public float sfxDelay = 0f;

    [Header("캐릭터 움직임 (DOTween)")]
    [Tooltip("움직일 대상. 물리(Rigidbody2D)가 붙은 오브젝트보다 그림만 있는 자식을 권장")]
    public Transform motionTarget;
    public BeatMotion motion = BeatMotion.None;
    [Tooltip("점프 높이 / 떨림 폭 / 움찔 거리 / 눌림 정도")]
    public float motionStrength = 0.5f;
    public float motionDuration = 0.4f;
    public float motionDelay = 0f;
    [Tooltip("켜면 애니메이션 유지 시간(HoldBool 의 Hold Seconds) 동안 움직임을 계속 반복 — 예: 화난 동안 계속 부들부들")]
    public bool repeatMotionWhileHold = false;

    [Header("카메라 이동")]
    [Tooltip("카메라가 비출 대상. 비우면 카메라 이동 없음")]
    public Transform cameraFocus;
    [Tooltip("대상까지 이동하는 시간(초). 돌아올 때도 같은 시간")]
    public float cameraMoveTime = 0.6f;
    [Tooltip("도착한 뒤 비추고 있는 시간(초). 끝나면 플레이어에게 돌아감")]
    public float cameraHoldSeconds = 1.5f;
    public float cameraDelay = 0f;

    [Header("화면 흔들림")]
    public bool shake = false;
    public float shakeStrength = 0.3f;
    public float shakeDuration = 0.3f;
    public float shakeDelay = 0f;

    [Header("기타")]
    [Tooltip("연출이 끝나면 이 오브젝트를 스스로 끔 → 다음 대화 때 다시 켜져서 또 재생됨")]
    public bool disableWhenDone = true;

    Sequence seq;
    bool boolHeld;   // HoldBool 로 켜둔 Bool 이 아직 켜져 있는지 (중간에 끊겨도 반드시 끄기 위해)

    // 카메라 빈 타겟은 연출 여러 개가 함께 쓴다 (가장 마지막에 카메라를 잡은 연출이 주인)
    static Transform camTarget;
    static DialogueBeat camOwner;
    // 카메라를 빼앗기 전 플레이어 타겟 설정 (돌려줄 때 그대로 복구)
    static Vector2 savedOffset;
    static float savedInfluenceH = 1f, savedInfluenceV = 1f;

    void OnEnable()
    {
        if (seq != null) seq.Kill();
        seq = DOTween.Sequence();
        float end = 0f;   // 연출 전체가 끝나는 시점 (초)

        // 1) 애니메이션
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            if (animationMode == BeatAnimMode.HoldBool)
            {
                // 켜기 → n초 유지 → 끄기
                float off = animationDelay + Mathf.Max(0f, animationHoldSeconds);
                seq.InsertCallback(animationDelay, () =>
                {
                    if (!CheckParam(AnimatorControllerParameterType.Bool)) return;
                    animator.SetBool(animatorTrigger, true);
                    boolHeld = true;
                });
                seq.InsertCallback(off, ReleaseBool);
                end = Mathf.Max(end, off);
            }
            else
            {
                seq.InsertCallback(animationDelay, () =>
                {
                    if (CheckParam(AnimatorControllerParameterType.Trigger)) animator.SetTrigger(animatorTrigger);
                });
            }
        }

        // 2) 파티클
        if (particlePrefab != null)
            seq.InsertCallback(particleDelay, SpawnParticle);

        // 3) 효과음
        if (sfx != null)
            seq.InsertCallback(sfxDelay, () => AudioManager.PlaySFX(sfx));

        // 4) 캐릭터 움직임
        Tween m = BuildMotion();
        if (m != null)
        {
            int loops = 1;
            if (repeatMotionWhileHold && animationMode == BeatAnimMode.HoldBool && motionDuration > 0f)
                loops = Mathf.Max(1, Mathf.RoundToInt(animationHoldSeconds / motionDuration));
            if (loops > 1) m.SetLoops(loops, LoopType.Restart);   // 유지 시간만큼 반복 (시퀀스 안이라 무한 반복은 불가 → 횟수로)
            seq.Insert(motionDelay, m);
            end = Mathf.Max(end, motionDelay + motionDuration * loops);
        }

        // 5) 화면 흔들림
        if (shake)
        {
            seq.InsertCallback(shakeDelay, () => CameraFollow.Shake(shakeStrength, shakeDuration));
            end = Mathf.Max(end, shakeDelay + shakeDuration);
        }

        // 6) 카메라 이동 → 머무르기 → 플레이어에게 복귀
        if (cameraFocus != null && GetProCam() != null)
        {
            float back = cameraDelay + cameraMoveTime + cameraHoldSeconds;
            seq.InsertCallback(cameraDelay, FocusCamera);
            seq.InsertCallback(back, ReleaseCamera);
            end = Mathf.Max(end, back);
        }

        end = Mathf.Max(end, animationDelay, particleDelay, sfxDelay);
        if (seq.Duration() < end) seq.AppendInterval(end - seq.Duration());
        if (seq.Duration() <= 0f) seq.AppendInterval(0.01f);   // 빈 시퀀스 방지

        seq.SetLink(gameObject);
        seq.OnComplete(() =>
        {
            seq = null;   // 먼저 비워둬야 아래 SetActive(false) → OnDisable 에서 다시 Complete 하지 않음
            // isActiveAndEnabled 확인 — 꺼지는 도중(OnDisable 안)에 또 끄면 Unity 에러가 나므로
            if (disableWhenDone && isActiveAndEnabled) gameObject.SetActive(false);
        });
    }

    void OnDisable()
    {
        // 중간에 꺼져도(Cutscene End Lines 등) 움직임은 끝 상태(원래 자리)로 맞추고, 카메라는 반드시 돌려준다
        if (seq != null)
        {
            Sequence s = seq;
            seq = null;
            if (s.IsActive()) s.Complete();
        }
        ReleaseBool();   // Complete() 는 중간 콜백을 건너뛰므로 직접 끈다
        if (camOwner == this) ReleaseCamera();
    }

    // 애니메이터에 그 이름·종류의 파라미터가 있는지 확인 — 없으면 이유를 콘솔에 알려준다
    bool CheckParam(AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name != animatorTrigger) continue;
            if (p.type == type) return true;
            Debug.LogWarning("[DialogueBeat] '" + name + "': 파라미터 '" + animatorTrigger + "' 는 " + p.type +
                             " 인데 Animation Mode 는 " + type + " 용입니다. " +
                             (type == AnimatorControllerParameterType.Bool ? "HoldBool 은 Bool 파라미터가 필요합니다." : "Trigger 모드는 Trigger 파라미터가 필요합니다."), this);
            return false;
        }
        Debug.LogWarning("[DialogueBeat] '" + name + "': 애니메이터 '" + animator.name + "' 에 파라미터 '" + animatorTrigger + "' 가 없습니다.", this);
        return false;
    }

    void ReleaseBool()
    {
        if (!boolHeld) return;
        boolHeld = false;
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
            animator.SetBool(animatorTrigger, false);
    }

    void SpawnParticle()
    {
        Transform point = particlePoint != null ? particlePoint : transform;
        Transform parent = attachToPoint ? point : null;
        GameObject p = Instantiate(particlePrefab, point.position + particleOffset, Quaternion.identity, parent);
        if (particleLifetime > 0f) Destroy(p, particleLifetime);
    }

    Tween BuildMotion()
    {
        if (motionTarget == null || motion == BeatMotion.None) return null;

        float s = motionStrength;
        float d = motionDuration;
        float facing = Mathf.Sign(motionTarget.lossyScale.x);   // 보는 방향 (+1 오른쪽 / -1 왼쪽)

        switch (motion)
        {
            case BeatMotion.Jump:     // 제자리 깡충
                return motionTarget.DOLocalJump(motionTarget.localPosition, s, 1, d);
            case BeatMotion.Shake:    // 좌우로 부들부들
                return motionTarget.DOShakePosition(d, new Vector3(s * 0.3f, 0f, 0f), 30, 0f, false, true);
            case BeatMotion.Punch:    // 보는 방향으로 움찔
                return motionTarget.DOPunchPosition(new Vector3(facing * s * 0.5f, 0f, 0f), d, 6, 0.5f);
            case BeatMotion.Squash:   // 눌렸다 펴짐
                return motionTarget.DOPunchScale(new Vector3(s * 0.3f, -s * 0.3f, 0f), d, 6, 0.5f);
        }
        return null;
    }

    // ── 카메라 ──
    // 카메라 Transform 을 직접 움직이면 ProCamera2D 가 덮어쓰므로, 빈 타겟을 카메라 타겟으로 바꿔 끼우고 그걸 움직인다.
    void FocusCamera()
    {
        ProCamera2D pc = GetProCam();
        if (pc == null || cameraFocus == null) return;

        if (camTarget == null)
        {
            Transform player = GetPlayer();
            camTarget = new GameObject("DialogueCamTarget").transform;
            camTarget.position = player != null ? player.position : pc.transform.position;

            // 플레이어 타겟의 오프셋/영향력을 기억해 둔다 (인스펙터에서 정해둔 화면 위치 그대로 돌아오도록)
            savedOffset = Vector2.zero; savedInfluenceH = 1f; savedInfluenceV = 1f;
            CameraTarget playerEntry = player != null ? pc.CameraTargets.Find(t => t.TargetTransform == player) : null;
            if (playerEntry != null)
            {
                savedOffset = playerEntry.TargetOffset;
                savedInfluenceH = playerEntry.TargetInfluenceH;
                savedInfluenceV = playerEntry.TargetInfluenceV;
                camTarget.position = player.position + (Vector3)savedOffset;   // 지금 화면 그대로에서 출발
            }

            pc.AddCameraTarget(camTarget);
            if (player != null) pc.RemoveCameraTarget(player);
        }

        camOwner = this;
        camTarget.DOKill();
        camTarget.DOMove(cameraFocus.position, cameraMoveTime).SetEase(Ease.InOutSine);
    }

    void ReleaseCamera()
    {
        if (camOwner != this) return;   // 다른 연출이 카메라를 넘겨받았으면 그쪽이 돌려준다
        camOwner = null;
        if (camTarget == null) return;

        ProCamera2D pc = GetProCam();
        Transform player = GetPlayer();
        if (pc != null)
        {
            if (player != null) pc.AddCameraTarget(player, savedInfluenceH, savedInfluenceV, cameraMoveTime, savedOffset);
            pc.RemoveCameraTarget(camTarget, cameraMoveTime);
        }
        camTarget.DOKill();
        Destroy(camTarget.gameObject, cameraMoveTime + 0.1f);
        camTarget = null;
    }

    static ProCamera2D GetProCam()
    {
        return ProCamera2D.Instance;
    }

    static Transform GetPlayer()
    {
        if (PlayerController.Instance != null) return PlayerController.Instance.transform;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");   // PlayerController 가 없는 테스트 씬용
        return tagged != null ? tagged.transform : null;
    }
}
