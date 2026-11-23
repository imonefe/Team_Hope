using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// [2026-11-07] 웹(WebGL) 빌드를 깃허브 페이지용 docs 폴더(Team_Hope/docs)에 한 번에 만드는 메뉴.
///
/// 메뉴: 상단 Build → WebGL 빌드 (깃허브 docs)
/// - 빌드 설정(Build Settings)에 체크된 씬을 순서대로 넣는다 (intro → SampleScene).
/// - 플랫폼이 WebGL 이 아니면 먼저 WebGL 로 전환한다 (처음엔 오래 걸림).
/// - 결과는 저장소 최상위의 docs 폴더에 덮어쓴다. 기존 docs 내용은 지워진다.
/// - 깃허브 페이지가 파일을 그대로 띄우도록 .nojekyll 파일을 함께 만든다.
/// - 끝나면 100MB 넘는 파일이 있는지 검사한다 (깃허브는 100MB 넘는 파일을 받지 않음).
/// [2026-11-24] 재발 방지 추가
/// - 빌드 직전에 WebGLTextureOptimizer 검사를 먼저 돌린다 (새로 들어온 그림이 '압축 안 함'으로 들어가는 것 방지).
/// - 프로젝트 Temp 폴더에 먼저 빌드하고, 성공 + 100MB 이하일 때만 docs 를 바꾼다
///   (예전: docs 를 먼저 지워서, 빌드가 실패하면 docs 가 빈 채로 남았음 / 100MB 넘어도 docs 에 들어가 푸시가 막혔음).
/// - 100MB 를 넘으면 빌드에서 가장 큰 애셋 목록을 보여 준다.
/// - 깃허브 페이지는 압축 헤더를 안 보내므로 '압축 해제 대체(Decompression Fallback)'를 항상 켠다.
/// 게임 코드는 건드리지 않는 에디터 전용 스크립트.
/// </summary>
public static class WebGLDocsBuilder
{
    const long GitHubFileLimit = 100L * 1024 * 1024;

    [MenuItem("Build/WebGL 빌드 (깃허브 docs)")]
    public static void BuildToDocs()
    {
        // Assets 의 상위(EventHorizon)의 상위(Team_Hope)/docs
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string outDir = Path.Combine(repoRoot, "docs");

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("WebGL 빌드", "Build Settings 에 체크된 씬이 없습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("WebGL 빌드",
            "아래 폴더에 웹 빌드를 만듭니다. 폴더 안의 기존 내용은 지워집니다.\n\n" + outDir +
            "\n\n씬: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)),
            "빌드", "취소"))
            return;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("WebGL 빌드", "WebGL 플랫폼으로 전환하지 못했습니다. Unity Hub 에서 WebGL Build Support 모듈 설치를 확인하세요.", "확인");
                return;
            }
        }

        // [2026-11-24] 새로 들어온 그림이 압축 없이 들어가지 않게 먼저 검사 (취소하면 빌드도 취소)
        if (!WebGLTextureOptimizer.Run(true)) return;
        WebGLTextureOptimizer.checkedByBuilder = true;   // 빌드 훅에서 다시 묻지 않게
        PlayerSettings.WebGL.decompressionFallback = true;

        // 프로젝트 Temp 에 먼저 빌드 → 성공했을 때만 docs 교체
        string tmpDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "WebGLDocsBuild"));
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = tmpDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report;
        try { report = BuildPipeline.BuildPlayer(options); }
        finally { WebGLTextureOptimizer.checkedByBuilder = false; }

        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("WebGL 빌드", "빌드 실패: " + report.summary.result + "\n콘솔의 빨간 에러를 확인하세요.\n기존 docs 는 그대로 두었습니다.", "확인");
            return;
        }

        // 깃허브 100MB 제한 검사
        var files = new DirectoryInfo(tmpDir).GetFiles("*", SearchOption.AllDirectories);
        var big = files.Where(f => f.Length > GitHubFileLimit).ToArray();
        long total = files.Sum(f => f.Length);
        if (big.Length > 0)
        {
            string top = string.Join("\n", report.packedAssets.SelectMany(pa => pa.contents)
                .GroupBy(c => c.sourceAssetPath).Select(g => new { path = g.Key, size = g.Sum(c => (long)c.packedSize) })
                .OrderByDescending(x => x.size).Take(12)
                .Select(x => (x.size / 1048576f).ToString("0.0") + " MB  " + x.path));
            string fail = "[중지] 100MB 넘는 파일이 있어 깃허브에 올라가지 않습니다. docs 는 바꾸지 않았습니다.\n" +
                          string.Join("\n", big.Select(f => f.Name + " (" + (f.Length / 1048576f).ToString("0.0") + " MB)")) +
                          "\n\n빌드에서 큰 애셋 (압축 전):\n" + top + "\n\n빌드 결과는 여기에 있습니다:\n" + tmpDir;
            Debug.LogError("[WebGLDocsBuilder] " + fail.Replace("\n", " / "));
            EditorUtility.DisplayDialog("WebGL 빌드", fail, "확인");
            return;
        }

        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        CopyDir(tmpDir, outDir);
        File.WriteAllText(Path.Combine(outDir, ".nojekyll"), "");

        string msg = "빌드 완료\n" + outDir + "\n전체 크기: " + (total / 1048576f).ToString("0.0") + " MB" +
                     "\n가장 큰 파일: " + files.OrderByDescending(f => f.Length).First().Name + " (" +
                     (files.Max(f => f.Length) / 1048576f).ToString("0.0") + " MB / 제한 100 MB)";
        Debug.Log("[WebGLDocsBuilder] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("WebGL 빌드", msg, "확인");
        EditorUtility.RevealInFinder(outDir);
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
