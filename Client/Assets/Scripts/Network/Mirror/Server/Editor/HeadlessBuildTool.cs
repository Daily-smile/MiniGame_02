#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless Server 构建工具 (Editor 菜单)
///
/// 使用方法:
///   1. Unity 菜单 → Tools → Build Headless Server (Windows)
///   2. Unity 菜单 → Tools → Build Headless Server (Linux)
///
/// 构建产物:
///   Builds/Headless-Win/Client.exe
///   Builds/Headless-Linux/Client.x86_64
///
/// 运行时使用 -batchmode -nographics 参数即可进入 Headless 模式
/// </summary>
public static class HeadlessBuildTool
{
    private const string BUILD_DIR = "Builds";

    [MenuItem("Tools/Build Headless Server (Windows)")]
    public static void BuildHeadlessWindows()
    {
        BuildHeadlessServer(BuildTarget.StandaloneWindows64, "Headless-Win", "Client.exe");
    }

    [MenuItem("Tools/Build Headless Server (Linux)")]
    public static void BuildHeadlessLinux()
    {
        BuildHeadlessServer(BuildTarget.StandaloneLinux64, "Headless-Linux", "Client.x86_64");
    }

    private static void BuildHeadlessServer(BuildTarget target, string folder, string exeName)
    {
        string[] scenes = GetBuildScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[Build] 没有找到有效的场景！请将场景添加到 Build Settings。");
            return;
        }

        string buildPath = Path.Combine(BUILD_DIR, folder, exeName);
        Directory.CreateDirectory(Path.GetDirectoryName(buildPath));

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = target,
            options = BuildOptions.None
        };

        Debug.Log($"[Build] 开始构建 Headless Server → {buildPath}");
        Debug.Log($"[Build] 场景列表: {string.Join(", ", scenes)}");

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Build] 构建成功! 输出: {buildPath}");
            Debug.Log($"[Build] 运行命令: ./{exeName} -batchmode -nographics -logFile server.log");
            Debug.Log($"[Build] 注意: 构建为普通 Standalone，通过 -batchmode 参数进入 Headless 模式");
        }
        else
        {
            Debug.LogError($"[Build] 构建失败! 错误数: {report.summary.totalErrors}");
        }
    }

    private static string[] GetBuildScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }
        return scenes.ToArray();
    }

    [MenuItem("Tools/Build Headless Server (Windows)", true)]
    [MenuItem("Tools/Build Headless Server (Linux)", true)]
    private static bool ValidateBuild()
    {
        return !Application.isPlaying && !EditorApplication.isCompiling;
    }
}
#endif
