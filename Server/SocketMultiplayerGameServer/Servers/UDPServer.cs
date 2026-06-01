using SocketGameProtocol;
using SocketMultiplayerGameServer.Controller;
using SocketMultiplayerGameServer.Tool;
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
    class UDPServer : IDisposable
    {
        Socket udpServer;
        IPEndPoint bindEP; // 本地监听ip
        EndPoint remoteEP; // 远程ip

        Server server;

        ControllerManager controllerManager;

        Byte[] buffer = new byte[1024]; // 消息缓存

        Thread receiveThread; // 接收线程
        private bool _disposed = false;
        public UDPServer(int port, Server server, ControllerManager controllerManager)
        {
            this.server = server;
            this.controllerManager = controllerManager;
            this.udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.bindEP = new IPEndPoint(IPAddress.Any, port);
            this.remoteEP = bindEP;
            this.udpServer.Bind(this.bindEP);
            this.receiveThread = new Thread(ReceiveMsg);
            this.receiveThread.Start();
            Console.WriteLine("UDP服务已启动...");
        }

        private void ReceiveMsg()
        {
            while (true)
            {
                try
                {
                    int len = udpServer.ReceiveFrom(buffer, ref remoteEP);
                    MainPack pack = (MainPack)MainPack.Descriptor.Parser.ParseFrom(buffer, 0, len);
                    HandleRequest(pack, remoteEP);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                }
            }
        }

        private void HandleRequest(MainPack pack,  EndPoint ipEndPoint)
        {
            Client client = server.ClientFormSessionId(pack.SessionId);
            if (client == null)
            {
                return;
            }
            if (client.IEP == null)
            {
                client.IEP = ipEndPoint;
            }
            controllerManager.HandleRequest(pack, client, true);
        }

        public void SendTo(MainPack pack, EndPoint point)
        {
            byte[] buff = Message.PackDataUDP(pack);
            udpServer.SendTo(buff, buff.Length, SocketFlags.None, point);
        }

        ~UDPServer()
        {
            Dispose(false);
        }

        // 添加 Dispose 方法
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 释放托管资源
                if (receiveThread != null)
                {
                    receiveThread.Abort();
                    receiveThread = null;
                }

                if (udpServer != null)
                {
                    udpServer.Close();
                    udpServer.Dispose();
                    udpServer = null;
                }
            }

            // 释放非托管资源（如果有）

            _disposed = true;
        }
    }
}
