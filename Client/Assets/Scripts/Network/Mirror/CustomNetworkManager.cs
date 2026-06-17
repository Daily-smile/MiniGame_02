using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 自定义网络管理器 (替代 ClientManager + MainPackManager 的连接逻辑)
///
/// 迁移对照：
///   ClientManager.InitSocket()    → StartClient()
///   ClientManager.Send(pack)      → SendToServer<T>()
///   ClientManager.SendTo(pack)    → SendToServer<T>(channels.Unreliable)
///   ClientManager.SendHeartbeat() → Mirror 内置超时
///   ClientManager.CloseSocket()   → StopClient()
///   ClientManager.ReconnectServer() → Mirror 自动重连
/// </summary>
public class CustomNetworkManager : NetworkManager
{
    [Header("Game Settings")]
    [Tooltip("登录场景名称")]
    public string loginScene = "Game";

    [Tooltip("大厅场景名称")]
    public string lobbyScene = "Game";

    [Tooltip("游戏场景名称 (双人/组队模式)")]
    public string gameScene = "GameScene";

    // playerPrefab 由基类 NetworkManager 提供，无需重复声明

    [Tooltip("Mirror 游戏管理器预制体 (挂载 MirrorGameManager)")]
    public GameObject gameManagerPrefab;

    [Header("Server Managers (Headless)")]
    [Tooltip("认证管理器预制体 (挂载 MirrorAuthManager)")]
    public GameObject authManagerPrefab;

    [Tooltip("房间管理器预制体 (挂载 MirrorRoomManager)")]
    public GameObject roomManagerPrefab;

    [Tooltip("游戏管理器预制体 (挂载 MirrorServerGameManager)")]
    public GameObject serverGameManagerPrefab;

    /// <summary>当前连接状态</summary>
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

    /// <summary>SessionId (登录成功后由服务器分配)</summary>
    public string SessionId { get; set; }

    /// <summary>当前用户名</summary>
    public string CurrentUsername { get; set; }

    /// <summary>当前玩家 ID（登录成功后由服务器分配）</summary>
    public int CurrentPlayerId { get; set; }

    /// <summary>当前是否有游戏正在进行中（防止重复开始）</summary>
    public bool IsGameInProgress { get; set; }

    // 单例便于跨模块访问
    public static new CustomNetworkManager singleton { get; private set; }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public event Action<ConnectionState> OnConnectionStateChanged;
    /// <summary>认证完成回调 (注意：不能命名为 OnClientAuthenticated，与基类方法冲突)</summary>
    public event Action<bool> OnAuthenticated;

    #region 生命周期

    public override void Awake()
    {
        // 场景重载（如返回主菜单）时，已有的 CustomNetworkManager 通过 DontDestroyOnLoad 存活，
        // 场景中的新实例成为副本 —— 直接销毁，避免触发 Mirror 的"重复 NetworkManager"警告。
        if (singleton != null && singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        // 在 base.Awake() 之前设置 Transport，避免"No Transport assigned"警告
        if (transport == null)
        {
            transport = GetComponent<kcp2k.KcpTransport>();
        }

        base.Awake();
        singleton = this;
        autoCreatePlayer = false; // 玩家只在游戏开始时生成，不在连接时自动生成
        DontDestroyOnLoad(gameObject);
    }

    public override void Start()
    {
        base.Start();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RegisterClientMessageHandlers();
        Debug.Log("[Mirror] 客户端已启动，消息处理器已注册");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[Mirror] 服务器已启动");

        try
        {
            // 生成所有服务端管理器 (无需 NetworkServer.Spawn，因为它们是纯消息处理器)
            SpawnServerManager(authManagerPrefab, "MirrorAuthManager");
            SpawnServerManager(roomManagerPrefab, "MirrorRoomManager");
            SpawnServerManager(serverGameManagerPrefab, "MirrorServerGameManager");

            // 显式注册消息处理器 (确保首次和重新Host都同步注册)
            GetComponent<MirrorAuthManager>()?.RegisterHandlers();
            GetComponent<MirrorRoomManager>()?.RegisterHandlers();
            GetComponent<MirrorServerGameManager>()?.RegisterHandlers();

            // 生成客户端游戏管理器
            if (gameManagerPrefab != null)
            {
                if (!NetworkClient.prefabs.ContainsValue(gameManagerPrefab))
                {
                    NetworkClient.RegisterPrefab(gameManagerPrefab);
                }
                GameObject gmObj = Instantiate(gameManagerPrefab);
                NetworkServer.Spawn(gmObj);
                Debug.Log("[Mirror] GameManager prefab spawned");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Mirror] 服务端启动异常: {e}");
        }
    }

    private void SpawnServerManager(GameObject prefab, string name)
    {
        if (prefab != null)
        {
            Instantiate(prefab);
            Debug.Log($"[Mirror] {name} 已在服务器生成");
        }
        else
        {
            // Prefab 未配置时，自动在当前 GameObject 上添加对应脚本
            switch (name)
            {
                case "MirrorAuthManager":
                    if (gameObject.GetComponent<MirrorAuthManager>() == null)
                        gameObject.AddComponent<MirrorAuthManager>();
                    break;
                case "MirrorRoomManager":
                    if (gameObject.GetComponent<MirrorRoomManager>() == null)
                        gameObject.AddComponent<MirrorRoomManager>();
                    break;
                case "MirrorServerGameManager":
                    if (gameObject.GetComponent<MirrorServerGameManager>() == null)
                        gameObject.AddComponent<MirrorServerGameManager>();
                    break;
            }
            Debug.Log($"[Mirror] {name} 已自动添加到 CustomNetworkManager (未配置 Prefab, 使用默认)");
        }
    }

    /// <summary>
    /// 玩家断线时回调 (替代原 Server.cs CheckHeartbeats + Client.HandleDisconnect)
    /// Mirror 内置超时检测自动触发此回调
    /// </summary>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);

        // 通知各管理器清理该玩家的状态
        MirrorRoomManager.OnPlayerDisconnected(conn);
        MirrorServerGameManager.OnPlayerDisconnected(conn);
        MirrorAuthManager.RemoveActiveLogin(conn);

        Debug.Log($"[Mirror] 玩家断线: connID={conn.connectionId}");
    }

    /// <summary>
    /// 玩家加入时服务器回调 (替代旧的 Client → Server 连接后手动创建 NetPlayer 的逻辑)
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[Mirror] playerPrefab 未配置，无法生成玩家");
            return;
        }

        // 防御性清理：如果该连接上已有旧的玩家对象，先销毁
        if (conn.identity != null)
        {
            Debug.LogWarning($"[Mirror] 连接 {conn.connectionId} 上存在旧玩家对象 {conn.identity.name}，先销毁再生成新玩家");
            NetworkServer.Destroy(conn.identity.gameObject);
        }

        // 使用当前场景的出生点或默认位置
        Transform startPos = GetStartPosition();
        Vector3 spawnPos = startPos != null ? startPos.position : Vector3.zero;

        GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        // 设置玩家名 (从 conn.authenticationData 或 CustomNetworkManager 获取)
        MirrorPlayer mirrorPlayer = playerObj.GetComponent<MirrorPlayer>();
        if (mirrorPlayer != null)
        {
            mirrorPlayer.playerName = CurrentUsername ?? $"Player_{conn.connectionId}";
        }

        // Mirror 标准流程：为这个连接添加玩家对象
        NetworkServer.AddPlayerForConnection(conn, playerObj);

        Debug.Log($"[Mirror] 服务器生成玩家: {mirrorPlayer?.playerName} at {spawnPos}");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        SessionId = null;
        CurrentUsername = null;
        SetConnectionState(ConnectionState.Disconnected);
        EventDispatcher.PostEvent(MessageEvent.ServerConnectChange, this, MessageEventType.ClientError);
        Debug.Log("[Mirror] 客户端已停止");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[Mirror] 服务器已停止");
    }

    #endregion

    #region 连接管理 (替代 ClientManager.ServerConnect/CloseSocket)

    /// <summary>
    /// 连接到服务器 (替代 ClientManager.HandleServerConnect)
    /// </summary>
    public void ConnectToServer(string address = "127.0.0.1")
    {
        // 从配置或参数获取端口
        ushort port = 6666;
        if (transport is kcp2k.KcpTransport kcp)
        {
            port = kcp.Port;
        }

        networkAddress = address;

        SetConnectionState(ConnectionState.Connecting);

        // 使用 Mirror 内置连接 (消息处理器已在OnStartClient中注册)
        // 注意：需要先配置 Transport 的地址
        if (transport is kcp2k.KcpTransport kcpTransport)
        {
            kcpTransport.Port = port;
        }

        StartClient();

        // Mirror 连接成功后通过 OnClientConnect 回调处理
        // 如果连接失败，Transport 层会抛异常，在 NetworkClient 中处理
    }

    /// <summary>
    /// 断开连接 (替代 ClientManager.CloseSocket)
    /// </summary>
    public void DisconnectFromServer()
    {
        SessionId = null;
        CurrentUsername = null;
        StopClient();
    }

    /// <summary>
    /// 启动 Host (本地服务器+客户端，用于单机测试)
    /// </summary>
    public void StartLocalHost()
    {
        SetConnectionState(ConnectionState.Connecting);
        StartHost();
    }

    /// <summary>
    /// 是否已连接到服务器
    /// </summary>
    public bool IsConnectedToServer()
    {
        return NetworkClient.isConnected;
    }

    #endregion

    #region 连接回调 (替代 ClientManager.HandleResponse 的顶层分发)

    /// <summary>
    /// 客户端连接成功时回调
    /// </summary>
    public override void OnClientConnect()
    {
        base.OnClientConnect();

        SetConnectionState(ConnectionState.Connected);

        Debug.Log($"[Mirror] 已连接到服务器 {networkAddress}");

        // 发送旧的 EventDispatcher 通知，兼容现有 UI 代码
        EventDispatcher.PostEvent(MessageEvent.ServerConnectChange, this, MessageEventType.ClientOK);

        // 如果有存储的 SessionId，尝试重连
        if (!string.IsNullOrEmpty(SessionId))
        {
            // 发送重连请求
            var reconnectMsg = new ReconnectMessage
            {
                username = CurrentUsername ?? "",
                sessionId = SessionId
            };
            NetworkClient.Send(reconnectMsg);
        }
    }

    /// <summary>
    /// 客户端断线时回调
    /// </summary>
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        bool wasReconnecting = CurrentState == ConnectionState.Reconnecting;
        SetConnectionState(ConnectionState.Disconnected);

        EventDispatcher.PostEvent(MessageEvent.ServerConnectChange, this, MessageEventType.ClientFail);
        if (wasReconnecting)
        {
            EventDispatcher.PostEvent(MessageEvent.OnReconnectFail, this, "连接已断开");
        }

        Debug.LogWarning("[Mirror] 与服务器的连接已断开");
    }

    /// <summary>
    /// 客户端认证完成 (NetworkManager 内部调用，不可 override)
    /// 通过 Awake 后注册 Authenticator 的监听来接收认证完成事件
    /// </summary>
    private void OnMirrorClientAuthenticated()
    {
        OnAuthenticated?.Invoke(true);
    }

    #endregion

    #region 场景管理 (替代旧的 Scene 加载相关逻辑)

    /// <summary>
    /// 客户端切换场景时回调
    /// </summary>
    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
    {
        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        Debug.Log($"[Mirror] 客户端正在切换场景到: {newSceneName}");
    }

    /// <summary>
    /// 客户端场景加载完成后
    /// </summary>
    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        Debug.Log($"[Mirror] 客户端场景已切换: {SceneManager.GetActiveScene().name}");
    }

    /// <summary>
    /// 服务端切换场景 (用于 StartGame 等流程)
    /// </summary>
    public void ChangeServerScene(string sceneName)
    {
        ServerChangeScene(sceneName);
    }

    #endregion

    #region 消息发送 API (替代 MainPackManager.Send/SendTo + ClientManager.SendPacket)

    /// <summary>
    /// 向服务器发送可靠消息 (替代 MainPackManager.Send)
    /// </summary>
    public static void SendToServer<T>(T message) where T : struct, NetworkMessage
    {
        if (NetworkClient.isConnected)
        {
            NetworkClient.Send(message, Channels.Reliable);
        }
        else
        {
            Debug.LogWarning($"[Mirror] 未连接到服务器，无法发送消息: {typeof(T).Name}");
        }
    }

    /// <summary>
    /// 向服务器发送不可靠消息 (替代 MainPackManager.SendTo UDP)
    /// 用于玩家位置同步等高频数据
    /// </summary>
    public static void SendToServerUnreliable<T>(T message) where T : struct, NetworkMessage
    {
        if (NetworkClient.isConnected)
        {
            NetworkClient.Send(message, Channels.Unreliable);
        }
        else
        {
            Debug.LogWarning($"[Mirror] 未连接到服务器，无法发送不可靠消息: {typeof(T).Name}");
        }
    }

    #endregion

    #region 客户端消息注册

    private void RegisterClientMessageHandlers()
    {
        // Auth
        NetworkClient.RegisterHandler<LogonResponse>(OnLogonResponse);
        NetworkClient.RegisterHandler<LoginResponse>(OnLoginResponse);

        // Room
        NetworkClient.RegisterHandler<RoomListResponse>(OnRoomListResponse);
        NetworkClient.RegisterHandler<RoomOperationResponse>(OnRoomOperationResponse);
        NetworkClient.RegisterHandler<ChatRoomMessage>(OnChatRoomMessage);
        NetworkClient.RegisterHandler<RoomInfo>(OnRoomUpdate);

        // Game
        NetworkClient.RegisterHandler<StartGameResponse>(OnStartGameResponse);
        NetworkClient.RegisterHandler<QuitMatchResponse>(OnQuitMatchResponse);
        NetworkClient.RegisterHandler<PlayerStateMessage>(OnPlayerStateUpdate);
        NetworkClient.RegisterHandler<PlayerConnectionEvent>(OnPlayerConnectionEvent);
        NetworkClient.RegisterHandler<GameOverMessage>(OnGameOver);

        // Error
        NetworkClient.RegisterHandler<ErrorResponse>(OnServerError);

        Debug.Log("[Mirror] 客户端消息处理器已注册");
    }

    private void OnServerError(ErrorResponse msg)
    {
        Debug.LogError($"[Mirror] 服务端错误: {msg.message}");
        EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, msg.message);
    }

    private void UnregisterClientMessageHandlers()
    {
        NetworkClient.UnregisterHandler<LogonResponse>();
        NetworkClient.UnregisterHandler<LoginResponse>();
        NetworkClient.UnregisterHandler<RoomListResponse>();
        NetworkClient.UnregisterHandler<RoomOperationResponse>();
        NetworkClient.UnregisterHandler<ChatRoomMessage>();
        NetworkClient.UnregisterHandler<RoomInfo>();
        NetworkClient.UnregisterHandler<StartGameResponse>();
        NetworkClient.UnregisterHandler<QuitMatchResponse>();
        NetworkClient.UnregisterHandler<PlayerStateMessage>();
        NetworkClient.UnregisterHandler<PlayerConnectionEvent>();
        NetworkClient.UnregisterHandler<GameOverMessage>();
        NetworkClient.UnregisterHandler<ErrorResponse>();
    }

    #endregion

    #region 消息处理器 (转发到 EventDispatcher 以兼容现有架构)

    private void OnLogonResponse(LogonResponse msg)
    {
        EventDispatcher.PostEvent(MessageEvent.OnLogonBack, this, msg.success);
        if (!msg.success)
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, msg.message);
        }
    }

    private void OnLoginResponse(LoginResponse msg)
    {
        if (msg.success)
        {
            SessionId = msg.sessionId;
            CurrentPlayerId = msg.playerId;
            EventDispatcher.PostEvent(MessageEvent.OnLoginOK, this, CurrentUsername, msg.sessionId, msg.playerId);
            EventDispatcher.PostEvent(MessageEvent.OnLoginOKBack, this, CurrentUsername, msg.sessionId);
            OnMirrorClientAuthenticated();
        }
        else
        {
            SessionId = null;
            EventDispatcher.PostEvent(MessageEvent.OnLoginFail, this, null);
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, msg.message);
        }
    }

    private void OnRoomListResponse(RoomListResponse msg)
    {
        EventDispatcher.PostEvent(MessageEvent.OnRefreshRoomList, this, msg.rooms, msg.success);
    }

    private void OnRoomOperationResponse(RoomOperationResponse msg)
    {
        // 根据消息内容分发到不同的事件
        // 替代原先 CreateRoomServerRespond / JoinRoomServerRespond / DestroyRoomServerRespond 等
        if (msg.success)
        {
            switch (msg.message)
            {
                case "CreateRoom":
                    EventDispatcher.PostEvent(MessageEvent.OnCreateRoomBack, this, true, msg.room);
                    break;
                case "JoinRoom":
                    EventDispatcher.PostEvent(MessageEvent.OnJoinRoomBack, this, msg.room);
                    EventDispatcher.PostEvent(MessageEvent.UpdatePlayerRoomPack, this, msg.room);
                    break;
                case "DestroyRoom":
                    EventDispatcher.PostEvent(MessageEvent.OnDestroyRoomBack, this, true);
                    EventDispatcher.PostEvent(MessageEvent.UpdatePlayerRoomPack, this, null);
                    break;
                case "QuitRoom":
                    EventDispatcher.PostEvent(MessageEvent.OnDestroyRoomBack, this, true);
                    break;
                case "SwitchRoomMaster":
                    EventDispatcher.PostEvent(MessageEvent.OnSwitchRoomMasterBack, this, msg.room.roomID, msg.room.roomMasterName);
                    break;
                case "KickoutRoom":
                    EventDispatcher.PostEvent(MessageEvent.OnKickoutRoomBack, this, msg.room.roomID, msg.room);
                    // 检查是否是自己被踢了：如果自己不在新房间的玩家列表中，清除本地房间数据
                    {
                        string selfName = CurrentUsername;
                        bool isSelfKicked = true;
                        // 优先用 playerId 判断是否自己被踢（比字符串更可靠）
                        int selfPlayerId = CurrentPlayerId;
                        if (selfPlayerId > 0 && msg.room.playerIds != null)
                        {
                            for (int i = 0; i < msg.room.playerIds.Length; i++)
                            {
                                if (msg.room.playerIds[i] == selfPlayerId)
                                {
                                    isSelfKicked = false;
                                    break;
                                }
                            }
                        }
                        else if (msg.room.playerNames != null)
                        {
                            // 回退到字符串比较（离线模式或 playerId 不可用时）
                            for (int i = 0; i < msg.room.playerNames.Length; i++)
                            {
                                if (msg.room.playerNames[i].Equals(selfName))
                                {
                                    isSelfKicked = false;
                                    break;
                                }
                            }
                        }
                        if (isSelfKicked)
                        {
                            EventDispatcher.PostEvent(MessageEvent.OnDestroyRoomBack, this, true);
                            EventDispatcher.PostEvent(MessageEvent.UpdatePlayerRoomPack, this, null);
                        }
                    }
                    break;
                case "NoticeInRoomDestroy":
                    EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, "房间已被房主解散");
                    EventDispatcher.PostEvent(MessageEvent.OnDestroyRoomBack, this, true);
                    EventDispatcher.PostEvent(MessageEvent.UpdatePlayerRoomPack, this, null);
                    break;
            }
        }
        else
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, msg.message);
        }
    }

    private void OnChatRoomMessage(ChatRoomMessage msg)
    {
        EventDispatcher.PostEvent(MessageEvent.OnReciveChatRoomMsgUpdate, this, msg);
    }

    private void OnRoomUpdate(RoomInfo msg)
    {
        // 房间人数变化等通知
        EventDispatcher.PostEvent(MessageEvent.UpdatePlayerRoomPack, this, msg);
        EventDispatcher.PostEvent(MessageEvent.RoomPlayerRefreshRespond, this, msg);
    }

    private void OnStartGameResponse(StartGameResponse msg)
    {
        if (msg.success)
        {
            // 已在游戏场景中收到success,这是服务端"全员就绪"信号,直接触发倒计时
            if (!IsGameInProgress && (msg.gameModel == 1 || msg.gameModel == 2))
            {
                IsGameInProgress = true;
                EventDispatcher.PostEvent(MessageEvent.StartGameRuning, this, null);
                return;
            }
            EventDispatcher.PostEvent(MessageEvent.StartGame, this, msg);
        }
        else if (msg.gameModel == 1)
        {
            // 双人匹配: success=false表示加入等待队列, 仍需触发StartGame事件以打开匹配UI
            EventDispatcher.PostEvent(MessageEvent.StartGame, this, msg);
        }
        else
        {
            EventDispatcher.PostEvent(MessageEvent.OnShowUIMessage, this, msg.message);
        }
    }

    private void OnPlayerStateUpdate(PlayerStateMessage msg)
    {
        EventDispatcher.PostEvent(MessageEvent.UpdateNetPlayer, this, msg);
    }

    private void OnPlayerConnectionEvent(PlayerConnectionEvent msg)
    {
        EventDispatcher.PostEvent(MessageEvent.RoomPlayerOnlineStatus, this, msg);
    }

    private void OnQuitMatchResponse(QuitMatchResponse msg)
    {
        EventDispatcher.PostEvent(MessageEvent.OnQuitMatch, this, null);
    }

    private void OnGameOver(GameOverMessage msg)
    {
        IsGameInProgress = false;
        EventDispatcher.PostEvent(MessageEvent.GameOver, this, msg);
    }

    #endregion

    #region 业务 API (替代 RequestManager 中的 SendRequest 方法)

    // -------- Auth --------
    public void SendLogin(string username, string password)
    {
        CurrentUsername = username;
        SendToServer(new LoginMessage { username = username, password = password });
    }

    public void SendLogon(string username, string password)
    {
        SendToServer(new LogonMessage { username = username, password = password });
    }

    // -------- Room --------
    public void SendCreateRoom(string name, int maxNum)
    {
        SendToServer(new CreateRoomMessage { roomName = name, maxNum = maxNum });
    }

    public void SendFindRoom(string name = "", int maxNum = 0, int status = 0)
    {
        SendToServer(new FindRoomMessage { roomName = name, maxNum = maxNum, status = status });
    }

    public void SendJoinRoom(int roomID)
    {
        SendToServer(new JoinRoomMessage { roomID = roomID });
    }

    public void SendQuitRoom(int roomID)
    {
        SendToServer(new QuitRoomMessage { roomID = roomID });
    }

    public void SendDestroyRoom(int roomID)
    {
        SendToServer(new DestroyRoomMessage { roomID = roomID });
    }

    public void SendChatRoomMsg(int roomID, string content)
    {
        SendToServer(new ChatRoomMessage
        {
            roomID = roomID,
            senderName = CurrentUsername,
            content = content
        });
    }

    public void SendSwitchRoomMaster(int roomID, string newMasterName, int newMasterPlayerId)
    {
        SendToServer(new SwitchRoomMasterMessage { roomID = roomID, newMasterName = newMasterName, newMasterPlayerId = newMasterPlayerId });
    }

    public void SendKickoutRoom(int roomID, string username, int targetPlayerId)
    {
        SendToServer(new KickoutRoomMessage { roomID = roomID, kickoutUsername = username, targetPlayerId = targetPlayerId });
    }

    // -------- Game --------
    public void SendStartGame(int gameModel, int roomID, string[] players)
    {
        SendToServer(new StartGameMessage
        {
            gameModel = gameModel,
            roomID = roomID,
            playerNames = players
        });
    }

    public void SendEnterGameScene(int gameModel, int roomID)
    {
        SendToServer(new EnterGameSceneMessage
        {
            gameModel = gameModel,
            username = CurrentUsername,
            roomID = roomID
        });
    }

    /// <summary>
    /// 发送玩家状态 (不可靠通道，替代 UDP 位置同步)
    /// </summary>
    public void SendPlayerState(PlayerStateMessage state)
    {
        SendToServerUnreliable(state);
    }

    // -------- Match --------
    public void SendQuitMatch()
    {
        SendToServer(new QuitMatchMessage { username = CurrentUsername });
    }

    /// <summary>返回登录界面</summary>
    public void SendReturnLogin()
    {
        SendToServer(new ReturnLoginMessage { username = CurrentUsername });
    }

    #endregion

    #region 工具方法

    private void SetConnectionState(ConnectionState newState)
    {
        if (CurrentState != newState)
        {
            CurrentState = newState;
            OnConnectionStateChanged?.Invoke(newState);

            // 转发到旧的 EventDispatcher (兼容 UI)
            if (newState == ConnectionState.Connected)
            {
                EventDispatcher.PostEvent(MessageEvent.ServerConnectChange, this, MessageEventType.ClientOK);
            }
            else if (newState == ConnectionState.Disconnected)
            {
                EventDispatcher.PostEvent(MessageEvent.ServerConnectChange, this, MessageEventType.ClientFail);
            }
        }
    }

    public override void OnDestroy()
    {
        // 只有当本实例是真正的单例时才清理消息处理器。
        // 场景重载时 Mirror 会销毁重复的 NetworkManager，
        // 如果让副本也调用 Unregister，会把单例注册的处理器也一并移除。
        if (singleton == this)
        {
            UnregisterClientMessageHandlers();
            singleton = null;
        }

        base.OnDestroy();
    }

    #endregion
}
}
