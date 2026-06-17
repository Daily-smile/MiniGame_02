using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

/// <summary>
/// 资源热更新构建工具。
/// 提供菜单命令：构建资源包、构建 App、部署到 StreamingAssets、部署到 HTTP 服务器。
/// </summary>
public static class HotfixBuildTool
{
    private const string PackageName = "DefaultPackage";
    private const string BuildPipelineName = "ScriptableBuildPipeline";

    /// <summary>
    /// 生成构建版本号（格式：yyyy-MM-dd-HHmm）
    /// </summary>
    public static string GenerateBuildVersion()
    {
        return DateTime.Now.ToString("yyyy-MM-dd-HHmm");
    }

    /// <summary>
    /// 创建 ScriptableBuildParameters 实例（从 EditorPrefs 读取持久化设置）
    /// </summary>
    /// <param name="forceCopyToStreamingAssets">出包时强制将资源复制到 StreamingAssets</param>
    private static ScriptableBuildParameters CreateBuildParameters(BuildTarget buildTarget, string buildVersion,
        bool forceCopyToStreamingAssets = false)
    {
        var fileNameStyle = BundleBuilderSetting.GetPackageFileNameStyle(PackageName, BuildPipelineName);
        var bundledCopyOption = forceCopyToStreamingAssets
            ? EBundledCopyOption.ClearAndCopyAll
            : BundleBuilderSetting.GetPackageBundledCopyOption(PackageName, BuildPipelineName);
        var bundledCopyParams = BundleBuilderSetting.GetPackageBundledCopyParams(PackageName, BuildPipelineName);
        var compressOption = BundleBuilderSetting.GetPackageCompressOption(PackageName, BuildPipelineName);
        var clearBuildCache = BundleBuilderSetting.GetPackageClearBuildCache(PackageName, BuildPipelineName);
        var useAssetDependencyDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(PackageName, BuildPipelineName);

        var buildParams = new ScriptableBuildParameters
        {
            BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
            BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
            BuildPipeline = BuildPipelineName,
            BuildBundleType = (int)EBundleType.AssetBundle,
            BuildTarget = buildTarget,
            PackageName = PackageName,
            PackageVersion = buildVersion,
            PackageNote = $"Build at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            EnableSharePackRule = true,
            VerifyBuildingResult = true,
            FileNameStyle = fileNameStyle,
            BundledCopyOption = bundledCopyOption,
            BundledCopyParams = bundledCopyParams,
            CompressOption = compressOption,
            ClearBuildCacheFiles = clearBuildCache,
            UseAssetDependencyDB = useAssetDependencyDB
        };

        return buildParams;
    }

    /// <summary>
    /// 执行资源包构建
    /// </summary>
    private static bool ExecuteBuild(BuildTarget buildTarget, string buildVersion,
        bool forceCopyToStreamingAssets = false)
    {
        try
        {
            Debug.Log($"[HotfixBuild] Starting build for {buildTarget}, version: {buildVersion}"
                     + (forceCopyToStreamingAssets ? " (force copy to StreamingAssets)" : ""));

            var buildParams = CreateBuildParameters(buildTarget, buildVersion, forceCopyToStreamingAssets);
            var pipeline = new ScriptableBuildPipeline();
            var buildResult = pipeline.Run(buildParams, true);

            if (buildResult.Success)
            {
                Debug.Log($"[HotfixBuild] Build succeeded! Output: {buildResult.OutputPackageDirectory}");
                EditorUtility.RevealInFinder(buildResult.OutputPackageDirectory);
                return true;
            }
            else
            {
                Debug.LogError($"[HotfixBuild] Build failed! Error: {buildResult.ErrorInfo}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[HotfixBuild] Build exception: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    #region Menu Items — 构建

    [MenuItem("YooAsset/Build Hotfix Resources (Current Platform)", false, 200)]
    public static void BuildHotfixForCurrentPlatform()
    {
        string version = GenerateBuildVersion();

        if (!EditorUtility.DisplayDialog(
            "Build Hotfix Resources",
            $"Build resources for {EditorUserBuildSettings.activeBuildTarget}?\nVersion: {version}",
            "Build", "Cancel"))
        {
            return;
        }

        ExecuteBuild(EditorUserBuildSettings.activeBuildTarget, version);
    }

    [MenuItem("YooAsset/Build Hotfix Resources (All Standalone)", false, 201)]
    public static void BuildHotfixForAllStandalone()
    {
        string version = GenerateBuildVersion();

        if (!EditorUtility.DisplayDialog(
            "Build Hotfix Resources (All Standalone)",
            $"Build resources for Windows64 + Linux64 + macOS?\nVersion: {version}",
            "Build", "Cancel"))
        {
            return;
        }

        EditorUtility.DisplayProgressBar("Build Hotfix", "Building for Windows64...", 0f);
        bool winOk = ExecuteBuild(BuildTarget.StandaloneWindows64, version);

        EditorUtility.DisplayProgressBar("Build Hotfix", "Building for Linux64...", 0.33f);
        bool linuxOk = ExecuteBuild(BuildTarget.StandaloneLinux64, version);

        EditorUtility.DisplayProgressBar("Build Hotfix", "Building for macOS...", 0.66f);
        bool macOk = ExecuteBuild(BuildTarget.StandaloneOSX, version);

        EditorUtility.ClearProgressBar();

        string summary = $"Windows64: {(winOk ? "OK" : "FAILED")}\n"
                       + $"Linux64:   {(linuxOk ? "OK" : "FAILED")}\n"
                       + $"macOS:     {(macOk ? "OK" : "FAILED")}";
        Debug.Log($"[HotfixBuild] All platforms done:\n{summary}");
        EditorUtility.DisplayDialog("Build Complete", summary, "OK");
    }

    [MenuItem("YooAsset/Build App With Hotfix (Windows64)", false, 210)]
    public static void BuildAppWithHotfixWindows()
    {
        string version = GenerateBuildVersion();

        if (!EditorUtility.DisplayDialog(
            "Build App With Hotfix",
            $"Step 1: Build resources + copy to StreamingAssets\nStep 2: Build Windows64 player\n\nVersion: {version}",
            "Build", "Cancel"))
        {
            return;
        }

        // Step 1: 构建资源包（forceCopyToStreamingAssets=true 会自动生成 BuiltinCatalog.bytes 并复制到 StreamingAssets）
        EditorUtility.DisplayProgressBar("Build App", "Step 1/2: Building resources + deploying to StreamingAssets...", 0.4f);
        if (!ExecuteBuild(BuildTarget.StandaloneWindows64, version, forceCopyToStreamingAssets: true))
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Build Failed", "Resource build failed. Check console for details.", "OK");
            return;
        }

        // Step 2: 构建 App
        EditorUtility.DisplayProgressBar("Build App", "Step 2/2: Building Windows64 player...", 0.7f);
        BuildPlayer(BuildTarget.StandaloneWindows64, version);

        EditorUtility.ClearProgressBar();
        Debug.Log($"[HotfixBuild] App with hotfix build complete. Version: {version}");
        EditorUtility.DisplayDialog("Build Complete",
            $"App built successfully.\n\nVersion: {version}\n\n"
            + "Next: deploy resources to HTTP server for hot update testing.\n"
            + "Menu: YooAsset > Deploy To Local HTTP Server",
            "OK");
    }

    #endregion

    #region Menu Items — 部署

    [MenuItem("YooAsset/Deploy Resources To StreamingAssets", false, 220)]
    public static void DeployToStreamingAssets()
    {
        string packageRoot = Path.Combine(
            BundleBuilderHelper.GetDefaultBuildOutputRoot(),
            EditorUserBuildSettings.activeBuildTarget.ToString(),
            PackageName);

        if (!Directory.Exists(packageRoot))
        {
            EditorUtility.DisplayDialog("Error",
                $"Package root not found:\n{packageRoot}\n\nPlease build resources first.", "OK");
            return;
        }

        string latestVersion = FindLatestVersion(packageRoot);
        if (string.IsNullOrEmpty(latestVersion))
        {
            EditorUtility.DisplayDialog("Error", "No build version found.", "OK");
            return;
        }

        string sourceDir = Path.Combine(packageRoot, latestVersion);
        // 必须包含 PackageName 子目录，匹配 YooAsset BuiltinFileSystem 默认结构
        string destDir = Path.Combine(BundleBuilderHelper.GetStreamingAssetsRoot(), PackageName);

        if (!EditorUtility.DisplayDialog(
            "Deploy To StreamingAssets",
            $"Copy version '{latestVersion}' to StreamingAssets?\n\nFrom: {sourceDir}\nTo:   {destDir}",
            "Deploy", "Cancel"))
        {
            return;
        }

        try
        {
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);

            CopyDirectory(sourceDir, destDir);
            AssetDatabase.Refresh();

            Debug.Log($"[HotfixBuild] Deployed version {latestVersion} to StreamingAssets.");
            EditorUtility.DisplayDialog("Deploy Complete",
                $"Version {latestVersion} deployed to StreamingAssets.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HotfixBuild] Deploy failed: {e.Message}");
            EditorUtility.DisplayDialog("Deploy Failed", e.Message, "OK");
        }
    }

    [MenuItem("YooAsset/Deploy To Local HTTP Server", false, 221)]
    public static void DeployToLocalHttpServer()
    {
        string packageRoot = Path.Combine(
            BundleBuilderHelper.GetDefaultBuildOutputRoot(),
            EditorUserBuildSettings.activeBuildTarget.ToString(),
            PackageName);

        if (!Directory.Exists(packageRoot))
        {
            EditorUtility.DisplayDialog("Error",
                $"Package root not found:\n{packageRoot}\n\nPlease build resources first.", "OK");
            return;
        }

        string latestVersion = FindLatestVersion(packageRoot);
        if (string.IsNullOrEmpty(latestVersion))
        {
            EditorUtility.DisplayDialog("Error", "No build version found.", "OK");
            return;
        }

        string lastDeployPath = EditorPrefs.GetString("HotfixDeploy_LastPath",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HotfixServer"));
        string deployPath = EditorUtility.OpenFolderPanel("Select HTTP Server Root Directory", lastDeployPath, "");

        if (string.IsNullOrEmpty(deployPath))
            return;

        EditorPrefs.SetString("HotfixDeploy_LastPath", deployPath);

        string targetDir = deployPath; // 所有文件统一部署到服务器根目录
        string sourceDir = Path.Combine(packageRoot, latestVersion);

        if (!EditorUtility.DisplayDialog(
            "Deploy To HTTP Server",
            $"Deploy version '{latestVersion}'?\n\nFrom: {sourceDir}\nTo:   {targetDir}",
            "Deploy", "Cancel"))
        {
            return;
        }

        try
        {
            // 清理旧的热更资源文件
            CleanOldHotfixFiles(deployPath, latestVersion);

            // 所有文件统一部署到服务器根目录
            CopyDirectory(sourceDir, targetDir);

            Debug.Log($"[HotfixBuild] Deployed version {latestVersion} to HTTP server root: {targetDir}");
            EditorUtility.DisplayDialog("Deploy Complete",
                $"Version {latestVersion} deployed.\n\n"
                + $"All files at root: {targetDir}\n\n"
                + "Example URL: http://localhost/DefaultPackage.version",
                "OK");

            EditorUtility.RevealInFinder(targetDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"[HotfixBuild] Deploy failed: {e.Message}");
            EditorUtility.DisplayDialog("Deploy Failed", e.Message, "OK");
        }
    }

    #endregion

    #region Helpers

    private static string FindLatestVersion(string packageRoot)
    {
        string latestVersion = "";
        DateTime latestTime = DateTime.MinValue;
        foreach (var dir in Directory.GetDirectories(packageRoot))
        {
            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.Name == "OutputCache" || dirInfo.Name == "Simulate")
                continue;
            if (dirInfo.LastWriteTime > latestTime)
            {
                latestTime = dirInfo.LastWriteTime;
                latestVersion = dirInfo.Name;
            }
        }
        return latestVersion;
    }

    /// <summary>
    /// 清理部署目录中的旧热更文件（避免 hash/manifest 版本冲突）。
    /// 仅删除 YooAsset 相关文件，不影响用户放在服务器目录中的其他文件。
    /// </summary>
    private static void CleanOldHotfixFiles(string deployDir, string newVersion)
    {
        foreach (string file in Directory.GetFiles(deployDir))
        {
            string name = Path.GetFileName(file);
            // 删除旧版本的 hash、bytes、version、bundle 文件（避免多个版本共存导致混乱）
            if ((name.StartsWith(PackageName) && (name.EndsWith(".hash") || name.EndsWith(".bytes") || name.EndsWith(".version")))
                || name.EndsWith(".bundle"))
            {
                File.Delete(file);
                Debug.Log($"[HotfixBuild] Cleaned old file: {name}");
            }
        }
    }

    private static void BuildPlayer(BuildTarget buildTarget, string version)
    {
        string buildDir = Path.Combine(
            BundleBuilderHelper.GetDefaultBuildOutputRoot(),
            "Builds",
            buildTarget.ToString(),
            version);

        Directory.CreateDirectory(buildDir);

        string ext = buildTarget == BuildTarget.StandaloneWindows64 ? ".exe" : "";
        string appName = $"KeepRun_{version}{ext}";
        string outputPath = Path.Combine(buildDir, appName);

        var scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        BuildPipeline.BuildPlayer(scenes, outputPath, buildTarget, BuildOptions.None);

        Debug.Log($"[HotfixBuild] Player built: {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    #endregion

    #region Menu Items — HybridCLR 热更 DLL

    private const string HotUpdateDllSourceDir = "HybridCLRData/HotUpdateDlls/{0}";
    private const string AOTMetadataSourceDir = "HybridCLRData/AssembliesPostIl2CppStrip/{0}";
    private const string YooAssetDllCollectDir = "Assets/GameAssets/HotUpdateDlls";
    private const string BuiltinAOTMetadataDir = "Assets/StreamingAssets/HotUpdateDlls";

    /// <summary>
    /// 将 HybridCLR 编译的热更 DLL 拷贝到 YooAsset 收集目录。
    /// 执行前请先运行 HybridCLR > CompileDll > ActiveBuildTarget。
    /// </summary>
    [MenuItem("YooAsset/Deploy HotUpdate DLLs To Collect Folder", false, 230)]
    public static void DeployHotUpdateDlls()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        string sourceDir = Path.Combine(
            Application.dataPath.Replace("/Assets", ""),
            string.Format(HotUpdateDllSourceDir, target));

        if (!Directory.Exists(sourceDir))
        {
            EditorUtility.DisplayDialog("Error",
                $"Hot update DLLs not found:\n{sourceDir}\n\n"
                + "Please run HybridCLR > CompileDll > ActiveBuildTarget first.", "OK");
            return;
        }

        // 确保 YooAsset 收集目录存在
        if (!Directory.Exists(YooAssetDllCollectDir))
            Directory.CreateDirectory(YooAssetDllCollectDir);

        // 拷贝 DLL 文件为 .bytes (Unity 会将 .dll 识别为脚本，必须改名)
        foreach (string dllPath in Directory.GetFiles(sourceDir, "*.dll"))
        {
            string dllName = Path.GetFileName(dllPath);
            string destPath = Path.Combine(YooAssetDllCollectDir, dllName + ".bytes");
            File.Copy(dllPath, destPath, true);
            Debug.Log($"[HotfixBuild] Copied: {dllName} → {destPath}");
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Deploy Complete",
            $"Hot update DLLs deployed to:\n{YooAssetDllCollectDir}\n\n"
            + "Next: YooAsset > Build Hotfix Resources to package them.", "OK");
    }

    /// <summary>
    /// AOT 补充元数据：仅拷贝 HybridCLRPreloader.AOTMetaAssemblyNames 中列出的程序集。
    /// </summary>
    private static readonly string[] AOTMetaFilter =
    {
        "LFFramework", "Network", "Mirror", "Mirror.Components",
        "Mirror.Transports", "Mirror.Authenticators", "kcp2k", "YooAsset",
        "HybridCLR.Runtime", "DOTween",
    };

    /// <summary>
    /// 将 AOT 补充元数据 DLL 拷贝到 StreamingAssets（随包发布）。
    /// 只拷贝实际需要的程序集，避免包体积膨胀。
    /// 执行前请先运行 HybridCLR > Generate > All。
    /// </summary>
    [MenuItem("YooAsset/Deploy AOT Metadata To StreamingAssets", false, 231)]
    public static void DeployAOTMetadata()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        string sourceDir = Path.Combine(
            Application.dataPath.Replace("/Assets", ""),
            string.Format(AOTMetadataSourceDir, target));

        if (!Directory.Exists(sourceDir))
        {
            EditorUtility.DisplayDialog("Error",
                $"AOT metadata not found:\n{sourceDir}\n\n"
                + "Please run HybridCLR > Generate > All first.", "OK");
            return;
        }

        if (!Directory.Exists(BuiltinAOTMetadataDir))
            Directory.CreateDirectory(BuiltinAOTMetadataDir);

        int copied = 0;
        foreach (string dllName in AOTMetaFilter)
        {
            string sourcePath = Path.Combine(sourceDir, dllName + ".dll");
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[HotfixBuild] AOT metadata not found, skipping: {dllName}.dll");
                continue;
            }
            string destPath = Path.Combine(BuiltinAOTMetadataDir, dllName + ".dll.bytes");
            File.Copy(sourcePath, destPath, true);
            Debug.Log($"[HotfixBuild] AOT metadata copied: {dllName}.dll → {destPath}");
            copied++;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Deploy Complete",
            $"{copied} AOT metadata files deployed to:\n{BuiltinAOTMetadataDir}\n\n"
            + "These files will be included in the app build via StreamingAssets.", "OK");
    }

    /// <summary>
    /// 完整热更构建流程：编译 DLL → 拷贝到收集目录 → 构建资源包。
    /// </summary>
    [MenuItem("YooAsset/Full HotUpdate Build (DLLs + Resources)", false, 199)]
    public static void FullHotUpdateBuild()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        string version = GenerateBuildVersion();

        if (!EditorUtility.DisplayDialog(
            "Full HotUpdate Build",
            $"This will:\n"
            + "1. Compile hot update DLLs (HybridCLR)\n"
            + "2. Copy DLLs to YooAsset collect folder\n"
            + "3. Build YooAsset resources\n\n"
            + $"Target: {target}\nVersion: {version}\n\n"
            + "Make sure you have run HybridCLR > Generate > All first!",
            "Build", "Cancel"))
        {
            return;
        }

        try
        {
            // Step 1: 编译热更 DLL
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 1/4: Compiling hot update DLLs...", 0.1f);
            HybridCLR.Editor.Commands.CompileDllCommand.CompileDll(target);
            Debug.Log("[HotfixBuild] Step 1/4: DLL compile done.");

            // Step 2: 拷贝热更 DLL 到收集目录
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 2/4: Deploying DLLs to collect folder...", 0.4f);
            DeployHotUpdateDlls();

            // Step 3: 拷贝 AOT 元数据到 StreamingAssets
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 3/4: Deploying AOT metadata to StreamingAssets...", 0.5f);
            DeployAOTMetadata();

            // Step 4: 构建 YooAsset 资源包
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 4/4: Building YooAsset resources...", 0.6f);
            ExecuteBuild(target, version);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[HotfixBuild] Full hot update build complete. Version: {version}");
        EditorUtility.DisplayDialog("Build Complete",
            $"Full hot update build finished.\n\nVersion: {version}\n\n"
            + "Next: deploy resources to HTTP server for testing.\n"
            + "Menu: YooAsset > Deploy To Local HTTP Server", "OK");
    }

    #endregion
}
