using MySql.Data.MySqlClient;
using SocketGameProtocol;
using SocketMultiplayerGameServer.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.DAO
{
    class UserData
    {
        private PlayerData userInfo;
        public PlayerData UserInfo
        {
            get { return userInfo; }
        }
        public UserData()
        {
            
        }

        /// <summary>
        /// 注册
        /// </summary>
        public bool Logon(MainPack pack, string sessionId)
        {
            string username = pack.Logonpack.Username;
            string pwd = pack.Logonpack.Password;

            MySqlConnection connection = null;
            try
            {
                connection = DatabaseManager.Instance.GetConnection();

                // 使用参数化查询防止SQL注入
                string checkUserSql = "SELECT COUNT(*) FROM userdata WHERE username = @username";

                // 检查用户名是否已存在
                using (MySqlCommand checkCmd = new MySqlCommand(checkUserSql, connection))
                {
                    checkCmd.Parameters.AddWithValue("@username", username);

                    // 使用ExecuteScalar而不是DataReader来避免连接占用
                    long userCount = (long)checkCmd.ExecuteScalar();

                    if (userCount > 0)
                    {
                        // 用户名已被注册
                        Console.WriteLine($"用户名 {username} 已被注册");
                        return false;
                    }
                }

                // 插入新用户数据，包含SessionID
                string insertSql = "INSERT INTO userdata (username, password, session_id) VALUES (@username, @password, @session_id)";

                using (MySqlCommand insertCmd = new MySqlCommand(insertSql, connection))
                {
                    insertCmd.Parameters.AddWithValue("@username", username);
                    insertCmd.Parameters.AddWithValue("@password", pwd);
                    insertCmd.Parameters.AddWithValue("@session_id", sessionId);

                    int rowsAffected = insertCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"用户 {username} 注册成功，SessionID: {sessionId}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"用户 {username} 注册失败，未影响任何行");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"注册过程中发生错误: {e.Message}");
                return false;
            }
            finally
            {
                if (connection != null)
                {
                    DatabaseManager.Instance.ReleaseConnection(connection);
                }
            }
        }

        /// <summary>
        /// 登录
        /// </summary>
        public bool Login(MainPack pack, string sessionId)
        {
            string username = pack.Loginpack.Username;
            string pwd = pack.Loginpack.Password;

            MySqlConnection connection = null;
            try
            {
                connection = DatabaseManager.Instance.GetConnection();

                string sql = "SELECT COUNT(*) FROM userdata WHERE username = @username AND password = @password";

                using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", pwd);

                    long count = (long)cmd.ExecuteScalar();
                    if (count > 0)
                    {
                        Console.WriteLine($"用户 {username} 登录成功");

                        // 更新SessionID
                        UpdateSessionId(username, sessionId, connection);

                        userInfo = new PlayerData();
                        userInfo.Username = username;
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"用户 {username} 登录失败");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"登录过程中发生错误: {e.Message}");
                Console.WriteLine($"堆栈跟踪: {e.StackTrace}");
                return false;
            }
            finally
            {
                if (connection != null)
                {
                    DatabaseManager.Instance.ReleaseConnection(connection);
                }
            }
        }

        // 更新SessionID的方法
        private void UpdateSessionId(string username, string sessionId, MySqlConnection mysqlCon)
        {
            // 确保数据库连接是打开的
            if (mysqlCon.State != System.Data.ConnectionState.Open)
            {
                try
                {
                    mysqlCon.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"打开数据库连接失败: {ex.Message}");
                    return;
                }
            }

            string updateSql = "UPDATE userdata SET session_id = @session_id WHERE username = @username";

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(updateSql, mysqlCon))
                {
                    cmd.Parameters.AddWithValue("@session_id", sessionId);
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine($"用户 {username} 的SessionID已更新为: {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新SessionID时发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}