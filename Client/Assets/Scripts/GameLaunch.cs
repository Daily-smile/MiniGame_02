using UnityEngine;

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
        Initialize();
        InitializeMirror();
        GameManager.Instance.StartGame();
    }

    private void Initialize()
    {
        GameObject mainCamera = GameObject.FindWithTag("MainCamera");
        GameObject.DontDestroyOnLoad(mainCamera);
        GameObject canvasPrefab = Resources.Load<GameObject>("Canvas");
        GameObject canvas = GameObject.Instantiate(canvasPrefab);
        canvas.transform.position = Vector3.zero;
        canvas.transform.name = canvasPrefab.name;
        ResourceManager.Instance.AddAsset(canvas.name, canvas);
        GameObject.DontDestroyOnLoad(canvas);
        GameObject eventSystemPrefab = Resources.Load<GameObject>("EventSystem");
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

    /// <summary>
    /// 初始化 Mirror 网络系统
    /// </summary>
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
            Debug.Log("[Mirror] CustomNetworkManager 自动创建完成");
        }
        else
        {
            Debug.Log("[Mirror] CustomNetworkManager 已存在于场景中");
        }
    }
}
