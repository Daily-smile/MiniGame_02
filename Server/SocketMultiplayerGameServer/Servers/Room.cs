using SocketGameProtocol;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SocketMultiplayerGameServer.Servers
{
    class Room
    {
        private RoomPack roomInfo;
        public RoomPack GetRoomInfo
        {
            get
            {
                return roomInfo;
            }
        }

        /// <summary>
        /// 房间内所有的客户端
        /// </summary>
        private List<Client> clientList = new List<Client>();

        /// <summary>
        /// 断开的客户端列表
        /// </summary>
        private List<Client> disconnectedClients = new List<Client>();

        public int ClientCount
        {
            get { return clientList.Count; }
        }

        public Room(Client client, RoomPack pack)
        {
            roomInfo = pack;
            for (int i = 0; i < clientList.Count; i++)
            {
                roomInfo.PlayerList.Add(clientList[i].GetUserData.UserInfo);
            }
            clientList.Add(client);
            client.EnterRoom(this);
        }

        public PlayerData QueryClientPlayerData(string name)
        {
            for (int i = 0; i < clientList.Count; i++)
            {
                if (clientList[i].GetUserData.UserInfo.Username.Equals(name))
                {
                    return clientList[i].GetUserData.UserInfo;
                }
            }
            return null;
        }

        /// <summary>
        /// 玩家断线
        /// </summary>
        public void OnPlayerDisconnect(Client client)
        {
            // 将客户端移动到断开连接列表
            if (clientList.Contains(client))
            {
                clientList.Remove(client);
                disconnectedClients.Add(client);

                Console.WriteLine($"玩家 {client.GetUserData.UserInfo.Username} 断线，房间ID: {roomInfo.RoomID}");

                // 通知其他玩家有玩家断线
                MainPack disconnectPack = new MainPack();
                disconnectPack.Actioncode = ActionCode.PlayerDisconnect;
                disconnectPack.Returncode = ReturnCode.Successed;

                RoomPack roomPack = new RoomPack();
                roomPack.RoomID = roomInfo.RoomID;
                roomPack.PlayerList.Add(client.GetUserData.UserInfo);
                disconnectPack.Roompack.Add(roomPack);

                NoticeClient(disconnectPack, null);
            }
        }

        /// <summary>
        /// 玩家重连
        /// </summary>
        public void OnPlayerReconnect(Client client)
        {
            // 将客户端从断开连接列表移回活跃列表
            if (disconnectedClients.Contains(client))
            {
                disconnectedClients.Remove(client);
                clientList.Add(client);

                Console.WriteLine($"玩家 {client.GetUserData.UserInfo.Username} 重连，房间ID: {roomInfo.RoomID}");

                // 向重连的玩家发送当前房间完整状态
                SendRoomStateToClient(client);

                // 通知其他玩家有玩家重连
                MainPack reconnectPack = new MainPack();
                reconnectPack.Actioncode = ActionCode.PlayerReconnect;
                reconnectPack.Returncode = ReturnCode.Successed;

                RoomPack roomPack = new RoomPack();
                roomPack.RoomID = roomInfo.RoomID;
                roomPack.PlayerList.Add(client.GetUserData.UserInfo);
                reconnectPack.Roompack.Add(roomPack);

                NoticeClient(reconnectPack, client);
            }
        }

        /// <summary>
        /// 向指定客户端发送房间完整状态
        /// </summary>
        private void SendRoomStateToClient(Client client)
        {
            MainPack roomStatePack = new MainPack();
            roomStatePack.Actioncode = ActionCode.RoomStateUpdate;
            roomStatePack.Returncode = ReturnCode.Successed;

            // 创建房间状态的深拷贝
            RoomPack roomPack = new RoomPack
            {
                RoomID = roomInfo.RoomID,
                Roomname = roomInfo.Roomname,
                Maxnum = roomInfo.Maxnum,
                Status = roomInfo.Status,
                RoomMaster = roomInfo.RoomMaster
            };

            // 添加所有玩家信息
            foreach (var player in roomInfo.PlayerList)
            {
                roomPack.PlayerList.Add(player);
            }

            roomStatePack.Roompack.Add(roomPack);

            // 发送房间状态
            client.Send(roomStatePack);

            Console.WriteLine($"向重连玩家 {client.GetUserData.UserInfo.Username} 发送房间状态");
        }

        /// <summary>
        /// 检查房间是否应该销毁（所有玩家都断线）
        /// </summary>
        public bool ShouldDestroyRoom()
        {
            // 如果房间内没有活跃玩家且所有玩家都断线超过一定时间，房间应该销毁
            if (clientList.Count == 0 && disconnectedClients.Count > 0)
            {
                // 检查所有断开连接的客户端是否都超过了重连时间窗口
                bool allExpired = true;
                foreach (Client client in disconnectedClients)
                {
                    // 假设Client类有一个DisconnectTime属性记录断线时间
                    // 这里需要根据实际实现调整
                    if ((DateTime.Now - client.DisconnectTime).TotalSeconds < CONFIG.RECONNECT_WINDOW) // 重连窗口
                    {
                        allExpired = false;
                        break;
                    }
                }

                return allExpired;
            }

            return false;
        }

        /// <summary>
        /// 当房间移除时(房主销毁或退出房间时)
        /// </summary>
        public void DestroyRoom(Client client, int roomID)
        {
            roomInfo = null;
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.NoticeInRoomDestroy;
            mainPack.Returncode = ReturnCode.Successed;
            RoomPack roomPack = new RoomPack();
            roomPack.RoomID = roomID;
            mainPack.Roompack.Add(roomPack);

            // 通知所有客户端（包括断开连接的）
            for (int i = 0; i < clientList.Count; i++)
            {
                clientList[i].ExistRoom();
                if (!clientList[i].Equals(client))
                    clientList[i].Send(mainPack);
            }

            // 也通知断开连接的客户端
            for (int i = 0; i < disconnectedClients.Count; i++)
            {
                disconnectedClients[i].ExistRoom();
                disconnectedClients[i].Send(mainPack);
            }

            clientList.Clear();
            disconnectedClients.Clear();
        }

        public void EnterOneClient(Client client)
        {
            clientList.Add(client);
        }

        public Client RemoveOneClient(PlayerData player)
        {
            for (int i = 0; i < clientList.Count; i++)
            {
                if (clientList[i].GetUserData == null)
                {
                    continue;
                }
                if (clientList[i].GetUserData.UserInfo.Username.Equals(player.Username))
                {
                    Client client = clientList[i];
                    clientList.Remove(client);
                    return client;
                }
            }

            // 也检查断开连接列表
            for (int i = 0; i < disconnectedClients.Count; i++)
            {
                if (disconnectedClients[i].GetUserData == null)
                {
                    continue;
                }
                if (disconnectedClients[i].GetUserData.UserInfo.Username.Equals(player.Username))
                {
                    Client client = disconnectedClients[i];
                    disconnectedClients.Remove(client);
                    return client;
                }
            }

            return null;
        }

        public void RemoveOneClient(Client client)
        {
            if (clientList.Contains(client))
            {
                clientList.Remove(client);
            }

            if (disconnectedClients.Contains(client))
            {
                disconnectedClients.Remove(client);
            }
        }

        public void NoticeClient(MainPack pack, Client ignoreClient)
        {
            for (int i = 0; i < clientList.Count; i++)
            {
                if (clientList[i].Equals(ignoreClient) || clientList[i].GetUserData == null)
                {
                    continue;
                }
                Client client = clientList[i];
                client.Send(pack);
            }
        }
        public void NoticeClientToUDP(MainPack pack, Client ignoreClient)
        {
            for (int i = 0; i < clientList.Count; i++)
            {
                if (clientList[i].Equals(ignoreClient) || clientList[i].GetUserData == null)
                {
                    continue;
                }
                Client client = clientList[i];
                client.SendTo(pack);
            }
        }

        /// <summary>
        /// 获取房间内所有客户端（包括断开连接的）
        /// </summary>
        public List<Client> GetAllClients()
        {
            List<Client> allClients = new List<Client>();
            allClients.AddRange(clientList);
            allClients.AddRange(disconnectedClients);
            return allClients;
        }
    }
}