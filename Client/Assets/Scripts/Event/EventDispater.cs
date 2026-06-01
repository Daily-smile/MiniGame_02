using System;
using System.Collections.Generic;
using System.Diagnostics;
//返回true表示删除
public delegate bool EventCallback(params object[] userInfo);
/// <summary>
/// 分发器
/// </summary>
public interface IDispatcher
{
    void Clear();
}
/// <summary>
/// 观察者信息
/// </summary>
public interface IObserverInfo
{
    string ToString();
    object GetObserver();
    object GetTarget();
    bool HasRemoved();
}
public class EventDispatcher<TEventNameType> : IDispatcher
{
    struct ObserverInfo : IObserverInfo
    {
        public TEventNameType eventName;
        public EventCallback callback;
        public object observer;
        public object target;
        public bool hasRemoved;
        /// <summary>
        /// 是否相同
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(ObserverInfo left, ObserverInfo right)
        {
            return left.Equals(right);
        }
        /// <summary>
        /// 是否不相同
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(ObserverInfo left, ObserverInfo right)
        {
            return !left.Equals(right);
        }
        /// <summary>
        /// 是否相同
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is ObserverInfo)
            {
                return Equals((ObserverInfo)obj);
            }
            return false;
        }
        /// <summary>
        /// 是否相同
        /// </summary>
        /// <param name="obs"></param>
        /// <returns></returns>
        public bool Equals(ObserverInfo obs)
        {
            return obs.observer == observer && obs.eventName.Equals(eventName) && obs.target == target && (obs.hasRemoved == hasRemoved);
        }
        /// <summary>
        /// 观察者对象是否一致
        /// </summary>
        /// <param name="obs"></param>
        /// <returns></returns>
        public bool IsObserverConsistent(ObserverInfo obs)
        {
            return obs.observer == observer && obs.eventName.Equals(eventName) && obs.target == target;
        }
        /// <summary>
        /// hash
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        /// <summary>
        /// 信息
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("Observer : {0}, Target : {1}, Event : {2}, Callback : {3}", observer, target, eventName, callback);
        }
        /// <summary>
        /// 观察者
        /// </summary>
        /// <returns></returns>
        public object GetObserver() { return observer; }
        /// <summary>
        /// 目标
        /// </summary>
        /// <returns></returns>
        public object GetTarget() { return target; }
        /// <summary>
        /// 移除
        /// </summary>
        /// <returns></returns>
        public bool HasRemoved() { return hasRemoved; }
    }
    /// <summary>
    /// 观察者列表
    /// </summary>
    private readonly Dictionary<TEventNameType, List<ObserverInfo>> observersList = new Dictionary<TEventNameType, List<ObserverInfo>>(1024);
    /// <summary>
    /// 添加观察者
    /// </summary>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">观察者观察事件</param>
    /// <param name="callback"></param>
    /// <param name="target">观察目标, 如果为null 则接收广播</param>
    public void AddObserver(object observer, TEventNameType eventName, EventCallback callback, object target)
    {
        List<ObserverInfo> list;
        if (!observersList.TryGetValue(eventName, out list))
        {
            list = new List<ObserverInfo>();
            observersList.Add(eventName, list);
        }
        var obs = new ObserverInfo()
        {
            observer = observer,
            callback = callback,
            eventName = eventName,
            target = target,
            hasRemoved = false,
        };
        for (int i = list.Count - 1; i >= 0; --i)
        {
            //存在同样的回调则不进行添加
            if (list[i].IsObserverConsistent(obs) && !list[i].hasRemoved)
            {
                list[i] = obs;
                return;
            }
        }
        list.Add(obs);
    }
    /// <summary>
    /// 是否存在该观察者
    /// </summary>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">观察者观察事件</param>
    /// <param name="target">观察目标, 如果为null 则接收广播</param>
    /// <returns></returns>
    public bool HasObserver(object observer, TEventNameType eventName, object target)
    {
        List<ObserverInfo> list;
        if (observersList.TryGetValue(eventName, out list))
        {
            return list.Contains(new ObserverInfo() { observer = observer, eventName = eventName, target = target });
        }
        return false;
    }
    /// <summary>
    /// 分发事件
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="sender">发送者</param>
    /// <param name="userInfo">附加参数</param>
    public void PostEvent(TEventNameType eventName, object sender, params object[] userInfo)
    {
        List<ObserverInfo> list;
        if (!observersList.TryGetValue(eventName, out list))
        {
            return;
        }
        int length = list.Count;
        //顺序触发
        for (int i = 0; i < length; ++i)
        {
            //fixed unknown
            if (i >= list.Count)
            {
                continue;
            }
            ObserverInfo obs = list[i];
            if (obs.hasRemoved)
            {
                continue;
            }
            if (obs.target == null || obs.target == sender)
            {
                try
                {
                    //#if UNITY_EDITOR
                    //                       GameProfiler.Start(eventName.ToString(), string.Format("消息回调时间过长 : {0}, 请确定是否需要优化", eventName));
                    //#endif
                    //观察者不存在, 或者是回调时候返回移除
                    if (obs.callback(userInfo))
                    {
                        obs.hasRemoved = true;
                    }
                    //#if UNITY_EDITOR
                    //                       GameProfiler.End(eventName.ToString());
                    //#endif
                }
                catch (Exception e)
                {
                    //LoggerInternal.LogException(ex);
                    //LoggerInternal.LogErrorFormat("事件分发时遇到异常情况, 现在移除该事件监听者. {0}", obs);
                    obs.hasRemoved = true;
                    UnityEngine.Debug.LogError(eventName.ToString() + "<<<" + e.Message);
                }
                if (i < list.Count)
                {
                    list[i] = obs;
                }
            }
        }
        //反序删除
        for (int i = list.Count - 1; i >= 0; --i)
        {
            if (list[i].hasRemoved)
            {
                list.RemoveAt(i);
            }
        }
        //保留注释
        ////反序触发, 反序删除
        //for (int i = length - 1; i >= 0; --i)
        //{
        //    ObserverInfo obs = list[i];
        //    if (obs.target == null || obs.target == sender)
        //    {
        //        try
        //        {
        //            //观察者不存在, 或者是回调时候返回移除
        //            if ((obs.observer == null) || obs.callback(userInfo))
        //            {
        //                list.RemoveAt(i);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.LogException(ex);
        //            Logger.LogErrorFormat("事件分发时遇到异常情况, 现在移除该事件监听者. {0}", obs);
        //            list.RemoveAt(i);
        //        }
        //    }
        //}
    }
    /// <summary>
    /// 移除指定观察者事件
    /// </summary>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">事件名</param>
    /// <param name="target">观察对象</param>
    /// <returns></returns>
    public bool RemoveObserver(object observer, TEventNameType eventName, object target)
    {
        List<ObserverInfo> list;
        if (observersList.TryGetValue(eventName, out list))
        {
            int count = list.Count;
            var obs = new ObserverInfo() { observer = observer, eventName = eventName, target = target };
            for (int i = 0; i < count; ++i)
            {
                if (list[i].IsObserverConsistent(obs))
                {
                    ObserverInfo info = list[i];
                    info.hasRemoved = true;
                    list[i] = info;
                }
            }
            return true;
        }
        return false;
    }
    /// <summary>
    /// 移除观察指定事件的所有观察者
    /// </summary>
    /// <param name="eventName"></param>
    /// <returns></returns>
    public bool RemoveObserver(TEventNameType eventName)
    {
        return observersList.Remove(eventName);
    }
    /// <summary>
    /// 移除指定观察者
    /// </summary>
    /// <param name="observer">已经注册的观察者对象</param>
    /// <returns></returns>
    public void RemoveObserver(object observer)
    {
        foreach (var item in observersList.Values)
        {
            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].observer == observer)
                {
                    var info = item[i];
                    info.hasRemoved = true;
                    item[i] = info;
                }
            }
        }
    }
    /// <summary>
    /// 移除指定观察者观察对象的事件
    /// </summary>
    /// <param name="target">观察者观察的对象</param>
    /// <returns></returns>
    public void RemoveObserverByTarget(object target)
    {
        foreach (var item in observersList.Values)
        {
            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].target == target)
                {
                    var info = item[i];
                    info.hasRemoved = true;
                    item[i] = info;
                }
            }
        }
    }
    /// <summary>
    /// 操作容器,
    /// 移除废弃对象
    /// </summary>
    public void ClearObsolete()
    {
        foreach (var list in observersList.Values)
        {
            //反序删除
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (list[i].hasRemoved)
                {
                    list.RemoveAt(i);
                }
            }
        }
    }
    /// <summary>
    /// 操作容器,
    /// 移除废弃对象
    /// </summary>
    /// <param name="eventName"></param>
    public void ClearObsolete(TEventNameType eventName)
    {
        List<ObserverInfo> list;
        if (!observersList.TryGetValue(eventName, out list))
        {
            return;
        }
        //反序删除
        for (int i = list.Count - 1; i >= 0; --i)
        {
            if (list[i].hasRemoved)
            {
                list.RemoveAt(i);
            }
        }
    }
    /// <summary>
    /// 清除
    /// </summary>
    public void Clear()
    {
        observersList.Clear();
    }
    /// <summary>
    /// 内部方法, 仅用于辅助消息跟踪以及查询
    /// 禁止用于移除, 新增等逻辑行为
    /// </summary>
    /// <returns></returns>
    //internal Dictionary<TEventNameType, List<ObserverInfo>> GetObserversList() { return observersList; }
    /// <summary>
    /// 获取分发器的所有事件名称
    /// </summary>
    /// <returns></returns>
    internal TEventNameType[] GetEvents()
    {
        TEventNameType[] events = new TEventNameType[observersList.Keys.Count];
        int idx = 0;
        foreach (var item in observersList.Keys)
        {
            events[idx++] = item;
        }
        return events;
    }
    /// <summary>
    /// 获取指定事件的所有观察者信息
    /// </summary>
    /// <param name="eventName">事件定义</param>
    /// <returns></returns>
    internal List<IObserverInfo> GetObservers(TEventNameType eventName)
    {
        List<ObserverInfo> infos;
        if (observersList.TryGetValue(eventName, out infos))
        {
            List<IObserverInfo> ret = new List<IObserverInfo>(infos.Count);
            infos.ForEach(info => ret.Add(info));
            return ret;
        }
        return null;
    }
}
/// <summary>
/// 具体应用的事件分发器
/// </summary>
public class EventDispatcher
{
    /// <summary>
    /// 分发器
    /// </summary>
    private static readonly Dictionary<string, IDispatcher> dispatchers = new Dictionary<string, IDispatcher>();
    /// <summary>
    /// 分发器数量
    /// </summary>
    /// <returns></returns>
    public static int GetDispatcherCount() { return dispatchers.Count; }
    /// <summary>
    /// 获取指定分发器类型的所有事件
    /// </summary>
    /// <returns></returns>
    public static TEventNameType[] GetEventNames<TEventNameType>()
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (dispatchers.TryGetValue(name, out dispatcher))
        {
            return ((EventDispatcher<TEventNameType>)(dispatcher)).GetEvents();
        }
        return null;
    }
    /// <summary>
    /// 尝试获取分发器事件的所有观察者信息
    /// </summary>
    /// <returns></returns>
    public static bool TryGetDispatcherEvents<TEventNameType>(TEventNameType eventName, out List<IObserverInfo> observers)
    {
        observers = null;
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return false;
        }
        observers = ((EventDispatcher<TEventNameType>)(dispatcher)).GetObservers(eventName);
        return observers != null && observers.Count > 0;
    }
    /// <summary>
    /// 操作容器,
    /// 移除废弃对象
    /// </summary>
    public static void ClearObsolete<TEventNameType>()
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return;
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).ClearObsolete();
    }
    /// <summary>
    /// 添加观察者
    /// </summary>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">观察者观察事件</param>
    /// <param name="callback"></param>
    /// <param name="target">观察目标, 如果为null 则接收广播</param>
    public static void AddObserver<TEventNameType>(object observer, TEventNameType eventName, EventCallback callback, object target)
    {
#if UNITY_EDITOR
        if (observer != null)
        {
            Type t = observer.GetType();
            if (!t.IsClass)
            {
                //LoggerInternal.LogError("只支持class使用EventDispatcher");
                return;
            }
        }
        if (target != null)
        {
            Type t = target.GetType();
            if (!t.IsClass)
            {
                //LoggerInternal.LogError("只支持class使用EventDispatcher");
                return;
            }
        }
#endif
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            dispatcher = new EventDispatcher<TEventNameType>();
            dispatchers.Add(name, dispatcher);
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).AddObserver(observer, eventName, callback, target);
    }
    /// <summary>
    /// 是否存在该观察者
    /// </summary>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">观察者观察事件</param>
    /// <param name="target">观察目标, 如果为null 则接收广播</param>
    /// <returns></returns>
    public static bool HasObserver<TEventNameType>(object observer, TEventNameType eventName, object target)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return false;
        }
        return ((EventDispatcher<TEventNameType>)(dispatcher)).HasObserver(observer, eventName, target);
    }
    /// <summary>
    /// 分发事件
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="sender">发送者</param>
    /// <param name="userInfo">附加参数</param>
    public static void PostEvent<TEventNameType>(TEventNameType eventName, object sender, params object[] userInfo)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return;
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).PostEvent(eventName, sender, userInfo);
    }
    /// <summary>
    /// 移除观察者
    /// </summary>
    /// <typeparam name="TEventNameType">事件类型</typeparam>
    /// <param name="observer">观察者</param>
    /// <param name="eventName">事件名</param>
    /// <param name="target">观察对象</param>
    /// <returns></returns>
    public static bool RemoveObserver<TEventNameType>(object observer, TEventNameType eventName, object target)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return false;
        }
        return ((EventDispatcher<TEventNameType>)(dispatcher)).RemoveObserver(observer, eventName, target);
    }
    /// <summary>
    /// 移除观察指定事件的所有观察者
    /// </summary>
    /// <param name="eventName"></param>
    /// <returns></returns>
    public static void RemoveObserver<TEventNameType>(TEventNameType eventName)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return;
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).RemoveObserver(eventName);
    }
    /// <summary>
    /// 移除指定观察者
    /// </summary>
    /// <param name="observer">已经注册的观察者对象</param>
    /// <returns></returns>
    public static void RemoveObserver<TEventNameType>(object observer)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return;
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).RemoveObserver(observer);
    }
    /// <summary>
    /// 移除指定观察者观察对象的事件
    /// </summary>
    /// <param name="target">观察者观察的对象</param>
    /// <returns></returns>
    public void RemoveObserverByTarget<TEventNameType>(object target)
    {
        string name = typeof(TEventNameType).Name;
        IDispatcher dispatcher;
        if (!dispatchers.TryGetValue(name, out dispatcher))
        {
            return;
        }
        ((EventDispatcher<TEventNameType>)(dispatcher)).RemoveObserverByTarget(target);
    }
    /// <summary>
    /// 清空所有观察者
    /// </summary>
    public static void Clear()
    {
        foreach (var dispatcher in dispatchers.Values)
        {
            dispatcher.Clear();
        }
    }
}
