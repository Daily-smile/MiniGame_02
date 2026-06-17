using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace LF.Framework
{
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

    /// <summary>
    /// 用户是否已点击"进入游戏"（下载完成后由 UI 设置）
    /// </summary>
    public bool UserReadyToEnter { get; set; }

    /// <summary>
    /// 用户是否已点击重试（下载失败后由 UI 设置）
    /// </summary>
    public bool UserRequestedRetry { get; set; }

    /// <summary>
    /// 当前下载是否成功（DoDownload 执行后读取）
    /// </summary>
    private bool _downloadSuccess;
    private bool _lastManifestLoadSuccess;

    /// <summary>
    /// 资源包初始化是否成功（InitPackageAsync 完成后读取，true 表示资源和 UI 可正常加载）
    /// </summary>
    public bool InitSuccess { get; private set; } = true;

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
        // 检查 HybridCLRPreloader 是否已在 BeforeSceneLoad 阶段创建包
        if (HybridCLRPreloader.IsLoaded && HybridCLRPreloader.BootstrapPackage != null
            && packageName == HybridCLRPreloader.PackageName)
        {
            _package = HybridCLRPreloader.BootstrapPackage;
            // 远端服务已使用 PlayerPrefs 缓存的 URL，需要时更新为传入的 URL
            if (!string.IsNullOrEmpty(remoteBaseUrl))
            {
                var newService = new ConfigurableRemoteService(remoteBaseUrl);
                _remoteService = newService;
            }
            ResourceManager.Instance.SetPackage(_package);
            Debug.Log("[PatchManager] Reusing preloaded package.");
            yield break;
        }

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
        _downloadSuccess = false;
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
            InitSuccess = false;
            yield break;
        }

        // Editor 下请求版本号 + 加载清单
        var versionOp = _package.RequestPackageVersionAsync();
        yield return versionOp;

        if (versionOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Editor version request failed: {versionOp.Error}");
            InitSuccess = false;
            yield break;
        }

        var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, 60);
        var manifestOp = _package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Editor manifest load failed: {manifestOp.Error}");
            InitSuccess = false;
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
            InitSuccess = false;
            yield break;
        }

        Debug.Log("[PatchManager] Package initialized.");

        // 获取用于加载初始清单的版本号
        LocalVersion = GetLocalVersion(packageName);
        string loadVersion = LocalVersion;

        // PlayerPrefs 有版本号时，先验证该版本的清单是否可用。
        // App 覆盖安装后内置版本可能已变，PlayerPrefs 里的旧版本清单会找不到，
        // 此时需要清除旧记录并回退到内置版本。
        if (!string.IsNullOrEmpty(loadVersion))
        {
            yield return LoadManifestAndConfig(loadVersion);
            if (!_lastManifestLoadSuccess)
            {
                Debug.LogWarning($"[PatchManager] Stored version '{loadVersion}' manifest unavailable, "
                               + "clearing and falling back to builtin (app may have been updated).");
                PlayerPrefs.DeleteKey($"ResourceVersion_{packageName}");
                LocalVersion = "";
                loadVersion = "";
            }
        }

        if (string.IsNullOrEmpty(loadVersion))
        {
            // 首次启动 / 旧版本失效：优先从内置资源（StreamingAssets）加载清单
            yield return RequestBuiltinPackageVersion(packageName);
            loadVersion = RemoteVersion;

            // 内置资源不可用时回退到远端请求
            if (string.IsNullOrEmpty(loadVersion))
            {
                Debug.Log("[PatchManager] Builtin version unavailable, falling back to remote...");
                yield return RequestRemoteVersion();
                loadVersion = RemoteVersion;
            }
            else
            {
                // 将内置版本号持久化，避免下次启动时因 LocalVersion 为空而误判为需要更新
                SaveLocalVersion(packageName, loadVersion);
                LocalVersion = loadVersion;
            }
        }

        // 所有版本获取途径均失败，无法加载任何资源
        if (string.IsNullOrEmpty(loadVersion))
        {
            Debug.LogError("[PatchManager] No version available — no builtin resources and no network.");
            InitSuccess = false;
            yield break;
        }

        // 非首次启动且旧版本清单已验证可用，跳过重复加载
        if (!_lastManifestLoadSuccess)
        {
            yield return LoadManifestAndConfig(loadVersion);
        }

        if (!_lastManifestLoadSuccess)
        {
            Debug.LogError($"[PatchManager] Manifest load failed, cannot initialize resources.");
            InitSuccess = false;
            yield break;
        }

        // 注入 ResourceManager，使 UI 等资源可加载
        ResourceManager.Instance.SetPackage(_package);
        Debug.Log("[PatchManager] Package ready, resources loadable.");
    }

    #endregion

    #region Runtime Mode — Phase 2: Check & Download

    /// <summary>
    /// 远端版本检查 + 按策略下载（通过 EventDispatcher 广播事件给 UI）。
    /// 新流程：
    ///   1. 请求远端版本号 → 不可达则等用户跳过（等待期间持续后台重试）
    ///   2. 有更新时预计算下载大小 → 自动开始下载
    ///   3. 下载（支持重试）→ 完成后等用户"点击进入游戏"
    ///   4. 下载失败 → 等用户选择重试或跳过
    /// </summary>
    private IEnumerator CheckAndDownloadUpdates()
    {
        Debug.Log($"[PatchManager] Checking remote version...");

        // ── Step 1: 请求远端版本号 ──
        yield return RequestRemoteVersion();

        // ── Step 2: 远端版本为空说明更新服务器不可达，等待用户点击跳过 ──
        if (string.IsNullOrEmpty(RemoteVersion))
        {
            LocalVersion = GetLocalVersion(_packageName);
            Debug.Log("[PatchManager] Update server unreachable, waiting for user to skip...");
            EventDispatcher.PostEvent(MessageEvent.OnPatchVersionGet, this, RemoteVersion, LocalVersion);
            yield return WaitForUserSkip();

            // 用户点击了跳过，取消监听，跳过热更进入游戏
            if (UserSkippedUpdate || string.IsNullOrEmpty(RemoteVersion))
            {
                yield break;
            }
            // 后台重试成功，继续执行下方版本对比（各分支有自己的广播）
        }

        // ── Step 3: 对比版本 ──
        LocalVersion = GetLocalVersion(_packageName);
        Debug.Log($"[PatchManager] Remote version: {RemoteVersion}, Local version: {LocalVersion}");

        // 仅当远端版本严格高于本地时才更新，避免降级
        // 版本号格式为 yyyy-MM-dd-HHmm，字符串可直接比较大小
        NeedUpdate = string.Compare(RemoteVersion, LocalVersion) > 0;

        if (!NeedUpdate)
        {
            Debug.Log("[PatchManager] Version up to date, verifying resource integrity...");

            // ── 校验资源完整性 + 自动修复损坏/缺失文件 ──
            yield return VerifyAndRepairResources();

            // ── 清理旧版本残留文件（仅在清单可用时执行，避免误删）──
            if (_lastManifestLoadSuccess)
            {
                yield return ClearUnusedCacheAsync();
            }

            // 校验/修复完成，等待用户点击进入
            EventDispatcher.PostEvent(MessageEvent.OnPatchVersionGet, this, RemoteVersion, LocalVersion);
            yield return WaitForUserReadyToEnter();
            yield break;
        }

        // ── Step 4: 有更新，预计算下载大小，直接进入下载 ──
        yield return PreComputeDownloadSize();
        Debug.Log($"[PatchManager] Update available: {TotalDownloadCount} files, {FormatSize(TotalDownloadSize)}");
        EventDispatcher.PostEvent(MessageEvent.OnPatchVersionGet, this, RemoteVersion, LocalVersion, TotalDownloadCount, TotalDownloadSize);

        // ── Step 5: 下载循环（支持重试）──
        while (true)
        {
            yield return DoDownload();

            if (_downloadSuccess)
            {
                // 下载成功，重新加载清单
                yield return LoadManifestAndConfig(RemoteVersion);

                // 清单重载失败则回到重试循环（可能网络波动导致 hash 文件下载不完整）
                if (!_lastManifestLoadSuccess)
                {
                    Debug.LogError("[PatchManager] Manifest reload failed after download, retrying...");
                    EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadFailed, this,
                        "清单加载失败，请重试", _retryCount);
                    continue;
                }

                ResourceManager.Instance.SetPackage(_package);

                // 等待用户"点击进入游戏"
                Debug.Log("[PatchManager] Download complete, waiting for user to enter game...");
                EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadComplete, this);
                yield return WaitForUserReadyToEnter();
                yield break;
            }
            else
            {
                // 下载失败，等待用户选择重试或跳过
                Debug.Log("[PatchManager] Download failed, waiting for retry or skip...");
                yield return WaitForRetryOrSkip();
                if (UserSkippedUpdate)
                {
                    NeedUpdate = false;
                    yield break;
                }
                // 否则：继续循环（重试）
            }
        }
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

        // 服务器不可达时 RemoteVersion 保持为空，让调用方走"服务器不可达"分支
        // 不再用本地版本兜底——Phase 1 已将内置版本存入 PlayerPrefs，
        // 兜底会掩盖"服务器连不上"的事实，误导用户以为检查成功
        RemoteVersion = null;
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
        _lastManifestLoadSuccess = false;

        if (string.IsNullOrEmpty(version))
        {
            Debug.LogError("[PatchManager] Cannot load manifest with empty version.");
            yield break;
        }

        // 超时 10 秒：内置清单本地读取极快，远端清单文件很小，10 秒足够
        var options = new LoadPackageManifestOptions(version, 10);
        var manifestOp = _package.LoadPackageManifestAsync(options);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Manifest load failed for version {version}: {manifestOp.Error}");
            yield break;
        }

        _lastManifestLoadSuccess = true;
        Debug.Log($"[PatchManager] Manifest loaded: version={version}");

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

    private IEnumerator DoDownload()
    {
        _downloadSuccess = false;

        // 加载远端清单（重试时重新拉取，确保下载器拿到最新缓存状态）
        var manifestOptions = new LoadPackageManifestOptions(RemoteVersion, 10);
        var manifestOp = _package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"[PatchManager] Failed to load remote manifest: {manifestOp.Error}");
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadFailed, this, manifestOp.Error, _retryCount);
            yield break;
        }

        // 创建下载器（YooAsset 自动跳过已缓存的文件，天然支持断点续传）
        var downloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));
        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("[PatchManager] No files need downloading (all cached).");
            SaveLocalVersion(_packageName, RemoteVersion);
            _downloadSuccess = true;
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

        // 开始下载（try-finally 确保异常时也能清理回调，避免重试时重复注册）
        try
        {
            downloader.StartDownload();
            yield return downloader;
        }
        finally
        {
            downloader.DownloadProgressChanged -= OnDownloadProgress;
            downloader.DownloadCompleted -= OnDownloadCompleted;
        }
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
            _downloadSuccess = true;
        }
        else
        {
            Debug.LogError($"[PatchManager] Download failed: {args.Error}");
            _downloadSuccess = false;
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadFailed, this, args.Error, _retryCount);
        }
    }

    #endregion

    /// <summary>
    /// 等待用户点击跳过（服务器不可达时）。
    /// 在等待期间持续重试连接热更服务器，直到连接成功或用户点击跳过。
    /// </summary>
    private IEnumerator WaitForUserSkip()
    {
        UserSkippedUpdate = false;
        const float retryInterval = 0.5f; // 每0.5秒重试一次连接
        float timeSinceLastRetry = 0f;

        while (!UserSkippedUpdate)
        {
            timeSinceLastRetry += Time.deltaTime;

            if (timeSinceLastRetry >= retryInterval)
            {
                timeSinceLastRetry = 0f;

                // 单次版本请求，不通过 RequestRemoteVersion（它有内部 3 次重试，会阻塞太久）
                var versionOp = _package.RequestPackageVersionAsync();
                yield return versionOp;

                // 网络等待期间用户可能已点跳过，立刻检查
                if (UserSkippedUpdate)
                    yield break;

                if (versionOp.Status == EOperationStatus.Succeeded)
                {
                    RemoteVersion = versionOp.PackageVersion;
                    Debug.Log("[PatchManager] Server became reachable during wait.");
                    yield break;
                }
            }
            yield return null;
        }
    }

    #region User Decision (Optional Strategy)

    /// <summary>
    /// 预计算待下载的文件数和大小（不实际下载）。
    /// 有更新时先调用此方法，再通过 OnPatchVersionGet 事件将信息传给 UI。
    /// </summary>
    private IEnumerator PreComputeDownloadSize()
    {
        TotalDownloadCount = 0;
        TotalDownloadSize = 0;

        var manifestOptions = new LoadPackageManifestOptions(RemoteVersion, 10);
        var manifestOp = _package.LoadPackageManifestAsync(manifestOptions);
        yield return manifestOp;

        if (manifestOp.Status == EOperationStatus.Succeeded)
        {
            var downloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));
            TotalDownloadCount = downloader.TotalDownloadCount;
            TotalDownloadSize = downloader.TotalDownloadBytes;
            Debug.Log($"[PatchManager] Pre-computed download size: {TotalDownloadCount} files, {FormatSize(TotalDownloadSize)}");
        }
    }

    /// <summary>
    /// 等待用户点击"进入游戏"（下载完成后调用）
    /// </summary>
    private IEnumerator WaitForUserReadyToEnter()
    {
        UserReadyToEnter = false;
        while (!UserReadyToEnter)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 等待用户选择重试或跳过（下载失败后调用）
    /// </summary>
    private IEnumerator WaitForRetryOrSkip()
    {
        UserRequestedRetry = false;
        UserSkippedUpdate = false;

        float waitTime = 0f;
        const float maxWaitTime = 120f;

        while (!UserRequestedRetry && !UserSkippedUpdate && waitTime < maxWaitTime)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (!UserRequestedRetry && !UserSkippedUpdate)
        {
            Debug.Log("[PatchManager] Retry/skip timeout, treating as skip.");
            UserSkippedUpdate = true;
        }
    }

    #endregion

    #region Verification & Repair

    /// <summary>
    /// 校验本地资源文件完整性，自动修复损坏或缺失的文件。
    /// 仅当版本号一致时调用——版本一致不代表文件完好（可能被误删或磁盘损坏）。
    /// </summary>
    private IEnumerator VerifyAndRepairResources()
    {
        // 通知 UI 开始校验
        EventDispatcher.PostEvent(MessageEvent.OnPatchVerifyStart, this);

        // 确保清单已加载（对比需要基于当前版本的远端清单）
        yield return LoadManifestAndConfig(RemoteVersion);
        if (!_lastManifestLoadSuccess)
        {
            Debug.LogError("[PatchManager] Cannot load manifest for verification, skipping.");
            yield break;
        }

        // 创建下载器：对比本地文件与远端清单的 hash
        // 若无远端访问，downloader 会返回 0 个文件，下方判断会自然跳过
        var downloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));

        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("[PatchManager] All resources verified intact — no repair needed.");
            yield break;
        }

        // 发现损坏/缺失文件，自动修复
        TotalDownloadCount = downloader.TotalDownloadCount;
        TotalDownloadSize = downloader.TotalDownloadBytes;
        Debug.Log($"[PatchManager] Found {TotalDownloadCount} damaged/missing files ({FormatSize(TotalDownloadSize)}), auto-repairing...");

        EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadStart, this, TotalDownloadCount, TotalDownloadSize);

        // 使用局部方法，避免触发 OnPatchDownloadFailed（修复失败直接继续进游戏，不弹重试 UI）
        bool repairOk = false;

        void OnRepairCompleted(DownloadCompletedEventArgs args)
        {
            if (args.Succeeded)
            {
                SaveLocalVersion(_packageName, RemoteVersion);
                repairOk = true;
            }
            else
            {
                Debug.LogWarning($"[PatchManager] Repair download failed: {args.Error}");
            }
        }

        downloader.DownloadProgressChanged += OnDownloadProgress;
        downloader.DownloadCompleted += OnRepairCompleted;
        try
        {
            downloader.StartDownload();
            yield return downloader;
        }
        finally
        {
            downloader.DownloadProgressChanged -= OnDownloadProgress;
            downloader.DownloadCompleted -= OnRepairCompleted;
        }

        if (repairOk)
        {
            yield return LoadManifestAndConfig(RemoteVersion);
            if (_lastManifestLoadSuccess)
            {
                ResourceManager.Instance.SetPackage(_package);
            }
            EventDispatcher.PostEvent(MessageEvent.OnPatchDownloadComplete, this);
            Debug.Log("[PatchManager] Resource repair completed.");
        }
        else
        {
            Debug.LogWarning("[PatchManager] Resource repair failed, continuing with local resources.");
        }
    }

    /// <summary>
    /// 清理沙盒缓存中不被当前资源清单引用的残留文件（旧版本残留）。
    /// </summary>
    private IEnumerator ClearUnusedCacheAsync()
    {
        Debug.Log("[PatchManager] Cleaning unused cache files...");
        var options = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
        var clearOp = _package.ClearCacheAsync(options);
        yield return clearOp;

        if (clearOp.Status == EOperationStatus.Succeeded)
        {
            Debug.Log("[PatchManager] Unused cache files cleaned.");
        }
        else
        {
            Debug.LogWarning($"[PatchManager] Cache cleanup failed: {clearOp.Error}");
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
        _baseUrl = (baseUrl ?? "http://127.0.0.1/").Trim().TrimEnd('/');
    }

    /// <summary>
    /// 更新远端 URL（供 RemoteConfig 加载后调用）
    /// </summary>
    public void UpdateBaseUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            _baseUrl = url.Trim().TrimEnd('/');
        }
    }

    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        // YooAsset 内部对版本文件使用了 "{PackageName}/{PackageName}.version" 路径，
        // 而对 hash/manifest/bundle 使用扁平路径。统一取文件名，让所有资源都能在服务器根目录找到。
        string flatName = System.IO.Path.GetFileName(fileName);
        return new System.Collections.Generic.List<string> { $"{_baseUrl}/{flatName}" };
    }
}
}
