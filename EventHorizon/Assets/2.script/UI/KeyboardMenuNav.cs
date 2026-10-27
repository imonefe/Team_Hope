using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-28 추가] 메뉴 버튼을 키보드 위/아래로 고르고 결정하는 공용 컴포넌트 (인트로 화면, ESC 일시정지 메뉴).
///
/// - ↑/↓ (또는 W/S): 버튼을 화면 위→아래 순서로 한 칸씩 이동. 끝에서 누르면 반대쪽 끝으로 돌아감.
///   아무것도 선택되지 않은 상태에서 처음 누르면 맨 위(또는 지정한 첫 버튼)를 고른다
///   (마우스만 쓰는 사람에게 처음부터 선택 표시가 떠 있지 않게).
/// - 엔터 / 스페이스 / 키패드 엔터: 고른 버튼 누르기. (엔터는 유니티 기본 입력이 처리하므로 여기선 스페이스·키패드 엔터만 추가)
/// - 마우스를 버튼 위에 올리면 그 버튼이 선택된 버튼이 되어, 키보드로 이어서 움직일 수 있고 강조가 두 군데 뜨지 않는다.
/// - 유니티 기본 자동 이동(Navigation)은 꺼서, 이 컴포넌트와 겹쳐 두 칸씩 움직이지 않게 한다.
///
/// 사용: KeyboardMenuNav.Attach(붙일 오브젝트, () => 버튼들이 들어 있는 부모, () => 지금 막아야 하는지, 첫 버튼(선택));
/// 선택 강조 모양은 버튼에 붙어 있는 ButtonHighlightDeco / ButtonTextColorSync 가 그대로 그린다.
/// </summary>
public class KeyboardMenuNav : MonoBehaviour
{
    Func<Transform> rootProvider;
    Func<bool> isBlocked;
    Func<GameObject> firstProvider;
    Transform preparedRoot;
    readonly List<Button> items = new List<Button>();

    public static KeyboardMenuNav Attach(GameObject host, Func<Transform> root, Func<bool> blocked, Func<GameObject> first = null)
    {
        if (host == null) return null;
        var nav = host.GetComponent<KeyboardMenuNav>();
        if (nav == null) nav = host.AddComponent<KeyboardMenuNav>();
        nav.rootProvider = root;
        nav.isBlocked = blocked;
        nav.firstProvider = first;
        return nav;
    }

    void Update()
    {
        if (rootProvider == null) return;
        Transform root = rootProvider();
        if (root == null || !root.gameObject.activeInHierarchy) return;
        if (isBlocked != null && isBlocked()) return;
        EventSystem es = EventSystem.current;
        if (es == null) return;

        Prepare(root);

        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!up && !down && !confirm) return;

        CollectItems(root);
        if (items.Count == 0) return;

        GameObject cur = es.currentSelectedGameObject;
        int idx = -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i].gameObject == cur) { idx = i; break; }

        if (up || down)
        {
            int next;
            if (idx < 0)
            {
                GameObject first = firstProvider != null ? firstProvider() : null;
                next = 0;
                if (first != null)
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].gameObject == first) { next = i; break; }
            }
            else next = (idx + (down ? 1 : -1) + items.Count) % items.Count;

            es.SetSelectedGameObject(null);   // 같은 버튼을 다시 골라도 선택 효과가 다시 나오게
            es.SetSelectedGameObject(items[next].gameObject);
        }
        else if (confirm && idx >= 0)
        {
            // 마우스 클릭·엔터와 같은 경로(Submit)로 눌러서 버튼 연출·소리도 똑같이 나오게
            ExecuteEvents.Execute(items[idx].gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
        }
    }

    // 처음 한 번(또는 패널이 바뀌었을 때): 기본 자동 이동을 끄고, 마우스 올리면 선택되게
    void Prepare(Transform root)
    {
        if (preparedRoot == root) return;
        preparedRoot = root;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            if (b.GetComponent<KeyboardMenuSelectOnHover>() == null) b.gameObject.AddComponent<KeyboardMenuSelectOnHover>();
        }
    }

    // 지금 보이고 누를 수 있는 버튼을 화면 위→아래(같은 높이면 왼쪽→오른쪽) 순서로
    void CollectItems(Transform root)
    {
        items.Clear();
        foreach (var b in root.GetComponentsInChildren<Button>(false))
            if (b.IsInteractable() && b.isActiveAndEnabled) items.Add(b);
        items.Sort((a, b) =>
        {
            float ay = a.transform.position.y, by = b.transform.position.y;
            if (Mathf.Abs(ay - by) > 1f) return by.CompareTo(ay);
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });
    }
}

/// <summary>마우스를 올린 버튼을 선택된 버튼으로 맞춘다 (키보드와 마우스를 섞어 써도 강조가 하나만 뜨게)</summary>
public class KeyboardMenuSelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        var sel = GetComponent<Selectable>();
        if (sel == null || !sel.IsInteractable() || EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
