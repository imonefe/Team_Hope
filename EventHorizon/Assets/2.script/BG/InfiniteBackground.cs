using UnityEngine;

/// <summary>
/// 가로: 카메라 X에 고정한 채 텍스처만 흘러 무한 스크롤 원근을 낸다(여백 없음).
/// 세로: 카메라를 따라가되, 카메라 세로 이동에 비례해 조금씩 더/덜 움직여 원근감을 준다.
///
/// 준비:
/// 1. 배경 텍스처 Wrap Mode = Repeat (가로 스크롤용).
/// 2. SpriteRenderer Draw Mode = Tiled 권장 (또는 Quad + Material).
/// 3. 이 스크립트를 붙인다.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class InfiniteBackground : MonoBehaviour
{
    [Header("가로 (텍스처 스크롤)")]
    [Tooltip("가로 이동 대비 텍스처가 흐르는 비율 (1=카메라와 동일, 0.5=원경)")]
    public float parallaxX = 0.5f;
    [Tooltip("텍스처 1장이 월드 몇 유닛에 해당하는지 (흐름 속도 보정)")]
    public float worldUnitsPerTile = 20f;
    [Tooltip("화면 폭의 몇 배로 덮을지 (SpriteRenderer Tiled일 때)")]
    public float widthMultiplier = 2f;

    [Header("세로 원근 (흔들림)")]
    [Tooltip("카메라 세로 이동에 비례한 배경의 추가 흔들림. 양수=더 움직임, 음수=덜 움직임(원경). 작게: 0.1~0.3")]
    public float swayY = 0.15f;
    [Tooltip("세로 위치 오프셋")]
    public float offsetY = 0f;

    [Header("세로 길이")]
    [Tooltip("세로로 길게 늘림 (반복). 켜면 아래 배수만큼 세로를 채움")]
    public bool tileVertically = false;
    [Tooltip("화면 높이의 몇 배로 세로를 채울지 (tileVertically 켜졌을 때)")]
    public float heightMultiplier = 2f;

    [Header("위치")]
    [Tooltip("카메라와의 깊이(Z). 클수록 뒤")]
    public float depth = 10f;

    [Header("정렬 (가려짐 방지)")]
    public string sortingLayer = "Default";
    public int sortingOrder = -100;

    Renderer rend;
    SpriteRenderer sr;
    Material mat;
    Camera cam;

    void Start()
    {
        rend = GetComponent<Renderer>();
        sr = GetComponent<SpriteRenderer>();
        mat = rend.material;

        if (sr != null && sr.drawMode == SpriteDrawMode.Simple)
            sr.drawMode = SpriteDrawMode.Tiled;

        rend.sortingLayerName = sortingLayer;
        rend.sortingOrder = sortingOrder;

        cam = Camera.main;

        ResizeToScreen();
    }

    // [2026-10-10 추가] rend.material로 만든 복제본 정리 (방에 들어갈 때마다 하나씩 쌓였음)
    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        ResizeToScreen();

        Vector3 camPos = cam.transform.position;

        // ── 가로: 카메라 X에 고정 (여백 방지) ──
        float x = camPos.x;

        // ── 세로: 카메라의 "현재 위치"를 기준으로 비례해서 더/덜 움직여 원근 ──
        // (예전엔 Start 시점 카메라 위치를 기준점으로 잡았는데, 그 시점에 Camera.main이
        //  아직 준비 안 됐으면 기준점이 0으로 굳어버려서 오프셋이 튀는 문제가 있었음.
        //  기준점 없이 카메라 현재 위치로 바로 계산하면 그 문제가 사라진다.)
        float y = camPos.y * (1f + swayY) + offsetY;

        // 이미지 끝이 카메라(화면)를 벗어나지 않도록 제한
        float camHalfH = cam.orthographicSize;
        float bgHalfH = rend.bounds.size.y * 0.5f;
        if (bgHalfH > camHalfH)
        {
            float slack = bgHalfH - camHalfH;
            y = Mathf.Clamp(y, camPos.y - slack, camPos.y + slack);
        }
        else
        {
            y = camPos.y; // 배경이 화면보다 작으면 카메라에 맞춰 덮음
        }

        transform.position = new Vector3(x, y, camPos.z + depth);

        // ── 가로 텍스처만 흘러서 스크롤 원근 (카메라 현재 위치 기준) ──
        float dx = camPos.x * parallaxX / worldUnitsPerTile;
        mat.mainTextureOffset = new Vector2(dx, 0f);
    }

    void ResizeToScreen()
    {
        if (sr == null || sr.drawMode == SpriteDrawMode.Simple) return;

        float screenW = cam.orthographicSize * 2f * cam.aspect;
        float screenH = cam.orthographicSize * 2f;
        Vector2 size = sr.size;
        size.x = screenW * widthMultiplier;

        if (tileVertically)
            size.y = screenH * heightMultiplier; // 세로로 길게 (반복)
        else if (sr.sprite != null)
            size.y = sr.sprite.bounds.size.y;     // 원본 높이

        sr.size = size;
    }
}