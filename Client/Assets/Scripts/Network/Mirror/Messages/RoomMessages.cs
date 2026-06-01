using Mirror;

// ==================== 房间管理消息 (替代原 ActionCode.Room 系列) ====================

/// <summary>
/// 创建房间请求
/// </summary>
public struct CreateRoomMessage : NetworkMessage
{
    public string roomName;
    public int maxNum;
}

/// <summary>
/// 查询房间请求
/// </summary>
public struct FindRoomMessage : NetworkMessage
{
    public string roomName;   // 按名称查询
    public int maxNum;        // 按人数过滤
    public int status;        // 按状态过滤
}

/// <summary>
/// 加入房间请求
/// </summary>
public struct JoinRoomMessage : NetworkMessage
{
    public int roomID;
}

/// <summary>
/// 退出房间请求
/// </summary>
public struct QuitRoomMessage : NetworkMessage
{
    public int roomID;
}

/// <summary>
/// 解散房间请求 (房主)
/// </summary>
public struct DestroyRoomMessage : NetworkMessage
{
    public int roomID;
}

/// <summary>
/// 切换房主请求
/// </summary>
public struct SwitchRoomMasterMessage : NetworkMessage
{
    public int roomID;
    public string newMasterName;
    /// <summary>新房主的唯一玩家ID</summary>
    public int newMasterPlayerId;
}

/// <summary>
/// 踢人请求
/// </summary>
public struct KickoutRoomMessage : NetworkMessage
{
    public int roomID;
    public string kickoutUsername;
    /// <summary>被踢玩家的唯一ID</summary>
    public int targetPlayerId;
}

/// <summary>
/// 房间信息 (Mirror 风格，替代原 RoomPack)
/// </summary>
public struct RoomInfo : NetworkMessage
{
    public int roomID;
    public string roomName;
    public int maxNum;
    public int status;            // 1=等待, 2=满人, 3=游戏中
    public string roomMasterName;
    public int playerCount;
    public string[] playerNames;
    /// <summary>房间内玩家的唯一ID列表（与 playerNames 一一对应）</summary>
    public int[] playerIds;
}

/// <summary>
/// 房间列表响应
/// </summary>
public struct RoomListResponse : NetworkMessage
{
    public bool success;
    public string message;
    public RoomInfo[] rooms;
}

/// <summary>
/// 房间操作通用响应
/// </summary>
public struct RoomOperationResponse : NetworkMessage
{
    public bool success;
    public string message;    // 对应原 ReturnCode
    public RoomInfo room;
}

/// <summary>
/// 聊天消息请求
/// </summary>
public struct ChatRoomMessage : NetworkMessage
{
    public int roomID;
    public string senderName;
    public string content;
}

// ==================== 匹配消息 (替代原 ActionCode.QuitMatch 等) ====================

/// <summary>
/// 匹配模式请求
/// </summary>
public struct MatchRequest : NetworkMessage
{
    public int gameModel;   // 1=Double, 2=RoomTeam
}

/// <summary>
/// 退出匹配
/// </summary>
public struct QuitMatchMessage : NetworkMessage
{
    public string username;
}
