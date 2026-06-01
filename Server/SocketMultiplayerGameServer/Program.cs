using SocketMultiplayerGameServer.Database;
using SocketMultiplayerGameServer.Servers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer
{
    class Program
    {
        private static Server server;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            Console.CancelKeyPress += Console_CancelKeyPress;

            try
            {
                Logger.Info("正在启动服务器...");
                // 启动连接池状态监控
                StartConnectionPoolMonitor();
                server = new Server(6666);
                Logger.Info("服务器启动成功");

                // 保持主线程运行
                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("服务器启动失败", ex);
            }
        }

        private static void StartConnectionPoolMonitor()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(30000); // 每30秒检查一次

                    var status = DatabaseManager.Instance.GetPoolStatus();
                    Logger.Info($"数据库连接池状态: 总连接数={status.TotalConnections}, 可用连接数={status.AvailableConnections}");

                    // 如果可用连接数过少，可以记录警告
                    if (status.AvailableConnections < 2)
                    {
                        Logger.Warn("数据库连接池可用连接数不足，考虑增加最大连接数");
                    }
                }
            });
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("未处理的异常", e.ExceptionObject as Exception);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            ShutdownServer();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // 防止立即退出
            ShutdownServer();
            Environment.Exit(0);
        }

        private static void ShutdownServer()
        {
            Logger.Info("正在关闭服务器...");
            server?.Dispose();
            Logger.Info("服务器已关闭");
        }
    }
}