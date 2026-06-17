using System;
using UnityEngine;
using YooAsset;

namespace LF.Framework
{
public interface IResourceLoader
{
    ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object;
    void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object;
    void UnloadAsset(ResourceHandle handle);
}

/// <summary>
/// YooAsset 统一资源加载器。
/// 根据 ResourcePackage 的初始化模式决定实际加载方式：
/// Editor下为 EditorSimulateMode（直接从 AssetDatabase 加载），
/// Runtime下为 HostPlayMode（从远端/内置包加载）。
/// </summary>
public class YooAssetResourceLoader : IResourceLoader
{
    private ResourcePackage _package;

    public YooAssetResourceLoader(ResourcePackage package)
    {
        _package = package;
    }

    public ResourceHandle LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        var handle = _package.LoadAssetSync<T>(path);
        if (handle.Status == EOperationStatus.Succeeded)
        {
            var resHandle = new ResourceHandle(path, handle.AssetObject) { YooAssetHandle = handle };
            return resHandle;
        }
        Debug.LogError($"[YooAssetResourceLoader] Failed to load asset: {path}, Status: {handle.Status}");
        return null;
    }

    public void LoadAssetAsync<T>(string path, Action<ResourceHandle> callback) where T : UnityEngine.Object
    {
        var handle = _package.LoadAssetAsync<T>(path);
        handle.Completed += (h) =>
        {
            if (h.Status == EOperationStatus.Succeeded)
            {
                var resHandle = new ResourceHandle(path, h.AssetObject) { YooAssetHandle = h };
                callback?.Invoke(resHandle);
            }
            else
            {
                Debug.LogError($"[YooAssetResourceLoader] Failed to load asset async: {path}, Status: {h.Status}");
                callback?.Invoke(null);
            }
        };
    }

    public void UnloadAsset(ResourceHandle handle)
    {
        if (handle == null) return;
        if (handle.YooAssetHandle != null)
        {
            handle.YooAssetHandle.Release();
        }
    }
}
}
