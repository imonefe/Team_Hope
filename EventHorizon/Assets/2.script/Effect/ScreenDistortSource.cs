using UnityEngine;

/// <summary>
/// [2026-11-01 추가] "이 오브젝트는 화면 왜곡용"이라고 표시하는 컴포넌트.
///
/// 사용법: 왜곡 머티리얼(SG_ScreenDistort 계열, Sprite Unlit)을 쓰는 파티클/메쉬/스프라이트 오브젝트에 붙이기만 하면 된다.
///  - 붙어 있는 동안만 ScreenDistortFeature 가 화면을 복사한다 (왜곡 이펙트가 없을 땐 비용 0).
///  - 렌더러에 "왜곡 전용" 렌더링 레이어 표시를 달고, 머티리얼의 UniversalForward 패스를 꺼서
///    일반 그리기에서는 빠지고 ScreenDistortFeature 가 화면 복사 직후에 따로 그리게 한다.
///
/// 왜 따로 그리나: 왜곡은 "내 뒤에 이미 그려진 화면"을 읽어야 하는데, 3D 렌더러(Renderer3D_LightingTest)는
/// 2D 렌더러처럼 중간 화면(_CameraSortingLayerTexture)을 자동으로 만들어 주지 않기 때문.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ScreenDistortSource : MonoBehaviour
{
    /// <summary>왜곡 오브젝트 표시용 렌더링 레이어 비트 (Rendering Layer 8번). 조명 계산에는 영향 없음 (왜곡 셰이더는 Unlit)</summary>
    public const uint RenderingLayerBit = 1u << 7;

    /// <summary>일반 3D 렌더러가 그리는 패스 이름. 왜곡 머티리얼에서는 이 패스를 꺼 둔다</summary>
    public const string ForwardPassName = "UniversalForward";

    static int activeCount;
    static readonly int[] layerCounts = new int[32];

    /// <summary>지금 켜져 있는 왜곡 오브젝트 수</summary>
    public static int ActiveCount => activeCount;

    /// <summary>왜곡 오브젝트들이 올라가 있는 GameObject 레이어 합 (이 레이어를 찍는 카메라에서만 화면 복사)</summary>
    public static int LayerMask
    {
        get
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
                if (layerCounts[i] > 0) mask |= 1 << i;
            return mask;
        }
    }

    int registeredLayer = -1;

    void OnEnable()
    {
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            r.renderingLayerMask |= RenderingLayerBit;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.GetShaderPassEnabled(ForwardPassName))
                    m.SetShaderPassEnabled(ForwardPassName, false);
        }

        registeredLayer = gameObject.layer;
        layerCounts[registeredLayer]++;
        activeCount++;
    }

    void OnDisable()
    {
        if (registeredLayer < 0) return;
        layerCounts[registeredLayer] = Mathf.Max(0, layerCounts[registeredLayer] - 1);
        activeCount = Mathf.Max(0, activeCount - 1);
        registeredLayer = -1;
    }
}
