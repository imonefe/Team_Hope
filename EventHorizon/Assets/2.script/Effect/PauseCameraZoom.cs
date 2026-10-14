using System.Collections.Generic;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

/// <summary>
/// 퍼즈(Q키)가 발동되어 있는 동안 카메라를 넓게 잡는다 (줌 아웃). 퍼즈가 끝나면 원래 크기로 돌아온다.
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 스스로 생성되고 씬이 바뀌어도 유지된다.
/// PauseManager.Instance.IsPaused 값만 지켜보므로 기존 스크립트는 수정하지 않았다.
/// 설정(얼마나 넓힐지, 시간)은 Resources/PauseGlitchSettings 의 "퍼즈 중 화면 넓게 잡기" 칸에서 조절한다.
///
/// SampleScene 처럼 카메라가 두 대일 때(원근 3D 카메라 + 그 자식인 직교 2D 카메라)
/// 배경과 캐릭터 크기가 어긋나지 않도록 두 대를 같은 배율로 동시에 넓힌다.
///  - ProCamera2D 가 붙은 카메라: ProCamera2D.UpdateScreenSize 로 (카메라 경계·방 고정 기능이 새 크기를 알도록)
///  - 그 아래(자식)의 직교 카메라들: orthographicSize 를 같은 배율로
/// </summary>
public class PauseCameraZoom : MonoBehaviour
{
    const string SettingsPath = "PauseGlitchSettings";

    static PauseCameraZoom instance;

    PauseGlitchSettings settings;

    float progress;            // 0 = 원래 크기, 1 = 가장 넓게
    bool captured;             // 원래 크기를 기억해 둔 상태인지
    ProCamera2D proCam;
    float baseProSize;         // ProCamera2D 화면 높이의 절반 (원래 값)
    readonly List<Camera> orthoCams = new List<Camera>();
    readonly List<float> orthoBaseSizes = new List<float>();

    // [2026-10-10 추가] 퍼즈 중에 방 고정(보스방 진입 등) 화면 전환이 일어나면, 퍼즈 시작 때 기억한 크기로 되돌리면서
    // 방 크기를 덮어썼다 → 전환 중에는 손대지 않고, 전환이 끝나면 그 크기를 새 기준으로 다시 기억한다
    ProCamera2DRooms rooms;
    bool roomTransitioning;
    bool needRebase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        PauseGlitchSettings s = Resources.Load<PauseGlitchSettings>(SettingsPath);
        if (s == null) return;   // 설정이 없으면 조용히 끔 (PauseGlitchEffect 쪽에서 이미 경고함)

        GameObject go = new GameObject("[PauseCameraZoom]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseCameraZoom>();
        instance.settings = s;
    }

    void Update()
    {
        bool paused = settings.cameraZoomEnabled
                      && PauseManager.Instance != null
                      && PauseManager.Instance.IsPaused;

        // 진행도: 퍼즈 중이면 1 쪽으로, 아니면 0 쪽으로 (실제 시간 기준)
        float dt = Time.unscaledDeltaTime;
        float time = paused ? settings.zoomInTime : settings.zoomOutTime;
        float speed = time > 0f ? 1f / time : 1000f;
        progress = Mathf.MoveTowards(progress, paused ? 1f : 0f, speed * dt);

        if (progress <= 0f)
        {
            if (captured) Restore();
            return;
        }

        // 막 넓어지기 시작함 → 지금 크기를 원래 값으로 기억
        if (!captured && !Capture()) return;

        // 씬이 바뀌어 카메라가 사라졌으면 정리
        if (proCam == null && orthoCams.Count == 0)
        {
            captured = false;
            return;
        }

        // 방 전환 중에는 방 쪽이 크기를 정하게 둔다
        if (roomTransitioning) return;
        if (needRebase) Rebase();

        float eased = Mathf.SmoothStep(0f, 1f, progress);
        Apply(Mathf.Lerp(1f, settings.zoomScale, eased));
    }

    void OnRoomTransitionStarted(int a, int b) { roomTransitioning = true; }
    void OnRoomTransitionFinished(int a, int b) { roomTransitioning = false; needRebase = true; }

    // 방 전환이 끝난 지금 크기를 "원래 크기"로 다시 기억 (방 전환이 크기를 배율 없이 직접 정했으므로)
    void Rebase()
    {
        needRebase = false;
        if (proCam != null) baseProSize = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;
        for (int i = 0; i < orthoCams.Count; i++)
            if (orthoCams[i] != null) orthoBaseSizes[i] = orthoCams[i].orthographicSize;
    }

    // 원래 크기 기억
    bool Capture()
    {
        // ProCamera2D.Instance 는 카메라가 없으면 예외를 던지므로 직접 찾는다 (퍼즈 시작 때 한 번만)
        proCam = FindAnyObjectByType<ProCamera2D>();
        if (proCam == null || !proCam.isActiveAndEnabled) { proCam = null; return false; }

        baseProSize = proCam.ScreenSizeInWorldCoordinates.y * 0.5f;

        orthoCams.Clear();
        orthoBaseSizes.Clear();
        foreach (Camera c in proCam.GetComponentsInChildren<Camera>())
        {
            if (c == proCam.GameCamera || !c.orthographic) continue;   // ProCamera2D 본인 카메라는 UpdateScreenSize 가 맡음
            orthoCams.Add(c);
            orthoBaseSizes.Add(c.orthographicSize);
        }

        rooms = proCam.GetComponent<ProCamera2DRooms>();
        if (rooms != null)
        {
            rooms.OnStartedTransition.AddListener(OnRoomTransitionStarted);
            rooms.OnFinishedTransition.AddListener(OnRoomTransitionFinished);
        }
        roomTransitioning = false;
        needRebase = false;

        captured = true;
        return true;
    }

    void Apply(float factor)
    {
        if (proCam != null && baseProSize > 0f)
            proCam.UpdateScreenSize(baseProSize * factor);   // 시간 0 = 즉시 (매 프레임 부르는 용도)

        for (int i = 0; i < orthoCams.Count; i++)
        {
            if (orthoCams[i] != null)
                orthoCams[i].orthographicSize = orthoBaseSizes[i] * factor;
        }
    }

    // 원래 크기로 정확히 되돌림
    void Restore()
    {
        if (!roomTransitioning) Apply(1f); // 방 전환 중이면 방 쪽이 정한 크기를 그대로 둔다
        if (rooms != null)
        {
            rooms.OnStartedTransition.RemoveListener(OnRoomTransitionStarted);
            rooms.OnFinishedTransition.RemoveListener(OnRoomTransitionFinished);
            rooms = null;
        }
        roomTransitioning = false;
        needRebase = false;
        captured = false;
        proCam = null;
        orthoCams.Clear();
        orthoBaseSizes.Clear();
    }
}
