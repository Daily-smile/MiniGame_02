using SocketGameProtocol;
using SocketMultiplayerGameServer.Servers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer.Controller
{
    class RoomController : BaseController
    {
        public RoomController() 
        {
            requestCode = RequestCode.Room;
        }

        public MainPack CreateRoom(Server server, Client client, MainPack pack)
        {
            return server.CreateRoom(client, pack);
        }

        public MainPack FindRoom(Server server, Client client, MainPack pack)
        {
            return server.FindRoom(pack.Queryroom);
        }

        public MainPack JoinRoom(Server server, Client client, MainPack pack)
        {
            return server.JoinRoom(pack.Queryroom.RoomID, client);
        }

        public MainPack QuitRoom(Server server, Client client, MainPack pack)
        {
            return server.QuitRoom(pack.Queryroom.RoomID, pack.IsNotReturn, client);
        }

        public MainPack DestroyRoom(Server server, Client client, MainPack pack)
        {
            return server.DestroyRoom(pack.Queryroom.RoomID, client);
        }

        public MainPack SwitchRoomMaster(Server server, Client client, MainPack pack)
        {
            return server.SwitchRoomMaster(pack.Roompack[0], client);
        }

        public MainPack KickoutRoom(Server server, Client client, MainPack pack)
        {
            return server.KickoutRoom(pack.Roompack[0], client);
        }

        public MainPack RoomMessageUpdate(Server server, Client client, MainPack pack)
        {
            return server.HandleChatRoomMsg(client, pack);
        }
    }
}
