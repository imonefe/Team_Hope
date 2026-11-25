using UnityEngine;
using TMPro;

/// <summary>
/// 데미지 숫자 / 재화 획득 등, 월드 공간에 잠깐 떴다가 위로 떠오르며 사라지는 텍스트.
/// 루트에 World Space Canvas를 두고 그 자식으로 TextMeshProUGUI 텍스트를 두는 구조
/// (프로젝트의 다른 UI들과 동일하게 TextMeshProUGUI를 재사용 — Canvas 하나짜리 미니 UI라고 보면 됨).
/// FloatingTextSpawner를 통해 스폰하는 걸 권장 (Show/ShowDamage는 스폰 직후 1회 호출).
/// </summary>
public class FloatingText : MonoBehaviour
{
    [Header("연출")]
    public float lifetime = 0.8f;
    public float riseDistance = 1.2f;
    public float randomXOffset = 0.3f;

    [Header("기본 색 (Show 계열에서 덮어씀)")]
    public Color defaultColor = Color.white;
    [Header("치명타 데미지 색/크기 배율")]
    public Color critColor = new Color(1f, 0.25f, 0.15f);
    public float critScaleMultiplier = 1.4f;

    [Tooltip("실제 텍스트가 표시될 TMP_Text (자식의 TextMeshProUGUI). 비워두면 자동으로 자식에서 찾음")]
    public TMP_Text label;

    Vector3 startPos;
    float timer;

    // [2026-11-25] 숫자를 화면 위(Screen Space Overlay) 캔버스로 옮겨 그린다.
    //   월드 캔버스는 카메라가 그려서 제로포인트 푸른 화면 · 피격 글리치 · 어두운 방 효과를 같이 받았음.
    //   오버레이 캔버스는 화면 효과가 다 끝난 뒤에 그려지므로 영향을 받지 않음.
    static Canvas overlay;
    RectTransform labelRect;
    Vector3 labelOffset;     // 루트 → 글자 위치 차이 (월드)
    float unitScale;         // 글자 한 칸이 월드에서 차지하는 크기 (루트 스케일 · 치명타 배율 포함)

    static Canvas Overlay()
    {
        if (overlay != null) return overlay;
        var go = new GameObject("FloatingTextOverlay");
        DontDestroyOnLoad(go);
        overlay = go.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 1;   // 게임 HUD 바로 위, 미니맵(5)·인벤토리(10) 아래
        return overlay;
    }

    void MoveToOverlay()
    {
        if (label == null || labelRect != null) return;
        labelRect = label.rectTransform;
        labelOffset = labelRect.position - transform.position;
        unitScale = labelRect.lossyScale.x;
        Vector2 size = labelRect.rect.size;   // [2026-11-25] 늘어나는 앵커였으면 옮긴 뒤 폭이 0이 되어 "20"이 "2 / 0"으로 줄바꿈됐음 → 원래 크기 유지
        labelRect.SetParent(Overlay().transform, false);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0f);
        labelRect.sizeDelta = size;
        label.enableWordWrapping = false;   // 숫자 · 짧은 글자는 줄바꿈하지 않음
        Follow();
    }

    void Follow()
    {
        if (labelRect == null) return;
        Camera cam = WorldCamera(gameObject.layer);
        if (cam == null) return;
        Vector3 world = transform.position + labelOffset;
        Vector3 a = cam.WorldToScreenPoint(world);
        if (a.z <= 0f) { labelRect.gameObject.SetActive(false); return; }
        Vector3 b = cam.WorldToScreenPoint(world + cam.transform.up);
        float px = Vector2.Distance(a, b);   // 월드 1 단위 = 화면 몇 픽셀
        labelRect.gameObject.SetActive(true);
        labelRect.position = new Vector3(a.x, a.y, 0f);
        labelRect.localScale = Vector3.one * (unitScale * px);
    }

    // [2026-11-25] 원래 이 숫자를 그리던 카메라 (이 레이어를 보는 카메라, 직교 우선).
    //   Camera.main 은 3D 원근 카메라라 위치 · 크기가 어긋났음 → 2D 카메라(Camera_2D)로 환산
    static Camera cachedCam;
    static int cachedLayer = -1;
    static Camera WorldCamera(int layer)
    {
        if (cachedCam != null && cachedCam.isActiveAndEnabled && cachedLayer == layer) return cachedCam;
        cachedCam = null;
        cachedLayer = layer;
        foreach (var c in Camera.allCameras)
        {
            if ((c.cullingMask & (1 << layer)) == 0) continue;
            if (cachedCam == null || (c.orthographic && !cachedCam.orthographic)) cachedCam = c;
        }
        if (cachedCam == null) cachedCam = Camera.main;
        return cachedCam;
    }

    void LateUpdate() => Follow();

    void OnDestroy()
    {
        if (labelRect != null) Destroy(labelRect.gameObject);
    }

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        Vector3 offset = new Vector3(Random.Range(-randomXOffset, randomXOffset), 0f, 0f);
        transform.position += offset;
        startPos = transform.position;
        Destroy(gameObject, lifetime);
        if (labelRect == null) MoveToOverlay();   // [2026-11-25] Show 없이 생성된 경우
    }

    /// <summary>텍스트 내용과 색을 지정해서 표시. 스폰 직후 한 번만 호출.</summary>
    public void Show(string text, Color color)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.text = text;
        label.color = color;
        if (labelRect == null) MoveToOverlay();   // [2026-11-25] 첫 프레임부터 오버레이에 그림
    }

    /// <summary>데미지 숫자용 — 치명타면 색/크기를 다르게 표시.</summary>
    public void ShowDamage(int amount, bool isCrit)
    {
        Show(amount.ToString(), isCrit ? critColor : defaultColor);
        if (isCrit) { transform.localScale *= critScaleMultiplier; unitScale *= critScaleMultiplier; }
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = lifetime > 0f ? Mathf.Clamp01(timer / lifetime) : 1f;

        // 처음엔 빠르게 떠오르다 서서히 멈추는 느낌 (ease-out)
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = startPos + Vector3.up * (eased * riseDistance);

        if (label != null)
        {
            Color c = label.color;
            c.a = 1f - t;
            label.color = c;
        }
    }
}
