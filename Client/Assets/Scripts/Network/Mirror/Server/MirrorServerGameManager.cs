using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 服务端游戏管理器 (替代 GameController + 部分 Server.cs 游戏相关逻辑)
///
/// 原架构对照：
///   GameController.StartGame()            → OnServerStartGame()
///   GameController.QuitMatch()            → OnServerQuitMatch()
///   GameController.UpdatePlayer() (UDP)   → KCP Unreliable 通道自动中继
///   GameController.EnterGameSceneRquestStart() → OnServerEnterGameScene()
///   GameController 双人匹配 (RunMatchGameDoubleModel/EndMatchGameDoubleModel) → 内置匹配队列
/// </summary>
public class MirrorServerGameManager : NetworkBehaviour
{
    /// <summary>双人匹配队列</summary>
    private static readonly Queue<MatchEntry> doubleMatchQueue = new Queue<MatchEntry>();

    /// <summary>匹配成功的对局 (username → opponent connection ID)</summary>
    private static readonly Dictionary<string, int> matchOKDict = new Dictionary<string, int>();

    /// <summary>匹配倒计时 (username → 剩余秒数)</summary>
    private static readonly Dictionary<string, float> matchTimers = new Dictionary<string, float>();

    /// <summary>进入游戏场景等待队列</summary>
    private static readonly Dictionary<string, bool> enterGameReady = new Dictionary<string, bool>();

    private const float MATCH_TIMEOUT = 20f; // 匹配超时 20 秒
    private const float MATCH_CHECK_INTERVAL = 1f; // 每秒检查一次

    private struct MatchEntry
    {
        public int connID;
        public string username;
    }

    // Awake: AddComponent时同步注册 (Host模式客户端立即可发送消息)
    // OnStartServer: 后续每次服务重启时重新注册
    private void Awake() { RegisterHandlers(); }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RegisterHandlers();
        Debug.Log("[Mirror] 游戏管理器 (服务器) 已启动");
    }

    public override void OnStopServer()
    {
        UnregisterHandlers();
        CleanupMatchState();
        base.OnStopServer();
    }

    public void RegisterHandlers()
    {
        ServerMessageGuard.WrapHandler<StartGameMessage>(OnServerStartGame);
        ServerMessageGuard.WrapHandler<QuitMatchMessage>(OnServerQuitMatch);
        ServerMessageGuard.WrapHandler<EnterGameSceneMessage>(OnServerEnterGameScene);
    }

    private void UnregisterHandlers()
    {
        NetworkServer.UnregisterHandler<StartGameMessage>();
        NetworkServer.UnregisterHandler<QuitMatchMessage>();
        NetworkServer.UnregisterHandler<EnterGameSceneMessage>();
    }

    private void CleanupMatchState()
    {
        doubleMatchQueue.Clear();
        matchOKDict.Clear();
        matchTimers.Clear();
        lock (enterGameReady) { enterGameReady.Clear(); }
    }

    private void Update()
    {
        if (!NetworkServer.active) return;

        // 每秒检查匹配状态
        ProcessMatchmaking(Time.deltaTime);
    }

    // ==================== 匹配逻辑 ====================

    private void ProcessMatchmaking(float dt)
    {
        // 更新所有匹配倒计时 (对keys快照迭代，避免修改字典值导致CollectionModified异常)
        var expiredUsers = new List<string>();
        var timerKeys = new List<string>(matchTimers.Keys);
        foreach (string user in timerKeys)
        {
            if (!matchTimers.ContainsKey(user)) continue;
            matchTimers[user] -= dt;
            if (matchTimers[user] <= 0)
            {
                expiredUsers.Add(user);
            }
        }

        // 处理超时的匹配请求（匹配失败）
        foreach (string user in expiredUsers)
        {
            matchTimers.Remove(user);
            RemoveFromQueue(user);
            SendMatchResult(user, false, null);
        }

        // 尝试配对
        while (doubleMatchQueue.Count >= 2)
        {
            MatchEntry player1 = doubleMatchQueue.Dequeue();
            MatchEntry player2 = doubleMatchQueue.Dequeue();

            matchTimers.Remove(player1.username);
            matchTimers.Remove(player2.username);

            // 双向记录匹配成功
            matchOKDict[player1.username] = player2.connID;
            matchOKDict[player2.username] = player1.connID;

            // 通知双方匹配成功
            SendMatchResult(player1.username, true, player2.username);
            SendMatchResult(player2.username, true, player1.username);

            Debug.Log($"[Mirror Game] 匹配成功: {player1.username} vs {player2.username}");
        }
    }

    private void RemoveFromQueue(string username)
    {
        // Queue doesn't support remove, rebuild
        var remaining = new Queue<MatchEntry>();
        while (doubleMatchQueue.Count > 0)
        {
            MatchEntry entry = doubleMatchQueue.Dequeue();
            if (entry.username != username)
            {
                remaining.Enqueue(entry);
            }
        }
        // Re-fill the queue
        while (remaining.Count > 0)
        {
            doubleMatchQueue.Enqueue(remaining.Dequeue());
        }
    }

    private void SendMatchResult(string username, bool success, string opponentName)
    {
        // 找到连接并发送结果
        foreach (var kvp in NetworkServer.connections)
        {
            if (MirrorAuthManager.GetUsername(kvp.Value) == username)
            {
                kvp.Value.Send(new StartGameResponse
                {
                    success = success,
                    gameModel = 1,
                    message = success ? "" : "匹配超时，未找到对手",
                    opponentName = opponentName
                });
                break;
            }
        }
    }

    // ==================== 开始游戏 (替代 GameController.StartGame) ====================

    private void OnServerStartGame(NetworkConnectionToClient conn, StartGameMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        if (msg.gameModel == 2) // RoomTeam
        {
            // 组队模式：通知房间内所有人开始游戏（不只是房主）
            var response = new StartGameResponse
            {
                success = true,
                gameModel = msg.gameModel,
                message = ""
            };
            var roomConns = MirrorRoomManager.GetRoomConnections(msg.roomID);
            foreach (var memberConn in roomConns)
            {
                memberConn.Send(response);
            }

            Debug.Log($"[Mirror Game] 组队模式开始游戏, RoomID={msg.roomID}, 通知 {roomConns.Count} 名成员");
        }
        else if (msg.gameModel == 1) // Double
        {
            // 双人匹配模式
            if (doubleMatchQueue.Count == 0)
            {
                // 加入匹配队列
                doubleMatchQueue.Enqueue(new MatchEntry
                {
                    connID = conn.connectionId,
                    username = username
                });
                matchTimers[username] = MATCH_TIMEOUT;

                conn.Send(new StartGameResponse
                {
                    success = false,
                    gameModel = msg.gameModel,
                    message = "匹配中..."
                });

                Debug.Log($"[Mirror Game] {username} 加入双人匹配队列");
            }
            else
            {
                // 立即匹配
                MatchEntry opponent = doubleMatchQueue.Dequeue();

                // 防止自匹配 (同一玩家重复点击)
                if (opponent.username == username || opponent.connID == conn.connectionId)
                {
                    doubleMatchQueue.Enqueue(opponent);
                    matchTimers[username] = MATCH_TIMEOUT;
                    conn.Send(new StartGameResponse { success = false, gameModel = msg.gameModel, message = "匹配中..." });
                    Debug.LogWarning($"[Mirror Game] 阻止自匹配: {username}");
                    return;
                }

                // 检查对方是否仍然在线
                if (!NetworkServer.connections.ContainsKey(opponent.connID))
                {
                    conn.Send(new StartGameResponse { success = false, gameModel = msg.gameModel, message = "匹配中..." });
                    doubleMatchQueue.Enqueue(new MatchEntry { connID = conn.connectionId, username = username });
                    matchTimers[username] = MATCH_TIMEOUT;
                    Debug.LogWarning($"[Mirror Game] 对手已断线: {opponent.username}");
                    return;
                }

                matchTimers.Remove(opponent.username);
                matchOKDict[username] = opponent.connID;
                matchOKDict[opponent.username] = conn.connectionId;

                SendMatchResult(username, true, opponent.username);
                SendMatchResult(opponent.username, true, username);
            }
        }
        else if (msg.gameModel == 0) // Single
        {
            // 单人模式：无需匹配，直接通知客户端进入游戏场景
            conn.Send(new StartGameResponse
            {
                success = true,
                gameModel = msg.gameModel,
                message = ""
            });

            Debug.Log($"[Mirror Game] 单人模式开始游戏: {username}");
        }
        else
        {
            conn.Send(new StartGameResponse { success = false, message = "无效的游戏模式" });
        }
    }

    // ==================== 退出匹配 (替代 GameController.QuitMatch) ====================

    private void OnServerQuitMatch(NetworkConnectionToClient conn, QuitMatchMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        matchTimers.Remove(username);
        RemoveFromQueue(username);

        if (matchOKDict.TryGetValue(username, out int opponentID))
        {
            matchOKDict.Remove(username);
            matchOKDict.Remove(GetUsernameByConnID(opponentID));
        }

        conn.Send(new QuitMatchResponse { success = true });
        Debug.Log($"[Mirror Game] {username} 退出匹配");
    }

    // ==================== 进入游戏场景 (替代 GameController.EnterGameSceneRquestStart) ====================

    private void OnServerEnterGameScene(NetworkConnectionToClient conn, EnterGameSceneMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        lock (enterGameReady)
        {
            enterGameReady[username] = true;
        }

        // 双人模式：检查双方是否都准备好了
        if (msg.gameModel == 1 && matchOKDict.TryGetValue(username, out int opponentID))
        {
            string opponentName = GetUsernameByConnID(opponentID);
            bool opponentReady = false;
            lock (enterGameReady)
            {
                opponentReady = enterGameReady.TryGetValue(opponentName, out bool ready) && ready;
            }

            if (opponentReady && NetworkServer.connections.TryGetValue(opponentID, out NetworkConnectionToClient oppConn))
            {
                lock (enterGameReady)
                {
                    enterGameReady.Remove(username);
                    enterGameReady.Remove(opponentName);
                }

                // 双方场景就绪, 为两个连接生成网络玩家
                SpawnPlayerForConnection(conn, username);
                SpawnPlayerForConnection(oppConn, opponentName);

                // 通知双方游戏就绪,开始倒计时
                conn.Send(new StartGameResponse { success = true, gameModel = msg.gameModel, message = "" });
                oppConn.Send(new StartGameResponse { success = true, gameModel = msg.gameModel, message = "" });

                // 启动服务端游戏逻辑
                MirrorGameManager.singleton?.ServerStartGame();

                Debug.Log($"[Mirror Game] 双人模式双方就绪，游戏开始: {username} vs {opponentName}");
            }
        }
        // 组队模式：检查房间内所有成员是否都已进入场景
        else if (msg.gameModel == 2)
        {
            var roomConns = MirrorRoomManager.GetRoomConnections(msg.roomID);
            bool allReady = true;
            foreach (var memberConn in roomConns)
            {
                string memberName = MirrorAuthManager.GetUsername(memberConn);
                bool ready = false;
                lock (enterGameReady)
                {
                    ready = enterGameReady.TryGetValue(memberName, out bool r) && r;
                }
                if (!ready)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                // 清理就绪标记
                lock (enterGameReady)
                {
                    foreach (var memberConn in roomConns)
                    {
                        string memberName = MirrorAuthManager.GetUsername(memberConn);
                        enterGameReady.Remove(memberName);
                    }
                }

                // 为所有房间成员生成网络玩家
                foreach (var memberConn in roomConns)
                {
                    string memberName = MirrorAuthManager.GetUsername(memberConn);
                    SpawnPlayerForConnection(memberConn, memberName);
                }

                // 通知所有人游戏就绪，开始倒计时
                var goResponse = new StartGameResponse { success = true, gameModel = msg.gameModel, message = "" };
                foreach (var memberConn in roomConns)
                {
                    memberConn.Send(goResponse);
                }

                // 启动服务端游戏逻辑
                MirrorGameManager.singleton?.ServerStartGame();

                Debug.Log($"[Mirror Game] 组队模式全部就绪，{roomConns.Count} 名玩家，游戏开始");
            }
        }
    }

    private void SpawnPlayerForConnection(NetworkConnectionToClient conn, string playerName)
    {
        // 防御性清理：如果该连接上已有旧的玩家对象（返回菜单时场景卸载导致未清理），先销毁
        if (conn.identity != null)
        {
            Debug.LogWarning($"[Mirror Game] 连接 {conn.connectionId} 上存在旧玩家对象 {conn.identity.name}，先销毁再生成新玩家");
            NetworkServer.Destroy(conn.identity.gameObject);
        }

        GameObject playerPrefab = CustomNetworkManager.singleton.playerPrefab;
        if (playerPrefab == null)
        {
            Debug.LogError("[Mirror Game] playerPrefab 未配置, 无法生成玩家");
            return;
        }

        GameObject playerObj = Instantiate(playerPrefab);
        MirrorPlayer mp = playerObj.GetComponent<MirrorPlayer>();
        if (mp != null) mp.playerName = playerName;

        NetworkServer.AddPlayerForConnection(conn, playerObj);
        Debug.Log($"[Mirror Game] 为连接 {conn.connectionId} 生成玩家: {playerName}");
    }

    // ==================== 工具方法 ====================

    private string GetUsernameByConnID(int connID)
    {
        if (NetworkServer.connections.TryGetValue(connID, out NetworkConnectionToClient conn))
        {
            return MirrorAuthManager.GetUsername(conn) ?? "";
        }
        return "";
    }

    /// <summary>连接断开时清理匹配状态</summary>
    public static void OnPlayerDisconnected(NetworkConnectionToClient conn)
    {
        string username = MirrorAuthManager.GetUsername(conn) ?? "";
        matchTimers.Remove(username);
        matchOKDict.Remove(username);
        lock (enterGameReady)
        {
            enterGameReady.Remove(username);
        }
    }
}
