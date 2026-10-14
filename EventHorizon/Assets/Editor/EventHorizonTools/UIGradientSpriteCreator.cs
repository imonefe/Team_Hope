using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 왼쪽은 투명, 오른쪽으로 갈수록 진해지는 그라데이션 이미지를 만들어 프로젝트에 저장한다.
/// 메뉴: Tools > Event Horizon > UI 그라데이션 스프라이트 생성
/// </summary>
public static class UIGradientSpriteCreator
{
    const string SavePath = "Assets/4. Art/UI/ui_gradient_right.png";

    [MenuItem("Tools/Event Horizon/UI 그라데이션 스프라이트 생성")]
    public static void Create()
    {
        const int width = 256;
        const int height = 4;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);      // 0 (왼쪽) → 1 (오른쪽)
            float alpha = Mathf.SmoothStep(0f, 1f, t);   // 부드럽게 진해지는 곡선
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        File.WriteAllBytes(SavePath, png);
        AssetDatabase.ImportAsset(SavePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SavePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        // 압축하면 줄무늬가 생기므로 끈다
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Object asset = AssetDatabase.LoadAssetAtPath<Object>(SavePath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log("그라데이션 이미지를 만들었습니다: " + SavePath);
    }
}
