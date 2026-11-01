#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// [2026-10-15 추가] Etc/Font 폴더의 한글 글꼴 파일(.otf/.ttf)로 TextMeshPro 글꼴 에셋(SDF)을 만들어 준다.
///
/// - 유니티가 켜지거나 스크립트가 다시 불러와질 때, 아직 SDF 에셋이 없는 KoPubWorld 글꼴이 있으면 자동으로 만든다.
///   (이미 있으면 아무것도 안 함)
/// - 메뉴 Tools > Event Horizon > 한글 글꼴 SDF 만들기 로 직접 실행할 수도 있다.
/// - 만들어지는 에셋: "글꼴 파일 이름 SDF.asset" (같은 폴더)
///   → TextMeshPro 글자의 Font Asset 칸에서 골라 쓰면 된다.
/// - 동적 글꼴(Dynamic): 처음엔 비어 있고, 화면에 쓰이는 글자가 생길 때마다 글꼴 파일에서 그때그때 그려 넣는다.
///   그래서 한글 11,172자 전부를 쓸 수 있고, 처음 에셋 크기도 작다.
/// </summary>
[InitializeOnLoad]
public static class KoreanFontAssetMaker
{
    const string FontFolder = "Assets/Etc/Font";
    const string NameFilter = "KoPubWorld";   // 이 이름이 들어간 글꼴 파일만 대상으로

    // 글자 하나를 그릴 때의 크기·여백, 글자판(아틀라스) 크기
    const int SamplingPointSize = 64;
    const int Padding = 7;
    const int AtlasSize = 2048;

    static KoreanFontAssetMaker()
    {
        EditorApplication.delayCall += () => MakeMissing(false);
    }

    [MenuItem("Tools/Event Horizon/한글 글꼴 SDF 만들기")]
    static void MenuMake()
    {
        int made = MakeMissing(true);
        if (made == 0) Debug.Log("[한글 글꼴] 새로 만들 글꼴이 없습니다. (이미 모두 SDF 에셋이 있음)");
    }

    static int MakeMissing(bool fromMenu)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return 0;
        if (!AssetDatabase.IsValidFolder(FontFolder)) return 0;

        int made = 0;
        foreach (string guid in AssetDatabase.FindAssets(NameFilter + " t:Font", new[] { FontFolder }))
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(fontPath).ToLowerInvariant();
            if (ext != ".otf" && ext != ".ttf") continue;

            string dir = Path.GetDirectoryName(fontPath).Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(fontPath);
            string assetPath = dir + "/" + baseName + " SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) continue;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null) continue;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogWarning("[한글 글꼴] 글꼴 에셋을 만들지 못했습니다: " + fontPath);
                continue;
            }

            fontAsset.name = baseName + " SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // 글자판 그림과 재질을 같은 에셋 안에 함께 저장해야 다음에 열어도 그대로 남는다
            Texture2D atlas = fontAsset.atlasTextures[0];
            atlas.name = baseName + " SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);

            Material mat = fontAsset.material;
            mat.name = baseName + " SDF Material";
            AssetDatabase.AddObjectToAsset(mat, fontAsset);

            // 빌드할 때는 플레이 중에 채워진 글자를 비우고 넣는다 (빌드 크기·저장소 변경 줄이기)
            var so = new SerializedObject(fontAsset);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }

            EditorUtility.SetDirty(fontAsset);
            made++;
            Debug.Log("[한글 글꼴] 만들었습니다: " + assetPath);
        }

        if (made > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        return made;
    }
}
#endif
