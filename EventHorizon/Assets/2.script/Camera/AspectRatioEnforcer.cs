using UnityEngine;

/// <summary>
/// 화면 비율을 목표 비율(기본 16:9)로 고정한다.
/// 창이 목표보다 넓으면 좌우에, 좁으면 위아래에 검은 여백(레터박스)을 넣어
/// 게임 화면과 UI가 항상 같은 비율로 보이게 만든다. → UI가 찌그러지거나 잘리지 않음.
///
/// 사용법:
/// 1. 메인 카메라에 이 스크립트를 붙인다.
/// 2. targetWidth / targetHeight 를 게임 기준 비율로 (16:9면 16/9).
/// 3. 레터박스 여백을 검게 채우려면 배경 카메라가 필요할 수 있음 (아래 note 참고).
///
/// Canvas 는 Screen Space - Camera 로 이 카메라에 연결하거나,
/// Overlay 를 쓰더라도 카메라 rect 안에서 보이도록 배치하면 된다.
/// </summary>
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class AspectRatioEnforcer : MonoBehaviour
{
    [Tooltip("목표 가로 비율 (16:9 이면 16)")]
    public float targetWidth = 16f;
    [Tooltip("목표 세로 비율 (16:9 이면 9)")]
    public float targetHeight = 9f;

    [Tooltip("레터박스 여백 색 (보통 검정)")]
    public Color letterboxColor = Color.black;

    Camera cam;
    Camera bgCam; // 여백을 채우는 배경 카메라

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        ApplyAspect();
        SetupBackgroundCamera();
    }

    // [2026-09-29 추가] 이 컴포넌트가 꺼지거나(다른 카메라 리그로 교체 등) 오브젝트 자체가
    // 파괴될 때 SetupBackgroundCamera()에서 만든 LetterboxCamera를 같이 정리한다.
    // 이게 없으면 카메라 리그를 런타임에 교체/리스폰할 때마다 소유자 없는 LetterboxCamera가
    // 계속 남아 화면 전체를 계속 렌더링하게 된다.
    void OnDisable()
    {
        if (bgCam != null)
        {
            if (Application.isPlaying)
                Destroy(bgCam.gameObject);
            else
                DestroyImmediate(bgCam.gameObject);
            bgCam = null;
        }
    }

    void Update()
    {
        // 창 크기가 바뀔 수 있으므로 매 프레임 확인 (에디터/빌드 모두)
        ApplyAspect();
    }

    void ApplyAspect()
    {
        if (cam == null) cam = GetComponent<Camera>();

        float targetAspect = targetWidth / targetHeight;
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect = cam.rect;

        if (scaleHeight < 1f)
        {
            // 창이 목표보다 세로로 김 → 위아래 레터박스
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
        }
        else
        {
            // 창이 목표보다 가로로 넓음 → 좌우 레터박스
            float scaleWidth = 1f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
        }

        cam.rect = rect;
    }

    // 레터박스 여백을 지정 색으로 채우는 배경 카메라 생성
    void SetupBackgroundCamera()
    {
        if (!Application.isPlaying) return;
        if (bgCam != null) return;

        GameObject go = new GameObject("LetterboxCamera");
        bgCam = go.AddComponent<Camera>();
        bgCam.depth = cam.depth - 1;           // 메인 카메라 뒤에
        bgCam.clearFlags = CameraClearFlags.SolidColor;
        bgCam.backgroundColor = letterboxColor;
        bgCam.cullingMask = 0;                  // 아무것도 안 그림 (색만)
        bgCam.rect = new Rect(0, 0, 1, 1);      // 전체 화면
        bgCam.allowHDR = false;
        bgCam.allowMSAA = false;
    }
}
