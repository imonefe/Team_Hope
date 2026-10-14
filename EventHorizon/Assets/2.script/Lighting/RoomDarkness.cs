using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 방에 들어가면 화면이 어두워지고, LightBeam(빛줄기)이 닿는 곳만 밝게 보인다.
/// 방 프리팹의 최상위(Room 컴포넌트가 있는 오브젝트)에 붙이면 된다. 방을 나가 이 방이 지워지면 어둠도 서서히 꺼진다.
///
/// 편집 중에도 게임 화면(Game 탭)과 씬 화면에서 미리 볼 수 있다. (previewInEditor)
/// 실제 처리: DarknessSystem(상태) → DarknessScreenFeature(화면 처리)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class RoomDarkness : MonoBehaviour
{
    [Header("어둠")]
    [Tooltip("0 = 평소 밝기, 1 = 완전 깜깜. 0.8 정도면 형태만 겨우 보이는 어둠")]
    [Range(0f, 1f)] public float darkness = 0.8f;

    [Tooltip("어두운 부분의 색감. 흰색이면 그냥 어둡고, 푸른색이면 밤 같은 느낌")]
    public Color ambientTint = new Color(0.7f, 0.78f, 1f, 1f);

    [Tooltip("빛이 겹칠 때 최대 밝기. 1 = 원래 밝기까지만, 1보다 크면 빛 속이 원래보다 더 환해짐")]
    [Range(1f, 3f)] public float maxBrightness = 1.3f;

    [Header("전환")]
    [Tooltip("방에 들어왔을 때 어두워지는 데 걸리는 시간 (초)")]
    [Min(0f)] public float fadeInTime = 0.5f;

    [Tooltip("방을 나갔을 때 밝아지는 데 걸리는 시간 (초)")]
    [Min(0f)] public float fadeOutTime = 0.5f;

    [Header("편집")]
    [Tooltip("재생하지 않을 때도 게임 화면 / 씬 화면에서 어둠을 미리 보여줌")]
    public bool previewInEditor = true;

    void OnEnable() { DarknessSystem.Register(this); }
    void OnDisable() { DarknessSystem.Unregister(this); }

    public DarknessSystem.Settings ToSettings()
    {
        return new DarknessSystem.Settings
        {
            darkness = darkness,
            ambientTint = ambientTint,
            maxBrightness = maxBrightness,
        };
    }
}
