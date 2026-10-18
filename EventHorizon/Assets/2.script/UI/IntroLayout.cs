using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 인트로 화면 배치를 한곳에서 조절한다 (화면 1920×1080 기준 px).
///
/// - 타이틀 / 메뉴 버튼 / 캐릭터 일러스트 영역의 위치와 크기를 이 컴포넌트 값으로 맞춘다.
/// - 플레이 전 에디터에서도 값을 바꾸는 즉시 화면에 반영된다.
/// - 이 컴포넌트가 위치·크기를 덮어쓰므로, 세 묶음(Title / Menu / IllustrationArea)은
///   씬 화면에서 끌어 옮기지 말고 여기 숫자로 조절할 것. (묶음 안의 글자·그림은 자유롭게 수정 가능)
/// - 일러스트 한 장 한 장의 크기·위치 보정은 Illustration 오브젝트의 IntroIllustrationCycler 목록에서.
/// </summary>
[ExecuteAlways]
public class IntroLayout : MonoBehaviour
{
    [Header("타이틀 (왼쪽 위 기준)")]
    public RectTransform titleRoot;
    [Tooltip("화면 왼쪽 위에서 타이틀 왼쪽 위까지 (X = 오른쪽, Y = 아래쪽)")]
    public Vector2 titlePosition = new Vector2(150f, 190f);
    [Range(0.2f, 3f)] public float titleScale = 1f;

    [Header("메뉴 버튼 (왼쪽 위 기준)")]
    public RectTransform menuRoot;
    [Tooltip("화면 왼쪽 위에서 첫 버튼 왼쪽 위까지 (X = 오른쪽, Y = 아래쪽)")]
    public Vector2 menuPosition = new Vector2(150f, 560f);
    [Range(0.2f, 3f)] public float menuScale = 1f;

    [Header("캐릭터 일러스트 영역 (발밑 기준)")]
    public RectTransform illustrationRoot;
    [Tooltip("화면 왼쪽 아래에서 일러스트 발밑 가운데까지 (X = 오른쪽, Y = 위쪽. 음수면 화면 아래로 살짝 잘림)")]
    public Vector2 illustrationPosition = new Vector2(1300f, -60f);
    [Tooltip("일러스트 전체 크기 배율 (그림별 배율과 곱해짐)")]
    [Range(0.2f, 3f)] public float illustrationScale = 1f;

    bool dirty = true;

    void OnEnable() { dirty = true; Apply(); }
    void OnValidate() { dirty = true; }

    void Update()
    {
        if (dirty) Apply();
    }

    public void Apply()
    {
        dirty = false;
        Place(titleRoot, new Vector2(0f, 1f), new Vector2(titlePosition.x, -titlePosition.y), titleScale);
        Place(menuRoot, new Vector2(0f, 1f), new Vector2(menuPosition.x, -menuPosition.y), menuScale);
        Place(illustrationRoot, new Vector2(0f, 0f), illustrationPosition, illustrationScale);
    }

    // anchor: 화면의 어느 모서리를 기준으로 할지 (0,1 = 왼쪽 위, 0,0 = 왼쪽 아래)
    static void Place(RectTransform target, Vector2 anchor, Vector2 pos, float scale)
    {
        if (target == null) return;
        if (target.anchorMin != anchor) target.anchorMin = anchor;
        if (target.anchorMax != anchor) target.anchorMax = anchor;
        if (target.anchoredPosition != pos) target.anchoredPosition = pos;
        Vector3 s = new Vector3(scale, scale, 1f);
        if (target.localScale != s) target.localScale = s;
    }
}
