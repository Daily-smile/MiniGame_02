using System;
using System.Collections;
using System.Reflection;
using HybridCLR;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace LF.Framework
{
/// <summary>
/// HybridCLR 预加载器 —— 在 LoadingScene 中完成热更 DLL 的加载，
/// 然后自动切换到 Game 场景。
///
/// 流程：
///   1. App 启动 → Unity 加载 LoadingScene（Index 0，无 GameLogic 脚本）
///   2. BeforeSceneLoad 创建 PreloaderRunner，启动协程
///   3. 协程：初始化 YooAsset → 加载 AOT 元数据 → 加载热更 DLL
///   4. 全部完成后 → SceneManager.LoadScene("Game")
///
/// 关键原则（来自 HybridCLR 官方文档）：
///   1. AOT 补充元数据 DLL 必须随包发布（StreamingAssets），不可远程热更。
///   2. 热更 DLL 在 Builtin（首次）或 Sandbox（热更后）中，由 YooAsset 统一管理。
///   3. DLL 加载必须在任何热更程序集代码执行前完成。
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
        "DOTween",
        "DOTween.Modules",
    };

    /// <summary>热更程序集列表</summary>
    private static readonly string[] HotUpdateAssemblyNames =
    {
        "GameLogic",
    };

    /// <summary>资源地址前缀（匹配 AddressByFolderAndFileName 规则生成的地址格式）</summary>
    private const string DllAssetPrefix = "HotUpdateDlls_";

    /// <summary>预加载完成后要加载的主场景名称</summary>
    private const string GameSceneName = "Game";

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
        Debug.Log("[HybridCLRPreloader] Editor mode: skip preloading (DLLs are compiled in Editor).");
        IsLoaded = true;
        return;
#else
        var go = new GameObject("__HybridCLRPreloader__");
        GameObject.DontDestroyOnLoad(go);
        go.AddComponent<PreloaderRunner>().StartPreload();
#endif
    }

    /// <summary>
    /// 辅助 MonoBehaviour，用于启动协程并在完成后加载 Game 场景。
    /// </summary>
    private class PreloaderRunner : MonoBehaviour
    {
        public void StartPreload()
        {
            StartCoroutine(RunPreload());
        }

        private IEnumerator RunPreload()
        {
            yield return PreloadAsync();

            if (IsLoaded)
            {
                Debug.Log("[HybridCLRPreloader] Preload complete, switching to Game scene...");
                yield return SceneManager.LoadSceneAsync(GameSceneName);
            }
            else
            {
                Debug.LogError("[HybridCLRPreloader] Preload failed — staying on loading scene.");
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 预加载流程：初始化 YooAsset → 加载 AOT 元数据 → 加载热更 DLL → 预热。
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

        // ── Step 2: 加载 AOT 补充元数据 ──
        foreach (string aotName in AOTMetaAssemblyNames)
        {
            yield return LoadAOTMetadata(BootstrapPackage, aotName);
        }

        // ── Step 3: 加载热更程序集 ──
        foreach (string hotName in HotUpdateAssemblyNames)
        {
            yield return LoadHotUpdateAssembly(BootstrapPackage, hotName);
        }

        // ── Step 4: 预热常用泛型方法 ──
        PreJitCommonMethods();

        IsLoaded = true;
        Debug.Log("[HybridCLRPreloader] Preload complete. Hot update assemblies are ready.");
    }

    /// <summary>
    /// 预热 Mirror 网络层和 YooAsset 资源系统中常用的泛型方法。
    /// </summary>
    private static void PreJitCommonMethods()
    {
        try
        {
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

            // 预热热更程序集中可能用到的泛型实例化（避免运行时报 MissingMethodException）。
            // GameLogic.dll 已在 Step 3 加载完毕，此处的反射可以找到热更类型。
            PreJitHotUpdateGenerics();

            Debug.Log("[HybridCLRPreloader] PreJit optimization applied.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HybridCLRPreloader] PreJit skipped (non-critical): {e.Message}");
        }
    }

    /// <summary>
    /// 预热热更程序集（GameLogic）中值类型在 List&lt;T&gt; 等泛型容器
    /// 中的方法实例化，避免运行时抛出 MissingMethodException。
    ///
    /// 背景：AOT 侧无法引用热更类型，List&lt;StartAnim&gt;.get_Item 等泛型方法
    /// 不会在 il2cpp 中提前生成。DLL 加载后通过反射手动触发 PreJit。
    /// </summary>
    private static void PreJitHotUpdateGenerics()
    {
        // StartAnim (struct in GameLogic.dll) → List<StartAnim>
        var startAnimType = Type.GetType("LF.GameLogic.StartAnim, GameLogic");
        if (startAnimType != null)
        {
            var listOfStartAnim = typeof(System.Collections.Generic.List<>).MakeGenericType(startAnimType);
            RuntimeApi.PreJitClass(listOfStartAnim);
        }

        // 如需添加更多热更值类型的泛型容器预热，在此追加。
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

        // 优先使用本地缓存的版本号
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
        else
        {
            Debug.LogError("[HybridCLRPreloader] No builtin catalog found. "
                         + "Ensure resources were deployed with forceCopyToStreamingAssets=true.");
        }
    }

    /// <summary>
    /// 加载 AOT 补充元数据。
    /// </summary>
    private static IEnumerator LoadAOTMetadata(ResourcePackage package, string assemblyName)
    {
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            Debug.Log($"[HybridCLRPreloader] AOT metadata not found: {assemblyName} (may be built into il2cpp)");
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
    /// </summary>
    private static IEnumerator LoadHotUpdateAssembly(ResourcePackage package, string assemblyName)
    {
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            Debug.LogError($"[HybridCLRPreloader] Hot update DLL not found: {assemblyName}");
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
