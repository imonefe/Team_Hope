using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마인드맵 버튼 하나
/// 씬의 Canvas 안에 직접 배치
/// 인스펙터에서 connectedButtons 에 연결할 버튼 드래그
///
/// 상태에 따라 아이콘 모습이 바뀐다 (MapManager.RefreshAllButtons 가 상태를 정해 줌)
///  - 지금 있는 곳(현재 노드): 원래 밝기 + 채도 낮은 옅은 노란 외곽 빛이 은은하게 항상 켜져 있음
///  - 지금 갈 수 있음: 원래 밝기, 마우스를 올리면 노란 외곽 빛이 강하게
///  - 지나온 길(완료): '이제 갈 수 없음'과 같은 모습 (아주 어둡고 회색)
///  - 나중에 갈 수 있음(아직은 못 감): 명도 중간 + 아주 옅은 기운 (채도 낮춤)
///  - 이제 갈 수 없음(지나온 길에서 갈라져 나가 다시는 닿을 수 없음): 아주 어둡고 회색
/// 아이콘 셰이더: Resources/UINodeIcon.mat, 발광 셰이더: Resources/UINodeGlow.mat
/// </summary>
public class MapButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("맵 데이터")]
    public MapRoomData roomData;

    [Header("연결된 다음 버튼들 (인스펙터에서 연결)")]
    public List<MapButton> connectedButtons = new List<MapButton>();

    [Header("UI")]
    public Image buttonImage;
    public Image iconImage;
    public Sprite icon;
    public GameObject lockIcon;   // 선택 불가 표시

    [Header("라인 설정")]
    public GameObject linePrefab; // 연결선 프리팹
    public Transform lineParent; // 라인이 배치될 부모 (Canvas 루트 권장)
    [Tooltip("노드 사이 연결선 이미지 높이 (픽셀, 1920×1080 기준). Line.png 는 가운데 약 1/4 만 진한 선이라 20 이면 진한 부분이 약 5픽셀")]
    public float lineWidth = 20f;

    [Header("상태별 아이콘 표시")]
    [Tooltip("이미 지나온 노드(완료)의 명도 (1 = 그대로) — 기본값은 '이제 갈 수 없음'과 같음")]
    [Range(0f, 2f)] public float completedBrightness = 0.28f;
    [Tooltip("이미 지나온 노드(완료)의 채도 (0 = 완전 회색) — 기본값은 '이제 갈 수 없음'과 같음")]
    [Range(0f, 3f)] public float completedSaturation = 0.2f;
    [Tooltip("이미 지나온 노드(완료)에 곱할 색 (흰색 = 그대로)")]
    public Color completedTint = Color.white;
    [Tooltip("아직은 못 가지만 나중에 갈 수 있는 노드의 명도")]
    [Range(0f, 2f)] public float laterBrightness = 0.6f;
    [Tooltip("아직은 못 가지만 나중에 갈 수 있는 노드의 채도")]
    [Range(0f, 3f)] public float laterSaturation = 0.8f;
    [Tooltip("나중에 갈 수 있는 노드에 곱할 색 (흰색 = 그대로, 지나온 길보다 옅은 기운)")]
    public Color laterTint = new Color(1f, 0.95f, 0.86f, 1f);
    [Tooltip("이제 갈 수 없는 노드의 명도")]
    [Range(0f, 2f)] public float lockedBrightness = 0.28f;
    [Tooltip("이제 갈 수 없는 노드의 채도 (0 = 완전 회색)")]
    [Range(0f, 3f)] public float lockedSaturation = 0.2f;
    [Tooltip("이제 갈 수 없는 노드에 곱할 색 (흰색 = 그대로)")]
    public Color lockedTint = Color.white;

    [Header("마우스 올렸을 때 외곽 발광 (갈 수 있는 노드만)")]
    [Tooltip("발광 이미지가 아이콘보다 몇 배 큰지 (UINodeGlow 머티리얼의 Pad 값과 같게)")]
    public float glowPadding = 1.8f;
    [Tooltip("발광이 켜지고 꺼지는 시간(초, 실제 시간)")]
    public float glowFadeTime = 0.12f;
    [Tooltip("지금 있는 노드(현재 위치)에 항상 켜 두는 외곽 빛 세기 (0 = 끔, 1 = 마우스 올렸을 때만큼)")]
    [Range(0f, 1f)] public float currentGlow = 0.35f;
    [Tooltip("마우스를 올렸을 때 발광 색 (갈 수 있는 노드)")]
    public Color hoverGlowColor = new Color(1f, 0.8f, 0.28f, 1f);
    [Tooltip("지금 있는 노드(선택한 방)의 발광 색 — 마우스 발광보다 채도를 낮춘 옅은 노랑")]
    public Color currentGlowColor = new Color(1f, 0.93f, 0.76f, 1f);

    // 상태
    [HideInInspector] public bool isCompleted = false;
    [HideInInspector] public bool isSelectable = false;
    [HideInInspector] public bool isCurrent = false;
    [HideInInspector] public bool isReachableLater = false;   // 지금은 못 가지만 앞으로 갈 수 있는 길 위에 있음

    Button btn;
    List<GameObject> spawnedLines = new List<GameObject>();

    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    Material iconMaterial;      // 이 노드 전용 아이콘 머티리얼 (명도·채도)
    Image glowImage;            // 마우스 올렸을 때 발광
    Material glowMaterial;      // 이 노드 전용 발광 머티리얼 (상태마다 색이 다름)
    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    bool hovered;
    float glowAmount;

    // 타입별 색상


    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);

        // 맵 노드 버튼 호버/클릭 연출
        UIButtonJuice.Attach(btn);

        // icon 직접 지정 없으면 buttonImage 의 sprite 사용
        if (iconImage != null)
            iconImage.sprite = icon != null ? icon : (buttonImage != null ? buttonImage.sprite : null);

        // 버튼 기본 색 바꿈(비활성 = 반투명 회색)은 끄고, 상태 표시는 아래 셰이더로만 한다
        btn.transition = Selectable.Transition.None;

        SetupIconMaterial();
        SetupGlow();
    }

    void SetupIconMaterial()
    {
        if (iconImage == null) return;
        Material src = Resources.Load<Material>("UINodeIcon");
        if (src == null) { Debug.LogWarning("[MapButton] Resources 폴더에 UINodeIcon 머티리얼이 없습니다."); return; }
        iconMaterial = new Material(src) { name = "UINodeIcon (" + name + ")" };
        iconImage.material = iconMaterial;
    }

    // 아이콘과 같은 모양의 발광 이미지를 아이콘 위에 하나 더 만든다 (빛은 아이콘 바깥쪽에만 그려짐)
    void SetupGlow()
    {
        if (iconImage == null) return;
        Material glowMat = Resources.Load<Material>("UINodeGlow");
        if (glowMat == null) { Debug.LogWarning("[MapButton] Resources 폴더에 UINodeGlow 머티리얼이 없습니다."); return; }

        GameObject go = new GameObject("HoverGlow", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(iconImage.rectTransform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = iconImage.rectTransform.rect.size * glowPadding;

        glowImage = go.AddComponent<Image>();
        glowImage.sprite = iconImage.sprite;
        glowMaterial = new Material(glowMat) { name = "UINodeGlow (" + name + ")" };
        glowImage.material = glowMaterial;
        glowImage.raycastTarget = false;
        glowImage.color = new Color(1f, 1f, 1f, 0f);
        glowImage.enabled = false;
    }

    void Update()
    {
        // 맵이 열려 있는 동안 게임 시간이 멈추므로 실제 시간 기준
        float target = (hovered && isSelectable) ? 1f : (isCurrent ? currentGlow : 0f);
        float speed = glowFadeTime > 0f ? 1f / glowFadeTime : 1000f;
        glowAmount = Mathf.MoveTowards(glowAmount, target, speed * Time.unscaledDeltaTime);
        if (glowImage != null)
        {
            glowImage.enabled = glowAmount > 0.001f;
            glowImage.color = new Color(1f, 1f, 1f, glowAmount);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }

    // 맵이 닫힐 때 마우스가 올라가 있었어도 다음에 열면 발광이 꺼진 상태로 시작 (현재 노드는 바로 켜진 상태로)
    void OnDisable()
    {
        hovered = false;
        glowAmount = isCurrent ? currentGlow : 0f;
        if (glowImage != null) { glowImage.enabled = glowAmount > 0.001f; glowImage.color = new Color(1f, 1f, 1f, glowAmount); }
    }

    void OnDestroy()
    {
        if (iconMaterial != null) Destroy(iconMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    void Start()
    {
        DrawLines();
        RefreshVisual();
    }

    // ── 클릭 ──────────────────────────────────────────────
    void OnClick()
    {
        if (!isSelectable) return;
        MapManager.Instance.SelectButton(this);
    }

    // ── 라인 그리기 ───────────────────────────────────────
    void DrawLines()
    {
        if (linePrefab == null || lineParent == null) return;

        foreach (var target in connectedButtons)
        {
            if (target == null) continue;

            GameObject line = Instantiate(linePrefab, lineParent);
            line.transform.SetAsFirstSibling(); // 라인은 버튼 뒤에

            RectTransform rt = line.GetComponent<RectTransform>();
            RectTransform myRT = GetComponent<RectTransform>();
            RectTransform tgRT = target.GetComponent<RectTransform>();

            Vector2 from = myRT.anchoredPosition;
            Vector2 to = tgRT.anchoredPosition;
            Vector2 dir = to - from;

            rt.anchoredPosition = from + dir * 0.5f;
            rt.sizeDelta = new Vector2(dir.magnitude, lineWidth);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            spawnedLines.Add(line);
        }
    }

    // ── 외형 업데이트 ─────────────────────────────────────
    public void RefreshVisual()
    {
        if (btn != null)
            btn.interactable = isSelectable;



        if (lockIcon != null)
            lockIcon.SetActive(!isSelectable && !isCompleted && !isCurrent);

        // 투명도로 구분하던 예전 방식 대신 명도·채도로 구분 (투명도는 항상 그대로)
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = 1f;

        float brightness = 1f, saturation = 1f;
        Color tint = Color.white;
        if (isSelectable || isCurrent)
        {
            // 지금 갈 수 있음 / 지금 있는 곳: 그대로 (현재 위치는 노란 외곽 빛으로 구분)
        }
        else if (isCompleted)
        {
            brightness = completedBrightness;  // 이미 지나온 길
            saturation = completedSaturation;
            tint = completedTint;
        }
        else if (isReachableLater)
        {
            brightness = laterBrightness;      // 아직은 못 가지만 나중에 갈 수 있음
            saturation = laterSaturation;
            tint = laterTint;
        }
        else
        {
            brightness = lockedBrightness;     // 이제 갈 수 없음
            saturation = lockedSaturation;
            tint = lockedTint;
        }

        if (iconImage != null)
            iconImage.color = tint;

        if (iconMaterial != null)
        {
            iconMaterial.SetFloat(BrightnessId, brightness);
            iconMaterial.SetFloat(SaturationId, saturation);
        }

        // 지금 있는 노드는 채도를 낮춘 옅은 노랑, 갈 수 있는 노드(마우스)는 진한 노랑
        if (glowMaterial != null)
            glowMaterial.SetColor(GlowColorId, isCurrent ? currentGlowColor : hoverGlowColor);
    }


}