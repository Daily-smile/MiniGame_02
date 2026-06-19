using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HybridCLR;
using UnityEngine;
using YooAsset;

namespace LF.Framework
{
/// <summary>
/// HybridCLR 热更程序集加载引导（辅助工具）。
///
/// 正常流程中，热更 DLL 由 HybridCLRPreloader 在 BeforeSceneLoad 阶段自动加载，
/// 无需手动调用此类。本类保留供特殊场景（如运行时手动重载）使用。
/// </summary>
public static class HybridCLRBootstrap
{
    /// <summary>
    /// AOT 元数据补充程序集列表。
    /// 这些是热更代码（GameLogic）依赖的 AOT 程序集，
    /// 需要加载补充元数据以支持热更代码调用 AOT 类型。
    /// </summary>
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

    /// <summary>
    /// 热更程序集名称列表（需要从资源包加载并执行）。
    /// </summary>
    private static readonly string[] HotUpdateAssemblyNames =
    {
        "GameLogic",
    };

    /// <summary>
    /// YooAsset 资源地址前缀，热更 DLL 的加载地址格式："{Prefix}{AssemblyName}.dll"
    /// </summary>
    private const string DllAssetPrefix = "HotUpdateDlls_";

    /// <summary>
    /// 加载所有热更程序集和 AOT 补充元数据。
    /// </summary>
    /// <param name="package">已初始化的 YooAsset 资源包</param>
    public static IEnumerator LoadAsync(ResourcePackage package)
    {
        if (package == null)
        {
            Debug.LogError("[HybridCLR] ResourcePackage is null, skip hot update assembly loading.");
            yield break;
        }

#if UNITY_EDITOR
        // Editor 模式下 HybridCLR 不需要手动加载 DLL（直接通过 Editor 编译运行）
        Debug.Log("[HybridCLR] Editor mode: skip hot update DLL loading.");
        yield break;
#else
        // ── Step 1: 加载 AOT 补充元数据 ──
        foreach (string aotName in AOTMetaAssemblyNames)
        {
            yield return LoadMetadataIfExists(package, aotName);
        }

        // ── Step 2: 加载热更程序集 ──
        foreach (string hotName in HotUpdateAssemblyNames)
        {
            yield return LoadHotUpdateAssembly(package, hotName);
        }

        Debug.Log("[HybridCLR] All hot update assemblies loaded successfully.");
#endif
    }

    /// <summary>
    /// 加载 AOT 程序集的补充元数据（如果存在）。
    /// 补充元数据允许热更代码访问 AOT 程序集中未直接编译到 il2cpp 的类型和方法。
    /// 若元数据文件不存在（首次启动、内置包内置了部分），则跳过不报错。
    /// </summary>
    private static IEnumerator LoadMetadataIfExists(ResourcePackage package, string assemblyName)
    {
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            // 元数据文件不存在是正常情况（该程序集没有补充元数据或已内置）
            Debug.Log($"[HybridCLR] AOT metadata not found: {assemblyName}, skipping.");
            yield break;
        }

        var textAsset = handle.AssetObject as TextAsset;
        if (textAsset == null)
        {
            Debug.LogWarning($"[HybridCLR] AOT metadata asset is not a TextAsset: {assemblyName}");
            handle.Release();
            yield break;
        }

        byte[] metaBytes = textAsset.bytes;
        handle.Release();

        LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(metaBytes, HomologousImageMode.SuperSet);
        if (err == LoadImageErrorCode.OK)
        {
            Debug.Log($"[HybridCLR] AOT metadata loaded: {assemblyName}");
        }
        else
        {
            Debug.LogError($"[HybridCLR] Failed to load AOT metadata for {assemblyName}: {err}");
        }
    }

    /// <summary>
    /// 加载热更程序集。
    /// 在 HybridCLR 中，热更 DLL 通过标准的 Assembly.Load 加载，
    /// 但实际执行由 HybridCLR 解释器接管。
    /// </summary>
    private static IEnumerator LoadHotUpdateAssembly(ResourcePackage package, string assemblyName)
    {
        string assetPath = $"{DllAssetPrefix}{assemblyName}.dll";
        var handle = package.LoadAssetAsync<TextAsset>(assetPath);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeeded || handle.AssetObject == null)
        {
            // 首次启动时热更 DLL 可能还未下载（使用内置版本），不算错误
            Debug.Log($"[HybridCLR] Hot update assembly not found in package: {assemblyName}, "
                    + "using built-in version if available.");
            yield break;
        }

        var textAsset = handle.AssetObject as TextAsset;
        if (textAsset == null)
        {
            Debug.LogWarning($"[HybridCLR] Hot update assembly asset is not a TextAsset: {assemblyName}");
            handle.Release();
            yield break;
        }

        byte[] dllBytes = textAsset.bytes;
        handle.Release();

        try
        {
            Assembly.Load(dllBytes);
            Debug.Log($"[HybridCLR] Hot update assembly loaded: {assemblyName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HybridCLR] Failed to load hot update assembly {assemblyName}: {e.Message}");
        }
    }
}
}
