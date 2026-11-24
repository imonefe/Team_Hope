using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [2026-11-22 추가 · 11-24 30%→15%] 보스급(중간 보스 · 보스) 처치 연출 — 게임 속도를 15%로 떨어뜨렸다가 1.2초(실제 시간)에 걸쳐 원래대로.
/// [2026-11-24 추가] 같은 시간 동안 카메라가 플레이어와 보스 사이를 조금 확대했다가, 슬로우가 풀리는 만큼 함께 원래대로.
/// BossController.OnTrueDeath() 가 Play(보스) 를 부른다.
/// - 메뉴 · 인벤토리 등 창이 시간을 멈춘 동안(GamePause)은 속도를 건드리지 않고 기다린다.
/// - 씬이 바뀌거나 중간에 다시 불리면 처음부터 다시.
///
/// 확대는 "화면을 그리는 순간에만" 건다: 그리기 직전에 카메라 시야를 좁히고 초점 쪽으로 옮겼다가, 다 그린 직후 원래 값으로 되돌린다.
/// 그래서 ProCamera2D(추적 · 보스방 고정 · 고정 풀기 전환)와 PauseCameraZoom 은 확대를 전혀 모르고 평소대로 움직인다.
///  - 3D 카메라(배경, ProCamera2D 본체): 시야각을 줄여 확대
///  - 그 자식 2D 카메라(캐릭터 · 타일): 직교 크기를 같은 배율로 줄여 확대
///  - 위치: 플레이어와 보스의 중간 쪽으로 옮기되, 원래 화면 밖은 보이지 않을 만큼만
/// </summary>
public class BossSlowMotion : MonoBehaviour
{
    public const float StartScale = 0.15f;
    public const float RecoverSeconds = 1.2f;

    // 확대 배율 — 0.8 이면 화면의 80% 만 보임 (20% 확대)
    public const float ZoomScale = 0.8f;
    // 처치 순간 확대되는 시간 (실제 시간 초)
    public const float ZoomInSeconds = 0.15f;

    static BossSlowMotion runner;
    Coroutine playing;

    /// <summary>[2026-11-24] 슬로우가 아직 원래 속도로 돌아오지 않았으면 true — 증강 창이 이게 끝나길 기다린다</summary>
    public static bool IsPlaying => runner != null && runner.playing != null;

    Transform focusBoss;
    Vector3 lastBossPos;
    bool hasBoss;
    float zoomWeight;   // 0 = 확대 없음, 1 = 가장 확대

    public static void Play() => Play(null);

    public static void Play(Transform boss)
    {
        if (runner == null)
        {
            var go = new GameObject("[BossSlowMotion]");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<BossSlowMotion>();
        }
        runner.focusBoss = boss;
        runner.hasBoss = boss != null;
        if (boss != null) runner.lastBossPos = boss.position;
        if (runner.playing != null) runner.StopCoroutine(runner.playing);
        runner.playing = runner.StartCoroutine(runner.Run());
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < RecoverSeconds)
        {
            if (!GamePause.IsPaused)
            {
                float k = t / RecoverSeconds;
                Time.timeScale = Mathf.Lerp(StartScale, 1f, k * k);   // 처음엔 느리게 머물다 빨리 돌아옴
                Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime * Time.timeScale;
                // 확대: 처음 ZoomInSeconds 동안 빠르게 들어갔다가, 슬로우가 풀리는 곡선(k²)과 똑같이 빠짐
                float zin = ZoomInSeconds > 0f ? Mathf.SmoothStep(0f, 1f, t / ZoomInSeconds) : 1f;
                zoomWeight = Mathf.Min(zin, 1f - k * k);
                t += Time.unscaledDeltaTime;
            }
            yield return null;
        }
        if (!GamePause.IsPaused) Time.timeScale = 1f;
        Time.fixedDeltaTime = GamePause.DefaultFixedDeltaTime;
        zoomWeight = 0f;
        playing = null;
    }

    // ── 그리는 순간에만 확대 ──
    void OnEnable()
    {
        RenderPipelineManager.beginContextRendering += BeginRender;
        RenderPipelineManager.endContextRendering += EndRender;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= BeginRender;
        RenderPipelineManager.endContextRendering -= EndRender;
        EndRender(default, null);
    }

    bool applied;
    Transform camRoot;
    Camera cam3D, cam2D;
    Vector3 savedPos;
    float savedFov, savedOrtho;

    void BeginRender(ScriptableRenderContext ctx, List<Camera> cams)
    {
        if (applied || zoomWeight <= 0.0001f) return;
        var follow = CameraFollow.Instance;
        if (follow == null) return;
        cam3D = follow.GetComponent<Camera>();
        cam2D = follow.camera2D;
        if (cam3D == null) return;
        camRoot = follow.transform;

        float f = Mathf.Lerp(1f, ZoomScale, zoomWeight);

        // 초점: 플레이어와 보스의 중간 (보스가 사라졌으면 마지막 위치)
        if (focusBoss != null) lastBossPos = focusBoss.position;
        Vector3 focus = lastBossPos;
        var player = PlayerController.Instance;
        if (player != null) focus = hasBoss ? (player.transform.position + lastBossPos) * 0.5f : player.transform.position;
        else if (!hasBoss) focus = camRoot.position;

        // 원래 화면 크기 (2D 카메라 기준, 없으면 3D 카메라에서 계산)
        float halfH = cam2D != null ? cam2D.orthographicSize
                    : Mathf.Abs(camRoot.position.z) * Mathf.Tan(cam3D.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * cam3D.aspect;
        // 확대된 화면이 원래 화면 밖으로 나가지 않을 만큼만 옮김
        float maxX = (1f - f) * halfW, maxY = (1f - f) * halfH;
        Vector3 center = camRoot.position;
        Vector2 shift = new Vector2(Mathf.Clamp(focus.x - center.x, -maxX, maxX), Mathf.Clamp(focus.y - center.y, -maxY, maxY));

        savedPos = camRoot.position;
        savedFov = cam3D.fieldOfView;
        savedOrtho = cam2D != null ? cam2D.orthographicSize : 0f;

        camRoot.position = savedPos + new Vector3(shift.x, shift.y, 0f);
        if (!cam3D.orthographic)
            cam3D.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(savedFov * 0.5f * Mathf.Deg2Rad) * f) * Mathf.Rad2Deg;
        if (cam2D != null) cam2D.orthographicSize = savedOrtho * f;
        applied = true;
    }

    void EndRender(ScriptableRenderContext ctx, List<Camera> cams)
    {
        if (!applied) return;
        applied = false;
        if (camRoot != null) camRoot.position = savedPos;
        if (cam3D != null) cam3D.fieldOfView = savedFov;
        if (cam2D != null) cam2D.orthographicSize = savedOrtho;
    }

    void OnDestroy()
    {
        if (runner == this) runner = null;
    }
}
