using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HybridCLR;
using UnityEngine;
using YooAsset;

namespace LF.Framework
{
/// <summary>
/// HybridCLR 预加载器 —— 在场景加载前完成热更 DLL 的加载。
///
/// 关键原则（来自 HybridCLR 官方文档）：
///   1. AOT 补充元数据 DLL 必须随包发布（StreamingAssets），不可远程热更。
///   2. 热更 DLL 在 Builtin（首次）或 Sandbox（热更后）中，由 YooAsset 统一管理。
///   3. DLL 加载必须在任何热更程序集代码执行前完成（BeforeSceneLoad）。
/// </summary>
public static class HybridCLRPreloader
{
    /// <summary>AOT 补充元数据程序集列表</summary>
    private static readonly string[] AOTMetaAssemblyNames =
    {
        "LFFramework",
        "Network",
        "Mirror",
        "Mirror.Components",
        "Mirror.Transports",
        "Mirror.Authenticators",
        "kcp2k",
        "YooAsset",
        "HybridCLR.Runtime",
    };

    /// <summary>热更程序集列表</summary>
    private static readonly string[] HotUpdateAssemblyNames =
    {
        "GameLogic",
    };

    /// <summary>资源地址前缀（匹配 AddressByFolderAndFileName 规则生成的地址格式）</summary>
    private const string DllAssetPrefix = "HotUpdateDlls_";

    /// <summary>
    /// 预加载是否已完成（供 GameLaunch 检查）
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>
    /// 预加载使用的 ResourcePackage（GameLaunch 可复用，避免重复初始化）
    /// </summary>
    public static ResourcePackage BootstrapPackage { get; private set; }

    /// <summary>
    /// 预加载包名称
    /// </summary>
    public const string PackageName = "DefaultPackage";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
#if UNITY_EDITOR
        Debug.Log("[HybridCLRPreloader] Editor mode: skip preloading (hot update DLLs are compiled in Editor).");
        IsLoaded = true;
        return;
#else
        // 同步启动协程（BeforeSceneLoad 阶段可用）
        var coroutine = PreloadAsync();
        while (coroutine.MoveNext()) { }
#endif
    }

    /// <summary>
    /// 预加载流程：初始化 YooAsset → 加载 AOT 元数据 → 加载热更 DLL。
    /// </summary>
    private static IEnumerator PreloadAsync()
    {
        Debug.Log("[HybridCLRPreloader] Starting preload...");

        // ── Step 1: 初始化 YooAsset（仅从内置资源，不访问网络）──
        yield return InitializeBuiltinPackage();

        if (BootstrapPackage == null)
        {
            Debug.LogError("[HybridCLRPreloader] Failed to initialize package, hot update may not work.");
            IsLoaded = false;
            yield break;
        }

        // ── Step 2: 加载 AOT 补充元数据（仅从内置/缓存，不访问网络）──
        foreach (string aotName in AOTMetaAssemblyNames)
        {
            yield return LoadAOTMetadata(BootstrapPackage, aotName);
        }

        // ── Step 3: 加载热更程序集（从内置或已缓存的热更版本）──
        foreach (string hotName in HotUpdateAssemblyNames)
        {
            yield return LoadHotUpdateAssembly(BootstrapPackage, hotName);
        }

        // ── Step 4: 预热常用泛型方法，减少首次调用 JIT 延迟（可选优化）──
        PreJitCommonMethods();

        IsLoaded = true;
        Debug.Log("[HybridCLRPreloader] Preload complete. Hot update assemblies are ready.");
    }

    /// <summary>
    /// 预热 Mirror 网络层和 YooAsset 资源系统中常用的泛型方法。
    /// 避免热更代码首次调用这些方法时出现 JIT 卡顿。
    /// </summary>
    private static void PreJitCommonMethods()
    {
        try
        {
            // 预热 Mirror NetworkBehaviour 常用 API
            var networkBehaviourType = Type.GetType("Mirror.NetworkBehaviour, Mirror");
            if (networkBehaviourType != null)
            {
                foreach (var method in networkBehaviourType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.IsGenericMethodDefinition)
                        RuntimeApi.PreJitMethod(method);
                }
            }

            // 预热常见的 Mirror 同步列表类型
            var syncListTypes = new[]
            {
                "Mirror.SyncList`1, Mirror",
                "Mirror.SyncList`1, Mirror.Components",
            };
            foreach (string typeName in syncListTypes)
            {
                var type = Type.GetType(typeName);
                if (type != null)
                    RuntimeApi.PreJitClass(type);
            }

            Debug.Log("[HybridCLRPreloader] PreJit optimization applied.");
        }
        catch (Exception e)
        {
            // PreJit 失败不影响核心功能，仅影响首次调用性能
            Debug.LogWarning($"[HybridCLRPreloader] PreJit skipped (non-critical): {e.Message}");
        }
    }

    /// <summary>
    /// 初始化 YooAsset 包（仅使用内置资源，不请求网络）。
    /// GameLaunch 之后可以用远端 URL 重新配置此 Package 进行版本检查。
    /// </summary>
    private static IEnumerator InitializeBuiltinPackage()
    {
        YooAssets.Initialize();
        BootstrapPackage = YooAssets.CreatePackage(PackageName);

        var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        builtinParams.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);

        // 使用 PlayerPrefs 中缓存的远端 URL（与 PatchManager 保持一致）
        string remoteUrl = PatchManager.GetBestRemoteUrl(PackageName, "http://127.0.0.1:8000");
        var remoteService = new ConfigurableRemoteService(remoteUrl);
        var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);

        var options = new HostPlayModeOptions
        {
            BuiltinFileSystemParameters = builtinParams,
            CacheFileSystemParameters = cacheParams
        };

        var initOp = BootstrapPackage.InitializePackageAsync(options);
        yield return initOp;

        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[HybridCLRPreloader] Package init failed: {initOp.Error}");
            BootstrapPackage = null;
            yield break;
        }

        // 使用本地缓存的版本号或内置版本号加载清单
        string localVersion = PatchManager.GetLocalVersion(PackageName);

        if (!string.IsNullOrEmpty(localVersion))
        {
            var manifestOp = BootstrapPackage.LoadPackageManifestAsync(
                new LoadPackageManifestOptions(localVersion, 10));
            yield return manifestOp;
            if (manifestOp.Status == EOperationStatus.Succeeded)
            {
                Debug.Log($"[HybridCLRPreloader] Loaded cached manifest: {localVersion}");
                yield break;
            }
            Debug.LogWarning("[HybridCLRPreloader] Cached manifest unavailable, trying builtin...");
        }

        // 回退：使用内置清单
        var versionOp = BootstrapPackage.RequestPackageVersionAsync();
        yield return versionOp;
        if (versionOp.Status == EOperationStatus.Succeeded)
        {
            var manifestOp = BootstrapPackage.LoadPackageManifestAsync(
                new LoadPackageManifestOptions(versionOp.PackageVersion, 10));
            yield return manifestOp;
            if (manifestOp.Status == EOperationStatus.Succeeded)
            {
                PatchManager.SaveLocalVersion(PackageName, versionOp.PackageVersion);
                Debug.Log($"[HybridCLRPreloader] Loaded builtin manifest: {versionOp.PackageVersion}");
            }
        }
    }

    /// <summary>
    /// 加载 AOT 补充元数据（本地加载，不访问网络）。
    /// 这些文件必须在 StreamingAssets 中随包发布。
    /// </summary>
    private static IEnumerator LoadAOTMetadata(ResourcePackage package, string assemblyName)
    {
        // AddressByFolderAndFileName 规则生成的地址格式: {文件夹名}_{文件名(无后缀)}
        // 如: HotUpdateDlls_LFFramework.dll
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            Debug.Log($"[HybridCLRPreloader] AOT metadata not found (may be built into il2cpp): {assemblyName}");
            yield break;
        }

        var textAsset = handle.AssetObject as TextAsset;
        if (textAsset == null)
        {
            handle.Release();
            yield break;
        }

        byte[] metaBytes = textAsset.bytes;
        handle.Release();

        LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(
            metaBytes, HomologousImageMode.SuperSet);

        if (err == LoadImageErrorCode.OK)
            Debug.Log($"[HybridCLRPreloader] AOT metadata OK: {assemblyName}");
        else
            Debug.LogError($"[HybridCLRPreloader] AOT metadata FAILED for {assemblyName}: {err}");
    }

    /// <summary>
    /// 加载热更程序集 DLL。
    /// 首次启动从内置资源加载，热更后从 Sandbox 缓存加载。
    /// </summary>
    private static IEnumerator LoadHotUpdateAssembly(ResourcePackage package, string assemblyName)
    {
        // AddressByFolderAndFileName 规则生成的地址格式: {文件夹名}_{文件名(无后缀)}
        // 如: HotUpdateDlls_GameLogic.dll
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            Debug.LogError($"[HybridCLRPreloader] Hot update DLL not found: {assemblyName}! "
                         + "App cannot start without it.");
            yield break;
        }

        var textAsset = handle.AssetObject as TextAsset;
        if (textAsset == null)
        {
            Debug.LogError($"[HybridCLRPreloader] Hot update DLL is not TextAsset: {assemblyName}");
            handle.Release();
            yield break;
        }

        byte[] dllBytes = textAsset.bytes;
        handle.Release();

        try
        {
            Assembly.Load(dllBytes);
            Debug.Log($"[HybridCLRPreloader] Hot update DLL loaded: {assemblyName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridCLRPreloader] Failed to load {assemblyName}: {e.Message}");
        }
    }

}
}
