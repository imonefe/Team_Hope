#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

/// <summary>
/// [2026-11-17 추가] 대화창·상점 캔버스의 캐릭터 정지 그림 칸에 스파인 대기 동작을 겹쳐 놓는다. (한 번 실행해 프리팹에 저장)
///   - 칸의 그림(Image.sprite)이 어떤 캐릭터인지 보고 같은 캐릭터의 스파인을 같은 자리·같은 크기로 붙인다.
///   - 원래 Image 는 지우지 않고 끄기만 한다 (되돌릴 수 있게). 색은 SpinePortraitTint 가 따라간다.
///   - 이미 붙어 있으면 다시 맞춰 준다 (변형 프리팹에서 그림이 바뀐 칸은 캐릭터도 바꿈).
///   - 상점 아이템·스킬 캔버스는 지금 그림 칸이 비어 있어서 남주·백이연 그림을 넣어 준다.
/// 기준: 기존 일러스트 png 와 스파인 원본 포토샵 파일의 크기가 같다 (여주만 좌우 반전).
/// </summary>
public static class DialoguePortraitSpineSetup
{
    class Entry
    {
        public string sprite, data;   // 일러스트 파일 이름, 스파인 폴더·파일 이름
        public float w, h, ox, oy;    // 원본 크기와 스파인 원점(그림 좌표, 왼쪽 위 기준)
        public bool flip;             // 일러스트가 원본과 좌우 반전인지
    }

    static readonly Entry[] Map =
    {
        new Entry { sprite = "Girl",    data = "heroine_idle", w = 3184, h = 5428, ox = 1650, oy = 5400, flip = true },
        new Entry { sprite = "boy",     data = "hero_idle",    w = 1948, h = 2806, ox = 900,  oy = 2730 },
        new Entry { sprite = "npc01",   data = "baek_idle",    w = 3330, h = 5150, ox = 1665, oy = 5050 },
        new Entry { sprite = "2s_boos", data = "boss_idle",    w = 2870, h = 3811, ox = 1435, oy = 3700 },
    };

    const string SpineDir = "Assets/4. Art/Illustration/Spine/";
    const string IlluDir  = "Assets/4. Art/Illustration/";
    const string ChildName = "SpinePortrait";

    // 기본 프리팹을 먼저, 그 변형을 뒤에 (변형은 기본에서 물려받은 것을 고친다)
    static readonly string[] Prefabs =
    {
        "Assets/1.Prefab/UI/Canvas/대화/대화창_기본.prefab",
        "Assets/1.Prefab/UI/Canvas/대화/엔딩_대화.prefab",
        "Assets/1.Prefab/UI/Canvas/koke Canvas.prefab",
        "Assets/1.Prefab/UI/Canvas/상점/ItemShopCanvas.prefab",
        "Assets/1.Prefab/UI/Canvas/상점/SkillShopCanvas.prefab",
    };

    // 상점 캔버스의 Illustration 칸에 넣을 그림 (지금은 고정 — 기획서 "대화 장면")
    static readonly Dictionary<string, string> ShopIllustration = new Dictionary<string, string>
    {
        { "ItemShopCanvas", "boy" },     // 남주
        { "SkillShopCanvas", "npc01" },  // 백이연
    };

    [MenuItem("EventHorizon/대화 초상 스파인 배치")]
    public static void Run()
    {
        // 화면용 일반 투명도 재질 (아틀라스를 일반 투명도로 묶었기 때문)
        string matGuid = AssetDatabase.FindAssets("SkeletonGraphicDefault-Straight t:Material").FirstOrDefault();
        Material mat = matGuid != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuid)) : null;
        if (mat == null) { Debug.LogError("[대화 초상] SkeletonGraphicDefault-Straight 재질을 찾지 못함"); return; }

        // 아틀라스 그림 최대 크기 4096 (기본 2048 이면 반으로 줄어듦), 뼈대 배율 0.01 (화면 1픽셀 = 원본 1픽셀 기준)
        foreach (var e in Map)
        {
            var ti = AssetImporter.GetAtPath(SpineDir + e.data + "/" + e.data + ".png") as TextureImporter;
            if (ti != null && ti.maxTextureSize < 4096) { ti.maxTextureSize = 4096; ti.SaveAndReimport(); }
            var d = Data(e);
            if (d == null) { Debug.LogError("[대화 초상] 뼈대 자료 없음: " + e.data); return; }
            if (!Mathf.Approximately(d.scale, 0.01f)) { d.scale = 0.01f; d.Clear(); EditorUtility.SetDirty(d); }
        }
        AssetDatabase.SaveAssets();

        int made = 0, fixedCount = 0;
        foreach (string path in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[대화 초상] 프리팹 없음: " + path); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var targets = new List<Image>();
                foreach (var npc in root.GetComponentsInChildren<NPC>(true))
                    if (npc.portraits != null) targets.AddRange(npc.portraits.Where(p => p != null));

                if (ShopIllustration.TryGetValue(name, out string spriteName))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IlluDir + spriteName + ".png");
                    foreach (var img in root.GetComponentsInChildren<Image>(true).Where(i => i.name == "Illustration"))
                    {
                        img.sprite = sprite;
                        img.preserveAspect = true;
                        targets.Add(img);
                    }
                }

                foreach (Image img in targets.Distinct())
                {
                    Entry e = img.sprite != null ? Map.FirstOrDefault(m => m.sprite == img.sprite.name) : null;
                    Transform child = img.transform.Find(ChildName);
                    if (e == null)
                    {   // 스파인이 없는 캐릭터(영복 등) → 정지 그림 그대로
                        if (child != null) child.gameObject.SetActive(false);
                        img.enabled = true;
                        continue;
                    }

                    SkeletonDataAsset data = Data(e);
                    SkeletonGraphic sg;
                    SkeletonAnimation anim;
                    if (child == null)
                    {
                        var comps = SkeletonGraphic.NewSkeletonGraphicGameObject(data, img.transform, mat);
                        sg = comps.skeletonRenderer;
                        anim = comps.skeletonAnimation;
                        sg.gameObject.name = ChildName;
                        made++;
                    }
                    else
                    {
                        child.gameObject.SetActive(true);
                        sg = child.GetComponent<SkeletonGraphic>();
                        anim = child.GetComponent<SkeletonAnimation>();
                        if (sg.skeletonDataAsset != data) { sg.skeletonDataAsset = data; sg.Initialize(true); }
                        sg.material = mat;
                        fixedCount++;
                    }
                    if (anim != null) { anim.AnimationName = "idle"; anim.loop = true; anim.unscaledTime = true; }   // 상점·대화 중 게임이 멈춰도(시간 배율 0) 계속 움직이게
                    sg.raycastTarget = false;

                    Place(sg, img, e, data);

                    var tint = sg.GetComponent<SpinePortraitTint>();
                    if (tint == null) tint = sg.gameObject.AddComponent<SpinePortraitTint>();
                    tint.source = img;
                    sg.color = img.color;
                    img.enabled = false;   // 원래 그림은 끄기만 (Npc.cs 가 색을 바꾸는 대상은 그대로 남음)
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"[대화 초상] 완료 — 새로 붙임 {made}, 다시 맞춤 {fixedCount}");
    }

    static SkeletonDataAsset Data(Entry e) =>
        AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SpineDir + e.data + "/" + e.data + "_SkeletonData.asset");

    /// <summary>원래 그림이 보이던 자리·크기와 똑같이 겹치도록 위치·배율을 정한다.</summary>
    static void Place(SkeletonGraphic sg, Image img, Entry e, SkeletonDataAsset data)
    {
        Rect r = img.rectTransform.rect;
        float sx = r.width / e.w, sy = r.height / e.h, offX = 0f, offY = 0f;
        if (img.preserveAspect)
        {
            float s = Mathf.Min(sx, sy);
            offX = (r.width - e.w * s) * 0.5f;
            offY = (r.height - e.h * s) * 0.5f;
            sx = sy = s;
        }
        Canvas canvas = img.GetComponentInParent<Canvas>(true);
        float ppu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        float unit = data.scale * ppu;            // 원본 1픽셀이 화면 단위 몇 칸인지 (배율 1일 때)

        float ox = e.flip ? e.w - e.ox : e.ox;    // 반전된 그림이면 원점도 반전
        float oyFromBottom = e.h - e.oy;

        var rt = sg.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // 원래 그림 칸의 왼쪽 아래 기준
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 10f);
        rt.anchoredPosition = new Vector2(offX + ox * sx, offY + oyFromBottom * sy);
        rt.localRotation = Quaternion.identity;
        rt.localScale = new Vector3(sx / unit * (e.flip ? -1f : 1f), sy / unit, 1f);
        rt.SetAsLastSibling();
    }
}
#endif
