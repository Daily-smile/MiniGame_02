using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

/// <summary>
/// 资源管理器 - 统一资源加载入口。
/// 通过 YooAsset 管理资源加载，Editor 下使用 EditorSimulateMode，
/// Runtime 下使用 HostPlayMode（远端更新）。
/// LoadAsset 路径为相对于 GameAssets 资源根目录的地址，
/// 例: LoadAsset("Panel_LoginPanel")。
/// </summary>
public class ResourceManager : MonoBehaviour
{
    #region Singleton
    private static ResourceManager _instance;
    public static ResourceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ResourceManager");
                _instance = go.AddComponent<ResourceManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    private Dictionary<string, ResourceHandle> resourceCache = new Dictionary<string, ResourceHandle>();
    private Dictionary<string, List<Action<UnityEngine.Object>>> loadingCallbacks = new Dictionary<string, List<Action<UnityEngine.Object>>>();
    private IResourceLoader resourceLoader;
    private ResourcePackage _package;
    private Dictionary<string, List<string>> dependencyMap = new Dictionary<string, List<string>>();

    public static event Action<long> OnMemoryCritical;

    private long totalMemoryUsage = 0;
    private long memoryThreshold = 100 * 1024 * 1024;

    public long MemoryThresholdMB
    {
        get { return memoryThreshold / (1024 * 1024); }
        set { memoryThreshold = value * 1024 * 1024; }
    }

    /// <summary>
    /// YooAsset 资源包（初始化完成后可用）
    /// </summary>
    public ResourcePackage Package => _package;

    void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        resourceCache.Clear();
        loadingCallbacks.Clear();
    }

    /// <summary>
    /// 设置资源加载器（在 YooAsset 初始化完成后调用）
    /// </summary>
    public void SetResourceLoader(IResourceLoader loader)
    {
        resourceLoader = loader;
    }

    /// <summary>
    /// 设置资源包并同时设置加载器。
    /// PatchManager 在热更新流程完成后调用此方法注入已初始化的 Package。
    /// </summary>
    public void SetPackage(ResourcePackage package)
    {
        _package = package;
        resourceLoader = new YooAssetResourceLoader(package);
    }

    /// <summary>
    /// 初始化 YooAsset 资源包（简化版）。
    /// 版本检查和资源下载已移至 PatchManager，此方法仅负责：
    /// 1. 初始化资源包文件系统
    /// 2. 加载本地清单
    /// 3. 设置资源加载器
    ///
    /// 调用前需确保 PatchManager.CheckAndUpdateAsync() 已完成。
    /// </summary>
    public IEnumerator InitializeYooAsset(string packageName, string remoteBaseUrl = null)
    {
#if UNITY_EDITOR
        // Editor 模式：EditorSimulateMode
        YooAssets.Initialize();
        var package = YooAssets.CreatePackage(packageName);
        _package = package;

        var buildResult = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
        var packageRoot = buildResult.PackageRootDirectory;
        var fsParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
        var options = new EditorSimulateModeOptions { EditorFileSystemParameters = fsParams };

        var initOp = package.InitializePackageAsync(options);
        yield return initOp;

        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[ResourceManager] YooAsset package '{packageName}' init failed! Error: {initOp.Error}");
            yield break;
        }

        var versionOp = package.RequestPackageVersionAsync();
        yield return versionOp;

        if (versionOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[ResourceManager] Editor version request failed: {versionOp.Error}");
            yield break;
        }

        var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, 60);
        var manifestOp = package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[ResourceManager] Failed to load package manifest! Error: {manifestOp.Error}");
            yield break;
        }

        SetResourceLoader(new YooAssetResourceLoader(package));
        Debug.Log($"[ResourceManager] YooAsset package '{packageName}' ready (Editor mode).");
#else
        // Runtime：PatchManager 已处理版本检查和下载，这里直接用 PlayePrefs 中的版本号加载清单
        YooAssets.Initialize();
        var package = YooAssets.CreatePackage(packageName);
        _package = package;

        var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(
            new DefaultRemoteService(remoteBaseUrl ?? "http://127.0.0.1/"), "GameAssets");
        var options = new HostPlayModeOptions
        {
            BuiltinFileSystemParameters = builtinParams,
            CacheFileSystemParameters = cacheParams
        };

        var initOp = package.InitializePackageAsync(options);
        yield return initOp;

        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[ResourceManager] YooAsset package '{packageName}' init failed! Error: {initOp.Error}");
            yield break;
        }

        string localVersion = PatchManager.GetLocalVersion(packageName);
        var manifestOptions = new LoadPackageManifestOptions(localVersion, 60);
        var manifestOp = package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[ResourceManager] Failed to load package manifest! Error: {manifestOp.Error}");
            yield break;
        }

        SetResourceLoader(new YooAssetResourceLoader(package));
        Debug.Log($"[ResourceManager] YooAsset package '{packageName}' ready (Runtime mode).");
#endif
    }

    #region 资源管理相关功能API

    public void AddAsset(string path, UnityEngine.Object asset)
    {
        if (asset == null) return;
        if (!resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = new ResourceHandle(path, asset);
            resourceCache[path] = handle;
        }
    }

    public T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Resource path is null or empty!");
            return null;
        }
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            handle.AddReference();
            return handle.Asset as T;
        }
        if (resourceLoader == null)
        {
            Debug.LogError("[ResourceManager] resourceLoader is null! YooAsset may not be initialized.");
            return null;
        }
        ResourceHandle newHandle = resourceLoader.LoadAsset<T>(path);
        if (newHandle != null && newHandle.Asset != null)
        {
            resourceCache[path] = newHandle;
            return newHandle.Asset as T;
        }
        Debug.LogError($"Failed to load resource: {path}");
        return null;
    }

    public void LoadAssetAsync<T>(string path, Action<T> callback) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Resource path is null or empty!");
            callback?.Invoke(null);
            return;
        }
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            handle.AddReference();
            callback?.Invoke(handle.Asset as T);
            return;
        }
        if (loadingCallbacks.ContainsKey(path))
        {
            loadingCallbacks[path].Add((obj) => callback?.Invoke(obj as T));
            return;
        }
        if (resourceLoader == null)
        {
            Debug.LogError("[ResourceManager] resourceLoader is null! YooAsset may not be initialized.");
            callback?.Invoke(null);
            return;
        }
        loadingCallbacks[path] = new List<Action<UnityEngine.Object>>();
        loadingCallbacks[path].Add((obj) => callback?.Invoke(obj as T));
        resourceLoader.LoadAssetAsync<T>(path, (handle) =>
        {
            if (handle != null && handle.Asset != null)
            {
                resourceCache[path] = handle;
                if (loadingCallbacks.ContainsKey(path))
                {
                    foreach (var cb in loadingCallbacks[path])
                    {
                        cb?.Invoke(handle.Asset);
                    }
                    loadingCallbacks.Remove(path);
                }
            }
            else
            {
                Debug.LogError($"Failed to load resource async: {path}");
                if (loadingCallbacks.ContainsKey(path))
                {
                    foreach (var cb in loadingCallbacks[path])
                    {
                        cb?.Invoke(null);
                    }
                    loadingCallbacks.Remove(path);
                }
            }
        });
    }

    public void UnloadAsset(string path)
    {
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            if (handle.Release())
            {
                resourceCache.Remove(path);
                resourceLoader?.UnloadAsset(handle);
            }
        }
    }

    public void UnloadUnusedAssets()
    {
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in resourceCache)
        {
            if (kvp.Value.ReferenceCount <= 0)
            {
                keysToRemove.Add(kvp.Key);
                resourceLoader?.UnloadAsset(kvp.Value);
            }
        }
        foreach (string key in keysToRemove)
        {
            resourceCache.Remove(key);
        }
    }

    public void PreloadAssets(List<string> paths)
    {
        foreach (string path in paths)
        {
            if (!resourceCache.ContainsKey(path))
            {
                LoadAssetAsync<UnityEngine.Object>(path, null);
            }
        }
    }

    public Dictionary<string, ResourceInfo> GetResourceInfo()
    {
        Dictionary<string, ResourceInfo> info = new Dictionary<string, ResourceInfo>();
        foreach (var kvp in resourceCache)
        {
            info[kvp.Key] = new ResourceInfo
            {
                Path = kvp.Key,
                ReferenceCount = kvp.Value.ReferenceCount,
                MemorySize = CalculateMemorySize(kvp.Value.Asset)
            };
        }
        return info;
    }

    private long CalculateMemorySize(UnityEngine.Object obj)
    {
        if (obj is Texture2D texture)
        {
            return texture.width * texture.height * 4;
        }
        return 0;
    }

    #endregion

    #region 内存监测

    public void CheckMemoryUsage()
    {
        totalMemoryUsage = 0;
        foreach (var kvp in resourceCache)
        {
            totalMemoryUsage += CalculateMemorySize(kvp.Value.Asset);
        }
        Debug.Log($"Memory usage: {totalMemoryUsage / 1024 / 1024}MB / {MemoryThresholdMB}MB");
        if (IsMemoryCritical())
        {
            Debug.LogWarning($"Memory critical: {totalMemoryUsage / 1024 / 1024}MB");
            OnMemoryCritical?.Invoke(totalMemoryUsage);
        }
    }

    public bool IsMemoryCritical()
    {
        return totalMemoryUsage >= memoryThreshold;
    }

    #endregion

    #region 依赖项管理

    public void AddDependency(string resourcePath, string dependencyPath)
    {
        if (!dependencyMap.ContainsKey(resourcePath))
        {
            dependencyMap[resourcePath] = new List<string>();
        }
        if (!dependencyMap[resourcePath].Contains(dependencyPath))
        {
            dependencyMap[resourcePath].Add(dependencyPath);
            if (resourceCache.ContainsKey(dependencyPath))
            {
                resourceCache[dependencyPath].AddReference();
            }
        }
    }

    public void UnloadAssetWithDependencies(string path)
    {
        if (dependencyMap.ContainsKey(path))
        {
            foreach (string dependency in dependencyMap[path])
            {
                UnloadAsset(dependency);
            }
            dependencyMap.Remove(path);
        }
        UnloadAsset(path);
    }

    #endregion
}

/// <summary>
/// 默认的远端资源地址查询服务
/// </summary>
public class DefaultRemoteService : IRemoteService
{
    private readonly string _baseUrl;

    public DefaultRemoteService(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        return new List<string> { $"{_baseUrl}/{fileName}" };
    }
}

public struct ResourceInfo
{
    public string Path;
    public int ReferenceCount;
    public long MemorySize;
}
