using MySql.Data.MySqlClient;
using SocketGameProtocol;
using SocketMultiplayerGameServer.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Controller
{
    class UserController : BaseController
    {
        public UserController() 
        {
            requestCode = RequestCode.User;
        }

        /// <summary>
        /// 注册
        /// </summary>
        public MainPack Logon(Server server, Client client, MainPack pack)
        {
            if (client.Logon(pack))
            {
                pack.Returncode = ReturnCode.Successed;
            }
            else
            {
                pack.Returncode = ReturnCode.Fail;
            }
            return pack;
        }

        /// <summary>
        /// 登录
        /// </summary>
        public MainPack Login(Server server, Client client, MainPack pack)
        {
            MainPack response = new MainPack();
            response.Actioncode = ActionCode.Login;

            try
            {
                if (client.Login(pack))
                {
                    // 将SessionId保存到数据库
                    SaveSessionToDatabase(client.GetUserData.UserInfo.Username, client.SessionId);
                    response.Returncode = ReturnCode.Successed;
                    response.SessionId = client.SessionId;
                    response.Loginpack = new LoginPack();
                    response.Loginpack.Username = client.GetUserData.UserInfo.Username;
                }
                else
                {
                    response.Returncode = ReturnCode.Fail;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录处理错误: {ex.Message}");
                response.Returncode = ReturnCode.Fail;
            }

            return response;
        }

        // 保存SessionId到数据库
        private void SaveSessionToDatabase(string username, string sessionId)
        {
            // 这里需要实现将SessionId保存到数据库的逻辑
            // 例如：UPDATE users SET session_id = @sessionId WHERE username = @username
            using (MySqlConnection connection = new MySqlConnection(CONFIG.GetConnectionString()))
            {
                connection.Open();
                string query = "UPDATE userdata SET session_id = @sessionId WHERE username = @username";
                MySqlCommand command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@sessionId", sessionId);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 处理重连的方法
        /// </summary>
        public MainPack Reconnect(Server server, Client client, MainPack pack)
        {
            return server.HandleReconnect(pack, client);
        }

        /// <summary>
        /// 断线返回登录
        /// </summary>
        public MainPack ReturnLogin(Server server, Client client, MainPack pack)
        {
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.ReturnLogin;
            try
            {
                client.HandleDisconnect();
                server.HandleReturnLogin(client, pack.Loginpack.Username);
                mainPack.Returncode = ReturnCode.Successed;
            }
            catch (Exception)
            {
                mainPack.Returncode = ReturnCode.Fail;
            }
            return mainPack;
        }
    }
}
