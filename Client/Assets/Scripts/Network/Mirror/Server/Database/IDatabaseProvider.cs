using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 数据库抽象接口
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>初始化数据库 (建表等)</summary>
    void Initialize();

    /// <summary>注册用户</summary>
    bool UserRegister(string username, string password);

    /// <summary>登录验证</summary>
    bool UserLogin(string username, string password);

    /// <summary>保存 SessionId</summary>
    void SaveSession(string username, string sessionId);

    /// <summary>获取 SessionId</summary>
    string GetSession(string username);
}
}
