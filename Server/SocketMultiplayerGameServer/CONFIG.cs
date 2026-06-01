using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer
{
    public static class CONFIG
    {
        public static readonly int HEARTBEAT_TIMEOUT = 2; // 心跳超时时间(秒)
        public static readonly int HEARTBEAT_TIME = 1000; // 心跳计时器(毫秒)
        public static readonly int ROOMCHECK_TIME = 2000; // 房间检查计时器(毫秒)
        public static readonly int RECONNECT_WINDOW = 15; // 允许重连的时间窗口(秒)
        public static readonly int CACHES_TIMEOUT = 15; // 缓存检查时间(秒)

        public static readonly int INITIAL_BUFFER_SIZE = 1024; // 消息Buff大小
        public static readonly int MAX_BUFFER_SIZE = 1024 * 1024 * 5; // 消息Buff，5MB最大缓冲区

        // 数据库连接池配置
        public static readonly int DB_CONNECTION_POOL_MIN_SIZE = 5;
        public static readonly int DB_CONNECTION_POOL_MAX_SIZE = 20;
        public static readonly int DB_CONNECTION_TIMEOUT = 15; // 连接超时时间(秒)

        public static string GetConnectionString()
        {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                Database = "test",
                UserID = "root",
                Password = "314159",
                CharacterSet = "utf8mb4"
            };
            return builder.ConnectionString;
        }
    }
}
