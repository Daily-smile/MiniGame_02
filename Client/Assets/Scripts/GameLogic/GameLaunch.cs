using System.Collections;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class GameLaunch : MonoBehaviour
{
    private static bool IsInit;

    private void Awake()
    {

    }

    private void Start()
    {
        if (IsInit)
        {
            return;
        }
        IsInit = true;
        StartCoroutine(InitAsync());
    }

    private IEnumerator InitAsync()
    {
        // ── Phase 1: 初始化 YooAsset 资源包（使 ResourceManager 可用）──
        // 临时清除旧版本缓存的不完整 URL（确认正常后删除此行）
        PlayerPrefs.DeleteKey("RemoteURL_DefaultPackage");

        PatchManager patchMgr = new PatchManager();
        string remoteUrl = PatchManager.GetBestRemoteUrl("DefaultPackage", "http://127.0.0.1:8000");
        yield return patchMgr.InitPackageAsync("DefaultPackage", remoteUrl);

        // ── 初始化失败兜底（无内置资源 + 无网络时，直接显示错误界面 + 重试按钮）──
        if (!patchMgr.InitSuccess)
        {
            ShowInitErrorFallback();
            yield break;
        }

        // ── Phase 2: 加载 UI 框架，通过 YooAsset 加载热更新面板 ──
        Initialize();
        BasePanel updatePanel = UIManager.instance.OpenPanel(UIPanelType.Update);

        // ── Phase 3: 版本检查 + 资源下载 ──
        yield return patchMgr.CheckAndUpdateAsync();

        // ── Phase 4: 检测热更 DLL 是否已更新 ──
        // HybridCLR 预加载器已在场景加载前完成 DLL 加载。
        // 如果本次下载了新的热更 DLL，它们将在下次启动时生效。
        CheckHotUpdateDllUpdated(patchMgr.Package);

        // ── Phase 5: 关闭热更新面板，初始化 Mirror 网络 ──
        if (updatePanel != null)
        {
            UIManager.instance.ClosePanel();
        }

        InitializeMirror();

        // ── Phase 6: 启动游戏 ──
        GameManager.Instance.StartGame();
    }

    /// <summary>
    /// 初始化失败时的兜底界面（不用任何 YooAsset 资源，纯代码创建）。
    /// 显示错误提示 + 重试按钮。
    /// </summary>
    private void ShowInitErrorFallback()
    {
        Debug.LogError("[GameLaunch] Resource init failed. Showing fallback error UI.");

        // 已有旧 Canvas 时清理
        GameObject existingCanvas = GameObject.Find("ErrorCanvas");
        if (existingCanvas != null) Destroy(existingCanvas);

        GameObject canvasObj = new GameObject("ErrorCanvas");
        DontDestroyOnLoad(canvasObj);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 半透明黑色背景
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // 错误提示文字
        GameObject textObj = new GameObject("ErrorText");
        textObj.transform.SetParent(canvasObj.transform, false);
        var text = textObj.AddComponent<Text>();
        text.text = "资源初始化失败\n\n请检查网络连接后重试";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.15f, 0.35f);
        textRt.anchorMax = new Vector2(0.85f, 0.65f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // 重试按钮
        GameObject btnObj = new GameObject("RetryButton");
        btnObj.transform.SetParent(canvasObj.transform, false);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.3f, 0.5f, 0.8f);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            Destroy(canvasObj);
            StartCoroutine(InitAsync());
        });
        var btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.35f, 0.68f);
        btnRt.anchorMax = new Vector2(0.65f, 0.74f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        GameObject btnTextObj = new GameObject("BtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        var btnText = btnTextObj.AddComponent<Text>();
        btnText.text = "重试";
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 28;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        var btnTextRt = btnTextObj.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 预加载所有 SpriteAtlas，确保 AssetBundle 中的精灵引用能正确解析。
    /// 在 AssetBundle 模式下，SpriteAtlas 必须在 SpriteRenderer 使用对应精灵之前加载，
    /// 否则精灵引用会丢失，导致渲染显示白块。
    /// </summary>
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
                Debug.Log($"[GameLaunch] SpriteAtlas loaded: {path}");
            }
            else
            {
                Debug.LogWarning($"[GameLaunch] Failed to load SpriteAtlas: {path}");
            }
        }
    }

    private void Initialize()
    {
        // ── 预加载所有 SpriteAtlas，确保 AssetBundle 中的精灵引用能正确解析 ──
        LoadSpriteAtlases();

        GameObject mainCamera = GameObject.FindWithTag("MainCamera");
        GameObject.DontDestroyOnLoad(mainCamera);
        GameObject canvasPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Boot_Canvas");
        GameObject canvas = GameObject.Instantiate(canvasPrefab);
        canvas.transform.position = Vector3.zero;
        canvas.transform.name = canvasPrefab.name;
        ResourceManager.Instance.AddAsset(canvas.name, canvas);
        GameObject.DontDestroyOnLoad(canvas);
        GameObject eventSystemPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Boot_EventSystem");
        GameObject eventSystem = GameObject.Instantiate(eventSystemPrefab);
        eventSystem.transform.position = Vector3.zero;
        eventSystem.transform.name = eventSystemPrefab.name;
        ResourceManager.Instance.AddAsset(eventSystem.name, eventSystem);
        GameObject.DontDestroyOnLoad(eventSystem);
        GameObject vs = new GameObject("VirtualInputSystem");
        vs.transform.position = Vector3.zero;
        vs.AddComponent<UpdateManager>();
        vs.AddComponent<VirtualInputSystem>();
        GameObject.DontDestroyOnLoad(vs);
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

    /// <summary>
    /// 检测资源更新后是否包含新的热更 DLL。
    /// 新 DLL 已在 Sandbox 缓存中，下次启动时 HybridCLRPreloader 会自动加载。
    /// </summary>
    private void CheckHotUpdateDllUpdated(YooAsset.ResourcePackage package)
    {
        if (package == null) return;

        // 地址格式匹配 AddressByFolderAndFileName 规则: {文件夹名}_{文件名(无后缀)}
        string dllPath = "HotUpdateDlls_GameLogic.dll";
        var handle = package.LoadAssetAsync<TextAsset>(dllPath);
        handle.Completed += (h) =>
        {
            if (h.Status == YooAsset.EOperationStatus.Succeeded && h.AssetObject != null)
            {
                Debug.Log("[GameLaunch] Hot update DLL detected in sandbox, "
                        + "will take effect on next app launch.");
            }
            h.Release();
        };
    }
}
}
