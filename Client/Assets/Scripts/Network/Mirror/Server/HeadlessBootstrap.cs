using System.Linq;
using Mirror;
using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// Headless 服务端启动器 (替代原 Program.cs / Server.cs 的入口逻辑)
///
/// Unity 命令行启动 Headless Server:
///   Windows: Client.exe -batchmode -nographics -logFile server.log
///   Linux:   ./Client.x86_64 -batchmode -nographics -logFile server.log
///
/// 对比原架构:
///   Program.Main() → new Server(6666)  → HeadlessBootstrap.Awake()
///   Server 构造函数 (TCP 监听)          → Mirror NetworkManager.StartServer()
///   心跳/房间检查定时器                 → Mirror 内置超时 + Server Managers
/// </summary>
public class HeadlessBootstrap : MonoBehaviour
{
    [Header("Headless Server Config")]
    [Tooltip("是否在 Awake 时自动启动服务器")]
    public bool autoStartServer = true;

    [Tooltip("服务器端口")]
    public ushort serverPort = 6666;

    [Tooltip("最大连接数")]
    public int maxConnections = 100;

    [Tooltip("服务器 Tick Rate")]
    public int sendRate = 30;

    private void Awake()
    {
        if (!autoStartServer) return;

        // 只在 batchmode (Headless) 或命令行指定 -server 时自动启动
        if (Application.isBatchMode || System.Environment.GetCommandLineArgs().Contains("-server"))
        {
            // 设置目标帧率 (Headless 服务器不需要高帧率)
            Application.targetFrameRate = 60;

            var netManager = GetComponent<CustomNetworkManager>();
            if (netManager == null)
            {
                netManager = gameObject.AddComponent<CustomNetworkManager>();
            }

            // 配置
            var kcp = GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            kcp.Port = serverPort;

            netManager.maxConnections = maxConnections;
            netManager.sendRate = sendRate;

            // 启动服务器
            netManager.StartServer();

            Debug.Log($"[Headless] 服务端已启动, 端口={serverPort}, 最大连接数={maxConnections}");
        }
    }

    private void OnEnable()
    {
        if (Application.isBatchMode)
        {
            // 重定向日志到文件
            Debug.Log($"[Headless] BatchMode 启动 @ {System.DateTime.Now}");
        }
    }
}
}
