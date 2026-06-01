using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 文件持久化数据库 (JSON)
/// 替代内存数据库，服务端重启后数据不丢失
/// </summary>
public class FileDatabaseProvider : IDatabaseProvider
{
    private readonly string _filePath;
    private DatabaseData _data;

    [Serializable]
    private class DatabaseData
    {
        public Dictionary<string, UserRecord> users = new Dictionary<string, UserRecord>();
        public Dictionary<string, string> sessions = new Dictionary<string, string>();
    }

    [Serializable]
    private class UserRecord
    {
        public string passwordHash;
        public string salt;
    }

    public FileDatabaseProvider(string filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Application.persistentDataPath, "user_database.json");
    }

    public void Initialize()
    {
        Load();
        Debug.Log($"[DB File] 文件数据库已初始化, 路径={_filePath}, 用户数={_data.users.Count}");
    }

    public bool UserRegister(string username, string password)
    {
        if (_data.users.ContainsKey(username))
        {
            Debug.Log($"[DB File] 注册失败: 用户 {username} 已存在");
            return false;
        }

        string salt = GenerateSalt();
        string hash = HashPassword(password, salt);

        _data.users[username] = new UserRecord { passwordHash = hash, salt = salt };
        Save();
        Debug.Log($"[DB File] 注册成功: {username}");
        return true;
    }

    public bool UserLogin(string username, string password)
    {
        if (!_data.users.TryGetValue(username, out UserRecord record))
        {
            Debug.Log($"[DB File] 登录失败: 用户 {username} 不存在");
            return false;
        }

        string hash = HashPassword(password, record.salt);
        if (hash == record.passwordHash)
        {
            Debug.Log($"[DB File] 登录成功: {username}");
            return true;
        }

        Debug.Log($"[DB File] 登录失败: {username} 密码错误");
        return false;
    }

    public void SaveSession(string username, string sessionId)
    {
        _data.sessions[username] = sessionId;
        Save();
    }

    public string GetSession(string username)
    {
        _data.sessions.TryGetValue(username, out string id);
        return id;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath, Encoding.UTF8);
                _data = JsonConvert.DeserializeObject<DatabaseData>(json) ?? new DatabaseData();
            }
            else
            {
                _data = new DatabaseData();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB File] 加载数据库失败: {e.Message}, 使用空白数据库");
            _data = new DatabaseData();
        }
    }

    private void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(_filePath, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB File] 保存数据库失败: {e.Message}");
        }
    }

    private static string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    private static string HashPassword(string password, string salt)
    {
        using (var sha256 = SHA256.Create())
        {
            string salted = password + salt;
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(salted));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
