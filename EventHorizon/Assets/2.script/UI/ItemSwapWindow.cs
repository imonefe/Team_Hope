using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-26] 사이버웨어 교체 — 8칸이 가득 찬 채로 새 사이버웨어를 얻을 때 (ItemObtain 이 부름).
///  게임을 멈추고, 가진 8개 중 하나를 골라 버리거나(판매가만큼 파편) 새 것을 포기한다.
///  버리는 건 되돌릴 수 없어서 [고르기 → 교체] 두 단계. ← → 고르기 · Enter 교체 · Esc 포기.
///
///  [17:31 변경] 인벤토리 창 안에서 고른다:
///   - 씬의 InventoryTab 에 붙고(메뉴 EventHorizon/인벤토리 8칸 배치 · 교체 줄 만들기), Inventory.swapWindow 에 연결됨
///   - 교체 중에는 인벤토리 창을 맨 위(정렬 inventorySortOrder)로 열고, 창 아래쪽 '교체 줄'(panel)을 켠다
///   - 인벤토리 칸을 누르면 그 칸을 고른 것 (ItemSlotUI → PickItem)
///  씬에 연결이 없으면 예전처럼 Resources/ItemSwapCanvas 프리팹 창으로 띄운다 (예비).
/// </summary>
public class ItemSwapWindow : MonoBehaviour
{
    const string ResourcePath = "ItemSwapCanvas";
    const string PauseKey = "ItemSwap";

    [Tooltip("켜고 끄는 본체 (인벤토리 안이면 '교체 줄', 프리팹이면 Canvas 바로 아래 Panel)")]
    public GameObject panel;
    public TMP_Text title;
    [Tooltip("선택 안내 / 고른 것 → 파편 한 줄")]
    public TMP_Text info;
    [Tooltip("새로 얻으려는 사이버웨어 칸")]
    public ItemSlotUI newSlot;
    public TMP_Text newName;
    [Tooltip("(프리팹 창만) 가진 사이버웨어 8칸")]
    public ItemSlotUI[] slots;
    [Tooltip("(프리팹 창만) 칸마다 고른 표시 테두리")]
    public GameObject[] frames;
    public Button swapButton;
    public Button cancelButton;
    [Tooltip("교체 버튼 — 아직 안 골랐을 때 흐리게")]
    public CanvasGroup swapGroup;
    [Header("인벤토리 안에서 고르기 (2026-11-26 17:31)")]
    [Tooltip("켜면 인벤토리 창을 열어 그 칸에서 고른다 (씬의 InventoryTab 에 붙은 것)")]
    public bool useInventory;
    [Tooltip("교체 중 인벤토리 창의 정렬 순서 — 증강 고르기(320) · 상점(300) · 특전(520) 위, 게임오버(600) · 설명 팝업(700) 아래")]
    public int inventorySortOrder = 555;
    [Tooltip("[17:41] 교체 중 인벤토리 뒤를 가리는 어두운 판 (InventoryPanel 맨 아래 SwapDim) — 뒤의 창이 비쳐 겹쳐 보이지 않게")]
    public GameObject dim;
    [Tooltip("[17:46] 교체 중에는 끄는 판 (상태 판) — 그 자리에 교체 판(panel)이 대신 뜸")]
    public GameObject[] hideWhileSwapping;
    [Tooltip("[18:02] 교체 중 화면 가운데로 옮길 것 (인벤토리 판 · 'inventory' 제목) — 닫으면 제자리")]
    public RectTransform[] centerDuringSwap;
    [Tooltip("가운데로 옮길 거리 (x)")]
    public float centerShiftX;

    [Header("[18:02] 교체 확인 창 — 칸을 고르면 뜸")]
    public GameObject confirmRoot;
    public ItemSlotUI confirmOld, confirmNew;
    public TMP_Text confirmOldName, confirmNewName, confirmInfo;
    public Button confirmYes, confirmNo;
    [Tooltip("확인 창 아래 키 안내 (키 이름은 설정을 따름)")]
    public TMP_Text confirmHint;
    bool confirming;
    int focus;   // 0 = 교체, 1 = 취소

    static ItemSwapWindow inst;
    static int closedFrame = -1;

    /// <summary>창이 떠 있는지</summary>
    public static bool IsOpen => inst != null && inst.open;
    /// <summary>떠 있거나 방금 닫힌 프레임 — 같은 키(Esc · Enter)로 뒤의 창이 같이 반응하지 않게</summary>
    public static bool BlocksInput => IsOpen || Time.frameCount == closedFrame;

    struct Request { public ItemData item; public Action<int> onSwap; public Action onCancel; }
    readonly Queue<Request> queue = new Queue<Request>();
    Request cur;
    bool open;
    bool wired;
    int selected = -1;
    Canvas raisedCanvas;
    int savedOrder;
    bool savedOverride;

    /// <summary>교체를 묻는다. 이미 떠 있으면 줄을 세워 차례로 묻는다</summary>
    public static void Ask(ItemData incoming, Action<int> onSwap, Action onCancel)
    {
        if (inst == null && Inventory.Instance != null && Inventory.Instance.swapWindow != null)
            inst = Inventory.Instance.swapWindow;   // 씬의 인벤토리 안 (꺼져 있어도 참조는 살아 있음)
        if (inst == null)
        {
            var prefab = Resources.Load<ItemSwapWindow>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[교체] 인벤토리 교체 줄도 프리팹도 없습니다 — 메뉴 EventHorizon/인벤토리 8칸 배치 · 교체 줄 만들기. 이번엔 포기로 처리");
                onCancel?.Invoke();
                return;
            }
            inst = Instantiate(prefab);
            inst.name = ResourcePath;
        }
        inst.queue.Enqueue(new Request { item = incoming, onSwap = onSwap, onCancel = onCancel });
        if (!inst.open) inst.ShowNext();
    }

    /// <summary>[18:27] 인벤토리 칸에 마우스를 올렸을 때 — 지금 칸만 옮김 (확인 창은 안 띄움)</summary>
    public static void FocusItem(ItemData d)
    {
        if (!IsOpen || inst.confirming || d == null || Inventory.Instance == null) return;
        int i = Inventory.Instance.passives.IndexOf(d);
        if (i < 0 || i == inst.selected) return;
        inst.selected = i;
        inst.Refresh();
    }

    /// <summary>인벤토리 칸을 눌렀을 때 (ItemSlotUI) — 그 사이버웨어를 버릴 후보로 고름</summary>
    public static void PickItem(ItemData d)
    {
        if (!IsOpen || d == null || Inventory.Instance == null) return;
        inst.Select(Inventory.Instance.passives.IndexOf(d));
    }

    void Awake() => Wire();

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (inst == null) inst = this;
        if (!useInventory && panel != null && panel.GetComponent<UIPanelAnimator>() == null) panel.AddComponent<UIPanelAnimator>();
        if (!open && panel != null) panel.SetActive(false);
        if (swapButton != null) swapButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (confirmYes != null) { confirmYes.onClick.AddListener(Confirm); HookFocus(confirmYes, 0); }
        if (confirmNo != null) { confirmNo.onClick.AddListener(CloseConfirm); HookFocus(confirmNo, 1); }
        if (!open && confirmRoot != null) confirmRoot.SetActive(false);
        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
            {
                int k = i;
                if (slots[i] != null) slots[i].onClick = _ => Select(k);
            }
    }

    void OnDestroy()
    {
        if (open) EndPause();
        if (inst == this) inst = null;
    }

    void ShowNext()
    {
        if (queue.Count == 0) { Close(); return; }
        cur = queue.Dequeue();
        if (!open)
        {
            open = true;
            GamePause.Push(PauseKey);
            CombatUIGate.Block();
            if (PlayerController.Instance != null) PlayerController.Instance.LockControl(this);
            if (useInventory) OpenInventory();
        }
        Wire();   // 인벤토리 안이면 창이 처음 켜질 때 Awake 가 돌지만, 혹시 몰라 한 번 더 (중복 연결 없음)
        ItemInfoPopup.Instance?.Hide();
        if (panel != null) panel.SetActive(true);
        selected = useInventory ? 0 : -1;   // [18:27] 인벤토리에서 고를 때는 첫 칸부터 (키보드로 바로 움직이게)
        Fill();
    }

    // 인벤토리 창을 맨 위로 열기 (증강 고르기 · 상점 창 위에 보이게)
    void OpenInventory()
    {
        var inv = Inventory.Instance;
        if (inv == null || inv.panelRoot == null) return;
        raisedCanvas = inv.panelRoot.GetComponentInParent<Canvas>(true);
        if (raisedCanvas != null)
        {
            savedOrder = raisedCanvas.sortingOrder;
            savedOverride = raisedCanvas.overrideSorting;
            if (!raisedCanvas.isRootCanvas) raisedCanvas.overrideSorting = true;
            raisedCanvas.sortingOrder = inventorySortOrder;
        }
        if (dim != null) dim.SetActive(true);
        if (hideWhileSwapping != null) foreach (var g in hideWhileSwapping) if (g != null) g.SetActive(false);
        if (centerDuringSwap != null)
            foreach (var rt in centerDuringSwap) if (rt != null) rt.anchoredPosition += new Vector2(centerShiftX, 0f);
        inv.Open();
    }

    void CloseInventory()
    {
        var inv = Inventory.Instance;
        if (dim != null) dim.SetActive(false);
        if (hideWhileSwapping != null) foreach (var g in hideWhileSwapping) if (g != null) g.SetActive(true);
        if (centerDuringSwap != null)
            foreach (var rt in centerDuringSwap) if (rt != null) rt.anchoredPosition -= new Vector2(centerShiftX, 0f);
        if (inv != null) inv.Close();
        if (raisedCanvas != null)
        {
            raisedCanvas.sortingOrder = savedOrder;
            raisedCanvas.overrideSorting = savedOverride;
            raisedCanvas = null;
        }
    }

    void Fill()
    {
        var owned = Inventory.Instance != null ? Inventory.Instance.passives : null;
        if (newSlot != null) newSlot.SetItem(cur.item, null);
        if (newName != null) newName.text = cur.item != null ? cur.item.itemName : "";
        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].SetItem(owned != null && i < owned.Count ? owned[i] : null, null);
        Refresh();
    }

    void Refresh()
    {
        var owned = Inventory.Instance != null ? Inventory.Instance.passives : null;
        var pick = owned != null && selected >= 0 && selected < owned.Count ? owned[selected] : null;
        if (pick == null) selected = -1;
        if (frames != null)
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) frames[i].SetActive(i == selected);
        ItemSlotUI.SetSwapPick(pick);   // 인벤토리 칸 붉은 표시
        if (info != null)
            info.text = pick == null
                ? (useInventory ? "버릴 사이버웨어를 인벤토리에서 고르세요" : "버릴 사이버웨어를 고르세요")
                : $"<color=#E3C27E>{pick.itemName}</color> 을(를) 버리고 파편 <color=#B08CE0>+{ItemObtain.DiscardShards(pick)}</color>";
        if (swapButton != null) swapButton.interactable = pick != null;
        if (swapGroup != null) swapGroup.alpha = pick != null ? 1f : 0.4f;
    }

    void Select(int i)
    {
        if (!open) return;
        var owned = Inventory.Instance != null ? Inventory.Instance.passives : null;
        if (owned == null || i < 0 || i >= owned.Count) return;
        selected = i;
        Refresh();
        if (useInventory) OpenConfirm();   // [18:02] 인벤토리에서 고르면 바로 확인 창
    }

    // ── [18:02] 교체 확인 창 ──
    void OpenConfirm()
    {
        if (!open || confirmRoot == null) return;
        var owned = Inventory.Instance != null ? Inventory.Instance.passives : null;
        var pick = owned != null && selected >= 0 && selected < owned.Count ? owned[selected] : null;
        if (pick == null) return;
        confirming = true;
        ItemInfoPopup.Instance?.Hide();
        confirmRoot.SetActive(true);
        confirmRoot.transform.SetAsLastSibling();
        if (confirmOld != null) confirmOld.SetItem(pick, null);
        if (confirmNew != null) confirmNew.SetItem(cur.item, null);
        if (confirmOldName != null) confirmOldName.text = pick.itemName;
        if (confirmNewName != null) confirmNewName.text = cur.item != null ? cur.item.itemName : "";
        if (confirmHint != null) confirmHint.text = $"← → 선택     {KeyBindings.KeyName(KeyAction.Interact)} 결정     Esc 취소";
        SetFocus(0);
        if (confirmInfo != null) confirmInfo.text = $"파편 <color=#B08CE0>+{ItemObtain.DiscardShards(pick)}</color>";
    }

    // [18:21] 버튼에 마우스를 올리면 그 버튼이 골라짐 + 누름 · 올림 반응 (UIButtonJuice)
    void HookFocus(Button b, int idx)
    {
        UIButtonJuice.Attach(b);
        var trig = b.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => SetFocus(idx));
        trig.triggers.Add(e);
    }

    // 고른 버튼만 금색 선택 테두리 (카드 그림의 SelectFrame)
    void SetFocus(int idx)
    {
        focus = Mathf.Clamp(idx, 0, 1);
        SetFrame(confirmYes, focus == 0);
        SetFrame(confirmNo, focus == 1);
    }

    static void SetFrame(Button b, bool on)
    {
        if (b == null) return;
        var f = b.transform.Find("SelectFrame");
        if (f != null) f.gameObject.SetActive(on);
    }

    void CloseConfirm()
    {
        confirming = false;
        ItemInfoPopup.Instance?.Hide();
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }

    // [18:27] 인벤토리 그리드 칸 이동 (끝에서 멈춤)
    void MoveGrid(int step)
    {
        int n = Inventory.Instance != null ? Inventory.Instance.passives.Count : 0;
        if (n == 0) return;
        if (selected < 0) { selected = 0; Refresh(); return; }
        int to = selected + step;
        if (to < 0 || to >= n) return;
        int cols = GridColumns();
        if ((step == 1 || step == -1) && cols > 1 && to / cols != selected / cols) return;   // 옆으로는 같은 줄 안에서만
        selected = to;
        Refresh();
    }

    int gridCols = -1;
    int GridColumns()
    {
        if (gridCols > 0) return gridCols;
        var g = GetComponentInChildren<GridLayoutGroup>(true);
        gridCols = g != null && g.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? Mathf.Max(1, g.constraintCount) : 2;
        return gridCols;
    }

    void Move(int dir)
    {
        int n = Inventory.Instance != null ? Inventory.Instance.passives.Count : 0;
        if (!useInventory && slots != null) n = Mathf.Min(n, slots.Length);
        if (n == 0) return;
        selected = selected < 0 ? (dir > 0 ? 0 : n - 1) : (selected + dir + n) % n;
        Refresh();
    }

    void Update()
    {
        if (!open) return;
        if (confirming)
        {
            // [18:21] ← → 로 버튼 고르기, 상호작용 키(F)로 결정, Esc 취소
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)) { UIClickSound.PlayClick(); SetFocus(1 - focus); }
            if (KeyBindings.Down(KeyAction.Interact)) { UIClickSound.PlayClick(); if (focus == 0) Confirm(); else CloseConfirm(); }
            else if (Input.GetKeyDown(KeyCode.Escape)) { UIClickSound.PlayClick(); CloseConfirm(); }
            return;
        }
        if (useInventory)
        {
            // [18:27] 인벤토리에서 고르는 중: 방향키로 칸 이동, F(상호작용) · Enter 로 확인 창, Esc = 새 것 포기
            int cols = GridColumns();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { UIClickSound.PlayClick(); MoveGrid(-1); }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { UIClickSound.PlayClick(); MoveGrid(+1); }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { UIClickSound.PlayClick(); MoveGrid(-cols); }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { UIClickSound.PlayClick(); MoveGrid(+cols); }
            if (KeyBindings.Down(KeyAction.Interact) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { UIClickSound.PlayClick(); OpenConfirm(); }
            else if (Input.GetKeyDown(KeyCode.Escape)) { UIClickSound.PlayClick(); Cancel(); }
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { UIClickSound.PlayClick(); Move(-1); }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { UIClickSound.PlayClick(); Move(+1); }
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) && selected >= 0) { UIClickSound.PlayClick(); if (useInventory && confirmRoot != null) OpenConfirm(); else Confirm(); }
        else if (Input.GetKeyDown(KeyCode.Escape)) { UIClickSound.PlayClick(); Cancel(); }
    }

    void Confirm()
    {
        if (!open || selected < 0) return;
        CloseConfirm();
        var r = cur;
        int idx = selected;
        Next();
        r.onSwap?.Invoke(idx);
        if (open) Fill();   // 줄 선 다음 것이 있으면 바뀐 목록으로 다시 그림
    }

    void Cancel()
    {
        if (!open) return;
        var r = cur;
        Next();
        r.onCancel?.Invoke();
        if (open) Fill();
    }

    // 지금 것을 끝내고 다음 차례로 (없으면 닫음)
    void Next()
    {
        closedFrame = Time.frameCount;
        ItemInfoPopup.Instance?.Hide();
        if (queue.Count > 0) { cur = queue.Dequeue(); selected = useInventory ? 0 : -1; }
        else Close();
    }

    void Close()
    {
        if (!open) return;
        open = false;
        closedFrame = Time.frameCount;
        ItemSlotUI.SetSwapPick(null);
        CloseConfirm();
        if (panel != null) panel.SetActive(false);
        if (useInventory) CloseInventory();
        EndPause();
    }

    void EndPause()
    {
        GamePause.Pop(PauseKey);
        CombatUIGate.Unblock();
        if (PlayerController.Instance != null) PlayerController.Instance.ResumeControl(this);
    }
}
