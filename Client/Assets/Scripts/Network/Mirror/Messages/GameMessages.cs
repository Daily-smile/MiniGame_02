using Mirror;
using UnityEngine;

// ==================== 游戏同步消息 (替代原 ActionCode.UpdatePlayer / GamePack 等) ====================

/// <summary>
/// 玩家信息同步 (替代原 PlayerInfoPack UDP 同步)
/// 在 Mirror 中，位置和动画由 NetworkTransform/NetworkAnimator 自动处理
/// 此消息用于补充自定义状态同步
/// </summary>
public struct PlayerStateMessage : NetworkMessage
{
    public string username;
    public int playerID;
    public int hp;
    public bool isDead;
    public string playAnim;
    public int starCount;

    // 位置信息 (当不使用 NetworkTransform 时可手动同步)
    public float posX;
    public float posY;
    public float posZ;
    public bool isFlipX;
}

/// <summary>
/// 开始游戏请求 (替代原 ActionCode.StartGame)
/// </summary>
public struct StartGameMessage : NetworkMessage
{
    public int gameModel;   // 1=Double, 2=RoomTeam
    public int roomID;
    public string[] playerNames;
}

/// <summary>
/// 开始游戏响应
/// </summary>
public struct StartGameResponse : NetworkMessage
{
    public bool success;
    public string message;
    public int gameModel;
    /// <summary>对手玩家名 (双人匹配模式)</summary>
    public string opponentName;
}

/// <summary>
/// 进入游戏场景请求
/// </summary>
public struct EnterGameSceneMessage : NetworkMessage
{
    public int gameModel;
    public string username;
    public int roomID;
}

/// <summary>
/// 玩家断线/重连事件
/// </summary>
public struct PlayerConnectionEvent : NetworkMessage
{
    public string username;
    public int playerId;
    public bool isDisconnected;  // true=断线, false=重连
}

/// <summary>
/// 游戏结束
/// </summary>
public struct GameOverMessage : NetworkMessage
{
    public string winnerName;
    public int winnerScore;
}

/// <summary>
/// 道具/武器拾取通知
/// </summary>
public struct PropPickupMessage : NetworkMessage
{
    public string playerName;
    public int propID;
    public Vector3 position;
}

/// <summary>
/// 退出匹配响应
/// </summary>
public struct QuitMatchResponse : NetworkMessage
{
    public bool success;
}
