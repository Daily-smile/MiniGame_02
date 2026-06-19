using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

/// <summary>
/// 资源热更新构建工具。
/// 提供菜单命令：一键热更构建、构建 App。
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
        bool forceCopyToStreamingAssets = false, bool revealInFinder = true)
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
                if (revealInFinder)
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

        // DisplayDialogComplex 实际按钮布局: ok(左=0) | alt(中=2) | cancel(右=1)
        int buildType = EditorUtility.DisplayDialogComplex(
            "Build Options",
            "Choose build type:",
            "Release Build",          // ok     → 左按钮 → 0
            "Development + Profiler", // cancel → 右按钮 → 1
            "Cancel");                // alt    → 中按钮 → 2

        if (buildType == 2) return; // 中间 Cancel

        bool isDevelopmentBuild = buildType == 1; // 右边 Dev+Profiler
        bool autoconnectProfiler = buildType == 1;

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
        BuildPlayer(BuildTarget.StandaloneWindows64, version, isDevelopmentBuild, autoconnectProfiler);

        EditorUtility.ClearProgressBar();
        Debug.Log($"[HotfixBuild] App with hotfix build complete. Version: {version}");
        EditorUtility.DisplayDialog("Build Complete",
            $"App built successfully.\n\nVersion: {version}",
            "OK");
    }

    #endregion

    private static void CleanBuildIntermediateFiles(BuildTarget buildTarget)
    {
        // 清理 DLL 收集目录（热更 DLL + AOT 元数据）
        if (Directory.Exists(YooAssetDllCollectDir))
        {
            Directory.Delete(YooAssetDllCollectDir, true);
            Debug.Log($"[HotfixBuild] Cleaned: {YooAssetDllCollectDir}");
        }

        string buildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
        // 清理 Player 构建目录
        string buildsDir = Path.Combine(buildOutputRoot, "Builds");
        if (Directory.Exists(buildsDir))
        {
            Directory.Delete(buildsDir, true);
            Debug.Log($"[HotfixBuild] Cleaned: {buildsDir}");
        }
        // 清理当前平台的资源构建目录
        string platformDir = Path.Combine(buildOutputRoot, buildTarget.ToString());
        if (Directory.Exists(platformDir))
        {
            Directory.Delete(platformDir, true);
            Debug.Log($"[HotfixBuild] Cleaned: {platformDir}");
        }

        AssetDatabase.Refresh();
    }

    private static void BuildPlayer(BuildTarget buildTarget, string version,
        bool isDevelopmentBuild, bool autoconnectProfiler)
    {
        string buildDir = Path.Combine(
            BundleBuilderHelper.GetDefaultBuildOutputRoot(),
            "Builds",
            buildTarget.ToString(),
            version);

        Directory.CreateDirectory(buildDir);

        string ext = buildTarget switch
        {
            BuildTarget.StandaloneWindows64 => ".exe",
            BuildTarget.Android => ".apk",
            BuildTarget.StandaloneOSX => ".app",
            _ => "",
        };
        string appName = $"KeepRun_{version}{ext}";
        string outputPath = Path.Combine(buildDir, appName);

        var scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        var options = BuildOptions.None;
        if (isDevelopmentBuild)
        {
            options |= BuildOptions.Development;
            if (autoconnectProfiler)
                options |= BuildOptions.ConnectWithProfiler;
        }

        var buildLabel = isDevelopmentBuild
            ? (autoconnectProfiler ? "Development + Profiler" : "Development")
            : "Release";
        Debug.Log($"[HotfixBuild] Building player ({buildLabel})...");

        BuildPipeline.BuildPlayer(scenes, outputPath, buildTarget, options);

        Debug.Log($"[HotfixBuild] Player built ({buildLabel}): {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }

    #region HybridCLR 热更 DLL

    private const string HotUpdateDllSourceDir = "HybridCLRData/HotUpdateDlls/{0}";
    private const string AOTMetadataSourceDir = "HybridCLRData/AssembliesPostIl2CppStrip/{0}";
    private const string YooAssetDllCollectDir = "Assets/GameAssets/HotUpdateDlls";

    /// <summary>
    /// 将 HybridCLR 编译的热更 DLL 拷贝到 YooAsset 收集目录。
    /// 执行前请先运行 HybridCLR > CompileDll > ActiveBuildTarget。
    /// </summary>
    private static void DeployHotUpdateDlls()
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
        Debug.Log($"[HotfixBuild] Hot update DLLs deployed to: {YooAssetDllCollectDir}");
    }

    /// <summary>
    /// AOT 补充元数据：仅拷贝 HybridCLRPreloader.AOTMetaAssemblyNames 中列出的程序集。
    /// </summary>
    private static readonly string[] AOTMetaFilter =
    {
        "LFFramework", "Network", "Mirror", "Mirror.Components",
        "Mirror.Transports", "Mirror.Authenticators", "kcp2k", "YooAsset",
        "HybridCLR.Runtime", "DOTween", "DOTween.Modules",
    };

    /// <summary>
    /// 将 AOT 补充元数据 DLL 拷贝到 YooAsset 收集目录（由 YooAsset 打包，随包发布）。
    /// 只拷贝实际需要的程序集，避免包体积膨胀。
    /// 执行前请先运行 HybridCLR > Generate > All。
    /// </summary>
    private static void DeployAOTMetadata()
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

        if (!Directory.Exists(YooAssetDllCollectDir))
            Directory.CreateDirectory(YooAssetDllCollectDir);

        int copied = 0;
        foreach (string dllName in AOTMetaFilter)
        {
            string sourcePath = Path.Combine(sourceDir, dllName + ".dll");
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[HotfixBuild] AOT metadata not found, skipping: {dllName}.dll");
                continue;
            }
            string destPath = Path.Combine(YooAssetDllCollectDir, dllName + ".dll.bytes");
            File.Copy(sourcePath, destPath, true);
            Debug.Log($"[HotfixBuild] AOT metadata copied: {dllName}.dll → {destPath}");
            copied++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[HotfixBuild] {copied} AOT metadata files deployed to: {YooAssetDllCollectDir}");
    }

    /// <summary>
    /// 完整热更构建流程：编译 DLL → 拷贝 DLL → 拷贝 AOT 元数据 → 构建资源包 → 出包。
    /// </summary>
    [MenuItem("YooAsset/Full HotUpdate Build (DLLs + Resources + App)", false, 199)]
    public static void FullHotUpdateBuild()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        string version = GenerateBuildVersion();

        if (!EditorUtility.DisplayDialog(
            "Full HotUpdate Build",
            $"This will:\n"
            + "1. Compile hot update DLLs\n"
            + "2. Copy DLLs to YooAsset collect folder\n"
            + "3. Copy AOT metadata to YooAsset collect folder\n"
            + "4. Build YooAsset resources\n"
            + "5. Build Player ({target})\n\n"
            + $"Target: {target}\nVersion: {version}\n\n"
            + "Make sure you have run HybridCLR > Generate > All first!",
            "Build", "Cancel"))
        {
            return;
        }

        // DisplayDialogComplex 实际按钮布局: ok(左=0) | alt(中=2) | cancel(右=1)
        int buildType = EditorUtility.DisplayDialogComplex(
            "Build Options",
            "Choose build type:",
            "Release Build",          // ok     → 左按钮 → 0
            "Development + Profiler", // cancel → 右按钮 → 1
            "Cancel");                // alt    → 中按钮 → 2

        if (buildType == 2) return; // 中间 Cancel

        bool isDevelopmentBuild = buildType == 1; // 右边 Dev+Profiler
        bool autoconnectProfiler = buildType == 1;

        try
        {
            // 清理上次构建残留，确保干净环境
            CleanBuildIntermediateFiles(target);

            // Step 1: 编译热更 DLL
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 1/5: Compiling hot update DLLs...", 0.1f);
            HybridCLR.Editor.Commands.CompileDllCommand.CompileDll(target);
            Debug.Log("[HotfixBuild] Step 1/5: DLL compile done.");

            // Step 2: 拷贝热更 DLL 到收集目录
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 2/5: Deploying DLLs to collect folder...", 0.35f);
            DeployHotUpdateDlls();

            // Step 3: 拷贝 AOT 元数据到 YooAsset 收集目录
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 3/5: Deploying AOT metadata to collect folder...", 0.45f);
            DeployAOTMetadata();

            // Step 4: 构建 YooAsset 资源包（forceCopy 确保资源打入包体）
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 4/5: Building YooAsset resources...", 0.55f);
            if (!ExecuteBuild(target, version, forceCopyToStreamingAssets: true, revealInFinder: false))
            {
                EditorUtility.DisplayDialog("Build Failed",
                    "Resource build failed. Check console for details.", "OK");
                return;
            }

            // Step 5: 出包
            EditorUtility.DisplayProgressBar("Full HotUpdate Build",
                "Step 5/5: Building player...", 0.8f);
            BuildPlayer(target, version, isDevelopmentBuild, autoconnectProfiler);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[HotfixBuild] Full hot update build complete. Version: {version}");
        EditorUtility.DisplayDialog("Build Complete",
            $"Full hot update build finished.\n\nVersion: {version}",
            "OK");
    }

    #endregion
}
