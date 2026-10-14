using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 볼류메트릭 라이트 테스트 전용 장치.
/// - 위치·조명 값은 전혀 건드리지 않습니다. (프리팹에 둔 그대로)
/// - 실행 중에만 그림자 거리를 늘리고(배경이 카메라에서 70~150 거리라서), 끄면 원래 값으로 되돌립니다.
/// </summary>
public class VolumetricTestRig : MonoBehaviour
{
    [Header("그림자 거리 (실행 중에만 적용)")]
    public bool overrideShadowDistance = true;
    public float shadowDistance = 200f;

    UniversalRenderPipelineAsset urp;
    float savedShadowDistance = -1f;

    void OnEnable()
    {
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (Application.isPlaying && overrideShadowDistance && urp != null)
        {
            savedShadowDistance = urp.shadowDistance;
            urp.shadowDistance = shadowDistance;
        }
    }

    void OnDisable()
    {
        if (urp != null && savedShadowDistance >= 0f)
        {
            urp.shadowDistance = savedShadowDistance;
            savedShadowDistance = -1f;
        }
    }
}
