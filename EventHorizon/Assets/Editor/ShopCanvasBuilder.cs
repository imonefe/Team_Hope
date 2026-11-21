using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-15 추가] 상점 화면 프리팹 생성기 (에디터 전용).
///
/// 메뉴: EventHorizon → 상점 프리팹 만들기
///   1.Prefab/UI/Canvas/상점/ 에 아래 3개를 만든다.
///   - ShopCanvas_Base : 공통 틀 (창·카드 4장·설명 띠·아래 버튼·일러스트 자리·대사창)
///   - ItemShopCanvas  : 기본 틀의 파생 프리팹, 영복 일러스트
///   - SkillShopCanvas : 기본 틀의 파생 프리팹, 일러스트 비움 (인물이 정해지면 그림만 넣기)
///
/// [2026-11-15 수정] 창 안의 배치는 전부 유니티 자동 정렬(가로/세로 정렬 묶음 + 크기 지정)로 한다.
///   - 크기·간격을 바꿀 때는 각 오브젝트의 '크기 지정(LayoutElement)' 값이나 부모의 '간격(Spacing)'만 고치면 나머지가 따라 움직인다.
///   - 테두리 선(Border, SelectFrame 등)은 정렬에서 빠지도록 '정렬 무시'가 켜져 있다.
///   - 일러스트와 대사창만 화면 모서리 기준(앵커)으로 고정 배치한다.
///
/// 만든 뒤에는 일반 프리팹이므로 에디터에서 직접 고치면 된다. 이 생성기를 다시 돌리면 덮어쓰므로 주의.
/// 화면만 만들고, 구매·판매 처리는 붙이지 않는다 (오브젝트 이름: Card_0~3, BuyButton, CurrencyAmount 등).
/// </summary>
public static class ShopCanvasBuilder
{
    const string ParentFolder = "Assets/1.Prefab/UI/Canvas";
    const string Folder = ParentFolder + "/상점";
    const string FramePath = "Assets/4. Art/Textures/UI/HUD/inventori.png";
    const string SlotPath = "Assets/4. Art/Textures/UI/HUD/레이어 54.png";
    const string CoinPath = "Assets/4. Art/Textures/UI/HUD/coin01.png";
    const string YoungbokPath = "Assets/4. Art/Textures/Illustration/이영복_대화.png";
    const string TitleFontPath = "Assets/Etc/Font/HiKR-ExtraBold SDF.asset";
    const string BodyFontPath = "Assets/Etc/Font/KoPubWorld Dotum_Pro Medium SDF.asset";

    const int FrameBorder = 140;          // 창 틀 그림의 늘림 기준선 (원본 픽셀)
    const float FrameBorderOnScreen = 44f; // 화면에 보일 틀 두께

    static readonly Color Gold = Hex("E3C27E");
    static readonly Color Dark = Hex("14110E");
    static readonly Color TextMain = Hex("EDE6D6");
    static readonly Color TextSub = Hex("B5B5B5");
    static readonly Color TextDim = Hex("A8A8A8");
    static readonly Color Line = Hex("3A3A3A");
    static readonly Color Legendary = Hex("E39A4B");
    static readonly Color Unique = Hex("B08BE0");
    static readonly Color Rare = Hex("6FA8DC");
    static readonly Color Common = Hex("B5AEA3");

    static TMP_FontAsset titleFont, bodyFont;
    static Sprite frameSprite, slotSprite, coinSprite, youngbokSprite;

    [MenuItem("EventHorizon/상점 프리팹 만들기")]
    public static void Build()
    {
        string basePath = Folder + "/ShopCanvas_Base.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(basePath) != null &&
            !EditorUtility.DisplayDialog("상점 프리팹 만들기", "이미 있는 상점 프리팹 3개를 덮어씁니다. 손으로 고친 내용은 사라집니다.", "덮어쓰기", "취소"))
            return;

        if (!LoadAssets()) return;

        GameObject root = BuildBase();
        PrefabUtility.SaveAsPrefabAsset(root, basePath);
        Object.DestroyImmediate(root);

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        MakeVariant(basePrefab, "ItemShopCanvas", youngbokSprite, "영복", "[영복 대사 한 줄]", false);
        MakeVariant(basePrefab, "SkillShopCanvas", null, "[인물 이름]", "[인물 대사 한 줄]", true);

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(basePrefab);
        Debug.Log("[상점 프리팹] " + Folder + " 에 3개를 만들었습니다.");
    }

    // 글꼴·그림 불러오기 (상점 프리팹 / 능력치 강화 프리팹 공용)
    static bool LoadAssets()
    {
        EnsureFrameBorder();
        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotPath);
        coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
        youngbokSprite = AssetDatabase.LoadAssetAtPath<Sprite>(YoungbokPath);
        if (titleFont == null || bodyFont == null || frameSprite == null || slotSprite == null || coinSprite == null)
        {
            EditorUtility.DisplayDialog("상점 프리팹 만들기", "글꼴 또는 그림을 찾지 못했습니다. 콘솔을 확인하세요.", "확인");
            Debug.LogError($"[상점 프리팹] 글꼴 {titleFont}/{bodyFont}, 틀 {frameSprite}, 칸 {slotSprite}, 동전 {coinSprite}");
            return false;
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(ParentFolder, "상점");
        return true;
    }

    // ───────────────────────── [2026-11-16 추가] 능력치 강화 화면 ─────────────────────────
    public const string StatUpgradePath = Folder + "/StatUpgradeCanvas.prefab";

    [MenuItem("EventHorizon/능력치 강화 프리팹 만들기")]
    public static void BuildStatUpgradeMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StatUpgradePath) != null &&
            !EditorUtility.DisplayDialog("능력치 강화 프리팹", "이미 있는 StatUpgradeCanvas 프리팹을 덮어씁니다. 손으로 고친 내용은 사라집니다.", "덮어쓰기", "취소"))
            return;
        BuildStatUpgrade();
    }

    /// <summary>
    /// 아이템 상점과 같은 틀(창·위쪽 줄·설명 띠·아래 줄·일러스트·대사창)에 카드 대신 가로로 긴 줄 5개.
    /// 전부 자동 정렬: Rows(세로 정렬) 안에 Row_0~4(가로 정렬) — 아이콘 · 이름/설명 · 단계 · 현재→후 · 비용 · 강화 버튼
    /// </summary>
    public static bool BuildStatUpgrade()
    {
        if (!LoadAssets()) return false;
        var root = BuildBase();
        root.name = "StatUpgradeCanvas";
        var win = (RectTransform)root.transform.Find("Window");

        var illImg = root.transform.Find("Illustration").GetComponent<Image>();
        illImg.sprite = null;
        illImg.color = new Color(1, 1, 1, 0.06f);   // 강화 담당 인물 그림이 정해지면 여기에 넣기
        root.transform.Find("DialogueBox/NpcName").GetComponent<TMP_Text>().text = "[강화 담당 인물]";
        root.transform.Find("DialogueBox/DialogueText").GetComponent<TMP_Text>().text = "[강화 담당 인물 대사 한 줄]";
        win.Find("HeaderRow/Title").GetComponent<TMP_Text>().text = "강화";

        // 카드 묶음 → 줄 묶음
        var cards = win.Find("Cards");
        int at = cards.GetSiblingIndex();
        Object.DestroyImmediate(cards.gameObject);
        var rows = Item("Rows", win);
        rows.SetSiblingIndex(at);
        LE(rows, -1, -1, 1, 1);
        var vg = VLayout(rows, 0, 0, 0, 0, 10, TextAnchor.UpperLeft);
        vg.childForceExpandWidth = true;
        string[] names = { "체력", "공격력", "방어력", "이동속도", "치명타" };
        string[] descs = { "최대 체력이 늘어난다.", "모든 공격의 피해가 늘어난다.", "받는 피해가 줄어든다.", "이동 속도가 빨라진다.", "치명타 확률과 배율이 함께 오른다." };
        for (int i = 0; i < names.Length; i++) BuildStatRow(rows, i, names[i], descs[i], i == 0);

        // 설명 띠 글자
        var col1 = win.Find("DetailStrip/Col_Selected");
        col1.Find("DetailLabel").GetComponent<TMP_Text>().text = "선택한 능력치";
        col1.Find("DetailName").GetComponent<TMP_Text>().text = "체력";
        var lvl = col1.Find("DetailGrade");
        lvl.name = "DetailLevel";
        var lvlT = lvl.GetComponent<TMP_Text>();
        lvlT.text = "0단계 / 5단계 · 다음 비용 [비용]";
        lvlT.color = TextDim;
        Object.DestroyImmediate(col1.Find("DetailFlavor").gameObject);
        win.Find("DetailStrip/Col_Effects/EffectLabel").GetComponent<TMP_Text>().text = "단계별 효과";
        win.Find("DetailStrip/Col_Effects/DetailEffects").GetComponent<TMP_Text>().text =
            "<color=#8FCB8F>▲ 다음 단계: 최대 체력 +[수치]</color>\n단계가 오를수록 비용이 늘어납니다\n강화는 이번 판 동안 유지됩니다";
        win.Find("DetailStrip/Col_Stats/StatLabel").GetComponent<TMP_Text>().text = "강화하면 달라지는 능력치";
        win.Find("DetailStrip/Col_Stats/DetailStats").GetComponent<TMP_Text>().text =
            "최대 체력  [현재] → <color=#8FCB8F>[후]</color>\n남은 재화  [현재] → [후]";
        SetBorderLines(win.Find("DetailStrip/Border"), Gold);

        // 아래 줄: 팔기·새로고침·확률 대신 안내 문구
        var bottom = win.Find("BottomRow");
        foreach (var n in new[] { "SellButton", "RefreshButton", "OddsText", "Spacer" })
        {
            var t = bottom.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        var hint = Txt(Item("HintText", bottom), "줄을 눌러 능력치를 고른 뒤 강화하세요", 14, TextDim, bodyFont, TextAlignmentOptions.MidlineLeft);
        hint.rectTransform.SetSiblingIndex(0);
        LE(hint.rectTransform, -1, 48, 1);

        PrefabUtility.SaveAsPrefabAsset(root, StatUpgradePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[상점 프리팹] " + StatUpgradePath + " 를 만들었습니다.");
        return true;
    }

    // 능력치 한 줄 (가로 정렬, 세로 가운데): 아이콘 · 이름/설명 · 단계 막대/글자 · 현재→후 · 비용 · 강화 버튼
    static void BuildStatRow(RectTransform parent, int index, string label, string desc, bool selected)
    {
        var row = Item("Row_" + index, parent);
        LE(row, -1, -1, 1, 1);
        var bg = Img(row, null, new Color(0.078f, 0.078f, 0.078f, 0.75f));
        bg.raycastTarget = true;
        row.gameObject.AddComponent<Button>().targetGraphic = bg;   // 줄을 누르면 선택
        HLayout(row, 22, 22, 0, 0, 24, TextAnchor.MiddleLeft);

        var slot = Item("IconSlot", row);
        LE(slot, 72, 72);
        Img(slot, slotSprite, Color.white).type = Image.Type.Sliced;
        Border(slot, Hex("5A5A5A"), 2, "GradeRing");
        var icon = Img(Rect("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56)), null, new Color(1, 1, 1, 0));
        icon.preserveAspect = true;
        Txt(Fill(Child("IconPlaceholder", slot)), "[아이콘]", 12, Hex("4A4A4A"), titleFont, TextAlignmentOptions.Center);

        var nameCol = Item("NameCol", row);
        LE(nameCol, 250, -1, 0);
        VLayout(nameCol, 0, 0, 0, 0, 4, TextAnchor.MiddleLeft);
        LE(Txt(Item("StatName", nameCol), label, 24, TextMain, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 32, 1);
        LE(Txt(Item("StatDesc", nameCol), desc, 15, TextSub, bodyFont, TextAlignmentOptions.TopLeft).rectTransform, -1, 40, 1);

        var lvCol = Item("LevelCol", row);
        LE(lvCol, 200, -1, 0);
        VLayout(lvCol, 0, 0, 0, 0, 8, TextAnchor.MiddleLeft);
        var pips = Item("Pips", lvCol);
        LE(pips, -1, 10, 1);
        HLayout(pips, 0, 0, 0, 0, 6, TextAnchor.MiddleLeft);
        for (int p = 0; p < 5; p++) LE(Img(Item("Pip_" + p, pips), null, Line).rectTransform, 30, 10);
        LE(Txt(Item("LevelText", lvCol), "0단계 / 5단계", 14, TextDim, bodyFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 20, 1);

        var val = Item("ValueRow", row);
        LE(val, 0, 40, 1);
        HLayout(val, 0, 0, 0, 0, 12, TextAnchor.MiddleLeft);
        LE(Txt(Item("Current", val), "[현재]", 22, Hex("C9C9C9"), bodyFont, TextAlignmentOptions.MidlineLeft).rectTransform, 150, 40);
        LE(Txt(Item("Arrow", val), "→", 22, Gold, bodyFont, TextAlignmentOptions.Center).rectTransform, 28, 40);
        LE(Txt(Item("Next", val), "[강화 후]", 22, Hex("8FCB8F"), titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, 170, 40);

        var cost = Item("CostRow", row);
        LE(cost, 130, 34, 0);
        HLayout(cost, 0, 0, 0, 0, 8, TextAnchor.MiddleRight);
        var c = Img(Item("CoinIcon", cost), coinSprite, Color.white);
        c.preserveAspect = true;
        LE(c.rectTransform, 24, 24);
        LE(Txt(Item("Cost", cost), "[비용]", 21, TextMain, titleFont, TextAlignmentOptions.MidlineRight).rectTransform, 90, 34);

        LE(Btn(Item("UpgradeButton", row), "강화", true, 18).GetComponent<RectTransform>(), 150, 52);

        Border(row, Gold, 2, "SelectFrame").SetActive(selected);
    }

    static void SetBorderLines(Transform border, Color c)
    {
        if (border == null) return;
        foreach (var img in border.GetComponentsInChildren<Image>(true)) img.color = c;
    }

    // 창 틀 그림에 늘림 기준선이 없으면 넣는다 (인벤토리는 늘림 없이 쓰므로 영향 없음)
    static void EnsureFrameBorder()
    {
        var imp = AssetImporter.GetAtPath(FramePath) as TextureImporter;
        if (imp == null || imp.spriteBorder != Vector4.zero) return;
        imp.spriteBorder = new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder);
        imp.SaveAndReimport();
    }

    // ───────────────────────── 기본 틀 ─────────────────────────
    static GameObject BuildBase()
    {
        var root = new GameObject("ShopCanvas", typeof(RectTransform));
        root.layer = 5;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // 인벤토리(10)·미니맵(5) 위, 조작법 창(400)·메뉴(500) 아래
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<CanvasGroup>();
        var rootRt = (RectTransform)root.transform;

        // 어두운 배경 (뒤 클릭 막음)
        var dim = Img(Fill(Child("Dim", rootRt)), null, new Color(0.043f, 0.039f, 0.031f, 0.88f));
        dim.raycastTarget = true;

        // 일러스트 (화면 오른쪽 위 모서리 기준, 하반신은 화면 밖으로 잘림)
        var ill = Rect("Illustration", rootRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(366, 168), new Vector2(1223, 1761));
        Img(ill, youngbokSprite, Color.white).preserveAspect = true;

        // 대사창 (화면 오른쪽 아래 모서리 기준) — 안쪽은 세로 정렬
        var dlg = Rect("DialogueBox", rootRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 40), new Vector2(520, 110));
        Img(dlg, null, new Color(0.055f, 0.047f, 0.039f, 0.92f));
        VLayout(dlg, 24, 24, 16, 16, 6, TextAnchor.UpperLeft);
        Border(dlg, Hex("5A4F42"), 1);
        LE(Txt(Item("NpcName", dlg), "[인물 이름]", 15, Gold, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 22, 1);
        LE(Txt(Item("DialogueText", dlg), "[대사 한 줄]", 18, TextMain, bodyFont, TextAlignmentOptions.TopLeft).rectTransform, -1, 50, 1, 1);

        // 창 (화면 왼쪽 위 모서리 기준) — 안쪽은 세로 정렬: 위쪽 줄 / 선 / 카드 / 설명 띠 / 선 / 아래 줄
        var win = Rect("Window", rootRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(1320, 1020));
        var winImg = Img(win, frameSprite, Color.white);
        winImg.type = Image.Type.Sliced;
        winImg.pixelsPerUnitMultiplier = FrameBorder / FrameBorderOnScreen;
        winImg.raycastTarget = true;
        VLayout(win, 56, 56, 44, 44, 16, TextAnchor.UpperLeft);

        // ① 위쪽 줄 (가로 정렬): 제목 · (빈 칸) · 동전 · 보유 재화 · 금액
        var header = Item("HeaderRow", win);
        LE(header, -1, 62, 1);
        HLayout(header, 0, 0, 0, 0, 12, TextAnchor.MiddleLeft);
        LE(Txt(Item("Title", header), "상점", 34, Gold, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, 300, 62);
        LE(Item("Spacer", header), -1, 62, 1);
        var coin = Img(Item("CoinIcon", header), coinSprite, Color.white);
        coin.preserveAspect = true;
        LE(coin.rectTransform, 34, 34);
        LE(Txt(Item("CurrencyLabel", header), "보유 재화", 15, TextDim, bodyFont, TextAlignmentOptions.MidlineRight).rectTransform, 90, 62);
        LE(Txt(Item("CurrencyAmount", header), "0,000", 24, Hex("D8D8D8"), bodyFont, TextAlignmentOptions.MidlineRight).rectTransform, 150, 62);

        LE(Img(Item("HeaderLine", win), null, Line).rectTransform, -1, 1, 1);

        // ② 카드 4장 (가로 정렬, 폭은 똑같이 나눔)
        var cards = Item("Cards", win);
        LE(cards, -1, -1, 1, 1);
        var cg = HLayout(cards, 0, 0, 0, 0, 18, TextAnchor.UpperLeft);
        cg.childForceExpandWidth = true;
        cg.childForceExpandHeight = true;
        var samples = new[]
        {
            ("세 시간의 말미", "레전더리", Legendary, "집중력이 크게 줄지만, 체력이 낮을수록 공격력이 오른다."),
            ("[아이템 이름]", "유니크", Unique, "[효과 설명 두 줄]"),
            ("[아이템 이름]", "레어", Rare, "[효과 설명 두 줄]"),
            ("[아이템 이름]", "일반", Common, "[효과 설명 두 줄]"),
        };
        for (int i = 0; i < 4; i++)
            BuildCard(cards, i, samples[i].Item1, samples[i].Item2, samples[i].Item3, samples[i].Item4, i == 0);

        // ③ 설명 띠 (가로 정렬): 선택한 아이템 · 정확한 효과 · 사면 달라지는 능력치
        var strip = Item("DetailStrip", win);
        LE(strip, -1, 152, 1);
        Img(strip, null, new Color(0.04f, 0.04f, 0.04f, 0.8f));
        HLayout(strip, 22, 22, 16, 12, 32, TextAnchor.UpperLeft);
        Border(strip, Legendary, 1);

        var col1 = Item("Col_Selected", strip);
        LE(col1, 300, -1, 0, 1);   // 고정 폭 (늘어나지 않음)
        VLayout(col1, 0, 0, 0, 0, 4, TextAnchor.UpperLeft);
        LE(Txt(Item("DetailLabel", col1), "선택한 아이템", 13, TextDim, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 20, 1);
        LE(Txt(Item("DetailName", col1), "세 시간의 말미", 23, TextMain, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 32, 1);
        LE(Txt(Item("DetailGrade", col1), "레전더리", 13, Legendary, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 20, 1);
        var flavor = Txt(Item("DetailFlavor", col1), "누군가가 건네준 세 시간의 말미. 가벼운 말 한마디가 거대한 조급함이 되어 다가왔다.", 14, TextDim, bodyFont, TextAlignmentOptions.TopLeft);
        flavor.fontStyle = FontStyles.Italic;
        LE(flavor.rectTransform, -1, 44, 1);

        var col2 = Item("Col_Effects", strip);
        LE(col2, 0, -1, 1, 1);   // 글이 길어도 띠 밖으로 밀어내지 않게 기본 폭 0
        VLayout(col2, 0, 0, 0, 0, 4, TextAnchor.UpperLeft);
        LE(Txt(Item("EffectLabel", col2), "정확한 효과", 13, TextDim, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 20, 1);
        LE(Txt(Item("DetailEffects", col2),
            "<color=#E07A6A>▼ 집중력 [수치] 감소</color>\n<color=#8FCB8F>▲ 공격력 최대 [수치]% 증가 (체력이 낮을수록 커짐)</color>\n<color=#C9C9C9>같은 아이템을 이미 가졌을 때: [겹침 규칙]</color>",
            16, TextMain, bodyFont, TextAlignmentOptions.TopLeft).rectTransform, -1, -1, 1, 1);

        var col3 = Item("Col_Stats", strip);
        LE(col3, 240, -1, 0, 1);   // 고정 폭 (늘어나지 않음)
        VLayout(col3, 0, 0, 0, 0, 4, TextAnchor.UpperLeft);
        LE(Txt(Item("StatLabel", col3), "사면 달라지는 능력치", 13, TextDim, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, -1, 20, 1);
        LE(Txt(Item("DetailStats", col3),
            "공격력  [현재] → <color=#8FCB8F>[후]</color>\n집중력  [현재] → <color=#E07A6A>[후]</color>",
            15, TextMain, bodyFont, TextAlignmentOptions.TopLeft).rectTransform, -1, -1, 1, 1);

        LE(Img(Item("BottomLine", win), null, Line).rectTransform, -1, 1, 1);

        // ④ 아래 줄 (가로 정렬): 팔기 · 새로고침 · (빈 칸) · 확률 · Esc
        var bottom = Item("BottomRow", win);
        LE(bottom, -1, 48, 1);
        HLayout(bottom, 0, 0, 0, 0, 12, TextAnchor.MiddleLeft);
        LE(Btn(Item("SellButton", bottom), "가진 아이템 팔기", false, 17).GetComponent<RectTransform>(), 220, 48);
        LE(Btn(Item("RefreshButton", bottom), "새로고침 · [비용]", false, 17).GetComponent<RectTransform>(), 220, 48);
        LE(Item("Spacer", bottom), -1, 48, 1);
        LE(Txt(Item("OddsText", bottom), "일반 37% · 레어 36% · 유니크 20% · 레전더리 7%", 14, TextDim, bodyFont, TextAlignmentOptions.MidlineRight).rectTransform, 420, 48);
        var esc = Item("EscHint", bottom);
        LE(esc, 130, 36);
        Border(esc, Hex("5A5A5A"), 1);
        Txt(Fill(Child("Label", esc)), "Esc 닫기", 15, TextMain, bodyFont, TextAlignmentOptions.Center);

        return root;
    }

    // 카드 한 장 (세로 정렬, 가운데 맞춤): 등급 막대 / 아이콘 칸 / 등급 / 이름 / 설명 / (빈 칸) / 가격 / 구매 버튼
    static void BuildCard(RectTransform parent, int index, string name, string grade, Color gradeColor, string desc, bool selected)
    {
        var card = Item("Card_" + index, parent);
        LE(card, 0, -1, 1, 1);   // 글 길이와 상관없이 4장 폭을 똑같이 (기본 폭 0 + 남는 공간 똑같이 나눔)
        var bg = Img(card, null, new Color(0.078f, 0.078f, 0.078f, 0.75f));
        bg.raycastTarget = true;
        card.gameObject.AddComponent<Button>().targetGraphic = bg;   // 카드를 누르면 선택 (연결은 나중에)
        VLayout(card, 16, 16, 16, 18, 10, TextAnchor.UpperCenter);

        LE(Img(Item("GradeBar", card), null, gradeColor).rectTransform, -1, 6, 1);

        var slot = Item("IconSlot", card);
        LE(slot, 116, 116);
        Img(slot, slotSprite, Color.white).type = Image.Type.Sliced;
        Border(slot, gradeColor, 2, "GradeRing");
        var icon = Img(Rect("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96)), null, new Color(1, 1, 1, 0));
        icon.preserveAspect = true;
        Txt(Fill(Child("IconPlaceholder", slot)), "[아이콘]", 14, Hex("4A4A4A"), titleFont, TextAlignmentOptions.Center);

        var badge = Item("GradeBadge", card);
        LE(badge, 110, 30);
        Border(badge, gradeColor, 1);
        Txt(Fill(Child("Label", badge)), grade, 14, gradeColor, titleFont, TextAlignmentOptions.Center);

        LE(Txt(Item("ItemName", card), name, 22, TextMain, titleFont, TextAlignmentOptions.Center).rectTransform, -1, 60, 1);
        LE(Txt(Item("Description", card), desc, 15, TextSub, bodyFont, TextAlignmentOptions.Top).rectTransform, -1, 120, 1);
        LE(Item("Spacer", card), -1, -1, 1, 1);

        // 가격 줄 (가로 정렬, 가운데)
        var price = Item("PriceRow", card);
        LE(price, -1, 34, 1);
        HLayout(price, 0, 0, 0, 0, 8, TextAnchor.MiddleCenter);
        var c = Img(Item("CoinIcon", price), coinSprite, Color.white);
        c.preserveAspect = true;
        LE(c.rectTransform, 24, 24);
        LE(Txt(Item("Price", price), "[가격]", 21, TextMain, titleFont, TextAlignmentOptions.MidlineLeft).rectTransform, 110, 34);

        LE(Btn(Item("BuyButton", card), "구매", true, 18).GetComponent<RectTransform>(), -1, 52, 1);

        var sel = Border(card, Gold, 2, "SelectFrame");
        sel.SetActive(selected);
    }

    // ───────────────────────── 파생 프리팹 ─────────────────────────
    static void MakeVariant(GameObject basePrefab, string name, Sprite illust, string npc, string line, bool isSkill)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        inst.name = name;
        var t = inst.transform;

        var ill = t.Find("Illustration").GetComponent<Image>();
        ill.sprite = illust;
        ill.color = illust != null ? Color.white : new Color(1, 1, 1, 0.06f);   // 그림이 없으면 자리만 흐리게 표시
        SetText(t, "DialogueBox/NpcName", npc);
        SetText(t, "DialogueBox/DialogueText", line);

        if (isSkill)
        {
            SetText(t, "Window/DetailStrip/Col_Selected/DetailLabel", "선택한 스킬");
            SetText(t, "Window/DetailStrip/Col_Selected/DetailName", "[스킬 이름]");
            SetText(t, "Window/DetailStrip/Col_Selected/DetailFlavor", "[스킬 설명]");
            SetText(t, "Window/DetailStrip/Col_Effects/DetailEffects", "<color=#C9C9C9>재사용 [초] · 피해 [수치]\n[효과 상세]</color>");
            SetText(t, "Window/DetailStrip/Col_Stats/StatLabel", "바뀌는 스킬 칸");
            SetText(t, "Window/DetailStrip/Col_Stats/DetailStats", "[바꿀 칸 선택]");
            SetText(t, "Window/BottomRow/SellButton/Label", "가진 스킬 팔기");
            for (int i = 0; i < 4; i++)
            {
                SetText(t, $"Window/Cards/Card_{i}/ItemName", "[스킬 이름]");
                SetText(t, $"Window/Cards/Card_{i}/Description", "[스킬 설명 두 줄]");
            }
        }

        PrefabUtility.SaveAsPrefabAsset(inst, Folder + "/" + name + ".prefab");
        Object.DestroyImmediate(inst);
    }

    static void SetText(Transform root, string path, string value)
    {
        var tr = root.Find(path);
        if (tr == null) { Debug.LogWarning("[상점 프리팹] 찾지 못함: " + path); return; }
        var tmp = tr.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = value;
    }

    // ───────────────────────── 도우미 ─────────────────────────
    static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    // 정렬 묶음 안에 들어갈 오브젝트 (위치·크기는 부모의 정렬이 정함)
    static RectTransform Item(string name, Transform parent)
        => Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));

    static RectTransform Child(string name, Transform parent)
        => Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

    static RectTransform Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    static VerticalLayoutGroup VLayout(RectTransform rt, int l, int r, int t, int b, float spacing, TextAnchor align)
        => Setup(rt.gameObject.AddComponent<VerticalLayoutGroup>(), l, r, t, b, spacing, align);

    static HorizontalLayoutGroup HLayout(RectTransform rt, int l, int r, int t, int b, float spacing, TextAnchor align)
        => Setup(rt.gameObject.AddComponent<HorizontalLayoutGroup>(), l, r, t, b, spacing, align);

    static T Setup<T>(T g, int l, int r, int t, int b, float spacing, TextAnchor align) where T : HorizontalOrVerticalLayoutGroup
    {
        g.padding = new RectOffset(l, r, t, b);
        g.spacing = spacing;
        g.childAlignment = align;
        g.childControlWidth = true;
        g.childControlHeight = true;
        g.childForceExpandWidth = false;
        g.childForceExpandHeight = false;
        return g;
    }

    // 크기 지정: 고정 폭·높이(-1이면 지정 안 함), 남는 공간 나눠 갖기(flex)
    static RectTransform LE(RectTransform rt, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
    {
        var e = rt.GetComponent<LayoutElement>();
        if (e == null) e = rt.gameObject.AddComponent<LayoutElement>();
        if (w >= 0) e.preferredWidth = w;
        if (h >= 0) e.preferredHeight = h;
        if (flexW >= 0) e.flexibleWidth = flexW;
        if (flexH >= 0) e.flexibleHeight = flexH;
        return rt;
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Txt(RectTransform rt, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // 테두리 (선 4개) — 부모의 정렬에서 빠짐
    static GameObject Border(RectTransform parent, Color color, float thickness, string name = "Border")
    {
        var holder = Fill(Child(name, parent));
        holder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Img(Rect("Top", holder, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, thickness)), null, color);
        Img(Rect("Bottom", holder, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, thickness)), null, color);
        Img(Rect("Left", holder, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(thickness, 0)), null, color);
        Img(Rect("Right", holder, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(thickness, 0)), null, color);
        return holder.gameObject;
    }

    // 버튼: 주 버튼 = 금색 바탕 + 어두운 글자, 보조 버튼 = 테두리만
    static Button Btn(RectTransform rt, string label, bool primary, float fontSize)
    {
        var img = Img(rt, null, primary ? Gold : new Color(0, 0, 0, 0.01f));
        img.raycastTarget = true;
        if (!primary) Border(rt, Hex("5A5A5A"), 1);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        Txt(Fill(Child("Label", rt)), label, fontSize, primary ? Dark : TextMain, primary ? titleFont : bodyFont, TextAlignmentOptions.Center);
        return b;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
