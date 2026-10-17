using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-10-17 추가 / 같은 날 변경] 상호작용 안내 — 상자·NPC 등에 가까이 가면 그 오브젝트 위에 "[F] 열기" 상자를 띄운다.
///
/// - 디자인은 처음 만든 안내 그대로 (어두운 반투명 상자 + "[F]" + 문구). 다만 화면 UI가 아니라
///   오브젝트의 자식으로 붙는 월드 공간 캔버스라서, 카메라나 캐릭터가 움직여도 흔들리지 않는다.
/// - ProximityInteractable 이 필요할 때 Create() 로 하나 만들어 두고 켜고 끈다
///   (PromptLabel 을 재정의한 것만: Box "열기", npcTrigger "대화").
/// - 모양(배경색·글꼴·글자 크기·색)은 Etc/Resources/InteractPrompt 프리팹의 Panel / Key_Text / Label_Text 에서,
///   전체 크기는 uiScale, 오브젝트와의 간격은 gap 에서 바꾼다. 오브젝트별 위치 조정은 각 오브젝트의 Prompt Offset.
/// - 위치: 오브젝트 그림(SpriteRenderer)의 맨 위 + gap (그림이 없으면 콜라이더 맨 위).
/// - 부모 오브젝트의 크기·좌우 반전과 상관없이 같은 크기로 보이도록 크기를 되돌린다.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    [Header("구성")]
    public RectTransform panel;
    public TextMeshProUGUI keyText;
    public TextMeshProUGUI labelText;
    [Tooltip("키 글자 모양 ({0} 자리에 키 이름)")]
    public string keyFormat = "[{0}]";

    [Header("크기 · 위치")]
    [Tooltip("UI 1픽셀이 월드에서 차지하는 크기. 작을수록 안내가 작아진다")]
    public float uiScale = 0.01f;
    [Tooltip("오브젝트 그림 맨 위에서 안내 상자 아래쪽까지 간격 (월드 단위)")]
    public float gap = 0.3f;
    public int sortingOrder = 5000;

    static InteractPrompt template;
    static bool loadFailed;

    string lastKey, lastLabel;

    /// <summary>owner 오브젝트의 자식으로 안내를 하나 만든다 (처음에는 꺼진 상태).</summary>
    public static InteractPrompt Create(Transform owner)
    {
        if (template == null && !loadFailed)
        {
            template = Resources.Load<InteractPrompt>("InteractPrompt");
            if (template == null)
            {
                loadFailed = true;
                Debug.LogWarning("[InteractPrompt] Resources/InteractPrompt 프리팹을 찾지 못했습니다.");
            }
        }
        if (template == null || owner == null) return null;

        InteractPrompt p = Instantiate(template, owner);
        p.name = "InteractPrompt";
        // 카메라에 보이도록 주인 오브젝트와 같은 레이어로
        foreach (Transform t in p.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = owner.gameObject.layer;
        var canvas = p.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = p.sortingOrder;
        foreach (var g in p.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        p.gameObject.SetActive(false);
        return p;
    }

    /// <summary>글자를 바꾸고 오브젝트 위에 띄운다. extraOffset 은 오브젝트별 추가 오프셋(월드 단위).</summary>
    public void Show(string key, string label, Vector3 extraOffset)
    {
        bool changed = false;
        if (key != lastKey)
        {
            lastKey = key; changed = true;
            if (keyText != null) keyText.text = string.Format(keyFormat, key);
        }
        if (label != lastLabel)
        {
            lastLabel = label; changed = true;
            if (labelText != null) labelText.text = label;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            changed = true;
            PlaceAbove(extraOffset);
        }
        if (changed && panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    // 주인 그림 맨 위에 놓고, 주인 크기·반전을 되돌려 일정한 크기로
    void PlaceAbove(Vector3 extraOffset)
    {
        Transform owner = transform.parent;
        if (owner == null) return;

        Vector3 top;
        if (TryGetTop(owner, out top)) top.y += gap;
        else top = owner.position + new Vector3(0f, 1f, 0f);

        transform.position = top + extraOffset;
        transform.rotation = Quaternion.identity;

        Vector3 ls = owner.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(ls.x, 0f) ? uiScale : uiScale / ls.x,
            Mathf.Approximately(ls.y, 0f) ? uiScale : uiScale / ls.y,
            Mathf.Approximately(ls.z, 0f) ? uiScale : uiScale / ls.z);
    }

    bool TryGetTop(Transform root, out Vector3 top)
    {
        bool found = false;
        Bounds b = default;
        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sprite == null) continue;
            if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds);
        }
        if (!found)
        {
            foreach (var c in root.GetComponentsInChildren<Collider2D>())
            {
                if (!c.enabled || c.isTrigger) continue;
                if (!found) { b = c.bounds; found = true; } else b.Encapsulate(c.bounds);
            }
        }
        top = found ? new Vector3(b.center.x, b.max.y, root.position.z) : Vector3.zero;
        return found;
    }
}
