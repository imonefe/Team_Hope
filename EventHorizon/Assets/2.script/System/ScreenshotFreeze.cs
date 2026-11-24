using UnityEngine;

/// <summary>
/// [2026-11-24 추가] 스크린샷용 화면 정지 — 창(일시정지 캔버스) 없이 게임 시간만 멈춘다. (씬에 놓을 필요 없음, 게임이 켜지면 스스로 생김)
///
///  - F9 : 멈춤 / 다시 흐름 (같은 키로 번갈아)
///    (F8 은 증강 시험 방 키라서 겹치지 않게 F9 로 — 2026-11-24)
///  - 멈추는 방식은 다른 창과 같은 GamePause 를 쓴다 → ESC 메뉴 등을 열었다 닫아도 정지가 풀리지 않음
///  - 전시가 끝나 필요 없으면 아래 Enabled 를 false 로.
/// </summary>
public class ScreenshotFreeze : MonoBehaviour
{
    public const bool Enabled = true;
    const KeyCode FreezeKey = KeyCode.F9;   // [2026-11-24] F8 → F9 (증강 시험 방 키와 겹침)

    static ScreenshotFreeze instance;
    bool frozen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || instance != null) return;
        var go = new GameObject("[ScreenshotFreeze]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenshotFreeze>();
    }

    void Update()
    {
        if (!Input.GetKeyDown(FreezeKey)) return;

        frozen = !frozen;
        if (frozen) GamePause.Push(this);
        else GamePause.Pop(this);
    }

    // 씬이 바뀌면 GamePause 가 기록을 지우므로 여기 상태도 맞춰 둔다
    void OnEnable() { GamePause.OnChanged += Sync; }
    void OnDisable() { GamePause.OnChanged -= Sync; }
    void Sync() { if (frozen && !GamePause.Has(this)) frozen = false; }
}
