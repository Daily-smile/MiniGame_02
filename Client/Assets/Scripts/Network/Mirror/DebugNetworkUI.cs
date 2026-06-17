using Mirror;
using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 测试用网络调试 UI (开发阶段使用，正式发布前删除)
///
/// 功能:
///   Start Host   — 同时启动服务器+客户端 (单机测试)
///   Connect      — 连接到 127.0.0.1:6666 (连接已有服务端)
///   Stop         — 断开连接 (Host模式同时停止服务器和客户端)
///   状态显示      — Host / Server / Client / Offline
/// </summary>
public class DebugNetworkUI : MonoBehaviour
{
    private enum Mode { Offline, Host, ClientOnly }

    private Mode _mode = Mode.Offline;
    private bool _showUI = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _showUI = !_showUI;
        }

        // 检测外部断开 (如服务器崩溃)
        if (_mode != Mode.Offline && !NetworkClient.isConnected && !NetworkServer.active)
        {
            _mode = Mode.Offline;
        }
    }

    private void OnGUI()
    {
        if (!_showUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 180));

        string statusText;
        if (_mode == Mode.Host)
            statusText = "Host (Server + Client)";
        else if (_mode == Mode.ClientOnly)
            statusText = NetworkClient.isConnected ? "Client Connected" : "Client (connecting...)";
        else
            statusText = "Offline";

        GUILayout.Box("Mirror Network Debug");
        GUILayout.Label($"Status: {statusText}");

        if (_mode == Mode.Offline)
        {
            if (GUILayout.Button("Start Host (Server + Client)", GUILayout.Height(35)))
            {
                if (CustomNetworkManager.singleton != null)
                {
                    CustomNetworkManager.singleton.StartHost();
                    _mode = Mode.Host;
                }
            }

            GUILayout.Space(5);

            GUILayout.Label("Server Address:");
            if (CustomNetworkManager.singleton != null)
            {
                CustomNetworkManager.singleton.networkAddress =
                    GUILayout.TextField(CustomNetworkManager.singleton.networkAddress ?? "127.0.0.1");
            }

            if (GUILayout.Button("Connect to Server", GUILayout.Height(35)))
            {
                if (CustomNetworkManager.singleton != null)
                {
                    CustomNetworkManager.singleton.ConnectToServer();
                    _mode = Mode.ClientOnly;
                }
            }
        }
        else
        {
            if (GUILayout.Button("Stop", GUILayout.Height(35)))
            {
                if (CustomNetworkManager.singleton != null)
                {
                    if (_mode == Mode.Host)
                    {
                        CustomNetworkManager.singleton.StopHost();
                    }
                    else
                    {
                        CustomNetworkManager.singleton.StopClient();
                    }
                }
                _mode = Mode.Offline;
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Press F1 to toggle this UI");
        GUILayout.EndArea();
    }
}
}
