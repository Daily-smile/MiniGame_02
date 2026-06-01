using System;

namespace SocketMultiplayerGameServer.Database
{
    public static class DatabaseManager
    {
        private static readonly Lazy<DatabaseConnectionManager> _instance =
            new Lazy<DatabaseConnectionManager>(() =>
                new DatabaseConnectionManager(CONFIG.GetConnectionString(), CONFIG.DB_CONNECTION_POOL_MIN_SIZE, CONFIG.DB_CONNECTION_POOL_MAX_SIZE));

        public static DatabaseConnectionManager Instance => _instance.Value;

        // 防止直接实例化
        static DatabaseManager() { }
    }
}