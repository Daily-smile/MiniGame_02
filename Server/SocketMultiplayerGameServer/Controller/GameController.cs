using Google.Protobuf.Collections;
using SocketGameProtocol;
using SocketMultiplayerGameServer.Servers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Controller
{
    class GameController : BaseController
    {
        private object _lockMatchClient = new object();
        private Dictionary<string, CountdownTimer<Client>> timers = new Dictionary<string, CountdownTimer<Client>>();
        private Dictionary<string, Client> matchClients = new Dictionary<string, Client>();
        private ConcurrentDictionary<string, Client> matchOKClients = new ConcurrentDictionary<string, Client>();
        private readonly object _lockEnterGameWaiting = new object();
        private Dictionary<string, bool> isEnterGameWaiting = new Dictionary<string, bool>();

        public GameController()
        {
            requestCode = RequestCode.Game;
        }

        private void RunMatchGameDoubleModel(Client selfClient)
        {
            if (matchClients.Count <= 1)
            {
                return;
            }
            Client toClient = null;
            foreach (Client item in matchClients.Values)
            {
                if (!item.Equals(selfClient))
                {
                    toClient = item;
                    break;
                }
            }
            if (toClient == null)
            {
                return;
            }
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.StartGame;
            mainPack.Returncode = ReturnCode.EndMatchGame;
            mainPack.Gamepack = new GamePack();
            mainPack.Gamepack.GameModel = GameModel.Double;
            mainPack.Gamepack.Player = toClient.GetUserData.UserInfo;
            selfClient.Send(mainPack);
            mainPack.Gamepack.Player = selfClient.GetUserData.UserInfo;
            toClient.Send(mainPack);
            lock (_lockMatchClient)
            {
                timers[selfClient.GetUserData.UserInfo.Username].Stop();
                timers.Remove(selfClient.GetUserData.UserInfo.Username);
                timers.Remove(toClient.GetUserData.UserInfo.Username);
                if (matchClients.ContainsKey(selfClient.GetUserData.UserInfo.Username))
                {
                    matchClients.Remove(selfClient.GetUserData.UserInfo.Username);
                }
                if (matchClients.ContainsKey(toClient.GetUserData.UserInfo.Username))
                {
                    matchClients.Remove(toClient.GetUserData.UserInfo.Username);
                }
                matchOKClients.GetOrAdd(selfClient.GetUserData.UserInfo.Username, toClient);
                matchOKClients.GetOrAdd(toClient.GetUserData.UserInfo.Username, selfClient);
            }
        }
        private void EndMatchGameDoubleModel(Client selfClient)
        {
            if (matchClients.Count <= 1)
            {
                if (timers.ContainsKey(selfClient.GetUserData.UserInfo.Username))
                {
                    lock (_lockMatchClient)
                    {
                        MainPack newMainPack = new MainPack();
                        newMainPack.Actioncode = ActionCode.StartGame;
                        //newMainPack.Returncode = ReturnCode.EndMatchGame;
                        newMainPack.Returncode = ReturnCode.Fail;
                        newMainPack.Gamepack = new GamePack();
                        newMainPack.Gamepack.GameModel = GameModel.Double;
                        newMainPack.Gamepack.Player = selfClient.GetUserData.UserInfo;
                        selfClient.Send(newMainPack);
                        timers.Remove(selfClient.GetUserData.UserInfo.Username);
                        if (matchClients.ContainsKey(selfClient.GetUserData.UserInfo.Username))
                        {
                            matchClients.Remove(selfClient.GetUserData.UserInfo.Username);
                        }
                    }
                }
                return;
            }
            Client toClient = null;
            foreach (Client item in matchClients.Values)
            {
                if (!item.Equals(selfClient))
                {
                    toClient = item;
                    break;
                }
            }
            if (toClient == null)
            {
                return;
            }
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.StartGame;
            mainPack.Returncode = ReturnCode.EndMatchGame;
            mainPack.Gamepack = new GamePack();
            mainPack.Gamepack.GameModel = GameModel.Double;
            mainPack.Gamepack.Player = toClient.GetUserData.UserInfo;
            selfClient.Send(mainPack);
            mainPack.Gamepack.Player = selfClient.GetUserData.UserInfo;
            toClient.Send(mainPack);
            lock (_lockMatchClient)
            {
                timers.Remove(selfClient.GetUserData.UserInfo.Username);
                timers.Remove(toClient.GetUserData.UserInfo.Username);
                if (matchClients.ContainsKey(selfClient.GetUserData.UserInfo.Username))
                {
                    matchClients.Remove(selfClient.GetUserData.UserInfo.Username);
                }
                if (matchClients.ContainsKey(toClient.GetUserData.UserInfo.Username))
                {
                    matchClients.Remove(toClient.GetUserData.UserInfo.Username);
                }
                matchOKClients.GetOrAdd(selfClient.GetUserData.UserInfo.Username, toClient);
                matchOKClients.GetOrAdd(toClient.GetUserData.UserInfo.Username, selfClient);
            }
        }

        private void OnMatchClientDispose(Client client)
        {
            lock (_lockMatchClient)
            {
                if (client.GetUserData != null && client.GetUserData.UserInfo != null && timers.TryGetValue(client.GetUserData.UserInfo.Username, out var timer))
                {
                    timer.Stop();
                    timers.Remove(client.GetUserData.UserInfo.Username);
                }
            }
        }

        public MainPack StartGame(Server server, Client client, MainPack pack)
        {
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.StartGame;
            mainPack.Returncode = ReturnCode.Successed;
            mainPack.Gamepack = new GamePack();
            mainPack.Gamepack.GameModel = pack.Gamepack.GameModel;
            if (pack.Gamepack.GameModel == GameModel.RoomTeam)
            {
                if (pack.Roompack.Count > 0)
                {
                    Room room = server.QueryRoom(pack.Roompack[0].RoomID);
                    if (room != null)
                    {
                        room.GetRoomInfo.Status = 3;
                        mainPack.Roompack.Add(room.GetRoomInfo);
                        room.NoticeClient(mainPack, client);
                    }
                    else
                    {
                        room.GetRoomInfo.Status = room.GetRoomInfo.PlayerList.Count < room.GetRoomInfo.Maxnum ? 1 : 2;
                        mainPack.Returncode = ReturnCode.NotFindRoom;
                    }
                }
                else
                {
                    mainPack.Returncode = ReturnCode.NotRoom;
                }
            }
            else if (pack.Gamepack.GameModel == GameModel.Double)
            {
                lock (_lockMatchClient)
                {
                    if (!matchClients.ContainsKey(client.GetUserData.UserInfo.Username))
                    {
                        matchClients.Add(client.GetUserData.UserInfo.Username, client);
                    }
                    if (timers.ContainsKey(client.GetUserData.UserInfo.Username))
                    {
                        timers[client.GetUserData.UserInfo.Username].Stop();
                        timers[client.GetUserData.UserInfo.Username] = new CountdownTimer<Client>(20, 1, RunMatchGameDoubleModel, EndMatchGameDoubleModel);
                    }
                    else
                    {
                        timers.Add(client.GetUserData.UserInfo.Username, new CountdownTimer<Client>(20, 1, RunMatchGameDoubleModel, EndMatchGameDoubleModel));
                    }
                    client.onDisposeEvent -= OnMatchClientDispose;
                    client.onDisposeEvent += OnMatchClientDispose;
                    timers[client.GetUserData.UserInfo.Username].Start(client);
                    mainPack.Returncode = ReturnCode.StartMatchGame;
                }
            }
            return mainPack;
        }

        public MainPack QuitMatch(Server server, Client client, MainPack pack)
        {
            MainPack mainPack = new MainPack();
            if (pack.Actioncode == ActionCode.QuitMatch)
            {
                mainPack.Actioncode = ActionCode.QuitMatch;
                if (pack.Gamepack == null || pack.Gamepack.Player == null)
                {
                    mainPack.Returncode = ReturnCode.Fail;
                    return mainPack;
                }
                lock (_lockMatchClient)
                {
                    if (timers.TryGetValue(pack.Gamepack.Player.Username, out var timer))
                    {
                        timer.Stop();
                        timers.Remove(pack.Gamepack.Player.Username);
                    }
                    if (matchClients.ContainsKey(pack.Gamepack.Player.Username))
                    {
                        matchClients.Remove(pack.Gamepack.Player.Username);
                    }
                }
                mainPack.Returncode = ReturnCode.Successed;
            }
            else
            {
                mainPack.Actioncode = ActionCode.ActionNone;
                mainPack.Returncode = ReturnCode.Fail;
            }
            return mainPack;
        }

        //public MainPack UpdatePlayer(Server server, Client client, MainPack pack)
        //{
        //    MainPack mainPack = new MainPack();
        //    mainPack.Actioncode = ActionCode.UpdatePlayer;
        //    mainPack.Gamepack = new GamePack();
        //    mainPack.Gamepack.GameModel = pack.Gamepack.GameModel;
        //    if (pack.Gamepack.GameModel == GameModel.RoomTeam)
        //    {
        //        Room room = server.QueryRoom(pack.Roompack[0].RoomID);
        //        if (room == null)
        //        {
        //            mainPack.Returncode = ReturnCode.NotFindRoom;
        //            return mainPack;
        //        }
        //        mainPack.Returncode = ReturnCode.Successed;
        //        mainPack.Gamepack.Player = pack.Gamepack.Player;
        //        room.NoticeClient(mainPack, client);
        //    }
        //    else if (pack.Gamepack.GameModel == GameModel.Double)
        //    {
        //        if (!matchOKClients.TryGetValue(pack.Gamepack.Player.Username, out Client toClient))
        //        {
        //            mainPack.Returncode = ReturnCode.GameError;
        //            return mainPack;
        //        }
        //        mainPack.Gamepack.Player = pack.Gamepack.Player;
        //        mainPack.Returncode = ReturnCode.Successed;
        //        toClient.Send(mainPack);
        //    }
        //    else
        //    {
        //        mainPack.Returncode = ReturnCode.Successed;
        //    }
        //    return null;
        //}
        public MainPack UpdatePlayer(Client client, MainPack pack)
        {
            MainPack mainPack = new MainPack();
            mainPack.Actioncode = ActionCode.UpdatePlayer;
            mainPack.Gamepack = new GamePack();
            mainPack.Gamepack.GameModel = pack.Gamepack.GameModel;
            if (pack.Gamepack.GameModel == GameModel.RoomTeam)
            {
                Room room = client.Room;
                if (room == null)
                {
                    mainPack.Returncode = ReturnCode.NotFindRoom;
                    return mainPack;
                }
                mainPack.Returncode = ReturnCode.Successed;
                mainPack.Gamepack.Player = pack.Gamepack.Player;
                room.NoticeClientToUDP(mainPack, client);
            }
            else if (pack.Gamepack.GameModel == GameModel.Double)
            {
                if (!matchOKClients.TryGetValue(pack.Gamepack.Player.Username, out Client toClient))
                {
                    mainPack.Returncode = ReturnCode.GameError;
                    return mainPack;
                }
                mainPack.Gamepack.Player = pack.Gamepack.Player;
                mainPack.Returncode = ReturnCode.Successed;
                toClient.SendTo(mainPack);
            }
            else
            {
                mainPack.Returncode = ReturnCode.Successed;
            }
            return null;
        }

        private bool CheckAllClientIsEnterGameScene(RepeatedField<PlayerData> allPlayer)
        {
            if (allPlayer.Count == 0) return false;
            lock (_lockEnterGameWaiting)
            {
                foreach (PlayerData player in allPlayer)
                {
                    if (!isEnterGameWaiting.ContainsKey(player.Username))
                    {
                        return false;
                    }
                    if (!isEnterGameWaiting[player.Username])
                    {
                        return false;
                    }
                }
                return true;
            }
        }
        public MainPack EnterGameSceneRquestStart(Server server, Client client, MainPack pack)
        {
            lock (isEnterGameWaiting)
            {
                if (!isEnterGameWaiting.ContainsKey(client.GetUserData.UserInfo.Username))
                {
                    isEnterGameWaiting.Add(client.GetUserData.UserInfo.Username, true);
                }
                MainPack mainPack = new MainPack();
                mainPack.Actioncode = ActionCode.EnterGameSceneRquestStart;
                try
                {
                    if (pack.Gamepack.GameModel == GameModel.Double)
                    {
                        if (matchOKClients.TryGetValue(client.GetUserData.UserInfo.Username, out Client toClient))
                        {
                            bool isCanRuning = isEnterGameWaiting.ContainsKey(toClient.GetUserData.UserInfo.Username) ? isEnterGameWaiting[toClient.GetUserData.UserInfo.Username] : false;
                            if (isCanRuning)
                            {
                                mainPack.Gamepack = new GamePack()
                                {
                                    GameModel = GameModel.Double,
                                };
                                mainPack.Returncode = ReturnCode.Successed;
                                toClient.Send(mainPack);
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            mainPack.Returncode = ReturnCode.ReturnNone;
                        }
                    }
                    else if (pack.Gamepack.GameModel == GameModel.RoomTeam)
                    {
                        Room room = server.QueryRoom(pack.Roompack[0].RoomID);
                        if (room != null)
                        {
                            if (CheckAllClientIsEnterGameScene(room.GetRoomInfo.PlayerList))
                            {
                                mainPack.Gamepack = new GamePack()
                                {
                                    GameModel = GameModel.RoomTeam
                                };
                                mainPack.Roompack.Add(new RoomPack()
                                {
                                    RoomID = pack.Roompack[0].RoomID,
                                    Roomname = room.GetRoomInfo.Roomname,
                                    RoomMaster = room.GetRoomInfo.RoomMaster,
                                });
                                for (int i = 0; i < room.GetRoomInfo.PlayerList.Count; i++)
                                {
                                    mainPack.Roompack[0].PlayerList.Add(room.GetRoomInfo.PlayerList[i]);
                                }
                                mainPack.Returncode = ReturnCode.Successed;
                                room.NoticeClient(mainPack, client);
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            mainPack.Returncode = ReturnCode.RoomIsFull;
                        }
                    }
                    else
                    {
                        mainPack.Returncode = ReturnCode.Successed;
                    }
                }
                catch (Exception)
                {
                    mainPack.Returncode = ReturnCode.GameError;
                }
                return mainPack;
            }
        }
    }
}
