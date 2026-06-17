using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LF.Framework
{
public class ObjectPool : Singleton<ObjectPool>
{
    private const int DEFAULT_MAX_POOL_SIZE = 50;
    private readonly object lockPool = new object();
    Dictionary<string, Queue<GameObject>> pool = new Dictionary<string, Queue<GameObject>>();

    /// <summary>
    /// Normalize object name by removing "(Clone)" suffix from Unity instantiation
    /// </summary>
    private static string GetPoolKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.Replace("(Clone)", "");
    }

    /// <summary>
    /// 将游戏对象放回对象池
    /// </summary>
    public void PutInPool(GameObject obj)
    {
        if (obj == null) return;
        lock (lockPool)
        {
            string key = GetPoolKey(obj.name);
            if (pool.TryGetValue(key, out Queue<GameObject> queue))
            {
                if (queue.Count >= DEFAULT_MAX_POOL_SIZE)
                {
                    GameObject.Destroy(obj);
                    return;
                }
                queue.Enqueue(obj);
            }
            else
            {
                Queue<GameObject> newQueue = new Queue<GameObject>();
                newQueue.Enqueue(obj);
                pool[key] = newQueue;
            }
            obj.SetActive(false);
        }
    }

    /// <summary>
    /// 从对象池获取游戏对象
    /// </summary>
    public GameObject GetInPool(string name)
    {
        lock (lockPool)
        {
            string key = GetPoolKey(name);
            if (pool.TryGetValue(key, out Queue<GameObject> queue) && queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();
                if (obj != null)
                {
                    obj.SetActive(true);
                    return obj;
                }
            }
            return null;
        }
    }

    public void ClearPool(string name)
    {
        lock (lockPool)
        {
            string key = GetPoolKey(name);
            if (pool.TryGetValue(key, out Queue<GameObject> queue))
            {
                while (queue.Count > 0)
                {
                    GameObject obj = queue.Dequeue();
                    if (obj != null) GameObject.Destroy(obj);
                }
                pool.Remove(key);
            }
        }
    }

    /// <summary>
    /// 清空对象池
    /// </summary>
    public void ClearAllPool()
    {
        lock (lockPool)
        {
            foreach (var queue in pool.Values)
            {
                while (queue.Count > 0)
                {
                    GameObject obj = queue.Dequeue();
                    if (obj != null) GameObject.Destroy(obj);
                }
            }
            pool.Clear();
        }
    }
}
}