using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketMultiplayerGameServer
{
    public interface ICacheItem
    {
        string Id { get; }
        void Release();
    }

    public class CacheItem<T> : ICacheItem
    {
        private T _data;
        public string Id { get; private set; }

        public CacheItem(string id, T data)
        {
            Id = id;
            _data = data;
        }

        public T GetData()
        {
            return _data;
        }

        public void Release()
        {
            // 释放资源
            if (_data is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _data = default(T);
            //Console.WriteLine($"Released cache item: {Id}");
        }
    }

    public class CacheManager
    {
        private static readonly Lazy<CacheManager> _instance = new Lazy<CacheManager>(() => new CacheManager());
        public static CacheManager Instance => _instance.Value;

        private readonly Dictionary<string, WeakReference<ICacheItem>> _cache;
        private Timer _cleanupTimer;

        private CacheManager()
        {
            _cache = new Dictionary<string, WeakReference<ICacheItem>>();
            // 每30秒检查一次缓存
            _cleanupTimer = new Timer(CleanupCache, null, TimeSpan.Zero, TimeSpan.FromSeconds(CONFIG.CACHES_TIMEOUT));
        }

        public void Add(string key, ICacheItem item)
        {
            lock (_cache)
            {
                _cache[key] = new WeakReference<ICacheItem>(item);
            }
            //Console.WriteLine($"Added to cache: {key}");
        }

        public bool TryGet<T>(string key, out T item) where T : class, ICacheItem
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out WeakReference<ICacheItem> weakRef) &&
                    weakRef.TryGetTarget(out ICacheItem cacheItem))
                {
                    item = cacheItem as T;
                    return item != null;
                }
            }

            item = null;
            return false;
        }

        public void CleanupCache(object state)
        {
            lock (_cache)
            {
                List<string> keysToRemove = new List<string>();

                foreach (var kvp in _cache)
                {
                    // 如果弱引用不再指向活动对象，或者对象没有被其他强引用
                    if (!kvp.Value.TryGetTarget(out ICacheItem _))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    if (_cache.TryGetValue(key, out WeakReference<ICacheItem> weakRef))
                    {
                        _cache.Remove(key);
                        if (weakRef.TryGetTarget(out ICacheItem item))
                        {
                            item.Release();
                        }
                    }
                    //Console.WriteLine($"Removed from cache: {key}");
                }

                //Console.WriteLine($"Cache cleanup completed. Removed {keysToRemove.Count} items.");
            }
        }

        public int Count => _cache.Count;

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            lock (_cache)
            {
                foreach (var weakRef in _cache.Values)
                {
                    if (weakRef.TryGetTarget(out ICacheItem item))
                    {
                        item.Release();
                    }
                }
                _cache.Clear();
            }
        }
    }

    /* 使用示例
    public class ExampleUsage
    {
        public static void Demo()
        {
            // 创建缓存项
            var cacheItem1 = new CacheItem<string>("user_1", "User Data 1");
            var cacheItem2 = new CacheItem<string>("user_2", "User Data 2");

            // 添加到缓存
            CacheManager.Instance.Add("user_1", cacheItem1);
            CacheManager.Instance.Add("user_2", cacheItem2);

            // 获取缓存项
            if (CacheManager.Instance.TryGet("user_1", out CacheItem<string> user1))
            {
                Console.WriteLine($"Retrieved: {user1.GetData()}");
            }

            // 释放强引用
            cacheItem1 = null;

            // 强制垃圾回收（在实际应用中通常不需要手动调用）
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 等待清理周期
            Thread.Sleep(35000);

            // 尝试获取已释放的缓存项
            if (!CacheManager.Instance.TryGet("user_1", out CacheItem<string> _))
            {
                Console.WriteLine("user_1 has been released from cache");
            }
        }
    }*/
}
