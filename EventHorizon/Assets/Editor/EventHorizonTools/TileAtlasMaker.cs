#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// [2026-10-28 추가] 2스테이지 타일 그림을 스프라이트 아틀라스로 묶어 "타일 사이 틈(옆 타일이 1프레임씩 비치는 선)" 을 없앤다.
///
/// 왜 생기나: 타일 묶음 그림은 타일끼리 틈 없이 붙어 있다. 카메라가 픽셀 단위가 아닌 위치(부드럽게 따라가는 중)에 있으면
///   가끔 타일 가장자리에서 바로 옆 타일의 픽셀을 한 줄 집어 오고, 그게 한 프레임씩 선으로 보인다.
/// 해결: 아틀라스는 타일 하나하나 사이에 여백(padding)을 넣고 가장자리 픽셀을 바깥으로 늘여서 담는다 →
///   가장자리 바로 바깥을 집어도 같은 타일의 색이라 선이 생기지 않는다. 타일맵·프리팹은 바꿀 것이 없다(유니티가 알아서 아틀라스를 씀).
///
/// - 유니티가 켜지거나 스크립트가 다시 불러와질 때 아틀라스가 없으면 자동으로 한 번 만든다.
///   메뉴 Tools > Event Horizon > 2스테이지 타일 아틀라스 만들기 로 다시 만들 수 있다.
/// - 만들어지는 파일: Assets/4. Art/Environment/Stage2/Stage2Tiles.spriteatlasv2
/// - 원본 그림 설정(점 방식)은 그대로 쓴다.
/// </summary>
[InitializeOnLoad]
public static class TileAtlasMaker
{
    const string AtlasPath = "Assets/4. Art/Environment/Stage2/Stage2Tiles.spriteatlasv2";
    static readonly string[] Sheets =
    {
        "Assets/4. Art/Environment/Stage2/Props/test02 1.png",
        "Assets/4. Art/Environment/Stage2/Tiles/tile.png",
    };

    static TileAtlasMaker()
    {
        EditorApplication.delayCall += () => Make(false);
    }

    [MenuItem("Tools/Event Horizon/2스테이지 타일 아틀라스 만들기")]
    static void Menu() => Make(true);

    static void Make(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!force && AssetDatabase.LoadMainAssetAtPath(AtlasPath) != null) return;

        var asset = new SpriteAtlasAsset();
        foreach (string p in Sheets)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex != null) asset.Add(new Object[] { tex });
            else Debug.LogWarning("[타일 아틀라스] 그림을 찾지 못함: " + p);
        }
        SpriteAtlasAsset.Save(asset, AtlasPath);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
        if (importer == null)
        {
            Debug.LogWarning("[타일 아틀라스] 설정을 바꾸지 못함 (SpriteAtlasImporter 없음)");
            return;
        }

        importer.includeInBuild = true;
        importer.packingSettings = new SpriteAtlasPackingSettings
        {
            blockOffset = 1,
            padding = 8,                 // 타일 사이 여백 (가장자리 픽셀로 채워짐)
            enableRotation = false,
            enableTightPacking = false,  // 타일은 사각형 그대로
            enableAlphaDilation = true,  // 여백을 가장자리 색으로 채움
        };
        importer.textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false,
            generateMipMaps = false,
            sRGB = true,
            filterMode = FilterMode.Point,   // 원본 타일과 같은 점 방식
            anisoLevel = 0,
        };
        var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        platform.maxTextureSize = 4096;
        platform.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformSettings(platform);
        importer.SaveAndReimport();

        SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("[타일 아틀라스] 만들었습니다: " + AtlasPath);
    }
}
#endif
