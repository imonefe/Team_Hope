#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// [2026-11-24] BoldGFCRedSpiritSDF 글꼴을 고신고딕 Bold(GoshinGothic-Bold) 로 바꾼다. 유니티가 켜질 때 한 번 자동 실행.
///
/// 1. GoshinGothic-Bold.otf 로 TMP 글꼴 에셋 'GoshinGothic-Bold SDF' 를 만든다
///    (기존 글꼴과 같은 방식: 동적 · 글자판 2048 · 여백 5 → 외곽선 재질 값이 그대로 맞음).
/// 2. 기존 글꼴을 쓰던 곳(씬 3개 · 프리팹 2개)의 글꼴 · 재질 연결을 새 글꼴로 바꾼다.
/// 3. 외곽선 재질(ItemInfoPopup_Outline_Bold)의 글자판 그림을 새 글꼴 것으로 바꾼다.
/// 4. BoldGFCRedSpiritSDF 를 지운다 (원본은 Desktop/백업/bak_1124_font 에 복사해 둠).
/// 메뉴: Tools > Event Horizon > 글꼴 교체 (GFC Bold → 고신고딕 Bold)
/// </summary>
[InitializeOnLoad]
public static class FontSwapGoshin
{
    const string OldPath = "Assets/Etc/Font/BoldGFCRedSpiritSDF.asset";
    const string FontPath = "Assets/Etc/Font/GoshinGothic-Bold.otf";
    const string NewPath = "Assets/Etc/Font/GoshinGothic-Bold SDF.asset";
    const string OutlineMat = "Assets/4. Art/Materials/UI/ItemInfoPopup_Outline_Bold.mat";
    const long OldMatId = -3349638658498980409, OldAtlasId = -40659314553122472;
    static readonly string[] Users =
    {
        "Assets/0.Scenes/intro.unity",
        "Assets/0.Scenes/SampleScene.unity",
        "Assets/_Test/VolumetricTest/VolumetricTest.unity",
        "Assets/1.Prefab/Effect/FloatingText.prefab",
        "Assets/1.Prefab/UI/Canvas/ItemInfoPopup.prefab",
    };

    static FontSwapGoshin()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (AssetDatabase.LoadAssetAtPath<Object>(OldPath) == null) return;   // 이미 끝남
            Run();
        };
    }

    [MenuItem("Tools/Event Horizon/글꼴 교체 (GFC Bold → 고신고딕 Bold)")]
    static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldPath);
        if (font == null || old == null) { Debug.LogWarning("[글꼴 교체] 고신고딕 글꼴 파일 또는 기존 글꼴이 없어 건너뜀"); return; }

        // 1) 새 글꼴 에셋
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogError("[글꼴 교체] 글꼴 에셋을 만들지 못함"); return; }
            fa.name = "GoshinGothic-Bold SDF";
            AssetDatabase.CreateAsset(fa, NewPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            var so = new SerializedObject(fa);
            var r = so.FindProperty("m_SourceFontFile_EditorRef"); if (r != null) r.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(NewPath);
            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewPath);
        }
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fa, out string newGuid, out long _)
            || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fa.material, out string _, out long newMat)
            || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fa.atlasTexture, out string _, out long newAtlas))
        { Debug.LogError("[글꼴 교체] 새 글꼴의 연결 번호를 읽지 못함"); return; }
        string oldGuid = AssetDatabase.AssetPathToGUID(OldPath);

        // 2) 쓰는 곳 연결 바꾸기 (파일 글자 바꾸기 — 열린 씬은 유니티가 다시 불러오기를 물음)
        int files = 0;
        foreach (var p in Users)
        {
            if (!File.Exists(p)) continue;
            string s = File.ReadAllText(p);
            string t = s.Replace("{fileID: 11400000, guid: " + oldGuid, "{fileID: 11400000, guid: " + newGuid)
                        .Replace("{fileID: " + OldMatId + ", guid: " + oldGuid, "{fileID: " + newMat + ", guid: " + newGuid)
                        .Replace("{fileID: " + OldAtlasId + ", guid: " + oldGuid, "{fileID: " + newAtlas + ", guid: " + newGuid);
            if (t != s) { File.WriteAllText(p, t); files++; }
        }

        // 3) 외곽선 재질: 글자판 그림 · 크기 값을 새 글꼴에 맞춤
        var m = AssetDatabase.LoadAssetAtPath<Material>(OutlineMat);
        if (m != null)
        {
            m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_GradientScale, fa.atlasPadding + 1);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, fa.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, fa.atlasHeight);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        foreach (var p in Users) if (File.Exists(p)) AssetDatabase.ImportAsset(p);

        // 4) 기존 글꼴 삭제
        AssetDatabase.DeleteAsset(OldPath);
        AssetDatabase.Refresh();
        Debug.Log($"[글꼴 교체] BoldGFCRedSpiritSDF → GoshinGothic-Bold SDF (바꾼 파일 {files}개, 외곽선 재질 {(m != null ? 1 : 0)}개). 기존 글꼴은 삭제됨");
    }
}
#endif
