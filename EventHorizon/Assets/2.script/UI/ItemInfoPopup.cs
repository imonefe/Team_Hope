using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아이템 정보 팝업 (공용 싱글턴).
/// 두 가지 상황에서 공용으로 사용:
///  1) 인벤토리 슬롯 호버(ItemSlotUI) — 마우스 커서를 따라다님
///  2) 필드에 굴러다니는 아이템 근처 접근(Item.cs) — 플레이어 머리 위에 붙어서 따라다님
/// InventoryPanel이나 battle Canvas 밑이 아니라, 독립된 오브젝트/캔버스에 이 스크립트를 붙여야 한다 —
/// 그래야 인벤토리가 열려있든 닫혀있든 항상 존재해서 양쪽 상황 모두에서 팝업을 띄울 수 있다.
///
/// 구조: 아이콘 + 이름 + 설명 (윗줄) / 수치 정보 최대 4줄(statTexts, 아랫줄).
/// 0번 줄 = 등급, 1~3번 줄 = 아이템 효과(ItemData.effects 각각의 설명을 EffectText 가 글자로 만들어 줌).
/// 짧은 스탯 항목("공격력 +3")은 한 줄에 2개씩 묶고, 줄이 모자라면 마지막 줄에 이어 붙인다. 안 쓰는 줄은 숨김.
///
/// (퍼즈 투척 아이템 지원 기능은 제거됨 — 퍼즈 아이템 시스템 자체가 게임에서 빠지면서 함께 제거)
///
/// Show(item, pos)              : 마우스 커서를 따라다니는 모드 (인벤토리 호버용)
/// ShowNearItem(item, playerTr) : 지정한 Transform(플레이어) 머리 위를 따라다니는 모드 (월드 근접용)
/// </summary>
public class ItemInfoPopup : MonoBehaviour
{
    public static ItemInfoPopup Instance;

    enum FollowMode { Mouse, WorldTarget }

    [Header("루트")]
    [Tooltip("보이기/숨기기를 토글할 루트 오브젝트 (배경 Panel)")]
    public GameObject popupRoot;

    [Header("마우스 추적 (인벤토리 호버용)")]
    [Tooltip("마우스 커서 기준 오프셋. 기본값은 커서 오른쪽 아래로 살짝 띄움")]
    public Vector2 mouseOffset = new Vector2(20f, -20f);

    [Header("월드 타겟 추적 (필드 아이템 근접용)")]
    [Tooltip("플레이어 위치 기준 위쪽으로 띄울 높이(월드 유닛) — 캐릭터 머리 높이에 맞게 조정")]
    public Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    [Header("윗줄 — 아이콘/이름/설명")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("아랫줄 — 수치 정보 (최대 4줄, 순서대로 Popup01~04)")]
    [Tooltip("0번 = 등급, 1~3번 = 아이템 효과. 안 쓰는 줄은 자동으로 숨겨짐.")]
    public TextMeshProUGUI[] statTexts = new TextMeshProUGUI[4];

    [Header("효과 줄 표시")]
    [Tooltip("보이는 글자 수가 이 값 이하인 짧은 항목(예: 공격력 +3)은 한 줄에 2개씩 묶어서 표시")]
    public int pairMaxLength = 9;
    [Tooltip("한 줄에 묶인 두 항목 사이 간격")]
    public string pairSeparator = "    ";
    [Tooltip("글자가 줄 칸보다 길면 글자 크기를 원래 크기의 이 비율까지 자동으로 줄여서 칸 안에 맞춤 (겹침 방지)")]
    [Range(0.3f, 1f)] public float minFontScale = 0.6f;

    RectTransform popupRect;
    FollowMode followMode = FollowMode.Mouse;
    Transform worldTarget;

    void Awake()
    {
        Instance = this;

        // 씬 저장 과정에서 Canvas의 Sorting Order가 실수로 0으로 되돌아가는 일이 반복돼서,
        // 인스펙터 값에 의존하지 않고 여기서 강제로 다른 캔버스(battle Canvas/InventoryPanel, 둘 다 0)보다
        // 위에 그려지도록 확실히 고정한다.
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = 100;

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
            popupRect = popupRoot.GetComponent<RectTransform>();
            if (popupRect != null)
                popupRect.pivot = new Vector2(0f, 1f); // 왼쪽 위 기준 — 커서/머리 위 기준점에서 오른쪽 아래로 펼쳐짐

            // 뜰 때마다(=마우스 호버/근접할 때마다) 살짝 팝인되는 연출
            if (popupRoot.GetComponent<UIPanelAnimator>() == null)
                popupRoot.AddComponent<UIPanelAnimator>();

            // 팝업은 순수 표시용이라 마우스 판정을 가로채면 안 됨 — 그대로 두면 팝업이 마우스를 따라
            // 슬롯 위로 올라오는 순간 슬롯의 OnPointerExit가 불려서 꺼졌다 켜졌다를 무한 반복하게 된다.
            // 배경/아이콘/텍스트 등 모든 그래픽의 Raycast Target을 꺼서 클릭/호버 판정을 완전히 통과시킨다.
            foreach (var g in popupRoot.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }

        // 효과 설명이 길어져도 다음 줄과 겹치지 않도록, 칸보다 길면 글자 크기를 자동으로 줄이게 한다
        if (statTexts != null)
        {
            foreach (var t in statTexts)
            {
                if (t == null) continue;
                float baseSize = t.fontSize;
                t.enableAutoSizing = true;
                t.fontSizeMax = baseSize;
                t.fontSizeMin = baseSize * minFontScale;
            }
        }
    }

    void Update()
    {
        if (popupRoot == null || !popupRoot.activeSelf) return;

        if (followMode == FollowMode.Mouse)
        {
            popupRoot.transform.position = (Vector2)Input.mousePosition + mouseOffset;
        }
        else if (followMode == FollowMode.WorldTarget)
        {
            if (worldTarget == null) { Hide(); return; }
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ItemInfoPopup] Camera.main이 null이라 위치 갱신 못함");
                return;
            }
            Vector3 screenPos = cam.WorldToScreenPoint(worldTarget.position + worldOffset);
            popupRoot.transform.position = screenPos;
        }
    }

    /// <summary>일반 아이템 — 마우스 커서를 따라다니는 모드 (인벤토리 호버용)</summary>
    public void Show(ItemData item, Vector3 pos)
    {
        if (popupRoot == null || item == null) return;
        followMode = FollowMode.Mouse;
        popupRoot.SetActive(true);
        popupRoot.GetComponent<UIPanelAnimator>()?.Show(); // 퇴장 애니메이션 도중 다시 뜨는 경우 대비
        popupRoot.transform.position = (Vector2)Input.mousePosition + mouseOffset;
        FillItem(item);
    }

    /// <summary>일반 아이템 — 지정한 Transform(플레이어) 머리 위를 따라다니는 모드 (필드 아이템 근접용)</summary>
    public void ShowNearItem(ItemData item, Transform player)
    {
        if (popupRoot == null || item == null || player == null) return;
        followMode = FollowMode.WorldTarget;
        worldTarget = player;
        popupRoot.SetActive(true);
        popupRoot.GetComponent<UIPanelAnimator>()?.Show(); // 퇴장 애니메이션 도중 다시 뜨는 경우 대비
        FillItem(item);
    }

    void FillItem(ItemData item)
    {
        if (iconImage != null) iconImage.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (descText != null) descText.text = item.description;

        SetStatLine(0, "등급 : " + item.tier);

        // 1번 줄부터 효과 설명 채우기 (남는 줄은 숨김)
        List<string> effectLines = BuildEffectLines(EffectText.Collect(item), statTexts != null ? statTexts.Length - 1 : 0);
        for (int i = 1; statTexts != null && i < statTexts.Length; i++)
            SetStatLine(i, i - 1 < effectLines.Count ? effectLines[i - 1] : null);
    }

    // 효과 항목들을 maxLines 줄 안에 배치한다.
    // 짧은 항목 2개는 한 줄로 묶고, 그래도 줄이 모자라면 마지막 줄에 ", "로 이어 붙인다.
    List<string> BuildEffectLines(List<string> entries, int maxLines)
    {
        var result = new List<string>();
        if (maxLines <= 0) return result;

        for (int i = 0; i < entries.Count; i++)
        {
            string line = entries[i];
            if (i + 1 < entries.Count
                && EffectText.VisibleLength(entries[i]) <= pairMaxLength
                && EffectText.VisibleLength(entries[i + 1]) <= pairMaxLength)
            {
                line += pairSeparator + entries[i + 1];
                i++;
            }
            result.Add(line);
        }

        while (result.Count > maxLines)
        {
            int last = result.Count - 1;
            result[last - 1] += ", " + result[last];
            result.RemoveAt(last);
        }
        return result;
    }

    // index번째 수치 줄에 text를 채운다. text가 null이면 그 줄 자체를 꺼서 레이아웃에서 빈 칸이 안 보이게 한다.
    void SetStatLine(int index, string text)
    {
        if (statTexts == null || index < 0 || index >= statTexts.Length) return;
        TextMeshProUGUI t = statTexts[index];
        if (t == null) return;

        bool show = text != null;
        t.gameObject.SetActive(show);
        if (show) t.text = text;
    }

    public void Hide()
    {
        worldTarget = null;
        UIUtil.HidePanel(popupRoot);
    }
}
