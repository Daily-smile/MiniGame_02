using MySql.Data.MySqlClient;
using SocketGameProtocol;
using SocketMultiplayerGameServer.Controller;
using SocketMultiplayerGameServer.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Servers
{
    class Server : IDisposable
    {
        private Socket socket;
        private UDPServer us;

        private readonly object clientListLock = new object();
        private readonly object roomListLock = new object();
        private List<Client> clientList = new List<Client>();
        private List<Room> roomList = new List<Room>();

        private System.Timers.Timer heartbeatTimer;
        private Dictionary<string, Client> disconnectedClients = new Dictionary<string, Client>();
        private System.Timers.Timer roomCheckTimer;

        private ControllerManager controllerManager;
        private bool _disposed = false;

        public Server(int port) 
        {
            controllerManager = new ControllerManager(this);

            // 初始化心跳计时器
            heartbeatTimer = new System.Timers.Timer(CONFIG.HEARTBEAT_TIME); 
            heartbeatTimer.Elapsed += CheckHeartbeats;
            heartbeatTimer.Start();
            // 初始化房间检查计时器
            roomCheckTimer = new System.Timers.Timer(CONFIG.ROOMCHECK_TIME); 
            roomCheckTimer.Elapsed += CheckRooms;
            roomCheckTimer.Start();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.Listen(0);
            StartAccept();
            Console.WriteLine("TCP服务已启动...");
            us = new UDPServer(6667, this, controllerManager);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // 添加受保护的虚方法以便子类可以重写
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 释放托管资源
                if (heartbeatTimer != null)
                {
                    heartbeatTimer.Stop();
                    heartbeatTimer.Dispose();
                    heartbeatTimer = null;
                }

                if (roomCheckTimer != null)
                {
                    roomCheckTimer.Stop();
                    roomCheckTimer.Dispose();
                    roomCheckTimer = null;
                }

                if (socket != null)
                {
                    socket.Close();
                    socket.Dispose();
                    socket = null;
                }

                if (us != null)
                {
                    // 假设 UDPServer 也实现了 IDisposable
                    us.Dispose();
                    us = null;
                }

                // 清理客户端列表
                lock (clientListLock)
                {
                    foreach (var client in clientList)
                    {
                        client.Dispose();
                    }
                    clientList.Clear();

                    foreach (var client in disconnectedClients.Values)
                    {
                        client.Dispose();
                    }
                    disconnectedClients.Clear();
                }

                // 清理房间列表
                lock (roomList)
                {
                    foreach (var room in roomList)
                    {
                        // 如果 Room 类实现了 IDisposable，则调用 Dispose
                        // 否则，调用适当的清理方法
                        // room.Dispose();
                    }
                    roomList.Clear();
                }
            }

            // 释放非托管资源（如果有）

            _disposed = true;
        }

        // 添加析构函数作为最后的安全网
        ~Server()
        {
            Dispose(false);
        }

        /// <summary>
        /// 开始接收应答
        /// </summary>
        void StartAccept()
        {
            socket.BeginAccept(AcceptCallback, null);
        }

        void AcceptCallback(IAsyncResult result)
        {
            lock (clientListLock)
            {
                Console.WriteLine("检测到一个客户端服务器连接请求...");
                Socket client = socket.EndAccept(result);
                clientList.Add(new Client(client, this, us));
                StartAccept();
            }
        }

        // 检查所有客户端的心跳
        private void CheckHeartbeats(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (clientListLock)
            {
                List<Client> clientsToRemove = new List<Client>();

                foreach (Client client in clientList.ToList())
                {
                    if (client.GetUserData == null || client.GetUserData.UserInfo == null)
                    {
                        continue;
                    }
                    if (client.CheckHeartbeatTimeout())
                    {
                        client.HandleDisconnect();
                        // 移动到断开连接列表
                        if (!string.IsNullOrEmpty(client.SessionId))
                        {
                            disconnectedClients[client.SessionId] = client;
                            clientsToRemove.Add(client);
                            Console.WriteLine($"客户端心跳超时: {client.GetUserData?.UserInfo?.Username}");
                        }
                    }
                }

                // 从活跃客户端列表中移除超时的客户端
                foreach (Client client in clientsToRemove)
                {
                    clientList.Remove(client);
                }
            }
        }

        // 检查所有房间的状态
        private void CheckRooms(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (roomListLock)
            {
                for (int i = roomList.Count - 1; i >= 0; i--)
                {
                    Room room = roomList[i];
                    if (room.ShouldDestroyRoom())
                    {
                        Console.WriteLine($"房间 {room.GetRoomInfo.RoomID} 因所有玩家断线超时而被销毁");

                        // 创建销毁房间的包
                        MainPack destroyPack = new MainPack();
                        destroyPack.Actioncode = ActionCode.NoticeInRoomDestroy;
                        destroyPack.Returncode = ReturnCode.Successed;

                        RoomPack roomPack = new RoomPack();
                        roomPack.RoomID = room.GetRoomInfo.RoomID;
                        destroyPack.Roompack.Add(roomPack);

                        // 通知所有断开连接的客户端
                        foreach (Client client in room.GetAllClients())
                        {
                            client.ExistRoom();
                            client.Send(destroyPack);
                        }

                        // 从房间列表中移除
                        roomList.RemoveAt(i);
                    }
                }
            }
        }

        public Client ClientFormSessionId(string sessionId)
        {
            lock (clientListLock)
            {
                for (int i = 0; i < clientList.Count; i++)
                {
                    if (clientList[i].SessionId.Equals(sessionId))
                    {
                        return clientList[i];
                    }
                }
                return null;
            }
        }

        public bool SetIEP(EndPoint iPEnd, string sessionId)
        {
            lock (clientListLock)
            {
                for (int i = 0; i < clientList.Count; i++)
                {
                    if (clientList[i].SessionId.Equals(sessionId))
                    {
                        clientList[i].IEP = iPEnd;
                        return true;
                    }
                }
                return false;
            }
        }

        public void HandleReturnLogin(Client client, string userName)
        {
            lock (clientListLock)
            {
                clientList.Remove(client);
            }
            if (disconnectedClients.Count == 0) return;
            if (string.IsNullOrEmpty(userName))
            {
                Console.WriteLine("重连客户端数据错误！！");
                return;
            }
            string sessionId = GetSessionFromDatabase(userName);
            if (disconnectedClients.TryGetValue(sessionId, out _))
            {
                disconnectedClients.Remove(sessionId);
            }
        }

        // 处理重连请求
        public MainPack HandleReconnect(MainPack pack, Client newClient)
        {
            MainPack response = new MainPack();
            response.Actioncode = ActionCode.Reconnect;

            string username = pack.Loginpack.Username;
            string sessionId = pack.SessionId;

            // 从数据库验证SessionID
            string storedSessionId = GetSessionFromDatabase(username);

            if (storedSessionId == sessionId && disconnectedClients.TryGetValue(sessionId, out Client oldClient))
            {
                if (oldClient.HandleReconnect(newClient.Socket, sessionId))
                {
                    // 重连成功
                    response.Returncode = ReturnCode.Successed;

                    // 更新客户端列表
                    lock (clientListLock)
                    {
                        clientList.Remove(newClient);
                        clientList.Add(oldClient);
                    }

                    // 从断开连接列表中移除
                    disconnectedClients.Remove(sessionId);

                    // 返回当前房间状态
                    if (oldClient.Room != null)
                    {
                        response.Roompack.Add(oldClient.Room.GetRoomInfo);
                    }
                }
                else
                {
                    response.Returncode = ReturnCode.Fail;
                }
            }
            else
            {
                response.Returncode = ReturnCode.NotFindRoom;
            }

            return response;
        }

        // 从数据库获取SessionID
        private string GetSessionFromDatabase(string username)
        {
            MySqlConnection connection = null;
            try
            {
                connection = DatabaseManager.Instance.GetConnection();

                string query = "SELECT session_id FROM userdata WHERE username = @username";
                MySqlCommand command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);

                object result = command.ExecuteScalar();
                return result != null ? result.ToString() : null;
            }
            catch (Exception ex)
            {
                Logger.Error($"获取用户SessionID失败: {username}", ex);
                return null;
            }
            finally
            {
                if (connection != null)
                {
                    DatabaseManager.Instance.ReleaseConnection(connection);
                }
            }
        }

        public Room QueryRoom(int id)
        {
            lock (roomListLock)
            {
                for (int i = 0; i < roomList.Count; i++)
                {
                    if (roomList[i].GetRoomInfo.RoomID == id)
                    {
                        return roomList[i];
                    }
                }
            }
            return null;
        }
        public Room QueryRoom(string name)
        {
            lock (roomListLock)
            {
                for (int i = 0; i < roomList.Count; i++)
                {
                    if (roomList[i].GetRoomInfo.Roomname.Equals(name))
                    {
                        return roomList[i];
                    }
                }
            }
            return null;
        }

        public void HandleRequest(MainPack pack, Client client)
        {
            controllerManager.HandleRequest(pack, client);
        }
        public void RemoveOneClient(Client client)
        {
            lock (clientListLock)
            {
                clientList.Remove(client);

                // 如果客户端有会话ID，添加到断开连接列表
                if (!string.IsNullOrEmpty(client.SessionId) && client.isConnected)
                {
                    disconnectedClients[client.SessionId] = client;
                    client.HandleDisconnect();
                }
            }
        }

        public MainPack CreateRoom(Client client, MainPack pack)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.CreateRoom;
            try
            {
                Room room = QueryRoom(pack.Queryroom.RoomName);
                if (room != null)
                {
                    newPack.Returncode = ReturnCode.ExistSomeRoom;
                }
                else
                {
                    RoomPack roomPack = new RoomPack();
                    roomPack.PlayerList.Add(client.GetUserData.UserInfo);
                    roomPack.RoomMaster = client.GetUserData.UserInfo;
                    roomPack.Maxnum = pack.Queryroom.RoomMaxNum;
                    roomPack.Roomname = pack.Queryroom.RoomName;
                    roomPack.Status = 0;
                    room = new Room(client, roomPack);
                    roomPack.RoomID = room.GetHashCode();
                    newPack.Roompack.Add(roomPack);
                    newPack.Returncode = ReturnCode.Successed;
                    lock (roomListLock)
                    {
                        roomList.Add(room);
                    }
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }

        public MainPack FindRoom(QueryRoomPack queryRoom)
        {
            MainPack pack = new MainPack();
            pack.Actioncode = ActionCode.FindRoom;
            try
            {
                bool isExistQuery = !string.IsNullOrEmpty(queryRoom.RoomName) || queryRoom.RoomMaxNum != 0 || queryRoom.RoomStatus != 0;
                lock (roomListLock)
                {
                    foreach (Room room in roomList)
                    {
                        if (room.GetRoomInfo.Status != 3)
                            room.GetRoomInfo.Status = room.GetRoomInfo.PlayerList.Count < room.GetRoomInfo.Maxnum ? 1 : 2;
                        if (isExistQuery)
                        {
                            if (queryRoom.RoomName == room.GetRoomInfo.Roomname)
                            {
                                pack.Roompack.Add(room.GetRoomInfo);
                                break;
                            }
                            else if (queryRoom.RoomMaxNum == room.GetRoomInfo.Maxnum || (queryRoom.RoomStatus != 0 && queryRoom.RoomStatus == room.GetRoomInfo.Status))
                            {
                                pack.Roompack.Add(room.GetRoomInfo);
                            }
                        }
                        else
                        {
                            pack.Roompack.Add(room.GetRoomInfo);
                        }
                    }
                }
                pack.Returncode = pack.Roompack.Count > 0 ? ReturnCode.Successed : ReturnCode.NotFindRoom;
            }
            catch (Exception)
            {
                pack.Returncode = ReturnCode.Fail;
            }
            return pack;
        }

        public MainPack JoinRoom(int roomID, Client client)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.JoinRoom;
            try
            {
                Room room = QueryRoom(roomID);
                if (room != null)
                {
                    if (room.GetRoomInfo.Status == 3)
                    {
                        newPack.Returncode = ReturnCode.RoomIsGame;
                        return newPack;
                    }
                    else if (room.GetRoomInfo.PlayerList.Count >= room.GetRoomInfo.Maxnum)
                    {
                        room.GetRoomInfo.Status = 2;
                        newPack.Returncode = ReturnCode.RoomIsFull;
                        return newPack;
                    }
                    room.GetRoomInfo.Status = 1;
                    room.EnterOneClient(client);
                    client.EnterRoom(room);
                    room.GetRoomInfo.PlayerList.Add(client.GetUserData.UserInfo);
                    newPack.Roompack.Add(room.GetRoomInfo);
                    newPack.Returncode = ReturnCode.Successed;
                    MainPack otherClientPack = newPack.Clone();
                    otherClientPack.Actioncode = ActionCode.RoomAddPlayer;
                    room.NoticeClient(otherClientPack, client);
                }
                else
                {
                    newPack.Returncode = ReturnCode.NotFindRoom;
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }

        public MainPack QuitRoom(int roomID, bool isNotReturn, Client client)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.QuitRoom;
            newPack.IsNotReturn = isNotReturn;
            try
            {
                Room room = QueryRoom(roomID);
                if (room != null)
                {
                    if (room.GetRoomInfo.RoomMaster.Equals(client.GetUserData.UserInfo))
                    {// 是房主本人退出房间
                        room.DestroyRoom(client, roomID);
                        lock (roomListLock)
                        {
                            roomList.Remove(room);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < room.GetRoomInfo.PlayerList.Count; i++)
                        {
                            if (room.GetRoomInfo.PlayerList[i].Equals(client.GetUserData.UserInfo))
                            {
                                room.GetRoomInfo.PlayerList.Remove(room.GetRoomInfo.PlayerList[i]);
                                break;
                            }
                        }
                        room.RemoveOneClient(client.GetUserData.UserInfo);
                        client.ExistRoom();
                        room.GetRoomInfo.Status = room.GetRoomInfo.PlayerList.Count < room.GetRoomInfo.Maxnum ? 1 : 2;
                        MainPack quitPack = new MainPack();
                        quitPack.Actioncode = ActionCode.RoomRemovePlayer;
                        quitPack.Returncode = ReturnCode.Successed;
                        quitPack.Roompack.Add(room.GetRoomInfo);
                        room.NoticeClient(quitPack, client);
                    }
                    newPack.Returncode = ReturnCode.Successed;
                }
                else
                {
                    newPack.Returncode = ReturnCode.NotFindRoom;
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }

        public MainPack DestroyRoom(int roomID, Client client)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.DestroyRoom;
            try
            {
                Room room = QueryRoom(roomID);
                if (room != null)
                {
                    room.DestroyRoom(client, roomID);
                    lock (roomListLock)
                    {
                        roomList.Remove(room);
                    }
                    newPack.Returncode = ReturnCode.Successed;
                }
                else
                {
                    newPack.Returncode = ReturnCode.NotRoom;
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }

        public MainPack SwitchRoomMaster(RoomPack roomPack, Client client)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.SwitchRoomMaster;
            try
            {
                Room room = QueryRoom(roomPack.RoomID);
                if (room != null)
                {
                    room.GetRoomInfo.Status = room.GetRoomInfo.PlayerList.Count < room.GetRoomInfo.Maxnum ? 1 : 2;
                    room.GetRoomInfo.RoomMaster = roomPack.RoomMaster;
                    RoomPack newRoomPack = new RoomPack();
                    newRoomPack.RoomID = roomPack.RoomID;
                    newRoomPack.RoomMaster = roomPack.RoomMaster;
                    newPack.Roompack.Add(newRoomPack);
                    newPack.Returncode = ReturnCode.Successed;
                    room.NoticeClient(newPack, client);
                }
                else
                {
                    newPack.Returncode = ReturnCode.NotFindRoom;
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }
        public MainPack KickoutRoom(RoomPack roomPack, Client client)
        {
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.RoomRemovePlayer;
            try
            {
                Room room = QueryRoom(roomPack.RoomID);
                if (room != null)
                {
                    PlayerData kickoutPlayer = roomPack.PlayerList[0];
                    for (int i = 0; i < room.GetRoomInfo.PlayerList.Count; i++)
                    {
                        if (room.GetRoomInfo.PlayerList[i].Username.Equals(kickoutPlayer.Username))
                        {
                            room.GetRoomInfo.PlayerList.RemoveAt(i);
                            break;
                        }
                    }
                    Client removeClient = room.RemoveOneClient(roomPack.PlayerList[0]);
                    removeClient.ExistRoom();
                    // 首先通知这位被踢出的玩家
                    MainPack romovePlayerPack = new MainPack();
                    RoomPack newRoomPack = new RoomPack();
                    newRoomPack.RoomID = roomPack.RoomID;
                    newRoomPack.PlayerList.Add(kickoutPlayer);
                    newRoomPack.RoomMaster = client.GetUserData.UserInfo;
                    romovePlayerPack.Roompack.Add(newRoomPack);
                    romovePlayerPack.Actioncode = ActionCode.KickoutRoom;
                    romovePlayerPack.Returncode = ReturnCode.Successed;
                    removeClient.Send(romovePlayerPack);
                    room.GetRoomInfo.Status = room.GetRoomInfo.PlayerList.Count < room.GetRoomInfo.Maxnum ? 1 : 2;
                    // 其次通知在这个房间里的其它玩家，有玩家被踢出
                    mainPack.Returncode = ReturnCode.Successed;
                    mainPack.Roompack.Add(room.GetRoomInfo);
                    if (room.ClientCount == 1)
                    {// 说明只剩下房主一人了

                        return mainPack;
                    }
                    else
                    {// 最后通知除房主外其他人
                        room.NoticeClient(mainPack, client);
                    }
                }
                else
                {
                    mainPack.Returncode = ReturnCode.NotFindRoom;
                }
            }
            catch (Exception)
            {
                mainPack.Returncode = ReturnCode.Fail;
            }
            return mainPack;
        }

        public MainPack HandleChatRoomMsg(Client client, MainPack mainPack)
        {
            MainPack newPack = new MainPack();
            newPack.Actioncode = ActionCode.RoomMessageUpdate;
            try
            {
                Room room = QueryRoom(mainPack.ChatRoom.RoomID);
                if (room != null)
                {
                    newPack.ChatRoom = new ChatRoomPack();
                    newPack.ChatRoom.RoomID = room.GetRoomInfo.RoomID;
                    newPack.ChatRoom.Player = client.GetUserData.UserInfo;
                    newPack.ChatRoom.ChatContent = mainPack.ChatRoom.ChatContent;
                    newPack.Returncode = ReturnCode.Successed;
                    room.NoticeClient(newPack, client);
                }
                else
                {
                    newPack.Returncode = ReturnCode.Fail;
                }
            }
            catch (Exception)
            {
                newPack.Returncode = ReturnCode.Fail;
            }
            return newPack;
        }
    }
}
