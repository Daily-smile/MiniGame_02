using System;
using System.Collections.Generic;
using UnityEngine;
public interface IResourceLoader
{
    ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object;
    void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object;
    void UnloadAsset(ResourceHandle handle);
}
// 编辑器资源加载器（使用Resources和AssetDatabase）
public class EditorResourceLoader : IResourceLoader
{
    public ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = Resources.Load<T>(path);
        if (asset != null)
        {
            return new ResourceHandle(path, asset);
        }
#if UNITY_EDITOR
        // 尝试使用AssetDatabase作为备选方案
        asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return new ResourceHandle(path, asset);
        }
#endif
        return null;
    }
    public void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        // 在编辑器中，我们可以使用协程模拟异步加载
        ResourceManager.Instance.StartCoroutine(LoadAsyncCoroutine<T>(path, callback));
    }
    private System.Collections.IEnumerator LoadAsyncCoroutine<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        // 模拟异步加载延迟
        yield return new WaitForSeconds(0.1f);
        ResourceHandle handle = LoadAsset<T>(path);
        callback?.Invoke(handle);
    }
    public void UnloadAsset(ResourceHandle handle)
    {
        if (handle != null && handle.Asset != null)
        {
            // 对于Resources加载的资源，使用UnloadAsset
            if (!(handle.Asset is GameObject || handle.Asset is Component))
            {
                Resources.UnloadAsset(handle.Asset);
            }
        }
    }
}
// 运行时资源加载器（支持AssetBundle）
public class RuntimeResourceLoader : IResourceLoader
{
    private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
    public ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        // 实现从AssetBundle加载资源的逻辑
        // 这里需要解析路径获取bundle名和资源名
        // 简化实现：直接使用Resources
        T asset = Resources.Load<T>(path);
        if (asset != null)
        {
            return new ResourceHandle(path, asset);
        }
        return null;
    }
    public void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        // 实现异步从AssetBundle加载资源
        ResourceManager.Instance.StartCoroutine(LoadFromBundleAsync<T>(path, callback));
    }
    private System.Collections.IEnumerator LoadFromBundleAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        // 解析bundle名和资源名
        // string bundleName = GetBundleNameFromPath(path);
        // string assetName = GetAssetNameFromPath(path);
        // 简化实现：使用Resources异步加载
        ResourceRequest request = Resources.LoadAsync<T>(path);
        yield return request;
        if (request.asset != null)
        {
            callback?.Invoke(new ResourceHandle(path, request.asset));
        }
        else
        {
            callback?.Invoke(null);
        }
    }
    public void UnloadAsset(ResourceHandle handle)
    {
        // 实现AssetBundle资源的卸载
        // 需要处理依赖关系
        if (handle != null && handle.Asset != null)
        {
            // 对于非GameObject/Component资源，使用UnloadAsset
            if (!(handle.Asset is GameObject || handle.Asset is Component))
            {
                Resources.UnloadAsset(handle.Asset);
            }
        }
    }
}
/*// Addressables资源加载器
public class AddressablesResourceLoader : IResourceLoader
{
    public ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        // 使用Addressables同步加载（注意：Addressables主要支持异步）
        var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(path);
        op.WaitForCompletion();
        if (op.Result != null)
        {
            return new ResourceHandle(path, op.Result as UnityEngine.Object);
        }
        return null;
    }
    public void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(path);
        op.Completed += (operation) =>
        {
            if (operation.Result != null)
            {
                callback?.Invoke(new ResourceHandle(path, operation.Result as Object));
            }
            else
            {
                callback?.Invoke(null);
            }
        };
    }
    public void UnloadAsset(ResourceHandle handle)
    {
        if (handle != null && handle.Asset != null)
        {
            UnityEngine.AddressableAssets.Addressables.Release(handle.Asset);
        }
    }
}*/