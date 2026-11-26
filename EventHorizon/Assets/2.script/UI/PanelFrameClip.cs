using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-26] 창 그림(inventori.png)의 모서리 밖으로 튀어나온 선이 옆 판의 선과 겹쳐 틀이 망가져 보이는 것 막기.
///  이 오브젝트(Back, RectMask2D)가 그림(자식 Art)을 잘라낸다.
///  이웃 판이 튀어나온 선이 닿는 거리에 있으면, 그 방향은 '두 판 사이 가운데 - gapMargin' 까지만 보이게 자름.
///  이웃이 없는 쪽은 그대로. 판 크기가 바뀌어도(평소 ↔ 교체 화면) 자동으로 다시 계산 (바뀔 때만 적용).
///  메뉴 EventHorizon/인벤토리 시안 v2 적용 이 붙이고 이웃을 연결한다.
/// </summary>
[RequireComponent(typeof(RectMask2D))]
public class PanelFrameClip : MonoBehaviour
{
    [Tooltip("이 그림의 판 (선 안쪽 상자)")]
    public RectTransform box;
    [Tooltip("겹칠 수 있는 다른 판들 (꺼져 있으면 무시)")]
    public RectTransform[] neighbors;
    [Tooltip("두 판 사이 가운데에서 더 떼어 둘 거리 (px) — 같은 줄의 선이 이어져 보이지 않게")]
    public float gapMargin = 6f;

    RectMask2D mask;
    RectTransform self;
    static readonly Vector3[] c = new Vector3[4];

    void OnEnable()
    {
        mask = GetComponent<RectMask2D>();
        self = (RectTransform)transform;
        Apply();
    }

    void LateUpdate() => Apply();

    static Rect World(RectTransform r)
    {
        r.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    void Apply()
    {
        if (mask == null || box == null) return;
        Rect b = World(box), k = World(self);
        float cutL = 0f, cutR = 0f, cutB = 0f, cutT = 0f;
        float sx = Mathf.Max(0.0001f, self.lossyScale.x), sy = Mathf.Max(0.0001f, self.lossyScale.y);
        float m = gapMargin * sx;
        if (neighbors != null)
            foreach (var n in neighbors)
            {
                if (n == null || n == box || !n.gameObject.activeInHierarchy) continue;
                Rect o = World(n);
                bool vOverlap = o.yMin < k.yMax && o.yMax > k.yMin;
                bool hOverlap = o.xMin < k.xMax && o.xMax > k.xMin;
                if (vOverlap && o.xMin >= b.xMax && o.xMin < k.xMax + (k.xMax - b.xMax))   // 오른쪽 이웃
                    cutR = Mathf.Max(cutR, k.xMax - ((b.xMax + o.xMin) * 0.5f - m));
                if (vOverlap && o.xMax <= b.xMin && o.xMax > k.xMin - (b.xMin - k.xMin))   // 왼쪽 이웃
                    cutL = Mathf.Max(cutL, ((b.xMin + o.xMax) * 0.5f + m) - k.xMin);
                if (hOverlap && o.yMin >= b.yMax && o.yMin < k.yMax + (k.yMax - b.yMax))   // 위 이웃
                    cutT = Mathf.Max(cutT, k.yMax - ((b.yMax + o.yMin) * 0.5f - m));
                if (hOverlap && o.yMax <= b.yMin && o.yMax > k.yMin - (b.yMin - k.yMin))   // 아래 이웃
                    cutB = Mathf.Max(cutB, ((b.yMin + o.yMax) * 0.5f + m) - k.yMin);
            }
        var pad = new Vector4(Mathf.Max(0f, cutL / sx), Mathf.Max(0f, cutB / sy), Mathf.Max(0f, cutR / sx), Mathf.Max(0f, cutT / sy));
        if ((mask.padding - pad).sqrMagnitude > 0.01f) mask.padding = pad;
    }
}
