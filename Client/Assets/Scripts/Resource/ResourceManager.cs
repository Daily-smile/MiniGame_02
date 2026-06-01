using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 资源管理器 - 统一资源加载入口
/// 资源目录约定: Assets/Resources/ 用于运行时加载, Assets/Res/ 用于编辑器参考资源
/// LoadAsset路径为Resources相对路径, 例: LoadAsset("Panel/LoginPanel")
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
    // 资源缓存
    private Dictionary<string, ResourceHandle> resourceCache = new Dictionary<string, ResourceHandle>();
    // 正在加载的资源回调列表
    private Dictionary<string, List<Action<UnityEngine.Object>>> loadingCallbacks = new Dictionary<string, List<Action<UnityEngine.Object>>>();
    // 资源加载器（可根据不同平台或加载方式实现）
    private IResourceLoader resourceLoader;
    // 添加依赖管理
    private Dictionary<string, List<string>> dependencyMap = new Dictionary<string, List<string>>();
    // 声明内存临界事件
    public static event Action<long> OnMemoryCritical;
    // 当前总内存使用量（字节）
    private long totalMemoryUsage = 0;
    // 内存阈值（100MB），可根据需要调整
    private long memoryThreshold = 100 * 1024 * 1024;
    // 获取或设置内存阈值（以MB为单位，便于理解）
    public long MemoryThresholdMB
    {
        get { return memoryThreshold / (1024 * 1024); }
        set { memoryThreshold = value * 1024 * 1024; }
    }
    void Awake()
    {
        // 根据平台初始化不同的资源加载器
#if UNITY_EDITOR
        resourceLoader = new EditorResourceLoader();
#else
        resourceLoader = new RuntimeResourceLoader();
#endif
        Initialize();
    }
    public void Initialize()
    {
        resourceCache.Clear();
        loadingCallbacks.Clear();
    }
    #region 资源管理相关功能API
    /// <summary>
    /// 添加资源到管理中
    /// </summary>
    public void AddAsset(string path, UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return;
        }
        if (!resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = new ResourceHandle(path, asset);
            resourceCache[path] = handle;
        }
    }
    // 同步加载资源
    public T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Resource path is null or empty!");
            return null;
        }
        // 检查缓存
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            handle.AddReference();
            return handle.Asset as T;
        }
        // 加载资源
        ResourceHandle newHandle = resourceLoader.LoadAsset<T>(path);
        if (newHandle != null && newHandle.Asset != null)
        {
            resourceCache[path] = newHandle;
            return newHandle.Asset as T;
        }
        Debug.LogError($"Failed to load resource: {path}");
        return null;
    }
    // 异步加载资源
    public void LoadAssetAsync<T>(string path, Action<T> callback) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Resource path is null or empty!");
            callback?.Invoke(null);
            return;
        }
        // 如果资源已在缓存中
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            handle.AddReference();
            callback?.Invoke(handle.Asset as T);
            return;
        }
        // 如果资源正在加载中，添加回调到列表
        if (loadingCallbacks.ContainsKey(path))
        {
            loadingCallbacks[path].Add((obj) => callback?.Invoke(obj as T));
            return;
        }
        // 创建新的回调列表并开始加载
        loadingCallbacks[path] = new List<Action<UnityEngine.Object>>();
        loadingCallbacks[path].Add((obj) => callback?.Invoke(obj as T));
        resourceLoader.LoadAssetAsync<T>(path, (handle) =>
        {
            if (handle != null && handle.Asset != null)
            {
                resourceCache[path] = handle;
                // 调用所有等待的回调
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
                // 加载失败，也需要调用回调
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
    // 卸载资源
    public void UnloadAsset(string path)
    {
        if (resourceCache.ContainsKey(path))
        {
            ResourceHandle handle = resourceCache[path];
            if (handle.Release())
            {
                resourceCache.Remove(path);
                resourceLoader.UnloadAsset(handle);
            }
        }
    }
    // 卸载所有未使用的资源
    public void UnloadUnusedAssets()
    {
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in resourceCache)
        {
            if (kvp.Value.ReferenceCount <= 0)
            {
                keysToRemove.Add(kvp.Key);
                resourceLoader.UnloadAsset(kvp.Value);
            }
        }
        foreach (string key in keysToRemove)
        {
            resourceCache.Remove(key);
        }
        Resources.UnloadUnusedAssets();
    }
    // 预加载资源
    public void PreloadAssets(List<string> paths)
    {
        foreach (string path in paths)
        {
            if (!resourceCache.ContainsKey(path))
            {
                // 使用异步加载但不立即使用，只是提前加载到缓存
                LoadAssetAsync<UnityEngine.Object>(path, null);
            }
        }
    }
    // 获取资源信息（用于调试或监控）
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
        // 这里实现计算资源内存占用的逻辑
        // 实际项目中可能需要更复杂的内存计算
        if (obj is Texture2D texture)
        {
            return texture.width * texture.height * 4; // 近似计算
        }
        return 0;
    }
    #endregion
    #region 内存监测以及相关
    /// <summary>
    /// 检查当前内存使用情况，并在超过阈值时触发事件
    /// </summary>
    public void CheckMemoryUsage()
    {
        totalMemoryUsage = 0;
        foreach (var kvp in resourceCache)
        {
            totalMemoryUsage += CalculateMemorySize(kvp.Value.Asset);
        }
        Debug.Log($"当前内存使用量: {totalMemoryUsage / 1024 / 1024}MB / {MemoryThresholdMB}MB");
        if (IsMemoryCritical())
        {
            Debug.LogWarning($"内存使用已达临界值: {totalMemoryUsage / 1024 / 1024}MB");
            // 触发事件，通知所有订阅者当前内存使用量
            OnMemoryCritical?.Invoke(totalMemoryUsage);
        }
    }
    /// <summary>
    /// 判断内存是否达到临界状态
    /// </summary>
    public bool IsMemoryCritical()
    {
        return totalMemoryUsage >= memoryThreshold;
    }
    // 可选的协程，用于定期检查内存
    private IEnumerator MemoryCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // 每5秒检查一次
            CheckMemoryUsage();
        }
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
            // 增加依赖项的引用计数
            if (resourceCache.ContainsKey(dependencyPath))
            {
                resourceCache[dependencyPath].AddReference();
            }
        }
    }
    // 在卸载资源时处理依赖关系
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
// 资源信息结构
public struct ResourceInfo
{
    public string Path;
    public int ReferenceCount;
    public long MemorySize;
}
/* 内存监测使用示例
// GameMemoryMonitor.cs
using UnityEngine;
public class GameMemoryMonitor : MonoBehaviour
{
    void Start()
    {
        // 订阅内存临界事件
        ResourceManager.OnMemoryCritical += HandleMemoryCritical;
        
        // 可选：启动ResourceManager的内存检查协程
        // ResourceManager.Instance.StartCoroutine(ResourceManager.Instance.MemoryCheckRoutine());
    }
    void OnDestroy()
    {
        // 取消订阅，避免内存泄漏
        ResourceManager.OnMemoryCritical -= HandleMemoryCritical;
    }
    /// <summary>
    /// 处理内存达到临界值的回调方法
    /// </summary>
    /// <param name="currentMemoryUsage">当前内存使用量（字节）</param>
    private void HandleMemoryCritical(long currentMemoryUsage)
    {
        Debug.LogWarning($"内存不足警告已接收！当前使用: {currentMemoryUsage / 1024 / 1024}MB");
        
        // 执行紧急内存释放操作：
        // 1. 强制进行垃圾回收（对托管堆有效）
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        // 2. 卸载所有未使用的资源（非常重要！）
        Resources.UnloadUnusedAssets();:cite[10]
        
        // 3. 清理ResourceManager中未被引用的缓存资源
        ResourceManager.Instance.UnloadUnusedAssets();:cite[10]
        
        // 4. （可选）可以根据当前使用量决定是否卸载一些非关键资源
        // UnloadNonCriticalResources();
        
        Debug.Log("紧急内存清理操作已完成.");
    }
    
    // 示例：卸载非关键资源的方法
    private void UnloadNonCriticalResources()
    {
        // 例如：卸载所有不在当前场景使用的UI纹理
        // ResourceManager.Instance.UnloadAsset("Textures/UI/Background");
        // 请根据你的游戏逻辑具体实现
    }
}
*/