using MySql.Data.MySqlClient;
using SocketGameProtocol;
using SocketMultiplayerGameServer.DAO;
using SocketMultiplayerGameServer.Tool;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Servers
{
    class Client : IDisposable
    {
        //private MySqlConnection mysqlCon;
        private Socket socket;
        public Socket Socket {  get { return socket; } }
        private Message message;
        private UserData userData;
        private Server server;
        private UDPServer serverUDP;
        private Room room;
        public Room Room { get { return room; } }
        private bool isDisposed = false;
        private object receiveLock = new object();

        public UserData GetUserData
        {
            get { return userData; }
        }

        private EndPoint remoteEp;
        public EndPoint IEP
        {
            get
            {
                return remoteEp;
            }
            set
            {
                remoteEp = value;
            }
        }

        public Action<Client> onDisposeEvent;
        // 断线重连
        private DateTime lastHeartbeatTime = DateTime.Now;
        public bool isConnected = true;
        public string SessionId { get; private set; }
        public DateTime DisconnectTime { get; private set; }

        public Client(Socket socket, Server server, UDPServer udpServer)
        {
            this.server = server;
            this.serverUDP = udpServer;
            this.socket = socket;
            this.message = new Message();
            this.userData = new UserData();
            onDisposeEvent = (thisCall) => { };

            // 生成唯一的SessionId
            this.SessionId = GenerateSessionId();

            // 移除ConnectMysql调用
            // ConnectMysql();

            StartReceive();
            Console.WriteLine("客户端连接成功！");
        }

        //private void ConnectMysql()
        //{
        //    try
        //    {
        //        mysqlCon = new MySqlConnection(CONFIG.GetConnectionString());
        //        //Console.WriteLine($"尝试连接数据库: {CONFIG.GetConnectionString()}");
        //        mysqlCon.Open();
        //        StartReceive();
        //        Console.WriteLine("连接成功！");
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine("连接失败！" + e.Message);
        //        //Console.WriteLine($"连接字符串: {CONFIG.GetConnectionString()}");
        //        Console.WriteLine($"异常类型: {e.GetType()}");
        //        Console.WriteLine($"堆栈跟踪: {e.StackTrace}");
        //        throw;
        //    }
        //}

        // 生成唯一SessionId的方法
        private string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        // 添加一个方法用于设置SessionId（例如从数据库加载时）
        public void SetSessionId(string sessionId)
        {
            this.SessionId = sessionId;
        }

        private void StartReceive()
        {
            if (socket == null || !socket.Connected || isDisposed)
                return;

            try
            {
                // 使用异步接收，但确保在正确的上下文中
                socket.BeginReceive(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ReceiveCallback, null);
            }
            catch (ObjectDisposedException)
            {
                // Socket 已被释放，正常退出
                Dispose();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"开始接收数据时发生 Socket 错误: {ex.SocketErrorCode} - {ex.Message}");
                Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"开始接收数据时发生错误: {ex.Message}");
                Dispose();
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            // 使用锁确保线程安全
            lock (receiveLock)
            {
                if (isDisposed || socket == null || !socket.Connected)
                    return;

                try
                {
                    int bytesRead = socket.EndReceive(result);
                    if (bytesRead == 0)
                    {
                        // 连接已正常关闭
                        Console.WriteLine("客户端连接已关闭");
                        Dispose();
                        return;
                    }

                    // 处理接收到的数据
                    //message.ReadBuffer(bytesRead, HandleRequest, receiveBuffer);
                    message.ReadBuffer(bytesRead, HandleRequest);

                    // 继续接收数据
                    StartReceive();
                }
                catch (ObjectDisposedException)
                {
                    // Socket 已被释放，正常退出
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"接收数据时发生 Socket 错误: {ex.SocketErrorCode} - {ex.Message}");
                    Dispose();
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"接收数据时发生无效操作错误: {ex.Message}");
                    Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"接收数据时发生未知错误: {ex.Message}");
                    Dispose();
                }
            }
        }

        void HandleRequest(MainPack pack)
        {
            try
            {
                // 首先检查是否是心跳包
                if (pack.Actioncode == ActionCode.Heartbeat)
                {
                    // 更新心跳时间
                    UpdateHeartbeat();

                    // 可以选择发送一个简单的心跳响应
                    MainPack response = new MainPack();
                    response.Actioncode = ActionCode.Heartbeat;
                    response.Returncode = ReturnCode.Successed;
                    Send(response);

                    //Console.WriteLine($"收到来自 {GetUserData?.UserInfo?.Username} 的心跳包");
                    return;
                }

                // 如果不是心跳包，交给服务器处理
                server.HandleRequest(pack, this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理请求时发生错误: {ex.Message}");
            }
        }

        private async void HandleRequestAsync(MainPack pack)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (pack.Actioncode == ActionCode.Heartbeat)
                    {
                        UpdateHeartbeat();
                        MainPack response = new MainPack();
                        response.Actioncode = ActionCode.Heartbeat;
                        response.Returncode = ReturnCode.Successed;
                        Send(response);
                        return;
                    }

                    server.HandleRequest(pack, this);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理请求时发生错误: {ex.Message}");
                }
            });
        }

        #region 断线重连
        // 更新心跳时间
        public void UpdateHeartbeat()
        {
            lastHeartbeatTime = DateTime.Now;
            isConnected = true;
        }

        // 检查心跳是否超时
        public bool CheckHeartbeatTimeout()
        {
            return (DateTime.Now - lastHeartbeatTime).TotalSeconds > CONFIG.HEARTBEAT_TIMEOUT;
        }

        // 处理断开连接
        public void HandleDisconnect()
        {
            isConnected = false;
            DisconnectTime = DateTime.Now;

            // 通知房间玩家断线
            if (room != null)
            {
                room.OnPlayerDisconnect(this);
            }
        }

        // 处理重连
        public bool HandleReconnect(Socket newSocket, string sessionId)
        {
            // 验证会话ID
            if (SessionId != sessionId || (DateTime.Now - DisconnectTime).TotalSeconds > CONFIG.RECONNECT_WINDOW)
            {
                return false;
            }

            // 替换Socket
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            socket = newSocket;
            isConnected = true;
            UpdateHeartbeat();

            // 重新开始接收数据
            StartReceive();

            // 通知房间玩家重连成功
            if (room != null)
            {
                room.OnPlayerReconnect(this);
            }

            return true;
        }
        #endregion

        public void Dispose()
        {
            if (isDisposed) return;

            CheckAndDispose();
            // 立即释放资源，不等待重连窗口
            //ActuallyDispose();
        }

        // 或者保持重连逻辑但添加超时检查
        private void CheckAndDispose()
        {
            if (!isConnected && (DateTime.Now - DisconnectTime).TotalSeconds > CONFIG.RECONNECT_WINDOW)
            {
                ActuallyDispose();
            }
        }

        // 实际清理资源的方法
        private void ActuallyDispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            onDisposeEvent?.Invoke(this);
            onDisposeEvent = null;
            if (room != null)
            {
                room.RemoveOneClient(this);
            }
            server.RemoveOneClient(this);

            try
            {
                /*if (mysqlCon != null)
                {
                    if (mysqlCon.State == System.Data.ConnectionState.Open)
                    {
                        mysqlCon.Close();
                    }
                    mysqlCon.Dispose();
                    mysqlCon = null;
                }*/
                if (socket != null)
                {
                    if (socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    socket.Close();
                    socket.Dispose();
                    socket = null;
                }

                // 释放其他资源
                message = null;
                userData = null;
                room = null;

                Console.WriteLine("客户端资源已释放");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"释放资源时发生错误: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }


        // 析构函数，确保资源被释放
        ~Client()
        {
            Dispose();
        }

        public void EnterRoom(Room newRoom)
        {
            this.room = newRoom;
        }
        public void ExistRoom()
        {
            this.room = null;
        }

        #region 客户端相关功能逻辑代码
        public void Send(MainPack pack)
        {
            if (!isConnected || isDisposed || socket == null || !socket.Connected)
                return;

            try
            {
                byte[] data = Message.PackData(pack);
                socket.Send(data);
            }
            catch (ObjectDisposedException)
            {
                // Socket 已被释放，正常退出
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"发送数据时发生 Socket 错误: {ex.SocketErrorCode} - {ex.Message}");
                Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送数据时发生错误: {ex.Message}");
                Dispose();
            }
        }

        // 异步发送方法
        public async Task SendAsync(MainPack pack)
        {
            if (isDisposed || socket == null || !socket.Connected)
                return;

            try
            {
                byte[] data = Message.PackData(pack);
                await Task.Factory.FromAsync(
                    (callback, state) => socket.BeginSend(data, 0, data.Length, SocketFlags.None, callback, state),
                    socket.EndSend,
                    null);
            }
            catch (ObjectDisposedException)
            {
                // Socket 已被释放，正常退出
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"异步发送数据时发生 Socket 错误: {ex.SocketErrorCode} - {ex.Message}");
                Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异步发送数据时发生错误: {ex.Message}");
                Dispose();
            }
        }

        public void SendTo(MainPack pack)
        {
            if (IEP == null) return;
            serverUDP.SendTo(pack, IEP);
        }

        /// <summary>
        /// 客户端注册
        /// </summary>
        public bool Logon(MainPack pack)
        {
            try
            {
                //return GetUserData.Logon(pack, mysqlCon, SessionId);
                return GetUserData.Logon(pack, SessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"用户注册时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 客户端登录
        /// </summary>
        public bool Login(MainPack pack)
        {
            try
            {
                /*// 确保数据库连接是打开的
                if (mysqlCon.State != System.Data.ConnectionState.Open)
                {
                    try
                    {
                        mysqlCon.Open();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"打开数据库连接失败: {ex.Message}");
                        return false;
                    }
                }*/
                //return GetUserData.Login(pack, mysqlCon, SessionId);
                return GetUserData.Login(pack, SessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"用户登录时发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }
        #endregion
    }
}