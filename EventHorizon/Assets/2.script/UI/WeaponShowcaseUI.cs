using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [2026-11-18 추가] 인벤토리 능력치 판의 '현재 무기' 칸 (시안 C4).
///
/// - 무기를 바꾸면 그림·이름·속성 글자가 바뀐다.
/// - 무기 그림은 모양이 제각각이라, 무기마다 '각도'만 정해 두면 크기는 칸에 꽉 차게 자동으로 맞춘다.
///   [2026-11-18 수정] 그림 네모 상자 기준으로 맞추면 대각선 무기(망치·건틀릿)가 너무 작아져서,
///   그림의 '실제로 칠해진 부분 외곽선'을 돌려 본 크기로 맞추고, 칠해진 부분이 칸 가운데 오게 옮긴다.
///   외곽선은 편집기에서 그림을 넣을 때 자동으로 읽어 저장한다 (shape — 인스펙터에는 안 보임).
/// - '자동 각도'를 켜면 칸에 가장 크게 들어가는 각도도 알아서 고른다 (기준 각도 ±90° 안).
/// - 자동 크기가 마음에 안 들면 무기별 '크기 배율'·'위치 보정'으로 미세 조정한다.
/// - 실행하지 않아도 인스펙터의 '미리보기 무기'를 바꾸면 편집기 화면에서 바로 맞춰진 모습을 볼 수 있다.
/// - 값은 PlayerController 에서 읽기만 한다 (공격 코드는 건드리지 않음).
/// </summary>
public class WeaponShowcaseUI : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Weapontype type = Weapontype.Sword;
        public Sprite icon;
        public string displayName = "검";
        [Tooltip("그림 회전 (도). 양수 = 시계 반대 방향.\n'자동 각도'를 켜면 이 값 ±90° 안에서 가장 크게 보이는 각도를 찾는다 (이 값은 무기가 향할 방향 기준)")]
        public float angle = 90f;
        [Tooltip("[2026-11-18] 켜면 칸에 가장 크게 들어가는 각도를 자동으로 고른다. 끄면 위 각도 그대로")]
        public bool autoAngle = true;
        [Tooltip("자동으로 맞춘 크기에 곱하는 값 (1 = 칸에 꽉 맞춤)")]
        public float scale = 1f;
        [Tooltip("칸 가운데에서 그림을 옮기는 값 (px)")]
        public Vector2 offset = Vector2.zero;

        [HideInInspector] public Vector2[] shape;   // 칠해진 부분 외곽선 (그림 가운데 기준 px, 위쪽 +)
        [HideInInspector] public Sprite shapeFrom;  // 외곽선을 읽은 그림 (그림을 바꾸면 다시 읽음)
    }

    [Header("연결")]
    [Tooltip("무기 그림이 들어갈 영역 (칸 안쪽). 여기 크기를 기준으로 맞춘다")]
    public RectTransform area;
    public Image weaponImage;
    public TMP_Text nameText;
    public TMP_Text attributeText;

    [Header("맞춤")]
    [Tooltip("영역 가장자리와 그림 사이 여백 (px)")]
    public float padding = 6f;
    public Entry[] entries =
    {
        new Entry { type = Weapontype.Sword,  displayName = "검",     angle = 90f },
        new Entry { type = Weapontype.Hammer, displayName = "망치",   angle = 68f, offset = new Vector2(0f, 6f) },
        new Entry { type = Weapontype.Glove,  displayName = "건틀릿", angle = 55f },
    };

    [Header("속성 이름 · 색 (WeaponStripUI 와 같은 값)")]
    public string slashName = "참격";
    public string pierceName = "관통";
    public string bluntName = "타격";
    public Color slashColor = new Color(0.498f, 0.827f, 0.839f, 1f);
    public Color pierceColor = new Color(0.624f, 0.706f, 1f, 1f);
    public Color bluntColor = new Color(0.937f, 0.478f, 0.384f, 1f);
    public Color noneColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("편집기 미리보기 (실행 중에는 무시)")]
    public Weapontype previewWeapon = Weapontype.Sword;

    PlayerController player;

    void Start()
    {
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterChanged += SetPlayer;
            SetPlayer(CharacterManager.Instance.CurrentCharacter);
        }
        else
        {
            SetPlayer(PlayerController.Instance != null ? PlayerController.Instance.gameObject : null);
        }
    }

    // 인벤토리 창을 열 때마다 최신 무기로 (창이 닫혀 있는 동안 바뀌었을 수 있음)
    void OnEnable()
    {
        if (player != null) Apply(player.weapontype);
    }

    void OnDestroy()
    {
        if (player != null) player.OnWeaponChanged -= Apply;
        if (CharacterManager.Instance != null) CharacterManager.Instance.OnCharacterChanged -= SetPlayer;
    }

    void SetPlayer(GameObject obj)
    {
        if (player != null) player.OnWeaponChanged -= Apply;
        player = obj != null ? obj.GetComponent<PlayerController>() : null;
        if (player != null)
        {
            player.OnWeaponChanged += Apply;
            Apply(player.weapontype);
        }
    }

    void Apply(Weapontype type)
    {
        NowAdvantage adv = player != null && player.EquippedWeapon != null ? player.EquippedWeapon.advantage : DefaultAdvantage(type);
        Show(type, adv);
    }

    /// <summary>무기 그림·이름·속성을 칸에 맞춰 표시</summary>
    public void Show(Weapontype type, NowAdvantage adv)
    {
        Entry e = Find(type);

        if (nameText != null) nameText.text = e != null ? e.displayName : "";
        if (attributeText != null)
        {
            attributeText.text = AdvName(adv);
            attributeText.color = AdvColor(adv);
        }

        if (weaponImage == null) return;
        bool has = e != null && e.icon != null;
        weaponImage.enabled = has;
        if (!has) return;

        weaponImage.sprite = e.icon;
        Fit(e);
    }

    // 돌린 그림(칠해진 부분)이 영역 안에 들어가는 가장 큰 배율로 맞추고 가운데로 옮긴다
    void Fit(Entry e)
    {
        RectTransform img = weaponImage.rectTransform;
        RectTransform box = area != null ? area : (RectTransform)img.parent;
        Vector2 room = box.rect.size - Vector2.one * (padding * 2f);

        // 각도 고르기: 자동이면 기준 각도 ±90° 안에서 1° 간격으로 가장 크게 들어가는 각도
        float angle = e.angle;
        if (e.autoAngle && HasShape(e) && room.x > 0f && room.y > 0f)
        {
            float bestK = -1f;
            for (int d = -90; d < 90; d++)
            {
                float a = e.angle + d;
                float k = FitScale(e, a, room, out _);
                if (k > bestK + 1e-5f) { bestK = k; angle = a; }
            }
        }

        float scale = FitScale(e, angle, room, out Vector2 mid);
        if (scale <= 0f) scale = 0.01f;   // 영역 크기가 아직 0일 때 대비
        scale *= e.scale;

        img.anchorMin = img.anchorMax = img.pivot = new Vector2(0.5f, 0.5f);
        img.sizeDelta = e.icon.rect.size;
        img.localEulerAngles = new Vector3(0f, 0f, angle);
        img.localScale = Vector3.one * scale;
        img.anchoredPosition = e.offset - mid * scale;   // 칠해진 부분의 가운데를 칸 가운데로
    }

    static bool HasShape(Entry e)
    {
        return e.shape != null && e.shape.Length >= 3 && e.shapeFrom == e.icon;
    }

    // angle 로 돌렸을 때 room 에 들어가는 배율. mid = 돌린 범위의 가운데 (그림 가운데 기준 px)
    static float FitScale(Entry e, float angle, Vector2 room, out Vector2 mid)
    {
        Vector2 native = e.icon.rect.size;
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        bool useShape = HasShape(e);   // 외곽선이 없으면 그림 네모 상자로 계산
        int n = useShape ? e.shape.Length : 4;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = useShape ? e.shape[i]
                : new Vector2((i & 1) == 0 ? -native.x * 0.5f : native.x * 0.5f, (i & 2) == 0 ? -native.y * 0.5f : native.y * 0.5f);
            Vector2 r = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            min = Vector2.Min(min, r);
            max = Vector2.Max(max, r);
        }
        Vector2 ext = max - min;
        mid = (min + max) * 0.5f;
        return Mathf.Min(room.x / Mathf.Max(1f, ext.x), room.y / Mathf.Max(1f, ext.y));
    }

    Entry Find(Weapontype type)
    {
        if (entries == null) return null;
        foreach (var e in entries) if (e != null && e.type == type) return e;
        return null;
    }

    static NowAdvantage DefaultAdvantage(Weapontype t)
    {
        switch (t)
        {
            case Weapontype.Sword: return NowAdvantage.Slash;
            case Weapontype.Hammer:
            case Weapontype.Glove: return NowAdvantage.Blunt;
            default: return NowAdvantage.None;
        }
    }

    string AdvName(NowAdvantage a)
    {
        switch (a)
        {
            case NowAdvantage.Slash: return slashName;
            case NowAdvantage.Pierce: return pierceName;
            case NowAdvantage.Blunt: return bluntName;
            default: return "";
        }
    }

    Color AdvColor(NowAdvantage a)
    {
        switch (a)
        {
            case NowAdvantage.Slash: return slashColor;
            case NowAdvantage.Pierce: return pierceColor;
            case NowAdvantage.Blunt: return bluntColor;
            default: return noneColor;
        }
    }

#if UNITY_EDITOR
    // 인스펙터 값을 바꾸면 편집기 화면에서 바로 맞춰 보이게 (실행 중이 아닐 때만)
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (RefreshShapes()) UnityEditor.EditorUtility.SetDirty(this);
            Show(previewWeapon, DefaultAdvantage(previewWeapon));
        };
    }

    [ContextMenu("미리보기 다시 맞추기")]
    void PreviewNow()
    {
        Show(previewWeapon, DefaultAdvantage(previewWeapon));
    }

    [ContextMenu("무기 외곽선 다시 읽기")]
    void RereadShapes()
    {
        if (entries != null) foreach (var e in entries) if (e != null) e.shapeFrom = null;
        if (RefreshShapes()) UnityEditor.EditorUtility.SetDirty(this);
        PreviewNow();
    }

    // 그림이 바뀐 무기만 외곽선을 다시 읽는다. 바뀐 게 있으면 true
    bool RefreshShapes()
    {
        bool changed = false;
        if (entries == null) return false;
        foreach (var e in entries)
        {
            if (e == null || e.icon == null || e.shapeFrom == e.icon) continue;
            Vector2[] hull = ReadShape(e.icon);
            if (hull == null) continue;
            e.shape = hull;
            e.shapeFrom = e.icon;
            changed = true;
        }
        return changed;
    }

    // 그림 파일을 직접 읽어 칠해진 픽셀(투명도 > alphaCut)의 볼록 외곽선을 구한다.
    // (텍스처 '읽기/쓰기' 설정을 켜지 않아도 되도록 원본 파일을 읽음)
    static Vector2[] ReadShape(Sprite sp, byte alphaCut = 40, int step = 2)
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(sp.texture);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;

        var tex = new Texture2D(2, 2);
        try
        {
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) return null;
            float f = tex.width / (float)sp.texture.width;   // 원본 파일과 가져온 텍스처 크기가 다를 때 대비
            Rect r = sp.rect;
            int x0 = Mathf.FloorToInt(r.xMin * f), x1 = Mathf.CeilToInt(r.xMax * f);
            int y0 = Mathf.FloorToInt(r.yMin * f), y1 = Mathf.CeilToInt(r.yMax * f);
            Color32[] px = tex.GetPixels32();
            int w = tex.width;

            // 줄마다 가장 왼쪽·오른쪽 칠해진 점만 모으면 외곽선 계산에 충분하다
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int y = y0; y < y1; y += step)
            {
                int left = -1, right = -1;
                for (int x = x0; x < x1; x++)
                    if (px[y * w + x].a > alphaCut) { if (left < 0) left = x; right = x; }
                if (left < 0) continue;
                pts.Add(new Vector2(left / f - r.center.x, y / f - r.center.y));
                pts.Add(new Vector2((right + 1) / f - r.center.x, y / f - r.center.y));
            }
            return pts.Count >= 3 ? ConvexHull(pts) : null;
        }
        finally
        {
            DestroyImmediate(tex);
        }
    }

    // 볼록 외곽선 (모노톤 체인)
    static Vector2[] ConvexHull(System.Collections.Generic.List<Vector2> p)
    {
        p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var h = new Vector2[p.Count * 2];
        int k = 0;
        for (int i = 0; i < p.Count; i++)
        {
            while (k >= 2 && Cross(h[k - 2], h[k - 1], p[i]) <= 0f) k--;
            h[k++] = p[i];
        }
        for (int i = p.Count - 2, t = k + 1; i >= 0; i--)
        {
            while (k >= t && Cross(h[k - 2], h[k - 1], p[i]) <= 0f) k--;
            h[k++] = p[i];
        }
        System.Array.Resize(ref h, Mathf.Max(0, k - 1));
        return h;
    }

    static float Cross(Vector2 o, Vector2 a, Vector2 b)
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
#endif
}
