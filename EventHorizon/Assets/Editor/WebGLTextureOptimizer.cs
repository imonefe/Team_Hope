using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// [2026-11-24] 웹(WebGL) 빌드용 텍스처 정리 도구.
///
/// 메뉴: 상단 Build → WebGL 텍스처 최적화 (검사·적용)
/// 'WebGL 빌드 (깃허브 docs)' 메뉴도 빌드 직전에 이 검사를 자동으로 먼저 돌린다.
///
/// 왜 필요한가 (11/23 웹 빌드에서 확인된 문제)
///  1. 스파인 그림(heroine·youngbok·boss·hero)이 '압축 안 함'으로 들어가 한 장에 32MB → 데이터 파일이 110MB 가 되어
///     깃허브 100MB 제한에 걸림.
///  2. 압축(Crunch)을 켜 둔 그림도 가로·세로가 4의 배수가 아니면 Unity 가 압축을 못 하고 '압축 안 함'으로 넣는다.
///     (예: 1s_boss 2870x3811 → 웹 최대 2048 로 줄이면 1542x2048, 1542 는 4의 배수가 아님 → 12MB 그대로)
///     Unity 는 이때 콘솔에 경고만 띄우고 빌드는 그대로 진행해서 알아채기 어렵다.
///
/// 하는 일 (빌드에 실제로 들어가는 텍스처만: 빌드 씬 + Resources 폴더가 쓰는 것)
///  - WebGL 전용 설정만 바꾼다. 기본·PC 설정, sRGB, 알파 설정은 건드리지 않는다 (스파인이 뿌옇게 보였던 문제 재발 방지).
///  - 팀원이 이미 WebGL 설정을 직접 켜 둔 그림은 그 설정을 그대로 두고, 4의 배수 문제만 고친다.
///  - 4의 배수가 아닌 그림:
///      스프라이트 → 그림 파일의 위·오른쪽 가장자리를 몇 픽셀 늘린다 (가장자리 색을 그대로 복사, 원본은 Desktop/백업 에 저장).
///                   위·오른쪽만 늘리므로 잘라 둔 스프라이트 칸(Multiple)의 위치는 그대로다.
///      일반 텍스처(셰이더·3D) → 'Non-Power of 2' 를 ToNearest 로 바꿔 Unity 가 2의 거듭제곱 크기로 맞추게 한다 (UV 그대로).
///  - 커서·라이트맵·읽기/쓰기 켠 텍스처·HDR 은 건드리지 않는다.
///  - 끝나면 실제로 압축이 안 된 텍스처를 다시 확인해 보고서로 남긴다 (Desktop/백업/WebGL_텍스처_보고서.txt).
/// </summary>
/// <summary>
/// [2026-11-24] File → Build Settings 창의 Build 버튼으로 웹 빌드를 해도 검사가 돌게 하는 훅.
/// (11/24 00:24 빌드가 이 창으로 빌드되어 검사를 건너뛰었고, 110MB 파일이 그대로 커밋됨)
/// - 빌드 전: 텍스처 검사 창을 띄움. '빌드 취소'를 누르면 빌드를 멈춤.
/// - 빌드 후: 100MB 넘는 파일이 있으면 콘솔 에러 + 경고 창 (깃허브에 올리지 말라고 알림).
/// </summary>
class WebGLTextureBuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        if (WebGLTextureOptimizer.checkedByBuilder) return;
        if (UnityEngine.Application.isBatchMode) return;
        if (!WebGLTextureOptimizer.Run(true))
            throw new BuildFailedException("WebGL 텍스처 검사에서 빌드를 취소했습니다.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        WebGLTextureOptimizer.checkedByBuilder = false;
        if (report.summary.platform != BuildTarget.WebGL) return;
        string dir = report.summary.outputPath;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var big = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Where(f => f.Length > 100L * 1024 * 1024).ToArray();
        if (big.Length == 0) return;
        string msg = "100MB 넘는 파일이 있어 깃허브에 푸시하면 거절됩니다. 커밋하지 마세요.\n" +
                     string.Join("\n", big.Select(f => f.Name + " (" + (f.Length / 1048576f).ToString("0.0") + " MB)")) +
                     "\n\nBuild → WebGL 텍스처 최적화 를 '적용'한 뒤 다시 빌드하세요.";
        Debug.LogError("[WebGLTextureBuildHook] " + msg.Replace("\n", " / "));
        if (!UnityEngine.Application.isBatchMode) EditorUtility.DisplayDialog("WebGL 빌드 경고", msg, "확인");
    }
}

public static class WebGLTextureOptimizer
{
    const string Marker = "EH_WebGLOpt";          // 이 도구가 정한 WebGL 설정 표시 (importer.userData)
    const string Platform = "WebGL";
    const long RawReportBytes = 256 * 1024;       // 이보다 작은 '압축 안 됨'은 무시

    class Plan
    {
        public string path; public TextureImporter ti;
        public int w, h;
        public TextureImporterPlatformSettings newWeb;   // null = WebGL 설정 그대로
        public int padW, padH;                           // 그림 파일 가장자리 늘리기
        public bool npotToNearest;
        public string note;
    }

    [MenuItem("Build/WebGL 텍스처 최적화 (검사·적용)")]
    public static void MenuRun() { Run(false); }

    /// <summary>WebGLDocsBuilder 가 이미 검사를 돌렸으면 true (빌드 훅에서 두 번 묻지 않게)</summary>
    internal static bool checkedByBuilder;

    /// <summary>검사 후 바꿀 것이 있으면 물어보고 적용. 취소하면 false</summary>
    public static bool Run(bool fromBuilder)
    {
        List<Plan> plans; List<string> skipped;
        try { EditorUtility.DisplayProgressBar("WebGL 텍스처 검사", "빌드에 들어가는 텍스처를 찾는 중", 0f); plans = Analyze(out skipped); }
        finally { EditorUtility.ClearProgressBar(); }

        var changes = plans.Where(p => p.newWeb != null || p.padW > 0 || p.padH > 0 || p.npotToNearest).ToList();
        var pads = changes.Where(p => p.padW > 0 || p.padH > 0).ToList();
        string backupDir = BackupRoot();

        if (changes.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("웹 빌드용 텍스처 설정을 정리합니다.\n");
            sb.AppendLine("· WebGL 압축 설정: " + changes.Count(p => p.newWeb != null) + "개");
            sb.AppendLine("· 2의 거듭제곱 맞춤(일반 텍스처): " + changes.Count(p => p.npotToNearest) + "개");
            sb.AppendLine("· 그림 가장자리 몇 픽셀 늘림(4의 배수 맞춤): " + pads.Count + "개");
            foreach (var p in pads.Take(8)) sb.AppendLine("    " + Path.GetFileName(p.path) + "  +" + p.padW + " x +" + p.padH);
            if (pads.Count > 8) sb.AppendLine("    … 외 " + (pads.Count - 8) + "개");
            if (pads.Count > 0) sb.AppendLine("\n원본 그림은 여기에 백업됩니다:\n" + backupDir);
            sb.AppendLine("\nPC·에디터용 기본 설정은 바꾸지 않습니다. 처음 한 번은 압축 때문에 몇 분 걸릴 수 있습니다.");
            int choice = EditorUtility.DisplayDialogComplex("WebGL 텍스처 최적화", sb.ToString(), "적용",
                                                            fromBuilder ? "빌드 취소" : "취소",
                                                            fromBuilder ? "적용하지 않고 빌드" : "보고서만");
            if (choice == 1) return false;
            if (choice == 0) Apply(changes, backupDir);
        }

        string report = Verify(plans, skipped, changes.Count);
        if (!fromBuilder) EditorUtility.DisplayDialog("WebGL 텍스처 최적화", report.Length > 1500 ? report.Substring(0, 1500) + "\n…(보고서 파일 참고)" : report, "확인");
        return true;
    }

    // ───────────────────────── 검사 ─────────────────────────

    static List<Plan> Analyze(out List<string> skipped)
    {
        skipped = new List<string>();
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/")
                        && !p.Contains("/Editor/") && !AssetDatabase.IsValidFolder(p)));
        var deps = AssetDatabase.GetDependencies(roots.ToArray(), true);

        // 재질(.mat)이 직접 쓰는 텍스처 = UV 0~1 을 그대로 쓰므로 그림 크기를 크게 늘리면 안 됨
        var matTex = new HashSet<string>();
        foreach (var m in deps.Where(d => d.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
            foreach (var d in AssetDatabase.GetDependencies(m, false)) matTex.Add(d);

        // 스프라이트 아틀라스에 묶인 그림은 아틀라스 그림이 대신 빌드에 들어가므로 건너뜀
        var packed = new HashSet<string>();
        foreach (var ap in deps.Where(d => d.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase) || d.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(ap);
                if (atlas == null) continue;
                foreach (var o in atlas.GetPackables())
                {
                    string op = AssetDatabase.GetAssetPath(o);
                    if (string.IsNullOrEmpty(op)) continue;
                    if (AssetDatabase.IsValidFolder(op))
                        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { op })) packed.Add(AssetDatabase.GUIDToAssetPath(g));
                    else packed.Add(op);
                }
            }
            catch (Exception e) { Debug.LogWarning("[WebGLTextureOptimizer] 아틀라스 읽기 실패 " + ap + ": " + e.Message); }
        }

        var plans = new List<Plan>();
        foreach (var path in deps)
        {
            if (!path.StartsWith("Assets/") || packed.Contains(path)) continue;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".hdr" || ext == ".exr") continue;
            var tt = ti.textureType;
            if (tt == TextureImporterType.Cursor || tt == TextureImporterType.Lightmap || tt == TextureImporterType.DirectionalLightmap
                || tt == TextureImporterType.Shadowmask || tt == TextureImporterType.Cookie || tt == TextureImporterType.SingleChannel)
                continue;
            if (ti.isReadable) { skipped.Add(path + "  (읽기/쓰기 켜짐 → 건드리지 않음)"); continue; }

            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            if (w <= 0 || h <= 0) continue;

            var p = new Plan { path = path, ti = ti, w = w, h = h };
            var web = ti.GetPlatformTextureSettings(Platform);
            bool ours = ti.userData == Marker;
            bool isSprite = tt == TextureImporterType.Sprite || tt == TextureImporterType.GUI;
            bool isSpine = File.Exists(path.Substring(0, path.Length - ext.Length) + ".atlas.txt");

            TextureImporterPlatformSettings target = web;
            if (!web.overridden || ours)
            {
                var n = new TextureImporterPlatformSettings();
                web.CopyTo(n);
                n.name = Platform; n.overridden = true;
                n.format = TextureImporterFormat.Automatic;
                n.textureCompression = TextureImporterCompression.Compressed;
                if (isSpine) { n.maxTextureSize = Mathf.Min(4096, Mathf.NextPowerOfTwo(Mathf.Max(w, h))); n.crunchedCompression = true; n.compressionQuality = 90; }
                else if (tt == TextureImporterType.NormalMap) { n.maxTextureSize = Mathf.Min(ti.maxTextureSize, 1024); n.crunchedCompression = false; }
                else if (isSprite) { n.maxTextureSize = Mathf.Min(ti.maxTextureSize, 4096); n.crunchedCompression = true; n.compressionQuality = 80; }
                else { n.maxTextureSize = Mathf.Min(ti.maxTextureSize, 1024); n.crunchedCompression = true; n.compressionQuality = 70; }
                p.newWeb = n;            // 같으면 맨 끝에서 지움
                target = n;
            }

            bool compressed = target.textureCompression != TextureImporterCompression.Uncompressed;
            if (compressed && !ScaledMultipleOf4(w, h, target.maxTextureSize, isSprite ? TextureImporterNPOTScale.None : ti.npotScale))
            {
                long rawBytes = RawSize(w, h, target.maxTextureSize);
                if (!isSprite)
                {
                    if (ti.npotScale == TextureImporterNPOTScale.None) p.npotToNearest = true;
                }
                else if (rawBytes >= RawReportBytes)
                {
                    if (ext != ".png") p.note = "PNG 가 아니라 가장자리를 늘리지 않음";
                    else if (IsPng16(path)) p.note = "16비트 PNG 라 가장자리를 늘리지 않음";
                    else
                    {
                        float lim = matTex.Contains(path) ? 0.005f : 0.02f;   // 재질이 쓰는 그림은 아주 조금만
                        int limW = Mathf.Max(3, Mathf.CeilToInt(w * lim)), limH = Mathf.Max(3, Mathf.CeilToInt(h * lim));
                        if (FindPad(w, h, target.maxTextureSize, limW, limH, out int pw, out int ph)) { p.padW = pw; p.padH = ph; }
                        else if (p.newWeb != null && TryLargerMax(w, h, p.newWeb)) { }   // 이 도구가 정한 설정이면 줄이지 않는 크기로 (최대 8192)
                        else p.note = "4의 배수로 맞출 만큼 조금만 늘리는 방법을 못 찾음 → 원본 크기를 4의 배수로 다시 내보내 주세요";
                    }
                }
            }
            if (p.newWeb != null && Same(web, p.newWeb)) p.newWeb = null;   // 이미 그 설정이면 다시 가져오지 않음
            plans.Add(p);
        }
        return plans;
    }

    static bool Same(TextureImporterPlatformSettings a, TextureImporterPlatformSettings b) =>
        a.overridden == b.overridden && a.maxTextureSize == b.maxTextureSize && a.format == b.format &&
        a.textureCompression == b.textureCompression && a.crunchedCompression == b.crunchedCompression &&
        (!b.crunchedCompression || a.compressionQuality == b.compressionQuality);

    /// <summary>Unity 가 최대 크기로 줄인 뒤의 가로·세로가 4의 배수인지 (큰 쪽을 max 에 맞추고 비율 유지)</summary>
    static bool ScaledMultipleOf4(int w, int h, int max, TextureImporterNPOTScale npot)
    {
        if (npot != TextureImporterNPOTScale.None) return true;   // 2의 거듭제곱으로 맞춰짐
        int L = Mathf.Max(w, h), S = Mathf.Min(w, h);
        if (L <= max) return w % 4 == 0 && h % 4 == 0;
        long num = (long)S * max;
        if (num % L != 0) return false;                          // 소수점이 생기면 반올림 방식에 따라 달라짐 → 안전하게 '아님'
        return (num / L) % 4 == 0;
    }

    /// <summary>최대 크기를 한두 단계 올려 '줄이지 않음'이 되면 (원본이 4의 배수일 때) 그 크기로 바꾼다</summary>
    static bool TryLargerMax(int w, int h, TextureImporterPlatformSettings s)
    {
        if (w % 4 != 0 || h % 4 != 0) return false;
        int need = Mathf.NextPowerOfTwo(Mathf.Max(w, h));
        if (need > 8192) return false;
        for (int m = s.maxTextureSize * 2; m <= need; m *= 2)
            if (ScaledMultipleOf4(w, h, m, TextureImporterNPOTScale.None)) { s.maxTextureSize = m; return true; }
        return false;
    }

    static long RawSize(int w, int h, int max)
    {
        float s = Mathf.Min(1f, max / (float)Mathf.Max(w, h));
        return (long)(w * s) * (long)(h * s) * 4;
    }

    /// <summary>가장 적게 늘려서 (원본도, 줄인 뒤도) 4의 배수가 되는 크기를 찾는다</summary>
    static bool FindPad(int w, int h, int max, int limW, int limH, out int pw, out int ph)
    {
        for (int total = 0; total <= limW + limH; total++)
            for (pw = 0; pw <= Mathf.Min(total, limW); pw++)
            {
                ph = total - pw;
                if (ph > limH) continue;
                int W = w + pw, H = h + ph;
                if (W % 4 != 0 || H % 4 != 0) continue;
                if (ScaledMultipleOf4(W, H, max, TextureImporterNPOTScale.None)) return true;
            }
        pw = ph = 0; return false;
    }

    static bool IsPng16(string path)
    {
        try
        {
            using (var f = File.OpenRead(path))
            {
                var b = new byte[25];
                if (f.Read(b, 0, 25) < 25) return false;
                return b[24] == 16;   // IHDR 의 비트 깊이
            }
        }
        catch { return false; }
    }

    // ───────────────────────── 적용 ─────────────────────────

    static string BackupRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "백업", "webgl_원본_" + DateTime.Now.ToString("yyyyMMdd"));

    static void Apply(List<Plan> changes, string backupDir)
    {
        // 1) 그림 파일 가장자리 늘리기 (원본 백업)
        var pads = changes.Where(p => p.padW > 0 || p.padH > 0).ToList();
        try
        {
            for (int i = 0; i < pads.Count; i++)
            {
                var p = pads[i];
                EditorUtility.DisplayProgressBar("WebGL 텍스처 최적화", "가장자리 맞춤: " + Path.GetFileName(p.path), i / (float)Mathf.Max(1, pads.Count));
                if (!PadFile(p, backupDir)) { p.note = "가장자리 늘리기 실패 (콘솔 참고)"; p.padW = p.padH = 0; }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // 2) 가져오기 설정 바꾸고 한꺼번에 다시 가져오기
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var p in changes)
            {
                if (p.newWeb != null)
                {
                    p.ti.SetPlatformTextureSettings(p.newWeb);
                    if (string.IsNullOrEmpty(p.ti.userData)) p.ti.userData = Marker;
                }
                if (p.npotToNearest) p.ti.npotScale = TextureImporterNPOTScale.ToNearest;
                EditorUtility.SetDirty(p.ti);
                p.ti.SaveAndReimport();
                if (p.padW > 0 || p.padH > 0) AssetDatabase.ImportAsset(p.path, ImportAssetOptions.ForceUpdate);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();
    }

    static bool PadFile(Plan p, string backupDir)
    {
        try
        {
            string full = Path.GetFullPath(p.path);
            string bak = Path.Combine(backupDir, p.path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(bak));
            if (!File.Exists(bak)) File.Copy(full, bak);

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(full))) { UnityEngine.Object.DestroyImmediate(src); return false; }
            int w = src.width, h = src.height, W = w + p.padW, H = h + p.padH;
            var a = src.GetPixels32();
            var b = new Color32[W * H];
            for (int y = 0; y < H; y++)                       // Unity 텍스처는 아래가 y=0 → 늘린 줄은 그림의 위쪽
            {
                int sy = y < h ? y : h - 1;
                int so = sy * w, d = y * W;
                for (int x = 0; x < W; x++) b[d + x] = a[so + (x < w ? x : w - 1)];   // 가장자리 색 복사
            }
            var dst = new Texture2D(W, H, src.format == TextureFormat.RGB24 ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);   // 알파 없는 그림은 그대로 알파 없이
            dst.SetPixels32(b); dst.Apply(false);
            File.WriteAllBytes(full, dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(dst);
            Debug.Log("[WebGLTextureOptimizer] 가장자리 맞춤 " + p.path + "  " + w + "x" + h + " → " + W + "x" + H + "  (원본: " + bak + ")");
            return true;
        }
        catch (Exception e) { Debug.LogError("[WebGLTextureOptimizer] " + p.path + " 가장자리 늘리기 실패: " + e.Message); return false; }
    }

    // ───────────────────────── 확인 · 보고서 ─────────────────────────

    static string Verify(List<Plan> plans, List<string> skipped, int changed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WebGL 텍스처 보고서  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine("검사한 텍스처: " + plans.Count + "개, 바꾼 것: " + changed + "개");
        bool onWeb = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
        long rawTotal = 0;
        var raw = new List<string>();
        if (onWeb)
        {
            foreach (var p in plans)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p.path);
                if (tex == null || GraphicsFormatUtility.IsCompressedFormat(tex.graphicsFormat)) continue;
                long bytes = (long)tex.width * tex.height * 4;
                if (bytes < RawReportBytes) continue;
                rawTotal += bytes;
                raw.Add((bytes / 1048576f).ToString("0.0").PadLeft(6) + " MB  " + tex.width + "x" + tex.height + "  " + p.path + (p.note != null ? "  — " + p.note : ""));
            }
            raw.Sort(); raw.Reverse();
            sb.AppendLine(raw.Count == 0 ? "\n압축되지 않은 큰 텍스처: 없음"
                                         : "\n압축되지 않고 들어가는 텍스처 " + raw.Count + "개 (합계 약 " + (rawTotal / 1048576f).ToString("0") + " MB):");
            foreach (var r in raw) sb.AppendLine("  " + r);
        }
        else sb.AppendLine("\n현재 플랫폼이 WebGL 이 아니라 실제 압축 결과는 확인하지 못했습니다. (WebGL 빌드 메뉴를 쓰면 자동으로 확인)");

        var notes = plans.Where(p => p.note != null).ToList();
        if (notes.Count > 0 && !onWeb) { sb.AppendLine("\n손대지 못한 그림:"); foreach (var p in notes) sb.AppendLine("  " + p.path + " — " + p.note); }
        if (skipped.Count > 0) { sb.AppendLine("\n건드리지 않은 텍스처:"); foreach (var s in skipped) sb.AppendLine("  " + s); }

        string text = sb.ToString();
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "백업");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "WebGL_텍스처_보고서.txt"), text, Encoding.UTF8);
        }
        catch { }
        if (raw.Count > 0) Debug.LogWarning("[WebGLTextureOptimizer] " + text); else Debug.Log("[WebGLTextureOptimizer] " + text);
        return text;
    }
}
