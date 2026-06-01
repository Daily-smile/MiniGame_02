using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 服务端认证管理器 (替代 UserController + 部分 Client.cs 的 Login/Logon/Reconnect 逻辑)
///
/// 原架构对照：
///   UserController.Logon()          → OnServerLogon()
///   UserController.Login()          → OnServerLogin()
///   UserController.Reconnect()      → OnServerReconnect()
///   Server.HandleReconnect()        → OnServerReconnect()
///   Client.SessionId               → conn.authenticationData / sessionDict
///   UserController.ReturnLogin()   → OnServerReturnLogin()
/// </summary>
public class MirrorAuthManager : NetworkBehaviour
{
    /// <summary>用户名 → SessionId 映射 (替代 Server.disconnectedClients + 数据库 session_id)</summary>
    private static readonly Dictionary<string, string> sessionDict = new Dictionary<string, string>();

    /// <summary>SessionId → 断线时间和房间信息 (用于重连窗口)</summary>
    private static readonly Dictionary<string, DisconnectInfo> disconnectInfoDict = new Dictionary<string, DisconnectInfo>();

    /// <summary>玩家ID自增器</summary>
    private static int _nextPlayerId = 1;

    /// <summary>connID → playerId 映射</summary>
    private static readonly Dictionary<int, int> connToPlayerId = new Dictionary<int, int>();

    /// <summary>playerId → connID 映射</summary>
    private static readonly Dictionary<int, int> playerIdToConn = new Dictionary<int, int>();

    /// <summary>当前已登录的用户名集合（防止重复登录）</summary>
    private static readonly HashSet<string> activeLogins = new HashSet<string>();

    private const float RECONNECT_WINDOW = 15f; // 重连窗口 15 秒

    private struct DisconnectInfo
    {
        public float disconnectTime;
        public int roomID;
        public string username;
    }

    // Awake: AddComponent时同步注册 (Host模式客户端立即可发送消息)
    // OnStartServer: 后续每次服务重启时重新注册
    private void Awake() { RegisterHandlers(); }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RegisterHandlers();
        DB.Provider.Initialize();
        Debug.Log("[Mirror] 认证管理器 (服务器) 已启动");
    }

    public override void OnStopServer()
    {
        UnregisterHandlers();
        base.OnStopServer();
    }

    public void RegisterHandlers()
    {
        ServerMessageGuard.WrapHandler<LogonMessage>(OnServerLogon);
        ServerMessageGuard.WrapHandler<LoginMessage>(OnServerLogin);
        ServerMessageGuard.WrapHandler<ReconnectMessage>(OnServerReconnect);
        ServerMessageGuard.WrapHandler<ReturnLoginMessage>(OnServerReturnLogin);
    }

    private void UnregisterHandlers()
    {
        NetworkServer.UnregisterHandler<LogonMessage>();
        NetworkServer.UnregisterHandler<LoginMessage>();
        NetworkServer.UnregisterHandler<ReconnectMessage>();
        NetworkServer.UnregisterHandler<ReturnLoginMessage>();
        sessionDict.Clear();
        disconnectInfoDict.Clear();
    }

    // ==================== 注册 (替代 UserController.Logon) ====================

    private void OnServerLogon(NetworkConnectionToClient conn, LogonMessage msg)
    {
        string username = msg.username;
        string password = msg.password;
        string sessionId = System.Guid.NewGuid().ToString("N");

        bool success = false;
        string errMsg = "";

        try
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                errMsg = "用户名或密码不能为空";
            }
            else
            {
                success = DB.Provider.UserRegister(username, password);
                if (!success)
                    errMsg = "用户名已被注册";
            }
        }
        catch (System.Exception e)
        {
            errMsg = $"注册失败: {e.Message}";
        }

        conn.Send(new LogonResponse { success = success, message = errMsg });
    }

    // ==================== 登录 (替代 UserController.Login) ====================

    private void OnServerLogin(NetworkConnectionToClient conn, LoginMessage msg)
    {
        string username = msg.username;
        string password = msg.password;

        bool success = false;
        string sessionId = "";
        string errMsg = "";

        try
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                errMsg = "用户名或密码不能为空";
            }
            else if (IsAlreadyLoggedIn(username, conn))
            {
                errMsg = "该用户名已被登陆";
            }
            else if (DB.Provider.UserLogin(username, password))
            {
                sessionId = System.Guid.NewGuid().ToString("N");
                DB.Provider.SaveSession(username, sessionId);
                sessionDict[username] = sessionId;
                conn.authenticationData = username;

                // 分配唯一玩家ID
                int playerId = _nextPlayerId++;
                connToPlayerId[conn.connectionId] = playerId;
                playerIdToConn[playerId] = conn.connectionId;
                activeLogins.Add(username);

                success = true;
                Debug.Log($"[Mirror Auth] 登录成功: {username}, playerId={playerId}");
            }
            else
            {
                errMsg = "用户名或密码错误";
            }
        }
        catch (System.Exception e)
        {
            errMsg = $"登录失败: {e.Message}";
        }

        int assignedPlayerId = connToPlayerId.TryGetValue(conn.connectionId, out int pid) ? pid : -1;
        conn.Send(new LoginResponse
        {
            success = success,
            sessionId = sessionId,
            message = errMsg,
            playerId = assignedPlayerId
        });
    }

    // ==================== 重连 (替代 UserController.Reconnect + Server.HandleReconnect) ====================

    private void OnServerReconnect(NetworkConnectionToClient conn, ReconnectMessage msg)
    {
        bool success = false;
        string errMsg = "";

        if (sessionDict.TryGetValue(msg.username, out string storedSessionId) &&
            storedSessionId == msg.sessionId &&
            disconnectInfoDict.TryGetValue(msg.sessionId, out DisconnectInfo info))
        {
            float elapsed = Time.time - info.disconnectTime;
            if (elapsed <= RECONNECT_WINDOW)
            {
                success = true;
                conn.authenticationData = msg.username;

                // 从断开字典中移除
                disconnectInfoDict.Remove(msg.sessionId);

                Debug.Log($"[Mirror Auth] 重连成功: {msg.username}, RoomID={info.roomID}");
            }
            else
            {
                errMsg = "重连超时";
                sessionDict.Remove(msg.username);
                disconnectInfoDict.Remove(msg.sessionId);
            }
        }
        else
        {
            errMsg = "Session无效或已过期";
        }

        conn.Send(new LoginResponse
        {
            success = success,
            sessionId = msg.sessionId,
            message = errMsg
        });
    }

    // ==================== 返回登录 (替代 UserController.ReturnLogin) ====================

    private void OnServerReturnLogin(NetworkConnectionToClient conn, ReturnLoginMessage msg)
    {
        string username = msg.username;

        if (sessionDict.TryGetValue(username, out string sessionId))
        {
            // 记录断开信息用于后续重连
            disconnectInfoDict[sessionId] = new DisconnectInfo
            {
                disconnectTime = Time.time,
                roomID = 0, // MirrorRoomManager 会更新这个值
                username = username
            };
        }

        // 清理该连接上的旧玩家对象（游戏结束后返回菜单时）
        if (conn.identity != null)
        {
            NetworkServer.Destroy(conn.identity.gameObject);
            Debug.Log($"[Mirror Auth] 已销毁玩家 {username} 的旧玩家对象");
        }

        // 清理该玩家的匹配/游戏状态、活跃登录和 playerId 映射
        MirrorServerGameManager.OnPlayerDisconnected(conn);
        RemoveActiveLogin(conn);

        Debug.Log($"[Mirror Auth] 玩家返回登录: {username}");
    }

    // ==================== 工具方法 ====================

    /// <summary>记录玩家断线 (供 MirrorRoomManager 调用)</summary>
    public static void RecordDisconnect(string username, int roomID)
    {
        if (sessionDict.TryGetValue(username, out string sessionId))
        {
            disconnectInfoDict[sessionId] = new DisconnectInfo
            {
                disconnectTime = Time.time,
                roomID = roomID,
                username = username
            };
        }
    }

    /// <summary>清理断开记录 (重连成功后)</summary>
    public static void ClearDisconnect(string username)
    {
        if (sessionDict.TryGetValue(username, out string sessionId))
        {
            disconnectInfoDict.Remove(sessionId);
        }
    }

    /// <summary>获取用户名对应的 SessionId</summary>
    public static string GetSessionId(string username)
    {
        sessionDict.TryGetValue(username, out string id);
        return id;
    }

    /// <summary>获取连接对应的用户名</summary>
    public static string GetUsername(NetworkConnectionToClient conn)
    {
        return conn.authenticationData as string;
    }

    /// <summary>检查用户名是否已在其他连接上登录</summary>
    private static bool IsAlreadyLoggedIn(string username, NetworkConnectionToClient selfConn)
    {
        foreach (var kvp in NetworkServer.connections)
        {
            if (kvp.Value == selfConn) continue;
            string authName = kvp.Value.authenticationData as string;
            if (!string.IsNullOrEmpty(authName) && authName == username)
                return true;
        }
        return false;
    }

    /// <summary>获取连接对应的玩家ID</summary>
    public static int GetPlayerId(NetworkConnectionToClient conn)
    {
        if (connToPlayerId.TryGetValue(conn.connectionId, out int pid))
            return pid;
        return -1;
    }

    /// <summary>根据玩家ID获取连接</summary>
    public static NetworkConnectionToClient GetConnByPlayerId(int playerId)
    {
        if (playerIdToConn.TryGetValue(playerId, out int connId)
            && NetworkServer.connections.TryGetValue(connId, out NetworkConnectionToClient conn))
            return conn;
        return null;
    }

    /// <summary>断开连接时清理 playerId 映射和活跃登录</summary>
    public static void ClearPlayerId(int connId)
    {
        if (connToPlayerId.TryGetValue(connId, out int pid))
        {
            connToPlayerId.Remove(connId);
            playerIdToConn.Remove(pid);
        }
    }

    /// <summary>移除活跃登录记录（断线/返回登录时调用）</summary>
    public static void RemoveActiveLogin(NetworkConnectionToClient conn)
    {
        string username = GetUsername(conn);
        if (!string.IsNullOrEmpty(username))
            activeLogins.Remove(username);
        ClearPlayerId(conn.connectionId);
    }
}
