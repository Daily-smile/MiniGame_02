using System.Collections.Generic;
using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 内存数据库实现 (默认，无需 MySQL)
/// 服务端重启后数据会丢失，适用于开发测试环境
/// </summary>
public class MemoryDatabaseProvider : IDatabaseProvider
{
    private readonly Dictionary<string, string> _userPasswords = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _userSessions = new Dictionary<string, string>();

    public void Initialize()
    {
        _userPasswords.Clear();
        _userSessions.Clear();
        Debug.Log("[DB Memory] 内存数据库已初始化");
    }

    public bool UserRegister(string username, string password)
    {
        if (_userPasswords.ContainsKey(username))
        {
            Debug.Log($"[DB Memory] 注册失败: 用户 {username} 已存在");
            return false;
        }

        _userPasswords[username] = password;
        Debug.Log($"[DB Memory] 注册成功: {username}");
        return true;
    }

    public bool UserLogin(string username, string password)
    {
        if (_userPasswords.TryGetValue(username, out string storedPwd) && storedPwd == password)
        {
            Debug.Log($"[DB Memory] 登录成功: {username}");
            return true;
        }

        Debug.Log($"[DB Memory] 登录失败: {username}");
        return false;
    }

    public void SaveSession(string username, string sessionId)
    {
        _userSessions[username] = sessionId;
    }

    public string GetSession(string username)
    {
        _userSessions.TryGetValue(username, out string id);
        return id;
    }
}
}
