using System;
using System.Collections;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
    /// <summary>
    /// 热更程序集入口。
    /// 由 GameBootstrap（AOT）在 DLL 加载完成后通过 IGameLogicEntry 接口调用。
    /// 负责：YooAsset 资源加载 → UI 初始化 → Mirror 网络 → 游戏启动。
    /// </summary>
    public class GameLogicEntry : MonoBehaviour, IGameLogicEntry
    {
        /// <summary>缓存的 ScriptableStats 配置（PlayerController 依赖，动态加载后注入）</summary>
        private static ScriptableStats _cachedStats;

        public IEnumerator InitializeAsync(PatchManager patchMgr)
        {
            Debug.Log("[GameLogicEntry] Starting game initialization...");

            // ── 加载 PlayerController 依赖的 ScriptableStats 配置 ──
            // 预制体上原本通过 [SerializeField] 拖好了 PlayerStatus.asset，
            // 但动态 AddComponent 后无法自动恢复，需要手动加载并注入。
            if (_cachedStats == null)
            {
                _cachedStats = ResourceManager.Instance.LoadAsset<ScriptableStats>("Boot_PlayerStatus");
                if (_cachedStats == null)
                    Debug.LogError("[GameLogicEntry] Failed to load ScriptableStats (Game_PlayerStatus), player won't move!");
                else
                    Debug.Log("[GameLogicEntry] ScriptableStats loaded for PlayerController injection.");
            }

            // ── 注册 MirrorPlayer 组件动态注入钩子 ──
            // 热更 MonoBehaviour 不能直接挂在 MirrorPlayer 预制体上（il2cpp 会剥离），
            // 改为运行时通过钩子动态 AddComponent，避免序列化不匹配。
            RegisterMirrorPlayerHooks();

            // ── 恢复场景上被剥离的 GameLogic 脚本 ──
            RestoreSceneScripts();

            // ── Phase 1: UI 框架初始化 ──
            LoadSpriteAtlases();

            // 不要 DontDestroyOnLoad 相机：场景重载（如单机模式进/退）时，
            // 场景文件中的新相机会替换旧相机，DontDestroyOnLoad 反而导致重复相机。
            // CameraController 会由 RestoreSceneScripts 在每次场景加载时重新挂载。
            GameObject mainCamera = GameObject.FindWithTag("MainCamera");

            GameObject canvasPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Boot_Canvas");
            GameObject canvas = Instantiate(canvasPrefab);
            canvas.transform.position = Vector3.zero;
            canvas.transform.name = canvasPrefab.name;
            ResourceManager.Instance.AddAsset(canvas.name, canvas);
            DontDestroyOnLoad(canvas);

            GameObject eventSystemPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Boot_EventSystem");
            GameObject eventSystem = Instantiate(eventSystemPrefab);
            eventSystem.transform.position = Vector3.zero;
            eventSystem.transform.name = eventSystemPrefab.name;
            ResourceManager.Instance.AddAsset(eventSystem.name, eventSystem);
            DontDestroyOnLoad(eventSystem);

            GameObject vs = new GameObject("VirtualInputSystem");
            vs.transform.position = Vector3.zero;
            vs.AddComponent<UpdateManager>();
            vs.AddComponent<VirtualInputSystem>();
            DontDestroyOnLoad(vs);

            // ── Phase 2: 打开更新面板 ──
            BasePanel updatePanel = UIManager.instance.OpenPanel(UIPanelType.Update);

            // ── Phase 3: 版本检查 + 资源下载 ──
            yield return patchMgr.CheckAndUpdateAsync();

            // ── Phase 4: 检测热更 DLL 是否已更新 ──
            CheckHotUpdateDllUpdated(patchMgr.Package);

            // ── Phase 5: 关闭更新面板 ──
            if (updatePanel != null)
            {
                UIManager.instance.ClosePanel();
            }

            // ── Phase 6: 初始化 Mirror 网络 ──
            InitializeMirror();

            // ── Phase 7: 启动游戏 ──
            GameManager.Instance.StartGame();

            Debug.Log("[GameLogicEntry] Game initialization complete.");
        }

        /// <summary>
        /// DLL 加载完成后，给场景中的 GameObject 补回 GameLogic 脚本组件。
        /// 这些脚本在构建时被剥离（因为 GameLogic.dll 是热更程序集），
        /// 场景加载时 Unity 无法反序列化它们，需要在 DLL 就绪后手动 AddComponent。
        /// </summary>
        private void RestoreSceneScripts()
        {
            // CameraController → Main Camera
            var mainCam = GameObject.FindWithTag("MainCamera");
            if (mainCam != null && mainCam.GetComponent<CameraController>() == null)
            {
                mainCam.AddComponent<CameraController>();
                Debug.Log("[GameLogicEntry] Restored CameraController on MainCamera.");
            }

            // MirrorPlayer 相关组件 → 场景中已存在的 MirrorPlayer 实例
            // CustomNetworkManager 在 OnStartServer 时可能已通过 playerPrefab 生成了实例，
            // 这些实例上的 GameLogic 脚本在反序列化时失败，需要重新添加。
            // 添加后还需手动初始化接口（MirrorPlayer.OnStartClient 时组件还不存在，Initialize() 未被调用）。
            var existingPlayers = FindObjectsOfType<MirrorPlayer>();
            foreach (var mp in existingPlayers)
            {
                if (mp != null)
                {
                    AddMirrorPlayerComponents(mp.gameObject);

                    // 手动初始化接口（弥补 OnStartClient 时组件缺失导致的 Initialize 漏调用）
                    var remoteView = mp.GetComponent<IRemotePlayerView>();
                    remoteView?.Initialize();

                    var localDriver = mp.GetComponent<ILocalPlayerDriver>();
                    if (localDriver != null && mp.isLocalPlayer)
                        localDriver.Initialize();

                    Debug.Log($"[GameLogicEntry] Restored scripts on existing MirrorPlayer: {mp.gameObject.name}");
                }
            }
        }

        /// <summary>
        /// 注册 MirrorPlayer 组件动态注入钩子。
        /// 任何 MirrorPlayer 实例（无论从预制体生成还是场景中已有）在 OnStartClient
        /// 和 OnStartAuthority 时都会触发这些回调，确保热更脚本被正确添加。
        /// </summary>
        private static void RegisterMirrorPlayerHooks()
        {
            MirrorPlayerComponentSetup.OnMirrorPlayerCreated = (go) =>
            {
                AddMirrorPlayerComponents(go);
            };

            MirrorPlayerComponentSetup.OnMirrorPlayerAuthority = (go) =>
            {
                // 本地玩家需要额外的输入控制组件
                if (go.GetComponent<LocalPlayerDriver>() == null)
                    go.AddComponent<LocalPlayerDriver>();

                // PlayerController 依赖 ScriptableStats，动态 AddComponent 后
                // 需要手动注入 _stats 字段（预制体上的 [SerializeField] 引用已丢失）
                var pc = go.GetComponent<PlayerController>();
                if (pc == null)
                    pc = go.AddComponent<PlayerController>();
                InjectPlayerControllerData(pc);
            };

            Debug.Log("[GameLogicEntry] MirrorPlayer component hooks registered.");
        }

        /// <summary>
        /// 往动态创建的 PlayerController 注入必需的配置数据。
        /// 这些数据原本在预制体上通过 [SerializeField] 拖拽设置，
        /// 动态 AddComponent 后字段为空，需要手动补充。
        /// </summary>
        private static void InjectPlayerControllerData(PlayerController pc)
        {
            if (pc == null) return;

            // 注入 ScriptableStats（物理/移动/跳跃参数）
            if (_cachedStats != null)
            {
                var statsField = typeof(PlayerController).GetField("_stats",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (statsField != null)
                {
                    statsField.SetValue(pc, _cachedStats);
                }
            }

            // _shooterRecoilForce 有默认值 60f，但如果预制体上配了不同值，
            // 可以在这里覆盖
        }

        /// <summary>
        /// 为 MirrorPlayer GameObject 添加所有必需的 GameLogic 脚本。
        /// 安全重复调用：已存在的组件不会被重复添加。
        /// PlayerAnimator 必须挂在 "Sprite" 子对象上（其 Awake 依赖同对象上的 AudioSource）。
        /// </summary>
        private static void AddMirrorPlayerComponents(GameObject go)
        {
            if (go == null) return;

            // PlayerAnimator 挂在 Sprite 子对象上（Awake 依赖同对象的 AudioSource）
            SetupPlayerAnimator(go);

            if (go.GetComponent<RemotePlayerView>() == null)
                go.AddComponent<RemotePlayerView>();

            // 死亡检测
            if (go.GetComponent<PlayerDeathCheck>() == null)
                go.AddComponent<PlayerDeathCheck>();
        }

        /// <summary>
        /// 为非 MirrorPlayer 的普通玩家对象（单机/无限模式）添加 PlayerAnimator。
        /// MirrorPlayer 路径由 AddMirrorPlayerComponents 自动处理。
        /// </summary>
        public static void SetupPlayerAnimator(GameObject playerGo)
        {
            if (playerGo == null) return;
            Transform spriteT = playerGo.transform.Find("Sprite");
            GameObject spriteGo = spriteT != null ? spriteT.gameObject : playerGo;
            if (spriteGo.GetComponent<PlayerAnimator>() == null)
            {
                var pa = spriteGo.AddComponent<PlayerAnimator>();
                InjectPlayerAnimatorData(pa);
            }
        }

        /// <summary>
        /// 为普通玩家对象（单机/无限模式）添加所有必需的 GameLogic 组件。
        /// 注意添加顺序：PlayerController 必须在 PlayerAnimator 之前，
        /// 因为 PlayerAnimator.Awake() 中 GetComponentInParent&lt;IPlayerController&gt;() 需要找到它。
        /// </summary>
        public static void SetupPlayerComponents(GameObject playerGo)
        {
            if (playerGo == null) return;

            try
            {
                // 1. PlayerController — 必须最先（PlayerAnimator 依赖它）
                if (playerGo.GetComponent<PlayerController>() == null)
                {
                    var pc = playerGo.AddComponent<PlayerController>();
                    InjectPlayerControllerData(pc);
                }

                // 2. PlayerAnimator → Sprite 子对象
                SetupPlayerAnimator(playerGo);

                // 3. PlayerDeathCheck
                if (playerGo.GetComponent<PlayerDeathCheck>() == null)
                    playerGo.AddComponent<PlayerDeathCheck>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameLogicEntry] SetupPlayerComponents failed: {e}");
            }
        }

        /// <summary>
        /// 向动态添加的 PlayerAnimator 注入原本在预制体上通过拖拽配置的数据。
        /// </summary>
        public static void InjectPlayerAnimatorData(PlayerAnimator pa)
        {
            if (pa == null) return;

            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // ── 引用类型：同 GameObject 上获取 ──
            var animField = typeof(PlayerAnimator).GetField("_anim", flags);
            animField?.SetValue(pa, pa.GetComponent<Animator>());

            var spriteField = typeof(PlayerAnimator).GetField("_sprite", flags);
            spriteField?.SetValue(pa, pa.GetComponent<SpriteRenderer>());

            // ── 数值类型：与预制体配置一致 ──
            typeof(PlayerAnimator).GetField("_maxTilt", flags)?.SetValue(pa, 5f);
            typeof(PlayerAnimator).GetField("_tiltSpeed", flags)?.SetValue(pa, 20f);

            // ── 音频资源：从 GameAssets/Sounds/ 加载 ──
            try
            {
                var footstepsField = typeof(PlayerAnimator).GetField("_footsteps", flags);
                if (footstepsField != null)
                {
                    var footsteps = new AudioClip[]
                    {
                        ResourceManager.Instance.LoadAsset<AudioClip>("Sounds_Splat 1"),
                        ResourceManager.Instance.LoadAsset<AudioClip>("Sounds_Splat 2"),
                        ResourceManager.Instance.LoadAsset<AudioClip>("Sounds_Splat 3"),
                    };
                    footstepsField.SetValue(pa, footsteps);
                }

                var jumpField = typeof(PlayerAnimator).GetField("_jump_audio", flags);
                if (jumpField != null)
                {
                    var jumpClip = ResourceManager.Instance.LoadAsset<AudioClip>("Sounds_sfx_jump");
                    jumpField.SetValue(pa, jumpClip);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameLogicEntry] Failed to load audio for PlayerAnimator: {e}");
            }
        }

        private void LoadSpriteAtlases()
        {
            string[] atlasPaths = new string[]
            {
                "Atlas_UI",
                "Atlas_GameScene",
                "Atlas_Character",
                "Atlas_Attacks",
                "Atlas_Particals",
            };

            foreach (string path in atlasPaths)
            {
                var atlas = ResourceManager.Instance.LoadAsset<SpriteAtlas>(path);
                if (atlas != null)
                {
                    Debug.Log($"[GameLogicEntry] SpriteAtlas loaded: {path}");
                }
                else
                {
                    Debug.LogWarning($"[GameLogicEntry] Failed to load SpriteAtlas: {path}");
                }
            }
        }

        private void InitializeMirror()
        {
            CustomNetworkManager netManager = FindObjectOfType<CustomNetworkManager>();
            if (netManager == null)
            {
                GameObject mirrorObj = new GameObject("CustomNetworkManager");
                mirrorObj.transform.position = Vector3.zero;
                mirrorObj.AddComponent<CustomNetworkManager>();

                var kcp = mirrorObj.AddComponent<kcp2k.KcpTransport>();
                kcp.Port = 6666;

                DontDestroyOnLoad(mirrorObj);
                Debug.Log("[Mirror] CustomNetworkManager auto created");
            }
            else
            {
                Debug.Log("[Mirror] CustomNetworkManager already exists in scene");
            }
        }

        private void CheckHotUpdateDllUpdated(YooAsset.ResourcePackage package)
        {
            if (package == null) return;

            string dllPath = "HotUpdateDlls_GameLogic.dll";
            var handle = package.LoadAssetAsync<TextAsset>(dllPath);
            handle.Completed += (h) =>
            {
                if (h.Status == YooAsset.EOperationStatus.Succeeded && h.AssetObject != null)
                {
                    Debug.Log("[GameLogicEntry] Hot update DLL detected in sandbox, "
                            + "will take effect on next app launch.");
                }
                h.Release();
            };
        }
    }
}
