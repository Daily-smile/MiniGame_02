using Mirror;

/// <summary>
/// 注册请求 (替代原 ActionCode.Logon)
/// </summary>
public struct LogonMessage : NetworkMessage
{
    public string username;
    public string password;
}

/// <summary>
/// 注册响应
/// </summary>
public struct LogonResponse : NetworkMessage
{
    public bool success;
    public string message;
}

/// <summary>
/// 登录请求 (替代原 ActionCode.Login)
/// </summary>
public struct LoginMessage : NetworkMessage
{
    public string username;
    public string password;
}

/// <summary>
/// 登录响应
/// </summary>
public struct LoginResponse : NetworkMessage
{
    public bool success;
    public string sessionId;
    public string message;
    /// <summary>服务端分配的唯一玩家ID（离线/单机模式为 -1）</summary>
    public int playerId;
}

/// <summary>
/// 心跳 (替代原 ActionCode.Heartbeat)
/// Mirror 已内置超时检测，此消息可在需要自定义心跳逻辑时使用
/// </summary>
public struct HeartbeatMessage : NetworkMessage
{
    public long timestamp;
}

/// <summary>
/// 重连请求 (替代原 ActionCode.Reconnect)
/// </summary>
public struct ReconnectMessage : NetworkMessage
{
    public string username;
    public string sessionId;
}

/// <summary>
/// 返回登录界面 (替代原 ActionCode.ReturnLogin)
/// </summary>
public struct ReturnLoginMessage : NetworkMessage
{
    public string username;
}
