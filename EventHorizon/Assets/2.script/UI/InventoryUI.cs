using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 창 컨트롤러.
/// - 패시브+일반 아이템 스크롤 (획득순)
/// - 상세 패널 / 호버 팝업
///
/// (퍼즈 투척 아이템 탭/등록 슬롯 기능은 제거됨 — 퍼즈 아이템 시스템 자체가 게임에서 빠지면서 함께 제거.
///  씬의 InventoryPanel에 남아있는 "퍼즈 탭" 관련 UI 오브젝트(전환 버튼, 퍼즈 탭 패널, 등록 5칸)는
///  에디터에서 직접 비활성화/삭제해줘야 함.)
///
/// 슬롯 프리팹(ItemSlot)에는 ItemSlotUI 컴포넌트가 붙어 있어야 한다.
/// Inventory 싱글턴의 변경 이벤트를 구독해 자동 갱신한다.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("일반 탭 - 스크롤")]
    public Transform generalContent;    // 일반 탭 Content
    public GameObject itemSlotPrefab;   // 슬롯 프리팹 (ItemSlotUI 포함)

    [Header("상세 패널")]
    public Image detailIcon;
    public Text detailName;
    public Text detailDesc;

    [Header("호버 팝업")]
    public GameObject hoverPopup;   // 평소엔 꺼둠
    public Text hoverText;

    [Header("아이템 없음 안내")]
    [Tooltip("아이템이 하나도 없을 때만 켜지는 안내 글자 오브젝트 (예: \"아이템 없음\").\n" +
             "비워두면 씬 전체에서 이름이 emptyLabelName 인 오브젝트를 자동으로 찾아서 쓴다")]
    public GameObject emptyLabel;
    [Tooltip("emptyLabel 칸이 비어 있을 때 자동으로 찾을 오브젝트 이름 (대소문자 무시)")]
    public string emptyLabelName = "noneitem";

    [Header("빈 칸 채우기 (2026-10-17 추가)")]
    [Tooltip("아이템이 하나라도 있으면 나머지 자리를 빈 칸으로 채워 그리드 모양을 보여준다")]
    public bool fillEmptySlots = true;
    [Tooltip("최소 칸 수. 0 이면 스크롤 영역에 보이는 줄 수만큼 자동으로 채움")]
    public int minSlotCount = 0;

    void Start()
    {
        FindEmptyLabelIfMissing();

        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged += RefreshGeneral;

        if (hoverPopup != null) hoverPopup.SetActive(false);

        RefreshGeneral();
    }

    void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnInventoryChanged -= RefreshGeneral;
    }

    // ────────────────────────────────────────────
    // 스크롤 목록 채우기
    // ────────────────────────────────────────────
    void RefreshGeneral()
    {
        if (generalContent == null || itemSlotPrefab == null || Inventory.Instance == null) return;
        ClearChildren(generalContent);

        // 패시브 (획득순) — 일반 아이템(generals)은 죽은 기능이라 제거됨, 현재는 패시브만 존재
        foreach (ItemData item in Inventory.Instance.passives)
            CreateItemSlot(item);

        // [2026-10-17 추가] 아이템이 하나라도 있으면 남은 자리를 빈 칸으로 채운다
        int count = Inventory.Instance.passives.Count;
        if (fillEmptySlots && count > 0)
        {
            int total = GetFilledSlotCount(count);
            for (int i = count; i < total; i++)
                CreateItemSlot(null);   // 아이템 없음 → 아이콘 꺼짐, 호버·클릭 반응 없음 (ItemSlotUI)
        }

        // 아이템이 하나도 없으면 "아이템 없음" 안내를 켜고, 하나라도 있으면 끈다
        // (안내 글자는 Content 밖에 둬야 함 — Content 자식은 위 ClearChildren에서 매번 지워짐)
        if (emptyLabel != null)
            emptyLabel.SetActive(Inventory.Instance.passives.Count == 0);
    }

    // emptyLabel 칸을 비워뒀으면, 씬 전체(꺼진 오브젝트 포함)에서 이름으로 찾아 연결한다
    void FindEmptyLabelIfMissing()
    {
        if (emptyLabel != null || string.IsNullOrEmpty(emptyLabelName)) return;

        // 이 스크립트(InventoryManager)와 인벤토리 창(Canvers)은 서로 다른 최상위 오브젝트 밑에 있으므로
        // 씬의 모든 최상위 오브젝트(꺼진 것 포함)를 다 뒤진다
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(t.name, emptyLabelName, System.StringComparison.OrdinalIgnoreCase)) continue;

            // Content 안에 있으면 목록을 새로 그릴 때마다 같이 지워지므로 경고
            if (generalContent != null && t.IsChildOf(generalContent))
                Debug.LogWarning("[InventoryUI] '" + t.name + "' 이(가) 아이템 목록(Content) 안에 있어서 목록 갱신 때 지워집니다. Content 밖으로 옮겨주세요.", t);

            emptyLabel = t.gameObject;
            return;
        }
    }

    // 채울 전체 칸 수 = max(아이템 수를 한 줄 단위로 올림, 스크롤 영역에 보이는 줄 수 × 한 줄 칸 수, 최소 칸 수)
    int GetFilledSlotCount(int itemCount)
    {
        GridLayoutGroup grid = generalContent.GetComponent<GridLayoutGroup>();
        int columns = (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            ? Mathf.Max(1, grid.constraintCount) : 1;

        int total = Mathf.CeilToInt(itemCount / (float)columns) * columns;

        if (minSlotCount > 0)
        {
            total = Mathf.Max(total, minSlotCount);
        }
        else if (grid != null)
        {
            // 스크롤 영역(Content 의 부모 = Viewport)에 보이는 줄 수
            RectTransform viewport = generalContent.parent as RectTransform;
            if (viewport != null)
            {
                float usable = viewport.rect.height - grid.padding.top - grid.padding.bottom + grid.spacing.y;
                int rows = Mathf.FloorToInt(usable / Mathf.Max(1f, grid.cellSize.y + grid.spacing.y));
                total = Mathf.Max(total, rows * columns);
            }
        }

        // 마지막 줄도 꽉 채우기
        return Mathf.CeilToInt(total / (float)columns) * columns;
    }

    void CreateItemSlot(ItemData item)
    {
        GameObject slot = Instantiate(itemSlotPrefab, generalContent);
        ItemSlotUI ui = slot.GetComponent<ItemSlotUI>();
        if (ui != null) ui.SetItem(item, this);
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ────────────────────────────────────────────
    // 상세 패널
    // ────────────────────────────────────────────
    public void ShowItemDetail(ItemData item)
    {
        if (item == null) return;
        UIUtil.SetIcon(detailIcon, item.icon);
        if (detailName != null) detailName.text = item.itemName;
        if (detailDesc != null) detailDesc.text = item.description;
    }

    // ────────────────────────────────────────────
    // 호버 팝업
    // ────────────────────────────────────────────
    public void ShowHoverItem(ItemData item, Vector3 pos)
    {
        if (hoverPopup == null || item == null) return;
        hoverPopup.SetActive(true);
        hoverPopup.transform.position = pos;
        if (hoverText != null)
        {
            hoverText.text = item.itemName
                + "\n등급 " + item.tier
                + "\n" + item.description;
        }
    }

    public void HideHover()
    {
        if (hoverPopup != null) hoverPopup.SetActive(false);
    }
}
