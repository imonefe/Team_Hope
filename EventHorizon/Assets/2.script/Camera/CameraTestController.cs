using System.Collections;
using UnityEngine;
using DG.Tweening;
using Com.LuisPedroFonseca.ProCamera2D;

/// <summary>
/// 카메라 연출 테스트용 씬 전용 컨트롤러.
/// 버튼 5개(줌인 / 줌아웃 / 포커스 이동 / 애니메이션 재생(색 변환 대체) / 슬로우모션)를
/// 각각 눌렀을 때의 동작을 담당한다. 실제 게임 로직과는 무관한 테스트 전용 스크립트.
/// (2026-09-29: 화면전환 버튼은 ScreenFader.cs가 죽은 코드로 삭제되면서 함께 제거됨)
/// </summary>
public class CameraTestController : MonoBehaviour
{
    [Header("참조")]
    public ProCamera2D proCam;
    public Transform boxA;
    public Transform boxB;
    public Renderer boxARenderer;
    public Renderer boxBRenderer;

    [Header("줌 설정 (orthographic size 절대값)")]
    public float defaultZoomSize = 5f;
    public float zoomInSize = 2.5f;
    public float zoomDuration = 0.4f;

    [Header("포커스 이동 설정")]
    public float focusBlendDuration = 0.6f;

    [Header("애니메이션 대체용 색상 목록 (버튼 누를 때마다 순환)")]
    public Color[] fakeAnimColors = { Color.white, Color.red, Color.yellow, Color.cyan, Color.green };

    [Header("슬로우모션 설정")]
    [Tooltip("슬로우모션 중 timeScale 배율")]
    public float slowMotionScale = 0.15f;
    [Tooltip("슬로우모션을 유지하는 실제(realtime) 시간(초)")]
    public float slowMotionHoldRealSeconds = 2f;
    [Tooltip("느려지고/복귀할 때 걸리는 시간(초, realtime)")]
    public float slowMotionRampSeconds = 0.25f;
    [Tooltip("박스가 좌우로 이동하는 거리")]
    public float boxMoveDistance = 6f;
    [Tooltip("박스 이동에 걸리는 시간(초, timeScale 영향을 받음)")]
    public float boxMoveDuration = 1.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    bool focusOnB = false;
    bool slowMotionRunning = false;
    bool boxesSwapped = false;
    int colorIndex = 0;
    float baseFixedDeltaTime;

    Vector3 boxAHome;
    Vector3 boxBHome;

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;

        if (boxA != null) boxAHome = boxA.position;
        if (boxB != null) boxBHome = boxB.position;

        if (proCam != null && boxA != null)
            proCam.AddCameraTarget(boxA, 1f, 1f, 0f, Vector2.zero);
    }

    // ── 줌인 / 줌아웃 : Zoom()은 "현재 크기에 더할 상대값"을 받으므로 목표 절대 크기와의 차를 계산해서 넘긴다 ──
    public void ZoomIn()
    {
        if (proCam == null) return;
        float currentHalfHeight = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
        proCam.Zoom(zoomInSize - currentHalfHeight, zoomDuration);
    }

    public void ZoomOut()
    {
        if (proCam == null) return;
        float currentHalfHeight = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
        proCam.Zoom(defaultZoomSize - currentHalfHeight, zoomDuration);
    }

    // ── 포커스 이동 : A ↔ B 사이를 왔다갔다 하며 영향력(influence)을 서서히 넘겨준다 ──
    public void ToggleFocus()
    {
        if (proCam == null || boxA == null || boxB == null) return;

        if (!focusOnB)
        {
            proCam.AddCameraTarget(boxB, 1f, 1f, focusBlendDuration, Vector2.zero);
            proCam.RemoveCameraTarget(boxA, focusBlendDuration);
        }
        else
        {
            proCam.AddCameraTarget(boxA, 1f, 1f, focusBlendDuration, Vector2.zero);
            proCam.RemoveCameraTarget(boxB, focusBlendDuration);
        }
        focusOnB = !focusOnB;
    }

    // ── 애니메이션 재생(대체) : 두 박스 색을 순서대로 바꿔서 "애니메이션이 재생됐다"를 흉내낸다 ──
    public void PlayFakeAnimation()
    {
        colorIndex = (colorIndex + 1) % fakeAnimColors.Length;
        Color c = fakeAnimColors[colorIndex];

        // 이 DOTween 버전에는 Material 전용 DOColor 숏컷이 없어서, 범용 DOTween.To(getter/setter)로 직접 트윈한다.
        TweenMaterialColor(boxARenderer, c);
        TweenMaterialColor(boxBRenderer, c);
    }

    static void TweenMaterialColor(Renderer renderer, Color target)
    {
        if (renderer == null) return;
        var mat = renderer.material;
        // URP Lit/Unlit은 "_BaseColor", 빌트인/레거시 셰이더는 "_Color"를 쓴다 — 있는 쪽을 사용
        int propId = mat.HasProperty(BaseColorId) ? BaseColorId : LegacyColorId;
        DOTween.To(() => mat.GetColor(propId), x => mat.SetColor(propId, x), target, 0.3f).SetUpdate(true);
    }

    // ── 슬로우모션 : 박스 두 개가 서로 반대 방향으로 이동하는 도중, 중간 지점에서 2초간 슬로우모션 ──
    public void DoSlowMotion()
    {
        if (slowMotionRunning) return;
        StartCoroutine(SlowMotionRoutine());
    }

    IEnumerator SlowMotionRoutine()
    {
        slowMotionRunning = true;

        // 매번 누를 때마다 홈 위치 ↔ 반대편 위치를 오가도록 토글
        float sign = boxesSwapped ? -1f : 1f;
        Vector3 endA = boxAHome + Vector3.right * boxMoveDistance * sign;
        Vector3 endB = boxBHome - Vector3.right * boxMoveDistance * sign;
        boxesSwapped = !boxesSwapped;

        boxA.DOKill();
        boxB.DOKill();
        boxA.DOMove(endA, boxMoveDuration).SetEase(Ease.InOutSine);
        boxB.DOMove(endB, boxMoveDuration).SetEase(Ease.InOutSine);

        // 이동 절반 지점 근처에서 슬로우모션 진입
        yield return new WaitForSeconds(Mathf.Max(0f, boxMoveDuration * 0.5f - slowMotionRampSeconds * 0.5f));

        yield return RampTimeScale(1f, slowMotionScale, slowMotionRampSeconds);
        yield return new WaitForSecondsRealtime(slowMotionHoldRealSeconds);
        yield return RampTimeScale(slowMotionScale, 1f, slowMotionRampSeconds);

        slowMotionRunning = false;
    }

    IEnumerator RampTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetTimeScale(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetTimeScale(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetTimeScale(to);
    }

    void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }

    // [2026-09-29 추가] 슬로우모션 코루틴 도중 이 오브젝트가 SetActive(false)로 비활성화되면
    // 코루틴이 그 자리에서 멈춰버리고(파괴되는 게 아니라서 OnDestroy도 안 불림) 다시 재생되지
    // 않기 때문에, timeScale이 slowMotionScale에 눌린 채로 영구히 남는 버그가 있었다.
    // 비활성화 시점에도 OnDestroy와 동일하게 복구해준다.
    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
        slowMotionRunning = false; // 멈춘 코루틴이 다시 false로 안 돌아오므로 여기서 같이 리셋
    }

    void OnDestroy()
    {
        // 씬을 나갈 때 실수로 timeScale이 눌린 채 남지 않도록 복구
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }
}
