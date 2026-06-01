using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SocketGameProtocol;
using Google.Protobuf;

namespace SocketMultiplayerGameServer.Tool
{
    class Message : IDisposable
    {
        private byte[] buffer;
        private int startIndex;
        private bool disposed = false;

        public Message()
        {
            buffer = ArrayPool<byte>.Shared.Rent(CONFIG.INITIAL_BUFFER_SIZE);
        }

        public byte[] Buffer
        {
            get
            {
                return buffer;
            }
        }

        public int StartIndex => startIndex;

        public int Remsize => buffer.Length - startIndex;

        /// <summary>
        /// 确保缓冲区有足够空间
        /// </summary>
        /// <param name="additionalLength">需要额外空间</param>
        public void EnsureCapacity(int additionalLength)
        {
            if (Remsize >= additionalLength)
                return;

            int requiredSize = startIndex + additionalLength;
            if (requiredSize > CONFIG.MAX_BUFFER_SIZE)
            {
                throw new InvalidOperationException($"消息过大，超过最大限制: {CONFIG.MAX_BUFFER_SIZE}字节");
            }

            // 计算新缓冲区大小(按2的指数增长)
            int newSize = buffer.Length;
            while (newSize < requiredSize)
            {
                newSize *= 2;
                if (newSize > CONFIG.MAX_BUFFER_SIZE)
                {
                    newSize = CONFIG.MAX_BUFFER_SIZE;
                    break;
                }
            }

            // 租用新缓冲区并复制数据
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            Array.Copy(buffer, 0, newBuffer, 0, startIndex);

            // 归还旧缓冲区
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }

        public void ReadBuffer(int len, Action<MainPack> HandleRequest)
        {
            if (len <= 0)
                return;

            startIndex += len;

            // 小于等于包头大小，数据不完整
            if (startIndex <= 4)
                return;

            try
            {
                // 使用一个变量来跟踪已处理的数据量
                int processed = 0;

                while (processed < startIndex)
                {
                    // 检查剩余数据是否足够读取包头
                    if (processed + 4 > startIndex)
                        break;

                    // 读取消息长度(使用大端字节序转换)
                    int count = ReadInt32BigEndian(buffer, processed);

                    // 检查消息长度有效性
                    if (count <= 0 || count > CONFIG.MAX_BUFFER_SIZE)
                    {
                        // 无效的消息长度，清空缓冲区并重置
                        startIndex = 0;
                        throw new ProtocolViolationException($"无效的消息长度: {count}");
                    }

                    // 检查是否收到完整消息
                    if (processed + 4 + count > startIndex)
                    {
                        // 数据不完整，退出循环
                        break;
                    }

                    // 解析消息
                    MainPack pack = (MainPack)MainPack.Descriptor.Parser.ParseFrom(buffer, processed + 4, count);
                    HandleRequest(pack);

                    // 更新已处理的数据量
                    processed += (4 + count);
                }

                // 移动剩余未处理的数据到缓冲区开头
                if (processed > 0)
                {
                    int remaining = startIndex - processed;
                    if (remaining > 0)
                    {
                        Array.Copy(buffer, processed, buffer, 0, remaining);
                    }
                    startIndex = remaining;
                }
            }
            catch (Exception ex)
            {
                // 解析过程中出现异常，重置缓冲区
                startIndex = 0;
                // 记录日志或处理异常
                Console.WriteLine($"消息解析错误: {ex.Message}");
                throw;
            }
        }

        public static Byte[] PackDataUDP(MainPack pack)
        {
            return pack.ToByteArray();
        }

        public static byte[] PackData(MainPack pack)
        {
            byte[] data = pack.ToByteArray();
            byte[] head = BitConverter.GetBytes(data.Length);

            // 确保使用大端字节序(网络字节序)
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(head);
            }

            byte[] result = new byte[head.Length + data.Length];
            // 修复Buffer命名冲突 - 明确使用System.Buffer
            System.Buffer.BlockCopy(head, 0, result, 0, head.Length);
            System.Buffer.BlockCopy(data, 0, result, head.Length, data.Length);

            return result;
        }

        /// <summary>
        /// 以大端字节序读取32位整数
        /// </summary>
        private static int ReadInt32BigEndian(byte[] data, int startIndex)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (data[startIndex] << 24) | (data[startIndex + 1] << 16) |
                       (data[startIndex + 2] << 8) | data[startIndex + 3];
            }
            else
            {
                return BitConverter.ToInt32(data, startIndex);
            }
        }

        /// <summary>
        /// 重置缓冲区
        /// </summary>
        public void Reset()
        {
            startIndex = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (buffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = null;
                    }
                }
                disposed = true;
            }
        }

        ~Message()
        {
            Dispose(false);
        }
    }
}