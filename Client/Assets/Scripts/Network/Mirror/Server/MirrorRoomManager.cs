using System.Collections.Generic;
using Mirror;
using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 服务端房间管理器 (替代 RoomController + Server.cs 中的房间操作方法 + Room.cs)
///
/// 原架构对照：
///   Server.CreateRoom()      → OnServerCreateRoom()
///   Server.FindRoom()        → OnServerFindRoom()
///   Server.JoinRoom()        → OnServerJoinRoom()
///   Server.QuitRoom()        → OnServerQuitRoom()
///   Server.DestroyRoom()     → OnServerDestroyRoom()
///   Server.SwitchRoomMaster()→ OnServerSwitchRoomMaster()
///   Server.KickoutRoom()     → OnServerKickoutRoom()
///   Server.HandleChatRoomMsg()→ OnServerChatRoomMsg()
///   Room 类                    → RoomData 结构体 + roomDict
/// </summary>
public class MirrorRoomManager : NetworkBehaviour
{
    /// <summary>RoomID → 房间数据</summary>
    private static readonly Dictionary<int, RoomData> roomDict = new Dictionary<int, RoomData>();

    /// <summary>ConnectionID → 所在房间 ID</summary>
    private static readonly Dictionary<int, int> connRoomDict = new Dictionary<int, int>();

    /// <summary>ConnectionID → 用户名 (连接断开时的缓存)</summary>
    private static readonly Dictionary<int, string> connUsernameDict = new Dictionary<int, string>();

    private static int nextRoomID = 1000;

    /// <summary>房间数据 (替代原 RoomPack Protobuf 类)</summary>
    private class RoomData
    {
        public int roomID;
        public string roomName;
        public int maxNum;
        public int status;        // 1=等待, 2=满人, 3=游戏中
        public string masterName;
        public int masterPlayerId; // 房主的玩家ID
        public List<int> connIDs = new List<int>(); // 房间内的连接 ID 列表
        public List<int> playerIds = new List<int>(); // 房间内的玩家 ID 列表（与 connIDs 一一对应）
    }

    // Awake: AddComponent时同步注册 (Host模式客户端立即可发送消息)
    // OnStartServer: 后续每次服务重启时重新注册
    private void Awake() { RegisterHandlers(); }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RegisterHandlers();
        Debug.Log("[Mirror] 房间管理器 (服务器) 已启动");
    }

    public override void OnStopServer()
    {
        UnregisterHandlers();
        base.OnStopServer();
    }

    public void RegisterHandlers()
    {
        ServerMessageGuard.WrapHandler<CreateRoomMessage>(OnServerCreateRoom);
        ServerMessageGuard.WrapHandler<FindRoomMessage>(OnServerFindRoom);
        ServerMessageGuard.WrapHandler<JoinRoomMessage>(OnServerJoinRoom);
        ServerMessageGuard.WrapHandler<QuitRoomMessage>(OnServerQuitRoom);
        ServerMessageGuard.WrapHandler<DestroyRoomMessage>(OnServerDestroyRoom);
        ServerMessageGuard.WrapHandler<SwitchRoomMasterMessage>(OnServerSwitchRoomMaster);
        ServerMessageGuard.WrapHandler<KickoutRoomMessage>(OnServerKickoutRoom);
        ServerMessageGuard.WrapHandler<ChatRoomMessage>(OnServerChatRoomMsg);
    }

    private void UnregisterHandlers()
    {
        NetworkServer.UnregisterHandler<CreateRoomMessage>();
        NetworkServer.UnregisterHandler<FindRoomMessage>();
        NetworkServer.UnregisterHandler<JoinRoomMessage>();
        NetworkServer.UnregisterHandler<QuitRoomMessage>();
        NetworkServer.UnregisterHandler<DestroyRoomMessage>();
        NetworkServer.UnregisterHandler<SwitchRoomMasterMessage>();
        NetworkServer.UnregisterHandler<KickoutRoomMessage>();
        NetworkServer.UnregisterHandler<ChatRoomMessage>();
        roomDict.Clear();
        connRoomDict.Clear();
        connUsernameDict.Clear();
        nextRoomID = 1000;
    }

    // ==================== 创建房间 ====================

    private void OnServerCreateRoom(NetworkConnectionToClient conn, CreateRoomMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        // 检查是否已存在同名房间
        foreach (var room in roomDict.Values)
        {
            if (room.roomName == msg.roomName)
            {
                conn.Send(new RoomOperationResponse
                {
                    success = false,
                    message = "已存在同名房间"
                });
                return;
            }
        }

        int playerId = MirrorAuthManager.GetPlayerId(conn);
        int roomID = nextRoomID++;
        RoomData newRoom = new RoomData
        {
            roomID = roomID,
            roomName = msg.roomName,
            maxNum = msg.maxNum,
            status = 1,
            masterName = username,
            masterPlayerId = playerId
        };
        newRoom.connIDs.Add(conn.connectionId);
        newRoom.playerIds.Add(playerId);

        roomDict[roomID] = newRoom;
        connRoomDict[conn.connectionId] = roomID;
        connUsernameDict[conn.connectionId] = username;

        RoomInfo info = BuildRoomInfo(newRoom);
        conn.Send(new RoomOperationResponse
        {
            success = true,
            message = "CreateRoom",
            room = info
        });

        Debug.Log($"[Mirror Room] 创建房间: {msg.roomName} (ID={roomID}), 房主={username}");
    }

    // ==================== 查找房间 ====================

    private void OnServerFindRoom(NetworkConnectionToClient conn, FindRoomMessage msg)
    {
        var resultList = new List<RoomInfo>();

        foreach (var room in roomDict.Values)
        {
            // 更新房间状态
            if (room.status != 3)
            {
                room.status = room.connIDs.Count < room.maxNum ? 1 : 2;
            }

            bool hasQuery = !string.IsNullOrEmpty(msg.roomName) || msg.maxNum != 0 || msg.status != 0;
            if (hasQuery)
            {
                if (!string.IsNullOrEmpty(msg.roomName) && room.roomName == msg.roomName)
                {
                    resultList.Add(BuildRoomInfo(room));
                    break;
                }
                else if (msg.maxNum == room.maxNum || (msg.status != 0 && msg.status == room.status))
                {
                    resultList.Add(BuildRoomInfo(room));
                }
            }
            else
            {
                resultList.Add(BuildRoomInfo(room));
            }
        }

        conn.Send(new RoomListResponse
        {
            success = resultList.Count > 0,
            rooms = resultList.ToArray()
        });
    }

    // ==================== 加入房间 ====================

    private void OnServerJoinRoom(NetworkConnectionToClient conn, JoinRoomMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间不存在" });
            return;
        }

        if (room.status == 3)
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间正在游戏中" });
            return;
        }

        if (room.connIDs.Count >= room.maxNum)
        {
            room.status = 2;
            conn.Send(new RoomOperationResponse { success = false, message = "房间已满" });
            return;
        }

        int playerId = MirrorAuthManager.GetPlayerId(conn);
        room.connIDs.Add(conn.connectionId);
        room.playerIds.Add(playerId);
        room.status = room.connIDs.Count < room.maxNum ? 1 : 2;
        connRoomDict[conn.connectionId] = msg.roomID;
        connUsernameDict[conn.connectionId] = username;

        RoomInfo info = BuildRoomInfo(room);

        // 通知加入者
        conn.Send(new RoomOperationResponse
        {
            success = true,
            message = "JoinRoom",
            room = info
        });

        // 通知房间内其他玩家
        BroadcastToRoomExcept(room, conn, info);
        // 通知所有客户端（房间大厅实时刷新人数和状态）
        BroadcastRoomInfoToAll(info);

        Debug.Log($"[Mirror Room] {username} 加入房间 {msg.roomID}");
    }

    // ==================== 退出房间 ====================

    private void OnServerQuitRoom(NetworkConnectionToClient conn, QuitRoomMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间不存在" });
            return;
        }

        if (room.masterName == username)
        {
            // 房主退出 = 解散房间
            BroadcastToRoom(room, new RoomOperationResponse
            {
                success = true,
                message = "NoticeInRoomDestroy"
            });

            foreach (int cid in room.connIDs)
            {
                connRoomDict.Remove(cid);
                connUsernameDict.Remove(cid);
            }
            roomDict.Remove(msg.roomID);
            Debug.Log($"[Mirror Room] 房主退出，房间 {msg.roomID} 已解散");
        }
        else
        {
            // 同步移除 playerId（与 connID 同索引）
            int idx = room.connIDs.IndexOf(conn.connectionId);
            if (idx >= 0 && idx < room.playerIds.Count)
                room.playerIds.RemoveAt(idx);
            room.connIDs.Remove(conn.connectionId);
            room.status = room.connIDs.Count < room.maxNum ? 1 : 2;
            connRoomDict.Remove(conn.connectionId);
            connUsernameDict.Remove(conn.connectionId);

            RoomInfo info = BuildRoomInfo(room);
            conn.Send(new RoomOperationResponse
            {
                success = true,
                message = "QuitRoom",
                room = info
            });

            BroadcastToRoomExcept(room, conn, info);
            // 通知所有客户端（房间大厅实时刷新人数和状态）
            BroadcastRoomInfoToAll(info);
            Debug.Log($"[Mirror Room] {username} 退出房间 {msg.roomID}");
        }
    }

    // ==================== 解散房间 ====================

    private void OnServerDestroyRoom(NetworkConnectionToClient conn, DestroyRoomMessage msg)
    {
        string username = MirrorAuthManager.GetUsername(conn);

        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间不存在" });
            return;
        }

        if (room.masterName != username)
        {
            conn.Send(new RoomOperationResponse { success = false, message = "只有房主可以解散房间" });
            return;
        }

        BroadcastToRoom(room, new RoomOperationResponse
        {
            success = true,
            message = "NoticeInRoomDestroy"
        });

        foreach (int cid in room.connIDs)
        {
            connRoomDict.Remove(cid);
            connUsernameDict.Remove(cid);
        }
        roomDict.Remove(msg.roomID);

        conn.Send(new RoomOperationResponse { success = true, message = "DestroyRoom" });
        Debug.Log($"[Mirror Room] 房间 {msg.roomID} 已解散");
    }

    // ==================== 切换房主 ====================

    private void OnServerSwitchRoomMaster(NetworkConnectionToClient conn, SwitchRoomMasterMessage msg)
    {
        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间不存在" });
            return;
        }

        room.masterName = msg.newMasterName;
        room.masterPlayerId = msg.newMasterPlayerId;
        RoomInfo info = BuildRoomInfo(room);

        BroadcastToRoom(room, new RoomOperationResponse
        {
            success = true,
            message = "SwitchRoomMaster",
            room = info
        });
        // 通知所有客户端（房间大厅实时刷新房主信息）
        BroadcastRoomInfoToAll(info);

        Debug.Log($"[Mirror Room] 房主切换: {msg.roomID}, 新房主={msg.newMasterName}");
    }

    // ==================== 踢人 ====================

    private void OnServerKickoutRoom(NetworkConnectionToClient conn, KickoutRoomMessage msg)
    {
        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            conn.Send(new RoomOperationResponse { success = false, message = "房间不存在" });
            return;
        }

        // 优先用 targetPlayerId 查找（比用户名字符串查找更可靠）
        int kickConnID = 0;
        int kickIndex = -1;
        if (msg.targetPlayerId > 0)
        {
            // 直接通过 playerId 在 room.playerIds 中查找
            for (int i = 0; i < room.playerIds.Count && i < room.connIDs.Count; i++)
            {
                if (room.playerIds[i] == msg.targetPlayerId)
                {
                    kickConnID = room.connIDs[i];
                    kickIndex = i;
                    break;
                }
            }
        }
        // 回退到用户名查找（离线模式或旧客户端，kickIndex < 0 表示 playerId 查找未命中）
        if (kickIndex < 0)
        {
            foreach (int cid in room.connIDs)
            {
                if (connUsernameDict.TryGetValue(cid, out string name) && name == msg.kickoutUsername)
                {
                    kickConnID = cid;
                    kickIndex = room.connIDs.IndexOf(cid);
                    break;
                }
            }
        }

        // 注意：不能用 kickConnID != 0 判断，Host 模式下本地连接 connID 可能为 0
        if (kickIndex >= 0)
        {
            if (kickIndex < room.playerIds.Count)
                room.playerIds.RemoveAt(kickIndex);
            room.connIDs.Remove(kickConnID);
            connRoomDict.Remove(kickConnID);
            connUsernameDict.Remove(kickConnID);
            room.status = room.connIDs.Count < room.maxNum ? 1 : 2;

            RoomInfo info = BuildRoomInfo(room);
            BroadcastToRoom(room, new RoomOperationResponse
            {
                success = true,
                message = "KickoutRoom",
                room = info
            });
            // 通知所有客户端（房间大厅实时刷新人数和状态）
            BroadcastRoomInfoToAll(info);

            Debug.Log($"[Mirror Room] {msg.kickoutUsername} 被踢出房间 {msg.roomID}");
        }
        else
        {
            conn.Send(new RoomOperationResponse { success = false, message = "未找到该玩家" });
            Debug.LogWarning($"[Mirror Room] 踢人失败: 未找到玩家 {msg.kickoutUsername} 在房间 {msg.roomID}");
        }
    }

    // ==================== 聊天 ====================

    private void OnServerChatRoomMsg(NetworkConnectionToClient conn, ChatRoomMessage msg)
    {
        if (!roomDict.TryGetValue(msg.roomID, out RoomData room))
        {
            return;
        }

        BroadcastToRoom(room, msg);
    }

    // ==================== 工具方法 ====================

    private RoomInfo BuildRoomInfo(RoomData room)
    {
        var names = new List<string>();
        foreach (int cid in room.connIDs)
        {
            if (connUsernameDict.TryGetValue(cid, out string name))
            {
                names.Add(name);
            }
        }

        return new RoomInfo
        {
            roomID = room.roomID,
            roomName = room.roomName,
            maxNum = room.maxNum,
            status = room.status,
            roomMasterName = room.masterName,
            playerCount = room.connIDs.Count,
            playerNames = names.ToArray(),
            playerIds = room.playerIds.ToArray()
        };
    }

    private void BroadcastToRoom(RoomData room, object message)
    {
        foreach (int cid in room.connIDs)
        {
            if (NetworkServer.connections.TryGetValue(cid, out NetworkConnectionToClient targetConn))
            {
                if (message is RoomOperationResponse ror) targetConn.Send(ror);
                else if (message is ChatRoomMessage crm) targetConn.Send(crm);
                else if (message is RoomInfo ri) targetConn.Send(ri);
                else if (message is PlayerConnectionEvent pce) targetConn.Send(pce);
            }
        }
    }

    private void BroadcastToRoomExcept(RoomData room, NetworkConnectionToClient except, RoomInfo message)
    {
        foreach (int cid in room.connIDs)
        {
            if (cid == except.connectionId) continue;
            if (NetworkServer.connections.TryGetValue(cid, out NetworkConnectionToClient targetConn))
            {
                targetConn.Send(message);
            }
        }
    }

    /// <summary>
    /// 向所有连接的客户端广播房间信息更新（房间大厅实时刷新）
    /// </summary>
    private void BroadcastRoomInfoToAll(RoomInfo info)
    {
        foreach (var kvp in NetworkServer.connections)
        {
            kvp.Value.Send(info);
        }
    }

    // ==================== 公开 API ====================

    /// <summary>获取房间内所有成员的连接列表（供 MirrorServerGameManager 广播游戏开始）</summary>
    public static List<NetworkConnectionToClient> GetRoomConnections(int roomID)
    {
        var result = new List<NetworkConnectionToClient>();
        if (roomDict.TryGetValue(roomID, out RoomData room))
        {
            foreach (int cid in room.connIDs)
            {
                if (NetworkServer.connections.TryGetValue(cid, out NetworkConnectionToClient conn))
                    result.Add(conn);
            }
        }
        return result;
    }

    /// <summary>连接断开时清理 (供 CustomNetworkManager 调用)</summary>
    public static void OnPlayerDisconnected(NetworkConnectionToClient conn)
    {
        string username = MirrorAuthManager.GetUsername(conn) ?? "";
        int connID = conn.connectionId;

        if (connRoomDict.TryGetValue(connID, out int roomID))
        {
            if (roomDict.TryGetValue(roomID, out RoomData room))
            {
                // 同步移除 playerId
                int idx = room.connIDs.IndexOf(connID);
                if (idx >= 0 && idx < room.playerIds.Count)
                    room.playerIds.RemoveAt(idx);
                room.connIDs.Remove(connID);

                if (room.connIDs.Count == 0)
                {
                    roomDict.Remove(roomID);
                    Debug.Log($"[Mirror Room] 房间 {roomID} 因所有玩家退出而销毁");
                }
                else
                {
                    // 通知剩余玩家
                    foreach (int cid in room.connIDs)
                    {
                        if (NetworkServer.connections.TryGetValue(cid, out NetworkConnectionToClient targetConn))
                        {
                            targetConn.Send(new PlayerConnectionEvent
                            {
                                username = username,
                                playerId = MirrorAuthManager.GetPlayerId(conn),
                                isDisconnected = true
                            });
                        }
                    }
                }

                MirrorAuthManager.RecordDisconnect(username, roomID);
            }

            connRoomDict.Remove(connID);
            connUsernameDict.Remove(connID);
        }
    }
}
}
