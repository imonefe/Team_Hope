using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-25 추가] GEEKS 2026 특전 창 프리팹을 만든다 — 에디터 전용.
///  만드는 것: 1.Prefab/UI/Canvas/특전/Resources/GeeksPerkCanvas.prefab
///    어둡게 덮개 · 패널(금색 테두리) · 제목 "GEEKS 2026 특전" · 설명 · 카드 5장(등급색 테두리 · 아이콘 · 이름 · 등급 · 키워드) · 닫기 안내
///  만든 뒤에는 프리팹을 직접 열어 위치 · 크기 · 글자를 고쳐도 된다 (GeeksPerkWindow 는 연결된 칸만 씀).
///  유니티가 켜질 때 한 번 자동 실행. 다시 만들기: 메뉴 "EventHorizon/GEEKS 2026 특전 창 만들기" (직접 고친 값은 덮어씀)
/// </summary>
public static class GeeksPerkCanvasBuilder
{
    const string AutoKey = "EH_GeeksPerkCanvasBuilder_v1";
    const string Dir = "Assets/1.Prefab/UI/Canvas/특전/Resources/";
    const string PrefabPath = Dir + "GeeksPerkCanvas.prefab";
    const string FontPath = "Assets/Etc/Font/KoPubWorld Dotum_Pro Medium SDF.asset";

    static readonly Color Gold = new Color(0.89f, 0.76f, 0.49f, 1f);      // #E3C27E (상점 금색)
    static readonly Color Muted = new Color(0.80f, 0.76f, 0.70f, 1f);
    static readonly Color PanelBg = new Color(0.08f, 0.07f, 0.06f, 0.97f);
    static readonly Color CardBg = new Color(0.13f, 0.12f, 0.11f, 1f);

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetBool(AutoKey, true);
            Build();
        };
    }

    [MenuItem("EventHorizon/GEEKS 2026 특전 창 만들기")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError("[GEEKS 특전] 글꼴이 없습니다: " + FontPath); return; }
        Directory.CreateDirectory(Dir);

        var root = new GameObject("GeeksPerkCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(GeeksPerkWindow));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;   // 컷신 덮개(550) · 게임오버(600) 아래, 메뉴(500) 위
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var w = root.GetComponent<GeeksPerkWindow>();
        w.group = root.GetComponent<CanvasGroup>();

        var dim = Img("Dim", root.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(dim.rectTransform);

        var panel = Img("Panel", root.transform, PanelBg);
        var prt = panel.rectTransform;
        prt.sizeDelta = new Vector2(1360f, 640f);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold; outline.effectDistance = new Vector2(2f, -2f);
        w.panel = prt;

        Text("Title", prt, font, "GEEKS 2026 특전", 48, Gold, new Vector2(0f, 250f), new Vector2(1200f, 70f));
        var line = Img("TitleLine", prt, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        line.rectTransform.sizeDelta = new Vector2(1240f, 2f);
        line.rectTransform.anchoredPosition = new Vector2(0f, 205f);
        Text("Subtitle", prt, font, "전시 기념 선물 — 사이버웨어 5개가 바로 장착됩니다", 24, Muted, new Vector2(0f, 172f), new Vector2(1200f, 40f));

        const float cardW = 230f, cardH = 300f, gap = 24f;
        for (int i = 0; i < GeeksPerkWindow.Count; i++)
        {
            float x = (i - (GeeksPerkWindow.Count - 1) * 0.5f) * (cardW + gap);
            var frame = Img("Card" + i, prt, ShopColorsTier());
            var rt = frame.rectTransform;
            rt.sizeDelta = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(x, -15f);
            rt.gameObject.AddComponent<CanvasGroup>();
            var inner = Img("Inner", rt, CardBg);
            inner.raycastTarget = false;
            Stretch(inner.rectTransform); inner.rectTransform.offsetMin = new Vector2(3f, 3f); inner.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var icon = Img("Icon", rt, Color.white);
            icon.rectTransform.sizeDelta = new Vector2(130f, 130f);
            icon.rectTransform.anchoredPosition = new Vector2(0f, 55f);
            icon.preserveAspect = true;
            var name = Text("Name", rt, font, "사이버웨어 이름", 22, Color.white, new Vector2(0f, -45f), new Vector2(cardW - 24f, 56f));
            var tier = Text("Tier", rt, font, "일반", 18, Muted, new Vector2(0f, -88f), new Vector2(cardW - 24f, 28f));
            var kw = Text("Keywords", rt, font, "키워드", 17, Muted, new Vector2(0f, -120f), new Vector2(cardW - 24f, 28f));
            w.cards[i] = new GeeksPerkWindow.Card { root = rt, frame = frame, icon = icon, name = name, tier = tier, keywords = kw };
        }

        Text("Hint", prt, font, "카드에 마우스를 올리면 설명   ·   Space · 클릭  확인", 20, Muted, new Vector2(0f, -268f), new Vector2(1200f, 34f));

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[GEEKS 특전] 창 프리팹을 만들었습니다: " + PrefabPath);
    }

    static Color ShopColorsTier() => new Color(0.71f, 0.68f, 0.64f, 1f);   // 일반 등급색 (실행 중 등급에 맞춰 바뀜)

    static Image Img(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string s, float size, Color c, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font; t.text = s; t.fontSize = size; t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        var rt = t.rectTransform; rt.sizeDelta = box; rt.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
