using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;

/// <summary>
/// ProCamera2D 에셋이 스스로 못 하는 부분만 보조하는 얇은 헬퍼.
///
/// [2026-09-19 리팩터]
/// 예전엔 이 스크립트가 타겟 추적/오프셋/부드러움까지 코드로 대신 설정해줬지만,
/// 그건 ProCamera2D 컴포넌트 자체의 기능과 완전히 중복이었다. 지금은 그 설정을
/// Main Camera의 ProCamera2D 컴포넌트 인스펙터에서 직접 한다:
///   - CameraTargets 리스트에 추적할 Transform 등록 (오프셋도 원소별 TargetOffset으로 설정)
///   - HorizontalFollowSmoothness / VerticalFollowSmoothness 로 부드러움 설정
///
/// 이 스크립트는 ProCamera2D 혼자서는 처리 못 하는 두 가지만 남아서 보조한다:
///   - Shake: ProCamera2DShake 자체에도 static Instance가 있어서 원래는 안 거쳐도 되지만,
///     Hitbox.cs가 이미 CameraFollow.Shake(...) 형태로 호출하고 있어서 그대로 통과만 시켜준다.
///   - HoldRoom / ReleaseRoom: ProCamera2DRooms엔 static Instance가 없어서 Room.cs가 직접 부를
///     방법이 없다. 이 두 기능만큼은 이 스크립트가 반드시 다리 역할을 해야 한다.
///
/// [2026-09-19 추가, 2026-09-20 정정] 대쉬 중 카메라 지연 효과: PlayerController.IsDashing을
/// 매 프레임 확인해서, 대쉬 중엔 ProCamera2D의 FollowSmoothness를 목표값까지 서서히 끌어올려
/// 카메라가 플레이어를 "느리게 쫓아가는" 느낌을 내고, 대쉬가 끝나면 다시 서서히 원래 값으로
/// 되돌린다. (PlayerController.cs는 건드리지 않는다.)
/// 처음엔 대쉬 시작/종료 순간에 값을 뚝 바꿨는데, 그러면 짧은 대쉬 동안 카메라가 사실상
/// "정지"한 것처럼 보였다 — 그래서 목표값으로 매 프레임 조금씩(smoothnessBlendSpeed) 다가가는
/// 방식으로 바꿔서, 뚝 멈췄다 따라잡는 느낌이 아니라 속도 자체가 스르륵 느려졌다 빨라지게 했다.
///
/// Main Camera 오브젝트에 이 스크립트와 함께 아래 컴포넌트들이 붙어있어야 한다:
///   - ProCamera2DShake   (피격 시 화면 흔들림 — Hitbox.cs가 사용)
///   - ProCamera2DRooms   (보스방 진입 시 카메라 고정 — Room.cs가 사용)
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance;

    [Header("대쉬 중 카메라 지연")]
    [Tooltip("대쉬 중 목표로 삼는 FollowSmoothness 값. 평소 값(ProCamera2D 인스펙터의 기본값)보다 크게 잡을수록 카메라가 플레이어를 더 느리게 쫓아감")]
    public float dashFollowSmoothness = 0.4f;
    [Tooltip("FollowSmoothness가 대쉬 값 ↔ 평소 값 사이를 오가는 속도(초당 변화량). 클수록 빨리 전환되고(정지처럼 보이기 쉬움), 작을수록 천천히 부드럽게 전환됨")]
    public float smoothnessBlendSpeed = 1.5f;

    ProCamera2D proCam;
    ProCamera2DShake proCamShake;
    ProCamera2DRooms proCamRooms;

    float defaultHorizontalSmoothness;
    float defaultVerticalSmoothness;

    void Awake()
    {
        Instance = this;

        proCam = GetComponent<ProCamera2D>();
        proCamShake = GetComponent<ProCamera2DShake>();
        proCamRooms = GetComponent<ProCamera2DRooms>();

        if (proCam == null)
            Debug.LogWarning("[CameraFollow] ProCamera2D 컴포넌트가 없습니다 — 대쉬 카메라 지연 효과가 동작하지 않습니다.");
        else
        {
            // 인스펙터에 설정해둔 평소 값을 기억해뒀다가, 대쉬가 끝나면 이 값으로 되돌린다.
            defaultHorizontalSmoothness = proCam.HorizontalFollowSmoothness;
            defaultVerticalSmoothness = proCam.VerticalFollowSmoothness;
        }

        if (proCamShake == null)
            Debug.LogWarning("[CameraFollow] ProCamera2DShake 컴포넌트가 없습니다 — 피격 화면 흔들림이 동작하지 않습니다.");
        if (proCamRooms == null)
            Debug.LogWarning("[CameraFollow] ProCamera2DRooms 컴포넌트가 없습니다 — 보스룸 카메라 고정이 동작하지 않습니다.");
    }

    void Update()
    {
        if (proCam == null) return;

        bool isDashingNow = PlayerController.Instance != null && PlayerController.Instance.IsDashing;
        float targetH = isDashingNow ? dashFollowSmoothness : defaultHorizontalSmoothness;
        float targetV = isDashingNow ? dashFollowSmoothness : defaultVerticalSmoothness;

        // 목표값으로 뚝 바꾸지 않고 매 프레임 조금씩만 다가가게 해서, "쫓아오다가 갑자기 멈춘 것"
        // 처럼 보이지 않고 "쫓아오는 속도 자체가 스르륵 느려졌다/빨라졌다" 하는 느낌이 나게 한다.
        proCam.HorizontalFollowSmoothness = Mathf.MoveTowards(proCam.HorizontalFollowSmoothness, targetH, smoothnessBlendSpeed * Time.deltaTime);
        proCam.VerticalFollowSmoothness = Mathf.MoveTowards(proCam.VerticalFollowSmoothness, targetV, smoothnessBlendSpeed * Time.deltaTime);
    }

    // ── 흔들림 (Hitbox.cs의 ApplyHit()에서 호출) ─────────────────────────
    // [2026-09-20] smoothness를 짧게 직접 지정한다. ProCamera2DShake.Shake()의 smoothness
    // 기본값(0.1초)은 Hitbox.cs가 쓰는 타격 흔들림 지속시간(0.15초 안팎)에 비해 너무 느려서,
    // SmoothDamp가 빠르게 방향이 바뀌는 진동을 거의 다 뭉개버려 "호출은 되는데 화면은 거의
    // 안 흔들리는" 현상이 났다. 에셋 기본 프리셋들(PlayerHit: Duration 0.04/Smoothness 0.0344,
    // SmallExplosion: Duration 0.253/Smoothness 0.0569)도 전부 Duration 대비 Smoothness를
    // 짧게 잡고 있어서, 그 비율을 참고해 0.04로 맞췄다.
    const float ShakeSmoothness = 0.04f;

    public static void Shake(float magnitude, float duration)
    {
        if (Instance == null || Instance.proCamShake == null) return;

        // [2026-10-18 추가] 설정 창의 '화면 흔들림' 배율 (0% 면 흔들지 않음)
        magnitude *= GameSettings.ShakeScale;
        if (magnitude <= 0f) return;

        Instance.proCamShake.Shake(duration, new Vector2(magnitude, magnitude), smoothness: ShakeSmoothness);
    }

    // ── 방 홀드 (보스방 등에서 플레이어 대신 방 전체를 잡아줄 때, Room.cs에서 호출) ──
    // center: 방을 비출 중심 좌표(월드), viewSize: 화면에 담고 싶은 방의 가로×세로 크기(월드 단위)
    public void HoldRoom(Vector3 center, Vector2 viewSize)
    {
        if (proCamRooms == null) return;

        // 매번 새로 등록/진입 — 이전에 등록해둔 방이 있으면 지우고 딱 하나만 유지한다.
        proCamRooms.Rooms.Clear();
        proCamRooms.AddRoom(
            center.x, center.y, viewSize.x, viewSize.y,
            transitionDuration: 0.3f,
            transitionEaseType: EaseType.EaseOut,
            scaleToFit: true);
        proCamRooms.EnterRoom(0, useTransition: true, forceEntrance: true);
    }

    // 방 홀드 해제 — 다시 target을 따라가는 원래 모드로 복귀 (보스 처치 등)
    public void ReleaseRoom()
    {
        if (proCamRooms == null) return;
        proCamRooms.ExitRoom();
    }

    // ── 즉시 스냅 (RoomManager.LoadRoom()이 플레이어를 새 방 스폰 위치로 순간이동시킨 직후 호출) ──
    // [2026-09-27 3차 수정] 처음엔 ProCamera2D.CenterOnTargets()를 그대로 썼는데, 그게 여전히
    // 정확한 위치로 못 잡는 근본 원인을 찾았다: ProCamera2D 내부의 GetTargetsWeightedMidPoint()가
    // 타겟이 "1개"이고 그 타겟의 TargetInfluenceV가 1보다 작을 때(cf 타겟의 TargetInfluenceV = 0.77)
    // 가중치 정규화를 잘못 계산해서, 최종 Y 좌표가 실제 타겟 위치의 0.77배로 줄어든 값이 나온다
    // (합 = position*0.77 인데 나누는 값도 그냥 1로 정규화해버려서 0.77배 축소된 값이 그대로 남음).
    // 원점(0,0) 근처 방에서는 0.77을 곱해도 오차가 안 보일 만큼 작아서 안 드러났지만, 스폰 위치의
    // Y가 원점에서 먼 방(예: 1S 스테이지 일부 방)에서는 카메라가 실제 목표(cf 위치)보다 상당히
    // 아래를 보게 되어 화면이 엉뚱한 빈 공간을 비추는 문제로 나타났다.
    //
    // 그래서 ProCamera2D의 내장 함수를 거치지 않고, CameraTargets 리스트를 직접 순회해서
    // "제대로" 가중 평균(각 타겟의 영향력을 더한 값으로 나눔)을 계산해 카메라를 그 위치로
    // 정확히 스냅시킨다. cf 타겟 하나뿐인 지금 상황에서는 이 계산이 정확히 cf의 실제 위치와
    // 같아진다. 대쉬 카메라 지연(Update()의 FollowSmoothness 블렌딩)과는 완전히 별개 코드
    // 경로라 서로 간섭하지 않는다.
    public void SnapToTargets()
    {
        if (proCam == null) return;

        var targets = proCam.CameraTargets;
        if (targets == null || targets.Count == 0) return;

        float sumH = 0f, sumV = 0f, weightH = 0f, weightV = 0f;
        foreach (var t in targets)
        {
            if (t == null || t.TargetTransform == null) continue;

            Vector3 pos = t.TargetTransform.position;
            sumH += (pos.x + t.TargetOffset.x) * t.TargetInfluenceH;
            sumV += (pos.y + t.TargetOffset.y) * t.TargetInfluenceV;
            weightH += t.TargetInfluenceH;
            weightV += t.TargetInfluenceV;
        }

        if (weightH <= 0f || weightV <= 0f) return;

        Vector2 finalPos = new Vector2(sumH / weightH, sumV / weightV);
        finalPos += new Vector2(proCam.GetOffsetX(), proCam.GetOffsetY());
        proCam.MoveCameraInstantlyToPosition(finalPos);
    }
}
