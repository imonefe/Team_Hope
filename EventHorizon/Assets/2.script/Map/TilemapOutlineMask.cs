using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// [2026-11-18 추가] 타일 테두리 회로 빛용 "칸 지도"
/// - 이 타일맵에서 타일이 있는 칸을 작은 흑백 그림(1칸 = 1픽셀)으로 만들어 렌더러에 넣어 준다
/// - TileDataBlue 셰이더가 이 지도를 보고 "빈 칸과 맞닿은 가장자리"에만 회로 빛을 그린다
/// - 타일을 칠하거나 지우면 (에디터·게임 모두) 자동으로 다시 만든다
/// - 재질은 건드리지 않음 (렌더러 개별 값으로만 넣음) → 같은 재질을 여러 타일맵이 같이 써도 됨
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public class TilemapOutlineMask : MonoBehaviour
{
    static readonly int MaskId = Shader.PropertyToID("_CellMask");
    static readonly int RectId = Shader.PropertyToID("_CellMaskRect");
    static readonly int SizeId = Shader.PropertyToID("_CellMaskSize");

    Tilemap tilemap;
    TilemapRenderer rend;
    Texture2D mask;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        tilemap = GetComponent<Tilemap>();
        rend = GetComponent<TilemapRenderer>();
        Tilemap.tilemapTileChanged += OnTilesChanged;
        Rebuild();
    }

    void OnDisable()
    {
        Tilemap.tilemapTileChanged -= OnTilesChanged;
        if (rend != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetVector(SizeId, Vector4.zero);   // 테두리 끔
            rend.SetPropertyBlock(block);
        }
        if (mask != null)
        {
            if (Application.isPlaying) Destroy(mask); else DestroyImmediate(mask);
            mask = null;
        }
    }

    void OnTilesChanged(Tilemap tm, Tilemap.SyncTile[] tiles)
    {
        if (tm == tilemap) Rebuild();
    }

    /// <summary>칸 지도를 다시 만들어 렌더러에 넣는다</summary>
    public void Rebuild()
    {
        if (tilemap == null || rend == null) return;
        BoundsInt b = tilemap.cellBounds;
        int w = Mathf.Max(1, b.size.x + 2), h = Mathf.Max(1, b.size.y + 2);   // 둘레 한 칸씩 빈칸 여유

        if (mask == null || mask.width != w || mask.height != h)
        {
            if (mask != null) { if (Application.isPlaying) Destroy(mask); else DestroyImmediate(mask); }
            mask = new Texture2D(w, h, TextureFormat.R8, false, true)
            {
                name = name + "_CellMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }

        var px = new byte[w * h];
        for (int y = 0; y < b.size.y; y++)
            for (int x = 0; x < b.size.x; x++)
                if (tilemap.HasTile(new Vector3Int(b.xMin + x, b.yMin + y, b.zMin)))
                    px[(y + 1) * w + (x + 1)] = 255;
        mask.LoadRawTextureData(px);
        mask.Apply(false, false);

        Vector3 origin = tilemap.CellToWorld(new Vector3Int(b.xMin - 1, b.yMin - 1, b.zMin));
        Vector3 cell = tilemap.CellToWorld(new Vector3Int(b.xMin, b.yMin, b.zMin)) - origin;   // 칸 하나 크기 (월드, 크기 조절 반영)

        if (block == null) block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetTexture(MaskId, mask);
        block.SetVector(RectId, new Vector4(origin.x, origin.y, w * cell.x, h * cell.y));
        block.SetVector(SizeId, new Vector4(w, h, 0, 0));
        rend.SetPropertyBlock(block);
    }
}
