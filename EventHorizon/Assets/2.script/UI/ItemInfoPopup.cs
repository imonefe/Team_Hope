using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아이템 정보 팝업 (공용 싱글턴).
/// 두 가지 상황에서 공용으로 사용:
///  1) 인벤토리 슬롯 호버(ItemSlotUI) — 마우스 커서를 따라다님
///  2) 필드에 굴러다니는 아이템 근처 접근(Item.cs) — 플레이어 머리 위에 떠서 따라다님
/// InventoryPanel이나 battle Canvas 밑이 아니라, 독립된 오브젝트/캔버스에 이 스크립트를 붙여야 한다 —
/// 그래야 인벤토리가 열려있든 닫혀있든 항상 존재해서 양쪽 상황 모두에서 팝업을 띄울 수 있다.
///
/// [2026-10-17 변경] 가독성 개선 (C시안 — 컴팩트형)
///  구조: [아이콘 칸 | 이름 + 등급 칩] / 설명 / 효과 칩 / (필드에서만) "F 줍기" 안내
///  - 등급은 영어(Epic) 대신 한글 이름(영웅)으로, 등급마다 색을 다르게 (아이콘 칸 테두리·등급 칩)
///  - 효과는 칸으로 정리: 짧은 효과("공격력 +5")는 한 줄에 같은 너비 2칸, 긴 효과는 한 줄 전체 1칸.
///    칸 안에서 이름은 왼쪽, 수치(+5 / -2 / +10%)는 오른쪽 정렬
///  - 팝업 크기는 내용에 맞게 자동으로 늘고 줄어듦 (Content Size Fitter)
///  - 마우스 모드: 커서 오른쪽 아래에 뜨고, 화면 오른쪽을 넘으면 커서 왼쪽으로 옮겨 뜸
///  - 머리 위 모드: 팝업 아래 가운데가 머리 위 기준점에 오도록 뜸
///  - 어느 모드든 화면 밖으로 나가면 안쪽으로 밀어 넣음
///
/// Show(item, pos)              : 마우스 커서를 따라다니는 모드 (인벤토리 호버용)
/// ShowNearItem(item, playerTr) : 지정한 Transform(플레이어) 머리 위를 따라다니는 모드 (월드 근접용)
///
/// [2026-10-17 추가] 스킬도 같은 팝업으로 보여줄 수 있음 (등급 칸 = 일반/무기 스킬, 효과 칸 = 쿨타임·들어갈 칸)
/// ShowSkill(skill)                : 마우스 커서를 따라다니는 모드 (나중에 스킬 칸 호버용)
/// ShowNearSkill(skill, playerTr)  : 머리 위 모드 (필드의 SkillPickup 근접용)
/// </summary>
public class ItemInfoPopup : MonoBehaviour
{
    public static ItemInfoPopup Instance;

    /// <summary>[2026-10-17 추가] 팝업이 지금 화면에 떠 있는지 (머리 위 상호작용 안내가 겹치지 않게 숨을 때 사용)</summary>
    public bool IsVisible => popupRoot != null && popupRoot.activeSelf;

    enum FollowMode { Mouse, WorldTarget }

    [Header("루트")]
    [Tooltip("보이기/숨기기를 토글할 루트 오브젝트 (팝업 패널)")]
    public GameObject popupRoot;

    [Header("마우스 추적 (인벤토리 호버용)")]
    [Tooltip("마우스 커서 기준 오프셋 (기준 해상도 1920x1080 기준 픽셀). 커서 오른쪽 아래로 띄움")]
    public Vector2 mouseOffset = new Vector2(24f, -24f);

    [Header("월드 타겟 추적 (필드 아이템 근접용)")]
    [Tooltip("플레이어 위치 기준 위쪽으로 띄울 높이(월드 유닛) — 팝업 아래 가운데가 이 점에 붙음")]
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("화면 가장자리")]
    [Tooltip("팝업이 화면 가장자리에서 최소한 이만큼 떨어지도록 밀어 넣음 (기준 해상도 픽셀)")]
    public float screenMargin = 12f;

    [Header("윗줄 — 아이콘 / 이름 / 등급")]
    public Image iconImage;
    [Tooltip("아이콘 뒤 칸 (등급 색으로 물듦)")]
    public Image iconSlot;
    public TextMeshProUGUI nameText;
    [Tooltip("등급 칩 배경 (등급 색으로 물듦)")]
    public Image tierChip;
    public TextMeshProUGUI tierText;

    [Header("설명")]
    public TextMeshProUGUI descText;

    [Header("효과 칸")]
    [Tooltip("효과 줄들이 쌓이는 곳 (세로 배치)")]
    public RectTransform effectsRoot;
    [Tooltip("칸 견본 (꺼둔 상태로 두면 필요한 만큼 복제해서 씀). 자식에 Label / Value 글상자가 있어야 함")]
    public GameObject effectCellTemplate;
    [Tooltip("한 줄에 놓인 두 칸 사이 간격")]
    public float effectCellSpacing = 8f;
    [Tooltip("보이는 글자 수가 이 값 이하인 효과는 '짧은 효과'로 보고 한 줄에 2칸씩 놓음. 더 길면 한 줄 전체를 씀")]
    public int effectShortMaxLength = 10;

    [Header("줍기 안내 (필드 근접 모드에서만 보임)")]
    public GameObject pickupHint;

    [Header("등급 표시 (일반 / 희귀 / 영웅 / 전설 순서)")]
    public string[] tierNames = { "일반", "희귀", "영웅", "전설" };
    public Color[] tierColors =
    {
        new Color32(0xB8, 0xBC, 0xC6, 0xFF),
        new Color32(0x5A, 0xA9, 0xFF, 0xFF),
        new Color32(0xB0, 0x7C, 0xFF, 0xFF),
        new Color32(0xFF, 0xB5, 0x47, 0xFF),
    };
    [Tooltip("등급 칩 배경의 진하기 (등급 색에 곱해짐)")]
    [Range(0f, 1f)] public float tierChipAlpha = 0.9f;

    [Header("스킬 표시 (일반 / 무기 순서 — Skill.SkillType 값 순서)")]
    public string[] skillTypeNames = { "일반 스킬", "무기 스킬" };
    public Color[] skillTypeColors =
    {
        new Color32(0xFF, 0x9F, 0x43, 0xFF),
        new Color32(0x8C, 0xCB, 0xFF, 0xFF),
    };
    [Tooltip("효과 칸 '들어갈 칸'에 보일 글자 (일반 스킬 → 1·2번 칸, 무기 스킬 → 0번 칸)")]
    public string[] skillSlotLabels = { "S · D", "A" };

    RectTransform popupRect;

    // 효과 칸 (한 번 만들고 재사용)
    class EffectCell { public GameObject go; public Image bg; public TextMeshProUGUI label, value; }
    class EffectRow { public GameObject go; public EffectCell left, right; }
    class EffectEntry { public string label, value; public bool isShort; }
    readonly List<EffectRow> effectRows = new List<EffectRow>();
    Canvas rootCanvas;
    FollowMode followMode = FollowMode.Mouse;
    Transform worldTarget;

    static readonly Vector2 MousePivot = new Vector2(0f, 1f);   // 왼쪽 위 기준 — 커서 오른쪽 아래로 펼쳐짐
    static readonly Vector2 WorldPivot = new Vector2(0.5f, 0f); // 아래 가운데 기준 — 머리 위로 펼쳐짐

    void Awake()
    {
        Instance = this;

        // 씬 저장 과정에서 Canvas의 Sorting Order가 실수로 0으로 되돌아가는 일이 반복돼서,
        // 인스펙터 값에 의존하지 않고 여기서 강제로 다른 캔버스(battle Canvas/InventoryPanel, 둘 다 0)보다
        // 위에 그려지도록 확실히 고정한다.
        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null)
            rootCanvas.sortingOrder = 100;

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
            popupRect = popupRoot.GetComponent<RectTransform>();
            if (popupRect != null)
                popupRect.pivot = MousePivot;

            // 뜰 때마다(=마우스 호버/근접할 때마다) 살짝 팝인되는 연출
            if (popupRoot.GetComponent<UIPanelAnimator>() == null)
                popupRoot.AddComponent<UIPanelAnimator>();

            // 팝업은 순수 표시용이라 마우스 판정을 가로채면 안 됨 — 그대로 두면 팝업이 마우스를 따라
            // 슬롯 위로 올라오는 순간 슬롯의 OnPointerExit가 불려서 꺼졌다 켜졌다를 무한 반복하게 된다.
            // 배경/아이콘/텍스트 등 모든 그래픽의 Raycast Target을 꺼서 클릭/호버 판정을 완전히 통과시킨다.
            foreach (var g in popupRoot.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }
    }

    void Update()
    {
        if (popupRoot == null || !popupRoot.activeSelf) return;
        UpdatePosition();
    }

    /// <summary>일반 아이템 — 마우스 커서를 따라다니는 모드 (인벤토리 호버용)</summary>
    public void Show(ItemData item, Vector3 pos)
    {
        if (popupRoot == null || item == null) return;
        followMode = FollowMode.Mouse;
        worldTarget = null;
        Open(() => FillItem(item));
    }

    /// <summary>일반 아이템 — 지정한 Transform(플레이어) 머리 위를 따라다니는 모드 (필드 아이템 근접용)</summary>
    public void ShowNearItem(ItemData item, Transform player)
    {
        if (popupRoot == null || item == null || player == null) return;
        followMode = FollowMode.WorldTarget;
        worldTarget = player;
        Open(() => FillItem(item));
    }

    /// <summary>스킬 — 마우스 커서를 따라다니는 모드 (나중에 스킬 칸 호버용)</summary>
    public void ShowSkill(Skill skill)
    {
        if (popupRoot == null || skill == null) return;
        followMode = FollowMode.Mouse;
        worldTarget = null;
        Open(() => FillSkill(skill));
    }

    /// <summary>스킬 — 플레이어 머리 위를 따라다니는 모드 (필드의 SkillPickup 근접용)</summary>
    public void ShowNearSkill(Skill skill, Transform player)
    {
        if (popupRoot == null || skill == null || player == null) return;
        followMode = FollowMode.WorldTarget;
        worldTarget = player;
        Open(() => FillSkill(skill));
    }

    void Open(System.Action fill)
    {
        if (popupRect != null)
            popupRect.pivot = followMode == FollowMode.Mouse ? MousePivot : WorldPivot;
        if (pickupHint != null)
            pickupHint.SetActive(followMode == FollowMode.WorldTarget);

        popupRoot.SetActive(true);
        popupRoot.GetComponent<UIPanelAnimator>()?.Show(); // 퇴장 애니메이션 도중 다시 뜨는 경우 대비
        fill();

        // 글자가 바뀐 직후 크기를 바로 다시 계산 — 안 하면 한 프레임 동안 예전 크기로 보였다가 바뀜
        if (popupRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
        UpdatePosition();
    }

    void FillItem(ItemData item)
    {
        // 등급 — 한글 이름 + 등급 색
        int tier = (int)item.tier;
        SetHeader(item.icon, item.itemName, item.description, GetTierName(tier), GetTierColor(tier));

        // 효과 — 칸으로 정리. 효과가 없으면 칸 영역 자체를 숨김
        FillEffects(EffectText.Collect(item));
    }

    // [2026-10-17 추가] 스킬 채우기 — 등급 칸에 일반/무기 스킬, 효과 칸에 쿨타임·들어갈 칸
    void FillSkill(Skill skill)
    {
        int type = (int)skill.skillType;
        string typeName = (skillTypeNames != null && type >= 0 && type < skillTypeNames.Length) ? skillTypeNames[type] : skill.skillType.ToString();
        Color typeColor = (skillTypeColors != null && type >= 0 && type < skillTypeColors.Length) ? skillTypeColors[type] : Color.white;
        SetHeader(skill.icon, skill.skillName, skill.description, typeName, typeColor);

        var entries = new List<EffectEntry>();
        if (skill.cooldown > 0f)
            entries.Add(new EffectEntry { label = "쿨타임", value = EffectText.Num(skill.cooldown) + "초", isShort = true });
        if (skillSlotLabels != null && type >= 0 && type < skillSlotLabels.Length && !string.IsNullOrEmpty(skillSlotLabels[type]))
            entries.Add(new EffectEntry { label = "들어갈 칸", value = skillSlotLabels[type], isShort = true });
        LayoutEffects(entries);
    }

    // 아이콘 · 이름 · 설명 · 등급 칸 채우기 (아이템/스킬 공용)
    void SetHeader(Sprite icon, string title, string description, string tierLabel, Color tierColor)
    {
        if (iconImage != null) iconImage.sprite = icon;
        if (nameText != null) nameText.text = title;

        // 설명 — 비어 있으면 줄 자체를 숨김
        if (descText != null)
        {
            bool hasDesc = !string.IsNullOrEmpty(description);
            descText.gameObject.SetActive(hasDesc);
            if (hasDesc) descText.text = description;
        }

        if (tierText != null)
        {
            tierText.text = tierLabel;
            tierText.color = tierColor;
        }
        if (tierChip != null)
            tierChip.color = new Color(tierColor.r, tierColor.g, tierColor.b, tierChipAlpha);
        if (iconSlot != null)
            iconSlot.color = tierColor;
    }

    // 효과 항목들을 줄 단위 칸으로 배치한다.
    //  짧은 효과 2개 → 한 줄에 같은 너비 2칸 / 긴 효과 → 한 줄 전체 1칸
    //  짧은 효과가 하나만 남으면 → 반 칸 + 빈 자리 (칸 너비가 위아래 줄과 맞도록)
    void FillEffects(List<string> lines)
    {
        var entries = new List<EffectEntry>();
        foreach (string e in lines)
        {
            if (string.IsNullOrEmpty(e)) continue;
            SplitValue(e, out string label, out string value);
            entries.Add(new EffectEntry { label = label, value = value, isShort = IsShort(e) });
        }
        LayoutEffects(entries);
    }

    void LayoutEffects(List<EffectEntry> entries)
    {
        if (effectsRoot == null || effectCellTemplate == null) return;
        effectCellTemplate.SetActive(false);

        int row = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            EffectEntry e = entries[i];
            EffectRow r = GetRow(row++);
            if (e.isShort)
            {
                // 다음 항목도 짧으면 같은 줄 오른쪽 칸에
                EffectEntry next = null;
                if (i + 1 < entries.Count && entries[i + 1].isShort) { next = entries[i + 1]; i++; }

                SetCell(r.left, e);
                r.right.go.SetActive(true);
                SetCell(r.right, next);   // next 가 없으면 빈 자리
            }
            else
            {
                SetCell(r.left, e);
                r.right.go.SetActive(false); // 한 줄 전체
            }
            r.go.SetActive(true);
        }

        for (int k = row; k < effectRows.Count; k++)
            effectRows[k].go.SetActive(false);

        effectsRoot.gameObject.SetActive(row > 0);
    }

    bool IsShort(string e) => EffectText.VisibleLength(e) <= effectShortMaxLength;

    EffectRow GetRow(int index)
    {
        while (effectRows.Count <= index)
        {
            var go = new GameObject("EffectRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.layer = effectsRoot.gameObject.layer;
            go.transform.SetParent(effectsRoot, false);

            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = effectCellSpacing;
            h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;   // 같은 줄 두 칸은 높이도 같게

            effectRows.Add(new EffectRow { go = go, left = MakeCell(go.transform), right = MakeCell(go.transform) });
        }
        return effectRows[index];
    }

    EffectCell MakeCell(Transform parent)
    {
        GameObject go = Instantiate(effectCellTemplate, parent, false);
        go.name = "EffectCell";
        go.SetActive(true);
        Transform label = go.transform.Find("Label");
        Transform value = go.transform.Find("Value");
        return new EffectCell
        {
            go = go,
            bg = go.GetComponent<Image>(),
            label = label != null ? label.GetComponent<TextMeshProUGUI>() : null,
            value = value != null ? value.GetComponent<TextMeshProUGUI>() : null,
        };
    }

    // 칸 하나 채우기. entry 가 null 이면 배경 없는 빈 자리로 둠
    void SetCell(EffectCell c, EffectEntry entry)
    {
        bool empty = entry == null;
        if (c.bg != null) c.bg.enabled = !empty;

        string label = empty ? "" : entry.label ?? "";
        string value = empty ? "" : entry.value ?? "";

        if (c.label != null) c.label.text = label;
        if (c.value != null)
        {
            c.value.text = value;
            c.value.gameObject.SetActive(value.Length > 0);
        }
    }

    // "공격력 <color=#7CE38B>+5</color>" → 이름 "공격력" / 수치 "<color=#7CE38B>+5</color>"
    // 마지막 띄어쓰기 뒤가 +, - 로 시작하는 수치일 때만 떼어냄. 아니면 전체를 이름 칸에
    static void SplitValue(string entry, out string label, out string value)
    {
        label = entry; value = "";
        int lastSpace = -1;
        bool inTag = false;
        for (int i = 0; i < entry.Length; i++)
        {
            char ch = entry[i];
            if (ch == '<') inTag = true;
            else if (ch == '>') inTag = false;
            else if (ch == ' ' && !inTag) lastSpace = i;
        }
        if (lastSpace <= 0) return;

        string tail = entry.Substring(lastSpace + 1);
        string visible = System.Text.RegularExpressions.Regex.Replace(tail, "<[^>]*>", "");
        if (visible.Length >= 2 && (visible[0] == '+' || visible[0] == '-') && char.IsDigit(visible[1]))
        {
            label = entry.Substring(0, lastSpace);
            value = tail;
        }
    }

    string GetTierName(int tier)
    {
        if (tierNames != null && tier >= 0 && tier < tierNames.Length && !string.IsNullOrEmpty(tierNames[tier]))
            return tierNames[tier];
        return ((ItemTier)tier).ToString();
    }

    Color GetTierColor(int tier)
    {
        if (tierColors != null && tier >= 0 && tier < tierColors.Length)
            return tierColors[tier];
        return Color.white;
    }

    // 팝업 위치 갱신 + 화면 밖으로 나가지 않게 밀어 넣기
    void UpdatePosition()
    {
        if (popupRect == null) return;

        float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;   // 기준 해상도 픽셀 → 실제 화면 픽셀
        Vector2 size = popupRect.rect.size * scale;
        Vector2 pos;

        if (followMode == FollowMode.Mouse)
        {
            Vector2 mouse = Input.mousePosition;
            pos = mouse + mouseOffset * scale;

            // 커서 오른쪽에 두면 화면을 넘는 경우 → 커서 왼쪽에 둠
            if (pos.x + size.x > Screen.width - screenMargin * scale)
                pos.x = mouse.x - mouseOffset.x * scale - size.x;
        }
        else
        {
            if (worldTarget == null) { Hide(); return; }
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ItemInfoPopup] Camera.main이 null이라 위치 갱신 못함");
                return;
            }
            pos = cam.WorldToScreenPoint(worldTarget.position + worldOffset);
        }

        // 화면 안으로 밀어 넣기 (기준점 위치를 고려해 팝업의 네 변을 확인)
        Vector2 pivot = popupRect.pivot;
        float m = screenMargin * scale;
        float left = pos.x - pivot.x * size.x;
        float bottom = pos.y - pivot.y * size.y;
        left = Mathf.Clamp(left, m, Mathf.Max(m, Screen.width - m - size.x));
        bottom = Mathf.Clamp(bottom, m, Mathf.Max(m, Screen.height - m - size.y));
        pos.x = left + pivot.x * size.x;
        pos.y = bottom + pivot.y * size.y;

        popupRoot.transform.position = pos;
    }

    public void Hide()
    {
        worldTarget = null;
        UIUtil.HidePanel(popupRoot);
    }
}
