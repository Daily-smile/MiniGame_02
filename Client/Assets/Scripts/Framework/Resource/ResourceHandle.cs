using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace LF.Framework
{
public class ResourceHandle
{
    public string Path { get; private set; }
    public Object Asset { get; private set; }
    public int ReferenceCount { get; private set; }
    public List<string> Dependencies { get; private set; }

    internal AssetHandle YooAssetHandle { get; set; }

    public ResourceHandle(string path, Object asset)
    {
        Path = path;
        Asset = asset;
        ReferenceCount = 1;
        Dependencies = new List<string>();
    }

    public void AddReference()
    {
        ReferenceCount++;
    }

    public bool Release()
    {
        ReferenceCount--;
        return ReferenceCount <= 0;
    }

    public void AddDependency(string dependencyPath)
    {
        if (!Dependencies.Contains(dependencyPath))
        {
            Dependencies.Add(dependencyPath);
        }
    }
}
}
