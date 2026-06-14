using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

/// <summary>
/// 资源热更新管理器。
/// 负责版本检查、资源对比、下载更新、错误重试的完整流程。
/// 通过 EventDispatcher 广播事件，与 UI 层解耦。
///
/// 使用方式（在 GameLaunch 中调用）：
/// <code>
///   var patchMgr = new PatchManager();
///   yield return patchMgr.CheckAndUpdateAsync("DefaultPackage", "http://127.0.0.1/");
///   // 热更流程结束后，ResourceManager.Package 已可用
/// </code>
/// </summary>
public class PatchManager
{
    private ResourcePackage _package;
    private RemoteConfig _remoteConfig;
    private ConfigurableRemoteService _remoteService;
    private string _packageName;
    private string _remoteBaseUrl;
    private int _retryCount;
    private int _maxRetryCount = 3;

    /// <summary>
    /// 当前操作的资源包（初始化完成后可用）
    /// </summary>
    public ResourcePackage Package => _package;

    /// <summary>
    /// 远端版本号（版本检查完成后可用）
    /// </summary>
    public string RemoteVersion { get; private set; }

    /// <summary>
    /// 本地版本号
    /// </summary>
    public string LocalVersion { get; private set; }

    /// <summary>
    /// 是否需要更新
    /// </summary>
    public bool NeedUpdate { get; private set; }

    /// <summary>
    /// 下载总大小（字节）
    /// </summary>
    public long TotalDownloadSize { get; private set; }

    /// <summary>
    /// 下载总文件数
    /// </summary>
    public int TotalDownloadCount { get; private set; }

    /// <summary>
    /// 更新策略（从 RemoteConfig 读取）
    /// </summary>
    public EUpdateStrategy UpdateStrategy { get; private set; } = EUpdateStrategy.Force;

    /// <summary>
    /// 用户是否同意更新（Optional 策略下由 UI 设置）
    /// </summary>
    public bool UserAgreedUpdate { get; set; }

    /// <summary>
    /// 用户是否已点击跳过（服务器不可达时由 UI 设置）
    /// </summary>
    public bool UserSkippedUpdate { get; set; }

    #region Version Persistence

    /// <summary>
    /// 获取本地缓存的资源版本号
    /// </summary>
    public static string GetLocalVersion(string packageName)
    {
        return PlayerPrefs.GetString($"ResourceVersion_{packageName}", "");
    }

    /// <summary>
    /// 保存资源版本号到本地
    /// </summary>
    public static void SaveLocalVersion(string packageName, string version)
    {
        PlayerPrefs.SetString($"ResourceVersion_{packageName}", version);
        PlayerPrefs.Save();
    }

    #endregion

    #region Main Flow

    /// <summary>
    /// 阶段一：初始化资源包（加载清单，使 ResourceManager 可用）。
    /// 完成后即可加载 UI 预制体，再调用 CheckAndUpdateAsync 进行版本检查。
    /// </summary>
    /// <param name="packageName">资源包名称</param>
    /// <param name="remoteBaseUrl">远端资源服务器根地址</param>
    /// <returns>初始化是否成功</returns>
    public IEnumerator InitPackageAsync(string packageName, string remoteBaseUrl = null)
    {
        _packageName = packageName;
        _remoteBaseUrl = remoteBaseUrl ?? "http://127.0.0.1/";

#if UNITY_EDITOR
        yield return InitializeEditorMode(packageName);
#else
        yield return InitializeRuntimePackageOnly(packageName);
#endif
    }

    /// <summary>
    /// 阶段二：版本检查 + 资源下载（通过 EventDispatcher 广播进度，供 UI 面板监听）。
    /// 调用前必须先完成 InitPackageAsync，并且 UI 面板已订阅事件。
    /// </summary>
    public IEnumerator CheckAndUpdateAsync()
    {
        _retryCount = 0;
        NeedUpdate = false;
        TotalDownloadSize = 0;
        TotalDownloadCount = 0;

        // ── 广播：开始检查 ──
        EventDispatcher.PostEvent(MessageEvent.OnPatchCheckStart, this, this);

#if UNITY_EDITOR
        // Editor 模式无需远端检查，直接完成
        EventDispatcher.PostEvent(MessageEvent.OnPatchFinish, this, true, "Editor mode ready.");
        yield break;
#else
        // 远端版本检查 + 下载
        yield return CheckAndDownloadUpdates();

        // ── 广播：流程结束 ──
        var success = _package != null && ResourceManager.Instance.Package != null;
        EventDispatcher.PostEvent(MessageEvent.OnPatchFinish, this, success, success ? "Ready" : "Failed");
#endif
    }

    #endregion

    #region Editor Mode

    private IEnumerator InitializeEditorMode(string packageName)
    {
        Debug.Log("[PatchManager] Editor mode: using EditorSimulateMode.");

        YooAssets.Initialize();
        _package = YooAssets.CreatePackage(packageName);

        var buildResult = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
        var packageRoot = buildResult.PackageRootDirectory;
        var fsParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
        var options = new EditorSimulateModeOptions { EditorFileSystemParameters = fsParams };

        var initOp = _package.InitializePackageAsync(options);
        yield return initOp;

        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Editor init failed: {initOp.Error}");
            yield break;
        }

        // Editor 下请求版本号 + 加载清单
        var versionOp = _package.RequestPackageVersionAsync();
        yield return versionOp;

        if (versionOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Editor version request failed: {versionOp.Error}");
            yield break;
        }

        var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, 60);
        var manifestOp = _package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Editor manifest load failed: {manifestOp.Error}");
            yield break;
        }

        // 从 AssetDatabase 加载 RemoteConfig（Editor 下直接访问）
        TryLoadRemoteConfigFromEditor();

        ResourceManager.Instance.SetPackage(_package);
        Debug.Log("[PatchManager] Editor mode ready.");
    }

    private void TryLoadRemoteConfigFromEditor()
    {
#if UNITY_EDITOR
        // Editor 下从 AssetDatabase 直接查找 RemoteConfig
        var guids = UnityEditor.AssetDatabase.FindAssets("t:RemoteConfig");
        foreach (var guid in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Boot") || path.Contains("GameAssets"))
            {
                _remoteConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<RemoteConfig>(path);
                if (_remoteConfig != null)
                {
                    UpdateStrategy = _remoteConfig.UpdateStrategy;
                    _maxRetryCount = _remoteConfig.MaxRetryCount;
                    Debug.Log($"[PatchManager] Loaded RemoteConfig from Editor: strategy={UpdateStrategy}");
                    break;
                }
            }
        }
#endif
    }

    #endregion

    #region Runtime Mode — Phase 1: Init

    /// <summary>
    /// 初始化资源包（仅本阶段），加载本地清单并设置 ResourceLoader。
    /// 完成后 ResourceManager 可用，可加载 UI 等资源。
    /// </summary>
    private IEnumerator InitializeRuntimePackageOnly(string packageName)
    {
        Debug.Log($"[PatchManager] Runtime: initializing package, remote={_remoteBaseUrl}");

        YooAssets.Initialize();
        _package = YooAssets.CreatePackage(packageName);

        var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        builtinParams.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);
        var remoteService = new ConfigurableRemoteService(_remoteBaseUrl);
        _remoteService = remoteService;
        var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);

        var options = new HostPlayModeOptions
        {
            BuiltinFileSystemParameters = builtinParams,
            CacheFileSystemParameters = cacheParams
        };

        var initOp = _package.InitializePackageAsync(options);
        yield return initOp;

        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Package init failed: {initOp.Error}");
            yield break;
        }

        Debug.Log("[PatchManager] Package initialized.");

        // 获取用于加载初始清单的版本号
        LocalVersion = GetLocalVersion(packageName);
        string loadVersion = LocalVersion;

        if (string.IsNullOrEmpty(loadVersion))
        {
            // 首次启动：优先从内置资源（StreamingAssets）加载清单，本地 I/O 速度快
            // 远端版本检查延后到 CheckAndUpdateAsync，使 UpdatePanel 能立即显示
            yield return RequestBuiltinPackageVersion(packageName);
            loadVersion = RemoteVersion;

            // 内置资源不可用时回退到远端请求
            if (string.IsNullOrEmpty(loadVersion))
            {
                Debug.Log("[PatchManager] Builtin version unavailable, falling back to remote...");
                yield return RequestRemoteVersion();
                loadVersion = RemoteVersion;
            }
        }

        yield return LoadManifestAndConfig(loadVersion);

        // 尝试加载 RemoteConfig
        TryLoadRemoteConfigFromPackage();

        // 注入 ResourceManager，使 UI 等资源可加载
        ResourceManager.Instance.SetPackage(_package);
        Debug.Log("[PatchManager] Package ready, resources loadable.");
    }

    #endregion

    #region Runtime Mode — Phase 2: Check & Download

    /// <summary>
    /// 远端版本检查 + 按策略下载（通过 EventDispatcher 广播事件给 UI）。
    /// </summary>
    private IEnumerator CheckAndDownloadUpdates()
    {
        Debug.Log($"[PatchManager] Checking remote version...");

        // ── Step 1: 请求远端版本号 ──
        yield return RequestRemoteVersion();

        // ── Step 2: 对比版本 ──
        LocalVersion = GetLocalVersion(_packageName);
        Debug.Log($"[PatchManager] Remote version: {RemoteVersion}, Local version: {LocalVersion}");

        // 远端版本为空说明更新服务器不可达，等待用户点击跳过
        if (string.IsNullOrEmpty(RemoteVersion))
        {
            Debug.Log("[PatchManager] Update server unreachable, waiting for user to skip...");
            EventDispatcher.PostEvent(MessageEvent.OnPatchVersionGet, this, RemoteVersion, LocalVersion);
            yield return WaitForUserSkip();
            yield break;
        }

        EventDispatcher.PostEvent(MessageEvent.OnPatchVersionGet, this, RemoteVersion, LocalVersion);

        NeedUpdate = !string.IsNullOrEmpty(RemoteVersion) && RemoteVersion != LocalVersion;

        if (!NeedUpdate)
        {
            Debug.Log("[PatchManager] Already up to date.");
            // 已是最新，无需额外操作，当前清单已在 InitPackageAsync 中加载
            yield break;
        }

        // ── Step 3: 根据策略决定下载行为 ──
        Debug.Log($"[PatchManager] Update strategy: {UpdateStrategy}");

        switch (UpdateStrategy)
        {
            case EUpdateStrategy.Silent:
                Debug.Log("[PatchManager] Silent mode: using local resources, will update on next launch.");
                if (string.IsNullOrEmpty(LocalVersion))
                {
                    // 首次启动无本地资源，必须下载
                    yield return DownloadAndApplyUpdate();
                }
                else
                {
                    NeedUpdate = false;
                }
                break;

            case EUpdateStrategy.Force:
                Debug.Log("[PatchManager] Force mode: downloading updates...");
                yield return DownloadAndApplyUpdate();
                break;

            case EUpdateStrategy.Optional:
                Debug.Log("[PatchManager] Optional mode: waiting for user decision...");
                yield return WaitForUserDecision();
                break;

            default:
                yield return DownloadAndApplyUpdate();
                break;
        }

        // ── Step 4: 如果下载了新版本，重新加载清单 ──
        if (NeedUpdate && !string.IsNullOrEmpty(RemoteVersion))
        {
            yield return LoadManifestAndConfig(RemoteVersion);
            ResourceManager.Instance.SetPackage(_package);
        }

        Debug.Log("[PatchManager] Check and update complete.");
    }

    #endregion

    #region Version & Config

    private IEnumerator RequestRemoteVersion()
    {
        while (_retryCount < _maxRetryCount)
        {
            var versionOp = _package.RequestPackageVersionAsync();
            yield return versionOp;

            if (versionOp.Status == EOperationStatus.Succeeded)
            {
                RemoteVersion = versionOp.PackageVersion;
                _retryCount = 0;
                yield break;
            }

            _retryCount++;
            Debug.LogWarning($"[PatchManager] Version request failed (retry {_retryCount}/{_maxRetryCount}): {versionOp.Error}");
            yield return new WaitForSeconds(1f);
        }

        Debug.LogError("[PatchManager] Version request failed after all retries.");

        // 失败降级：尝试使用本地版本
        RemoteVersion = GetLocalVersion(_packageName);
        if (string.IsNullOrEmpty(RemoteVersion))
        {
            Debug.LogError("[PatchManager] No local version available. First launch requires network.");
        }
    }

    /// <summary>
    /// 从内置资源（StreamingAssets）读取资源包版本号，并将内置清单文件拷贝到沙盒缓存，
    /// 使 HostPlayMode 的 SandboxFileSystem 在离线时能从缓存中找到清单。
    /// 兼容两种内置资源布局：根目录和包名子目录。
    /// </summary>
    private IEnumerator RequestBuiltinPackageVersion(string packageName)
    {
        string yooFolder = "yoo";
        string builtinBase;
        string sandboxCacheRoot;

#if UNITY_ANDROID && !UNITY_EDITOR
        builtinBase = Application.streamingAssetsPath + "/" + yooFolder;
        sandboxCacheRoot = Application.persistentDataPath + "/" + yooFolder + "/" + packageName + "/ManifestFiles";
#else
        builtinBase = Application.streamingAssetsPath + "/" + yooFolder;
        sandboxCacheRoot = Application.dataPath + "/" + yooFolder + "/" + packageName + "/ManifestFiles";
#endif

        // 兼容两种布局: 新版构建放在 {builtinBase}/{packageName}/, 旧版直接在 {builtinBase}/
        string[] versionPaths = new string[]
        {
            builtinBase + "/" + packageName + "/" + packageName + ".version",
            builtinBase + "/" + packageName + ".version"
        };

        string versionFilePath = null;

        // ── Step 1: 查找并读取内置版本号 ──
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (var path in versionPaths)
        {
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    RemoteVersion = www.downloadHandler.text.Trim();
                    versionFilePath = path;
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(RemoteVersion))
        {
            Debug.LogError("[PatchManager] Failed to read builtin version from any location.");
            yield break;
        }
#else
        foreach (var path in versionPaths)
        {
            if (System.IO.File.Exists(path))
            {
                versionFilePath = path;
                try
                {
                    RemoteVersion = System.IO.File.ReadAllText(path).Trim();
                    break;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PatchManager] Failed to read builtin version '{path}': {e.Message}");
                }
            }
        }
        if (string.IsNullOrEmpty(RemoteVersion))
        {
            Debug.LogError($"[PatchManager] Builtin version file not found in any location.");
            yield break;
        }
#endif

        Debug.Log($"[PatchManager] Builtin version: {RemoteVersion} (from {versionFilePath})");

        // ── Step 2: 确定源文件目录（与版本文件同目录）──
        string sourceDir = System.IO.Path.GetDirectoryName(versionFilePath);
        string hashFileName = $"{packageName}_{RemoteVersion}.hash";
        string manifestFileName = $"{packageName}_{RemoteVersion}.bytes";

        string srcHashPath = sourceDir + "/" + hashFileName;
        string srcManifestPath = sourceDir + "/" + manifestFileName;
        string dstHashPath = sandboxCacheRoot + "/" + hashFileName;
        string dstManifestPath = sandboxCacheRoot + "/" + manifestFileName;

        // ── Step 3: 拷贝内置清单文件到沙盒缓存目录 ──
        try
        {
            if (!System.IO.Directory.Exists(sandboxCacheRoot))
                System.IO.Directory.CreateDirectory(sandboxCacheRoot);

#if UNITY_ANDROID && !UNITY_EDITOR
            yield return CopyFileFromStreamingAssets(srcHashPath, dstHashPath);
            yield return CopyFileFromStreamingAssets(srcManifestPath, dstManifestPath);
#else
            System.IO.File.Copy(srcHashPath, dstHashPath, true);
            System.IO.File.Copy(srcManifestPath, dstManifestPath, true);
#endif
            Debug.Log($"[PatchManager] Builtin manifest copied to sandbox cache: {sandboxCacheRoot}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PatchManager] Failed to copy builtin manifest: {e.Message}");
            RemoteVersion = string.Empty;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator CopyFileFromStreamingAssets(string sourcePath, string destPath)
    {
        using (var www = UnityEngine.Networking.UnityWebRequest.Get(sourcePath))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                System.IO.File.WriteAllBytes(destPath, www.downloadHandler.data);
            }
            else
            {
                Debug.LogError($"[PatchManager] Failed to copy builtin file '{sourcePath}': {www.error}");
            }
        }
    }
#endif

    private IEnumerator LoadManifestAndConfig(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            Debug.LogError("[PatchManager] Cannot load manifest with empty version.");
            yield break;
        }

        var options = new LoadPackageManifestOptions(version, 60);
        var manifestOp = _package.LoadPackageManifestAsync(options);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Manifest load failed for version {version}: {manifestOp.Error}");
        }
        else
        {
            Debug.Log($"[PatchManager] Manifest loaded: version={version}");
        }

        // 尝试从已加载的资源中读取 RemoteConfig
        TryLoadRemoteConfigFromPackage();
    }

    private void TryLoadRemoteConfigFromPackage()
    {
        if (_remoteConfig != null) return;

        try
        {
            // 地址格式由 BundleCollector.AddressByFolderAndFileName 生成:
            // Assets/GameAssets/Boot/RemoteConfig.asset → "Boot_RemoteConfig"
            var handle = _package.LoadAssetSync<RemoteConfig>("Boot_RemoteConfig");
            if (handle.Status == EOperationStatus.Succeeded && handle.AssetObject != null)
            {
                _remoteConfig = handle.AssetObject as RemoteConfig;
                UpdateStrategy = _remoteConfig.UpdateStrategy;
                _maxRetryCount = _remoteConfig.MaxRetryCount;

                // 如果 RemoteConfig 中有不同的远端 URL，更新 ConfigurableRemoteService
                if (!string.IsNullOrEmpty(_remoteConfig.UpdateServerURL))
                {
                    var configUrl = _remoteConfig.GetUpdateServerURL();
                    if (configUrl != _remoteBaseUrl.TrimEnd('/'))
                    {
                        Debug.Log($"[PatchManager] RemoteConfig URL differs from initial URL. "
                                 + $"Config: {configUrl}, Initial: {_remoteBaseUrl}. "
                                 + "Updating remote service now.");
                        // 立即更新远端服务 URL（当次启动生效）
                        _remoteService?.UpdateBaseUrl(configUrl);
                        _remoteBaseUrl = configUrl;
                        // 同时保存到 PlayerPrefs 供下次启动使用
                        PlayerPrefs.SetString($"RemoteURL_{_packageName}", configUrl);
                        PlayerPrefs.Save();
                    }
                }

                Debug.Log($"[PatchManager] Loaded RemoteConfig: strategy={UpdateStrategy}, "
                         + $"retry={_maxRetryCount}, url={_remoteConfig.GetUpdateServerURL()}");
            }
            handle.Release();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PatchManager] Failed to load RemoteConfig, using defaults: {e.Message}");
        }
    }

    #endregion

    #region Download

    private IEnumerator DownloadAndApplyUpdate()
    {
        // 加载远端清单
        var manifestOptions = new LoadPackageManifestOptions(RemoteVersion, 60);
        var manifestOp = _package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Failed to load remote manifest: {manifestOp.Error}");
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadFailed, this, manifestOp.Error, _retryCount);
            yield break;
        }

        // 创建下载器
        var downloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));
        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("[PatchManager] No files need downloading (manifest was already cached).");
            SaveLocalVersion(_packageName, RemoteVersion);
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadComplete, this);
            yield break;
        }

        TotalDownloadCount = downloader.TotalDownloadCount;
        TotalDownloadSize = downloader.TotalDownloadBytes;

        Debug.Log($"[PatchManager] Downloading {TotalDownloadCount} files, {FormatSize(TotalDownloadSize)}.");

        // 广播下载开始
        EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadStart, this, TotalDownloadCount, TotalDownloadSize);

        // 注册进度回调
        downloader.DownloadProgressChanged += OnDownloadProgress;
        downloader.DownloadCompleted += OnDownloadCompleted;

        // 开始下载
        downloader.StartDownload();
        yield return downloader;
    }

    private void OnDownloadProgress(DownloadProgressChangedEventArgs args)
    {
        EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadProgress, this,
            args.CurrentDownloadCount, args.TotalDownloadCount,
            args.CurrentDownloadBytes, args.TotalDownloadBytes);
    }

    private void OnDownloadCompleted(DownloadCompletedEventArgs args)
    {
        if (args.Succeeded)
        {
            Debug.Log($"[PatchManager] Download completed.");
            SaveLocalVersion(_packageName, RemoteVersion);
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadComplete, this);
        }
        else
        {
            Debug.LogError($"[PatchManager] Download failed: {args.Error}");
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadFailed, this, args.Error, _retryCount);
        }
    }

    #endregion

    /// <summary>
    /// 等待用户点击跳过（服务器不可达时）
    /// </summary>
    private IEnumerator WaitForUserSkip()
    {
        UserSkippedUpdate = false;
        float waitTime = 0f;
        const float maxWaitTime = 120f;

        while (!UserSkippedUpdate && waitTime < maxWaitTime)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (!UserSkippedUpdate)
        {
            Debug.Log("[PatchManager] User skip timeout, continuing with local resources.");
        }
    }

    #region User Decision (Optional Strategy)

    private IEnumerator WaitForUserDecision()
    {
        // 等待用户通过 UI 设置 UserAgreedUpdate
        UserAgreedUpdate = false;

        float waitTime = 0f;
        const float maxWaitTime = 120f; // 最多等 2 分钟

        while (!UserAgreedUpdate && waitTime < maxWaitTime)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (UserAgreedUpdate)
        {
            Debug.Log("[PatchManager] User agreed to update, starting download...");
            yield return DownloadAndApplyUpdate();
        }
        else
        {
            Debug.Log("[PatchManager] User declined or timeout, using local resources.");
            NeedUpdate = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 根据远端配置获取最佳远端 URL
    /// </summary>
    public static string GetBestRemoteUrl(string packageName, string defaultUrl = "http://127.0.0.1/")
    {
        // 优先使用上次从 RemoteConfig 保存的 URL
        string savedUrl = PlayerPrefs.GetString($"RemoteURL_{packageName}", "");
        if (!string.IsNullOrEmpty(savedUrl))
            return savedUrl;

        return defaultUrl;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
        return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
    }

    #endregion
}

/// <summary>
/// 可配置的远端资源地址查询服务。
/// 与 DefaultRemoteService 功能相同，但支持运行时更新 URL。
/// </summary>
public class ConfigurableRemoteService : IRemoteService
{
    private string _baseUrl;

    public ConfigurableRemoteService(string baseUrl)
    {
        _baseUrl = (baseUrl ?? "http://127.0.0.1/").TrimEnd('/');
    }

    /// <summary>
    /// 更新远端 URL（供 RemoteConfig 加载后调用）
    /// </summary>
    public void UpdateBaseUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            _baseUrl = url.TrimEnd('/');
        }
    }

    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        return new System.Collections.Generic.List<string> { $"{_baseUrl}/{fileName}" };
    }
}
