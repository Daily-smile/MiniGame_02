using System.Linq;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HybridCLR 首次配置引导。
/// 项目首次在 Editor 中打开时自动创建并配置 HybridCLRSettings.asset。
/// 之后不再覆盖已有配置。
/// </summary>
[InitializeOnLoad]
public static class HybridCLRSetup
{
    static HybridCLRSetup()
    {
        EditorApplication.delayCall += ConfigureOnce;
    }

    private static void ConfigureOnce()
    {
        EditorApplication.delayCall -= ConfigureOnce;

        var settings = HybridCLRSettings.LoadOrCreate();
        bool needsSave = false;

        // 修复旧配置中错误的 "Mirror.Core" → "Mirror"
        if (settings.patchAOTAssemblies != null)
        {
            for (int i = 0; i < settings.patchAOTAssemblies.Length; i++)
            {
                if (settings.patchAOTAssemblies[i] == "Mirror.Core")
                {
                    settings.patchAOTAssemblies[i] = "Mirror";
                    needsSave = true;
                    Debug.Log("[HybridCLRSetup] Fixed: Mirror.Core → Mirror in patchAOTAssemblies.");
                }
            }
        }

        // 仅修复时保存并返回
        if (needsSave)
        {
            HybridCLRSettings.Save();
            Debug.Log("[HybridCLRSetup] Saved fixed settings.");
            return;
        }

        // 仅在未配置时自动初始化（首次）
        if (settings.hotUpdateAssemblies.Length > 0
            || settings.hotUpdateAssemblyDefinitions.Length > 0)
        {
            Debug.Log("[HybridCLRSetup] Settings already configured, skipping auto-setup.");
            return;
        }

        Debug.Log("[HybridCLRSetup] First-time setup: configuring HybridCLR settings...");

        // ── 配置热更程序集（使用字符串名，避免 AssemblyDefinitionAsset 类型依赖）──
        settings.hotUpdateAssemblies = new[] { "GameLogic" };
        Debug.Log("[HybridCLRSetup] HotUpdate assembly: GameLogic");

        // ── 配置 AOT 补充元数据程序集 ──
        settings.patchAOTAssemblies = new[]
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
        Debug.Log($"[HybridCLRSetup] AOT patch assemblies: {string.Join(", ", settings.patchAOTAssemblies)}");

        // ── 保存设置 ──
        HybridCLRSettings.Save();
        Debug.Log("[HybridCLRSetup] Settings saved to ProjectSettings/HybridCLRSettings.asset");
        AssetDatabase.Refresh();

        Debug.Log("[HybridCLRSetup] Setup complete. Next step: 'HybridCLR > Generate > All'.");
    }
}
