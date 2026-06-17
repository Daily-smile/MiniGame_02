using UnityEngine;
using LF.Framework;

namespace LF.Network
{
/// <summary>
/// 数据库静态入口 (替代原 DBManager / DatabaseManager)
///
/// 默认使用内存数据库。要接入 MySQL：
///   1. 将 MySql.Data.dll 放入 Assets/Plugins/
///   2. 创建 MySQLDatabaseProvider : IDatabaseProvider
///   3. 在服务启动时: DB.SetProvider(new MySQLDatabaseProvider())
/// </summary>
public static class DB
{
    private static IDatabaseProvider _provider;

    /// <summary>当前使用的数据库实现</summary>
    public static IDatabaseProvider Provider
    {
        get
        {
            if (_provider == null)
            {
                _provider = new FileDatabaseProvider();
                _provider.Initialize();
            }
            return _provider;
        }
    }

    /// <summary>替换数据库实现 (接入 MySQL 时使用)</summary>
    public static void SetProvider(IDatabaseProvider provider)
    {
        _provider = provider;
        provider.Initialize();
        Debug.Log($"[DB] 数据库已切换为: {provider.GetType().Name}");
    }
}
}
