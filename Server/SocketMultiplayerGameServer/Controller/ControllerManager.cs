using SocketGameProtocol;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocketMultiplayerGameServer.Servers;

namespace SocketMultiplayerGameServer.Controller
{
    class ControllerManager
    {
        private Dictionary<RequestCode, BaseController> controlDict = new Dictionary<RequestCode, BaseController>();
        private Server server;

        public ControllerManager(Server server) 
        { 
            this.server = server;
            UserController userController = new UserController();
            controlDict.Add(userController.GetRequestCode, userController);
            RoomController roomController = new RoomController();
            controlDict.Add(roomController.GetRequestCode, roomController);
            GameController gameController = new GameController();
            controlDict.Add(gameController.GetRequestCode, gameController);
        }

        public void HandleRequest(MainPack pack, Client client, bool isUDP = false)
        {
            if (controlDict.TryGetValue(pack.Requestcode, out BaseController controller))
            {
                string metname = pack.Actioncode.ToString();
                MethodInfo method = controller.GetType().GetMethod(metname);
                if (method == null)
                {
                    Console.WriteLine("没有找到绑定的事件处理:" + pack.Actioncode.ToString());
                    return;
                }
                object[] obj = !isUDP ? new object[] { server, client, pack } : new object[] { client, pack };
                object ret = method.Invoke(controller, obj);
                if (ret != null && !isUDP)
                {
                    MainPack mainPack = ret as MainPack;
                    if (mainPack != null && !mainPack.IsNotReturn)
                    {
                        // 如果是登录或注册成功响应，添加SessionId
                        if ((pack.Actioncode == ActionCode.Login || pack.Actioncode == ActionCode.Logon) &&
                            mainPack.Returncode == ReturnCode.Successed)
                        {
                            mainPack.SessionId = client.SessionId;
                        }

                        client.Send(mainPack);
                    }
                }
            }
            else
            {
                Console.WriteLine("没有找到对应的controller处理<<<" + pack.Requestcode.ToString());
            }
        }
    }
}
