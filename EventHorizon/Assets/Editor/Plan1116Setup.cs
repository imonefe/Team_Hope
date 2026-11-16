using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-16 추가] 기획서 반영용 1회성 도구.
/// 1) 상점 노드: ShopMap(MapRoomData, 상점 방) 만들기 + 1스테이지 노드 캔버스(graduate)에서
///    전투 5번 두 칸 → 상점 → 보스 순으로 연결 (보스 직전 필수 상점)
/// 2) 무기 교체(Tab) 안내: 설정 > 키 설정 창에 줄 추가, 조작법 창(열린 씬)에 줄 추가
/// </summary>
public static class Plan1116Setup
{
    const string ShopMapPath = "Assets/3. Data/MapData/1S/ShopMap.asset";
    const string CombatMapPath = "Assets/3. Data/MapData/1S/CombatMap.asset";
    const string ShopRoomPath = "Assets/1.Prefab/Room/Shop/ShopRoom_2S.prefab";
    const string NodeCanvasPath = "Assets/1.Prefab/UI/MapNode/graduate.prefab";
    const string ShopIconPath = "Assets/4. Art/UI/MapNode/Shop.png";
    const string SettingsPath = "Assets/1.Prefab/UI/Canvas/설정/Resources/SettingsCanvas.prefab";

    // ───────── 1) 상점 노드 ─────────
    [MenuItem("EventHorizon/상점 노드 연결 (1스테이지)")]
    public static void ConnectShopNode()
    {
        MapRoomData shopMap = AssetDatabase.LoadAssetAtPath<MapRoomData>(ShopMapPath);
        if (shopMap == null)
        {
            shopMap = ScriptableObject.CreateInstance<MapRoomData>();
            MapRoomData combat = AssetDatabase.LoadAssetAtPath<MapRoomData>(CombatMapPath);
            if (combat != null) shopMap.tier = combat.tier;
            shopMap.type = MapType.Shop;
            AssetDatabase.CreateAsset(shopMap, ShopMapPath);
        }
        shopMap.type = MapType.Shop;
        shopMap.roomPrefabs = new List<GameObject> { AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPath) };
        EditorUtility.SetDirty(shopMap);
        AssetDatabase.SaveAssets();

        GameObject root = PrefabUtility.LoadPrefabContents(NodeCanvasPath);
        try
        {
            MapButton boss = null, c5 = null, c5b = null, shop = null;
            foreach (MapButton b in root.GetComponentsInChildren<MapButton>(true))
            {
                if (b.name == "Boss_Button") boss = b;
                else if (b.name == "Combat_05") c5 = b;
                else if (b.name == "Combat_05 (1)") c5b = b;
                else if (b.name == "Shop_Button") shop = b;
            }
            if (boss == null || c5 == null || c5b == null) { Debug.LogError("[Plan1116] 노드를 못 찾음"); return; }

            if (shop == null)
            {
                GameObject go = Object.Instantiate(c5b.gameObject, c5b.transform.parent);
                go.name = "Shop_Button";
                shop = go.GetComponent<MapButton>();
            }
            // 전투 5번 두 칸(266,211 / 272,-116)과 보스(727,226) 사이
            ((RectTransform)shop.transform).anchoredPosition = new Vector2(500f, 40f);
            shop.roomData = shopMap;
            shop.isStartRoom = false;
            shop.connectedButtons = new List<MapButton> { boss };
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(ShopIconPath);
            if (shop.buttonImage != null && icon != null) shop.buttonImage.sprite = icon;
            c5.connectedButtons = new List<MapButton> { shop };
            c5b.connectedButtons = new List<MapButton> { shop };

            PrefabUtility.SaveAsPrefabAsset(root, NodeCanvasPath);
            Debug.Log("[Plan1116] 상점 노드 연결: 전투5 · 전투5(1) → 상점 → 보스");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ───────── 2) 무기 교체 안내 ─────────
    [MenuItem("EventHorizon/무기 교체 키 안내 추가 (설정 창 + 열린 씬 조작법)")]
    public static void AddWeaponSwapGuide()
    {
        AddKeyBindRow();
        AddControlsGuideRow();
    }

    static void AddKeyBindRow()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SettingsPath);
        try
        {
            KeyBindWindow win = root.GetComponentInChildren<KeyBindWindow>(true);
            if (win == null) { Debug.LogError("[Plan1116] KeyBindWindow 없음"); return; }
            foreach (var r in win.rows) if (r.action == KeyAction.WeaponSwap) { Debug.Log("[Plan1116] 키 설정 줄 이미 있음"); return; }

            Transform panel = FindDeep(root.transform, "KeyPanel");
            Transform label8 = panel.Find("KeyLabel_8"), button8 = panel.Find("KeyButton_8");
            GameObject label9 = Object.Instantiate(label8.gameObject, panel);
            GameObject button9 = Object.Instantiate(button8.gameObject, panel);
            label9.name = "KeyLabel_9"; button9.name = "KeyButton_9";
            label9.transform.SetSiblingIndex(button8.GetSiblingIndex() + 1);
            button9.transform.SetSiblingIndex(label9.transform.GetSiblingIndex() + 1);
            float step = ((RectTransform)label8).anchoredPosition.y - ((RectTransform)panel.Find("KeyLabel_7")).anchoredPosition.y; // -62
            ((RectTransform)label9.transform).anchoredPosition += new Vector2(0, step);
            ((RectTransform)button9.transform).anchoredPosition += new Vector2(0, step);
            TMP_Text lt = label9.GetComponentInChildren<TMP_Text>(true);
            if (lt != null) lt.text = KeyBindings.ActionName(KeyAction.WeaponSwap);

            var rows = new List<KeyBindWindow.Row>(win.rows);
            rows.Add(new KeyBindWindow.Row
            {
                action = KeyAction.WeaponSwap,
                button = button9.GetComponent<Button>(),
                keyText = button9.GetComponentInChildren<TMP_Text>(true),
            });
            win.rows = rows.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, SettingsPath);
            Debug.Log("[Plan1116] 키 설정 창에 '무기 교체' 줄 추가");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void AddControlsGuideRow()
    {
        ControlsGuidePopup popup = Object.FindObjectOfType<ControlsGuidePopup>(true);
        if (popup == null) { Debug.LogWarning("[Plan1116] 열린 씬에 조작법 창 없음 (SampleScene을 열고 실행)"); return; }
        Transform p = popup.transform;
        Transform existing = FindDeep(p, "Key_Tab");
        if (existing != null)
        {
            // 창 틀(Key_Tab의 부모) 높이가 예전 그대로(760)면 한 줄만큼 늘림
            RectTransform frame = (RectTransform)existing.parent;
            if (Mathf.Approximately(frame.sizeDelta.y, 760f))
            {
                frame.sizeDelta = new Vector2(frame.sizeDelta.x, 760f + 84f);
                EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
                EditorSceneManager.SaveScene(popup.gameObject.scene);
                Debug.Log("[Plan1116] 조작법 창 높이 760 → 844 (씬 저장)");
            }
            else Debug.Log("[Plan1116] 조작법 창 줄 이미 있음");
            return;
        }

        const float step = 84f;
        Transform keyD = FindDeep(p, "Key_D"), labelPause = FindDeep(p, "Label_Pause");
        if (keyD == null || labelPause == null)
        {
            string kids = ""; foreach (Transform c in p) kids += c.name + ", ";
            Debug.LogError($"[Plan1116] Key_D/Label_Pause 못 찾음 ({popup.name} 아래: {kids})"); return;
        }
        p = keyD.parent;
        GameObject key = Object.Instantiate(keyD.gameObject, p);
        GameObject label = Object.Instantiate(labelPause.gameObject, p);
        key.name = "Key_Tab"; label.name = "Label_WeaponSwap";
        key.transform.SetSiblingIndex(labelPause.GetSiblingIndex() + 1);
        label.transform.SetSiblingIndex(key.transform.GetSiblingIndex() + 1);
        RectTransform krt = (RectTransform)key.transform;
        krt.anchoredPosition += new Vector2(0, -step);
        krt.sizeDelta = new Vector2(84f, krt.sizeDelta.y);
        ((RectTransform)label.transform).anchoredPosition += new Vector2(0, -step);

        // 키 글자: 동작을 무기 교체로 (KeyLabel이 설정된 키 이름을 알아서 띄움)
        foreach (MonoBehaviour mb in key.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("action");
            if (prop != null) { prop.intValue = (int)KeyAction.WeaponSwap; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        TMP_Text kt = key.GetComponentInChildren<TMP_Text>(true);
        if (kt != null) kt.text = "Tab";
        TMP_Text lt = label.GetComponentInChildren<TMP_Text>(true);
        if (lt != null) lt.text = "무기 교체 <size=78%><color=#A79F8D>검 · 건틀릿 · 망치</color></size>";

        // 아래 줄(안내·아무 키) 내리고 창 높이 늘리기
        foreach (string n in new[] { "Label_Footer", "Label_PressAnyKey" })
        {
            RectTransform rt = FindDeep(p, n) as RectTransform;
            if (rt != null) rt.anchoredPosition += new Vector2(0, -step);
        }
        RectTransform prt = (RectTransform)p;   // 창 틀 (ControlsGuidePopup 컴포넌트는 캔버스에 붙어 있음)
        prt.sizeDelta += new Vector2(0, step);

        EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
        EditorSceneManager.SaveScene(popup.gameObject.scene);
        Debug.Log("[Plan1116] 조작법 창에 'Tab 무기 교체' 줄 추가 (씬 저장)");
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }
}
