using Mirror;
using UnityEngine;

/// <summary>
/// 服务端消息处理安全包装器
/// 防止单个消息处理异常导致服务端崩溃
/// </summary>
public static class ServerMessageGuard
{
    /// <summary>
    /// 包装 NetworkMessage 处理器，捕获所有异常并记录日志
    /// </summary>
    public static void WrapHandler<T>(System.Action<NetworkConnectionToClient, T> handler)
        where T : struct, NetworkMessage
    {
        NetworkServer.ReplaceHandler<T>((conn, msg) =>
        {
            try
            {
                handler(conn, msg);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServerGuard] 处理消息 {typeof(T).Name} 时异常: {e}");
                try
                {
                    conn?.Send(new ErrorResponse { message = "服务器内部错误" });
                }
                catch { }
            }
        });
    }
}

/// <summary>
/// 服务端通用错误响应
/// </summary>
public struct ErrorResponse : NetworkMessage
{
    public string message;
}
