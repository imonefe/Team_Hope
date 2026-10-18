using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-18 추가] 작은 무늬 그림 한 칸을 화면 전체에 바둑판처럼 반복하고, 천천히 흐르게 한다 (인트로 배경용).
///
/// - RawImage 에 붙인다. RawImage 의 Texture 칸에 무늬 그림(한 칸짜리)을 넣는다.
/// - 무늬 색·진하기는 RawImage 의 Color 에서 조절 (알파가 낮을수록 은은함).
/// - 칸 크기(tileSize)와 흐르는 속도(scrollSpeed)는 여기서. 에디터에서도 칸 크기는 바로 보인다.
/// - 무늬 그림은 반복(Repeat)되어야 이음매 없이 이어진다. 가져오기 설정이 달라도 실행 중에는 반복으로 바꿔 준다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class UIScrollingPattern : MonoBehaviour
{
    [Tooltip("무늬 한 칸이 화면에서 차지하는 크기 (px, 화면 1920×1080 기준)")]
    public Vector2 tileSize = new Vector2(84f, 146f);
    [Tooltip("1초에 무늬가 움직이는 거리 (px). X + = 오른쪽, Y + = 위쪽")]
    public Vector2 scrollSpeed = new Vector2(2.1f, -3.65f);

    RawImage raw;
    RectTransform rt;
    Vector2 offset;     // 0~1 (칸 단위)

    void Awake()
    {
        raw = GetComponent<RawImage>();
        rt = transform as RectTransform;
        raw.raycastTarget = false;
    }

    void OnEnable()
    {
        if (raw == null) Awake();
        if (Application.isPlaying && raw.texture != null) raw.texture.wrapMode = TextureWrapMode.Repeat;
    }

    void Update()
    {
        if (raw == null) Awake();

        if (Application.isPlaying && tileSize.x > 0f && tileSize.y > 0f)
        {
            // 무늬가 오른쪽으로 가려면 그림을 읽는 위치(uv)는 왼쪽으로 가야 한다
            offset.x = Mathf.Repeat(offset.x - scrollSpeed.x / tileSize.x * Time.unscaledDeltaTime, 1f);
            offset.y = Mathf.Repeat(offset.y - scrollSpeed.y / tileSize.y * Time.unscaledDeltaTime, 1f);
        }

        Vector2 size = rt.rect.size;
        Vector2 count = new Vector2(
            tileSize.x > 0f ? size.x / tileSize.x : 1f,
            tileSize.y > 0f ? size.y / tileSize.y : 1f);

        Rect uv = new Rect(offset, count);
        if (raw.uvRect != uv) raw.uvRect = uv;
    }
}
