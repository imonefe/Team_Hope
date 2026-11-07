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

        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("WebGL 빌드", "빌드 실패: " + report.summary.result + "\n콘솔의 빨간 에러를 확인하세요.", "확인");
            return;
        }

        File.WriteAllText(Path.Combine(outDir, ".nojekyll"), "");

        // 깃허브 100MB 제한 검사
        var big = new DirectoryInfo(outDir).GetFiles("*", SearchOption.AllDirectories)
                                           .Where(f => f.Length > GitHubFileLimit).ToArray();
        long total = new DirectoryInfo(outDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        string msg = "빌드 완료\n" + outDir + "\n전체 크기: " + (total / 1048576f).ToString("0.0") + " MB";
        if (big.Length > 0)
            msg += "\n\n[주의] 100MB 넘는 파일이 있어 깃허브에 올라가지 않습니다:\n" +
                   string.Join("\n", big.Select(f => f.Name + " (" + (f.Length / 1048576f).ToString("0.0") + " MB)"));
        Debug.Log("[WebGLDocsBuilder] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("WebGL 빌드", msg, "확인");
        EditorUtility.RevealInFinder(outDir);
    }
}
