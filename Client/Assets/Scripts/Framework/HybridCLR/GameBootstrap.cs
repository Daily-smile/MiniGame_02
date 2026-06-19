using System;
using System.Collections;
using UnityEngine;

namespace LF.Framework
{
    /// <summary>
    /// 游戏启动引导器（AOT 程序集）。
    /// 挂载在 Game 场景的根 GameObject 上。
    ///
    /// 流程：
    ///   1. 等待 HybridCLRPreloader 完成（DLL 已加载）
    ///   2. 初始化 YooAsset 资源包（复用预加载器的 Package）
    ///   3. 通过反射找到 GameLogicEntry（热更程序集）并调用其初始化
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 防止场景重载（如进入/退出单机游戏）时重复执行初始化流程。
        /// HybridCLRPreloader 只在进程启动时运行一次，GameBootstrap 也只应运行一次。
        /// </summary>
        private static bool IsInitialized;

        private IEnumerator Start()
        {
            if (IsInitialized)
            {
                Debug.Log("[GameBootstrap] Already initialized, restoring scene scripts only.");
                // 场景重载（如单机模式进/退）：DontDestroyOnLoad 的 UI 和网络对象幸存，
                // 但新场景的 MainCamera 等对象丢失了热更脚本，需要重新挂载。
                yield return RestoreSceneScriptsOnly();
                yield break;
            }
            IsInitialized = true;

            // ── Step 1: 等待预加载完成 ──
            Debug.Log("[GameBootstrap] Waiting for preloader...");
            while (!HybridCLRPreloader.IsLoaded)
                yield return null;
            Debug.Log("[GameBootstrap] Preloader done.");

            // ── Step 2: 初始化 YooAsset 资源包 ──
            var patchMgr = new PatchManager();
            string remoteUrl = PatchManager.GetBestRemoteUrl("DefaultPackage", "http://127.0.0.1:8000");
            yield return patchMgr.InitPackageAsync("DefaultPackage", remoteUrl);

            if (!patchMgr.InitSuccess)
            {
                Debug.LogError("[GameBootstrap] Resource init failed.");
                yield break;
            }

            // ── Step 3: 调用热更程序集入口 ──
            var entryType = Type.GetType("LF.GameLogic.GameLogicEntry, GameLogic");
            if (entryType == null)
            {
                Debug.LogError("[GameBootstrap] GameLogicEntry not found! Is GameLogic.dll loaded?");
                yield break;
            }

            // 防止场景重载后重复创建（DontDestroyOnLoad 的旧实例可能仍在）
            if (GameObject.Find("__GameLogicEntry__") != null)
            {
                Debug.Log("[GameBootstrap] __GameLogicEntry__ already exists, skipping creation.");
                yield break;
            }

            var entryGo = new GameObject("__GameLogicEntry__");
            DontDestroyOnLoad(entryGo);
            var entry = (IGameLogicEntry)entryGo.AddComponent(entryType);
            yield return StartCoroutine(entry.InitializeAsync(patchMgr));
        }

        /// <summary>
        /// 场景重载时仅恢复被剥离的热更脚本组件，不执行完整初始化流程。
        /// 通过反射调用 GameLogicEntry.RestoreSceneScripts()，避免 GameBootstrap (AOT)
        /// 直接引用热更类型。
        /// </summary>
        private static IEnumerator RestoreSceneScriptsOnly()
        {
            // 确保预加载已完成（场景重载理论上已完成，但做一次防御检查）
            while (!HybridCLRPreloader.IsLoaded)
                yield return null;

            var entryType = Type.GetType("LF.GameLogic.GameLogicEntry, GameLogic");
            if (entryType == null)
            {
                Debug.LogError("[GameBootstrap] Cannot restore scripts — GameLogicEntry type not found.");
                yield break;
            }

            // 创建临时 GameObject 执行恢复操作，完成后立即销毁
            var tempGo = new GameObject("__TempScriptRestorer__");
            var tempEntry = tempGo.AddComponent(entryType);

            // 通过反射调用 RestoreSceneScripts() 和 RegisterMirrorPlayerHooks()
            var restoreMethod = entryType.GetMethod("RestoreSceneScripts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hookMethod = entryType.GetMethod("RegisterMirrorPlayerHooks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            hookMethod?.Invoke(null, null);
            restoreMethod?.Invoke(tempEntry, null);

            Debug.Log("[GameBootstrap] Scene scripts restored after scene reload.");

            // 延迟一帧确保 AddComponent 完成，然后清理临时对象
            yield return null;
            Destroy(tempGo);
        }
    }
}
