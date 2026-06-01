using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer
{
    public static class Logger
    {
        public static void Info(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}");
        }

        public static void Error(string message, Exception ex = null)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}");
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        public static void Warn(string message, Exception ex = null)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARN: {message}");
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        public static void Debug(string message)
        {
#if DEBUG
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DEBUG: {message}");
#endif
        }
    }
}
