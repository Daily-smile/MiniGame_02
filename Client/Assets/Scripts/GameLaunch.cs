using System.Collections;
using UnityEngine;
using UnityEngine.U2D;

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
        string remoteUrl = PatchManager.GetBestRemoteUrl("DefaultPackage", "http://192.168.100.199:8000");
        yield return patchMgr.InitPackageAsync("DefaultPackage", remoteUrl);

        // ── Phase 2: 加载 UI 框架，通过 YooAsset 加载热更新面板 ──
        Initialize();
        BasePanel updatePanel = UIManager.instance.OpenPanel(UIPanelType.Update);

        // ── Phase 3: 版本检查 + 资源下载（UpdatePanel 已订阅事件，可接收进度）──
        yield return patchMgr.CheckAndUpdateAsync();

        // ── Phase 4: 关闭热更新面板，初始化 Mirror 网络 ──
        if (updatePanel != null)
        {
            UIManager.instance.ClosePanel();
        }

        InitializeMirror();

        // ── Phase 5: 启动游戏 ──
        GameManager.Instance.StartGame();
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
}
