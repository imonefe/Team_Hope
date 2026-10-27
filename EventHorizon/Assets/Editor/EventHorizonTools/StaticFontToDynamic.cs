#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// [2026-10-27 추가] 한글 2,488자를 4096 글자판에 미리 구워 둔 "고정(Static)" 글꼴 에셋을 "동적(Dynamic)" 으로 바꾼다.
///
/// - 대상: HiKR-ExtraBold SDF, BoldGFCRedSpiritSDF, BlackGFCRedSpirit SDF (각 34.8MB → 거의 0)
/// - 바꾸는 것: 미리 구운 글자 전부 지움 → 글자판 2048 한 장(모자라면 자동으로 한 장 더) →
///   화면에 처음 나오는 글자만 그때 글꼴 파일에서 그려 넣음. 빌드할 때는 그려 둔 글자를 비워서 빌드 크기도 작게.
/// - 유니티가 켜지거나 스크립트가 다시 불러와질 때 아직 고정 방식이면 자동으로 한 번 실행된다 (이미 동적이면 아무것도 안 함).
///   메뉴 Tools > Event Horizon > 고정 글꼴을 동적으로 바꾸기 로 직접 실행할 수도 있다.
/// - 같은 글자판을 쓰는 재질(외곽선 재질 등)도 크기 값을 함께 맞춘다.
/// - 원본은 bak_1027/fonts 에 백업되어 있다.
/// </summary>
[InitializeOnLoad]
public static class StaticFontToDynamic
{
    static readonly string[] Targets =
    {
        "Assets/Etc/Font/HiKR-ExtraBold SDF.asset",
        "Assets/Etc/Font/BoldGFCRedSpiritSDF.asset",
        "Assets/Etc/Font/BlackGFCRedSpirit SDF.asset",
    };
    const int AtlasSize = 2048;

    static StaticFontToDynamic()
    {
        EditorApplication.delayCall += () => Run(false);
    }

    [MenuItem("Tools/Event Horizon/고정 글꼴을 동적으로 바꾸기")]
    static void Menu() => Run(true);

    static void Run(bool fromMenu)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        ShaderUtilities.GetShaderPropertyIDs();

        int changed = 0;
        foreach (string path in Targets)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null) continue;
            if (fa.atlasPopulationMode == AtlasPopulationMode.Dynamic) continue;

            var so = new SerializedObject(fa);
            var editorRef = so.FindProperty("m_SourceFontFile_EditorRef");
            if (editorRef == null || editorRef.objectReferenceValue == null)
            {
                Debug.LogWarning($"[글꼴 동적 전환] {fa.name}: 원본 글꼴 파일을 찾지 못해 건너뜀");
                continue;
            }

            Texture oldAtlas = fa.atlasTexture;

            so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
            so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) clear.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;   // 원본 글꼴 파일 연결까지 함께 됨
            fa.ClearFontAssetData(false);                          // 미리 구운 글자 지우고 글자판을 2048로

            // 이 글자판을 쓰는 재질들(글꼴 기본 재질 + 외곽선 등 프리셋)의 글자판 크기 값 맞추기
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !mat.HasProperty(ShaderUtilities.ID_MainTex)) continue;
                if (mat.GetTexture(ShaderUtilities.ID_MainTex) != oldAtlas && mat.GetTexture(ShaderUtilities.ID_MainTex) != fa.atlasTexture) continue;
                FixMaterial(mat, fa);
            }
            if (fa.material != null) FixMaterial(fa.material, fa);

            EditorUtility.SetDirty(fa);
            changed++;
            Debug.Log($"[글꼴 동적 전환] {fa.name} → 동적 (글자판 {AtlasSize}, 빌드 시 비움)");
        }

        if (changed > 0) AssetDatabase.SaveAssets();
        else if (fromMenu) Debug.Log("[글꼴 동적 전환] 바꿀 글꼴이 없습니다 (이미 모두 동적).");
    }

    static void FixMaterial(Material mat, TMP_FontAsset fa)
    {
        mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
        if (mat.HasProperty(ShaderUtilities.ID_TextureWidth)) mat.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
        if (mat.HasProperty(ShaderUtilities.ID_TextureHeight)) mat.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
        EditorUtility.SetDirty(mat);
    }
}
#endif
