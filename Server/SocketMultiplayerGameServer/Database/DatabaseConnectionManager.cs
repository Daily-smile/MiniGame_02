using MySql.Data.MySqlClient;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Database
{
    public class DatabaseConnectionManager : IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentBag<MySqlConnection> _connectionPool;
        private readonly int _maxPoolSize;
        private int _currentPoolSize;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public DatabaseConnectionManager(string connectionString, int initialPoolSize = 5, int maxPoolSize = 20)
        {
            _connectionString = connectionString;
            _maxPoolSize = maxPoolSize;
            _connectionPool = new ConcurrentBag<MySqlConnection>();
            _currentPoolSize = 0;

            // 初始化连接池
            InitializePool(initialPoolSize);
        }

        private void InitializePool(int initialSize)
        {
            for (int i = 0; i < initialSize; i++)
            {
                var connection = CreateNewConnection();
                if (connection != null)
                {
                    _connectionPool.Add(connection);
                    _currentPoolSize++;
                }
            }
        }

        private MySqlConnection CreateNewConnection()
        {
            try
            {
                var connection = new MySqlConnection(_connectionString);
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                Logger.Error("创建数据库连接失败", ex);
                return null;
            }
        }

        public MySqlConnection GetConnection()
        {
            if (_disposed)
                throw new ObjectDisposedException("DatabaseConnectionManager");

            MySqlConnection connection;

            // 尝试从池中获取连接
            if (_connectionPool.TryTake(out connection))
            {
                // 检查连接是否有效
                if (connection.State == ConnectionState.Open)
                {
                    return connection;
                }
                else
                {
                    // 连接已关闭，创建新连接替换
                    _currentPoolSize--;
                    connection.Dispose();
                    return CreateNewConnection();
                }
            }

            // 池中没有可用连接，创建新连接
            lock (_lock)
            {
                if (_currentPoolSize < _maxPoolSize)
                {
                    connection = CreateNewConnection();
                    if (connection != null)
                    {
                        _currentPoolSize++;
                    }
                    return connection;
                }
            }

            // 如果连接池已满，等待一段时间再尝试
            // 在实际应用中，您可能需要更复杂的等待策略
            System.Threading.Thread.Sleep(50);
            return GetConnection();
        }

        public void ReleaseConnection(MySqlConnection connection)
        {
            if (_disposed)
            {
                connection.Dispose();
                return;
            }

            if (connection == null)
                return;

            // 如果连接已关闭或池已满，直接销毁连接
            if (connection.State != ConnectionState.Open || _connectionPool.Count >= _maxPoolSize)
            {
                connection.Dispose();
                _currentPoolSize--;
                return;
            }

            // 将连接返回池中
            _connectionPool.Add(connection);
        }

        public async Task<MySqlConnection> GetConnectionAsync()
        {
            return await Task.Run(() => GetConnection());
        }

        public async Task ReleaseConnectionAsync(MySqlConnection connection)
        {
            await Task.Run(() => ReleaseConnection(connection));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // 使用循环清空 ConcurrentBag 而不是调用 Clear() 方法
            while (_connectionPool.TryTake(out MySqlConnection connection))
            {
                try
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                    }
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("关闭数据库连接时发生错误", ex);
                }
            }

            // 重置当前连接数
            _currentPoolSize = 0;
        }

        // 获取连接池状态
        public (int TotalConnections, int AvailableConnections) GetPoolStatus()
        {
            return (_currentPoolSize, _connectionPool.Count);
        }
    }
}